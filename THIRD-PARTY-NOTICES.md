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
- Reproduction: `scripts/prepare-fonts.py`, using fonttools 4.60.1.
- Provenance, filenames and SHA-256 hashes: `assets/fonts/manifest.json`.

Font redistribution and document embedding are permitted by these licences.
The application loads its own copies without installing fonts system-wide.
The selected font is embedded in each presentation as a full EOT font part.

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
- fonttools 4.60.1 (MIT): optional font preparation tool, not required to build
  the app from the committed fonts and not shipped in the installer.

The sample song lyrics and vector mountain background were created for this
project and are available under the project's MIT licence. Sample Scripture
uses the public-domain 1919 Chinese Union Version. No commercial song catalogue
or CCLI content is included.
