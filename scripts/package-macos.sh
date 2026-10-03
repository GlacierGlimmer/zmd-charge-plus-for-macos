#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
rid="${1:?Usage: package-macos.sh osx-arm64|osx-x64 [version]}"
version="${2:-$(python3 -c 'import xml.etree.ElementTree as ET; print(ET.parse("EndfieldChargePlus.csproj").findtext(".//Version"))')}"
case "$rid" in osx-arm64) arch=arm64 ;; osx-x64) arch=x86_64 ;; *) exit 2 ;; esac
[[ "$version" =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]] || { echo 'Invalid version'; exit 2; }
name='Endfield Charge Plus For MacOS'
mkdir -p "$PWD/artifacts/$rid"
stage=$(mktemp -d "$PWD/artifacts/$rid/stage.XXXXXX")
app="$stage/$name.app"
out="$PWD/artifacts/packages"
mkdir -p "$app/Contents/MacOS" "$app/Contents/Resources" "$out"
bash scripts/build-native.sh "$rid"
dotnet publish EndfieldChargePlus.csproj -c Release -r "$rid" --self-contained true \
  -p:Version="$version" -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=false \
  -p:DebugType=embedded -p:DebugSymbols=false -p:PublishTrimmed=false -o "$app/Contents/MacOS"
cp native/libecpmac.dylib "$app/Contents/MacOS/"
chmod 755 "$app/Contents/MacOS/EndfieldChargePlus"
python3 - "$app" "$version" "$arch" <<'PY'
import sys,plistlib,pathlib
app,version,arch=sys.argv[1:]
info={"CFBundleName":"ECP For MacOS","CFBundleDisplayName":"Endfield Charge Plus For MacOS",
      "CFBundleIdentifier":"com.glacierglimmer.endfieldchargeplus.macos","CFBundleVersion":version,
      "CFBundleShortVersionString":version,"CFBundleExecutable":"EndfieldChargePlus","CFBundlePackageType":"APPL",
      "CFBundleIconFile":"AppIcon.icns","LSMinimumSystemVersion":"13.0","LSUIElement":True,
      "NSHighResolutionCapable":True,"NSPrincipalClass":"NSApplication","LSArchitecturePriority":[arch],
      "NSHumanReadableCopyright":"© 2026 GlacierGlimmer_冰川雪貓"}
with open(pathlib.Path(app)/'Contents/Info.plist','wb') as f: plistlib.dump(info,f)
PY
iconset="$PWD/artifacts/$rid/AppIcon.iconset"
mkdir -p "$iconset"
for size in 16 32 128 256 512; do
  sips -z "$size" "$size" Assets/tray_bolt.png --out "$iconset/icon_${size}x${size}.png" >/dev/null
  double=$((size*2))
  sips -z "$double" "$double" Assets/tray_bolt.png --out "$iconset/icon_${size}x${size}@2x.png" >/dev/null
done
iconutil -c icns "$iconset" -o "$app/Contents/Resources/AppIcon.icns"
cp LICENSE NOTICE.md "$app/Contents/Resources/"
plutil -lint "$app/Contents/Info.plist"

# Sign inner code first. A Developer ID can be supplied without changing the build.
# Ad-hoc signatures enable ARM execution but are not Developer ID / notarization.
identity="${MACOS_SIGNING_IDENTITY:--}"
options=(--timestamp=none)
if [[ "$identity" != '-' ]]; then options=(--options runtime --timestamp); fi
while IFS= read -r -d '' file; do
  if file -b "$file" | grep -q 'Mach-O'; then
    lipo "$file" -verify_arch "$arch"
    codesign --force --sign "$identity" "${options[@]}" --entitlements packaging/macos/entitlements.plist "$file"
  fi
done < <(find "$app/Contents/MacOS" -type f -print0)
codesign --force --sign "$identity" "${options[@]}" --entitlements packaging/macos/entitlements.plist "$app"
codesign --verify --deep --strict --verbose=2 "$app"
ln -s /Applications "$stage/Applications"
cp packaging/macos/READ-ME.txt "$stage/READ-ME.txt"
dmg="$out/EndfieldChargePlusForMacOS-v${version}-${rid}.dmg"
hdiutil create -volname "ECP For MacOS $version" -srcfolder "$stage" -format UDZO -fs HFS+ -ov "$dmg"
if [[ -n "${MACOS_NOTARY_PROFILE:-}" ]]; then
  [[ "$identity" != '-' ]] || { echo 'Notarization requires Developer ID signing'; exit 1; }
  codesign --sign "$identity" --timestamp "$dmg"
  xcrun notarytool submit "$dmg" --keychain-profile "$MACOS_NOTARY_PROFILE" --wait
  xcrun stapler staple "$dmg"
  xcrun stapler validate "$dmg"
fi
hdiutil verify "$dmg"
shasum -a 256 "$dmg" | sed "s|$out/||" > "$dmg.sha256"
echo "Created $dmg"
