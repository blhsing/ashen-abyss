"""Print licence, last-modified date and file sizes of Hugging Face model repos (no account, no token)."""
import json, sys, urllib.request
for m in sys.argv[1:]:
    req = urllib.request.Request(f"https://huggingface.co/api/models/{m}?blobs=true", headers={"User-Agent": "Mozilla/5.0"})
    try:
        d = json.load(urllib.request.urlopen(req, timeout=60))
    except Exception as e:
        print("==", m, "ERROR", e); continue
    sib = d.get("siblings", [])
    total = sum((s.get("size") or 0) for s in sib)
    print(f"== {m}  licence={(d.get('cardData') or {}).get('license')}  modified={d.get('lastModified')}  total={total/1e9:.2f} GB")
    for s in sorted(sib, key=lambda s: -(s.get("size") or 0))[:14]:
        print(f"   {(s.get('size') or 0)/1e6:10.1f} MB  {s['rfilename']}")
