# Drift catalogue: the four C# client emitters

Phase 0, PR 0b, step 5 of [the plan](OpenApiClientsForOtherLanguages.md). Before the four spec-version generators'
client emission is consolidated into one `CSharpClientEmitter`, this catalogue records every way the four emitters
disagree when given the same prepared operation model, and the decision taken for each.

## Method

`DriftCatalogueTests.CrossFeedEveryDocumentThroughEveryEmitter` (in `Corvus.Text.Json.OpenApi.CodeGeneration.Tests`,
category `outerloop`) prepares each document once, with its own version's generator, and emits the resulting
`OpenApiClientModel` through all four emitters over one synthetic type map (every schema pointer gets a deterministic
type name and a builder shape, so every emitter produces its fullest surface over identical input). The emitters'
outputs are diffed against the document's own emitter's output. Run it with
`CORVUS_DRIFT_CATALOGUE_OUT=<dir>` to keep the raw outputs and `summary.md`.

Documents: covspec and petstore for 2.0, 3.0, 3.1 and 3.2 (paths scope), and callbacks for 3.0, 3.1 and 3.2
(webhooks-and-callbacks scope).

## Result

The 3.0 and 3.1 emitters are byte-identical to each other on every document, in both scopes. The 3.2 emitter and
the 2.0 emitter differ from them in exactly the ways below. Everything else that differs when a 3.2-prepared model
goes through an older emitter is feature-driven: fields only 3.2 fills (`CustomMethodName`, `ItemSchemaPointer`,
`MixedPartInfo`, `AllowReserved`, `BinaryParts`, response `Summary`), which an older emitter does not read.
The 2.0 emitter lacks links and callback clients, which its own models never carry.

| # | Kind | Where | What | Decision |
|---|---|---|---|---|
| D1 | Drift | Client interface (`IApi*Client.cs`) | The 3.2 emitter writes a nested `SecuritySchemes` static class (name, type, HTTP scheme, API key name and location, OpenID Connect and OAuth2 URLs, available scopes) from `SecuritySchemeInfo`. The 2.0, 3.0 and 3.1 emitters write nothing, and until phase 0 step 1 their generators did not prepare the schemes at all | Upgrade: additive constants, patch-level. The consolidated emitter writes the class for every version whose document declares schemes |
| D2 | Drift | Request struct `<remarks>` | The 3.2 emitter documents a request with `Description ?? Summary`; the others use `Summary` only | Upgrade: documentation only. Description first, summary as the fallback, everywhere |
| D3 | Feature (2.0) | Request struct, `#warning` | The 2.0 emitter writes a `#warning` when a `collectionFormat` has no wire mapping at its location (`DegradedCollectionFormat`); the 3.x emitters do not know the field | Keep: the consolidated emitter writes it whenever the field is set, which only 2.0 preparation sets |
| D4 | Feature (3.2) | Requests, responses, interfaces, implementations | Custom HTTP methods, `querystring` parameters, streaming item enumerators (SSE, NDJSON), multipart mixed bodies and responses, binary response parts, response summaries | Keep: the consolidated emitter is the 3.2 behaviour; older versions' records never carry these fields |

No drift was found in the generated client implementations, request writers, response matchers, links or callback
clients between any pair of emitters. The consolidation therefore starts from the 3.2 emitter's code, carries D1 and
D2 as explicit `SpecVersion` branches while the family-by-family move is held byte-identical, and then removes those
two branches in one commit each, regenerating the runtime test trees and recipes deliberately.

## Summary table

Prepared by a document's own version; "own" is that emitter's output. Files only one side writes, and files whose
content differs (with the count of lines present on one side only).

| document | scope | emitter | only here | only in own | differing files |
|---|---|---|---|---|---|
| covspec-2.0 | paths | 3.0, 3.1 | 0 | 0 | RenderWidgetRequest.cs (D3) |
| covspec-2.0 | paths | 3.2 | 0 | 0 | RenderWidgetRequest.cs (D3), IApiWidgetsClient.cs, IApiUploadsClient.cs, IApiDefaultClient.cs (D1) |
| petstore-2.0 | paths | 3.0, 3.1 | 0 | 0 | none |
| petstore-2.0 | paths | 3.2 | 0 | 0 | every request struct, one line each (D2) |
| covspec-3.0 | paths | 3.1 | 0 | 0 | none |
| covspec-3.0 | paths | 3.2 | 0 | 0 | IApiDefaultClient.cs, IApiItemsClient.cs, IApiSearchClient.cs (D1) |
| covspec-3.0 | paths | 2.0 | 0 | 0 | the 2.0 emitter's capability gaps (links, text and binary responses): not drift, see above |
| covspec-3.1 | paths | 3.0 | 0 | 0 | none |
| covspec-3.1 | paths | 3.2 | 0 | 0 | IApiDefaultClient.cs, IApiItemsClient.cs, IApiSearchClient.cs (D1) |
| petstore-3.0, petstore-3.1 | paths | all | 0 | 0 | none |
| callbacks-3.0, 3.1, 3.2 | callbacks | 3.0, 3.1, 3.2 | 0 | 0 | none (the 2.0 emitter rejects the callback scope) |
| covspec-3.2 | paths | 3.0, 3.1 | 0 | 0 | 16 files: all D4, plus D2 on ListItemsRequest.cs |
| petstore-3.2 | paths | all | 0 | 0 | none |
