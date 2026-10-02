# Version History

The version history of the corvus-json-schema C library and its C++ wrapper (`src-rs/corvus-json-schema-capi`), released on GitHub as `capi-v<version>`. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md), and of the Rust crate it wraps.

## V0.1.0

The first release.

### New features

- **The C library.** `libcorvus_json_schema`, shared and static, with the C99 header `corvus_json_schema.h`: compile a schema (with options for the dialect, format and content assertion, a base URI, an entry point, the recursion depth, custom formats and a document resolver), validate JSON text or a parsed document, and evaluate into a collector for Basic, Detailed or Verbose results and annotations. Errors are `cjs_status` codes with the message and byte offset per thread; no Rust panic crosses into the caller. Validating JSON text allocates nothing in the steady state. It passes the whole JSON-Schema-Test-Suite, with every result row as the Rust crate gives it.

- **The C++ wrapper.** `corvus_json_schema.hpp`, header-only C++17: RAII classes owning their handles through `std::unique_ptr`, exceptions (or `try_validate`), and format validators and resolvers as `std::function`.

- **Packages for every major platform.** Linux (x86_64 and aarch64, glibc and musl), macOS (universal2) and Windows (x64 and arm64), each with a CMake package (`find_package(corvus_json_schema)`, targets `corvus_json_schema::corvus_json_schema` and `corvus_json_schema::corvus_json_schema_static`) and a pkg-config file (`corvus-json-schema`). The glibc libraries need glibc 2.17 or newer. The Windows packages, built with MSVC, add a static library for the static C runtime (`/MT`), the target `corvus_json_schema::corvus_json_schema_static_mt`.

- **Faster than Blaze.** On the sourcemeta jsonschema-benchmark corpora, in the benchmark's harness, the C++ wrapper parses and validates faster than Blaze on all 37 (about a quarter of Blaze's time, geometric mean).
