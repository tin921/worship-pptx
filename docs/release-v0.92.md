# Worship PPTX 0.92

Switch between Traditional and Simplified Chinese using **繁體 / 简体** in the left sidebar.

- Updates the interface, service history, tab titles, lyrics, Scripture, announcements, copyright text, previews, and PowerPoint export immediately.
- Remembers the selected language after restarting. Original text is retained when switching scripts, including ambiguous Traditional characters.
- Converts locally without an internet connection. Keeps verse tags, spacing, CCLI numbers, English text, and the worship pronouns 祢 / 祂.
- Retains the existing backgrounds, fonts, formatting, and camera preview layouts.

[Watch or download the English tutorial](https://github.com/tin921/worship-pptx/blob/main/docs/tutorial.md)

## Downloads

- **WorshipPptx-0.92.0-Setup.msi** — Windows installer; upgrades v0.91 and the original v1.0.0 release.
- **WorshipPptx-0.92.0-Portable.zip** — extract the entire folder and run `WorshipPptx.exe`.
- **SHA256SUMS.txt** — SHA-256 checksums for both downloads.

For Windows 10 22H2 or Windows 11, x64. The runtime, fonts, background library, local matching model, and Chinese conversion dictionaries are included. No separate .NET installation or API key is needed. Builds are unsigned.

**macOS version coming soon.**

## Validation

77 automated checks passed, including language switching, original-text preservation, restarting, and valid PowerPoint exports in both scripts. Installer and portable ZIP payloads are checked against the release build. Native installer upgrade/uninstall testing and PowerPoint playback have not been repeated for this release.
