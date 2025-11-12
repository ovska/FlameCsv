#!/usr/bin/env bash
set -euo pipefail

root="${1:-$PWD}"

failed=0

# Find all .tt files under the workspace, skip common junk dirs.
# Uses -print0 to handle spaces.
while IFS= read -r -d '' tt; do
  echo "T4: ${tt}"
  # Run from the template's directory so relative includes work.
  dir="$(dirname "$tt")"
  file="$(basename "$tt")"
  if ( cd "$dir" && dotnet tool run t4 -o "${file%.tt}.cs" "$file" ); then
    :
  else
    echo "T4 failed: ${tt}" >&2
    failed=1
  fi
done < <(find "$root" \
  \( -path "*/.git" -o -path "*/bin" -o -path "*/obj" \) -prune -o \
  -type f -name "*.tt" -print0)

exit "$failed"
