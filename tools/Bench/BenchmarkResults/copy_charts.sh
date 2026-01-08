#!/bin/bash

# Copy benchmark chart SVG files to docs/data/charts

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../../.." && pwd)"
DOCS_CHARTS="$REPO_ROOT/docs/data/charts"

# Create target directories
mkdir -p "$DOCS_CHARTS/x86"
mkdir -p "$DOCS_CHARTS/arm"

# Copy AVX2 charts to x86
echo "Copying AVX2 charts to docs/data/charts/x86..."
cp "$SCRIPT_DIR/AVX2/"*.svg "$DOCS_CHARTS/x86/" 2>/dev/null && echo "  Done" || echo "  No SVG files found in AVX2"

# Copy Neon charts to arm
echo "Copying Neon charts to docs/data/charts/arm..."
cp "$SCRIPT_DIR/Neon/"*.svg "$DOCS_CHARTS/arm/" 2>/dev/null && echo "  Done" || echo "  No SVG files found in Neon"

echo ""
echo "Charts copied to:"
echo "  $DOCS_CHARTS/x86/"
echo "  $DOCS_CHARTS/arm/"
