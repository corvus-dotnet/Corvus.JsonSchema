# JSON Schema for C

The corvus-json-schema C library is the Corvus JSON Schema evaluator for C: draft 4, 6, 7, 2019-09 and 2020-12. It is
a C ABI over the [Rust crate](JsonSchemaForRust.md), with a C99 header, and gives the same results and annotations as
every other Corvus implementation. The [C++ wrapper](JsonSchemaForCpp.md) and the [Swift package](JsonSchemaForSwift.md)
are built on it.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`. Every result row matches the Rust crate's at every level.
- **Fast.** Over the [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark) corpora,
  parsing and validating JSON text takes about a quarter of the time of [Blaze](https://github.com/sourcemeta/blaze),
  the C++ validator, and it is faster on all 37 corpora. In the steady state, validating JSON text allocates nothing.
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations.
- **Safe at the boundary.** Errors are status codes, with a message and byte offset per thread; no Rust panic crosses
  into the caller. Validators and documents are immutable and can be shared between threads.

## Install

Download the package for your platform from the latest
[C library release](https://github.com/corvus-dotnet/Corvus.JsonSchema/releases?q=capi-v) (tagged `capi-v<version>`):

| Platform | Package |
|---|---|
| Linux, glibc 2.17 or later | `corvus-json-schema-<version>-x86_64-unknown-linux-gnu.tar.gz`, `...-aarch64-unknown-linux-gnu.tar.gz` |
| Linux, musl | `corvus-json-schema-<version>-x86_64-unknown-linux-musl.tar.gz`, `...-aarch64-unknown-linux-musl.tar.gz` |
| macOS (Intel and Apple silicon) | `corvus-json-schema-<version>-universal2-apple-darwin.tar.gz` |
| Windows (MSVC) | `corvus-json-schema-<version>-x86_64-pc-windows-msvc.zip`, `...-aarch64-pc-windows-msvc.zip` |

Each holds the header, the shared and static libraries, a CMake package and a pkg-config file. Point CMake at it:

```cmake
find_package(corvus_json_schema REQUIRED)
target_link_libraries(app PRIVATE corvus_json_schema::corvus_json_schema)  # or ..._static
```

or use pkg-config:

```sh
cc app.c $(pkg-config --cflags --libs corvus-json-schema)
```

## Validate

```c
#include <stdbool.h>
#include <stdio.h>
#include <string.h>

#include "corvus_json_schema.h"

int main(void) {
    const char *schema = "{\"type\": \"object\", \"required\": [\"id\"],"
                         " \"properties\": {\"id\": {\"type\": \"integer\"}}}";
    cjs_validator *validator = NULL;
    if (cjs_compile(schema, strlen(schema), NULL, &validator) != CJS_OK) {
        cjs_str message = cjs_last_error_message();
        fprintf(stderr, "%.*s\n", (int)message.len, message.ptr);
        return 1;
    }

    const char *json = "{\"id\": 3}";
    bool valid = false;
    if (cjs_validator_validate_json(validator, json, strlen(json), &valid) == CJS_OK) {
        printf("%s\n", valid ? "valid" : "invalid");
    }
    cjs_validator_free(validator);
    return 0;
}
```

`cjs_validator_validate_json` parses the text into per-thread buffers and validates it in place. To validate the same
text more than once, parse it with `cjs_document_parse` and validate the document with
`cjs_validator_validate_document`.

## Options

`cjs_options_new` creates options with the defaults; pass them to `cjs_compile`, then free them with
`cjs_options_free`:

| Function | Meaning |
|---|---|
| `cjs_options_set_default_dialect` | The dialect of a schema without `$schema` (`CJS_DRAFT4` to `CJS_DRAFT202012`, the default). |
| `cjs_options_set_assert_format` | `CJS_TRUE` asserts `format`, `CJS_FALSE` never does; `CJS_DEFAULT` follows the schema's vocabularies. |
| `cjs_options_set_assert_format_in_legacy_drafts` | With format assertion left to its default, assert it in drafts 4 to 7 too. |
| `cjs_options_set_assert_content` | Assert `contentEncoding` and `contentMediaType` in draft 7 (default true). |
| `cjs_options_add_format` | A custom format: a callback from the string to whether it is valid. |
| `cjs_options_set_resolver` | A callback from an absolute URI to the document's JSON text, for remote references. |
| `cjs_options_set_base_uri` | The base URI of the root document. |
| `cjs_options_set_entry_point` | A subschema to validate against, such as `#/$defs/item`. |
| `cjs_options_set_max_depth` | The deepest the evaluator recurses in place (default 128). |

Callbacks run on whichever thread validates (format callbacks) or compiles (the resolver), and must not unwind into
the library.

## Results and annotations

```c
cjs_collector *collector = cjs_collector_new(CJS_DETAILED);
bool valid = false;
cjs_validator_evaluate_json(validator, json, strlen(json), collector, &valid);
for (size_t i = 0; i < cjs_collector_count(collector); i++) {
    cjs_str message = cjs_collector_message(collector, i);
    cjs_str location = cjs_collector_instance_location(collector, i);
    /* cjs_collector_is_match, cjs_collector_evaluation_location, cjs_collector_schema_location */
}
cjs_collector_free(collector);
```

`CJS_BASIC` records the failures without messages, `CJS_DETAILED` adds the messages, and `CJS_VERBOSE` records every
keyword, passing ones and annotations included. After a verbose evaluation, `cjs_collector_annotations_json` gives the
annotations as JSON text, grouped by instance location, keyword and schema location.

## Links

- Releases: [capi-v releases](https://github.com/corvus-dotnet/Corvus.JsonSchema/releases?q=capi-v)
- Source and design: [src-rs/corvus-json-schema-capi](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-rs/corvus-json-schema-capi)
  ([DESIGN.md](https://github.com/corvus-dotnet/Corvus.JsonSchema/blob/main/src-rs/corvus-json-schema-capi/DESIGN.md))
- The other languages: see [Other languages](OtherLanguages.md)
