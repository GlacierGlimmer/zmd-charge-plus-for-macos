#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
case "${1:-$(uname -m)}" in
  osx-arm64|arm64) arch=arm64 ;;
  osx-x64|x86_64) arch=x86_64 ;;
  *) echo 'Expected osx-arm64 or osx-x64' >&2; exit 1 ;;
esac
xcrun clang -arch "$arch" -mmacosx-version-min=13.0 -O2 -Wall -Wextra -Werror \
  -Wno-deprecated-declarations -fobjc-arc -fvisibility=hidden -dynamiclib native/ecpmac.m \
  -framework Foundation -framework AppKit -framework IOKit -framework Metal -framework Security \
  -install_name @rpath/libecpmac.dylib -o native/libecpmac.dylib
