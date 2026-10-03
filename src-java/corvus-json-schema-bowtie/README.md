# Bowtie harness

A [Bowtie](https://github.com/bowtie-json-schema/bowtie) harness for the Java evaluator. It speaks IHOP (one JSON
request per line on standard input, one response per line on standard output):
- `start` reports the implementation and its dialects;
- `dialect` sets the dialect for schemas without `$schema`;
- `run` compiles the case's schema with the case's `registry` as the document resolver and validates each instance.
  For `annotations` output, it evaluates through a verbose results collector and reports each annotation with its
  instance location and `#…` keyword location.
- `stop` exits.

An exception, or an evaluation beyond the maximum depth, is reported for that case or instance as an error, not a
crash. The harness lives in the library's package (`io.github.corvusdotnet.jsonschema`) to read requests through the
document's own accessors.

## Checking the harness without containers

```sh
./mvnw test
```

`IhopTest` starts the harness in its own JVM and drives it over IHOP exactly as Bowtie does. It sends every required
case of the JSON-Schema-Test-Suite for draft 4, 6, 7, 2019-09 and 2020-12 with the suite's remotes as the registry,
and the suite's annotation tests, and compares both with the expected results. It needs the library in the local
Maven repository (`./mvnw install` in `../corvus-json-schema`).

## Running it under Bowtie

Copy this directory to `implementations/java-corvus-jsonschema` in a Bowtie checkout, build the image, then:

```sh
bowtie suite -i localhost/java-corvus-jsonschema 2020-12 | bowtie summary --show failures
```

Until the library is on Maven Central, the image builds it from this repository (the `CORVUS_REF` build argument,
default `main`), with the harness copied beside it.
