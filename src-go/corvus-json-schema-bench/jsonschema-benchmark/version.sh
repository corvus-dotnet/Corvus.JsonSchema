#!/bin/sh

set -o errexit
set -o nounset

# The corvus-json-schema module version the image built (the Dockerfile takes the latest release).
docker run --rm --entrypoint cat jsonschema-benchmark/corvus-go /app/corvus-version
