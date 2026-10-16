"""Draws the application icon (a blue rounded square with a white graduation cap, like the brand mark in the app) and writes
src/SmartSchoolTimetable.Launcher/app.ico (16-256 px, PNG entries) plus installer/wizard-small.bmp and installer/wizard-large.bmp.
Usage: python tools/make_icon.py   (needs Pillow; run from the repository root)"""
from pathlib import Path

from PIL import Image, ImageDraw

BLUE = (23, 88, 166, 255)       # --color-primary
WHITE = (255, 255, 255, 255)
SIZE = 1024


def draw_mark(size: int = SIZE) -> Image.Image:
    image = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.rounded_rectangle((0, 0, size - 1, size - 1), radius=size // 5, fill=BLUE)
    s = size / 24  # the lucide "graduation-cap" lives on a 24 x 24 grid
    # Mortarboard (a flat diamond), the cap under it and the tassel.
    draw.polygon([(12 * s, 5 * s), (22 * s, 10 * s), (12 * s, 15 * s), (2 * s, 10 * s)], fill=WHITE)
    draw.polygon([(6.2 * s, 12.6 * s), (12 * s, 15.6 * s), (17.8 * s, 12.6 * s), (17.8 * s, 16.4 * s), (12 * s, 19.2 * s), (6.2 * s, 16.4 * s)], fill=WHITE)
    draw.rectangle((20.4 * s, 10.4 * s, 21.4 * s, 16 * s), fill=WHITE)
    draw.ellipse((19.9 * s, 15.6 * s, 21.9 * s, 17.6 * s), fill=WHITE)
    return image


def main() -> None:
    root = Path(__file__).resolve().parent.parent
    mark = draw_mark()
    icon_sizes = [16, 24, 32, 48, 64, 128, 256]
    target = root / "src" / "SmartSchoolTimetable.Launcher" / "app.ico"
    target.parent.mkdir(parents=True, exist_ok=True)
    mark.save(target, format="ICO", sizes=[(size, size) for size in icon_sizes])
    installer = root / "installer"
    installer.mkdir(exist_ok=True)
    # Installer wizard pictures: 55x58 (small, top right) and 164x314 (large, side panel), white background.
    small = Image.new("RGB", (55, 58), "white")
    small.paste(mark.resize((48, 48), Image.LANCZOS), (4, 5), mark.resize((48, 48), Image.LANCZOS))
    small.save(installer / "wizard-small.bmp")
    large = Image.new("RGB", (164, 314), (23, 88, 166))
    big = mark.resize((120, 120), Image.LANCZOS)
    large.paste(big, (22, 90), big)
    large.save(installer / "wizard-large.bmp")
    print("written", target.relative_to(root), "and installer pictures")


if __name__ == "__main__":
    main()
