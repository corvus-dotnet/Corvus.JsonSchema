#!/bin/sh

set -o errexit
set -o nounset

docker run --rm --entrypoint node jsonschema-benchmark/corvus-ts \
  -p "require('/app/node_modules/@corvus-dotnet/json-schema/package.json').version"
