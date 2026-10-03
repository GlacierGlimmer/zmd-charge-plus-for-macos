#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
rid="${1:?runtime ID required}"
dmg=$(find artifacts/packages -name "*-${rid}.dmg" -print -quit)
[[ -n "$dmg" ]]
mount="$PWD/artifacts/$rid/mounted"
mkdir -p "$mount"
hdiutil attach "$dmg" -nobrowse -readonly -mountpoint "$mount"
trap 'hdiutil detach "$mount" >/dev/null || true' EXIT
app="$mount/Endfield Charge Plus For MacOS.app"
test -L "$mount/Applications"
codesign --verify --deep --strict "$app"
file "$app/Contents/MacOS/EndfieldChargePlus"
"$app/Contents/MacOS/EndfieldChargePlus" --autostart >"artifacts/audit/bundle-launch.log" 2>&1 &
pid=$!
sleep 8
kill -0 "$pid"
# Second launch must signal the existing process and terminate successfully.
"$app/Contents/MacOS/EndfieldChargePlus" >"artifacts/audit/second-launch.log" 2>&1 &
second=$!
for _ in {1..20}; do kill -0 "$second" 2>/dev/null || break; sleep 0.5; done
if kill -0 "$second" 2>/dev/null; then kill "$second" "$pid"; echo 'Second instance did not exit'; exit 1; fi
wait "$second"
kill -0 "$pid"
kill "$pid"
wait "$pid" || true
echo 'Mounted bundle launch and single instance checks passed.'
