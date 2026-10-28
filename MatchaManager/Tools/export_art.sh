#!/bin/zsh
# Re-export background_claude.aseprite layers into Assets/Art/Layers.
# Run after editing the art in Aseprite, then use Matcha > Build Scene in Unity.
set -e
ASE="$HOME/Library/Application Support/Steam/steamapps/common/Aseprite/Aseprite.app/Contents/MacOS/aseprite"
HERE="${0:A:h}"
SRC="${1:-$HERE/../../Sprites/background_claude.aseprite}"
OUT="$HERE/../Assets/Art/Layers"
mkdir -p "$OUT"
"$ASE" -b --split-layers --all-layers "$SRC" --trim \
  --data "$OUT/layers.json" --format json-array --filename-format '{layer}' \
  --save-as "$OUT/{layer}.png"
echo "Exported to $OUT"
