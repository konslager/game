#!/usr/bin/env python3
"""Embed data/regions/emberwatch.json into web/index.html (the <script id="region-data"> block).

Run after editing the JSON:  python3 tools/embed_region.py
"""
import json
import pathlib
import re

ROOT = pathlib.Path(__file__).resolve().parent.parent
data = json.loads((ROOT / "data/regions/emberwatch.json").read_text(encoding="utf-8"))
html_path = ROOT / "web/index.html"
html = html_path.read_text(encoding="utf-8")

payload = json.dumps(data, ensure_ascii=False, separators=(",", ":")).replace("</", "<\\/")
pattern = re.compile(r'(<script id="region-data" type="application/json">)(.*?)(</script>)', re.S)
if not pattern.search(html):
    raise SystemExit("region-data block not found in web/index.html")
html_path.write_text(pattern.sub(lambda m: f"{m.group(1)}\n{payload}\n{m.group(3)}", html, count=1), encoding="utf-8")
print(f"Embedded {len(payload)} bytes into {html_path.relative_to(ROOT)}")
