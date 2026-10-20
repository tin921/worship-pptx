"""Reproduce the bundled static fonts (optional; binaries are checked in).
Requires Python 3 and fonttools==4.60.1. No font installation is performed.
"""
from pathlib import Path
from urllib.request import urlopen
import hashlib
import json
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

ROOT = Path(__file__).resolve().parents[1]
COMMIT = "f8d157532fbfaeda587e826d4cd5b21a49186f7c"
FAMILIES = [("Sans", "NotoSansCJKhk"), ("Serif", "NotoSerifCJKhk")]

def main():
    dest = ROOT / "assets" / "fonts"
    cache = ROOT / ".tools" / "font-sources"
    dest.mkdir(parents=True, exist_ok=True)
    cache.mkdir(parents=True, exist_ok=True)
    manifest = []
    for directory, family in FAMILIES:
        url = f"https://raw.githubusercontent.com/notofonts/noto-cjk/{COMMIT}/{directory}/Variable/TTF/{family}-VF.ttf"
        source = cache / f"{directory}.ttf"
        # Always fetch the pinned source rather than trusting an unrelated cache.
        source.write_bytes(urlopen(url, timeout=60).read())
        font = TTFont(source, recalcTimestamp=False)
        instantiateVariableFont(font, {"wght": 400}, inplace=True, updateFontNames=True)
        assert "fvar" not in font and "glyf" in font
        assert font["OS/2"].fsType == 0, "Only unrestricted OFL fonts are bundled"
        output = dest / f"{family}-Regular.ttf"
        font.save(output)
        license_url = f"https://raw.githubusercontent.com/notofonts/noto-cjk/{COMMIT}/{directory}/LICENSE"
        (dest / f"{directory}-OFL.txt").write_bytes(urlopen(license_url, timeout=30).read())
        manifest.append({"file": output.name, "family": font["name"].getDebugName(1), "source": url,
                         "sha256": hashlib.sha256(output.read_bytes()).hexdigest(), "weight": 400,
                         "license": f"{directory}-OFL.txt", "glyphs": len(font.getBestCmap())})
        print(output.name, output.stat().st_size, manifest[-1]["family"])
    (dest / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")

if __name__ == "__main__":
    main()
