# Bowtie harness

A [Bowtie](https://github.com/bowtie-json-schema/bowtie) harness for the Go evaluator. It speaks IHOP (one JSON
request per line on standard input, one response per line on standard output):
- `start` reports the implementation and its dialects;
- `dialect` sets the dialect for schemas without `$schema`;
- `run` compiles the case's schema with the case's `registry` as the document resolver and validates each instance.
  For `annotations` output, it evaluates through a verbose results collector and reports each annotation with its
  instance location and `#…` keyword location.
- `stop` exits.

A compilation error, a panic, or an evaluation beyond the maximum depth is reported for that case or instance as an
error, not a crash. The harness is a module of its own and reads requests with `encoding/json`, passing each schema
and instance to the library as the JSON text it arrived as.

## Checking the harness without containers

```sh
go test -p 4 ./...
```

`ihop_test.go` runs the harness in its own process and drives it over IHOP exactly as Bowtie does. It sends every
required case of the JSON-Schema-Test-Suite for draft 4, 6, 7, 2019-09 and 2020-12 with the suite's remotes as the
registry, and the suite's annotation tests, and compares both with the expected results. The suite is the
repository's submodule, or `JSON_SCHEMA_TEST_SUITE`. The library is the one in this checkout, through the `replace`
directive in `go.mod`.

## Running it under Bowtie

Copy this directory to `implementations/go-corvus-jsonschema` in a Bowtie checkout, build the image, then:

```sh
bowtie suite -i localhost/go-corvus-jsonschema 2020-12 | bowtie summary --show failures
```

Bowtie reads the suite from GitHub by default. To use a local checkout, pass the directory instead:
`bowtie suite -i localhost/go-corvus-jsonschema ../../JSON-Schema-Test-Suite/tests/draft2020-12`. Run that way
(Bowtie 2025.8.1, 2026-10-07), the harness reports no failures, errors or skips for draft 4, 6, 7, 2019-09 and
2020-12.

The image builds the library from this repository (the `CORVUS_REF` build argument, default `main`, which can also be
a release tag such as `src-go/corvus-json-schema/v0.1.0`), with the harness copied beside it. To build it from a
branch that is not on GitHub, mount the repository and name it:

```sh
podman build -t localhost/go-corvus-jsonschema --volume "$(git rev-parse --git-common-dir):/repo.git:ro" \
    --build-arg CORVUS_REPO=file:///repo.git --build-arg CORVUS_REF=<branch> .
```

Bowtie talks to the container engine through `DOCKER_HOST`. With podman, serve its API first
(`podman system service --time=180 unix:///run/user/$UID/podman/podman.sock`) and point `DOCKER_HOST` at that socket.
