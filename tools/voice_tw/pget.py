"""Parallel downloader for big wheels / weights (IPv4, HTTP range requests, sha256 check).

    python pget.py <dest_dir> <url> [<url> ...]            urls may end in  #sha256=<hex>
    python pget.py <dest_dir> --report pip_report.json     every download in a `pip install --dry-run --report` file

pip fetches one file at a time over one connection; on this shared, slow link that takes hours.
This fetches several files at once and splits large ones into ranges.
"""
import hashlib
import json
import os
import socket
import sys
import threading
import time
import urllib.parse
import urllib.request
from concurrent.futures import ThreadPoolExecutor

_orig = socket.getaddrinfo
socket.getaddrinfo = lambda *a, **k: [r for r in _orig(*a, **k) if r[0] == socket.AF_INET] or _orig(*a, **k)

UA = {"User-Agent": "Mozilla/5.0"}
PART = 16 * 1024 * 1024
FILES_AT_ONCE = 6
PARTS_AT_ONCE = 6
_lock = threading.Lock()
_done_bytes = 0


def _open(url, headers=None, timeout=60):
    return urllib.request.urlopen(urllib.request.Request(url, headers={**UA, **(headers or {})}), timeout=timeout)


def _fetch_range(url, start, end, path):
    global _done_bytes
    for attempt in range(8):
        try:
            with _open(url, {"Range": f"bytes={start}-{end}"}) as r, open(path, "r+b") as f:
                f.seek(start)
                pos = start
                while True:
                    buf = r.read(1 << 18)
                    if not buf:
                        break
                    f.write(buf)
                    pos += len(buf)
                    with _lock:
                        _done_bytes += len(buf)
                if pos == end + 1:
                    return
                start = pos                      # short read: continue from where it stopped
        except Exception as e:                    # noqa: BLE001
            time.sleep(1 + attempt)
    raise RuntimeError(f"range {start}-{end} failed for {url}")


def fetch(url, dest_dir, pool):
    global _done_bytes
    sha = None
    if "#sha256=" in url:
        url, sha = url.split("#sha256=")
    name = urllib.parse.unquote(os.path.basename(urllib.parse.urlparse(url).path))
    path = os.path.join(dest_dir, name)
    if os.path.exists(path):
        return name, "cached"
    tmp = path + ".part"
    with _open(url, {"Range": "bytes=0-0"}) as r:
        cr = r.headers.get("Content-Range")
        size = int(cr.split("/")[1]) if cr else None
        real = r.geturl()
    if size is None:
        with _open(url) as r, open(tmp, "wb") as f:
            while True:
                buf = r.read(1 << 18)
                if not buf:
                    break
                f.write(buf)
                with _lock:
                    _done_bytes += len(buf)
    else:
        with open(tmp, "wb") as f:
            f.truncate(size)
        ranges = [(s, min(s + PART, size) - 1) for s in range(0, size, PART)]
        list(pool.map(lambda se: _fetch_range(real, se[0], se[1], tmp), ranges))
    if sha:
        h = hashlib.sha256()
        with open(tmp, "rb") as f:
            for chunk in iter(lambda: f.read(1 << 20), b""):
                h.update(chunk)
        if h.hexdigest() != sha:
            os.remove(tmp)
            raise RuntimeError(f"sha256 mismatch for {name}")
    os.replace(tmp, path)
    return name, "ok"


def main():
    dest = sys.argv[1]
    os.makedirs(dest, exist_ok=True)
    args = sys.argv[2:]
    urls = []
    if args and args[0] == "--report":
        rep = json.load(open(args[1], encoding="utf-8"))
        for item in rep.get("install", []):
            di = item.get("download_info", {})
            u = di.get("url", "")
            if not u.startswith("http"):
                continue
            sha = (di.get("archive_info", {}).get("hashes") or {}).get("sha256")
            urls.append(u.split("#")[0] + (f"#sha256={sha}" if sha else ""))
    else:
        urls = args
    t0 = time.time()
    pool = ThreadPoolExecutor(PARTS_AT_ONCE * 2)
    stop = False

    def progress():
        last = 0
        while not stop:
            time.sleep(15)
            with _lock:
                d = _done_bytes
            print(f"  {d / 1e6:8.1f} MB  {(d - last) / 15 / 1e3:7.0f} kB/s", flush=True)
            last = d

    threading.Thread(target=progress, daemon=True).start()
    failed = []
    with ThreadPoolExecutor(FILES_AT_ONCE) as files:
        for fut, u in [(files.submit(fetch, u, dest, pool), u) for u in urls]:
            try:
                name, state = fut.result()
                if state != "cached":
                    print(f"{state:6} {name}", flush=True)
            except Exception as e:                # noqa: BLE001
                failed.append(u)
                print(f"FAILED {u}: {e}", flush=True)
    stop = True
    print(f"done: {len(urls) - len(failed)}/{len(urls)} files, {_done_bytes / 1e6:.1f} MB in {time.time() - t0:.0f} s", flush=True)
    sys.exit(1 if failed else 0)


if __name__ == "__main__":
    main()
