# JSON Schema for C++

The corvus-json-schema C library ships a header-only C++17 wrapper, `corvus_json_schema.hpp`: the Corvus JSON Schema
evaluator for C++, draft 4, 6, 7, 2019-09 and 2020-12. It wraps the [C library](JsonSchemaForC.md) in RAII classes and
exceptions, and gives the same results and annotations as every other Corvus implementation.

- **Conformant.** Passes the whole JSON-Schema-Test-Suite (required, optional and `optional/format`, every draft) except
  `draft4/optional/zeroTerminatedFloats.json`.
- **Fast.** Over the [jsonschema-benchmark](https://github.com/sourcemeta-research/jsonschema-benchmark) corpora,
  parsing and validating takes 0.24 of the time of [Blaze](https://github.com/sourcemeta/blaze), and it is faster on
  all 37 corpora.
- **Results and annotations.** Basic, Detailed and Verbose results, and annotations.
- **Idiomatic.** Each class owns its handle through `std::unique_ptr`; errors are exceptions, or status codes with
  `try_validate` for code built without exceptions; format validators and resolvers are `std::function`s.

## Install

The wrapper is in every [C library package](JsonSchemaForC.md#install), beside the C header. With CMake:

```cmake
find_package(corvus_json_schema REQUIRED)
target_link_libraries(app PRIVATE corvus_json_schema::corvus_json_schema_static)  # or the shared library
```

## Validate

```cpp
#include <iostream>

#include "corvus_json_schema.hpp"

namespace cjs = corvus::json_schema;

int main() {
    cjs::validator validator = cjs::validator::compile(R"({
        "type": "object",
        "required": ["id"],
        "properties": {"id": {"type": "integer"}}
    })");
    std::cout << validator.is_valid(R"({"id": 3})") << '\n';    // 1
    std::cout << validator.is_valid(R"({"id": "3"})") << '\n';  // 0
}
```

`is_valid` parses the text into per-thread buffers and validates it in place. To validate the same text more than
once, parse it into a `cjs::document` (`document::parse`, or `document::parse_borrowed` to keep the text unchanged
instead of copying it) and pass the document to `is_valid`. A `validator` and a `document` are immutable and can be
shared between threads.

## Options

```cpp
cjs::options options;
options.default_dialect(cjs::dialect::draft2019_09)
    .assert_format(true)
    .format("even", [](std::string_view s) { return s.size() % 2 == 0; })
    .resolver([](std::string_view uri) -> std::optional<std::string> {
        return std::nullopt;  // the document's JSON text, or nullopt when unknown
    });
cjs::validator validator = cjs::validator::compile(schema, options);
```

The options are those of the C library: `default_dialect`, `assert_format`, `assert_format_in_legacy_drafts`,
`assert_content`, `format`, `resolver`, `base_uri`, `entry_point` and `max_depth`. Failures throw
`corvus::json_schema::error`, with the C library's status, the message and, for invalid JSON, the byte offset.

## Results and annotations

```cpp
cjs::collector collector(cjs::results_level::detailed);
bool valid = validator.evaluate(R"({"id": "3"})", collector);
for (std::size_t i = 0; i < collector.size(); i++) {
    cjs::result_row row = collector[i];
    // row.is_match, row.message, row.evaluation_location, row.schema_location, row.instance_location
}

cjs::collector verbose(cjs::results_level::verbose);
validator.evaluate(R"({"id": 3})", verbose);
std::string_view annotations = verbose.annotations_json();  // instance location -> keyword -> schema location -> value
```

`basic` records the failures without messages, `detailed` adds the messages, and `verbose` records every keyword,
passing ones and annotations included.

## Links

- Releases: [capi-v releases](https://github.com/corvus-dotnet/Corvus.JsonSchema/releases?q=capi-v)
- Source and design: [src-rs/corvus-json-schema-capi](https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-rs/corvus-json-schema-capi)
- The other languages: see [Other languages](OtherLanguages.md)
