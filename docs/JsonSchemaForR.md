# JSON Schema for R

[corvusjsonschema](https://corvus-dotnet.r-universe.dev/corvusjsonschema) is the Corvus JSON Schema evaluator for R:
draft 4, 6, 7, 2019-09 and 2020-12. It is an R package over the [Rust crate](JsonSchemaForRust.md), and gives the same
results and annotations as every other Corvus implementation.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft)
  except `draft4/optional/zeroTerminatedFloats.json`, as JSON text and read in place from R values.
- **Fast.** R lists and vectors are read in place, with nothing converted or copied, and JSON text is validated
  without creating R values for it.
- **Results and annotations.** Basic, detailed and verbose results, and annotations, as data frames.
- **No R dependencies.** The package needs R 4.2 or later and nothing else at run time.

## Install

```r
install.packages("corvusjsonschema", repos = c("https://corvus-dotnet.r-universe.dev", "https://cloud.r-project.org"))
```

R-universe serves binary packages for Windows and macOS. On Linux the package is built from source, which needs a Rust
toolchain (cargo and rustc, 1.89 or later).

## Validate

```r
library(corvusjsonschema)

validator <- compile_schema('{
  "type": "object",
  "required": ["id"],
  "properties": {"id": {"type": "integer", "minimum": 1}}
}')

is_valid(validator, list(id = 3))                     # TRUE: a list, read in place
is_valid_json(validator, '{"id": 0}')                 # FALSE: JSON text, parsed in Rust
is_valid_json(validator, c('{"id": 1}', '{}', NA))    # TRUE FALSE NA: one verdict for each text
```

`compile_schema` takes the schema as JSON text or as R values (a list, or `TRUE` or `FALSE`). Compile a schema once and
keep the validator: compiling does the work that makes validating fast. `is_valid_json` takes a character vector and
gives a verdict for each element, so a column of JSON documents is validated in one call.

## Options

| Option | Default | Meaning |
|---|---|---|
| `default_dialect` | `"draft2020-12"` | The dialect of a schema without `$schema`: `"draft4"`, `"draft6"`, `"draft7"`, `"draft2019-09"` or `"draft2020-12"`. |
| `assert_format` | `NA` | Whether `format` is asserted; `NA` follows the schema's vocabularies. |
| `assert_format_in_legacy_drafts` | `FALSE` | Assert `format` in drafts 4 to 7 when `assert_format` is `NA`. |
| `assert_content` | `TRUE` | Assert `contentEncoding` and `contentMediaType` in draft 7. |
| `formats` | `NULL` | A named list of functions, each taking a string and returning whether it is valid for that format. |
| `resolver` | `NULL` | A function from an absolute URI to the document (JSON text or R values), or `NULL` when it has none. |
| `base_uri` | `NULL` | The schema's base URI, when it has no `$id`. |
| `entry_point` | `NULL` | A subschema to validate against, such as `"#/$defs/item"`. |
| `max_depth` | `128` | The deepest the evaluator recurses in place before it raises `corvus_depth_error`. |

Failures are conditions that inherit from `corvus_json_schema_error`: `corvus_compilation_error`,
`corvus_invalid_json_error`, `corvus_value_error`, `corvus_depth_error` and `corvus_callback_error`.

## Results and annotations

```r
result <- evaluate(validator, list(id = "three"))
result$valid                                           # FALSE
result$results[, c("instance_location", "message")]
#>   instance_location                                        message
#> 1               /id The value was expected to match the subschema.
#> 2               /id The value was expected to be of type 'integer'
#> 3                   The value was expected to match the subschema.

titled <- compile_schema('{"title": "Person", "properties": {"name": {"title": "Name"}}}')
evaluate(titled, list(name = "Ada"), level = "verbose")$annotations
#>   instance_location keyword   schema_location  value
#> 1             /name   title #/properties/name   Name
#> 2                     title                 # Person
```

`evaluate` and `evaluate_json` take a level. `"basic"` records the failures without messages, `"detailed"` (the
default) adds the messages, and `"verbose"` records every keyword, passing ones and annotations included. The results
are a data frame with the columns `valid`, `message`, `evaluation_location`, `schema_location` and
`instance_location`. The annotations are a data frame too, with the columns `instance_location`, `keyword`,
`schema_location` and `value`, where the other implementations nest them in that order.

## Values

An R value reads as JSON the way jsonlite's `fromJSON(simplifyVector = FALSE)` gives JSON values. `NULL` is `null`. A
list with names is an object and a list without is an array, so `list()` is `[]` and an empty named list is `{}`. A
logical, integer, double or character vector of length one is a scalar, and of any other length an array of scalars.
Wrap a vector of length one in `I()` for an array of one element. `NA` of any type is `null`. A whole double, such as
`3`, is an integer as well as a number.

Values are read only as the schema examines them. A value the schema does examine that is not JSON raises
`corvus_value_error`: `NaN` and the infinities, a function, an environment, and any object with a class, such as a
data frame, a factor or a date.

## Links

- Package: [R-universe](https://corvus-dotnet.r-universe.dev/corvusjsonschema)
- Source and README: [src-r/corvusjsonschema](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-r/corvusjsonschema)
- The other languages: see [Other languages](OtherLanguages.md)
