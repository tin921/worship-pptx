"""Reproduce the bundled static fonts (optional; binaries are checked in).
Requires Python 3 and fonttools==4.60.1. No font installation is performed.
"""
from pathlib import Path
from urllib.request import urlopen
import hashlib
import json
from io import BytesIO
from zipfile import ZipFile
from fontTools.ttLib import TTFont
from fontTools.varLib.instancer import instantiateVariableFont

ROOT = Path(__file__).resolve().parents[1]
COMMIT = "f8d157532fbfaeda587e826d4cd5b21a49186f7c"
FAMILIES = [("Sans", "NotoSansCJKhk"), ("Serif", "NotoSerifCJKhk")]
STATIC_FONTS = [
    ("LXGWWenKaiTC-Regular.ttf", "lxgw/LxgwWenkaiTC", "e046996b1568dd4df1c6f9a84441ce62a3dc4b2e", "fonts/TTF/LXGWWenKaiTC-Regular.ttf", "OFL.txt", "WenKai-OFL.txt", "6662480a3f95b4a90c80b189c679617a48adce3a505118a6af63ce5a482b83fa"),
    ("jf-openhuninn-2.1.ttf", "justfont/open-huninn-font", "f240f9071a78298f379bc087cae2010df11cf477", "font/jf-openhuninn-2.1.ttf", "LICENSE", "Huninn-OFL.txt", "9d5bf4932d31fe94c18cd8cfddc98bc1b14ce10f4e354c682179db290a99c825"),
    ("MaShanZheng-Regular.ttf", "google/fonts", "e7db6ca603fd4862010383ff17ffd6cc4ffac1c0", "ofl/mashanzheng/MaShanZheng-Regular.ttf", "ofl/mashanzheng/OFL.txt", "MaShanZheng-OFL.txt", "6d2546bb189c732a8ca29af9e22457b152387d158aa459e4ac2ce1e51788b7fb"),
    ("ZCOOLKuaiLe-Regular.ttf", "google/fonts", "e7db6ca603fd4862010383ff17ffd6cc4ffac1c0", "ofl/zcoolkuaile/ZCOOLKuaiLe-Regular.ttf", "ofl/zcoolkuaile/OFL.txt", "ZCOOLKuaiLe-OFL.txt", "812a6fc1fe54b6d73a419245c32dfeba8aa33104d5be90d1cf6af082007cb71d"),
    ("LXGWMarkerGothic-Regular.ttf", "google/fonts", "e7db6ca603fd4862010383ff17ffd6cc4ffac1c0", "ofl/lxgwmarkergothic/LXGWMarkerGothic-Regular.ttf", "ofl/lxgwmarkergothic/OFL.txt", "MarkerGothic-OFL.txt", "e6a55cfd5f18dc393f92670a164397a69242cb4eabe1d3feb5dc22fc4947a8ba"),
]

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
    for filename, repo, commit, source_path, license_path, license_name, expected in STATIC_FONTS:
        base = f"https://raw.githubusercontent.com/{repo}/{commit}"
        url = f"{base}/{source_path}"
        data = urlopen(url, timeout=60).read()
        assert hashlib.sha256(data).hexdigest() == expected, "Static font checksum mismatch"
        output = dest / filename
        output.write_bytes(data)
        font = TTFont(output)
        assert "fvar" not in font and "glyf" in font and font["OS/2"].fsType == 0
        (dest / license_name).write_bytes(urlopen(f"{base}/{license_path}", timeout=30).read())
        manifest.append({"file": filename, "family": font["name"].getDebugName(1), "source": url,
                         "sha256": expected, "weight": font["OS/2"].usWeightClass,
                         "license": license_name, "glyphs": len(font.getBestCmap())})
        print(filename, output.stat().st_size, manifest[-1]["family"])
    # Upstream distributes Zhuque as an archive. Keep its editable-embedding flag.
    url = "https://github.com/TrionesType/zhuque/releases/download/v0.212/ZhuqueFangsong-v0.212.zip"
    archive = urlopen(url, timeout=60).read()
    assert hashlib.sha256(archive).hexdigest() == "bb8b661a7643d2296a72d9d10530a00949419c4e527fb61783f73c2ba1a8c062"
    with ZipFile(BytesIO(archive)) as package:
        data = package.read("ZhuqueFangsong-Regular.ttf")
    expected = "558c62730844fe54ba220146ed62f859d4e2880188d92d985f8921c6e3743bc4"
    assert hashlib.sha256(data).hexdigest() == expected
    output = dest / "ZhuqueFangsong-Regular.ttf"
    output.write_bytes(data)
    font = TTFont(output)
    assert "fvar" not in font and "glyf" in font and font["OS/2"].fsType == 8
    license_url = "https://raw.githubusercontent.com/TrionesType/zhuque/005a8d0d620cb1fe2a576620a69daea3cc7477b4/LICENSE.txt"
    (dest / "Zhuque-OFL.txt").write_bytes(urlopen(license_url, timeout=30).read())
    manifest.append({"file": output.name, "family": font["name"].getDebugName(1), "source": url,
                     "sha256": expected, "weight": font["OS/2"].usWeightClass,
                     "license": "Zhuque-OFL.txt", "glyphs": len(font.getBestCmap())})
    print(output.name, output.stat().st_size, manifest[-1]["family"])
    (dest / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2)+"\n", encoding="utf-8")

if __name__ == "__main__":
    main()
