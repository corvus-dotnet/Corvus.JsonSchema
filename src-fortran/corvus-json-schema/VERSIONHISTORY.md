# Version History

The version history of `corvus-json-schema` for Fortran (`src-fortran/corvus-json-schema`), a binding to the
corvus-json-schema C library. It is versioned independently of the Corvus NuGet packages, whose history is in the
Corvus.JsonSchema repository's [VERSIONHISTORY.md](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/VERSIONHISTORY.md),
and of the C library, whose history is in [its own](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/src-rs/corvus-json-schema-capi/VERSIONHISTORY.md).

## V0.1.0

The first release of the Fortran binding.

### New features

- **JSON Schema validation for Fortran.** The module `corvus_json_schema` binds the corvus-json-schema C library
  (0.1.4 or later) through `ISO_C_BINDING`. It covers draft 4, 6, 7, 2019-09 and 2020-12, and passes all 7,966 tests
  of the JSON-Schema-Test-Suite with the same single exclusion as the other runners, as JSON text, as parsed
  documents and through a collector, and the 217 assertions of the suite's annotation tests.

- **Handles that free themselves.** `validator_t`, `options_t`, `document_t` and `collector_t` free the C library's
  handles when they are finalized. Assigning a `validator_t` gives a new handle to the same compiled schema, which
  any number of threads may use at once.

- **Errors as the intrinsic statements report them.** A procedure that can fail takes `stat` and `errmsg`; without
  `stat`, a failure stops the program with its message.

- **Options, custom formats and a resolver.** The default dialect, format and content assertion, a base URI, an
  entry point, the depth limit and the pattern match limit, custom formats as Fortran functions, and a resolver for
  referenced documents as a Fortran subroutine.

- **Limits on regular expressions.** A match of a regular expression is limited to one second by default
  (`CJS_PATTERN_MATCH_LIMIT`), and a regular expression over a limit of size does not compile
  (`CJS_PATTERN_TOO_LARGE`).

- **Results and annotations.** Basic, Detailed and Verbose results, with rows numbered from 1, and the annotations of
  a verbose evaluation as JSON text.

- **fpm and CMake.** The package builds with fpm (a git dependency) and with CMake (`corvus_json_schema::fortran`, and
  an installed package config). It is tested with gfortran 11 and 14 on Linux, gfortran on macOS, and Intel ifx on
  Linux and Windows.
