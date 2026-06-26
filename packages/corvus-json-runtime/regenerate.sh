#!/usr/bin/env bash
# Regenerate src/index.ts from the C# RuntimeModuleSource() (the source of truth). The runtime is
# authored as const blocks in TypeScriptLanguageProvider.cs; this dumps the emitted form.
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"; ROOT="$(cd "$HERE/../.." && pwd)"
dotnet build "$ROOT/src/Corvus.Json.Cli/Corvus.Json.Cli.csproj" -c Debug >/dev/null
DLL="$(ls "$ROOT"/src/Corvus.Json.Cli/bin/Debug/net*/Corvus.Json.Cli.dll | tail -1)"
TMP="$(mktemp -d)"; echo '{"title":"X","type":"object"}' > "$TMP/x.json"
dotnet "$DLL" jsonschema "$TMP/x.json" --engine TypeScript --outputPath "$TMP" >/dev/null
cp "$TMP/corvus-runtime.ts" "$HERE/src/index.ts"; rm -rf "$TMP"
echo "regenerated src/index.ts"
