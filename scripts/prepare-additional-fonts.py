"""Reproduce the additional Chinese fonts; no system installation is performed.

Requires fonttools==4.67.0. Pinned sources and hashes are in additional-fonts.json.
The checked-in notices accompany every font. Original TrueType files are copied
unchanged, including the Ministry of Education's CC BY-ND font. The two CFF
fonts are converted to static TrueType for PowerPoint's editable EOT embedding.
"""
import argparse
import hashlib
import json
import tempfile
from io import BytesIO
from pathlib import Path
from urllib.request import urlopen
from zipfile import ZipFile

from fontTools.pens.cu2quPen import Cu2QuPen
from fontTools.pens.ttGlyphPen import TTGlyphPen
from fontTools.ttLib import TTFont, newTable

ROOT = Path(__file__).resolve().parents[1]


def sha256(data):
    return hashlib.sha256(data).hexdigest()


def convert_cff(source, output):
    """Retain the complete character set, layout tables, names and permissions.

    Curve conversion uses fontTools' Cu2QuPen with a maximum error of one font
    unit and clockwise TrueType contours. Neither font reserves its family name;
    their original Source/Adobe notices remain in the accompanying OFL files.
    """
    font = TTFont(source, recalcTimestamp=False)
    assert font.sfntVersion == "OTTO" and "CFF " in font
    order = font.getGlyphOrder()
    outlines = font.getGlyphSet()
    converted = {}
    for name in order:
        pen = TTGlyphPen(outlines)
        outlines[name].draw(Cu2QuPen(pen, 1.0, reverse_direction=True))
        converted[name] = pen.glyph()
    font["loca"] = newTable("loca")
    font["glyf"] = glyf = newTable("glyf")
    glyf.glyphOrder, glyf.glyphs = order, converted
    del font["CFF "]
    if "VORG" in font:
        del font["VORG"]
    glyf.compile(font)
    for name, glyph in converted.items():
        if hasattr(glyph, "xMin"):
            font["hmtx"][name] = (font["hmtx"][name][0], glyph.xMin)
    font["maxp"] = maxp = newTable("maxp")
    maxp.tableVersion = 0x00010000
    maxp.maxZones = 1
    for field in ("maxTwilightPoints", "maxStorage", "maxFunctionDefs",
                  "maxInstructionDefs", "maxStackElements", "maxSizeOfInstructions",
                  "maxComponentElements"):
        setattr(maxp, field, 0)
    # Glyph names are retained in cmap/layout tables; post format 3 avoids the
    # 65,535-byte name limit for these large CJK fonts.
    font["post"].formatType = 3.0
    font.sfntVersion = "\0\1\0\0"
    font.save(output)


def main(cache=None):
    cache = Path(cache or Path(tempfile.gettempdir()) / "worship-font-sources")
    cache.mkdir(parents=True, exist_ok=True)
    dest = ROOT / "assets" / "fonts"
    specs = json.loads((ROOT / "scripts" / "additional-fonts.json").read_text(encoding="utf-8"))
    manifest_path = dest / "manifest.json"
    manifest = json.loads(manifest_path.read_text(encoding="utf-8"))
    replaced = {s["file"] for s in specs}
    manifest = [m for m in manifest if m["file"] not in replaced]
    for spec in specs:
        source = cache / (spec["file"] + ".source")
        if not source.exists() or sha256(source.read_bytes()) != spec["source_sha256"]:
            data = urlopen(spec["source"], timeout=120).read()
            if spec.get("archive_member"):
                with ZipFile(BytesIO(data)) as archive:
                    data = archive.read(spec["archive_member"])
            assert sha256(data) == spec["source_sha256"], "Source checksum mismatch: " + spec["file"]
            source.write_bytes(data)
        output = dest / spec["file"]
        if spec.get("convert_cff"):
            convert_cff(source, output)
        else:
            output.write_bytes(source.read_bytes())
        with TTFont(output) as font:
            assert "glyf" in font and "fvar" not in font
            assert font["OS/2"].fsType in (0, 8), "Font must allow editable embedding"
            assert (dest / spec["license"]).exists(), "Missing font notice"
            record = dict(spec)
            record.update(sha256=sha256(output.read_bytes()), glyphs=len(font.getBestCmap()),
                          weight=font["OS/2"].usWeightClass)
            manifest.append(record)
        print(spec["file"], output.stat().st_size, flush=True)
    manifest_path.write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--cache", type=Path)
    main(parser.parse_args().cache)
