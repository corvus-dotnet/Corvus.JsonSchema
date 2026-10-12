# Version History

The version history of the corvus-json-schema C library and its C++ wrapper (`src-rs/corvus-json-schema-capi`), released on GitHub as `capi-v<version>`. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md), and of the Rust crate it wraps.

## V0.1.4

V0.1.4 takes version 0.1.7 of the `corvus-json-schema` crate. That fixes wrong results for several forms of pattern and a validation that did not end, replaces the crate's regular expression engine, and gives every match of a pattern a limit of time. The functions and types that were there are unchanged, and there are additions for the limit. Versions 0.1.0 to 0.1.3 are affected, and so is everything built on them, the C++ wrapper included.

### The regular expression engine

The crate no longer uses the `regress` crate for the patterns with a lookaround, a backreference or a modifier group. It matches them with an engine of its own, a port of the ECMA-262 engine of the Corvus Go module, and matches every pattern as it is written. The crate's [version history](../corvus-json-schema/VERSIONHISTORY.md) lists the patterns that were answered wrongly before (a repetition inside a repetition that lost its count or did not end, a backreference from inside its group, two groups with one name, case-insensitive matching without the `u` flag, a pattern that is valid only without the `u` flag and a character beyond the Basic Multilingual Plane, `\B` beside a character of several bytes, and others), and what was measured: V8's answer for all 4,268,313 texts of the ECMA-262 regular expression test data that can be asked.

### Limits on regular expressions

A schema and an instance may both come from outside, so a pattern is now bounded in what it costs. Nothing that was in the header has changed. There are two new statuses, one new function and one new constant.

- **A match has a limit of one second.** One match of a `pattern` or `patternProperties` regular expression against one string may take one second (`CJS_DEFAULT_PATTERN_MATCH_LIMIT_MS`, 1000). `cjs_options_set_pattern_match_limit_ms(options, milliseconds)` sets another limit, and 0 turns it off for a caller who trusts both the schema and the instances. The limit is on each match, not on the validation.
- **`CJS_PATTERN_MATCH_LIMIT` (7)** is the status of a call whose match reached the limit, or whose backtracking held 1,048,576 entries. `cjs_validator_validate_json`, `cjs_validator_validate_document`, `cjs_validator_evaluate_json` and `cjs_validator_evaluate_document` return it, and `*valid` is then not an answer. `cjs_compile` and `cjs_compile_uri` can return it too, because a pattern of `patternProperties` is matched against the property names the schema declares. `cjs_last_error_message` names the pattern and the limit.
- **`CJS_PATTERN_TOO_LARGE` (8)** is the status of `cjs_compile` and `cjs_compile_uri` for a schema with a regular expression over a limit of size: 1,048,576 bytes of pattern, 1,000 levels of groups, or 1,048,576 counted for it as it is read (a pattern of plain characters can be 16,384 characters long). A regular expression that is not valid is `CJS_COMPILATION_FAILED`, as before.
- **A program that tests a status against the ones it knows** sees two it did not know. Before this version a match could not be stopped, and a pattern of that size compiled or was refused as not valid.
- **The C++ wrapper** has `options::pattern_match_limit(std::chrono::milliseconds)`. The two statuses arrive as the `status()` of the `error` it throws, and from `validator::try_validate`.

### Bug fixes

- **A separated list with a `$` inside it.** The crate matches a pattern of the form `^item(separator item)*$`, such as `^([a-z]+)(\.[a-z]+)*$`, without a regular expression engine. It dropped a `$` at the end of the first item or of the repeated group, so `^x(,a$)*$` was matched as `^x(,a)*$`. A `$` holds only at the end of the string, so the group of `^x(,a$)*$` can match once at most. For `{"pattern": "^x(,a$)*$"}` the functions that validate or evaluate set `*valid` to true for `"x,a,a"`, where the instance is not valid. `^x$(,a)*$` took `"x,a"`, and `^(a,$)*b$` took `"a,b"`. A pattern used for `patternProperties` or `propertyNames` was affected in the same way. Such a pattern is now matched by a regular expression engine. An escaped `\$` is a literal character and is matched as before. No other form of pattern was affected.

### Notices

- **The notice of the Unicode data is in every release archive.** The Unicode tables of the `corvus-json-schema` crate are derived from the Unicode Character Database, which is under the Unicode License V3. That license asks that its notice goes with every copy of the data, and the shared and static libraries hold the tables. `THIRD-PARTY-NOTICES.txt` has it, with the notice of the `regress` crate that the tables were first read from. It is now at the root of each platform package, beside `LICENSE`, and inside `CorvusJsonSchema.xcframework`, beside `Info.plist`, with a copy of `LICENSE`. Versions 0.1.0 to 0.1.3 carried no such notice.

## V0.1.3

V0.1.3 fixes a stack overflow for a schema whose `not` leads back to the schema it is in, by taking version 0.1.6 of the `corvus-json-schema` crate. There are no API changes. Versions 0.1.0 to 0.1.2 are affected, and so is everything built on them, the C++ wrapper included.

### Bug fixes

- **A `not` that leads back to the schema it is in.** A schema can loop without consuming the instance. The library abandons such an evaluation at the maximum depth (`cjs_options_set_max_depth`, 128 by default) and returns `CJS_DEPTH_EXCEEDED`. Evaluating `not` went around that guard. For a schema such as `{"not": {"$ref": "#"}}`, validating an instance that reached the `not` recursed until the stack overflowed, which ends the process with a segmentation fault or an abort. No status was returned, and the C++ wrapper threw no exception. The four functions that validate or evaluate, for JSON text and for a parsed document, were affected. A `not` is now under the guard, so they return `CJS_DEPTH_EXCEEDED`. The fault is in the schema. No instance causes it for a schema without such a loop, so a program was exposed only if it compiled schemas it did not write. A schema that loops elsewhere under a `not` was not affected in this library: it returned `CJS_DEPTH_EXCEEDED`, and still does.

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
