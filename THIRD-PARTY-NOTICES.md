# Third-party notices

## Noto Sans CJK HK / Noto Serif CJK HK

The app bundles the full CJK fonts with Hong Kong glyph conventions, instantiated
at regular weight (400) as static TrueType faces for Windows 10 compatibility.
They include Traditional and Simplified Chinese characters. These are not the
smaller Google Fonts Hong Kong subsets.

- Upstream: https://github.com/notofonts/noto-cjk
- Pinned revision: `f8d157532fbfaeda587e826d4cd5b21a49186f7c`
- Copyright: Adobe and the font contributors, as stated in the included licences.
- Licence: SIL Open Font License 1.1, in `assets/fonts/Sans-OFL.txt` and
  `assets/fonts/Serif-OFL.txt` (installed as `Fonts/*-OFL.txt`).
- Reproduction: `scripts/prepare-fonts.py`, using fonttools 4.67.0.
- Provenance, filenames and SHA-256 hashes: `assets/fonts/manifest.json`.

Font redistribution and document embedding are permitted by these licences.
The application loads its own copies without installing fonts system-wide.
The selected font is embedded in each presentation as a full EOT font part.

## Additional Chinese fonts

The app also bundles LXGW WenKai TC, jf open huninn, Zhuque Fangsong, LXGW
Marker Gothic, Ma Shan Zheng and ZCOOL KuaiLe under SIL OFL 1.1. Their original
copyright and licence notices are in `assets/fonts/*-OFL.txt`.

The following additions are distributed under SIL OFL 1.1, with the publishers'
copyright, licence and source information in each corresponding
`assets/fonts/*-LICENSE.txt` file (also installed in `Fonts/`):

- Bakudai / 莫大毛筆, MasaFont / 正風毛筆, NaikaiFont / 內海字體,
  NaniFont / 何某手寫體, Kurewa Gothic / 苦累蛙圓體 and Swei Spring Sugar /
  獅尾四季春, from https://github.com/max32002 and the original creators
  identified in the accompanying notices.
- JasonHandwriting 4 and 6 / 清松手寫, Jason (Yu Ching Sung),
  https://github.com/jasonhandwriting/JasonHandwriting.
- GenWanMin2 / 源雲明體 and GenSenRounded2 / 源泉圓體, But Ko / ButTaiwan and
  Adobe contributors, https://github.com/ButTaiwan.
- MaokenAssortedSans-TC / 貓啃什錦黑, Skr-ZERO and the contributors credited
  in the publisher's notice, https://github.com/Skr-ZERO/MaokenAssortedSans-TC.
- Mantou Sans / 饅頭黑體, Mant0u and the Dela Gothic Project,
  https://github.com/mant0u0/MantouSans.
- Cubic 11 / 俐方體11號, ACh and the contributors listed in the font notice,
  https://github.com/ACh-K/Cubic-11.
- Chuan Qu / 傳趣體, SteveYuu and the Stick Project,
  https://github.com/Steve-Yuu/Chuanqu.
- QIJIC / 齊伋體, Lingdong Huang, https://github.com/LingDong-/qiji-font.
  This is the publisher's combined edition, which also contains distressed
  Source Han Serif glyphs. Both notices accompany the unchanged font.

教育部隸書 / TW-MOE-Li 3.00 is attributed to **中華民國教育部** and distributed
unchanged under Creative Commons Attribution-NoDerivs 3.0 Taiwan. The original
file is copied byte for byte, never renamed internally or subsetted. Its notice
and full terms are in `assets/fonts/TW-MOE-Li-LICENSE.txt` and
`TW-MOE-Li-CC-BY-ND-3.0-TW.html`. Presentations embedding it also carry its
attribution and licence link in document properties.
Publisher: https://language.moe.gov.tw/material/info?m=9fe3fb11-c3d5-41f2-b029-6d18a2c2fd0d.

Pinned download URLs, original and bundled SHA-256 hashes, and conversion flags
are recorded in `assets/fonts/manifest.json` and `scripts/additional-fonts.json`.
`scripts/prepare-additional-fonts.py --cache <directory>` reproduces the new
fonts using fonttools 4.67.0. GenWanMin2 and GenSenRounded2 are converted from
CFF to static TrueType with a maximum curve error of one font unit, retaining
their character sets, layout tables, names and embedding permissions. All other
new font binaries are unchanged originals. No system font installation is needed.

Coverage varies by font. The picker marks the known limited character or
punctuation coverage of JasonHandwriting 4/6, MaokenAssortedSans-TC, QIJIC and
TW-MOE-Li. Our 248-character worship-text fixture is a regression sample, not a
claim of complete Traditional Chinese coverage. Missing characters may use a
fallback font in the preview or PowerPoint.

## Desktop runtime

The Avalonia build includes these open-source runtime components. Copyright and
licence texts, including native-library dependencies, are bundled in
`THIRD-PARTY-LICENSES.txt`.

- Avalonia 12.1.4 and its desktop backends / Fluent theme (MIT), AvaloniaUI Project.
- SkiaSharp 3.119.4 (MIT) and Skia native dependencies (their included licences), Microsoft / Google and contributors.
- HarfBuzzSharp 8.3.1.3 (MIT) and HarfBuzz native dependencies (their included licences).
- ANGLE native graphics (BSD-style), the ANGLE Project Authors.
- MicroCom.Runtime 0.11.6 (MIT), Nikita Tsukanov.
- Tmds.DBus.Protocol 0.94.1 (MIT), Tom Deseyn; Linux desktop dependency.
- Microsoft .NET 10 runtime (MIT and the accompanying third-party notices), included in self-contained builds.
- Windows builds also include the Microsoft Visual C++ v14 CRT DLLs from the build host's licensed Visual Studio redistributable folder, under Microsoft's terms (not the project's MIT licence). `VC-RUNTIME-MANIFEST.json` records versions and hashes. This application-local deployment supports portable/per-user use without installing a system runtime. These copies are serviced through application updates. See https://learn.microsoft.com/cpp/windows/redistributing-visual-cpp-files and https://learn.microsoft.com/cpp/windows/choosing-a-deployment-method.

## Local background matching and generated media

- Microsoft.ML.OnnxRuntime 1.31.0 (1.23.2 for Intel Mac) and Microsoft.ML.Tokenizers 2.0.1: MIT, Microsoft and contributors. Native runtime notices accompany the package. Intel Mac keeps the matching managed/native 1.23.2 pair because newer official packages omit its native library.
- `sentence-transformers/paraphrase-multilingual-MiniLM-L12-v2`: Apache 2.0, Sentence Transformers contributors. Pinned model/tokenizer sources and checksums are in `scripts/prepare-model.ps1`; the full Apache licence is included at `assets/models/LICENSE.txt` (installed at `Models/LICENSE.txt`).
- The Traditional-to-Simplified character map is derived from OpenCC, Apache 2.0, BYVoid and contributors, https://github.com/BYVoid/OpenCC. The same included Apache licence applies to the derived normalization table.
- Themed media are original AI-generated assets. Generic graphics and their renderer are original project work. Prompts/provenance and runtime use are described in `docs/media-library.md`.
- Historical animation experiments used FFmpeg as a developer-only tool. The app ships static images only, without MP4 files, animated WebP previews or FFmpeg binaries.

## Offline Chinese script conversion

- OpenccNetLib 1.7.0 (MIT), laisuk, https://github.com/laisuk/OpenccNet,
  package source revision `01f23ccd4feda86e7607cc1dd92ba93a44252e61`.
  Its embedded OpenCC dictionaries are Apache 2.0, BYVoid and contributors.
- ZstdSharp.Port 0.8.8 (MIT), Oleg Stepanischev; managed dictionary decompression.
- System.Formats.Cbor 8.0.0 (MIT), .NET Foundation and contributors.

Conversion runs locally on Windows and macOS without an API, model download or
native converter. Full licences are included in `THIRD-PARTY-LICENSES.txt`.
Worship pronouns 祢 and 祂 are preserved in both scripts.

## Build and verification tools

- Microsoft .NET 10 SDK: development only.
- Microsoft Open XML SDK 3.3.0 (MIT): test-only schema validation; not shipped in the app.
- WiX Toolset 3.14.1 (Microsoft Reciprocal License): MSI build tool; downloaded with
  a pinned SHA-256 hash. The WiX UI and standard actions are included in the MSI.
- fonttools 4.67.0 (MIT): optional font preparation tool, not required to build
  the app from the committed fonts and not shipped in the installer.

The sample song lyrics and vector mountain background were created for this
project and are available under the project's MIT licence. Sample Scripture
uses the public-domain 1919 Chinese Union Version. No commercial song catalogue
or CCLI content is included.
