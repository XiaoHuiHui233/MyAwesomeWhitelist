#!/usr/bin/env bash
# Bundle the game DLLs referenced by MyAwesomeWhitelist.csproj into dist/refs.zip.
# Upload it once (and again after every game update) to the "game-refs" GitHub
# release — CI downloads it to satisfy GAME_MANAGED during builds:
#
#   gh release create game-refs dist/refs.zip \
#     --title "Game refs" --notes "Human: Fall Flat Managed DLLs (build refs)"
#   # refreshing after a game update:
#   gh release upload game-refs dist/refs.zip --clobber
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
MANAGED="${GAME_MANAGED:-$HOME/Library/Application Support/Steam/steamapps/common/Human Fall Flat/Human.app/Contents/Resources/Data/Managed}"

# Keep in sync with the <Reference> items in src/MyAwesomeWhitelist/MyAwesomeWhitelist.csproj.
DLLS=(
  mscorlib System System.Core System.Xml
  Assembly-CSharp Assembly-CSharp-firstpass HumanAPI
  UnityEngine UnityEngine.CoreModule UnityEngine.IMGUIModule
  UnityEngine.TextRenderingModule UnityEngine.UI UnityEngine.UIModule
)

for dll in "${DLLS[@]}"; do
  [[ -f "$MANAGED/$dll.dll" ]] || { echo "missing: $MANAGED/$dll.dll" >&2; exit 1; }
done

mkdir -p "$ROOT/dist"
OUT="$ROOT/dist/refs.zip"
rm -f "$OUT"
(cd "$MANAGED" && zip -q "$OUT" "${DLLS[@]/%/.dll}")

echo "==> $OUT"
unzip -l "$OUT"
