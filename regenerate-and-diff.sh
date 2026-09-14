#!/bin/bash
# Regenerate-and-diff gate for the V5 code generator: proves that a generator change leaves the
# generated output byte-identical.
#
#   ./regenerate-and-diff.sh snapshot   # with the generator BEFORE the change: regenerate everything and keep the output
#   ./regenerate-and-diff.sh check      # with the generator AFTER the change: regenerate again and diff against the snapshot
#
# What is regenerated:
#   - with the CLI (dotnet src/Corvus.Json.Cli/bin/$CONFIGURATION/net10.0/Corvus.Json.Cli.dll jsonschema ...):
#     src/Corvus.Text.Json.AsyncApi30/AsyncApi30.json, src/Corvus.Text.Json.OpenApi31/OpenApi31.json and the
#     tests/Corvus.Text.Json.Tests.MigrationSchemas/*.json models (the recipes from the AsyncApi30 README and docs/RunningTests.md);
#   - with the source generator: the obj/$CONFIGURATION/net10.0/generated output of the in-repo consumers listed in CONSUMERS
#     (they set EmitCompilerGeneratedFiles), by building each project.
# The CLI, the generator and the consumers are built unless --no-build is given. Extra MSBuild switches come from $MSBUILD_ARGS.
# The snapshot lives in $CORVUS_REGEN_SNAPSHOT (default /tmp/corvus-regenerate-and-diff).
#
# Known: CorvusJsonSchemaProgram*.g.cs embeds corvus-schema:///<guid>/ locations for rebased islands and synthetic $ref
# roots (Guid.NewGuid() in JsonSchemaRegistry), so it differs between two builds of the same generator; a difference
# confined to that file is reported but does not fail the check. The committed src/Corvus.Text.Json.AsyncApi30/Generated
# and tests/Corvus.Text.Json.Tests.MigrationModels.V5 files are regenerated on release and may lag the generator, so the
# check compares generator-before with generator-after, and only reports how far the committed files are from fresh output.
# Take the snapshot and the check in the same worktree: the generator's raw string literals (the emitted
# JsonSchemaTypeGeneratorAttribute, for one) take the line endings of its source files on disk, so a worktree whose .cs files
# are not checked out with CRLF (.gitattributes) builds a generator that emits different line endings.
set -u
ROOT=$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)
MODE=${1:-check}; shift || true
BUILD=1; for a in "$@"; do [ "$a" = --no-build ] && BUILD=0; done
CONFIGURATION=${CONFIGURATION:-Debug}
SNAP=${CORVUS_REGEN_SNAPSHOT:-/tmp/corvus-regenerate-and-diff}
TMP=${TMPDIR:-/tmp}/corvus-regenerate-and-diff-work
MSBUILD_ARGS=${MSBUILD_ARGS:-}
CLI=$ROOT/src/Corvus.Json.Cli/bin/$CONFIGURATION/net10.0/Corvus.Json.Cli.dll
CONSUMERS="src/Corvus.Text.Json.OpenApi20 src/Corvus.Text.Json.OpenApi30 src/Corvus.Text.Json.OpenApi31 src/Corvus.Text.Json.OpenApi32 src/Corvus.Text.Json.Patch tests/Corvus.Text.Json.Tests.GeneratedModels tests/Corvus.Text.Json.Tests.GeneratedModels.NativeEnums tests/Corvus.Text.Json.Tests.GeneratedModels.NativeEnums.Disabled tests/Corvus.Text.Json.Tests.GeneratedModels.NullOrUndefinedExceptNonNullDefaulted tests/Corvus.Text.Json.Tests.GeneratedModels.OptionalAsNullable"
FAIL=0
export MSBUILDDISABLENODEREUSE=1

build() { echo "--- build $1 $(date +%T)"; dotnet build "$ROOT/$1" -c $CONFIGURATION $MSBUILD_ARGS > "$TMP/build-$(basename "$1").log" 2>&1 || { echo "BUILD FAILED: $1"; grep -E " error " "$TMP/build-$(basename "$1").log" | sort -u | head -5; FAIL=1; }; }
generate() { dotnet "$CLI" jsonschema "$ROOT/$1" --rootNamespace "$2" --outputRootTypeName "$3" --outputPath "$TMP/cli/$4" >> "$TMP/cli-$4.log" 2>&1 || { echo "CLI FAILED: $1"; tail -3 "$TMP/cli-$4.log"; FAIL=1; }; }
compare() { # compare <snapshot dir> <fresh dir> <label>
  local d; d=$(diff -rq "$1" "$2"); local other prog
  other=$(grep -vc "CorvusJsonSchemaProgram" <<<"$d"); [ -z "$d" ] && other=0
  prog=$(grep -c "CorvusJsonSchemaProgram" <<<"$d")
  if [ "$other" = 0 ]; then echo "$3: identical ($(find "$2" -type f | wc -l) files$([ "$prog" != 0 ] && echo "; program image differs only in GUID-keyed synthetic root locations"))"
  else echo "$3: DIFFERENT"; head -10 <<<"$d"; FAIL=1; fi
}

rm -rf "$TMP"; mkdir -p "$TMP/cli/asyncapi30" "$TMP/cli/openapi31" "$TMP/cli/migration"
[ $BUILD = 1 ] && build src/Corvus.Json.Cli/Corvus.Json.Cli.csproj
echo "--- CLI regeneration $(date +%T)"
generate src/Corvus.Text.Json.AsyncApi30/AsyncApi30.json Corvus.Text.Json.AsyncApi30 AsyncApiDocument asyncapi30
generate src/Corvus.Text.Json.OpenApi31/OpenApi31.json Corvus.Text.Json.OpenApi31 OpenApiDocument openapi31
for s in person:MigrationPerson nested:MigrationNested composite:MigrationComposite item-array:MigrationItemArray int-vector:MigrationIntVector status-enum:MigrationStatusEnum tuple:MigrationTuple union:MigrationUnion pattern-union:MigrationPatternUnion with-defaults:MigrationWithDefaults; do
  generate "tests/Corvus.Text.Json.Tests.MigrationSchemas/migration-${s%%:*}.json" Corvus.Text.Json.Tests.MigrationModels.V5 "${s##*:}" migration
done
# Clear each consumer's net10.0 intermediate folder first: Roslyn never deletes emitted files, so an emitted-files folder can
# keep files from an older generator, and without its intermediate assembly the build cannot skip compilation.
for p in $CONSUMERS; do [ $BUILD = 1 ] && { rm -rf "$ROOT/$p/obj/$CONFIGURATION/net10.0"; build "$p"; }; done

case $MODE in
  snapshot)
    rm -rf "$SNAP"; mkdir -p "$SNAP/cli" "$SNAP/obj"; cp -r "$TMP/cli/." "$SNAP/cli/"
    for p in $CONSUMERS; do g="$ROOT/$p/obj/$CONFIGURATION/net10.0/generated"; [ -d "$g" ] && cp -r "$g" "$SNAP/obj/$(basename "$p")" || { echo "no generated output for $p (build it first)"; FAIL=1; }; done
    echo "snapshot: $(find "$SNAP" -type f | wc -l) files in $SNAP" ;;
  check)
    [ -d "$SNAP/cli" ] || { echo "no snapshot in $SNAP: run '$0 snapshot' with the generator before the change"; exit 2; }
    for d in asyncapi30 openapi31 migration; do compare "$SNAP/cli/$d" "$TMP/cli/$d" "CLI $d"; done
    for p in $CONSUMERS; do compare "$SNAP/obj/$(basename "$p")" "$ROOT/$p/obj/$CONFIGURATION/net10.0/generated" "$(basename "$p")"; done ;;
  *) echo "usage: $0 snapshot|check [--no-build]"; exit 2 ;;
esac
echo "committed AsyncApi30/Generated vs fresh CLI output (informational): $(diff -rq "$ROOT/src/Corvus.Text.Json.AsyncApi30/Generated" "$TMP/cli/asyncapi30" | wc -l) files differ"
[ $FAIL = 0 ] && echo "REGENERATE_AND_DIFF_OK ($MODE)" || { echo "REGENERATE_AND_DIFF_FAILED ($MODE)"; exit 1; }
