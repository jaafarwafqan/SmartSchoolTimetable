"""Finds the page of every heading marker ("SSTMARK-<id>") of the guide PDF and prints {marker number: page number} as JSON (page 1 = the cover).
Usage: python tools/guide/find_pages.py <pdf>"""
import json
import re
import sys

import pymupdf

pages = {}
with pymupdf.open(sys.argv[1]) as document:
    for number, page in enumerate(document, start=1):
        for found in re.findall(r"SSTMARK(\d{4})X", page.get_text()):
            pages.setdefault(int(found), number)
    pages["__count"] = document.page_count
print(json.dumps(pages))
