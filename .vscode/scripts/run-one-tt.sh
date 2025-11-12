#!/usr/bin/env bash
set -euo pipefail

path="${1:-}"

if [[ -z "$path" ]]; then
  echo "error: no active editor file. Open a .tt file and re-run." >&2
  exit 2
fi

if [[ ! -f "$path" ]]; then
  echo "error: file not found: $path" >&2
  exit 2
fi

case "$path" in
  *.tt) ;;
  *)
    echo "error: active file is not a .tt template: $path" >&2
    exit 2
    ;;
esac

dir="$(dirname "$path")"
file="$(basename "$path")"
cd "$dir"
echo "T4: $path"
dotnet tool run t4 -o "${file%.tt}.cs" "$file"
