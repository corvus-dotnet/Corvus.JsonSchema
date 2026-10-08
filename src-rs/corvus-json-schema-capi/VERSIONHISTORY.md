# Version History

The version history of the corvus-json-schema C library and its C++ wrapper (`src-rs/corvus-json-schema-capi`), released on GitHub as `capi-v<version>`. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md), and of the Rust crate it wraps.

## V0.1.2

V0.1.2 fixes wrong validation results for one form of pattern, by taking version 0.1.4 of the `corvus-json-schema` crate. There are no API changes. Versions 0.1.0 and 0.1.1 are affected, and so is everything built on them, the C++ wrapper included.

### Bug fixes

- **A pattern of the form `^(?=[^SET]+$)(?=(.*\w)).+$` with a character outside ASCII in its excluded set.** The library matches this form without a regular expression engine and keeps the excluded set as one bit for each ASCII character. A member outside ASCII, such as `é` in `^(?=[^é]+$)(?=(.*\w)).+$`, was read one UTF-8 byte at a time, and set the bits of unrelated ASCII characters (`C` and `)` for `é`). A validator compiled from such a schema rejected valid strings (`"C1"`) and accepted invalid ones (`"é1"`), with no error. Such a pattern is now matched by the regular expression engine. A pattern of this form whose excluded set is all ASCII was never affected.

## V0.1.1

V0.1.1 adds an XCFramework for Swift. The library is unchanged.

### New features

- **An XCFramework for Swift packages.** Each release now also carries `CorvusJsonSchema.xcframework.zip`: the static library for macOS (arm64 and x86_64, 10.15 or later), iOS (arm64, 13 or later) and the iOS simulator (arm64 and x86_64), with the header and a module map declaring the Clang module `CCorvusJsonSchema`, for a Swift package's `binaryTarget`. The release notes give its SwiftPM checksum, and `CorvusJsonSchema.xcframework.zip.sha256` holds it. CI tests it from Swift on macOS and builds it for iOS and the simulator.

## V0.1.0

The first release.

### New features

- **The C library.** `libcorvus_json_schema`, shared and static, with the C99 header `corvus_json_schema.h`: compile a schema (with options for the dialect, format and content assertion, a base URI, an entry point, the recursion depth, custom formats and a document resolver), validate JSON text or a parsed document, and evaluate into a collector for Basic, Detailed or Verbose results and annotations. Errors are `cjs_status` codes with the message and byte offset per thread; no Rust panic crosses into the caller. Validating JSON text allocates nothing in the steady state. It passes the whole JSON-Schema-Test-Suite, with every result row as the Rust crate gives it.

- **The C++ wrapper.** `corvus_json_schema.hpp`, header-only C++17: RAII classes owning their handles through `std::unique_ptr`, exceptions (or `try_validate`), and format validators and resolvers as `std::function`.

- **Packages for every major platform.** Linux (x86_64 and aarch64, glibc and musl), macOS (universal2) and Windows (x64 and arm64), each with a CMake package (`find_package(corvus_json_schema)`, targets `corvus_json_schema::corvus_json_schema` and `corvus_json_schema::corvus_json_schema_static`) and a pkg-config file (`corvus-json-schema`). The glibc libraries need glibc 2.17 or newer. The Windows packages, built with MSVC, add a static library for the static C runtime (`/MT`), the target `corvus_json_schema::corvus_json_schema_static_mt`.

- **Faster than Blaze.** On the sourcemeta jsonschema-benchmark corpora, in the benchmark's harness, the C++ wrapper parses and validates faster than Blaze on all 37 (about a quarter of Blaze's time, geometric mean).
