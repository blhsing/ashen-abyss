"""Download model files from Hugging Face with parallel range requests (anonymous), verifying the
LFS sha256 published by the Hub API. Used instead of fetch_models.py when the link is slow/shared:
huggingface_hub fetched ~0.3 MB/s here while other jobs were downloading too.

    python fetch_fast.py voxcpm2 cosy3
"""
import fnmatch
import json
import os
import sys
import urllib.request

import pget
from fetch_models import MODELS, ROOT

pget.FILES_AT_ONCE = 2
pget.PARTS_AT_ONCE = 8


def listing(repo: str) -> list[dict]:
    req = urllib.request.Request(f"https://huggingface.co/api/models/{repo}?blobs=true", headers=pget.UA)
    return json.load(urllib.request.urlopen(req, timeout=60))["siblings"]


def main():
    for name in sys.argv[1:]:
        m = MODELS[name]
        dest = os.path.join(ROOT, "models", name)
        files = [s for s in listing(m["repo"]) if m["allow"] is None or any(fnmatch.fnmatch(s["rfilename"], p) for p in m["allow"])]
        files = [s for s in files if not s["rfilename"].startswith(".")]
        print(f"== {name}: {len(files)} files, {sum((s.get('size') or 0) for s in files) / 1e9:.2f} GB -> {dest}", flush=True)
        by_dir: dict[str, list[str]] = {}
        for s in sorted(files, key=lambda s: s.get("size") or 0):
            rel = s["rfilename"]
            sub = os.path.join(dest, os.path.dirname(rel))
            if os.path.exists(os.path.join(dest, rel)) and os.path.getsize(os.path.join(dest, rel)) == (s.get("size") or -1):
                continue
            url = f"https://huggingface.co/{m['repo']}/resolve/main/{rel}"
            sha = (s.get("lfs") or {}).get("sha256")
            by_dir.setdefault(sub, []).append(url + (f"#sha256={sha}" if sha else ""))
        for sub, urls in by_dir.items():
            sys.argv = ["pget.py", sub] + urls
            try:
                pget.main()
            except SystemExit as e:
                if e.code:
                    raise
        print(f"== {name}: done", flush=True)


if __name__ == "__main__":
    main()
