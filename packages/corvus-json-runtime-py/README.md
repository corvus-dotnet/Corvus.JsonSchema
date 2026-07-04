# corvus-json-runtime

The shared runtime imported by Corvus JSON Schema generated Python models (the `corvus_json_runtime`
import package). It is the Python peer of the `@endjin/corvus-json-runtime` TypeScript package.

Generated modules stay small by delegating to this runtime for the shared machinery. It carries the
exact-number primitives (validation on a number's decimal value, never a lossy float), the evaluation
tracker and spec-output failure collector, temporal conversions over `whenever`, the JSON Pointer and
deep-equality helpers, the format checks, and the JSON Patch / Merge Patch surface.

Implemented: the full evaluate/parse/build core, the format family, temporal conversions, RFC 7396 merge
patch (`apply_merge_patch` / `create_merge_patch`), and the byte-native read-modify-write helpers
(`rmw_upsert` / `rmw_produce_full`, member and array-element splicing). The RFC 6902 JSON Patch
(`apply_patch` / `create_patch`) and the immer-style `produce` draft are not yet implemented and currently
raise `NotImplementedError`.

Third-party dependencies: `whenever` (temporal), `regex` (ECMA-compatible regex), `idna` (IDN hostnames/emails).