#!/bin/bash
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${1:-osx-arm64}"
VERSION="${2:-$(sed -n 's/.*<Version>\(.*\)<\/Version>.*/\1/p' "$ROOT/src/WorshipPptx/WorshipPptx.csproj" | head -n 1)}"
case "$RID" in osx-arm64|osx-x64) ;; *) echo 'Use osx-arm64 or osx-x64.' >&2; exit 1;; esac
if [[ "$(uname -s)" != Darwin ]]; then echo 'Run this packaging script on macOS.' >&2; exit 1; fi
if [[ ! -f "$ROOT/assets/models/model.onnx" ]]; then
  echo 'Run pwsh ./scripts/prepare-model.ps1 first to download the pinned offline model.' >&2; exit 1
fi
STAGE="$(mktemp -d "${TMPDIR:-/tmp}/worship-pptx.XXXXXX")"
APP="$STAGE/Worship PPTX.app"
mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources" "$ROOT/dist"
dotnet publish "$ROOT/src/WorshipPptx/WorshipPptx.csproj" -c Release -r "$RID" --self-contained true -p:PublishTrimmed=false "-p:Version=$VERSION" -o "$APP/Contents/MacOS"
cp "$ROOT/LICENSE" "$ROOT/THIRD-PARTY-NOTICES.md" "$ROOT/THIRD-PARTY-LICENSES.txt" "$APP/Contents/MacOS/"
cat > "$APP/Contents/Info.plist" <<EOF
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0"><dict>
<key>CFBundleIdentifier</key><string>org.worshippptx.desktop</string>
<key>CFBundleName</key><string>Worship PPTX</string>
<key>CFBundleDisplayName</key><string>敬拜簡報</string>
<key>CFBundleExecutable</key><string>WorshipPptx</string>
<key>CFBundlePackageType</key><string>APPL</string>
<key>CFBundleVersion</key><string>$VERSION</string>
<key>CFBundleShortVersionString</key><string>$VERSION</string>
<key>LSMinimumSystemVersion</key><string>14.0</string>
<key>NSHighResolutionCapable</key><true/>
</dict></plist>
EOF
chmod +x "$APP/Contents/MacOS/WorshipPptx"
# Ad-hoc signing permits local testing. Distribution needs Developer ID signing and notarization.
codesign --force --deep --sign - "$APP"
codesign --verify --deep --strict "$APP"
ditto -c -k --sequesterRsrc --keepParent "$APP" "$ROOT/dist/WorshipPptx-$VERSION-$RID.zip"
hdiutil create -volname 'Worship PPTX' -srcfolder "$STAGE" -ov -format UDZO "$ROOT/dist/WorshipPptx-$VERSION-$RID.dmg"
echo "Local-test app: $APP"
echo 'Mac packages were created locally. Nothing was uploaded.'
