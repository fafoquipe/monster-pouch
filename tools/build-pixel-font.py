"""Create the local OFL derivative from the official Pixelify Sans variable TTF.

Requires fonttools. This edits vector font outlines and never installs an OS font.
Usage: python tools/build-pixel-font.py --source PixelifySans-original.ttf
"""
import argparse
import hashlib
from pathlib import Path

from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

ORIGINAL_SHA256 = "9ba86cd010a4de309d263ceff8e8044092c9db7efda869620cb9ff1c4389e8a5"

STAR = [
    "....#....", "....#....", "...###...", "#########", ".#######.",
    "..#####..", "..#####..", ".###.###.", ".##...##.",
]


def border(grid):
    return ["".join("#" if cell == "#" and any(
        not (0 <= x + dx < 9 and 0 <= y + dy < 9) or grid[y + dy][x + dx] != "#"
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))) else "."
        for x, cell in enumerate(row)) for y, row in enumerate(grid)]


GLYPHS = {
    0x2605: STAR,
    0x2606: border(STAR),
    0x25C6: ["....#....", "...###...", "..#####..", ".#######.", "#########",
             ".#######.", "..#####..", "...###...", "....#...."],
    0x2726: ["....#....", "....#....", "...###...", "..#####..", "#########",
             "..#####..", "...###...", "....#....", "....#...."],
    0x21BB: ["..#####..", ".##...##.", ".#.....#.", "##...####", "##....###",
             "##.....#.", ".#.......", ".##...##.", "..#####.."],
    0x2161: ["........."] + [".##...##."] * 7 + ["........."],
}


def main():
    args = argparse.ArgumentParser()
    args.add_argument("--source", required=True, type=Path)
    args.add_argument("--output", type=Path,
                      default=Path("Assets/resources/MonsterPouch/PixelFont.ttf"))
    options = args.parse_args()
    source = options.source.read_bytes()
    if hashlib.sha256(source).hexdigest() != ORIGINAL_SHA256:
        raise SystemExit("Source is not the documented, unmodified Google Fonts TTF.")
    font = TTFont(options.source, recalcTimestamp=False)
    # Static regular weight improves compatibility with Unity's dynamic Font importer.
    # Existing Spanish/Latin outlines and their spacing remain at the original default.
    instantiateVariableFont(font, {"wght": 400}, inplace=True)
    unit = max(1, round(font["OS/2"].sCapHeight / 9))
    order = font.getGlyphOrder()
    for codepoint, grid in GLYPHS.items():
        name = "mpPixel%04X" % codepoint
        pen = TTGlyphPen(None)
        for y, row in enumerate(grid):
            for x, pixel in enumerate(row):
                if pixel != "#":
                    continue
                left, bottom = (x + 1) * unit, (8 - y) * unit
                pen.moveTo((left, bottom))
                pen.lineTo((left, bottom + unit))
                pen.lineTo((left + unit, bottom + unit))
                pen.lineTo((left + unit, bottom))
                pen.closePath()
        font["glyf"][name] = pen.glyph()
        font["hmtx"].metrics[name] = (11 * unit, unit)
        if name not in order:
            order.append(name)
        for table in font["cmap"].tables:
            if table.isUnicode():
                table.cmap[codepoint] = name
    font.setGlyphOrder(order)
    for record in font["name"].names:
        # Preserve license, author and copyright records. Rename family identifiers.
        if record.nameID in (1, 3, 4, 6, 16, 21, 25):
            value = record.toUnicode().replace("Pixelify Sans", "Monster Pouch Pixel")
            value = value.replace("PixelifySans", "MonsterPouchPixel")
            record.string = value.encode(record.getEncoding())
    options.output.parent.mkdir(parents=True, exist_ok=True)
    font.save(options.output)
    verified = TTFont(options.output, checkChecksums=2)
    expected = "ÁÉÍÓÚÜÑáéíóúüñ¿¡" + "".join(chr(cp) for cp in GLYPHS)
    missing = ["U+%04X" % ord(char) for char in expected if ord(char) not in verified.getBestCmap()]
    if missing:
        raise SystemExit("Missing required glyphs: " + ", ".join(missing))
    print("Family:", verified["name"].getDebugName(1))
    print("Added symbols:", ", ".join("U+%04X" % cp for cp in GLYPHS))
    print("Spanish coverage: complete")
    print("SHA256:", hashlib.sha256(options.output.read_bytes()).hexdigest())


if __name__ == "__main__":
    main()
