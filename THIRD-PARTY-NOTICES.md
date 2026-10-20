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

## Build and verification tools

- Microsoft .NET Framework reference assemblies: development only; NuGet packages.
- Microsoft Open XML SDK 3.3.0 (MIT): test-only schema validation; not shipped in the app.
- WiX Toolset 3.14.1 (Microsoft Reciprocal License): MSI build tool; downloaded with
  a pinned SHA-256 hash. The WiX UI and standard actions are included in the MSI.
- fonttools 4.60.1 (MIT): optional font preparation tool, not required to build
  the app from the committed fonts and not shipped in the installer.

The sample song lyrics and vector mountain background were created for this
project and are available under the project's MIT licence. Sample Scripture
uses the public-domain 1919 Chinese Union Version. No commercial song catalogue
or CCLI content is included.
