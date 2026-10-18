# corvus-json-schema for Fortran

JSON Schema validation for Fortran: draft 4, 6, 7, 2019-09 and 2020-12. The module `corvus_json_schema` is a binding,
through `ISO_C_BINDING`, to the [corvus-json-schema C library](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/JsonSchemaForC.md),
so it gives the same results and annotations as every other Corvus implementation.

- **Conformant.** It passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft)
  except `draft4/optional/zeroTerminatedFloats.json`, as JSON text, as parsed documents and through a collector, and
  the suite's annotation tests.
- **Results and annotations.** Basic, Detailed and Verbose results, and the annotations of an evaluation.
- **Safe with input from outside.** A match of a regular expression is limited to one second by default, and a
  regular expression over a limit of size does not compile.
- **Compilers.** gfortran 11 or later and Intel ifx are tested, on Linux, macOS (gfortran) and Windows (ifx). The
  source is standard Fortran 2018. LLVM flang has not been tested.

## Install

The binding needs the C library, version 0.1.4 or later. Download the archive for your platform from the
[Corvus.JsonSchema releases](https://github.com/corvus-dotnet/Corvus.JsonSchema/releases?q=capi-v) (tags `capi-v<version>`)
and unpack it. It has `lib` (the shared and static libraries, a pkg-config file and a CMake package), `include` and,
on Windows, `bin` (the DLL).

### fpm

Add the package to your `fpm.toml`:

```toml
[dependencies]
corvus-json-schema = { git = "https://github.com/corvus-dotnet/corvus-json-schema-fortran.git", tag = "v0.1.0" }
```

and give the linker the C library's `lib` directory:

```sh
export LIBRARY_PATH=/path/to/corvus-json-schema-0.1.4-x86_64-unknown-linux-gnu/lib
export LD_LIBRARY_PATH=$LIBRARY_PATH   # macOS: DYLD_LIBRARY_PATH
fpm run
```

On Windows with ifx, put the `lib` directory on `LIB`, and add the system libraries the static library uses:
`fpm run --link-flag "kernel32.lib ntdll.lib userenv.lib ws2_32.lib dbghelp.lib"`.

### CMake

Build and install the binding, then use its package. CMake finds the C library through its own package, so give its
directory too:

```sh
cmake -S . -B build -DCMAKE_PREFIX_PATH=/path/to/corvus-json-schema-0.1.4-x86_64-unknown-linux-gnu
cmake --build build
cmake --install build --prefix /path/to/prefix
```

```cmake
find_package(corvus_json_schema_fortran 0.1 REQUIRED)
target_link_libraries(app PRIVATE corvus_json_schema::fortran)
```

`add_subdirectory` on the package's directory works too. The target links the C library's shared library.

## Validate

```fortran
program validate
  use corvus_json_schema
  implicit none
  type(validator_t) :: validator
  type(collector_t) :: collector
  integer :: i
  logical :: valid

  call validator%compile('{"type": "object", "required": ["id"], "properties": {"id": {"type": "integer"}}}')
  print "(a,l1)", "{""id"": 3} is valid: ", validator%is_valid('{"id": 3}')

  call collector%init(DETAILED)
  valid = validator%evaluate('{"id": "3"}', collector)
  print "(a,l1)", "{""id"": ""3""} is valid: ", valid
  do i = 1, collector%count()
    if (.not. collector%is_match(i)) then
      print "(a)", "  " // collector%instance_location(i) // ": " // collector%message(i)
    end if
  end do
end program validate
```

`validator%compile(schema, options)` compiles a schema from its JSON text. `is_valid(json)` validates JSON text,
evaluating only as far as the answer needs. To validate the same JSON more than once, parse it into a `document_t`
and call `is_valid_document(document)`. A compiled `validator_t` is immutable, and any number of threads may use one
at once.

## Errors

A procedure that can fail takes the optional arguments `stat` and `errmsg`, as the intrinsic statements do. `stat` is
`CJS_OK` or the status of the failure, and `errmsg` its message. When `stat` is absent, a failure stops the program
with the message (`error stop`).

```fortran
integer :: stat
character(:), allocatable :: errmsg

call validator%compile('{"$ref": "#/$defs/missing"}', stat=stat, errmsg=errmsg)
if (stat /= CJS_OK) print "(a)", errmsg
```

| Status | Meaning |
|---|---|
| `CJS_INVALID_ARGUMENT` | A value out of range, or a validator, document or collector that is not ready. |
| `CJS_INVALID_UTF8` | Text that is not UTF-8 (`last_error_offset()` is the offset of the first invalid byte). |
| `CJS_INVALID_JSON` | Text that is not JSON (`last_error_offset()` is where it was found). |
| `CJS_COMPILATION_FAILED` | A schema that does not compile: an unresolvable reference, a pattern that is not valid. |
| `CJS_DEPTH_EXCEEDED` | Evaluation recursed in place beyond the maximum depth. |
| `CJS_PATTERN_MATCH_LIMIT` | A match of a regular expression reached its limit: there is no answer. |
| `CJS_PATTERN_TOO_LARGE` | A regular expression over a limit of size. |
| `CJS_PANIC` | A bug in the library, caught. |

A validation that fails returns `.false.`, which is then not an answer.

## Options

```fortran
type(options_t) :: options
call options%set_default_dialect(DRAFT7)
call options%set_assert_format(.true.)
call validator%compile(schema, options)
```

| Procedure | Default | Meaning |
|---|---|---|
| `set_default_dialect(dialect)` | `DRAFT2020_12` | The dialect of a schema without `$schema`: `DRAFT4`, `DRAFT6`, `DRAFT7`, `DRAFT2019_09` or `DRAFT2020_12`. |
| `set_assert_format([enabled])` | as the vocabularies say | Whether `format` is asserted; without `enabled`, it follows the vocabularies again. |
| `set_assert_format_in_legacy_drafts(enabled)` | `.false.` | Assert `format` in drafts 4 to 7 when it follows the vocabularies. |
| `set_assert_content(enabled)` | `.true.` | Assert `contentEncoding` and `contentMediaType` in draft 7. |
| `set_base_uri(uri)` | none | The schema's base URI, when it has no `$id`. |
| `set_entry_point(reference)` | the root | A subschema to validate against, such as `"#/$defs/item"`. |
| `set_max_depth(depth)` | `128` | The deepest the evaluator recurses in place before it fails with `CJS_DEPTH_EXCEEDED`. |
| `set_pattern_match_limit(seconds)` | `1.0` | The seconds one match of a regular expression may take; zero or less turns the limit off. |
| `add_format(name, procedure)` | none | A custom format, taking precedence over the built-in one of that name. |
| `set_resolver(procedure)` | none | A resolver for referenced documents other than the standard metaschemas. |

A custom format is a function, and a resolver a subroutine, with these interfaces (`format_procedure` and
`resolver_procedure`):

```fortran
function is_even(value) result(valid)
  character(*), intent(in) :: value
  logical :: valid
end function

subroutine resolve(uri, json, found)
  character(*), intent(in) :: uri
  character(:), allocatable, intent(out) :: json
  logical, intent(out) :: found
end subroutine
```

A format runs during validation, on whichever thread validates. A resolver runs during compilation.
`validator%compile_uri(uri, options)` compiles the document at a URI through the resolver, or one of the standard
metaschemas.

## Limits on regular expressions

A schema and an instance may both come from outside, so a regular expression is bounded in what it costs. A match of
a `pattern` or `patternProperties` regular expression that takes longer than the pattern match limit is abandoned,
and the call fails with `CJS_PATTERN_MATCH_LIMIT`: the instance is then neither valid nor not valid. Compiling can
fail with it too, because a pattern of `patternProperties` is matched against the property names the schema
declares. A schema with a regular expression longer than 1,048,576 bytes, nested more than 1,000 groups deep or of
more than about 16,000 terms does not compile, and fails with `CJS_PATTERN_TOO_LARGE`.

## Results and annotations

```fortran
type(collector_t) :: collector
call collector%init(VERBOSE)
valid = validator%evaluate('{"name": "a"}', collector)
print "(a)", collector%annotations_json()   ! {"/name": {"title": {"#/properties/name": "Name"}}}
```

`BASIC` records the failures without messages, `DETAILED` adds the messages, and `VERBOSE` records every keyword,
passing ones and annotations included. Rows are numbered from 1, and at every level the root's own result is the last
row: `is_match(i)`, `message(i)`, `evaluation_location(i)`, `schema_location(i)` and `instance_location(i)`. A
collector is used by one thread at a time.

## Handles

`validator_t`, `options_t`, `document_t` and `collector_t` hold the C library's handles, and free them when they are
finalized. Assigning a `validator_t` gives a new handle to the same compiled schema. The other handles may not be
copied: assigning one stops the program. Options may be freed once the validators are compiled.

`library_version()` is the C library's version, and `binding_version` this binding's. `required_library_version` is
the oldest C library it needs, as `(major << 16) | (minor << 8) | patch`.

## Links

- The documentation: [JSON Schema for Fortran](https://corvus-oss.org/Corvus.JsonSchema/docs/json-schema-for-fortran.html)
- The C library: [JSON Schema for C](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/docs/JsonSchemaForC.md)
- The source, in the Corvus.JsonSchema repository: [src-fortran/corvus-json-schema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-fortran/corvus-json-schema)

The package is published to [corvus-json-schema-fortran](https://github.com/corvus-dotnet/corvus-json-schema-fortran)
from the Corvus.JsonSchema repository, where changes are made.

## Licence

Apache 2.0. See [LICENSE](LICENSE).
