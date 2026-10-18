# JSON Schema for Fortran

[corvus-json-schema](https://github.com/corvus-dotnet/corvus-json-schema-fortran) is the Corvus JSON Schema evaluator
for Fortran: draft 4, 6, 7, 2019-09 and 2020-12. The module `corvus_json_schema` binds the [C library](JsonSchemaForC.md)
through `ISO_C_BINDING`, so it gives the same results and annotations as every other Corvus implementation.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, as JSON text, as parsed documents and through a collector, and the
  suite's annotation tests.
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations.
- **Compilers.** gfortran 11 or later and Intel ifx, tested on Linux, macOS (gfortran) and Windows (ifx). The source
  is standard Fortran 2018. LLVM flang has not been tested.

## Install

The binding needs the C library, 0.1.4 or later: unpack the archive for your platform from the
[C library's releases](https://github.com/corvus-dotnet/Corvus.JsonSchema/releases?q=capi-v). Then, with fpm, add the
package to your `fpm.toml`:

```toml
[dependencies]
corvus-json-schema = { git = "https://github.com/corvus-dotnet/corvus-json-schema-fortran.git", tag = "v0.1.0" }
```

and put the C library's `lib` directory on the linker's path (`LIBRARY_PATH`, and `LD_LIBRARY_PATH` or
`DYLD_LIBRARY_PATH` to run). With CMake, build and install the package, and give CMake the C library's directory:

```cmake
find_package(corvus_json_schema_fortran 0.1 REQUIRED)
target_link_libraries(app PRIVATE corvus_json_schema::fortran)
```

The package's [README](https://github.com/corvus-dotnet/corvus-json-schema-fortran#install) has the details, Windows
included.

## Validate

```fortran
use corvus_json_schema
type(validator_t) :: validator

call validator%compile('{"type": "object", "required": ["id"], "properties": {"id": {"type": "integer"}}}')
print *, validator%is_valid('{"id": 3}')    ! T
print *, validator%is_valid('{"id": "3"}')  ! F
```

`validator%compile(schema, options)` compiles a schema from its JSON text. A compiled `validator_t` is immutable, and
any number of threads may use one at once. To validate the same JSON more than once, parse it into a `document_t` and
call `is_valid_document(document)`.

## Errors

A procedure that can fail takes the optional arguments `stat` and `errmsg`, as the intrinsic statements do: `stat` is
`CJS_OK` or the status of the failure, and `errmsg` its message. Without `stat`, a failure stops the program with the
message. The statuses are `CJS_INVALID_ARGUMENT`, `CJS_INVALID_UTF8` and `CJS_INVALID_JSON` (with
`last_error_offset()`), `CJS_COMPILATION_FAILED`, `CJS_DEPTH_EXCEEDED`, `CJS_PATTERN_MATCH_LIMIT`,
`CJS_PATTERN_TOO_LARGE` and `CJS_PANIC`.

## Options

| Procedure of `options_t` | Default | Meaning |
|---|---|---|
| `set_default_dialect(dialect)` | `DRAFT2020_12` | The dialect of a schema without `$schema`. |
| `set_assert_format([enabled])` | as the vocabularies say | Whether `format` is asserted; without `enabled`, it follows the vocabularies again. |
| `set_assert_format_in_legacy_drafts(enabled)` | `.false.` | Assert `format` in drafts 4 to 7 when it follows the vocabularies. |
| `set_assert_content(enabled)` | `.true.` | Assert `contentEncoding` and `contentMediaType` in draft 7. |
| `set_base_uri(uri)` | none | The schema's base URI, when it has no `$id`. |
| `set_entry_point(reference)` | the root | A subschema to validate against, such as `"#/$defs/item"`. |
| `set_max_depth(depth)` | `128` | The deepest the evaluator recurses in place before it fails with `CJS_DEPTH_EXCEEDED`. |
| `set_pattern_match_limit(seconds)` | `1.0` | The seconds one match of a regular expression may take before the call fails with `CJS_PATTERN_MATCH_LIMIT`; zero or less turns the limit off. |
| `add_format(name, procedure)` | none | A custom format: a function of the string that returns whether it is valid. |
| `set_resolver(procedure)` | none | A subroutine from an absolute URI to the document's JSON text, for referenced documents. |

A schema and an instance may both come from outside, so a regular expression is bounded in what it costs. A match that
takes longer than the pattern match limit is abandoned, and the instance is then neither valid nor not valid.
Compiling can fail with it too, because a pattern of `patternProperties` is matched against the property names the
schema declares. A schema with a regular expression longer than 1,048,576 bytes, nested more than 1,000 groups deep or
of more than about 16,000 terms does not compile, and fails with `CJS_PATTERN_TOO_LARGE`.

## Results and annotations

```fortran
type(collector_t) :: collector
logical :: valid
integer :: i

call collector%init(DETAILED)
valid = validator%evaluate('{"id": "3"}', collector)
do i = 1, collector%count()
  if (.not. collector%is_match(i)) print "(a)", collector%instance_location(i) // ": " // collector%message(i)
end do
```

`BASIC` records the failures without messages, `DETAILED` adds the messages, and `VERBOSE` records every keyword,
passing ones and annotations included; `collector%annotations_json()` gives the annotations of a verbose evaluation
as JSON text. Rows are numbered from 1, and at every level the root's own result is the last row. A collector is used
by one thread at a time.

## Links

- Package: [corvus-json-schema-fortran](https://github.com/corvus-dotnet/corvus-json-schema-fortran)
- The C library it uses: [JSON Schema for C](JsonSchemaForC.md)
- The other languages: see [Other languages](OtherLanguages.md)
