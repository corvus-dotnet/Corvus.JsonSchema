# corvus-json-runtime

The shared runtime imported by Corvus JSON Schema generated Python models (the `corvus_json_runtime`
import package). It is the Python peer of the `@endjin/corvus-json-runtime` TypeScript package.

Generated modules stay small by delegating to this runtime for the shared machinery. It carries the
exact-number primitives (validation on a number's decimal value, never a lossy float), the evaluation
tracker and spec-output failure collector, temporal conversions over `whenever`, the JSON Pointer and
deep-equality helpers, the format checks, and the JSON Patch / Merge Patch surface.

This is the Phase 0 skeleton. The evaluate/parse/build core is implemented; the full format family, the
RFC 6902 patch, the immer-style `produce`, and the byte-native read-modify-write helpers land in later
phases and currently raise `NotImplementedError`.

Sole third-party dependency: `whenever`.