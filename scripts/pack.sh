#!/usr/bin/env bash
# Build the plugin and assemble a Thunderstore-uploadable zip into dist/.
#   scripts/pack.sh [version]   — override the version from manifest.json
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT="$ROOT/src/MyAwesomeWhitelist/bin/Release/netstandard2.0"
DIST="$ROOT/dist"

VERSION="${1:-}"
if [[ -z "$VERSION" ]]; then
  VERSION=$(python3 -c "import json,sys; print(json.load(open('$ROOT/thunderstore/manifest.json'))['version_number'])")
fi

echo "==> Building MyAwesomeWhitelist $VERSION"
dotnet build "$ROOT/src/MyAwesomeWhitelist/MyAwesomeWhitelist.csproj" -c Release

STAGE="$(mktemp -d)/pkg"
mkdir -p "$STAGE/plugins"
cp "$ROOT/thunderstore/manifest.json" "$STAGE/"
# An explicit version argument overrides version_number inside the packed
# manifest too — otherwise the zip name would lie about its contents.
if [[ -n "${1:-}" ]]; then
  python3 - "$STAGE/manifest.json" "$VERSION" <<'PY'
import json, sys
path, version = sys.argv[1], sys.argv[2]
with open(path) as f:
    manifest = json.load(f)
manifest["version_number"] = version
with open(path, "w") as f:
    json.dump(manifest, f, indent=2)
PY
fi
cp "$OUT/MyAwesomeWhitelist.dll" "$STAGE/plugins/"
[[ -f "$ROOT/thunderstore/icon.png" ]] && cp "$ROOT/thunderstore/icon.png" "$STAGE/"
[[ -f "$ROOT/README.md" ]] && cp "$ROOT/README.md" "$STAGE/"

mkdir -p "$DIST"
ZIP="$DIST/MyAwesomeWhitelist-$VERSION.zip"
rm -f "$ZIP"
(cd "$STAGE" && zip -r -q "$ZIP" .)

echo "==> Packed $ZIP"
unzip -l "$ZIP"
