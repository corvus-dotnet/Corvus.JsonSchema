# Bowtie harness

A [Bowtie](https://github.com/bowtie-json-schema/bowtie) harness for the Rust evaluator. It speaks IHOP (one JSON
request per line on standard input, one response per line on standard output):
- `start` reports the implementation and its dialects;
- `dialect` sets the dialect for schemas without `$schema`;
- `run` compiles the case's schema with the case's `registry` as the document resolver and validates each instance.
  For `annotations` output, it evaluates through a verbose results collector and reports each annotation with its
  instance location and `#…` keyword location.
- `stop` exits.

A panic or an evaluation beyond the maximum depth is reported for that case or instance as an error, not a crash.

## Checking the harness without containers

```sh
cargo test
```

`tests/ihop.rs` drives the built harness over IHOP exactly as Bowtie does. It sends every required case of the
JSON-Schema-Test-Suite for draft 4, 6, 7, 2019-09 and 2020-12 with the suite's remotes as the registry. It also runs
the suite's annotation tests, and compares both with the expected results.

## Running it under Bowtie

Copy this directory to `implementations/rust-corvus-jsonschema` in a Bowtie checkout, build the image, then:

```sh
bowtie suite -i localhost/rust-corvus-jsonschema 2020-12 | bowtie summary --show failures
```

Bowtie reads the suite from GitHub by default. To use a local checkout, pass the directory instead:
`bowtie suite -i localhost/rust-corvus-jsonschema ../../JSON-Schema-Test-Suite/tests/draft2020-12`. Run that way
(Bowtie 2026.7.4, 2026-09-30), the harness reports no failures, errors or skips for draft 4, 6, 7, 2019-09 and 2020-12.
`bowtie annotation-suite` (Bowtie's `main` branch, 2026-09-30) reports no mismatches for any dialect, for example
`bowtie annotation-suite -i localhost/rust-corvus-jsonschema --dialect 2019-09 ../../JSON-Schema-Test-Suite/annotations/tests`.

Until the crate is published to crates.io, the image builds it from this repository (the `CORVUS_REF` build argument,
default `main`), with the harness copied beside the crate. Once it is published, depend on
`corvus-json-schema = "${IMPLEMENTATION_VERSION}"` instead, as the other Rust harnesses do.
