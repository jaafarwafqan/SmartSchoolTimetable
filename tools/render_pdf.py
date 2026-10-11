"""Renders every page of the PDFs in a folder to PNG files next to them (for looking at printing results).
Usage: python tools/render_pdf.py <folder> [dpi]
Prints, per PDF, the page count, page size and whether the page contains colour (non-grey pixels) at all."""
import sys
from pathlib import Path

import pymupdf

folder = Path(sys.argv[1])
dpi = int(sys.argv[2]) if len(sys.argv) > 2 else 60
for pdf in sorted(folder.glob("*.pdf")):
    with pymupdf.open(pdf) as document:
        for index, page in enumerate(document, start=1):
            pixmap = page.get_pixmap(dpi=dpi, alpha=False)
            target = pdf.with_name(f"{pdf.stem}-p{index}.png")
            pixmap.save(target)
            samples = pixmap.samples
            colourful = any(abs(samples[i] - samples[i + 1]) > 12 or abs(samples[i + 1] - samples[i + 2]) > 12 for i in range(0, len(samples) - 2, 3 * 7))
            print(f"{pdf.name} page {index}/{document.page_count}: {page.rect.width / 72 * 25.4:.0f}x{page.rect.height / 72 * 25.4:.0f} mm, colour={'yes' if colourful else 'no'}")
