#!/usr/bin/env bash
set -euo pipefail

echo "SCRIPT VERSION: $(date) $(sha256sum "$0" | awk "{print \$1}")" >&2

RAM=/ram
SRC=$RAM/src
OUT=$RAM/findings
SEEDS=$RAM/Testcases


mkdir -p "$SRC" "$OUT" "$SEEDS"

rsync -a --delete /repo/ "$SRC/"
rsync -a --delete "$SRC/tools/Fuzzing/Testcases/" "$SEEDS/"

cd "$SRC/tools/Fuzzing"

# Publish into a clean folder (SharpFuzz’s recommended approach)
rm -rf bin findings
dotnet publish -c Release -o bin

# Instrument (all non-System DLLs except SharpFuzz’s own + your entry DLL)
EXCLUSIONS=("dnlib.dll" "SharpFuzz.dll" "SharpFuzz.Common.dll")
ENTRY_DLL="$(basename "$(ls -1 *.csproj | head -n1)" .csproj).dll"

shopt -s nullglob
for dll in bin/*.dll; do
  name="$(basename "$dll")"
  skip=0
  for ex in "${EXCLUSIONS[@]}" "$ENTRY_DLL"; do
    [[ "$name" == "$ex" ]] && skip=1
  done
  [[ "$name" == System.*.dll ]] && skip=1
  [[ $skip -eq 1 ]] && continue

  echo "[*] Instrumenting $name"
  sharpfuzz "$dll"
done

# Critical: bypass AFL’s “binary is not instrumented” check (dotnet isn’t)
export AFL_SKIP_BIN_CHECK=1

# Recommended defaults from SharpFuzz script
export AFL_SKIP_CPUFREQ=1

# Run AFL++ (use @@ if your OutOfProcess runner expects a file path)
afl-fuzz -i "$SEEDS" -o "$OUT" -t 10000 -m none -- \
  dotnet "bin/$ENTRY_DLL" @@
