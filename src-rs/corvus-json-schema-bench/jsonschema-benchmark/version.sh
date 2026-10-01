#!/bin/sh

set -o errexit
set -o nounset

# The version of the corvus-json-schema crate the image was built from.
docker run --rm --entrypoint cat jsonschema-benchmark/corvus-rs /corvus/src-rs/corvus-json-schema/Cargo.toml \
  | sed -n 's/^version *= *"\(.*\)"/\1/p' | head -n 1
