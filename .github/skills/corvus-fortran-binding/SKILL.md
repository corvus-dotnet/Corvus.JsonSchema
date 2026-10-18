---
name: corvus-fortran-binding
description: >
  Work on the Fortran binding to the corvus-json-schema C library (src-fortran/corvus-json-schema, fpm and CMake
  package corvus-json-schema, module corvus_json_schema): the ISO_C_BINDING interfaces, the handle types and their
  finalizers, the callback trampolines for custom formats and the resolver, stat/errmsg reporting, the suite,
  annotation and API tests, the CMake build and package config, and the release through the publication
  repository. USE FOR: changing or debugging the binding, keeping it building on gfortran 11+ and Intel ifx,
  exposing a new C library function, releasing the package.
---

# The Fortran binding

`src-fortran/corvus-json-schema` binds the C library (`src-rs/corvus-json-schema-capi`, released as `capi-v<version>`)
through `ISO_C_BINDING`. It holds no evaluator of its own: conformance, performance and pattern behaviour are the C
library's, and so the Rust crate's (`docs/JsonSchemaForC.md`, `src-rs/corvus-json-schema-capi/DESIGN.md`, and the
`corvus-ecma-regex` skill for patterns). The binding's job is a faithful, safe Fortran surface.

## Layout

- `src/corvus_json_schema.f90`: the whole module. The `bind(c)` interface block mirrors
  `src-rs/corvus-json-schema-capi/include/corvus_json_schema.h`: keep them in step when the C API changes.
- `test/suite_support.f90`: file reading, a portable directory listing (`execute_command_line`: `ls` or `dir /b`),
  the suite's remote resolver, and a scanner that cuts JSON values out of a text as they are written (the suite's
  `1.0` must reach the validator as `1.0`).
- `test/suite_test.f90`: the JSON-Schema-Test-Suite as JSON text, as a document and through a verbose collector.
  7,893 run plus 73 leap-second format cases skipped: 7,966.
- `test/annotations_test.f90`: the annotation suite (217 assertions). JSON values are compared with the library
  itself, by validating the produced object against `{"const": <expected>}`; `api_test` checks that this comparison
  can fail.
- `test/api_test.f90`: the API, option by option.
- `example/validate.f90`: the README's example, run by CI with fpm and as a CMake consumer.
- `CMakeLists.txt` and `cmake/corvus_json_schema_fortranConfig.cmake.in`: the target `corvus_json_schema::fortran`,
  ctest, and an installed package config.

## Conventions

- **Handles.** Each type holds one `type(c_ptr)` and frees it in a `final` procedure. Procedures that create a
  handle are subroutines (`call v%compile(...)`), never functions returning a handle type, so no temporary is
  finalized. Assignment is defined: a `validator_t` clones (`cjs_validator_clone`), the others stop the program.
- **Errors.** `stat` and `errmsg`, as the intrinsic statements. Every procedure reports into a LOCAL
  `character(:), allocatable :: message` and copies it to `errmsg` itself. Never pass an optional deferred-length
  character dummy on to another procedure: gfortran 11 crashes (SIGSEGV) or loses the value.
- **Callbacks.** A Fortran procedure is boxed (`format_box`, `resolver_box`), `c_loc` of the box is the
  `user_data`, a `bind(c)` trampoline unboxes and calls it, and the C library frees the box through the free
  callback when the options and its validators are gone.
- **Indexing.** Collector rows are 1-based in Fortran, 0-based in C.
- **Names.** Fortran is case-insensitive: a local named `basic` hides the constant `BASIC`.

## Building and testing locally

There is no system gfortran: use the podman images built from `~/src/corvus-session-tools/fortran-dev`
(`localhost/fortran-corvus-dev:14` and `:11`, with fpm 0.13 and the C library under `/usr/local`):

```sh
podman run --rm --userns=keep-id -v "$PWD":/repo:Z -w /repo/src-fortran/corvus-json-schema \
  localhost/fortran-corvus-dev:14 fpm test --flag "-Wall -Wextra -std=f2018 -fimplicit-none -g -fcheck=all"
```

The tests find the suite at `../../JSON-Schema-Test-Suite` (initialize the submodule) or `JSON_SCHEMA_TEST_SUITE`.
Intel ifx and Windows are tested in CI only (`.github/workflows/fortran.yml`).

## Releasing

`fortran-publish.yml` publishes when `fpm.toml`'s version has no release in `corvus-dotnet/corvus-json-schema-fortran`.
Bump the version in `fpm.toml`, `binding_version`, `CMakeLists.txt`, `test/api_test.f90` and `VERSIONHISTORY.md`
together; CI checks them. The details and the one-time set-up are in `docs/ReleaseProcess.md`, "The Fortran binding".
