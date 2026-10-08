# Bowtie harness

A [Bowtie](https://github.com/bowtie-json-schema/bowtie) harness for the Julia evaluator. It speaks IHOP (one JSON
request per line on standard input, one response per line on standard output):
- `start` reports the implementation and its dialects;
- `dialect` sets the dialect for schemas without `$schema`;
- `run` compiles the case's schema with the case's `registry` as the document resolver and validates each instance.
  For `annotations` output, it evaluates through a verbose results collector and reports each annotation with its
  instance location and `#…` keyword location.
- `stop` exits.

A compilation error, an exception, or an evaluation beyond the maximum depth is reported for that case or instance as
an error, not a crash. The harness is a package of its own, `CorvusJsonSchemaBowtie`, with `bowtie.jl` to start it,
so that Julia compiles it once and not in every process. Julia has no JSON reader in its standard library, so the
harness has a small one (`src/rawjson.jl`) that finds each value in a request and keeps it as the text it arrived
as. Each schema and instance goes to the library as that text.

## Checking the harness without containers

```sh
julia --project=. -e 'using Pkg; Pkg.develop(path="../CorvusJsonSchema")'
julia --project=. -t 4 test/runtests.jl
```

`test/runtests.jl` runs the harness in its own process and drives it over IHOP exactly as Bowtie does. It sends every
required case of the JSON-Schema-Test-Suite for draft 4, 6, 7, 2019-09 and 2020-12 with the suite's remotes as the
registry, and the suite's annotation tests, and compares both with the expected results. The suite is the
repository's submodule, or `JSON_SCHEMA_TEST_SUITE`. The library is the one in this checkout, through `[sources]` in
`Project.toml` (which Julia 1.11 and later read) and the `Pkg.develop` call above (which Julia 1.10 needs).
`Manifest.toml` is not committed.

## Running it under Bowtie

Bowtie has no Julia implementation yet (2026-10-08), so there is no convention of another Julia harness to follow.
The harness is named as Bowtie names them, by language and then implementation: `julia-corvus-jsonschema`. Bowtie's
harnesses now live in a repository each, made from its
[test-harness-template](https://github.com/bowtie-json-schema/test-harness-template). This directory is what that
repository would hold. Build the image, then:

```sh
bowtie suite -i localhost/julia-corvus-jsonschema 2020-12 | bowtie summary --show failures
```

Bowtie reads the suite from GitHub by default. To use a local checkout, pass the directory instead:
`bowtie suite -i localhost/julia-corvus-jsonschema ../../JSON-Schema-Test-Suite/tests/draft2020-12`. Run that way
(Bowtie 2025.8.1, 2026-10-08), the harness reports no failures, errors or skips for draft 4, 6, 7, 2019-09 and
2020-12, and `bowtie smoke` succeeds.

The image builds the library from this repository (the `CORVUS_REF` build argument, default `main`, which can also be
a release tag such as `CorvusJsonSchema-v0.1.0`), with the harness copied beside it. To build it from a branch that
is not on GitHub, mount the repository and name it:

```sh
podman build -t localhost/julia-corvus-jsonschema --volume "$(git rev-parse --path-format=absolute --git-common-dir):/repo.git:ro" \
    --build-arg CORVUS_REPO=file:///repo.git --build-arg CORVUS_REF=<branch> .
```

The image is the official Julia image, on Debian: Julia publishes no build for musl, so there is no Alpine image to
use. It compiles the library and the harness into package images when it is built, for any processor of the
architecture (`JULIA_CPU_TARGET=generic`), so a container starts with both compiled wherever it runs.

Bowtie talks to the container engine through `DOCKER_HOST`. With podman, serve its API first
(`podman system service --time=180 unix:///run/user/$UID/podman/podman.sock`) and point `DOCKER_HOST` at that socket.
