# Arazzo Workflow Catalog — design

A governance + usability service in the control plane: an immutable, content-hashed, versioned store of
**workflow document packages** (an Arazzo workflow plus the OpenAPI/AsyncAPI source documents it references),
with title/description/tag search, an explicit governance owner, referential integrity against workflow
runs, and obsolete→purge lifecycle. It extends the existing control-plane API and the durability backends.

## Goals

- **Self-contained, immutable packages.** Each catalog version bundles the Arazzo workflow + every referenced
  OpenAPI/AsyncAPI document, hashed so it is verifiable and content-addressed. Being self-contained, a package
  is a complete input to **code generation**: it can later be handed to a service that runs this repo's code
  generators over its contents to produce the workflow's executor code/assembly (see Future phase).
- **Versioning by base id.** Versions are grouped under a *base workflow id*; the stored workflow id carries
  the version (`nightly-reconcile` → `nightly-reconcile-v4`). Submissions must not already carry a `-vN`
  suffix (collision defence); the store rewrites the workflow's id to the versioned form.
- **Governance.** A first-class `owner` (with contact details), and durable `createdBy` / `lastUpdatedBy`
  attribution on every record — so the catalog integrates with governance tooling and answers "who owns / who
  changed this".
- **Discoverability.** `title` + `description` (from the Arazzo `info`) and free-form `tags` are searchable.
- **Safe lifecycle.** A version cannot be deleted while runs reference it; versions can be marked obsolete and
  obsolete versions with no runs can be purged in bulk.

## Data model

A catalog **version** is the unit. Versions of one logical workflow share a `baseWorkflowId`.

**Immutable** (fixed when the version is added; define the content hash):

| Field | Source |
|-------|--------|
| `baseWorkflowId` | the submitted workflow id (must have no `-vN` suffix) |
| `versionNumber` | assigned by the store = (current max for the base id) + 1 |
| `workflowId` | `{baseWorkflowId}-v{versionNumber}` — the stored/rewritten id runs execute under |
| `package` | the `{ workflow, sources: { <name>: <doc> } }` envelope (Arazzo + OpenAPI/AsyncAPI), stored so each document is individually retrievable; transferred as a file, never embedded in metadata JSON |
| `hash` | SHA-256 over the RFC 8785 (`JsonCanonicalizer`) canonical form of `package` |
| `title`, `description` | extracted from the workflow's `info.title` / `info.description` (fallback `info.summary`) |
| `sources` | the list of `{ name, type }` (from the workflow's `sourceDescriptions`) — surfaced in metadata so a client knows which documents are addressable |
| `createdBy`, `createdAt` | the authenticated actor + time of the add |

**Mutable governance metadata** (updatable; every change stamps `lastUpdatedBy` + `lastUpdatedAt`):

| Field | Notes |
|-------|-------|
| `owner` | `{ name, email, team?, url? }` — the accountable owner, for governance integration |
| `tags` | free-form string set, for display + filtering (AND-matched, as runs are) |
| `status` | `Active` \| `Obsolete` |
| `lastUpdatedBy`, `lastUpdatedAt` | the actor + time of the last metadata change |
| `obsoletedBy`, `obsoletedAt` | the actor + time the version was marked `Obsolete` (a distinct governance event; null while Active) |

Audit is **fields-on-the-record** (`createdBy`, `lastUpdatedBy`, `obsoletedBy` + timestamps) for governance
visibility; the forensic trail is OpenTelemetry (no separate durable audit-log entity).

The package itself (workflow + sources + hash + title/description) never changes; a "new version of a workflow"
is a new version record. Only governance metadata is mutable.

### Package format

A package is a **self-contained, nupkg-style ZIP archive** — an opaque binary artifact moved as a file
(multipart upload / streamed download) and stored verbatim. Its documented internal layout (all entries UTF-8
JSON), implemented by `WorkflowPackage` (the pack/unpack runtime tools in `Corvus.Text.Json.Arazzo.Durability`):

```
manifest.json     { "formatVersion": 1, "workflow": "workflow.json", "sources": [ { "name", "path" } ] }
workflow.json     the Arazzo workflow document
sources/<name>.json   each referenced source document (name = the workflow's sourceDescriptions[].name)
```

The archive is written **deterministically** (fixed entry order + timestamps) so identical content yields
identical bytes. The **content hash** (`hash`) is SHA-256 over the RFC 8785 canonical form of the *logical*
`{ workflow, sources }` content — independent of the ZIP container — so it is stable across repacks, zip
implementations, property ordering, and insignificant whitespace. On add, the store validates that every
non-arazzo `sourceDescriptions` entry has a matching source document in the archive. `WorkflowPackage.Pack` /
`Open` (and the CLI's `pack` / `unpack` / `verify`) are the .NET tools for producing and consuming it; the
future code-generation service consumes the same archive.

### Workflow-id rewrite

On add, the store:
1. Reads the submitted workflow id; rejects (`400`) if it matches `-v\d+$`.
2. Computes `versionNumber` and the versioned id.
3. Rewrites the workflow document's `workflowId` (the package's root workflow) to the versioned id before
   hashing/storing, so the persisted package and any run created from it agree on the id.

## API (added to `arazzo-control-plane.openapi.json`)

Metadata is JSON; the **package is uploaded as a file** (multipart) and the package + its documents are
downloaded by addressable endpoint — see *Package transfer* below. JSON responses surface the governance/audit metadata only (`owner`,
`tags`, `status`, `createdBy`/`createdAt`, `lastUpdatedBy`/`lastUpdatedAt`, `obsoletedBy`/`obsoletedAt`,
`hash`, and the list of contained `sources`); the documents themselves are fetched from the addressable
retrieval endpoints.

| HTTP | Path | Scope | Purpose |
|------|------|-------|---------|
| `POST` | `/catalog` | `catalog:write` | **Upload** a new version as `multipart/form-data` — a `package` file part (the `{workflow,sources}` envelope) plus `owner` + `tags` parts. Returns the version metadata (versionNumber, workflowId, hash, …). |
| `GET` | `/catalog` | `catalog:read` | Search versions — filters: `q` (title/description), `baseWorkflowId`, `tag` (repeatable, AND), `status`, `owner`; keyset paged. Returns version summaries (metadata). |
| `GET` | `/catalog/{baseWorkflowId}` | `catalog:read` | List the versions of a base id. |
| `GET` | `/catalog/{baseWorkflowId}/versions/{versionNumber}` | `catalog:read` | Get a version's **metadata** (no documents embedded). |
| `GET` | `/catalog/{baseWorkflowId}/versions/{versionNumber}/package` | `catalog:read` | **Download** the whole canonical package envelope (`application/json`). |
| `GET` | `/catalog/{baseWorkflowId}/versions/{versionNumber}/workflow` | `catalog:read` | Get just the Arazzo workflow document (`application/json`) — the common UI case. |
| `GET` | `/catalog/{baseWorkflowId}/versions/{versionNumber}/sources/{sourceName}` | `catalog:read` | Get one referenced source document (OpenAPI/AsyncAPI) by its `sourceDescriptions` name (`application/json`). |
| `PATCH` | `/catalog/{baseWorkflowId}/versions/{versionNumber}` | `catalog:write` | Update governance metadata (`owner`, `tags`, `status`). Stamps `lastUpdatedBy`; status→Obsolete stamps `obsoletedBy`. |
| `DELETE` | `/catalog/{baseWorkflowId}/versions/{versionNumber}` | `catalog:purge` | Delete one version. `409` if any run references its `workflowId`. |
| `PURGE` | `/catalog` | `catalog:purge` | Bulk-reap **obsolete** versions that have no referencing runs. |

Scopes mirror the runs tiers (`catalog:read` / `catalog:write` / `catalog:purge`). Errors are RFC 9457
`problem+json`.

### Package transfer (upload / download)

The package is **uploaded as a file** (multipart), keeping the large envelope out of a JSON request body — the
repo's OpenAPI generator supports this (a `format: binary` multipart part is bound as the upload's file part).

- **Upload** — `POST /catalog` is `multipart/form-data` with parts: `package` (`format: binary`, the envelope
  file), `owner` (`CatalogOwner` JSON), `tags` (string array). The server reads the `package` part,
  canonicalises + hashes it, projects title/description/sources, rewrites the workflow id, and stores it.
  Responds `201` with the version metadata.
- **Whole-package download** — `…/package` returns the canonical envelope as `application/json` (the package
  *is* a JSON document — the self-contained, hash-verifiable artifact for backup/export or the future
  code-generation service). The package is JSON, so it is returned as JSON rather than `octet-stream`; the
  current server generator writes response bodies through a JSON writer (it has no raw byte-stream response
  path), and a JSON body is the natural, working representation. A genuinely streamed binary download (and the
  later compiled-assembly blob, which is **not** JSON) would need a server-side raw-stream response capability;
  that is a follow-up if/when the assembly sub-resource is specified.
- **Addressable documents** — the package is stored so its constituents are individually retrievable: `…/workflow`
  returns the Arazzo document and `…/sources/{sourceName}` returns one OpenAPI/AsyncAPI document, both as
  `application/json` — so the UI can fetch just the workflow definition (the common case) or a single source
  without downloading the whole package. The version metadata lists the available `sources` (name + type) so a
  client knows what is addressable.

### Referential integrity

A run references a version by its exact `WorkflowId` (`nightly-reconcile-v4`) — already an indexed exact-match
query (`WorkflowQuery(WorkflowId: …)`). `DELETE` and `PURGE` consult the run store: a version with ≥1
referencing run cannot be deleted; `PURGE` only reaps `Obsolete` versions whose `workflowId` matches no run.

## Store

A new `IWorkflowCatalogStore` in `Corvus.Text.Json.Arazzo.Durability`, implemented **inside the existing
backend projects** (no new projects): in-memory (reference) + Sqlite/Postgres/SqlServer/MySql/Mongo/Cosmos/
Redis/NATS/AzureStorage. A shared `WorkflowCatalogStoreConformance` suite runs against every backend.

```csharp
public interface IWorkflowCatalogStore
{
    // Add a version: the store assigns versionNumber, rewrites the workflow id, hashes the canonical package,
    // persists it (so its documents stay individually retrievable), and returns the version metadata.
    ValueTask<CatalogVersion> AddAsync(string baseWorkflowId, ReadOnlyMemory<byte> packageUtf8, CatalogMetadata metadata, CancellationToken ct);
    ValueTask<CatalogVersion?> GetAsync(string baseWorkflowId, int versionNumber, CancellationToken ct);            // metadata only
    ValueTask<ReadOnlyMemory<byte>?> GetPackageAsync(string baseWorkflowId, int versionNumber, CancellationToken ct); // whole canonical envelope
    // An individually-addressable document: name "$workflow" for the Arazzo doc, or a sourceDescriptions name.
    ValueTask<ReadOnlyMemory<byte>?> GetDocumentAsync(string baseWorkflowId, int versionNumber, string documentName, CancellationToken ct);
    ValueTask<CatalogPage> QueryAsync(CatalogQuery query, CancellationToken ct);                                    // summaries (no documents)
    ValueTask<CatalogVersion?> UpdateMetadataAsync(string baseWorkflowId, int versionNumber, CatalogMetadataPatch patch, CancellationToken ct);
    ValueTask<bool> DeleteAsync(string baseWorkflowId, int versionNumber, CancellationToken ct);                    // caller checks refs first
    ValueTask<IReadOnlyList<CatalogVersionRef>> ListObsoleteAsync(CancellationToken ct);                            // for purge ref-check
    ValueTask DeleteManyAsync(IReadOnlyList<CatalogVersionRef> versions, CancellationToken ct);
}
```

The index projection (`CatalogVersionIndexEntry`) carries the searchable/governance fields (baseWorkflowId,
versionNumber, workflowId, title, description, tags, status, owner, sources, createdBy/at, lastUpdatedBy/at,
hash) so `QueryAsync` answers search without loading documents — the same authoritative-store-with-index
pattern the run store uses. Title/description search is `contains` (per backend, like the run workflowId
filter); tags are contains-ALL. `GetDocumentAsync` slices the stored canonical envelope to return one document.

### Response links (HATEOAS)

The version-metadata responses (`POST /catalog`, `GET …/versions/{n}`) carry OpenAPI `links` so a client can
navigate to the documents without constructing URLs: a `package` link (whole-package download) and a
`workflow` link (the Arazzo document), with parameters resolved from the response body
(`$response.body#/baseWorkflowId`, `$response.body#/versionNumber`). Per-source links can't be a single static
link (the source name is dynamic), so the metadata lists `sources` (name + type) and the UI composes the
`…/sources/{name}` URL. This reuses the same `links` mechanism the `/runs` responses already use.

The management client gains catalog methods (coordinating the catalog store + the run store for referential
integrity), and the OpenTelemetry audit spans gain catalog actions (`catalog.add` / `catalog.update` /
`catalog.delete` / `catalog.purge`, tagged with actor, base id, version, outcome) — telemetry is the forensic
trail; the durable record carries `createdBy`/`lastUpdatedBy` for governance visibility.

## UI

A catalog view in the kit (follow-on to the runs UI): a searchable, tag/owner/status-filterable list of
versions, a version detail showing the package + hash + owner + governance attribution, and write actions
(add, edit metadata, obsolete, delete) gated by the `catalog:*` scopes — reusing the kit's components,
theming, and auth model.

## Future phase (noted, not yet specified)

The package is deliberately a **complete, self-contained input to code generation**: it bundles the Arazzo
workflow plus every referenced OpenAPI/AsyncAPI document, so it carries everything this repo's code generators
need with no external resolution.

**Code-generation service.** A version's package can be handed to a generation service that runs **our code
generators** (the same `corvusjson` / executor-emitter pipeline used elsewhere) over the package contents to
produce the workflow's executor code — entirely from the package, against the exact, content-hashed documents
that version captured (so the generated code is reproducible and provably tied to the version).

**Compiled-assembly blob.** The output — a **release-build** compiled assembly — can then be stored on the
catalog version, **cryptographically tied to the version** (signed over the package hash). A hosting service
will discover and **dynamically load** that assembly to publish the workflow at a configured, secured endpoint.

This adds (later): a generate-from-package step (package → generated code → compiled assembly), an assembly
blob + signature on the version record, a publish/verify step, and the hosting/loader service. The design is
ready for it now: the version is immutable and content-hashed, and the package is self-contained, so both the
generated code and the assembly can be bound to — and regenerated from — the exact version. The API/store
shape will extend with an assembly (and/or generated-code) sub-resource when specified.
