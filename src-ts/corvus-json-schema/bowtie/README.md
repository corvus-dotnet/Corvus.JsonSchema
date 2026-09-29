# Bowtie harness

A [Bowtie](https://github.com/bowtie-json-schema/bowtie) harness for the TypeScript evaluator. It speaks IHOP (one JSON
request per line on standard input, one response per line on standard output): `start` reports the implementation
and its dialects, `dialect` sets the dialect for schemas without `$schema`, `run` compiles the case's schema with the
case's `registry` as the document resolver and validates each instance (for `annotations` output, through a verbose
results collector, reporting each annotation with its instance location and `#…` keyword location), and `stop`
exits.

## Checking the harness without containers

```sh
npm run build
node test/bowtie-ihop.mjs
```

This drives `bowtie_corvus.js` over IHOP exactly as Bowtie does, sending every required case of the
JSON-Schema-Test-Suite with the suite's remotes as the registry, and compares the results with the expected ones.

## Running it under Bowtie

Copy this directory to `implementations/js-corvus-jsonschema` in a Bowtie checkout, then:

```sh
bowtie suite -i localhost/js-corvus-jsonschema 2020-12 | bowtie summary --show failures
```

Bowtie reads the suite from GitHub by default; to use a local checkout pass the directory instead, e.g.
`bowtie suite -i localhost/js-corvus-jsonschema ../../JSON-Schema-Test-Suite/tests/draft2020-12`. Run that way
(Bowtie 2026.7.4, 2026-09-29), the harness reports no failures, errors or skips for draft 4, 6, 7, 2019-09 and 2020-12.
`bowtie annotation-suite` (Bowtie's `main` branch, 2026-09-29) likewise reports none for every dialect, e.g.
`bowtie annotation-suite -i localhost/js-corvus-jsonschema --dialect 2019-09 ../../JSON-Schema-Test-Suite/annotations/tests`.

Until the package is published to npm the image builds it from this repository (`CORVUS_REF` build argument, default
`main`). Once it is published, replace the clone-and-build step with
`npm install @corvus-dotnet/json-schema@${IMPLEMENTATION_VERSION}` as the other JavaScript harnesses do.
