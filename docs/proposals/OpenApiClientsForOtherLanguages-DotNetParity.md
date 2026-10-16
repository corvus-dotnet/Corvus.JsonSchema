# .NET OpenAPI client generator: capability checklist

Paths: G31 = src/Corvus.Text.Json.OpenApi31/OpenApi31CodeGenerator.cs; G32 = src/Corvus.Text.Json.OpenApi32/OpenApi32CodeGenerator.cs (3.2 specs; has streaming and multipart-response accessors G31 lacks); H = src/Corvus.Text.Json.OpenApi.CodeGeneration/CodeEmitHelpers.cs; RT = src/Corvus.Text.Json.OpenApi; HT = src/Corvus.Text.Json.OpenApi.HttpTransport; PO = src/Corvus.Text.Json.OpenApi.Polly. Spec fixture: tests/Corvus.Text.Json.OpenApi.CodeGeneration.Tests/TestData/covspec-3.1.json. Doc = docs/OpenApi.md.

| # | Capability | Doc section (line) | Generator method(s) (file:line) | Runtime support types | covspec-3.1 operation |
|---|---|---|---|---|---|
| 1 | Client class per tag/API (`ApiXClient`), ctor takes `IApiTransport`, DisposeAsync | What the Generator Produces (133-143) | G31 EmitImplementation:4448, ctor :4477, DisposeAsync :4507 | RT IApiTransport.cs; HT HttpClientTransport.cs | all operations |
| 2 | Client interface (DI/testing contract) | What the Generator Produces (133-143) | G31 EmitInterface:4293, EmitInterfaceMethodSignature:4415 | RT IApiTransport.cs | all |
| 3 | Request struct per operation (serialises path/query/header/cookie) | Client Flow (168-177) | G31 EmitRequestStruct:2510, EmitRequestFields:2592, EmitRequestConstructor:2607 | RT IApiRequest.cs, OperationMethod.cs | all |
| 4 | Response struct per operation (status, body, headers; `await using`) | Client Flow (168-177) | G31 EmitResponseStruct:3049, DisposeAsync:3227, EmitCreateAsync:3610 | RT IApiResponse.cs | all |
| 5 | Model types in `{root}.Models` namespace; request/response/client in root namespace | Namespace Layout (155-164) | G31 Generate:378 (CollectSchemaPointers:196) | | all |
| 6 | Client name prefix from title or `--clientName` | CLI Reference (1454-1470) | H GeneratedClientTypeNaming.cs | | n/a |
| 7 | Path parameter, simple style | Parameter Styles (190-202) | G31 EmitWriteResolvedPath:2648, EmitPathSegmentWrites:2669 | | getItem (`itemId`) |
| 8 | Path parameter, label style (explode and non-explode) | Parameter Styles (190-202) | G31 :2669; H :962, :1076, :1084 | RT StyleValueSplitter.cs | getAdvancedStyles (`ids`), getByLabelItems |
| 9 | Path parameter, matrix style (explode and non-explode, object) | Parameter Styles (190-202) | G31 :2669; H :966, :1077-1096 | | getByMatrixCodes, getByMatrixTags, getByStyledObject |
| 10 | Path parameter, simple array | Parameter Styles (190-202) | G31 :2669 | | none found (array path params not isolated) |
| 11 | Query form style, scalar | Parameter Styles (190-202) | G31 EmitWriteQueryString:2723 | RT FormUrlEncodedQueryStringWriter.cs | listItems (`active`,`category`,`page`,`sort`,`verbose`), getAdvancedStyles (`limit`,`weight`,`score`) |
| 12 | Query form array, explode (`tags=a&tags=b`) | Parameter Styles (190-202) | G31 :2723 | | getAdvancedStyles (`matrixTags`, form explode) |
| 13 | Query deepObject filter, `Filter.Build(...)` | Deep-Object Filters (Client) (204-216) | G31 :2723 (style resolved :1365) | | searchItems (`filter`) |
| 14 | Query pipeDelimited / spaceDelimited arrays | Parameter Styles (190-202; styles not named in table) | G31 :2723; H style helpers | | searchItems (`tags` pipe, `coords` space) |
| 15 | Invalid/mismatched style or unknown `in` location handled | Parameter Styles (190-202) | G31 parameter extraction ~:1365 | | getQuirky (`in: custom`), getStyledQuirky (path `form`, query `simple`) |
| 16 | Header parameter | Parameter Styles (190-202) | G31 EmitWriteHeaders:2765 (HeaderCallback) | RT HeaderCallback.cs | createItem (`X-Correlation-Id`), uploadFile (`X-File-Name`) |
| 17 | Accept header derived from declared response media types | Client Flow (168-177) | G31 EmitWriteHeaders:2765 (`acceptMediaTypes`), `HasHeaderParameters` :2565 | | getItem (json + text/plain), exportData |
| 18 | Cookie parameter, `Cookie` header | Cookie Authentication (1043-1049) | G31 EmitWriteCookies:2811 | | searchItems (`session`,`prefs`), searchItemsAdvanced |
| 19 | Parameter default values and descriptions in XML docs | Quick Start (41-81) | G31 EmitMethodDoc:5047 (`Default:` note) | | listItems ("various default parameter values") |
| 20 | Path-level parameters inherited by operations | Parameter Styles (190-202) | G31 DescribeOperations:422 (PathItem.Parameters :284) | | getAdvancedStyles (`ids` at path level) |
| 21 | Required vs optional parameters in signature | Quick Start (41-81) | G31 BuildParameterList (used :4561), EmitInterfaceMethodSignature:4415 | | listItems |
| 22 | JSON request body via generated `Build(...)` / `Source` | JSON Bodies (Client) (295-311) | G31 EmitClientMethod:4557, BuildParameterList (`{Body}.Source body` ~:5118) | | createItem, searchItemsAdvanced |
| 23 | Optional body omitted when not supplied (no empty body sent) | Optional Bodies (291-293) | G31 EmitClientMethod:4557 (`optionalJsonBody` ~:4600) | | optionalBodyProbe (`required: false`) |
| 24 | Closure-free, context-threaded body (`Source<TContext>`, `MethodAsync<TContext>`, `allows ref struct`) | Building a Body Without Closures (270-289) | G31 EmitClientMethod:4557 (`contextThreaded`), EmitContextConstraint:4440, EmitInterfaceMethodSignature:4415; body-type detection :2273, :2484 | RT (generated `Source<TContext>` on models) | createItem, searchItemsAdvanced (object bodies) |
| 25 | Scalar body: no `Source<TContext>` overload emitted | Building a Body Without Closures (270-289) | G31 :2484 | | none found |
| 26 | Form-urlencoded body (builder, encoded on send) | Form-Encoded Bodies (332-361) | G31 EmitClientMethod:4557 (`isFormUrlEncodedBody`, :4748); IsFormUrlEncodedRequestBody | RT FormUrlEncodedSerializer.cs | updateItemForm, submitFeedback |
| 27 | Form per-property `encoding` (style, explode, allowReserved, contentType) | Form-Encoded Bodies (332-361; encoding not detailed) | G31 :1866-1873, EmitStaticEncodingFields:4926, EmitEncodingsField:4975 | RT PropertyEncoding.cs | submitFeedbackEncoded, uploadAttachmentEncoded |
| 28 | `--ignoreEmptyFormUrlEncodedBody` (propertyless form body treated as absent) | CLI Reference (1454-1470), Stripe example | G31 ctor :69, flag :37, :1401, :1609 | | none found |
| 29 | multipart/form-data upload, typed non-binary parts + `BinaryPartData` | File Upload (Multipart) (447-478) | G31 EmitClientMethod:4557 (`isMultipart`:4520), EmitBinaryPartsDictionary:5019, IsMultipartRequestBody:2376 | RT BinaryPartData.cs, MultipartFormDataSerializer.cs | uploadItemData, uploadAttachment, uploadAttachmentEncoded |
| 30 | Binary parts written after non-binary parts; async write callback `WriteContentAsync` | File Upload (Multipart) (447-478) | G31 :5019-5045 | RT BinaryPartData.cs, MultipartBinaryOrdering.cs | uploadAttachment |
| 31 | multipart/mixed request body (positional parts) | File Upload (447-478), Server notes | G32 EmitMultipartMixedBody:6945 | RT MultipartMixedSerializer.cs | none found |
| 32 | Raw `application/octet-stream` body: `Stream` overload plus write-callback overload | File Upload (447-478) | G31 BuildParameterList (`Stream body` ~:5111), `rawStreamAsWriter` param on EmitClientMethod:4557; IsRawStreamRequestBody | RT JsonStreamWriter.cs | uploadFile |
| 33 | JSON response body typed, per status code | Response Handling (239-262) | G31 EmitResponseBodyForCategory:3740, EmitTryGetMethod:3923 | RT ResponseMatcher.cs | listItems, createItem |
| 34 | Content-type branching when one status has several media types | Response Handling (239-262) | G31 EmitContentTypeBranching:3765 | RT ResponseMatcher.cs | getItem (json + text/plain), exportData (default: json + octet-stream) |
| 35 | `MatchResult<TResult>` with one handler per status / `default` | MatchResult (241-252) | G31 EmitMatchResult:3994 (:4088), EmitMatchCall:4202, EmitMultiCategoryMatchDispatch:4221, EmitMultiCategoryDefaultMatchDispatch:4259 | RT ResponseMatcher.cs | listItems (200 + default) |
| 36 | `MatchResult<TContext,TResult>` (closure-free matcher) | MatchResult (241-252; overload not shown) | G31 EmitMatchResult:4084 | | listItems |
| 37 | `MatchResult<ValueTask>` for async handlers / stream matchers (`matchOkStream`) | File Download (600-621) | G31 EmitMatchResult:3994 | | downloadFile |
| 38 | `TryGet{Status}` accessors | Response Handling (239-262; not named) | G31 EmitTryGetMethod:3923 | | listItems |
| 39 | Typed response headers (`XTotalCountHeader`), scalars and arrays | Response Headers (254-262) | G31 DescribeResponseHeaders:492, EmitResponseHeaderProperties:3801, EmitReadResponseHeaders:3861, PrepareResponseHeaders:1879 | RT IResponseHeaders.cs, HeaderValueParser.cs | getItem (`X-Rate-Limit`,`X-Active`,`X-Tags`,`X-Page-Sizes`,`X-Flags`), getAdvancedStyles (`X-Counts`,`X-Scores`,`X-Weights`), searchItems (`X-Facets`) |
| 40 | `text/plain` response, lazy buffering (`GetOkTextAsync`, `OkTextStream`, `OkText`, `OkUtf8Bytes`) | Text Responses (264-266) | H EmitTextPlainResponseBody:2280, EmitTextPlainProperties:2305 (emits `Get{Status}TextAsync`, `{Status}TextStream`); G31 IsTextOnlyResponse:2430 | RT TextResponseBody.cs | getItem (`text/plain` 200) |
| 41 | Binary / raw stream response (`OkStream`) | File Download (600-621) | G32 IsOctetStreamResponse:3569; G31 EmitResponseBodyForCategory:3740 | | downloadFile (`*/*`), exportData |
| 42 | Wildcard media types (`*/*`, `application/*`) classified | File Download (600-621; not stated) | H ClassifyMediaType (~:2365-2396) | | downloadFile, exportData |
| 43 | Multipart response (client): `Get{Status}MultipartAsync`, `BinaryPartHandle.OpenStreamAsync`, `maxNonBinaryPartsLength` | Multipart Responses (Client) (581-598) | G32 EmitMultipartResponseAccessors:4548, :4576, EmitMultipartViewStructs:4607, HasMultipartResponse:3631 | RT MultipartResponseBody.cs, StreamingMultipartReader.cs, BinaryPartHandle.cs, MultipartMixedReader.cs, MultipartFormReader.cs, RequiredBinaryPartMissingException.cs | none found |
| 44 | SSE response: `Enumerate{Status}Items()` and `Enumerate{Status}SseItems()` (`SseEvent<T>`) | Server-Sent Events (365-398) | G32 EmitStreamingItemMethods:5255 (:5280, :5304) | RT SseEvent.cs, JsonStreamReader.cs | none found (itemSchema is 3.2 only) |
| 45 | NDJSON response: `Enumerate{Status}Items()` (pooled `ParsedJsonDocument<T>`) | NDJSON (400-427) | G32 EmitStreamingItemMethods:5255, GetStreamingContent:3510 | RT JsonStreamReader.cs | none found |
| 46 | Client-side validation of parameters and request body before send | Client-Side Validation (625-648) | G31 EmitValidate:2853, EmitBodyValidation:3025 | RT ValidationMode.cs, ValidationDetail.cs, SchemaValidationDetail.cs | listItems (constraints) |
| 47 | `ValidationMode.Basic` / `Detailed` / `None` argument on each method | Client-Side Validation (625-648) | G31 EmitValidate:2859-2872 | RT ValidationMode.cs | all |
| 48 | Response validation (`responseValidationMode`) | Client Flow (168-177; not named) | G31 EmitResponseValidate:2932, EmitResponseValidateBodies:2976 | RT ValidationMode.cs | createItem |
| 49 | Validation failure surfaces as `InvalidOperationException` | Client-Side Validation (625-648) | G31 EmitValidate:2853 | RT ThrowHelper.cs, SR.cs | listItems |
| 50 | Cancellation: `CancellationToken cancellationToken = default` on methods, links, CreateAsync | Quick Start (41-81; implied) | G31 :3411, :3640 | | all |
| 51 | Servers: `CreateServerUri()` static for default server | Quick Start (41-81; BaseAddress) | G31 EmitCreateServerUri:4342 (no variables :4353) | | getEmptyServers (`servers: []`) |
| 52 | Server variables: `CreateServerUri(environment, version, ...)` with defaults/enums | Quick Start (41-81; not detailed) | G31 EmitCreateServerUri:4378, GetDefaultServerInfo:2105, ResolveEffectiveServer ~:1405 | | root `servers` (`{environment}` enum, `{version}`) |
| 53 | Path-level and operation-level `servers` override | Not in doc | G31 ResolveEffectiveServer ~:1405, `EffectiveServer` :99 | | /items (path servers), getItem (op servers), getEmptyServers |
| 54 | Deprecated operations get `[Obsolete]` | Not in doc | G31 :4423 (interface), :4566 (method) | | none found |
| 55 | XML doc comments from summary/description/params | Not in doc | G31 EmitMethodDoc:5047 | | summaries on optionalBodyProbe, listItems, createItem, uploadFile, submitFeedback |
| 56 | Operation naming from operationId (else derived) | Not in doc | G31 DescribeOperations:422 | | all |
| 57 | Links: `response.{Status}Links.{Op}Async()` typed follow-on calls | Links (1380-1402) | G31 EmitLinksAccessor:3273, EmitLinkMethod:3325 (:3381), EmitRuntimeExpressionValue:3483 | RT IApiResponse.cs | createItem (`GetCreatedItem`,`SearchByName`), getItem (`RefreshItem`), search (`NextPage`) |
| 58 | Link parameter from `$response.body#/...` | Runtime Expressions (1352-1378) | G31 :3483 | | createItem, getAdvancedStyles (`GetFirstItemById`) |
| 59 | Link parameter from `$response.header.X` | Runtime Expressions (1352-1378) | G31 :3483 | | getAdvancedStyles (`GetItemByHeader`) |
| 60 | Link parameter from `$request.body#/`, `$request.query`, `$request.header`, `$request.path` (request context captured, `bodyValue`) | Runtime Expressions (1352-1378) | G31 HasRequestBasedExpressions / HasRequestBodyExpressions (used :4562-4593) | H RuntimeExpression.cs, RuntimeExpressionKind.cs | uploadFile (`ReUpload`), submitFeedback (`UpdateFeedback`) |
| 61 | Link parameter literal / URL / method expression | Runtime Expressions (1352-1378; not named) | G31 :3483 | H RuntimeExpression.cs | getAdvancedStyles (`SearchWithLiteral`,`SearchWithUrl`,`SearchWithMethod`) |
| 62 | Unrecognised / unresolvable link expression handled | Not in doc | G31 :3483, :3325 | | getAdvancedStyles (`UnrecognizedExpr`), getItem (`NonExistentLink`, links to missing operation `doesNotExist`) |
| 63 | Link by `operationId` reference to component link | Links (1380-1402) | G31 :3325 | | components.links (`getItem`) |
| 64 | Callback client (`openapi-callback-client`): webhooks + per-operation callbacks | Callback Client (1323-1350) | G31 GenerateCallbackClient:5245, CollectWebhookAndCallbackSchemaPointers:5286, ListWebhookAndCallbackOperations:5385 | HT HttpClientTransport.cs | none found (spec has no webhooks/callbacks; see callbacks-3.1.json) |
| 65 | Webhook path-items from `components.pathItems` | Webhooks and Callbacks (1268-1292) | G31 :5286, :5385 | | components.pathItems (`healthCheck`) |
| 66 | Callback runtime expression URL (`{$request.body#/callbackUrl}`) | Runtime Expressions (1352-1378) | G31 :5245 | H RuntimeExpression.cs | none found (callbacks-3.1.json) |
| 67 | 2.0 documents rejected by callback client | OpenAPI 2.0 notes (1623-1633) | src/Corvus.Json.Cli.Core/OpenApiCallbackClientCommand.cs | | n/a |
| 68 | Auth: transport takes `IHttpAuthenticationProvider` (HttpClientTransport ctor) | Authentication (806-812) | n/a (runtime) | HT IHttpAuthenticationProvider.cs, HttpClientTransport.cs:79 | n/a |
| 69 | Auth: bearer token | Bearer Token (939-963) | not generator-emitted (DelegatingHandler pattern in doc) | HT BearerTokenAuthenticationProvider.cs | components.securitySchemes `bearerAuth` |
| 70 | Auth: API key header / query / cookie | API Key (965-998) | not generator-emitted | HT ApiKeyAuthenticationProvider.cs, ApiKeyLocation.cs | `apiKeyAuth` (header) |
| 71 | Auth: HTTP Basic | Basic Authentication (1000-1009) | not generator-emitted | HT BasicAuthenticationProvider.cs | none found |
| 72 | Auth: anonymous (default, no handler) | Anonymous (1011-1021) | n/a | HT HttpClientTransport.cs | n/a |
| 73 | Auth: Entra ID / MSAL via DelegatingHandler | Microsoft Entra ID (814-937) | n/a | n/a (Azure.Identity) | n/a |
| 74 | Auth: OAuth2 scope constants (`SecuritySchemes.*`, `SecurityRequirements.{Op}Oauth2Scopes`, `AllOauth2Scopes`) documented as emitted on the client interface | Authentication (806-812), Entra ID (814-937) | G31 ~:6634 (scope arrays emitted from OperationSecurityRequirement.Scopes), ExtractSecurityRequirements:1428, ParseSecurityRequirements:1481; G32 PrepareSecuritySchemes:2655 (SecuritySchemeInfo with AvailableScopes) | | `oauth2Auth` scheme; listItems has operation `security` |
| 75 | Auth: cookie API-key scheme name constant `SecuritySchemes.{Name}KeyName` (client) | Cookie Authentication (1043-1229) | G32 only (`KeyName` emission, 7 sites); not in G20/G30/G31 | | none found (no cookie apiKey scheme) |
| 76 | Auth: per-request override via `HttpRequestMessage.Options` | Per-Request Authentication Override (1039-1041) | n/a | HT HttpClientTransport.cs | n/a |
| 77 | Auth + resilience composition via `AddStandardResilienceHandler` | Combining Authentication with Resilience (1023-1037) | n/a | n/a (Microsoft.Extensions.Http.Resilience) | n/a |
| 78 | Polly: `ResilientApiTransport(inner, pipeline)` wrapping any `IApiTransport` | Resilience (Polly Integration) (1231-1257) | n/a (runtime) | PO ResilientApiTransport.cs | n/a |
| 79 | Retry-safe request bodies (JSON re-serialise, write callbacks re-invoked, seekable stream rewound, non-seekable fails) | Retries and Request Bodies (1259-1266) | G31 EmitClientMethod:4557 (callbacks for form/multipart/raw) | PO ResilientApiTransport.cs; RT BinaryPartData.cs | uploadFile, uploadAttachment |
| 80 | Telemetry / instrumented transport | Not in doc | n/a | RT InstrumentedApiTransport.cs, OpenApiTelemetry.cs | n/a |
| 81 | Transport factory (`IApiTransportFactory`) | Not in doc | n/a | RT IApiTransportFactory.cs; HT HttpClientApiTransportFactory.cs | n/a |
| 82 | Lock file `corvusjson-openapi.lock`: skip when current, `--force`, records include/exclude, files, `descriptionLocation` | Lock File and Regeneration (1608-1610) | H OpenApiLockFile.Create:35, TryLoad:100, IsUpToDate:135, Save:193; CLI OpenApiGenerateCommand.cs:88 | H OpenApiLockFileModel.cs | n/a |
| 83 | `--spec-url`: fetch spec, store locally, record in lock file, re-fetch on update | CLI Reference (1454-1470) | CLI OpenApiGenerateCommand.cs:37-49 | | n/a |
| 84 | Path filters `--include-path` / `--exclude-path` (`*`, `**`, `{param}`, case-insensitive, additive/subtractive) | Path Filtering (1515-1606) | H OperationFilter.cs:14; `filter` param on G31 Generate:378 | | n/a |
| 85 | Filtering is operation-level; models for retained operations still emitted | Path Filtering (1515-1606) | G31 Generate:378, CollectSchemaPointers:196 | | n/a |
| 86 | `openapi-show` previews operations, `--group-by path\|tag` | Inspecting Operations (1500-1513) | G31 ListOperations:238, ListTags:321 | H OperationSummary.cs, TagInfo.cs | n/a |
| 87 | Spec version auto-detect (`openapi`/`swagger` field) and `--specVersion` override (2.0/3.0/3.1/3.2) | Version Support (1612-1621) | CLI OpenApiGenerateCommand.cs:116 (20), :147-234 (30/31/32 dispatch) | | n/a |
| 88 | OpenAPI 3.0 models (Draft 4 subset dialect) | Version Support (1612-1621) | src/Corvus.Text.Json.OpenApi30 generator | | covspec-3.0.json |
| 89 | OpenAPI 3.1 models (JSON Schema 2020-12) | Version Support (1612-1621) | G31 | | covspec-3.1.json (this file) |
| 90 | OpenAPI 3.2 (itemSchema streaming) | Version Support (1612-1621) | G32 | | covspec-3.2.json |
| 91 | OpenAPI 2.0 lowering: body param, formData to form/multipart, collectionFormat (csv/ssv/tsv/pipes/multi), consumes/produces, host+basePath+schemes server URL | OpenAPI 2.0 notes (1623-1633) | src/Corvus.Text.Json.OpenApi20 OpenApi20CodeGenerator (CLI :116) | | covspec-2.0.json |
| 92 | Same client API surface across spec versions | Version Support (1612-1621) | G31/G32/OpenApi30/OpenApi20 | | n/a |
| 93 | YAML specs (`--yaml`, extension auto-detect) | Not in doc table | CLI OpenApiGenerateSettings.cs | | n/a |
| 94 | `$ref` resolution (local and external) | Not in doc | H LocalReferenceResolver.cs, ExternalReferenceResolver.cs, IOpenApiReferenceResolver.cs | | n/a |
| 95 | Operation without parameters or body (plain call) | Quick Start (41-81) | G31 EmitClientMethod:4557 (`hasParams`) | | getEmptyServers, downloadFile |

## CLI options: `openapi-client` and `openapi-callback-client`

Both commands bind the same `OpenApiGenerateSettings` (src/Corvus.Json.Cli.Core/OpenApiGenerateSettings.cs) deriving from `OpenApiSettings` (src/Corvus.Json.Cli.Core/OpenApiSettings.cs). Registered in CliAppFactory.cs:36 and :42. The callback client processes only webhook and callback path-items.

| Name | Default | Description (settings text) |
|---|---|---|
| `<SpecFile>` (positional) | required | Path to the OpenAPI specification file (JSON or YAML). |
| `--rootNamespace` | derived from spec title | The root namespace for generated types. |
| `--outputPath` | `./` (doc) | The path to which to write the generated code. |
| `--clientName` | API title | The prefix for generated client type names. |
| `--force` | false | Force regeneration even if the lock file indicates no changes. |
| `--spec-url` | none | Original URL of the API description; spec is fetched from it and stored locally; recorded in the lock file for update-style re-fetch. |
| `--serverBinaryParts` | `buffer` | Server-only: `buffer` or `stream` for multipart binary parts (stream supported for 3.2). No client effect. |
| `--ignoreEmptyFormUrlEncodedBody` | false | Treat form-urlencoded request bodies whose schema defines no properties as absent. |
| `--yaml` | auto-detect from .yaml/.yml | Treat the input specification as YAML. |
| `--include-path` (OpenApiSettings) | all paths | Glob patterns for paths to include (repeatable or comma-separated). |
| `--exclude-path` (OpenApiSettings) | none | Glob patterns for paths to exclude (repeatable or comma-separated). |
| `--filter` (OpenApiSettings) | none | Deprecated: use `--include-path`. |
| `--specVersion` (OpenApiSettings) | auto-detected | OpenAPI spec version to use. Settings text says "3.0 or 3.1"; doc says `2.0`, `3.0`, `3.1`, `3.2`. |
| `--group-by` (OpenApiSettings) | `path` | Applies to `openapi-show` (`path` or `tag`). |
