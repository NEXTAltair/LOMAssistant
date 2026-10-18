#!/usr/bin/env bash
# 重新生成随时存档的兼容契约（LOMAssistant.Features.AnytimeSave/contract.tsv.gz）。
# 游戏更新后先只对比：./tools/anytime-contract/generate.sh -   （列出改动的方法）
# 确认改动不影响存读档后再生成：./tools/anytime-contract/generate.sh
set -euo pipefail
cd "$(dirname "$0")/../.."
GAME="${GAME:-D:/Steam/steamapps/common/LegendOfMortal}"
CSC="$(ls /c/Windows/Microsoft.NET/Framework64/v4*/csc.exe | head -1)"
OUT=tools/anytime-contract/bin
mkdir -p "$OUT"
cp "$GAME/BepInEx/core/Mono.Cecil.dll" "$OUT/"
# 旧版 csc 不认正斜杠路径
"$CSC" -nologo -out:"$(cygpath -w "$OUT/generate.exe")" -r:"$(cygpath -w "$OUT/Mono.Cecil.dll")" "$(cygpath -w tools/anytime-contract/Generate.cs)" "$(cygpath -w LOMAssistant.Features.AnytimeSave/CompatibilityContract.cs)"
C=LOMAssistant.Features.AnytimeSave/contract.tsv.gz
if [ "${1:-}" = "-" ]; then
	"$OUT/generate.exe" "$GAME/Mortal_Data/Managed" - "$C"
else
	"$OUT/generate.exe" "$GAME/Mortal_Data/Managed" "$C" $([ -f "$C" ] && echo "$C")
fi
