"""Resumable downloader (standard library only) for wheels and model weights.

Why not uv / huggingface_hub: on this network IPv6 routes to the CDNs crawl (about 30 kB/s) while IPv4 works,
and neither tool has an IPv4 switch.  This script forces IPv4, sends a generic user agent and never sends
tokens or cookies.  Files are fetched as 16 MB chunks by a few worker threads into a pre-allocated file; a small
JSON next to it records the finished chunks, so a run can be stopped and restarted at any time (also with a
different number of workers) without losing more than the chunks in flight.

Job file: one job per line, '#' comments allowed
    url  <url>  <dest file>
    hf   <repo id>  <dest dir>  [path prefix filter]      (whole Hugging Face repo, or only paths starting with prefix)
For hf jobs the SHA-256 of every LFS file is checked against the hash published by the Hub.

Usage:  python -I fetch.py <job file> <log file> [workers=4]
"""
import hashlib
import json
import os
import re
import socket
import sys
import threading
import time
import urllib.request

UA = "Mozilla/5.0 (compatible; fetch/1.0)"
CHUNK = 16 << 20
_orig_getaddrinfo = socket.getaddrinfo


def _ipv4_only(host, port, family=0, *args, **kwargs):
    return _orig_getaddrinfo(host, port, socket.AF_INET, *args, **kwargs)


socket.getaddrinfo = _ipv4_only

LOG = None
_log_lock = threading.Lock()


def log(msg):
    line = time.strftime("%H:%M:%S ") + msg
    with _log_lock:
        print(line, flush=True)
        if LOG:
            with open(LOG, "a", encoding="utf-8") as f:
                f.write(line + "\n")


def _retry(fn, what):
    """Small requests (API listing, size probe) also hit transient DNS / connection failures: retry them."""
    for attempt in range(1, 31):
        try:
            return fn()
        except Exception as e:
            log("  %s: %s (%s, attempt %d)" % (type(e).__name__, e, what, attempt))
            time.sleep(min(60, 3 * attempt))
    raise RuntimeError("giving up on " + what)


def http_json(url):
    def go():
        req = urllib.request.Request(url, headers={"User-Agent": UA})
        with urllib.request.urlopen(req, timeout=60) as r:
            return json.load(r)
    return _retry(go, url)


def remote_size(url):
    def go():
        req = urllib.request.Request(url, headers={"User-Agent": UA, "Range": "bytes=0-0"})
        with urllib.request.urlopen(req, timeout=60) as r:
            cr = r.headers.get("Content-Range") or ""
            return int(cr.rsplit("/", 1)[1]) if "/" in cr else None
    return _retry(go, url)


def _adopt_legacy(dest, size, fh, done):
    """Earlier versions of this script left <dest>.part / .partN / .partN-of-M files: reuse their bytes."""
    folder, base = os.path.split(dest)
    for name in sorted(os.listdir(folder)):
        m = re.fullmatch(re.escape(base) + r"\.part(?:(\d+)(?:-of-(\d+))?)?", name)
        if not m:
            continue
        path = os.path.join(folder, name)
        if m.group(1) is None:
            start = 0
        else:
            idx = int(m.group(1))
            if m.group(2):
                nseg = int(m.group(2))
            else:                       # ".partN" was used for one or two segments
                nseg = 2 if (idx == 1 or os.path.exists(os.path.join(folder, base + ".part1"))) else 1
            start = idx * -(-size // nseg)
        have = os.path.getsize(path)
        with open(path, "rb") as src:
            fh.seek(start)
            left = have
            while left > 0:
                buf = src.read(min(left, 8 << 20))
                if not buf:
                    break
                fh.write(buf)
                left -= len(buf)
        first = -(-start // CHUNK)
        last = (start + have) // CHUNK          # chunks [first, last) lie completely inside the adopted bytes
        done.update(range(first, last))
        os.remove(path)
        log("  adopted %s (%.0f MB)" % (name, have / 1e6))


def download(url, dest, expect_size=None, workers=4):
    os.makedirs(os.path.dirname(dest), exist_ok=True)
    if expect_size is None:
        expect_size = remote_size(url)
        if expect_size is None:
            raise RuntimeError("cannot determine the size of " + url)
    if os.path.exists(dest) and os.path.getsize(dest) == expect_size:
        return
    size = expect_size
    nchunks = max(1, -(-size // CHUNK))
    data, state = dest + ".chunks", dest + ".chunks.json"
    done = set()
    if os.path.exists(data) and os.path.exists(state):
        try:
            with open(state, encoding="utf-8") as f:
                st = json.load(f)
            if st.get("size") == size and st.get("chunk") == CHUNK:
                done = set(st["done"])
        except Exception:
            done = set()
    fh = open(data, "r+b" if os.path.exists(data) and done else "w+b")
    fh.truncate(size)
    lock = threading.Lock()

    def save_state():
        tmp = state + ".tmp"
        with open(tmp, "w", encoding="utf-8") as f:
            json.dump(dict(size=size, chunk=CHUNK, done=sorted(done)), f)
        os.replace(tmp, state)

    _adopt_legacy(dest, size, fh, done)
    fh.flush()
    save_state()
    todo = [i for i in range(nchunks) if i not in done]
    stats = {"got": 0, "t0": time.time(), "last": time.time()}
    errors = []

    def worker():
        while True:
            with lock:
                if not todo or errors:
                    return
                i = todo.pop(0)
            a, b = i * CHUNK, min(size, (i + 1) * CHUNK) - 1
            for attempt in range(1, 41):
                try:
                    req = urllib.request.Request(url, headers={"User-Agent": UA, "Range": "bytes=%d-%d" % (a, b)})
                    with urllib.request.urlopen(req, timeout=90) as r:
                        if r.status != 206 and not (r.status == 200 and a == 0 and b == size - 1):
                            raise RuntimeError("server did not honour the byte range (HTTP %s)" % r.status)
                        buf = r.read()
                    if len(buf) != b - a + 1:
                        raise RuntimeError("short read %d of %d bytes" % (len(buf), b - a + 1))
                    with lock:
                        fh.seek(a)
                        fh.write(buf)
                        fh.flush()
                        done.add(i)
                        save_state()
                        stats["got"] += len(buf)
                        now = time.time()
                        if now - stats["last"] > 120:
                            stats["last"] = now
                            log("  ... %s %.0f / %.0f MB (%.2f MB/s)" % (os.path.basename(dest), min(size, len(done) * CHUNK) / 1e6,
                                                                    size / 1e6, stats["got"] / 1e6 / (now - stats["t0"])))
                    break
                except Exception as e:               # timeouts, resets, HTTP errors
                    if attempt >= 3:
                        log("  %s: %s (chunk %d, attempt %d)" % (type(e).__name__, e, i, attempt))
                    time.sleep(min(60, 2 * attempt))
            else:
                errors.append(RuntimeError("giving up on chunk %d of %s" % (i, url)))
                return

    threads = [threading.Thread(target=worker, daemon=True) for _ in range(max(1, min(workers, len(todo))))]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    fh.close()
    if errors:
        raise errors[0]
    os.replace(data, dest)
    os.remove(state)
    if os.path.getsize(dest) != size:
        raise RuntimeError("size mismatch for %s" % dest)


def sha256_of(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        while True:
            chunk = f.read(8 << 20)
            if not chunk:
                break
            h.update(chunk)
    return h.hexdigest()


def hf_repo(repo, dest_dir, prefix="", workers=4):
    tree = http_json("https://huggingface.co/api/models/%s/tree/main?recursive=true" % repo)
    files = [f for f in tree if f.get("type") == "file" and f["path"].startswith(prefix)]
    files.sort(key=lambda f: f["size"])
    log("hf %s (%s*): %d files, %.2f GB" % (repo, prefix, len(files), sum(f["size"] for f in files) / 1e9))
    for f in files:
        dest = os.path.join(dest_dir, f["path"].replace("/", os.sep))
        if os.path.exists(dest) and os.path.getsize(dest) == f["size"]:
            continue
        t0 = time.time()
        download("https://huggingface.co/%s/resolve/main/%s" % (repo, f["path"]), dest, f["size"], workers)
        oid = (f.get("lfs") or {}).get("oid")
        if oid:
            got = sha256_of(dest)
            if got != oid:
                os.replace(dest, dest + ".bad")
                raise RuntimeError("SHA-256 mismatch for %s (%s != %s)" % (f["path"], got, oid))
        if f["size"] > 5e6:
            log("  done %s %.0f MB in %.0f s%s" % (f["path"], f["size"] / 1e6, time.time() - t0, ", sha256 ok" if oid else ""))


def single_instance(lock_path):
    """Two downloaders writing the same file corrupt it: hold an exclusive lock for the whole run."""
    import msvcrt
    fh = open(lock_path, "a+")
    try:
        fh.seek(0)
        msvcrt.locking(fh.fileno(), msvcrt.LK_NBLCK, 1)
    except OSError:
        raise SystemExit("another fetch.py is already running (lock %s)" % lock_path)
    return fh


def main():
    global LOG
    jobs_file, LOG = sys.argv[1], sys.argv[2]
    workers = int(sys.argv[3]) if len(sys.argv) > 3 else 4
    _lock = single_instance(os.path.abspath(LOG) + ".lock")   # one instance per job/log; jobs must not share destinations
    with open(jobs_file, encoding="utf-8") as f:
        jobs = [ln.split() for ln in f if ln.strip() and not ln.lstrip().startswith("#")]
    for job in jobs:
        t0 = time.time()
        if job[0] == "url":
            log("url %s" % job[1])
            download(job[1], job[2], None, workers)
            log("  done %s %.0f MB in %.0f s, sha256 %s" % (os.path.basename(job[2]), os.path.getsize(job[2]) / 1e6,
                                                         time.time() - t0, sha256_of(job[2])))
        elif job[0] == "hf":
            hf_repo(job[1], job[2], job[3] if len(job) > 3 else "", workers)
        else:
            raise SystemExit("bad job line: %r" % (job,))
    log("ALL DONE")


if __name__ == "__main__":
    main()
