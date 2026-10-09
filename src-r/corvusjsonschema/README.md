# corvusjsonschema

A JSON Schema validator for R (draft 4, 6, 7, 2019-09 and 2020-12), built on the
[corvus-json-schema](https://crates.io/crates/corvus-json-schema) Rust crate, the Rust port of the Corvus.Text.Json V5
standalone evaluator.

- **Conformant**: passes the JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`, which the other Corvus evaluators also exclude (a JSON parser reads
  `1.0` as an integer).
- **Fast**: R lists and vectors are read in place (nothing is converted or copied), and JSON text is validated without
  creating R values for it.
- **No R dependencies**: the package needs R 4.2 or later and nothing else at run time.

## Install

From [R-universe](https://corvus-dotnet.r-universe.dev), which serves binary packages for Windows and macOS and source
packages for Linux:

```r
install.packages("corvusjsonschema", repos = c("https://corvus-dotnet.r-universe.dev", "https://cloud.r-project.org"))
```

Installing from source needs a Rust toolchain (cargo and rustc, 1.89 or later). See
[Building from source](#building-from-source).

## Usage

```r
library(corvusjsonschema)

validator <- compile_schema('{
  "type": "object",
  "required": ["id"],
  "properties": {"id": {"type": "integer", "minimum": 1}}
}')

is_valid(validator, list(id = 3))             # TRUE: a list, read in place
is_valid_json(validator, '{"id": 0}')         # FALSE: JSON text, parsed in Rust
is_valid_json(validator, c('{"id": 1}', '{}', NA))   # TRUE FALSE NA: one verdict for each text

result <- evaluate(validator, list(id = "three"))
result$valid                                   # FALSE
result$results[, c("instance_location", "message")]
#>   instance_location                                       message
#> 1               /id The value was expected to be of type 'integer'
```

Compile a schema once and keep the validator: compiling does the work that makes validating fast.

A schema is JSON text or R values:

```r
compile_schema(list(type = "object", required = list("id"), properties = list(id = list(type = "integer"))))
```

`evaluate()` and `evaluate_json()` evaluate every keyword and report at a level: `"basic"` (the failures),
`"detailed"` (the failures, with the subschemas they belong to) or `"verbose"` (every keyword, and the annotations).
The results are a data frame, and at the verbose level so are the annotations:

```r
titled <- compile_schema('{"title": "Person", "properties": {"name": {"title": "Name"}}}')
evaluate(titled, list(name = "Ada"), level = "verbose")$annotations
#>   instance_location keyword   schema_location  value
#> 1             /name   title #/properties/name   Name
#> 2                     title                 # Person
```

### Options

`compile_schema()` takes:

| Option | Meaning |
| --- | --- |
| `default_dialect` | The dialect of a schema with no `$schema`: `"draft4"`, `"draft6"`, `"draft7"`, `"draft2019-09"` or `"draft2020-12"` (the default) |
| `assert_format` | Whether `format` is asserted. `NA` (the default) follows the dialect's vocabularies |
| `assert_format_in_legacy_drafts` | With `assert_format = NA`, assert `format` in draft 4 to 7 |
| `assert_content` | Assert `contentEncoding` and `contentMediaType` in draft 7 (the default) |
| `formats` | A named list of functions, each taking a string and returning whether it is valid for that format |
| `resolver` | A function from an absolute URI to the document: JSON text, R values, or `NULL` when unknown |
| `base_uri` | The base URI of the schema |
| `entry_point` | A reference to the subschema to validate against, such as `"#/$defs/item"` |
| `max_depth` | How many times a schema may refer to itself without moving through the value (128) |

```r
even <- compile_schema('{"format": "even-length"}', assert_format = TRUE,
                       formats = list("even-length" = function(s) nchar(s) %% 2 == 0))

remote <- compile_schema('{"$ref": "https://example.com/item.json"}',
                         resolver = function(uri) if (uri == "https://example.com/item.json") '{"type": "string"}')
```

### Conditions

Every failure is a condition that inherits from `corvus_json_schema_error`:

| Class | Raised when |
| --- | --- |
| `corvus_compilation_error` | A schema cannot be compiled (an unresolved reference, an invalid pattern) |
| `corvus_invalid_json_error` | Schema or instance text is not JSON |
| `corvus_value_error` | An R value is not a JSON value |
| `corvus_depth_error` | A schema refers to itself more than `max_depth` times without moving through the value |
| `corvus_callback_error` | A custom format function signalled an error |

## Values

An R value reads as JSON the way [jsonlite](https://cran.r-project.org/package=jsonlite)'s
`fromJSON(simplifyVector = FALSE)` gives JSON values:

| R | JSON |
| --- | --- |
| `NULL` | `null` |
| A list with names | An object. Where two elements have one name, the first is read |
| A list without names | An array. `list()` is `[]`; an empty named list is `{}` |
| A logical, integer, double or character vector of length one | A scalar |
| Such a vector of any other length | An array of scalars (its names are ignored) |
| A vector of length one wrapped in `I()` | An array of one element |
| `NA` of any type | `null` |

- A whole double, such as `3`, is an integer as well as a number, in every dialect: R writes `3` for a double.
- Strings are read as UTF-8 whatever encoding they are marked with.
- Nothing else is a JSON value: not `NaN` or an infinity, a function, an environment, a raw or complex vector, or an
  object with a class, such as a data frame, a factor or a date. Convert it first. A value is read only where the
  schema examines it, so a value the schema ignores is never an error.
- An R string cannot hold a NUL character, so JSON text with one in a string can only be validated as text.
- A compiled schema holds native code. It does not survive `saveRDS()` or a saved workspace: compile it again in a new
  session.

## How it works

The package's native code is a Rust static library (`src/rust`) linked into the package. It implements the crate's
`Instance` trait over R's own values, so the evaluator walks a list through R's C API with no intermediate tree: a
property is found by scanning the list's names, and an element of an atomic vector is read where it lies. JSON text
goes to the crate's own parser, which keeps the text as bytes and evaluates it in place, allocating nothing in the
steady state.

The R API of the C level is declared by hand, so the build needs no crates beyond the evaluator and its dependencies.
Failures come back to R as values and are raised there, so no R error unwinds through Rust code.

## Building from source

```sh
R CMD INSTALL src-r/corvusjsonschema
```

This needs cargo and rustc 1.89 or later on the `PATH` (or in `~/.cargo/bin`), and on Windows Rtools with the
`x86_64-pc-windows-gnu` Rust target (`rustup target add x86_64-pc-windows-gnu`). Cargo takes the crates from crates.io.

The tests are plain R scripts in `tests/`. `test_suite.R` runs the JSON-Schema-Test-Suite, found through the
`JSON_SCHEMA_TEST_SUITE` environment variable or the repository's submodule, and needs jsonlite.

## License

Apache-2.0.
