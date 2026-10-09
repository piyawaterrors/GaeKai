#!/bin/bash
# ---------------------------------------------------------------------------
#  GaeKai build script for macOS - uses swiftc from the Xcode Command Line Tools
#  (xcode-select --install), so the full Xcode app is not required.
#
#    mac/build.sh          build dist/GaeKai.app and dist/GaeKai-mac.dmg
#    mac/build.sh test     build and run the unit tests
#    mac/build.sh icon     regenerate mac/Resources/GaeKai.icns
#
#  Set GAEKAI_VERSION=1.2.3 to stamp a version into the app (default 1.0.1).
# ---------------------------------------------------------------------------
set -euo pipefail
cd "$(dirname "$0")"

VERSION="${GAEKAI_VERSION:-1.0.1}"
DIST=../dist
BUILD=../dist/mac-build
MIN_MACOS=13.0
SWIFT_FLAGS=(-O -swift-version 5)

mkdir -p "$DIST" "$BUILD"

build_tests() {
    swiftc "${SWIFT_FLAGS[@]}" -parse-as-library -o "$BUILD/GaeKai.Tests" \
        tests/Tests.swift Sources/LayoutConverter.swift Sources/KeyboardLayouts.swift Sources/Hotkey.swift
    "$BUILD/GaeKai.Tests"
}

build_icon() {
    swiftc "${SWIFT_FLAGS[@]}" -parse-as-library -o "$BUILD/IconGen" tools/IconGen.swift Sources/AppIcon.swift
    "$BUILD/IconGen" "$BUILD/GaeKai.iconset"
    mkdir -p Resources
    iconutil -c icns "$BUILD/GaeKai.iconset" -o Resources/GaeKai.icns
    cp "$BUILD/GaeKai.iconset/icon_512x512@2x.png" "$DIST/icon-preview-mac.png"
    echo "Wrote mac/Resources/GaeKai.icns"
}

build_app() {
    # Universal binary: Apple Silicon + Intel
    for arch in arm64 x86_64; do
        swiftc "${SWIFT_FLAGS[@]}" -target "$arch-apple-macos$MIN_MACOS" \
            -o "$BUILD/GaeKai-$arch" Sources/*.swift
    done

    APP="$DIST/GaeKai.app"
    rm -rf "$APP"
    mkdir -p "$APP/Contents/MacOS" "$APP/Contents/Resources"
    lipo -create -output "$APP/Contents/MacOS/GaeKai" "$BUILD/GaeKai-arm64" "$BUILD/GaeKai-x86_64"
    sed "s/__VERSION__/$VERSION/g" Info.plist > "$APP/Contents/Info.plist"
    cp Resources/GaeKai.icns "$APP/Contents/Resources/"

    # Ad-hoc signature: Apple Silicon refuses to run unsigned code. This is not a Developer ID
    # signature, so Gatekeeper still asks the user to confirm on first launch.
    codesign --force --sign - --identifier io.github.piyawaterrors.gaekai "$APP"
    echo "Built dist/GaeKai.app ($VERSION)"

    # Disk image with a shortcut to /Applications for drag-and-drop install
    STAGE="$BUILD/dmg"
    rm -rf "$STAGE"
    mkdir -p "$STAGE"
    cp -R "$APP" "$STAGE/"
    ln -s /Applications "$STAGE/Applications"
    rm -f "$DIST/GaeKai-mac.dmg"
    hdiutil create -quiet -volname "GaeKai" -srcfolder "$STAGE" -format UDZO "$DIST/GaeKai-mac.dmg"
    echo "Built dist/GaeKai-mac.dmg"
}

case "${1:-}" in
    test) build_tests ;;
    icon) build_icon ;;
    "") build_app ;;
    *) echo "usage: $0 [test|icon]" >&2; exit 2 ;;
esac
