# Version History

The version history of the `corvus_json_schema` PHP extension (Packagist package `corvus-dotnet/corvus-json-schema`). It is versioned independently of the Corvus NuGet packages and of the [corvus-json-schema](../../src-rs/corvus-json-schema/VERSIONHISTORY.md) Rust crate it is built on.

## V0.1.1

V0.1.1 fixes the extension for Alpine Linux on arm64. There are no API changes.

### Bug fixes

- **The musl builds need only musl.** They linked the GCC unwinder dynamically (`libgcc_s.so.1`), which the official `php:*-alpine` images for arm64 do not include, so `pie install` there installed an extension PHP could not load. The unwinder is now linked statically, and each musl build is tested in a fresh container of the stock image, with nothing added.

## V0.1.0

The first release: a PHP extension over the corvus-json-schema Rust crate (0.1.3), for drafts 4, 6, 7, 2019-09 and 2020-12, and PHP 8.2 to 8.5. It reads PHP arrays and objects in place, validates JSON text without creating PHP values for it, and collects results (Basic, Detailed and Verbose) and annotations. It passes the whole JSON-Schema-Test-Suite, read in place, as JSON text and through a collector.
