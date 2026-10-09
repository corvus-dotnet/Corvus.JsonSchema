#!/bin/sh

set -o errexit
set -o nounset

# The CorvusJsonSchema version the image built (the Dockerfile takes the latest in Julia's General registry).
docker run --rm --entrypoint cat jsonschema-benchmark/corvus-jl /app/corvus-version
