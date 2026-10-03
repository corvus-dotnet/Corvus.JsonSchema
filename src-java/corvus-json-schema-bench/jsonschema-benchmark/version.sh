#!/bin/sh

set -o errexit
set -o nounset

# The corvus-json-schema version the image built (pom.xml takes the latest release).
docker run --rm --entrypoint cat jsonschema-benchmark/corvus-java /app/corvus-version
