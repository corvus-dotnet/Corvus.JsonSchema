# ADR 0030. Immutable, content-hashed, versioned packages

Date: 2026-07-21. Status: **Accepted**. Scope: how a workflow is stored in the catalog. Builds on
[ADR 0017](0017-code-generate-the-executor.md). This records why a catalogued workflow is an immutable,
content-addressable package, and why versions are minted by assigning a number and rewriting the workflow id
rather than editing a version in place.

## Context

A control plane governs which workflow versions may run, so it has to be certain what a version is and that it
does not change under it. If a version could be edited in place, a run that started against one definition
could finish against another, and an audit could not say which definition executed. The catalog also needs a
stable identity for a version that does not depend on how the bytes happen to be framed, so the same logical
content is the same version whether it was packed once or repacked.

### Grounded architectural facts

- **A version is a self-contained package.** `WorkflowPackage`
  (`src/Corvus.Text.Json.Arazzo.Durability/Catalog/WorkflowPackage.cs`) bundles the Arazzo workflow document,
  the OpenAPI and AsyncAPI source documents it references, optional precomputed schema metadata, and the
  compiled executor, as one artifact.
- **Adding a version assigns a number and rewrites the id.** `IWorkflowCatalogStore`
  (`Durability/Catalog/IWorkflowCatalogStore.cs`) assigns the next version number for a base id, rewrites the
  package's first workflow id to `{baseWorkflowId}-v{versionNumber}` (`CatalogPackage.Process`), repacks
  canonically, content-hashes it, and persists it.
- **A base id must not already carry a version.** `SecuredWorkflowCatalog` rejects a submission whose id
  already has a `-vN` suffix, so a submitter supplies the base id and the store owns versioning.
- **A version is content-addressed.** Each stored version carries a SHA-256 content hash of its logical content
  ([ADR 0031](0031-content-hash-over-rfc8785-canonical.md)), so the version's identity is its content.

## Decision

A catalogued workflow version is an **immutable, content-addressed package**. Adding a version assigns the next
version number, rewrites the workflow id to `{baseWorkflowId}-v{versionNumber}`, hashes the logical content,
and stores the package. A version is never edited in place; a change is a new version with a new number and a
new hash. The submitter provides the base id and the store owns version assignment.

## Consequences

- A run's definition is fixed for its lifetime. The version it started against is the version it finishes
  against, because that version cannot change.
- An audit can name exactly which version executed, by its versioned id and content hash.
- Because the store owns version numbering and id rewriting, a submitter cannot squat a versioned id or
  overwrite an existing version.
- The package is a complete input to code generation ([ADR 0033](0033-compile-at-catalog-add.md)), so the
  version carries everything needed to compile and run it, with nothing to fetch at run time.
