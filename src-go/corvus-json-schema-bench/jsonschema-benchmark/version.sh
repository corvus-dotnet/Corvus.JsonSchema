#!/bin/sh

set -o errexit
set -o nounset

# The version of the corvus-json-schema module in go.mod.
sed -n 's|.*github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema v\([^ ]*\).*|\1|p' \
  implementations/corvus-go/go.mod | head -n 1
