"""Turns the print PDFs of a guide capture (docs/guide/tmp/print-*.pdf) into the figures of the guide chapter on printing:
first page of each PDF as docs/guide/img/6x-print-*.png (PyMuPDF, 80 dpi), and prints the page count of each PDF.
Usage: python tools/guide/render_prints.py   (run from the repository root)"""
from pathlib import Path

import pymupdf

root = Path(__file__).resolve().parent.parent.parent
tmp = root / "docs" / "guide" / "tmp"
img = root / "docs" / "guide" / "img"
names = {
    "print-section-colour": "64-print-section-colour",
    "print-section-bw": "65-print-section-bw",
    "print-teachers": "66-print-teacher",
    "print-school-colour": "67-print-school",
    "print-school-per-stage": "68-print-school-per-stage",
    "print-teacher-cards": "69e-share-cards",
}
for source, target in names.items():
    pdf = tmp / f"{source}.pdf"
    with pymupdf.open(pdf) as document:
        pixmap = document[0].get_pixmap(dpi=80, alpha=False)
        pixmap.save(img / f"{target}.png")
        print(f"{pdf.name}: {document.page_count} pages, first page saved as {target}.png ({pixmap.width}x{pixmap.height})")
