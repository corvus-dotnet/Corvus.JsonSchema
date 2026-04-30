JSON Patch (RFC 6902), JSON Merge Patch (RFC 7396), and JSON diff operations on `JsonElement` using pooled-memory infrastructure. Apply, generate, and compute patches without string allocations.

Key types include [`JsonPatchExtensions`](/api/v5/corvus-text-json-patch-jsonpatchextensions.html) (extension methods for applying patches and computing diffs), [`PatchBuilder`](/api/v5/corvus-text-json-patch-patchbuilder.html) (fluent builder for constructing patch documents), and [`JsonPatchDocument`](/api/v5/corvus-text-json-patch-jsonpatchdocument.html) (a parsed RFC 6902 patch document).
