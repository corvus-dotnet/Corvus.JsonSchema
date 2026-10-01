# Version History

The version history of `corvus-json-schema-rs`, the Python package backed by the `corvus-json-schema` Rust crate. It is versioned independently of the Corvus NuGet packages, whose history is in the repository's [VERSIONHISTORY.md](../../VERSIONHISTORY.md).

## V0.1.0

The first release.

### New features

- **The Rust evaluator for Python.** It has the API of the pure-Python `corvus-json-schema` package and passes the JSON-Schema-Test-Suite (every draft, with the C# runner's single exclusion) and its annotation tests.

- **Instances are read in place.** Python objects are read through CPython's C API, without converting them to `serde_json` values first, so validating an object costs the evaluation alone. JSON text can also be validated directly, parsed in Rust.

- **Wheels for every major platform.** The extension uses the stable ABI, so one wheel per platform serves CPython 3.10 and later, on Linux (x86_64 and aarch64, glibc and musl), macOS (universal2) and Windows (x64 and arm64).
