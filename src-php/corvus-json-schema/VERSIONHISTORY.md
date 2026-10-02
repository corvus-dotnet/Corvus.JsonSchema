# Version History

The version history of the `corvus_json_schema` PHP extension (Packagist package `corvus-dotnet/corvus-json-schema`). It is versioned independently of the Corvus NuGet packages and of the [corvus-json-schema](../../src-rs/corvus-json-schema/VERSIONHISTORY.md) Rust crate it is built on.

## V0.1.0

The first release: a PHP extension over the corvus-json-schema Rust crate (0.1.3), for drafts 4, 6, 7, 2019-09 and 2020-12, and PHP 8.2 to 8.5. It reads PHP arrays and objects in place, validates JSON text without creating PHP values for it, and collects results (Basic, Detailed and Verbose) and annotations. It passes the whole JSON-Schema-Test-Suite, read in place, as JSON text and through a collector.
