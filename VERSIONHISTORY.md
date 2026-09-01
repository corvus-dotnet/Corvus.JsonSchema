# Version History

## V5.5.3

V5.5.3 fixes document corruption in the mutable builder's JSON Patch `move` and `copy` operations, and corrects `move` destination resolution to match RFC 6902. There are no new features and no breaking changes.

### Bug fixes

- **Moving or copying a value within the same document no longer corrupts the builder.** The builder's move and copy primitives capture the value being relocated before removing it, and the capture used the cross-document mechanism, which records a value as a reference addressed by row index into the source document's metadata table. When the source document is also the destination, the operation's own row-table surgery (the removal and the insertion) shifts those indices, so the relocated value ends up referencing whatever row later occupies that position. Depending on where the stale reference lands, the document silently contains wrong values (including property-name bytes read as data), serialization throws `ArgumentException` ("'t' is an invalid end of a number. Expected a delimiter.", the report in the issue), or the reference resolves to itself and reading the document overflows the stack. Randomized testing measured roughly half of all move-containing patch sequences corrupting a builder that parsed its own content; builders importing a parsed document were affected once a locally added value was moved. Same-document captures now copy the value's rows by content: row locations are byte offsets into the document's append-only backing buffers, which survive row-table surgery, with an end-object row's property-map index restored to its plain length because map entries hold absolute row positions. The fix sits at the one point every affected operation funnels through, covering the move primitives, the copy overlap fallbacks, `SetProperty`'s builder fallback, and root replacement; the fast paths that insert a direct reference row (`SetProperty`, `TryReplaceProperty`, `SetItem`, `InsertItem`) now route same-document sources through the same safe path. A randomized harness verifying 1500 patch sequences against an independent RFC 6902 implementation after every operation guards the fix. See [#954](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/954).

- **A `move` destination is resolved against the document after the removal.** RFC 6902 defines `move` as a remove followed by an add, with the destination path evaluated against the document that results from the removal. The patch dispatcher resolved the destination before removing, so a destination pointer that descended through the source's parent array at a later index landed one element early, silently placing the value on the wrong element. The dispatcher now rewrites that single ancestor segment to its pre-removal index before resolving; a destination whose final segment indexes the same array was already applied after the removal by the move primitives and is unaffected. See [#954](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/954).

## V5.5.2

V5.5.2 fixes a defect in the typed generated models that can make validation fail in both directions when a property name is declared in more than one composed schema scope. There are no new features and no breaking changes.

### Bug fixes

- **A property declared in both a schema's `properties` and its `$ref` or `allOf` target validates in every scope.** When a generated type hoists a composed `$ref` or `allOf` branch's property validation into its own object enumeration (which happens when the composition contributes four or more properties), the property-name lookup mapped each name to a single dispatch site, with local and hoisted entries indexed independently and no deduplication by name. A name declared both locally and in a hoisted branch, or in two hoisted branches, therefore reached only one of its validation sites, and whichever site lost the lookup was silently skipped: its `required` tracking never saw the property, so a present property was reported as "Required property not present" (the false rejection reported in the issue), and its property subschema was never evaluated, so an instance violating, for example, a `const` on the shared name was accepted. The lookup and every switch that consumes it are now built from a single dispatch plan with one entry per distinct property name, whose case executes the local match and every hoisted branch body. Compositions below the hoisting threshold and the standalone evaluator were not affected, and emitted code is unchanged for schemas with no shared names. See [#949](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/949).

- **A type hoisting branches from both `$ref` and `allOf` compiles and dispatches correctly.** Branch-scoped identifiers in the hoisted validation code were derived from the branch's position within its own keyword alone, so a type composing hoistable branches from two keywords (for example a `$ref` sibling of an `allOf`) emitted colliding fields and locals that failed to compile, and the shared property-name lookup's indices did not line up with each keyword group's own dispatch switch. Identifiers are now disambiguated across keyword groups, and each group's switch carries its entries' indices from the shared plan. See [#949](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/949).

## V5.5.1

V5.5.1 fixes two defects in the standalone schema evaluator surface, one of which can make a generated evaluator validate incorrectly in both directions. There are no new features and no breaking changes.

### Bug fixes

- **The standalone evaluator compiles a distinct regex for every distinct `pattern`.** The V5 standalone evaluator named its compiled `Regex` fields for the `pattern` keyword after the keyword itself, so every distinct full-regex `pattern` in a document shared one field name, and a document-wide deduplication set meant only the first pattern's regex was ever compiled. Every other pattern site then matched against that first regex while reporting its own pattern text in diagnostics, producing both false rejections (a valid instance rejected) and false acceptances (an invalid instance accepted). A second, related collision affected `patternProperties`, whose fields were already named by sanitized pattern text. Distinct patterns that sanitize to the same identifier (for example `^[a-b]+$` and `^[a.b]+$`) also shared one compiled regex. Regex fields are now allocated through a per-document registry keyed by the raw pattern text and uniquified when sanitized identifiers collide, so every distinct pattern gets its own compiled regex, and a pattern site that cannot find its regex fails generation loudly instead of silently reusing the wrong one. The typed generated-model output was not affected, because its regex fields are scoped per generated type. See [#947](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/947).

- **`patternProperties` subschemas are bound to the right patterns regardless of culture.** The shared keyword layer paired each `patternProperties` pattern with its subschema by positionally zipping two sorted lists, one sorted ordinally and one with the culture-sensitive default comparer. The orders diverge whenever linguistic and ordinal comparison disagree for a pattern pair, for example `^B$` and `^a$` on every runtime, or `^[a-b]+$` and `^[a.b]+$` on .NET Framework, and each pattern was then validated against the other pattern's subschema. Both the typed generated models and the standalone evaluator consumed the cross-wired pairing. Both lists are now sorted ordinally. See [#947](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/947).

- **The CLI emits the standalone evaluator for `--codeGenerationMode Both` and `SchemaEvaluationOnly`.** The V5 CLI accepted both modes but never emitted an evaluator. `Both` produced the typed surface only, and `SchemaEvaluationOnly` produced no output at all, in each case exiting 0 with no diagnostic. Evaluator emission requires the unreduced root types to be registered on the language provider, which the Roslyn source generator did (so `EmitEvaluator = true` worked there) but the CLI generation driver did not. The driver now registers the root types whenever an evaluator-producing mode is selected, covering both the `jsonschema` command and the driver-config path, and end-to-end CLI tests cover both modes. See [#926](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/926).

## V5.5.0

V5.5.0 adds retrieval of an element's location as an RFC 6901 JSON Pointer, and completes the numeric conversion surface of generated types with direct casts to every numeric CLR type. The new cast operators change the failure mode of lossy numeric casts, which is a breaking change.

### New features

- **Retrieve an element's location as a JSON Pointer.** `JsonElement` (and every generated type, via `IJsonElement<T>` extension methods) can now report its location relative to its document root as an [RFC 6901](https://datatracker.ietf.org/doc/html/rfc6901) JSON Pointer. The zero-allocation `TryGetJsonPointer` overloads write UTF-8 bytes or UTF-16 characters into a caller-supplied buffer and return the written length; the `GetJsonPointer()` convenience method returns a string. The pointer is derived on demand by walking the document's metadata database from the element back to its ancestors, so parsing carries no extra bookkeeping. Property names are unescaped and then pointer-escaped (`~` as `~0`, `/` as `~1`), and the root element produces the empty pointer. This makes it straightforward to turn JSONPath query results into JSON Patch targets. See [#942](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/942).

- **Direct casts from generated numeric types to every numeric CLR type.** Generated numeric types now emit explicit conversion operators for `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `ulong`, and `float` (plus `Int128`, `UInt128`, and `Half` on .NET), alongside the existing implicit `long`, `double`, and format-specific conversions. A cast such as `(ushort)value` previously routed through the implicit conversion to `long`, which tripped the IDE0221 analyzer; some casts, such as `(ulong)` on a plain integer type, were ambiguous and did not compile, and the .NET-only numeric types could not be cast to at all. Every numeric cast now binds a user-defined operator directly. See [#937](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/937).

### Breaking changes

- **Lossy numeric casts now throw `FormatException` instead of silently truncating.** Because casts to narrower types previously converted through `long`, an out-of-range value was silently wrapped: `(byte)` applied to a value of 300 produced 44. The new direct operators are range-checked, so an out-of-range or non-integral value now throws `FormatException`, matching the V4 behavior. Code that relied on silent truncation should cast to `(long)` explicitly and narrow with standard numeric casts. See [#937](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/937).

## V5.4.3

V5.4.3 completes the CS0618 suppression in generated code that V5.4.2 attempted. There are no new features and no breaking changes.

### Bug fixes

- **Generated code no longer warns CS0618 from the V5 source generator.** The V5.4.2 fix wrapped the `DefaultInstance` initializer in a CS0618 suppression in the V4 code generator only, so projects using `Corvus.Text.Json.SourceGenerator` still saw the warning. The V5 generator emits through its own layer, which had three places that call the generated type's obsolete `ParseValue` without a suppression: the `DefaultInstance` initializer for a schema whose `default` is an object or an array, and the static validation constants for object-valued and array-valued `const` and `enum` keywords. All three now wrap the emitted member in a localized CS0618 suppression, so a consumer project without a `NoWarn` for CS0618, including warnings-as-errors builds, compiles generated code cleanly. See [#944](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/944).

## V5.4.2

V5.4.2 lets generated OpenAPI server stubs stream large multipart uploads, and multipart responses, without loading them into memory, giving the server the property the generated client already had. Streaming is opt-in and the buffered path stays the default indefinitely, so existing generated code is unaffected. The release also hardens the buffered path, adds retry safety for streamed request bodies, fixes two security defects found reviewing the streaming surface, and rolls in an unrelated base64 content validation fix. There are no breaking changes.

### New features

- **Streaming multipart uploads in generated server stubs.** A new generator option, `--serverBinaryParts stream` (OpenAPI 3.2), makes a generated `multipart/form-data` endpoint stop buffering its binary parts. The non-binary form fields still parse into the typed `Body` first, bounded by `ApiServerOptions.MaxNonBinaryPartsLength`, and each `format: binary` property becomes a `BinaryPartHandle` the handler opens to read that part straight off the wire, for example forwarding an upload to blob storage without it ever being resident in memory. Because the typed body must be complete before the handler runs, binary-part placement matters, so streaming takes a per-registration ordering policy on `ApiServerOptions`: `RequireBinaryLast` (true streaming, a non-binary part after a binary part is rejected with 400) or `SpoolOutOfOrder` (standard browser part order, where an early binary part spools to pooled memory below a threshold and a temporary file above it, with the framework owning spool cleanup). One generated server serves both an internal API and a browser-facing app from the same code. `multipart/mixed` bodies stream too. The generated client now always emits binary parts after all non-binary parts, which RFC 7578 permits, so Corvus-to-Corvus traffic streams under the default policy unchanged. Documented under Streaming File Uploads, with a browser `FormData` sample. See [#936](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/936) and [#941](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/941).
- **Multipart responses, in both directions.** Multipart responses were previously unsupported either way. A 2xx (or default) response declaring `multipart/form-data` content with `format: binary` properties now gets a server result factory taking the typed body plus a `BinaryPartData` per binary part, streamed to the response with a fresh boundary, non-binary fields first. On the client, receiving such a response is streaming only, through a generated `Get{Status}MultipartAsync` accessor that yields the typed body plus the same wire-order handle surface the streaming server uses, read once and valid until the response is disposed. See [#936](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/936).
- **A write-callback overload for raw request bodies.** Raw `application/octet-stream` request bodies gain a `Func<Stream, CancellationToken, ValueTask>` overload alongside the `Stream` overload, in all four client generators, so a source that can only write into a stream needs no intermediate pipe. This completes the push-model symmetry with `BinaryPartData` on multipart parts. See [#936](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/936).

### Security

- **The streaming non-binary projection is bounded against its own limit.** `MaxNonBinaryPartsLength` charged only part body bytes, not the property names or JSON structure accumulated in the in-memory projection, so a request of many empty-bodied named parts grew the projection far past the configured cap (measured at 371 times over), an out-of-memory risk on the raised request-size limits streaming deployments run with. The projection is now bounded directly against the limit after each part. See [#936](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/936).
- **Spool files are created owner-only.** Under `SpoolOutOfOrder`, spooled binary parts were written to files created world-readable in a directory that may be shared (the system temporary directory by default), so a local user could read another user's in-flight upload. Spool files are now created readable and writable only by the owner. See [#936](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/936).

### Bug fixes

- **`JsonBase64Content` validation no longer throws for binary payloads.** `StandardContent.ParseEscapedJsonContentInJsonString` ran base64-decoded content through the JSON-string unescaper, which assumes already-validated input, so decoded bytes that looked like a malformed escape (a truncated `\u` near the end, a trailing backslash, or an unpaired surrogate) made it read past the buffer or throw, and the exception escaped `Validate(...)`. The decoded bytes are the content, so they are now parsed directly as a JSON document: malformed content reports `UnableToParseToMediaType` like every other non-JSON payload instead of throwing, and a valid document whose decoded bytes contain a legitimate string escape, previously mangled and reported undecodable, now round-trips. See [#940](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/940).
- **Retrying a streamed request body no longer re-sends a consumed stream.** `ResilientApiTransport` rewinds a seekable request body to its entry position before each attempt, and refuses to re-send a non-seekable body that a previous attempt already consumed, throwing a descriptive `InvalidOperationException` rather than silently sending a truncated body. Write callbacks and JSON element bodies are re-invoked and re-serialized per attempt, a contract now documented. See [#936](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/936).
- **Buffered multipart server stubs no longer silently discard binary parts.** Swagger 2.0 server stubs read and dropped `type: file` parts so the handler could never reach them, and `multipart/mixed` server stubs dropped their binary parts; both now bind the parts. The buffered request-body paths (`multipart/form-data`, `multipart/mixed`, and `application/x-www-form-urlencoded`) also enforce a size limit, 128 MiB by default and configurable through `ApiServerOptions`, rejecting an oversized body with 413 rather than buffering it unbounded, and cancellation now propagates to the host instead of being reported as a 400. See [#936](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/936).
- **WebSocket transport disposal is best-effort.** Disposing a `WebSocketMessageTransport` ran the full close handshake, waiting for the peer's close frame with no timeout, so a concurrent teardown where the peer reset the connection during the close could make disposal throw. Disposal now sends a close frame without waiting for the peer's reply, bounds it with a short timeout, and does not throw or hang when the connection has already gone away. This removes an intermittent teardown failure in the WebSocket transport integration tests.
- **Generated code no longer warns CS0618 for a type with a default value.** A type whose schema declares a `default` materializes its `DefaultInstance` through `ParseValue`, which is `[Obsolete]`, so the self-call surfaced as CS0618 in consumer projects that do not suppress it (build-breaking under warnings-as-errors). `ParseValue` is the right choice for a long-lived static, a standalone non-pooled value rather than a pooled `ParsedJsonDocument<T>` that would have to be disposed, so the generator now wraps the initializer in a localized CS0618 suppression. See [#939](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/939).

### Other changes

- **Binary and text response gaps closed.** Binary result factories are emitted for `default`-status and Swagger 2.0 responses, not only 2xx, and text-only responses gain a streaming text accessor alongside async, idempotent buffering, so a large text response carrying a proprietary protocol can be consumed as a live stream. Streaming media types (`text/event-stream`, NDJSON) are correctly excluded from the binary-style response paths. See [#936](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/936).

## V5.4.1

V5.4.1 completes the V5.4.0 delivery-context feature by extending it to request/reply responders, which V5.4.0 explicitly excluded. Like the original capability, this is a community contribution from Levy Barbosa, hardened in review before merging. There are no breaking changes: the new interface members carry default implementations, so existing `IMessageDeliveryContextTransport` implementations keep compiling and behave as before.

### New features

- **Responder delivery context** — `IMessageDeliveryContextTransport` gains `SubscribeReplyWithDeliveryContextAsync<TRequest, TReply>`, the delivery-context counterpart of `SubscribeReplyAsync`: the responder's handler receives the request payload and a `MessageDeliveryContext` (subscribed channel, headers, transport-native message) and still returns the reply payload the transport publishes correlated to the request. It has the same two-overload shape as its sibling (plain, and `in MessageContext` for binding-aware transports) and the same opt-in posture (the default implementation throws `NotSupportedException`, matching `SubscribeReplyAsync`'s own default). All seven transports implement it, the generators now emit `*WithDeliveryContext` handler and consumer variants for responder operations too, and `InstrumentedMessageTransport`'s context-capable wrappers forward the new capability through both overloads, so instrumentation does not downgrade a transport's support for it. The internal delivery plumbing mirrors the V5.4.0 design: the new public SPI type `Corvus.Text.Json.AsyncApi.Internal.MessageReplyHandler{TRequest, TReply}` stores either callback shape without a per-delivery adapter delegate, and the native message is captured only when a context handler will consume it. This feature was designed and contributed by [Levy Barbosa (@Levyks)](https://github.com/Levyks), completing the delivery-context capability credited in the V5.4.0 notes. Thank you, Levy! Contributed in [#932](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/932); review findings tracked in [#933](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/933).

### Other changes

- **Responder emission tidied** — The generated responder+context `HandleMessageAsync` no longer declares a `headers` local when the message declares no headers schema; regenerated output for such operations loses that dead line. See [#933](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/933).
- **Generated responder consumers are compiled and exercised in the build** — The runtime test suite's specification now includes a receive+reply operation, so both generated responder consumer variants compile on every build and round-trip end to end through the in-memory transport, closing the gap where responder emission was verified only by string assertions. `MessageReplyHandler` and the instrumented forwarding path gained direct tests to the same standard as their V5.4.0 siblings. See [#933](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/933).
- **PR previews skip fork pull requests** — Fork PRs run without repository secrets, so the documentation site cannot build and the preview deploy failed on a missing artifact; the preview deploy and cleanup jobs now run for same-repo pull requests only.

## V5.4.0

V5.4.0 adds an opt-in delivery-context surface to the AsyncAPI transports and generators, so a consumer can receive transport delivery metadata alongside its payload without an allocating adapter on the delivery path. This capability is a community contribution from Levy Barbosa, hardened in review before merging. That hardening tightened the subscription lifecycle across every transport and generated consumer, and some of those changes are breaking, which is why this is a minor rather than a patch release.

### Breaking changes

- **A channel has exactly one subscription, and a second subscribe throws** — Every transport now allows exactly one subscription per channel, of any kind (legacy data, delivery-context data, or responder). Subscribing a channel that already has one throws `InvalidOperationException`; previously most transports silently displaced the existing subscription, leaking a consumer that nothing could stop and stealing the channel from whoever held it. Unsubscribe first if replacement is what you want. A consequence is that the two generated consumers for an operation are mutually exclusive on their shared channel.
- **Generated consumer lifecycle is strict** — `StartAsync` on a consumer that is already started throws `InvalidOperationException` instead of silently orphaning the first subscription. `StopAsync` on a consumer that is not started throws `InvalidOperationException`; previously a static-address consumer unsubscribed its channel address unconditionally, even when this instance had never subscribed it, which could tear down another consumer's live subscription. `DisposeAsync` stops the consumer only if it is started and completes quietly otherwise, so `await using` around a consumer that never started (or whose start was refused) no longer throws — and no longer unsubscribes a channel the instance does not own. A stop that arrives while the subscription is still being established is honored: the start releases the subscription it just created and throws an `InvalidOperationException` explaining that the consumer was stopped during start; a restart issued inside that window may be refused with "already has a subscription" until the superseded start's release completes.
- **`ProcessingLoopHeartbeat` records the owning subscription** — `Start`, `Stop`, and `Tick` now take an `owner` object identifying the subscription they belong to, and `Stop` and `Tick` only act while the entry still belongs to that owner. Without this, a losing subscribe racer or a subscription still unwinding after a resubscribe could mark the live subscription's heartbeat stopped permanently, or keep it artificially fresh. Anyone calling the heartbeat directly (rather than letting a transport drive it via `ITransportOptions.Heartbeat`) must pass the owning object.
- **Generated `SendAndReceive*` requesters take the caller's `JsonWorkspace`** — The generated request/reply methods now require the workspace that owns the reply's lifetime, positioned with the message-content parameters ahead of any channel arguments. Previously the requester parsed the reply into an internal workspace and disposed it before returning, so every reply the generated requester handed back was a view over already-disposed documents, and reading any property threw `ObjectDisposedException`. The reply now stays valid until the caller disposes the supplied workspace, matching the `IMessageTransport.RequestAsync` contract introduced in V5.3.0. Call sites fail to compile until they pass a workspace, which is the point.
- **`MessageHandlerMiddleware` is an abstract class, not a delegate** — The middleware contract is now a class with generic `InvokeAsync<TState>` and `InvokeAsync<TState, TResult>` methods, mirroring Polly's `ResiliencePipeline.ExecuteAsync<TState>`, so a middleware-wrapped dispatch allocates no per-message closure at any layer. Code assigning a lambda to `ITransportOptions.HandlerMiddleware` no longer compiles: use `PollyResilienceMiddleware.Create` (unchanged), or derive a small class from `MessageHandlerMiddleware`. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **NATS and MQTT header values are compact JSON text, not base64** — Both transports' string-typed header slots now carry the headers JSON directly (ASCII-escaped on NATS per the protocol's header rules, raw UTF-8 on MQTT), removing the base64 expansion and transform. V5.4.0 consumers still decode the V5.3.0 base64 form, so upgrade consumers before producers; a V5.3.0 consumer receiving the new form treats the headers (including `traceparent`/`tracestate`) as absent, payloads unaffected. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **MQTT requests send the correlation ID only as `CorrelationData` by default** — The duplicate `corvus-correlation-id` user property is now opt-in via `CorrelationIdPropertyKey` (default `null`). Corvus-to-Corvus request/reply is unaffected (both ends already correlate on the MQTT 5 native slot); set the option only for foreign responders that cannot read `CorrelationData`. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **Kafka acknowledges through `CommitStrategy`, defaulting to `Windowed`** — Handled messages store their offset locally and the client's auto-commit interval (and close) flushes them, instead of a synchronous broker commit per message. Both strategies are at-least-once; after a crash, up to one auto-commit interval (five seconds by default) of already-handled messages redelivers where previously about one message did. Set `KafkaTransportOptions.CommitStrategy = KafkaCommitStrategy.PerMessage` to keep the old behavior. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **A subscription no longer ends when the token that subscribed it is cancelled** — Every broker transport linked its per-subscription cancellation source to the `SubscribeAsync` caller's token, so cancelling that token later, however unrelated its purpose, silently tore down the running subscription and its consume loop outside the lifecycle that unsubscribe and dispose own. The subscription's source is now independent: the caller's token governs only the establishing call itself, and a subscription ends only through `UnsubscribeAsync` or disposal. Code that relied on cancelling the subscribe token as an ersatz unsubscribe must call `UnsubscribeAsync`.

### New features

- **AsyncAPI message delivery context** — The new `IMessageDeliveryContextTransport` capability interface adds `SubscribeWithDeliveryContextAsync`, whose handler receives a `MessageDeliveryContext` carrying the subscribed channel (as UTF-8 bytes), the message headers, and the transport-native message when one exists. All seven transports implement it (AMQP, Azure Service Bus, Kafka, MQTT, NATS, WebSocket, and the in-memory test transport), and the AsyncAPI generators emit `*WithDeliveryContext` handler and consumer variants for receive operations (responders excluded, since a responder's reply path never surfaces the context). A message with a typed headers schema keeps its typed headers parameter in the context variant. The context is valid only for the duration of the handler invocation; transports may recycle the buffers it references once the handler returns. The internal delivery plumbing stores each subscription's callback in the new public SPI type `Corvus.Text.Json.AsyncApi.Internal.MessageHandler{TPayload}` without a per-delivery adapter delegate, and NATS boxes its native message struct only when a context handler will consume it. This feature was designed and contributed by [Levy Barbosa (@Levyks)](https://github.com/Levyks), continuing the AsyncAPI channel-parameter and bindings contributions credited in the V5.3.x notes. Thank you, Levy! Contributed in [#930](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/930), merged via [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **Capability-matched transport instrumentation** — `InstrumentedMessageTransport.Create` returns a wrapper that implements `IMessageDeliveryContextTransport` and/or `IHealthCheckableTransport` exactly when the wrapped transport does, so a capability probe against the wrapper answers for the wrapped transport: delivery-context consumers fail at probe time rather than at subscribe time, and broker health checks keep working when instrumentation is enabled. The constructor keeps its plain-wrapper behavior. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).

### Bug fixes

- **Subscription teardown is deadlock-free, fault-tolerant, and leak-free across every transport** — A handler stopping its own subscription (which the Abort error-policy path does) could deadlock a transport by waiting for the consume loop the handler was running on; teardown now detects the self case and completes the broker-side stop as the handler returns. A consume loop that died on an unexpected fault stranded a registry entry that refused resubscription forever; loops now release their channel on exit and record the fault on the new `corvus.asyncapi.subscription_faults` counter, and Kafka commit and NATS JetStream acknowledge failures consult the error policy or are recorded on the new `corvus.asyncapi.acknowledge_failures` counter instead of silently killing the loop. The internal reply-channel consumers that serve `RequestAsync` are transport-scoped rather than tied to the first requester's cancellation token, so a cancelled request no longer disables request-reply on its channel, and they do not occupy the channel's application-visible subscription slot. Subscribe paths that fail before claiming the channel release everything they created (consumers, channels, processors, linked cancellation sources) instead of leaking it, and teardown failures are recorded on `corvus.asyncapi.subscription_teardown_failures` rather than thrown through a dispose that is walking every subscription. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **The generated request path authenticates and releases what it rents** — The generated `SendAndReceive*` requesters never invoked the operation's authentication provider, so a request on an operation with security schemes went out unauthenticated while the corresponding publish authenticated correctly; the request path now mirrors the publish path. A reply address derived from a runtime expression was encoded into a pooled buffer that was never returned to the pool; it now travels into the request core and is returned alongside the channel buffer. And on both the publish and request paths, a failure before the core call (payload building, validation, channel encoding) now disposes the request-owning workspace and returns any rented channel buffer instead of leaking them. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **Security schemes reach AsyncAPI 2.6 emissions and keep their exact variant** — The 2.6 generator delegates its emission to the 3.0 generator but passed an empty security-scheme list, so a 2.6 document whose servers declare security produced producers and consumers that never authenticated. The 2.6 path now collects server security, honoring a channel's `servers` restriction, exactly as the 3.0 path does. Separately, the scheme-type mapping collapsed every SASL variant to `Plain` and the `httpApiKey` scheme to `Http`, so an authentication provider routing on `SecuritySchemeType` (such as `CompositeAuthenticationProvider`) silently failed to match the scheme the document actually declares; `plain`, `scramSha256`, `scramSha512`, `gssapi`, and `httpApiKey` now map to their own members. A 2.6 security requirement naming a scheme that `components.securitySchemes` does not define is reported as a diagnostic rather than silently mapped. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **A dynamic-address consumer dead-letters to the channel it subscribed** — A consumer whose channel address is supplied by the caller at start baked its dead-letter address at generation time from the channel *key*, so every dead-lettered message went to the same wrong constant regardless of the channel actually subscribed. The dead-letter address is now composed at start time from the subscribed channel behind the `dead-letter.` prefix and carried by the subscription, exactly as parameterized consumers already did. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **An unusable reply-address expression degrades instead of breaking the build** — A request/reply operation whose reply declares an `address.location` that is not a supported runtime expression, or that reads the message headers when the request message declares no headers schema, generated code referencing values that do not exist, which failed to compile. Both shapes now fall back to the reply channel's declared address and are reported as generation diagnostics. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **A malformed channel-address template degrades to literal text instead of breaking generation** — A template with an unclosed `{` crashed the producer generator outright, and a `{name}` referring to a parameter the specification does not declare generated code referencing a method parameter that does not exist, which failed to compile. Both are now treated as literal text in the composed address and reported as generation diagnostics, on the producer and consumer paths alike, which now share one template splitter. A declared parameter that never appears in the template no longer inflates the composed address with bytes nothing writes. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **Correlation matching no longer eats loopback requests, and disposal releases parked requesters** — MQTT and WebSocket dispatch replies by correlation match in their global receive paths, so a client subscribed to the very topic it requests on (a delivery MQTT brokers make back to the sender) had its own request consumed as its reply: the requester returned the request payload as the "reply" and the data handler never ran. A message that advertises a response topic (MQTT) or names a reply channel (WebSocket) while arriving on a channel this client data-subscribes is now recognized as a request in flight and dispatched to the channel's handler; on any other channel the correlation match stands, so an MQTT 5 responder that sets a response topic on its reply still completes the pending request. And on the four transports that park requesters awaiting correlated replies (Kafka, AMQP, MQTT, WebSocket), disposal now fails each parked wait with `ObjectDisposedException` instead of stranding the awaiting requester forever. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **Policy decisions and dead-letter failures are visible in telemetry wherever they happen** — A generated consumer applies its error policy above the transport, so its Skip and Abort decisions reached no counter (dead-letters were already counted through the transport call); the generated arms now record them, under the `generated` messaging-system tag, on the same counters transport-level policy decisions use. The instrumented wrapper's dead-letter span closed green with no `corvus.asyncapi.dead_letter_failures` increment when the inner dead-letter itself failed — the dropped-message signal that counter exists for; the failure now marks the span and fires the counter before surfacing. Failed sends and requests also carry `messaging.operation.name`, so they no longer vanish from operation-name groupings. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **A responder's reply survives the teardown that follows it** — `ReceiveOneAndReplyAsync` signals completion the instant the handler returns, and its caller then unsubscribes — which cancelled the subscription's token while the computed reply was still being published on it, on Azure Service Bus, Kafka, and NATS. The requester then waited out its full timeout for a reply that was computed but never sent, and the race hid behind scheduling luck: the same round trip passed on a quiet thread pool and failed on a busy one. Reply publishes now run to completion on the transport-scoped connection regardless of the subscription's teardown, which is how the AMQP transport already handled it (and documented it). One consequence to know about: a teardown that arrives while a reply is in flight against an unreachable broker now waits out that transport's own delivery timeout (for example librdkafka's `message.timeout.ms`) instead of abandoning the reply promptly. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).
- **The in-memory test transport delivers requests to data subscriptions** — On a real broker a request is an ordinary message on its channel, but the testing transport's `RequestAsync` bypassed data subscriptions entirely, parking the request invisibly. A plain or delivery-context subscription on the request channel now receives the request through the same dispatch a publish uses, with the reply still coming from `CompleteRequest`, so tests observe the same traffic a broker would deliver. A request whose wait is abandoned (cancelled, or faulted by the subscriber) also unparks itself, so a later `CompleteRequest` for its correlation ID fails loudly instead of completing a wait nothing is awaiting. And a loopback publish (a publish delivered to a subscription on the same transport) no longer throws the handler's failure at the publisher — a broker never would — recording it in `DeliveryFailures` for assertions instead. See [#931](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/931).

## V5.3.2

V5.3.2 makes the AsyncAPI generators resolve `$ref` at every referenceable position instead of silently dropping the referenced object, and gives generation a diagnostics channel so nothing the generator skips is ever silent again.

### Bug fixes

- **The AsyncAPI generators resolve `$ref` everywhere the specification allows one** — AsyncAPI 3.0 made nearly every object referenceable, but the generator resolved references at only a few positions and silently discarded the rest. A referenced channel produced a consumer subscribed to the channel *key* rather than its real address; referenced operations and servers vanished from the output; bindings behind references never reached the transport; a chained security scheme reported its type as `unknown`; and a 2.6 parameter expressed as a reference kept its argument name but lost its description, enum, and default. Every site now resolves through the shared reference-resolution chain before matching, with regression coverage for each position including compilation of the generated output. The channel-parameter instance of this class was fixed first by [Levy Barbosa (@Levyks)](https://github.com/Levyks) in [#923](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/923), with thanks; that fix is included here. See [#924](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/924).

### New features

- **AsyncAPI generation diagnostics and `asyncapi-generate --strict`** — Generation stays deliberately lenient, but anything the generator skips or degrades (for example a `$ref` that does not resolve) is now recorded as a diagnostic carrying its specification location, exposed as a `Diagnostics` property on both generators and via optional parameters on the public inspection helpers. The CLI prints each diagnostic as a warning after generation, and the new `--strict` option fails the run when any were produced. See [#924](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/924).

## V5.3.1

V5.3.1 closes out the doc-comment emission defects found while reviewing [#916](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/916): the last place specification text reached a generated doc comment unescaped, and the XML doc comments in generated code that had drifted out of step with the signatures they document.

### Bug fixes

- **A multi-line OpenAPI link description no longer breaks the generated build** — The `description` of a response `links` entry was written into the generated link-traversal method's `<summary>` verbatim, in the 3.0, 3.1, and 3.2 client generators. A line break in that description terminated the doc comment and the remainder parsed as source code, the same failure mode #916 reported for operation summaries; a `<` or `&` produced malformed XML docs. The value now goes through the same `EscapeXml` flattening every other doc-comment site has used since #915. See [#917](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/917).
- **Generated code compiles clean under `GenerateDocumentationFile` with warnings as errors** — Several generated doc comments contradicted the signatures they document, so any consuming project that turns on XML documentation output failed its build with CS1572/CS1573. In the model templates, the `(IJsonDocument parent, int idx)` constructors were documented with a single `value` tag, the conversion operators and `From<T>` documented `value` while the parameter is named `instance`, and `TryParseValue` documented an `element` parameter while emitting `result`. In the OpenAPI client generators, the generated operation methods documented every parameter except the trailing `validationMode` and `responseValidationMode` pair, in all four spec versions. All of those now match, and a regression test compiles generated `jsonschema` and `openapi-client` output with documentation diagnostics enabled to keep them matching. The V4 engine was verified against the same gate and does not have the defect (its doc tags match its signatures, and its description emission was already per-line and HTML-encoded), so the suite now guards both engines. Generated public members that carry no doc comment at all (CS1591) are a separate surface, tracked in [#919](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/919). See [#918](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/918).

## V5.3.0

V5.3.0 brings the OpenAPI and AsyncAPI generation work from the workflow-engine campaign back to the mainline. Generated clients gain a closure-free request-body overload, generated servers describe themselves from the specification, optional request bodies are finally optional, and the AsyncAPI transport surface gains a request/reply responder. Three changes are breaking, which is why this is a minor rather than a patch release. It also carries two community contributions from Levy Barbosa, covering AsyncAPI channel parameters and channel/operation bindings.

### Breaking changes

- **`IMessageTransport.RequestAsync` takes a `JsonWorkspace`** — The request/reply call now receives the workspace that owns the reply's lifetime, as a required parameter ahead of the optional `headers` and `cancellationToken`. Previously the reply was materialised against an ambient lifetime the caller could not control, which is the wrong shape for a caller that wants the reply to live exactly as long as the document it is being folded into. Every call site needs the workspace threading through it, and any custom `IMessageTransport` needs the new signature on its implementation of the abstract overload. The convenience overload that takes channel strings forwards to it unchanged in every other respect.
- **A generated AsyncAPI consumer for a parameterised channel now requires its parameters** — A channel whose address declares parameters (`orders.{orderId}.created`) generated a consumer that subscribed to the address *literally*, placeholder and all, so it listened on a channel no publisher ever wrote to. `StartAsync()` therefore took no arguments and looked like it worked. It now takes each declared parameter, so every call site for such a consumer fails to compile until it supplies them, and the subscription moves to the address those parameters compose. Both are intended: the call that compiled before was subscribing to nothing real. Composition allocates only the arrays the subscription retains, because the template is split at generation time so its literal parts are `u8` literals, only the parameter values are transcoded, and the address is filled once; the dead-letter address is built from those bytes rather than by concatenating a second string. A `ReadOnlySpan<char>` overload sits beneath the `string` one, so a caller holding a span never creates a string just to have it measured and copied. This reaches AsyncAPI 2.6 as well as 3.0, because the 2.6 generator delegates its emission to the 3.0 one. Contributed by [Levy Barbosa (@Levyks)](https://github.com/Levyks) in [#914](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/914), with thanks; the allocation-free composition was added on merge.
- **A generated binary response carries its body through the result factory** — An operation whose response is binary generated a parameterless `Ok()`, which could not express the body at all. The shipped example recipe said as much in a comment, returning `Ok()` and noting that the streaming was somebody else's problem. It now generates `Ok(ReadOnlyMemory<byte> body, string? contentType)` and `Ok(Func<Stream, CancellationToken, ValueTask> writeBody, string? contentType)`, so the handler supplies the bytes or a writer and chooses the content type rather than accepting whichever one the specification listed first. Handlers returning a binary response fail to compile until they pass a body, which is the point. Regenerate to pick it up.

### New features

- **Generated AsyncAPI consumers carry channel and operation bindings to the transport** — A consumer whose channel or operation declares bindings is now subscribed with them, as a `MessageContext`, so protocol-specific metadata reaches the transport instead of stopping at the generator. It applies to responders too, via a new `MessageContext` overload of `SubscribeReplyAsync` whose default implementation drops the context and forwards, so no existing transport changes. Contributed by [Levy Barbosa (@Levyks)](https://github.com/Levyks) in [#913](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/913), with thanks.
- **Generated clients accept a context-threaded request body** — A server result factory has long offered `Ok<TContext>(Source<TContext>, workspace)`, so a caller can assemble a response body lazily with its context threaded through and materialise it in one pass with no per-item closure. A client had no counterpart, so anyone with a collection to put in a *request* body had to close over it. The machinery was already present: the generators take the set of body pointers whose type is an object or array, and emit the generic overload only for those. The server command computed that set and the client command never did, so the client path silently opted out under what its own doc comment called "the conservative default". Generated clients now emit `OperationAsync<TContext>(Model.Source<TContext> body, ...)` alongside the plain overload. OpenAPI 2.0 was worse and is worth naming separately: the parameter did not exist there at all, so a 2.0 *server* was also missing the closure-free response factories every 3.x server has had.
- **AsyncAPI gains a request/reply responder** — `IMessageTransport.SubscribeReplyAsync` subscribes to a channel, hands each request to a handler, and publishes the handler's reply on the correlated reply channel. It ships with a default implementation that throws `NotSupportedException`, so a transport that does not support responders is unaffected and existing custom transports continue to compile.
- **Dynamic channel addresses take spans and memory, not only strings** — The generated methods for a channel whose whole address is supplied by the caller now offer `string`, `ReadOnlySpan<char>`, and `ReadOnlyMemory<byte>` overloads, so an address composed from UTF-8 bytes no longer has to become a string on the way to the transport.
- **Generated code carries the documentation the specification declares** — Operation, parameter, and model descriptions from the source document are emitted as XML doc comments on the generated members, XML-escaped so a description containing markup does not break the build.
- **An optional request body is optional in generated clients and servers** — A request body not marked `required` generated a mandatory parameter, so a caller had to supply something for a body the specification says may be absent. Clients now omit the body when it is not supplied, and servers treat it as absent rather than empty.
## V5.2.13

V5.2.13 fixes two defects in the schema validation error messages and makes the source generator work for structs declared in the global namespace.

### Bug fixes

- **Integer-valued validation messages restore the space before the quoted value** — Every message family whose expected value is an integer (`minLength`/`maxLength`, `minItems`/`maxItems`, `minContains`/`maxContains`, `minProperties`/`maxProperties`, across all six comparison variants) emitted text like `Expected the item count to be greater than or equal to'3'`. The messages are composed from a resource string that deliberately ends without trailing whitespace plus an appender that writes the quoted expected value; the string-valued appender wrote the separator space but the integer-valued appender did not. The integer appender now writes the same leading space, and exact-format tests pin all 24 affected message providers. See [#910](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/910).
- **`exclusiveMaximum` failures report the correct comparison direction** — The `JsonSchema_ExpectedLessThan` resource read "The value was expected to be greater than", so a failed less-than comparison reported the opposite direction. Both copies of the resource (the main library and the JsonLogic source generator) now read "less than". See [#910](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/910).
- **The source generator emits types for structs declared in the global namespace** — A `[JsonSchemaTypeGenerator]` struct declared outside any namespace generated nothing, silently: the generator's syntactic pre-filter required the struct's parent to be a namespace declaration, so a global-namespace target never entered the pipeline and no diagnostic was reported. The filter now accepts a compilation-unit parent, the target namespace maps to the empty string (as the query-language source generators already do), and the C# emission omits the namespace declaration and the leading dot in fully qualified names when the namespace is empty. The default namespace used for shared generated types prefers the first non-empty target namespace, so a global-namespace target cannot capture it. Generated output for namespaced targets is byte-for-byte unchanged. See [#906](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/906).

## V5.2.12

V5.2.12 adds `MatchEvery()` to generated `anyOf` types in both the V4 and V5 engines: an accumulator-threading counterpart to `Match()` that visits every matching subschema instead of only the first.

### New features

- **Generated `anyOf` composition types now emit `MatchEvery()` (V4 and V5 engines)** — `Match()` calls the match function for the first subschema that matches, in declaration order. That is the correct `anyOf` validation semantic, but for an `anyOf` whose arms overlap (a bare `{ "type": "string" }` arm ahead of a pattern-constrained string arm) the more specific arm is unreachable, so value-based dispatch on it is impossible. Each `anyOf` composition now also generates `MatchEvery<TAccumulator>(TAccumulator accumulator, ...)`: every subschema is evaluated, each matching arm's function is called in declaration order, receiving the current accumulator and returning the next, and the final accumulator is returned. `defaultMatch` is called only when no subschema matched, receiving the seed unchanged. A later, more specific arm therefore takes precedence naturally, and the accumulator can collect results across arms, or carry state that `Match` would need a closure or a context parameter for. The arm delegates reuse the existing `Matcher<TMatch, TContext, TResult>` delegate as `Matcher<TArm, TAccumulator, TAccumulator>`, so there are no runtime-library changes and code generated by the updated engines compiles against existing runtime packages. The V5 engine emits the method on both the immutable type and its `.Mutable` partial; the V4 engine mirrors the same shape with its `As<T>()`/`IsValid()` idiom. `oneOf` is excluded because exactly one arm can match a valid value, and `allOf` because every arm matches a valid value. The CTJ003 analyzer covers the new method: its make-static code fix applies to `MatchEvery` lambdas, and its advice for a capturing lambda is to thread the captured state through the accumulator, since `MatchEvery` deliberately has no context overload. This is a code-generation feature; regenerate models to pick it up. See [#905](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/905).

## V5.2.11

V5.2.11 is a format-conformance release: the email formats gain RFC 5321 address-literal support, `idn-hostname` gains the RFC 5893 Bidi rules and several other IDNA2008 checks, and the JSON-Schema-Test-Suite submodule is updated to its latest revision with every new test passing.

### Bug fixes

- **`email` and `idn-email` now validate RFC 5321 address-literals** — `idn-email` had no address-literal handling at all: bracketed IPv4 domains (`δοκιμή@[192.0.2.1]`) were rejected, while bracketed content that is not an address at all (`user@[]`, `user@[IPv6:zzz]`) was accepted because the IDN hostname path does not reject `[`, `:` or `]`. RFC 6531 extends only the local part of a mailbox, so `idn-email` must accept exactly the literals `email` does. Both formats now share an address-literal validator: `[IPv6:...]` contents go through the strict IPv6 parser, and other bracketed contents must be an RFC 5321 `IPv4-address-literal` — exactly four 1–3 digit decimal octets, each 0–255, with leading zeros permitted. That last point also fixes `email`, which previously validated IPv4 literals against the RFC 3986 `dec-octet` grammar and so rejected `user@[01.0.0.1]`. The V4 engine (`Corvus.Json.ExtendedTypes`) is fixed in parallel: `TypeIdnEmail` rejected every address-literal (the IdnMapping domain normalization throws on brackets under STD3 rules), `EmailPattern`'s octet subpattern was `dec-octet` based, and the `[GeneratedRegex]` copy of `EmailPattern` diverged from the net481 fallback by rejecting TLDs ending in an uppercase letter (`user@EXAMPLE.COM`) on net8.0+ only. See [#904](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/904).
- **`idn-hostname` no longer accepts ASCII symbols in decoded labels** — The decoded-hostname validator only rejected characters on its disallowed-IDN list, so ASCII symbols such as `[`, `:`, `_` and `!` fell through the Unicode contextual rules and were accepted; `"[]"` validated as an `idn-hostname`, and the punycode path of the plain `hostname` format leaked symbols in labels after the `xn--` label (`xn--4gbwdl.foo_bar` validated even though `foo_bar` alone never did). Any ASCII character that is not a letter, digit, `.` or `-` is now rejected in decoded labels. See [#904](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/904).
- **`idn-hostname` conformance: RFC 5893 Bidi rules, the ZWNJ contextual rule, label limits, and disallowed decoded code points** — Updating the JSON-Schema-Test-Suite surfaced five gaps, now closed: the RFC 5893 Bidi rules are implemented using the runtime-derived strong-bidi data (label direction from the first character, no RTL characters in LTR labels and vice versa, no mixing of European and Arabic-Indic digits, enforced only in Bidi domain names); the ZWNJ contextual rule (RFC 5892 Appendix A.1) is enforced at every occurrence — previously ZWNJ was not checked at all; a U-label whose A-label form exceeds the 63-octet limit is rejected via an allocation-free RFC 3492 Punycode length counter; the 253-character total-length limit was off by one; and non-letter, non-digit, non-mark code points are rejected in U-labels except the RFC 5892 §2.6 PVALID exceptions, which catches A-labels that decode to disallowed code points such as `xn--7a` (U+00A1). See [#907](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/907).
- **`idn-email` accepts RFC 6531 quoted local parts containing non-ASCII** — `"δοκιμή"@example.com` was rejected: the Unicode local-part matcher was missing the quoted-string branch its ASCII counterpart already had. See [#907](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/907).
- **`uuid` rejects trailing content after a complete UUID** — `2eb8aa08-aa98-11ea-b4aa-73b441d16380-` validated because the matcher discarded the parser's consumed-byte count; the parse must now consume the entire value. See [#907](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/907).
- **Annotation schema locations are URI-fragment encoded** — The annotation producer wrote absolute keyword locations raw (`#/patternProperties/^a`); RFC 3986 requires characters outside the fragment grammar to be percent-encoded (`#/patternProperties/%5Ea`), which is the form the JSON-Schema-Test-Suite annotation expectations have used since April 2025. The committed generated annotation test classes were stale and masked the mismatch until regeneration. See [#907](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/907).
- **JSON-Schema-Test-Suite updated to `be54236`** — 24 upstream commits adding format tests across draft 7, 2019-09, and 2020-12 (idn-hostname Bidi and Punycode edge cases, idn-email non-ASCII local parts and address-literals, ipv4 inet_aton shorthands and non-ASCII digits, uuid suffix and prefix forms, date boundary tightening). All regenerated test classes pass; the full solution runs 94,049 tests clean. See [#907](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/907).

## V5.2.10

V5.2.10 adds native OpenAPI 2.0 (Swagger) support: `swagger: "2.0"` documents now generate strongly-typed HTTP clients and ASP.NET Core server stubs through a dedicated per-version generator and JSON Schema dialect, exactly as 3.0, 3.1, and 3.2 documents do.

### New features

- **Native OpenAPI 2.0 (Swagger) client and server generation** — Many real-world APIs (the archived Slack Web API among them) still publish Swagger 2.0 documents. Previously the CLI sniffed only the `openapi` field, so a `swagger: "2.0"` document silently mis-dispatched to the 3.1 generator. The version is now detected from either field, and 2.0 documents flow through a new `OpenApi20CodeGenerator` and a first-class OpenAPI 2.0 JSON Schema dialect (the draft-04 keyword set extended with the 2.0 fixed fields, `x-nullable`, boolean-`required` handling on Parameter Objects, and tuple-capable `items`). The 2.0 shapes are lowered onto the same generated-client architecture as 3.x. An `in: body` parameter becomes the typed request body via the operation's effective `consumes`; `in: formData` parameters are aggregated into a synthesized `<Operation>FormBody` model serialized as `application/x-www-form-urlencoded` or `multipart/form-data`; non-body Parameter Objects are read directly as schemas (2.0 puts `type`, `maximum`, `enum` and friends on the parameter itself); `collectionFormat` maps onto the serialization styles, including native tab-delimited `tsv`; `host` plus `basePath` plus `schemes` become the server URI; the response `schema` crossed with the effective `produces` yields typed response bodies; `type: file` responses surface as raw streams; and `securityDefinitions` flow into the request security requirements. Three new packages ship the surface: `Corvus.Text.Json.OpenApi20` (the typed 2.0 document model) and the dialect pair `Corvus.Json.CodeGeneration.OpenApi20` and `Corvus.Json.JsonSchema.OpenApi20`. The CLI auto-detects the version (`--specVersion 2.0` forces it, with a warning on a mismatch), and the callback commands fail cleanly since 2.0 defines no callbacks or webhooks. The feature is covered by a 653-case openApi20 conformance corpus, client and server wire-level runtime suites, a purpose-built covspec fixture of hard corners, and a real-world acceptance test generating the full archived Slack Web API spec (174 operations). The OpenAPI playground accepts Swagger documents with a new Petstore (Swagger 2.0) sample, and example recipe `042-OpenApi20Client` walks the 2.0-specific shapes end to end. See [#899](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/899).
- **`ParameterStyle.TabDelimited`** — `collectionFormat: tsv` is a first-class serialization style. Clients join array values with tabs (`%09` on the wire) and generated servers split them, on query and form-data parameters. `ssv`, `tsv`, and `pipes` on path and header parameters have no 3.x-shaped wire form and degrade to comma-separated values on both sides, with a `#warning` in the generated code naming the operation.
- **Encodings-aware form-urlencoded deserialization** — `FormUrlEncodedSerializer` gains `Deserialize` and `DeserializeAsync` overloads that accept a per-property encodings map and split delimited single-occurrence fields (comma, space, pipe, or tab) into typed arrays. Generated 2.0 servers pass a synthesized encodings map for their formData array fields. The overloads are additive, so existing callers are unaffected.

### Bug fixes

- **`MultipartFormReader` now parses unquoted `Content-Disposition` names** — The server-side multipart reader only recognized the quoted form (`name="notes"`). A part whose name used the token form permitted by the HTTP parameter grammar (`name=notes`) was read with an empty name, so its value surfaced under the wrong key. Both forms are parsed now, with a delimiter guard so a `filename=` parameter can no longer satisfy a `name=` search. This is a runtime fix in `Corvus.Text.Json.OpenApi`; no regeneration is required.

## V5.2.9

V5.2.9 adds end positions to YAML events: every `YamlEvent` now reports the full source span of its content.

### New features

- **`YamlEvent` now exposes `EndLine` and `EndColumn` properties** — A `YamlEvent` surfaced by `YamlDocument.EnumerateEvents` previously reported only its start position (`Line`/`Column`), so a consumer that needed the extent of an event's source text — an editor highlight, a linter diagnostic, or mapping a validation failure back to the YAML source — had to re-scan the text to find where the event ended. Every event now also carries `EndLine` and `EndColumn`: the 1-based position one past the last character of the event's content, so the half-open `(Line, Column)`–`(EndLine, EndColumn)` range slices the event's raw source text exactly. A scalar's span covers its complete source form — the bare word of a plain scalar, a quoted scalar including its quotes, a block scalar from its `|`/`>` header through its last content character. Purely structural events (stream and document boundaries, mapping and sequence starts and ends) report a zero-width span (start equals end): a start event sits where its construct begins and an end event just past where it ends. The new `YamlEventSpanTests` suite pins the spans for every event type across plain, quoted, and literal block scalars, flow and block collections, and empty nodes. This is a purely additive runtime change in `Corvus.Text.Json.Yaml`; no regeneration is required. Contributed by [@OpenByteDev](https://github.com/OpenByteDev). See [#897](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/897).

## V5.2.8

V5.2.8 fixes a code-generation defect in which multiple hoisted `allOf` branches were committed to a validation results collector out of LIFO order.

### Bug fixes

- **Hoisted `allOf` branch contexts are now committed in reverse push order** — The generator's `HoistedAllOfPropertyValidationHandler` pushed every hoistable `allOf` branch context up front in forward order but then committed them in that same forward order, violating the results collector's strict LIFO context-sequence invariant. On a schema with two or more hoistable `allOf` branches *and* a results collector attached — the JSON Schema 2020-12 meta-schema, with its seven vocabulary `allOf` branches, is the first common schema to expose it — this terminated the process with a debug assertion in DEBUG builds and silently produced corrupt detailed results in RELEASE. The boolean-only validation path was unaffected, which is why the meta-schema validated correctly wherever no collector was attached. The branch contexts are now committed in exact reverse of the push order in both the parent-hosted and standalone object-loop paths; all other per-branch work is order-independent, so only the commit ordering changes. The fix is compliance-neutral by construction — regenerating the entire JSON Schema Test Suite produces no diff — and is covered by a new `MetaSchemaCollectorTests` regression suite. This is a code-generation fix; regenerate affected models (schemas with multiple hoistable `allOf` branches validated with a collector) to pick it up. See [#838](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/838).

## V5.2.7

V5.2.7 adds `Create()` factory methods to generated types: the `ParsedJsonDocument<T>`-producing analogue of `CreateBuilder()`, for the create-but-don't-modify pattern.

### Breaking changes

- **`Create`, `CreateArray`, and `CreateObject` are now reserved member names in generated types (V5 engine)** — A schema property whose formatted name lands on one of these (for example a JSON property named `"create"`) previously generated a member of that name; it now receives the standard reserved-name suffix, generating `CreateValue` instead — exactly as a property named `"createBuilder"` or `"build"` always has. This only affects models with such property names (none of the shipped models or benchmarks except the UI5 manifest schema's `create` navigation property); regenerating such a model renames the accessor, and consuming code updates from `.Create` to `.CreateValue`. The JSON on the wire is unchanged.

### New features

- **Generated types now emit `Create()` factory methods that return a `ParsedJsonDocument<T>` directly** — Sometimes you need to build a new document and return it to your caller as a `ParsedJsonDocument<T>`. Previously that took a `JsonDocumentBuilder<T>` plus a serialization round trip, or hand-crafting into a buffer with `Utf8JsonWriter` and parsing it back — either way, a "create" effort followed by a "parse" effort. Every generated type now also emits `Create()` overloads mirroring its `CreateBuilder()` overloads — from a `Source`, from a `Builder.Build` delegate (with and without a flowed `TContext`), from per-property values, from positional tuple item sources, from a numeric span for tensor types, and parameterless `Create()`/`CreateArray()`/`CreateObject()` for an empty document — minus the `JsonWorkspace` parameter: no workspace is needed. Construction runs through the same `ComplexValueBuilder` machinery as `CreateBuilder()`, into a new pooled `ParsedJsonDocumentBuilder` document that writes the final UTF-8 document text directly as values are added — values embedded from other documents are captured into the backing at the point they are added, so the result is fully self-contained — while the metadata rows are built once in the usual way and receive their text locations in a single in-place patch at handoff. Nothing is parsed and no content is written twice; the one exception is a build that removes a property mid-construction (for example an `Apply` composition overwriting an existing member), which pays a single compaction pass over the completed document at handoff. The builder and its workspace are rented from thread-local caches, so steady-state construction allocates only the returned document and its pooled buffers. There is no instance-method form (an instance `Create()` would collide with the static parameterless overload); the mirrored surface is otherwise identical in form to `CreateBuilder()`, which also eases upgrading a V4 codebase that used the create-only pattern. The feature lives in the shared V5 code-generation core, so it applies to models emitted by the source generator and the `corvusjson` CLI alike; regenerate your models to pick it up. See [#836](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/836).

## V5.2.6

V5.2.6 restores implicit numeric conversion operators on generated types whose values are always numeric — a constrained integer, or a nullable integer/number — in both the V4 and V5 engines.

### Bug fixes

- **Numeric conversions on constrained and nullable numeric types are implicit again (V4 engine)** — 5.1 made conversions between a *multi-core union* type and .NET numeric types `explicit`, fixing implicit conversions that could throw when the instance held a non-numeric branch. The union test counted `ImpliedCoreTypes()` flags, but that set unions keyword implications: `Number` and `Integer` are distinct flags and a numeric constraint keyword (`minimum`, `maximum`, …) implies `Number` alongside the `Integer` implied by `"type": "integer"`, so an ordinary constrained integer — `{ "type": "integer", "format": "int32", "minimum": 0 }` — counted two flags and was treated as a union; `["integer", "null"]` counted `Integer|Null` the same way. Both regressed from the 4.x output, where the conversion to and from the preferred .NET numeric type (`int` for `format: int32`; `long` for an unformatted integer) is `implicit` — breaking consumers with `CS0029` on upgrade. The generator now classifies a type as a union for conversion purposes only when it can hold a **non-numeric** value kind: `Number`/`Integer` are one numeric domain, and `null` does not demote the conversions (matching 4.x). A genuine mixed union such as `["integer", "string"]` keeps the explicit conversions introduced in 5.1. Regenerate affected models to pick this up; output for such models is then identical to 4.3.x. See [#834](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/834).
- **The V5 engine aligns to the same rule** — the identical flag-counting sent a constrained integer or nullable numeric type down the multi-type-union path, so the `long` and `double` conversions that a bare `"type": "integer"` emits implicitly were demoted to `explicit` (a format-driven conversion such as `int` for `format: int32` was unaffected). Types whose implied kinds are all within the numeric domain (`number`/`integer`/`null`) now emit the same conversions as a single-core numeric type: `implicit` `long` and `double`, `explicit` for the allocating `BigNumber`/`BigInteger` and `decimal` conversions. Mixed-kind unions are unchanged. This widens the conversions available on affected V5 models (an explicit cast that compiled before still compiles), so no consuming-code changes are needed; regenerate models to pick it up. The benchmark `C/` model directories are resynced to the current generator output in the same change.

## V5.2.5

V5.2.5 fixes the V4 engine so that a schema typed as a nullable boolean — `"type": ["boolean", "null"]` — again generates code that compiles on .NET 8 and later.

### Bug fixes

- **A `["boolean", "null"]` (nullable boolean) schema generated with the V4 engine now compiles on .NET 8+** — The generated type carries `[JsonConverter(typeof(JsonValueConverter<T>))]` and explicit `IJsonValue`/`IJsonValue<T>` member implementations, all of which require the type to implement `IJsonValue<T>`. A single-type value obtains `IJsonValue<T>` transitively from its type-family interface (`IJsonBoolean<T>`, `IJsonString<T>`, and so on), and a union obtains it from whichever of those interfaces its member types contribute. On .NET 8+, `IJsonBoolean<T>` declares a `static abstract implicit operator bool(T)`, which a union cannot satisfy because a union converts to `bool` *explicitly* (the value may not be a boolean); the boolean interface is therefore declared only on pre-.NET 8 frameworks for a multi-type union. For a `["boolean", "null"]` union the boolean is the **only** member contributing a type-family interface (`null` contributes none), so on .NET 8+ nothing supplied `IJsonValue<T>` and the generated code failed to compile with `CS0315` (the `JsonValueConverter<T>` constraint) and `CS0540` (the explicit `IJsonValue` implementations). The generator now declares `IJsonValue<T>` directly on the core partial for such a union on .NET 8+, so the type implements it on every target framework. Other unions containing a boolean (for example `["boolean", "string"]`) were unaffected — they already obtained `IJsonValue<T>` from their other member's interface — and their output is unchanged. This is a V4 code-generation fix; regenerate any affected models to pick it up. See [#832](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/832).

## V5.2.4

V5.2.4 fixes the `HttpClient`-backed OpenAPI transport so that a base URL carrying a path prefix — for example an API-gateway route — is preserved in every request URI.

### Bug fixes

- **`HttpClientTransport` now preserves a base URL's path prefix** — Generated operation paths always begin with `/`, and the transport previously sent them as relative URIs for `HttpClient` to resolve against `HttpClient.BaseAddress`. Under RFC 3986 §5.3, an absolute-path reference such as `/transactions` **replaces** the base URI's entire path, so any deployment whose base URL carries a path prefix — the Azure API Management pattern, `https://apim.example/inventory/` — silently lost the prefix: requests landed on `https://apim.example/transactions` instead of `https://apim.example/inventory/transactions`, and because generated paths always start with `/` there was no `BaseAddress` spelling that avoided it. The transport now composes the final absolute URI itself — the base address up to and including its path, with any trailing `/` trimmed, followed by the resolved operation path and query — so the prefix is preserved; this is the same composition NSwag- and Kiota-generated clients use. For a base address with no path segment the composed URI is byte-identical to the previous resolution, and a client with no `BaseAddress` at all still fails with `HttpClient`'s usual invalid-request-URI error, so no other behavior changes. This is a runtime fix in `Corvus.Text.Json.OpenApi.HttpTransport`; no regeneration of generated clients is required.

## V5.2.3

V5.2.3 contains no library changes: a documentation-only release that modernised the `Build`/`CreateBuilder` callback samples to the compact field-set form across the docs and example recipes.

## V5.2.2

V5.2.2 lets the property-parameter `Build(...)` factory be used directly as an array element (or object-property value) inside a mutable builder callback.

### Bug fixes

- **The property-parameter `Build(...)` factory can now be passed straight to a builder's `AddItem`/`InsertItem`/`SetProperty`/`Set…` methods** — Generated mutable models expose a `T.Build(field: value, …)` factory that captures the `Create(...)` arguments into a lazy `T.Source`. Previously that `Source` could only be handed to a *direct* consumer (a `CreateBuilder(...)` call or a generated client/result factory); using it as an **array element** inside an array-builder callback — `b.AddItem(Item.Build(id: …, name: …))` — failed to compile with `CS8347`/`CS8350`/`CS8156`. The C# ref-safety analysis assumed the `Build` result (a `ref struct` constructed from `in` parameters) might escape into the wider-scoped builder, even though `AddItem`/`InsertItem` materialize it synchronously and never retain it. The generated property-parameter `Build(...)` factory parameters, the capturing `Source(...)` constructor parameters, and the array `AddItem`/`InsertItem` and object `Set…`/`SetProperty` consumers are now emitted as `scoped in`. This is both truthful (each copies or materializes its argument synchronously) and a **backward-compatible** relaxation — callers may pass narrower-scoped values, and nothing that compiled before stops compiling — so the compact factory form now works uniformly when building arrays of objects. The fix is in the shared V5 code-generation core, so it applies to models emitted by the source generator, the `corvusjson` CLI, and the OpenAPI/AsyncAPI generators alike; regenerate your models to pick it up. The delegate-form `Build(static (ref Builder b) => …)` was unaffected (it wraps a heap closure, not parameter refs).

## V5.2.1

V5.2.1 fixes several validation defects — a runtime crash in evaluation tracking, a property-dispatch hash collision, detailed-results faults and corruption on complex schemas, an `unevaluatedProperties` over-rejection through `dependentSchemas`, and a struct-layout cycle in generated recursive discriminated unions — and lets you disable `format` assertion globally to produce annotation-only output.

### Bug fixes

- **Validating an object or array with 232–255 evaluation-tracked properties or items no longer throws `ArgumentOutOfRangeException`** — The internal evaluation-tracking context records "evaluated" property/item bits in an inline 8-`int` (256-bit) buffer and flagged whether those bits had spilled to a larger rented buffer using a bit of that buffer's last `int`. The flag was `0b1000_0000` — bit 7 of the last int, i.e. the data bit for **index 231** — so marking property or item 231 as evaluated (which happens for any object or array with 232 or more tracked entries) corrupted the flag, and the buffer accessor then mis-read it as the rented layout and threw. The flag now uses the most-significant bit (index 255), which `MaxComplexValueCount` already keeps out of inline data. This is a runtime fix in `Corvus.Text.Json` shared by every generated validator, the standalone evaluator, and the dynamic validator; no regeneration is required.

- **Recursive discriminated unions no longer produce a `CS0523` ("cycle in the struct layout") compile error** — The discriminated-union wiring projects each constituent's own `Source` builder through the union's `Source` by value. For a union with a constituent that (transitively) embeds the union's own `Source`, that projection closed a value-type containment cycle, which the C# compiler rejects. The generator's cycle analysis now models the union→constituent projection edge and suppresses the by-value projection for a cyclic constituent (which keeps its builder/`JsonElement` path), so the generated types compile. Regenerate generated code to pick up the fix.

- **Property dispatch no longer mis-resolves a property whose name is a prefix of, and hash-collides with, another declared property** — The internal property-name hash (used to look up the matcher/value for a property in objects with many declared members) packs a key's first seven bytes verbatim and folds its length plus two more bytes into the top byte. For keys of eight or more bytes that top byte could come out as `0` — the value reserved as the marker for a short, fully-encoded key — so a long key whose `(length + key[7] + last byte)` is a multiple of 256 collided with a short key sharing its first seven bytes (e.g. `NewLine` versus `NewLinesForBracesInMethods`, whose top byte is `(26 + 's' + 's') % 256 = 0`). The short-key lookup fast-path trusts that marker and skips the full key comparison, so it resolved the short key to the colliding sibling's entry — validating the value against the wrong subschema and, in the document property map, returning the wrong property's value. The hash now maps that top byte into `1..255`, keeping the length in the hash while reserving `0` for short keys. The same algorithm was duplicated in five places (property and enum lookups, the document property map, and the JSON Path planner); all are fixed. This is a runtime fix; no regeneration is required.

- **Detailed (verbose) validation results no longer fault on schema locations that contain URI-unsafe characters** — A schema evaluation location — for example the subschema under a `patternProperties` key whose regex is `^x-` — is a JSON Pointer (RFC 6901), in which a reference token may legally contain characters that are unsafe in a URI fragment (such as `^`). The path-copy routine used when assembling detailed results asserted the location was a canonical URI, so `#/patternProperties/^x-` faulted. Schema locations are now kept as raw JSON Pointers throughout — percent-encoding is reserved for the point at which a location is actually rendered as a URI — and the path-copy routine no longer over-validates them. Regenerate generated code to pick up the unencoded locations.

- **Detailed validation results no longer corrupt when validating deeply-branching schemas** — When the results collector discarded a child evaluation context that had produced no results (for example a matching `if` condition, which contributes no output in detailed mode), it rolled its result buffer and committed-result stack back to `(0, 0)` instead of to the position where the context began, clobbering earlier committed results; enumerating the detailed results then read a corrupted length header and threw an out-of-range index. This surfaced validating the OpenAPI 3.1 metaschema, whose nested `oneOf`/`if`/`dependentSchemas` over a recursive `$dynamicRef` discard many such contexts. The collector now rolls back to the start-of-context marker it records when the context begins, whose buffer position and committed-result count are mutually consistent. This is a runtime fix; no regeneration is required.

- **A property evaluated only through `dependentSchemas` is now credited for `unevaluatedProperties` regardless of object property order** — `dependentSchemas` is an applicator over the whole object, so a dependent subschema can evaluate — and so credit for `unevaluatedProperties` — any property, including one positioned before the property that triggers the dependency. The object validator emitted the dependent subschema lazily, inside the trigger property's named-property validator (run at that property's position in the single forward property loop), while `unevaluatedProperties` is checked inline in the same loop, so a property positioned before its trigger failed its `unevaluatedProperties` check before the dependent subschema credited it. The most common victim is an OpenAPI 3.1 parameter's `example` — reachable only through `dependentSchemas.schema`, and placed before `schema` by real specifications — which made the OpenAPI 3.1 metaschema over-reject valid documents. `dependentSchemas` is now evaluated as a whole-object pre-pass before the property loop (detecting each dependency directly), so its crediting always precedes the inline `unevaluatedProperties` checks; the extra pass exists only when `dependentSchemas` are present. Regenerate generated code to pick up the fix.

### New features

- **`format` assertion can be disabled globally to generate annotation-only output** — Corvus.Text.Json asserts `format` by default on every draft. `--assertFormat false` — which previously had no effect, because the CLI option was a value-less flag stuck on its `true` default — now binds a value and disables assertion for drafts where `format` is an annotation by vocabulary (e.g. 2020-12). For drafts whose vocabulary asserts `format` (draft-04/06/07), the new global form of `--formatMode` — a bare `--formatMode disable` (equivalently `--formatMode *=disable`) — disables assertion for **every** format on **every** draft, so `format` is recorded as an annotation but never fails validation. Per-format overrides (e.g. `--formatMode date-time=disable`) continue to take precedence over both the global default and the vocabulary.

## V5.2.0

V5.2.0 makes generated JSON Schema type names deterministic across operating systems, fixing a rare case where the same schema produced a different generated type name on Windows than on Linux or macOS.

### Breaking changes

- **Generated type names no longer depend on the host operating system** — In a **rare** case, the documentation-based type-name heuristic could derive a different name for the same anonymous (inline) subschema depending on which OS the generator ran on. When such a subschema had a `description` and no `title`, the heuristic only named the type from that description when its length was under a 64-character cap — but the length it measured included a trailing line break assembled with `Environment.NewLine`, which is `"\r\n"` on Windows and `"\n"` on Linux and macOS. A `description` whose length landed exactly on that boundary (or one carrying trailing whitespace) therefore passed the cap on Linux/macOS — yielding a name derived from the description — yet failed it on Windows by a single character, where the generator fell back to the next heuristic (typically the required-property name, e.g. `TheIdentifierOfTheAssociatedRequiredDocument` on Linux/macOS versus `RequiredDocumentId` on Windows). The assembled documentation is now joined with a fixed `'\n'` separator and the length is measured after trimming, so the heuristic reaches the same decision on every platform. This affects only the **narrow** set of schemas whose documentation-derived name sat exactly on the boundary; for those, regenerating may change a generated type name (and any hand-written code that referenced it). Because the fix is in the shared code-generation core, it applies to the V4 and V5 engines, the `corvusjson` CLI, and the source generators. See [#825](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/825).

## V5.1.19

V5.1.19 adds a KYAML output mode to the JSON→YAML writer.

### New features

- **JSON→YAML conversion can now emit [KYAML](https://github.com/kubernetes/enhancements/tree/master/keps/sig-cli/5295-kyaml)** — KYAML (Kubernetes KEP-5295) is a strict subset of YAML 1.2 designed to be unambiguous: every mapping uses explicit `{ }` braces and every sequence explicit `[ ]` brackets, laid out across indented lines with a trailing comma after each element; string values (and ambiguous keys) are always double-quoted while numbers, booleans, and `null` are written bare. This eliminates the "Norway problem" and indentation-sensitivity pitfalls, and because KYAML is valid YAML 1.2 the output round-trips through any conforming parser (including this library's reader, which already accepts it with no configuration). Enable it with the new `YamlWriterOptions.Kyaml` preset (or `YamlWriterFormat.Kyaml`) on any `ConvertToYaml`/`ConvertToYamlString` overload or directly on `Utf8YamlWriter`; the default output remains canonical block-style YAML. The feature lives entirely in `Utf8YamlWriter`, so both the `Corvus.Text.Json.Yaml` and `Corvus.Yaml.SystemTextJson` packages gain it. See [#823](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/823).

## V5.1.18

V5.1.18 fixes a numeric validation bug where a zero-valued bound mis-validated `number` values whose magnitude was just below `0.1`.

### Bug fixes

- **A zero-valued numeric bound (`minimum`/`exclusiveMinimum`/`maximum`/`exclusiveMaximum` = `0`) no longer mis-validates `number` values in the open interval `(0, 0.1)`** — The shared numeric comparator `JsonElementHelpers.CompareNormalizedJsonNumbers` orders two normalized numbers by their *effective length* (significand length + exponent), the position of the most significant digit. Zero normalizes to an **empty significand** with exponent `0`, giving it effective length `0` — the same band as values in `[0.1, 1)`. As a result any value whose absolute magnitude was less than `0.1` (effective length `< 0`, e.g. `0.05`, `0.083`) was ordered as if it were *smaller* than zero: `minimum: 0` and `exclusiveMinimum: 0` wrongly **rejected** such values, while `maximum: 0` and `exclusiveMaximum: 0` wrongly **accepted** them. Values of `0`, values `>= 0.1`, negative values (handled by the sign comparison), and the same keywords with non-zero bounds were all unaffected, as were `integer`-typed schemas (only integer magnitudes reach the comparator). The comparator now detects an empty significand and orders zero explicitly — less than every positive value, greater than every negative value — before the effective-length comparison. This is a runtime fix in the shared comparator used by generated models, the standalone evaluator, the dynamic `Validator`, and JsonLogic; no regeneration of generated code is required. See [#819](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/819).

## V5.1.17

V5.1.17 fixes an RFC 7396 JSON Merge Patch bug where merging a nested object corrupted the parent element handle.

### Bug fixes

- **`ApplyMergePatch` no longer leaves the parent element stale when merging a nested object** — When a merge patch recursed into a nested object (`JsonMergePatchExtensions.ApplyMergePatch`), the recursion mutated the document but the parent `JsonElement.Mutable` it was iterating kept its now-stale cached document version. Processing any further property of that same (non-root) parent then failed its staleness check, and a frozen patch document copied into the merge could appear disposed on a subsequent read — surfacing as `InvalidOperationException` ("Operation is not valid due to the current state of the object") during the merge or `ObjectDisposedException: 'JsonDocument'` when the patch was read afterwards. Because a recursive merge only mutates the child's own subtree, the parent element's start index is still valid; the merge now re-mints the parent handle with the current version after each nested merge so subsequent siblings and reads succeed. Simple single-property-per-level merges were unaffected, which is why the existing RFC 7396 suite did not catch it. See [#820](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/820).

### New features

- **`JsonMarshal.RefreshUnsafe<T>(in T)`** — Returns a fresh handle to a mutable JSON element whose cached document version is brought up to date with its parent document's current version, without re-validating the element's position. This is a deliberately **dangerous** marshalling helper: it must only be called when the caller knows the element's start index is still valid — i.e. the document has been mutated only *within that element's own subtree* (descendant nodes). It backs the RFC 7396 merge-patch fix above, where a recursive merge into a child object leaves the (structurally still valid) parent element version-stale. See [#820](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/820).

## V5.1.16

V5.1.16 fixes schema reference resolution for `$ref`s expressed as `file://` URIs.

### Bug fixes

- **Schema references expressed as `file://` URIs now resolve correctly** — A `$ref` (or top-level schema reference) given as an absolute `file://` URI — for example `file:///C:/schemas/foo.json` or `file:///home/me/schemas/foo.json` — failed to resolve during code generation and runtime validation. `SchemaReferenceNormalization.TryNormalizeSchemaReference` passed the raw URI string to `Path.GetFullPath`, which treated the `file://` scheme as part of a relative path and produced a nonsensical location, so the document resolver could not find the schema. The normalizer now converts an absolute `file://` reference to its local filesystem path via `Uri.LocalPath` (handling percent-decoding such as `%20`, and the platform-specific path shape) before resolving it. Relative references and non-`file` URIs (for example `http(s)://`) are unaffected. This shared normalizer is used by the V4 and V5 code generators, the CLI, the source generators, and the dynamic `Validator`. Reported via [#724](https://github.com/corvus-dotnet/Corvus.JsonSchema/pull/724); see [#817](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/817).

## V5.1.15

V5.1.15 fixes the analyzer diagnostic documentation links, which pointed at a non-existent domain.

### Bug fixes

- **Analyzer `helpLinkUri` values now resolve to the live documentation site** — The `HelpLinkUri` for every diagnostic in both analyzer packages pointed at `https://corvus-text-json.dev/docs/...`, a domain that does not exist, so clicking "Show documentation" on a diagnostic (or following the link in build output) reached a dead link. Both packages now point at the real site, `https://corvus-oss.org/Corvus.JsonSchema/docs/...`. For the production analyzers (`CTJ001`–`CTJ010`) the per-diagnostic anchors on `analyzers.html` already matched. For the migration analyzers (`CVJ001`–`CVJ025`) the base page was additionally retargeted from the prose migration guide to the dedicated `migration-analyzers.html`, and every diagnostic now deep-links to its own `#cvjNNN-…` section anchor (previously several fragments such as `#namespace-changes`, `#core-types`, and `#mutation` did not match any anchor on the target page). See [#766](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/766).

## V5.1.14

V5.1.14 fixes a concurrency bug in OpenAPI 3.2 generated streaming server endpoints.

### Bug fixes

- **OpenAPI 3.2 streaming endpoints no longer hold a thread-local rented `Utf8JsonWriter` across an `await`** — Generated server endpoints for streaming (`itemSchema`/NDJSON/SSE) responses rented a `Utf8JsonWriter` from the thread-local writer cache (`workspace.RentWriter(...)`) and held it across the `await` on `WriteStreamAsync`. Because the cache is backed by `[ThreadStatic]` state, the writer is bound to the renting thread; when the streaming continuation resumed on a different thread pool thread (the normal case under Kestrel, which has no `SynchronizationContext`), the matching `ReturnWriter` ran on the wrong thread and corrupted the cache — throwing a `NullReferenceException` (or, in debug builds, failing fast) when the continuation thread had no cache state, and otherwise silently poisoning another thread's cache. Streaming endpoints now use a new `JsonWorkspace.CreateWriter(IBufferWriter<byte>)` that returns a dedicated, non-pooled writer with no thread affinity, disposed via `await writer.DisposeAsync()` — which is safe to release on any thread. The non-streaming response path was unaffected (it rents and returns the writer synchronously with no intervening `await`) and is unchanged, as are the OpenAPI 3.0 and 3.1 generators (which have no streaming path). See [#814](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/814).

### New features

- **`JsonWorkspace.CreateWriter(IBufferWriter<byte>)`** — Creates a dedicated `Utf8JsonWriter` that is **not** drawn from the thread-local writer cache, so it carries no thread affinity and is safe to hold across an `await` boundary (for example while streaming a response). The caller owns the writer and disposes it; it must not be passed to `ReturnWriter`. Use `RentWriter`/`RentWriterAndBuffer` for the common synchronous case where the writer is rented and returned on the same thread without an intervening `await`. See [#814](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/814).

## V5.1.13

V5.1.13 fixes three V5 bugs — `CreatePatch` failing on frozen `JsonDocumentBuilder` elements, schema `default` values being invisible through the mutable view, and circular schemas producing non-terminating generated code — and adds the ability to construct a discriminated union directly from one of its branches.

### New features

- **Construct a discriminated union from a constituent branch** — A well-formed discriminated union (a `oneOf` whose branches carry a required `const` discriminator, with no further required structure on the base) can now be built directly from one of its branches. A branch's mutable view converts to the union in a single implicit hop (`Circle.Mutable` → `Shape`), and the union's builder `Source` accepts a branch by value, so a built branch flows straight into a containing type's `CreateBuilder` for a union-typed property — for example `ShapeHolder.CreateBuilder(ws, ShapeHolder.Circle.Build(...))` — with no intermediate union document. Detection is precise: extra required properties on the base, a missing or non-required `const`, or an `anyOf` (rather than `oneOf`) do not enable the wiring, while additional non-structural keywords (`title`, `description`, and so on) on the base still do. See [#812](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/812).

### Bug fixes

- **Circular same-instance composition now fails generation with a diagnostic** — A `oneOf`/`allOf`/`anyOf`/`$ref` cycle that evaluates against the *same instance* previously produced infinitely-recursive `Match()`/`TryGetAs` code (or silently-omitted code). Code generation now detects the non-terminating composition cycle and fails with a `CircularSchemaReferenceException` naming the referencing and referenced schema locations, surfaced by the V4 and V5 source generators as diagnostic `CRV1002`. Data-driven recursion through properties or items is correctly not flagged. Applies to both the V4 and V5 engines. See [#810](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/810).
- **Schema `default` values are surfaced through the mutable view** — A non-null `default` declared on a property was invisible through a type's mutable view, and reading it could corrupt the pooled workspace. The mutable getter now returns a zero-copy frozen facade over the immutable default: reads forward to the underlying value, child elements rebase onto the facade, and any attempt to mutate the default throws an `InvalidOperationException` instructing the caller to set the value on its parent first. See [#811](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/811).
- **`CreatePatch` works with frozen `JsonDocumentBuilder` elements** — Generating a JSON Patch from a frozen `JsonDocumentBuilder` element threw because the read-only metadata copy-out paths performed an unnecessary immutability check. The check has been removed from the copy-out paths (which only read data), so `CreatePatch` round-trips correctly when either side is a frozen builder element. See [#809](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/809).

## V5.1.12

V5.1.12 fixes two V5 bugs: `Corvus.Text.Json.Period` could emit `duration` strings that are invalid under the JSON Schema `duration` format, and calling `Freeze()` on a `JsonDocumentBuilder` created via `Parse` threw an exception.

### Bug fixes

- **`Period` no longer emits invalid `duration` strings** — The V5 `Corvus.Text.Json.Period` formatter (used by `Period.ToString()` and when writing a period to a JSON document, including the OpenAPI server `Build` methods) could emit a fractional seconds value, for example `P0Y0M0DT0H0M6.220724100S` for a period built from a sub-second `System.TimeSpan`. The JSON Schema `duration` format (RFC 3339 Appendix A) does not permit fractional seconds, so such values failed to round-trip through `Period.TryParse`. The formatter now rounds any sub-second component (milliseconds, ticks, nanoseconds) to the nearest whole second — rounding halves away from zero — producing a valid, round-trippable duration. As part of the fix the formatter also omits unnecessary zero-valued units where the RFC 3339 grammar allows, so output is now compact (for example `PT6S` and `P7D` rather than `P0Y0M0DT0H0M6S` and `P0Y0M7D`). Consumers that compared the exact formatted string should note the more compact output. See [#805](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/805).
- **`Freeze()` on a parsed `JsonDocumentBuilder` no longer throws** — Calling `Freeze()` on a document produced by `JsonDocumentBuilder<T>.Parse(...)` threw `ArgumentException` (`Offset and length were out of bounds`) from `Buffer.BlockCopy`. The freeze copy assumed every value was stored as a length-prefixed *DynamicValue* blob, but values from a parsed document point directly into the original UTF-8 JSON backing region with no such header, so the copy read a bogus length. Freezing now compacts the value backing into a raw-JSON region and a DynamicValue region and propagates the raw-region length to the frozen document, so parsed, mutated, and from-scratch documents all freeze correctly. See [#808](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/808).

## V5.1.11

V5.1.11 adds per-format assertion mode configuration to the V4 and V5 code generation pipelines, so consumers can selectively disable a single `format` assertion or downgrade it to a warning without weakening all format validation.

### New features

- **Per-format assertion modes** — A new `FormatAssertionMode` (`Assert` / `Disable` / `Warning`) can be configured per format, letting you keep strict format validation while relaxing an individual format that real-world data violates (for example a `date-time` missing its timezone offset). The effective mode is resolved per format as override > vocabulary > the global `assertFormat` default > disable. Configurable via the `corvusjson` CLI `--formatMode` flag, a `formatMode` object in `generator-config.json`, the `CorvusTextJsonFormatMode` / `CorvusJsonSchemaFormatMode` MSBuild properties, and the `CSharpLanguageProvider.Options` API. `Warning` applies to string formats (a non-conformant value is reported rather than rejected); numeric formats fall back to `Assert`. Mode selection happens at generation time, so the default assert path carries no extra runtime branches. Supported by both engines. See [#749](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/749).

## V5.1.10

V5.1.10 extends OpenAPI server generation to extract an operation's declared `security` for OpenAPI 3.0 and 3.1 (not just 3.2), models it with correct OR/AND semantics, and adds a one-line authorization helper.

### New features

- **Declared security extraction and authorization helper** — Generated servers now populate each endpoint's security requirements for OpenAPI 3.0 and 3.1 (previously only 3.2 did so; 3.0/3.1 always emitted an empty array despite the spec supporting `security`). `EndpointSecurityRequirement` carries the resolved `SchemeType` (oauth2/apiKey/http/openIdConnect) and a canonical `PolicyName`, and a generated `EndpointSecurityConventions.RequireDeclaredAuthorization` extension implements the default mapping (`AllowAnonymous` when there is no security, `RequireAuthorization(PolicyName)` per requirement) so the common case is a one-line hook. The generated registration takes no dependency on `Microsoft.AspNetCore.Authorization`, and the custom `configureEndpoint` escape hatch is unchanged. See [#791](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/791).
- **OR/AND security semantics** — `EndpointDescriptor.SecurityRequirements` is now an `IReadOnlyList<EndpointSecurityRequirementSet>`, one element per OR alternative, each exposing its AND-group of requirements, an `IsOptional` marker for the anonymous (`{}`) requirement, and a canonical `PolicyName`. `RequireDeclaredAuthorization` honours the structure (AND within an alternative; a single combined policy for multiple OR alternatives), correcting the previous behaviour that flattened the array and enforced the alternatives as if all were required. See [#791](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/791).

## V5.1.9

V5.1.9 emits `Build(...)` overloads on V5 object types that take the `Create(...)` property parameters directly, removing the nested builder lambda at call sites.

### New features

- **Property-parameter `Build(...)` overloads** — V5 object types now emit `Build(...)` / `Build<TContext>(...)` factories that capture the `Create(...)` arguments directly, so `quaternion: Quaternion.Build(w: r.W, x: r.X, y: r.Y, z: r.Z)` replaces the previous `(ref b) => b.Create(...)` lambda form. The captured arguments materialise through the existing static `Create` with no closure allocation. A type emits the overload only when it has at least one `Create` parameter, is not part of a reference cycle (keeping a `Source` from ever transitively containing itself), and its captured-slot weight is within the configured threshold (default 32); recursive or over-threshold types keep the delegate/context `Build` form with a `<remarks>` explaining the omission. The threshold is configurable via the `CSharpLanguageProvider.Options` API, the `CorvusTextJsonBuildParametersThreshold` MSBuild property, and the `corvusjson` CLI `--buildParametersThreshold` flag. See [#789](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/789).

## V5.1.8

V5.1.8 adds an opt-in `NullOrUndefinedExceptNonNullDefaulted` value for the `OptionalAsNullable` generation option — generating optional properties that declare a non-null `default` as non-nullable `T` — and fixes a V5 bug where an explicit JSON `null` was not mapped to C# `null` under `OptionalAsNullable=NullOrUndefined`.

### New features

- **`OptionalAsNullable=NullOrUndefinedExceptNonNullDefaulted`** — A new opt-in value for the `OptionalAsNullable` generation option (CLI `--optionalAsNullable`, MSBuild `CorvusTextJsonOptionalAsNullable` / `CorvusJsonSchemaOptionalAsNullable`), supported by both the V4 and V5 engines. It behaves like `NullOrUndefined`, except that an optional property which declares a **non-null** `default` is generated as a non-nullable `T` (the default is always materialised when the property is absent), rather than `T?`. Optional properties without a `default`, or whose `default` is JSON `null`, remain nullable `T?`. The new value is additive — existing `None` and `NullOrUndefined` consumers are unaffected. See [#787](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/787).

### Breaking changes

- **V5 `OptionalAsNullable=NullOrUndefined` now maps an explicit JSON `null` to C# `null`** — Previously the V5 engine returned a `Null`-kind value (rather than C# `null`) when an optional property was explicitly present as JSON `null`, implementing only the "Undefined" half of the documented "JSON `null` or missing values map to C# `null`" contract. It now returns C# `null` for an explicit JSON `null`, matching both the documentation and the V4 engine. Only generated getters for optional properties under `NullOrUndefined` are affected; absent properties, and properties with a non-null `default`, are unchanged. See [#787](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/787).

## V5.1.7

V5.1.7 adds an optional per-endpoint configuration callback to the generated OpenAPI server registration, enabling consumers to apply ASP.NET endpoint conventions — including wiring the OpenAPI security specification onto endpoints — without editing generated code.

### New features

- **Per-endpoint configuration callback for generated servers** — The generated `MapApiEndpoints` extension gains an additive overload that accepts a `ConfigureEndpoint` callback. It is invoked once per generated endpoint (including webhook/callback endpoints) with an `EndpointDescriptor` (operation id, generated method name, HTTP verb, route template, tags, callback origin, and the operation's security requirements) and the route's `IEndpointConventionBuilder`. This lets consumers apply per-endpoint conventions — authorization, naming, tags, output caching, rate limiting — and wire the OpenAPI security specification onto endpoints without editing generated code. The original overload is preserved, so the change is source- and binary-compatible. Implemented for OpenAPI 3.0/3.1/3.2 across both the regular and callback/webhook server paths, with no new package dependency on the generated server. See [#783](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/783).

## V5.1.5

V5.1.5 adds context-flowing source overloads to generated V5 mutable builders.

### New features

- **Context source builder overloads** — Generated V5 mutable object and array builders now expose `AddProperty<TContext>(propertyName, in PropertyType.Source<TContext> value)` and `AddItem<TContext>(in ItemType.Source<TContext> value)` overloads (for the `ReadOnlySpan<byte>`, `ReadOnlySpan<char>`, and `string` property-name forms, with `where TContext : allows ref struct` on .NET 9 and later). This lets a context-bearing `.Source<TContext>` value be added directly to a builder without first materialising it. See [#780](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/780).

## V5.1.4

V5.1.4 changes generated OpenAPI server result factories to take `.Source` types for response headers, deprecates `ParseValue()`, adds `JsonWorkspace.TakeOwnership()`, and fixes webhook schema pointer resolution.

### Breaking changes

- **Server result factory methods use `.Source` for response headers** — Generated server result factory methods (e.g., `ListPetsResult.Ok(...)`) now accept `.Source` types for response header parameters instead of realized types. For example, a header parameter changes from `JsonString xNext = default` to `JsonString.Source xNext = default`. Existing code that passes realized types (e.g., `JsonString.ParseValue(...)`) continues to compile via implicit conversion. Factories with headers but no body now also require a `JsonWorkspace workspace` parameter.
- **`ParseValue()` deprecated** — All `ParseValue()` overloads on `JsonElement` and generated types are now marked `[Obsolete]`. Use `ParsedJsonDocument<T>.Parse()` for pooled-memory parsing (returns a disposable document that recycles memory), or `Clone()` when you genuinely need a standalone copy. `ParseValue()` allocates backing memory that becomes GC garbage — the deprecation makes the performance tradeoff explicit. See [#772](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/772).

### New features

- **`JsonWorkspace.TakeOwnership()`** — Transfers lifetime ownership of any `IJsonDocument` (including `ParsedJsonDocument<T>`) to a workspace. The document is disposed when the workspace is disposed or reset. This enables handler patterns where parsed response data must outlive the handler method but should be cleaned up with the workspace, without forcing consumers into mutable builders for read-only scenarios. See [#777](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/777).

### Bug fixes

- **Webhook schema pointer resolution** — `openapi-callback-server` previously constructed JSON pointers using `#/paths/{name}` instead of `#/webhooks/{name}` when processing webhook schemas, causing resolution failures. `SchemaPointerBuilder` full-pointer methods now accept a `rootSegmentUtf8` parameter to correctly distinguish between paths and webhooks. See [#773](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues/773).

## V5.1.2

V5.1.2 is a breaking change that moves generated JSON Schema model types into a `.Models` sub-namespace for OpenAPI and AsyncAPI code generation, preventing name collisions with request/response infrastructure types.

### Breaking changes

- **OpenAPI/AsyncAPI model types now in `.Models` sub-namespace** — JSON Schema model types generated by `corvusjson openapi-client`, `openapi-server`, `openapi-callback-client`, `openapi-callback-server`, and `asyncapi-generate` are now placed in a `.Models` sub-namespace (e.g., `Petstore.Client.Models` instead of `Petstore.Client`). Consumer code must add a `using` directive for the new namespace (e.g., `using Petstore.Client.Models;`). Request/response infrastructure types (producers, consumers, handler interfaces, request/response classes) remain in the root namespace.

## V5.1.1

V5.1.1 adds TOON conversion support and fixes OpenAPI 3.2 server streaming responses generated from `itemSchema` response content.

### New features

- **TOON conversion** — Added bidirectional TOON (Token-Oriented Object Notation) conversion for JSON-shaped data, with `Corvus.Text.Json.Toon` for the Corvus pooled document model and `Corvus.Toon.SystemTextJson` for `System.Text.Json`-only applications. The converters support TOON to JSON, JSON to TOON, parsed documents, UTF-8 buffer APIs, tabular object arrays, dotted-path expansion, key folding, documentation, examples, benchmarks, and a browser playground.

### Bug fixes

- **OpenAPI 3.2 server streaming responses** — Generated ASP.NET Core server stubs now emit response bodies for `itemSchema` streaming responses. Streaming result factories use generated push-writer callbacks, frame `text/event-stream` responses as SSE (`data: ...\n\n`), frame `application/x-ndjson` and other streaming media as newline-delimited JSON, and complete the HTTP stream when the callback returns.

## V5.1

V5.1 expands Corvus.Text.Json beyond JSON Schema model generation into strongly-typed API generation, and closes a set of V4/V5 parity gaps that were identified during the 5.1 release wrap-up.

### New features

- **OpenAPI client generation** — `corvusjson openapi-client` generates strongly-typed HTTP clients for OpenAPI 3.0, 3.1, and 3.2 specifications, including typed parameters, request validation, response result types, headers, streaming, multipart forms, binary payloads, and `MatchResult()` response dispatch.
- **OpenAPI server generation** — `corvusjson openapi-server` generates ASP.NET Core handler interfaces and endpoint registration for minimal APIs, using the same typed request/response models and schema validation as generated clients.
- **AsyncAPI producer/consumer generation** — `corvusjson asyncapi-generate` generates strongly-typed producers, consumers, handlers, and request/reply flows for AsyncAPI 2.6 and 3.0 specifications.
- **AsyncAPI transports** — Generated AsyncAPI applications can use runtime transport packages for NATS, Kafka, AMQP, MQTT, WebSocket, Azure Service Bus, and in-memory testing.
- **OpenAPI and AsyncAPI document models** — Added strongly-typed V5 models for OpenAPI 3.0/3.1/3.2 and AsyncAPI 2.6/3.0 specifications.
- **Pattern property helper APIs** — V5 generated types with `patternProperties` now include generated `MatchesPattern*`, `TryAsPattern*`, and `MatchPatternProperties()` visitor dispatch helpers. V4 now has the visitor dispatch helper alongside its existing per-pattern helpers.
- **CLI accessibility configuration** — The `corvusjson` CLI and config file now support default and per-named-type generated accessibility (`Public`/`Internal`) for both V4 and V5 engines.
- **V4 generated formatting APIs** — V4 generated types now implement `IFormattable`, and on supported TFMs `ISpanFormattable` and `IUtf8SpanFormattable`, including format-aware numeric and string-format paths.
- **V4 functional composition apply** — V4 generated object composition types now emit `WithApplied(in ComposedType value)` methods for functional merging of `allOf`/`anyOf`/`oneOf` composition type properties.
- **V4 generated debugger display** — V4 generated types now include `[DebuggerDisplay]` with a hidden debugger display property.

### Breaking changes

- **V4 multi-core union conversions are now explicit** — Generated V4 conversions from a multi-core union type to `bool` or the preferred numeric .NET type are now `explicit` instead of `implicit`. This fixes unsafe implicit conversions that could throw at runtime when the instance held a different branch. Code that relied on these implicit conversions must now use an explicit cast and handle invalid branch values appropriately.
- **V5 mutable composition conversions now return mutable results** — Generated `TryGetAs___` methods on mutable V5 composition types now use `out Component.Mutable` instead of `out Component`. Existing callers that declare the old immutable out-variable type, rely on `out var` inferring the immutable type, or depend on generic/overload inference from the old result type must update the declaration or assign the returned mutable value to an immutable variable after the call.

### Bug fixes

- **V4 escaped property-name handling** — `JsonPropertyName`, `JsonObjectProperty`, and `JsonObjectProperty<T>` now use decoded/unescaped property names when backed by `JsonProperty`. This fixes comparisons and regex matching for escaped JSON property names while preserving zero-allocation callback paths.
- **V4 WASM trim warnings** — Removed trim-unsafe `System.Text.Json.JsonSerializer` usage from V4 value serialization and Int128/UInt128 numeric fallbacks. WASM/trimming builds no longer report the related IL2026 warnings.
- **API documentation signatures** — API documentation generation now preserves `in`/`out`/`ref` modifiers and nullable annotations when generating method signatures on Ubuntu.
- **JsonElement API example** — The V5 `JsonElement` API example now demonstrates `JsonElement` directly instead of showing generated-model APIs.
- **CLI package README** — The `Corvus.Json.Cli` NuGet package now has its own README, and the V4 engine is described as the immutable `Corvus.Json.ExtendedTypes` model rather than as legacy.

## V5.0

V5 introduces the new **Corvus.Text.Json** engine — a brand new code generator and runtime library that uses the existing Corvus.Json.CodeGeneration framework, and builds on the patterns of `System.Text.Json` with pooled-memory parsing, mutable document building via `JsonWorkspace`, and familiar strongly-typed `readonly struct` wrappers generated from JSON Schema, with a streamlined API and substantial performance improvements.

What we now call the V4 Engine continues to be maintained in this library - and with the same command line tool - and provides our solution for a side-effect-free mutation model.

### New features

- **Pooled-memory parsing** — `ParsedJsonDocument<T>` backed by `ArrayPool<byte>`. Just 136 bytes per document.
- **Mutable documents** — `JsonDocumentBuilder<T>` and `JsonWorkspace` provide a builder pattern for creating and modifying JSON 'in place', with versioned elements that detect stale references.
- **Extended numeric types** — `BigNumber` for arbitrary-precision decimals, `BigInteger` for large integers, plus `Int128`, `UInt128`, and `Half`.

### Breaking changes

- The CLI tool has been renamed from `generatejsonschematypes` (package: `Corvus.Json.CodeGenerator`) to `corvusjson` (package: `Corvus.Json.Cli`). Schema generation is now the `jsonschema` subcommand: `corvusjson jsonschema schema.json ...`. The legacy `generatejsonschematypes` command still works as a shim but displays a deprecation warning.
- The `corvusjson` CLI tool defaults to the V5 engine. The legacy `generatejsonschematypes` shim defaults to V4. To explicitly select an engine, use `--engine V4` or `--engine V5`.
- V5 generated types use the `Corvus.Text.Json` namespace and require the `Corvus.Text.Json` NuGet package at runtime, rather than `Corvus.Json.ExtendedTypes`.
- The immutable functional API from V4 (`WithProperty()`, `SetItem()`, etc.) is replaced by the mutable builder pattern (`CreateBuilder()`, `SetProperty()`, etc.).
- We now support the syntax of ECMAScript Regular Expressions (with the /u Unicode option) by translating them to .NET Regular Expressions during code generation.

### Bug fixes

- **Duration format validation** — Now strictly validates against RFC 3339 Appendix A grammar. Previously accepted fractional values (`PT0.5S`), non-contiguous designators (`P1Y2D` skipping Months), and other ISO 8601 extensions that are not part of RFC 3339. Both V5 and V4 engines are fixed.
- **URI percent-encoding validation** — Now rejects invalid percent-encoded sequences (`%`, `%A`, `%6G`). Previously these were accepted by the URI validator. Both V5 and V4 engines are fixed.
- **Hostname validation** — Now correctly allows consecutive hyphens in hostnames per RFC 1123 (`ab--cd.example`). Previously all `--` sequences were rejected due to overly strict IDNA/punycode detection. Both V5 and V4 engines are fixed.

### V4 test infrastructure

- **Name-based test exclusions** — The V4 spec generator now uses name-based test exclusions (matching by scenario and test description) instead of brittle index-based exclusions. This makes the V4 test suite stable across JSON Schema Test Suite updates that reorder tests.
- **OpenAPI 3.0 patternProperties exclusions** — Tests for `patternProperties` in the OpenAPI 3.0 test suite are now excluded, as `patternProperties` is not a supported keyword in the [OpenAPI 3.0 Schema Object](https://spec.openapis.org/oas/v3.0.4.html#schema-object).

## V4.6 Updates

### Breaking changes

We had a long-standing bug with the pseudo-generic type pattern and `$dynamicRef` where you would get an extra level of indirection because the anchoring `$ref` was not reducible. This is now fixed, but any code using `$dynamicRef` will need to be simplified to remove the redundant indirection.

## V4.5 Updates

### Breaking changes (Language Provider Implementers only)

The `IKeywordValidationHandler` interface contains a number of APIs that are, with hindsight, specific to the particular implementation in the `CSharpLanguageProvider` implementation.

It forces you into a method definition / method call pattern, and also implementing the "child handler" pattern.

While the child-handler pattern is still likely useful, it may have a completely different implementation in other providers.

The method definition / method call pattern is very much an "implementer's choice" and should not be imposed on all future implementations.

This breaking change applies to *language provider implementers* only, and it splits `IKeywordValidationHandler` into `IKeywordValidationHandler` and `IMethodBasedKeywordValidationHandlerWithChildren`

If you have any existing code that depends on `IKeywordValidationHandler` you will need to update it to use `IMethodBasedKeywordValidationHandlerWithChildren` instead. This can be done with a global search and replace.

You will likely also need to use the new overload of the `TypeDeclaration` extension method `OrderedValidationHandlers<T>()` to retrieve the handlers using the correct interface.

For example, the CSharpLanguageProvider has been updated in three places to use the new overload, so we can access the handler via the new interface. Unsurprisingly, these are the three places that make use of the method based/child handler pattern. Here's one of those.

```csharp
    private static CodeGenerator AppendValidationHandlerSetup(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator.AppendUsingEvaluatedItems(typeDeclaration);
        generator.AppendUsingEvaluatedProperties(typeDeclaration);

        foreach (IMethodBasedKeywordValidationHandlerWithChildren handler in typeDeclaration.OrderedValidationHandlers<IMethodBasedKeywordValidationHandlerWithChildren>(generator.LanguageProvider))
        {
            handler.AppendValidationSetup(generator, typeDeclaration);
        }

        return generator;
    }
```

## V4.4.3 Updates

Added <CorvusJsonSchemaFallbackVocabulary>Corvus202012</CorvusJsonSchemaFallbackVocabulary> to support the `$corvusTypeName` keyword without requiring you to specify an explicit `$schema` for the vocabulary.

## V4.4 Updates

### Breaking changes

The property accessor mechanism now respects the default value of the property type.

If the schema for the property defines a `default` value, then the property accessor will return this value if and only if the value actual value is `ValueKind.Undefined`.

This does *not* affect equality or other comparisons for the object as a whole - if one value has the property *explicitly* set to the default value, and the other is relying on the "default" value, then the instances *will not* be equal.

If you use the `TryGetProperty()` mechanism to get the property value, this *will not* return the default from its `JsonObjectProperty.Value`. However, if you have access to a `JsonObjectProperty<TValue>` where the type of the property is known, then the `Value` will respect the default in the same way as the property itself.

### Fixes

In the latest version of the .NET SourceGenerator codebase, the behaviour when properties are missing has changed (we would suggest "is broken"). It no longer returns false and a null value for a missing property, but instead provides an empty string. This broke our default-value logic for settings - the net effect of which is that forcing format validation by default is accidentally switched off.

We have restored the previous behaviour, regardless of which version of the source generator infrastructure is in use.

## V4.3.17 Updates

Added netstandard2.1 packages for `Corvus.Json.ExtendedTypes` and `Corvus.Json.JsonReference` in order to support Unity builds.

## V4.3.16 Updates

### Use of IndexRange package is deprecated.

As of V1.1 of IndexRange, it now type-forwards to the recently shipped `Microsoft.Bcl.Memory` library. We will be removing the dependency on IndexRange in the V4.4 release cycle (some time after .NET 10 ships), and replacing it directly with `Microsoft.Bcl.Memory`. You should make the changes in your own code base if you have a direct dependency on `IndexRange` with this releasee in order to prepare for that change.

## V4.3.10 Updates

Added `<CorvusJsonSchemaUseImplicitOperatorString>true</CorvusJsonSchemaUseImplicitOperatorString>` to enable implicit conversion to `string`.

WARNING: Although this is very convenient for string-heavy code, it may cause unintended allocations if used without care.

## V4.3.0 Updates

### Type Accessibility using the Source Generator

The source generator now respects the accessibility of the model type.

For example

```csharp
[JsonSchemaTypeGenerator("../test.json#/$defs/FlimFlam")]
internal readonly partial struct FlimFlam
{
}
```

Any nested types will be generated with `public` accessibility.

Only `internal` and `public` are supported. The source generator will fail for an unsupported accessiblity declaration.

You can override the default accessibility for all generated types with a build property:

`<CorvusJsonSchemaDefaultAccessibility>Internal</CorvusJsonSchemaDefaultAccessibility>`

Note that you can still generate code that will not compile if you incorrectly mix-and-match `public` and `internal`. It is your responsibility to ensure that your types have compatible accessibility.

## V4.2.0 Updates

### Breaking change

The heuristic for naming (but not ordering) parameters to the `JsonObject.Create()` function has changed, to fix an issue with a parameter naming where properties differ only by case.

This could affect code that is using explicit named parameters with `Create()`, if your parameter changes its name.

If this causes a significant problem in your codebase, please raise an issue here and we will work with you to resolve the problem.

## V4.1.2 Updates

Added the `--addExplicitUsings` switch to the code generator (and a corresponding property to the `generator-config.json` schema). If `true`, then
the source generator will emit the standard global usings explicitly into the generated source files. You can then use the generated code in a project that does not have `<ImplicitUsings>enable</ImplicitUsings>`.

```csharp
using global::System;
using global::System.Collections.Generic;
using global::System.IO;
using global::System.Linq;
using global::System.Net.Http;
using global::System.Threading;
using global::System.Threading.Tasks;
```

## V4.1.1 Updates

## Help for people building analyzers and source generators with JSON Schema code generation

We have built a self-contained package called Corvus.Json.SourceGeneratorTools for people looking to build .NET Analyzers or Source Generators that take advantage of JSON Schema code generation.

See the [README](./Solutions/Corvus.Json.SourceGeneratorTools/README.md) for details.

## V4.1 Updates

### YAML support

We now support YAML documents for the CLI tool.

You can mix-and-match YAML and JSON documents in the same schema set, and the tool will generate code for either.

Your JSON schema can be embedded in a YAML document (such as a YAML-based OpenAPI or AsyncAPI document), and you can resolve internal references just as with a JSON document.

Add the `--yaml` command line option to enable YAML support, or set the `supportYaml: true` property in a generator config file

#### Example

*schema.yaml*
```yaml
type: array
prefixItems:
  - $ref: ./positiveInt32.yaml
  - type: string
  - type: string
    format: date-time
unevaluatedItems: false
```

*positiveInt32.yaml*
```yaml
type: integer
format: int32
minimum: 0
```

```
generatejsonschematypes --rootNamespace TestYaml --outputPath .\Model --yaml schema.yaml
```

## V4.0 Updates

There are a number of significant changes in this release

### Support for cross-vocabulary schema generation.

  So if you are upgrading a draft6 or draft7 schema set to 2020-12, for example, you can do it piecemeal and reference a schema with one dialect from a schema with another.

### Opt-in support for .NET nullable properties

  Where JSON Schema object properties are optional or nullable, use the `--optionalAsNullable` command line switch to emit nullable properties.

### Opt-in support for implicit conversions to `string` from JSON `string` types

If you have a JSON `string` type, we currently emit an `explicit` operator to convert to a .NET `string` (the counterpart of the `implicit` conversion operator *from* a .NET `string`).

We do this because conversion to string causes an allocation, and it is very easy to inadvertently do this when working with APIs that offer `string`-based overloads, in addition to e.g.
`ReadOnlySpan<char>` overloads. When passing an instance of the generated type directly to the API, the implicit conversion would kick in, allocating a string, with no warning that this
is what you have done. In a high-performance/low-allocation scenario this would be undesirable, and you would prefer to use the `GetValue()` method on the instance,
and pass the `ReadOnlySpan<char>` provided to the callback for that method.

However, sometimes you just want the convenience of being able to behave as if your JSON value is a `string`.

If so, you can now use the `--useImplicitOperatorString` command line switch to emit an implicit conversion operator to `string` for JSON `string` types.

Note: this means you will never use the built-in `Corvus.Json` types for your string-like types. This could increase the amount of code generated for your schema.

### New Source Generator

We now have a source generator that can generate types at compile time, rather than using the `generatejsonschematypes` tool.

### Using the source generator

Add a reference to the `Corvus.Json.SourceGenerator` nuget package in addition to `Corvus.Json.ExtendedTypes`. [Note, you may need to restart Visual Studio once you have done this.]
Add your JSON schema file(s), and set the Build Action to _C# analyzer additional file_.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Corvus.Json.ExtendedTypes" Version="4.3.9" />
    <PackageReference Include="Corvus.Json.SourceGenerator" Version="4.3.9">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="test.json" />
  </ItemGroup>

</Project>
```

Now, create a `readonly partial struct` as a placeholder for your root generated type, and attribute it with
`[JsonSchemaTypeGenerator]`. The path to the schema file is relative to the file containing the attribute. You can
provide a pointer fragment in the usual way, if you need to e.g. `"./somefile.json#/components/schema/mySchema"`

```csharp
namespace SourceGenTest2.Model;

using Corvus.Json;

[JsonSchemaTypeGenerator("../test.json")]
public readonly partial struct FlimFlam
{
}
```

The source generator will now automatically emit code for your schema, and you can use the generated types in your code.

```
using Corvus.Json;
using SourceGenTest2.Model;

FlimFlam flimFlam = JsonAny.ParseValue("[1,2,3]"u8);
Console.WriteLine(flimFlam);
JsonArray array = flimFlam.As<JsonArray>();
Console.WriteLine(array);
```

You can find an example project here: [Sandbox.SourceGenerator](./Solutions/Sandbox.SourceGenerator)

We'd like to credit our Google Summer of Code 2024 contributor, [Pranay Joshi](https://github.com/pranayjoshi) and mentor [Greg Dennis](https://github.com/gregsdennis) for their work on this tool.

#### Configuring the source generator

There are a number of global configuration options for the source generator. These can be added to a `PropertyGroup` in your `.csproj` file.

e.g.

```xml
<PropertyGroup>
   <CorvusJsonSchemaOptionalAsNullable>None</CorvusJsonSchemaOptionalAsNullable>
</PropertyGroup>
```

`CorvusJsonSchemaOptionalAsNullable`
  - `None` - Do not emit nullable properties for optional properties
  - `NullOrUndefined` - Emit nullable properties for optional properties

`CorvusJsonSchemaDisableOptionalNamingHeuristics`
  - `False` - Enable optional naming heuristics [default]
  - `True` - Disable optional naming heuristics

`CorvusJsonSchemaDisabledNamingHeuristics`
  - Semi-colon separated list of naming heuristics to disable. You can list the available name heuristics with the `generatejsonschematypes listNameHeuristics` command in the CLI.

`CorvusJsonSchemaAlwaysAssertFormat`
  - `False` - Respect the vocabulary's format assertion
  - `True` - Always assert format assertions [default]

### New dynamic schema validation

There is a new `Corvus.Json.Validator` assembly, containing a `JsonSchema` type.

This is a new *dynamic* JSON Schema validator that can validate JSON data against a JSON Schema document, without the need to generate code ahead-of-time.

This is useful for scenarios where you have a JSON Schema document that is not known at compile time, and you only require validation, not deserialization.

You can load the schema with

```csharp
var corvusSchema = CorvusValidator.JsonSchema.FromFile("./person-array-schema.json");
```

This builds and caches a schema object from the file, and you can then validate JSON data against it with

```csharp
JsonElement elementToValidate = ...
ValidationContext result = this.corvusSchema.Validate(elementToValidate);
```

Note that this uses dynamic code generation under the hood, with Roslyn, so there is an appreciable cold-start cost for the very first schema you validate in this way
while the Roslyn components are jitted. Subsequent schema are much faster, and reused schema come from the cache.

If you reference the `Corvus.Validator` package directly in your executing assembly, it will include a target that ensures `<PreserveCompilationContext>true</PreserveCompilationContext>`
is added to a `<PropertyGroup>` in your project.

If you are using the `Corvus.Json.Validator` package in a library, you should ensure that the consuming project has this property set, to avoid issues with dynamic code generation.

You will have to do this manually if it is consumed via a Project Reference.

### New `generatejsonschematypes config` command

  Supply a json config file to the generate command, to configure and generate 1 or many schema in a single command.

  The configuration file also allows you to explicitly name arbitrary types, and optionally map them in to a specific .NET namespace.

  You can also map json schema base file URIs to specific .NET namespaces, and pre-load known-good versions of file reference dependencies.

  The [schema for the configuration file is here](./Corvus.Json.CodeGenerator/generator-config.json).

### New command line validator with `generatejsonschematypes validateDocument`

This command will validate a JSON document against a JSON schema, and output the results to the console.

For example, given schema `schema.json`

```json
{
    "$schema": "https://corvus-oss.org/json-schema/2020-12/schema",
    "type": "array",
    "prefixItems": [
        {
            "$corvusTypeName": "PositiveInt32",
            "type": "integer",
            "format": "int32",
            "minimum": 0
        },
        { "type": "string" },
        {
            "type": "string",
            "format": "date-time"
        }
    ],
    "unevaluatedItems": false
}
```

and the document `document_to_validate.json`

```json
[
    -1,
    "Hello",
    "Goodbye"
]
```

If we run:

```
generatejsonschematypes validateDocument ./schema.json ./document_to_validate.json`
```

We see the output:

```
Validation minimum - -1 is less than 0 (#/prefixItems/0/minimum, #/prefixItems/0, #/0, ./testdoc.json#1:4)
Validation type - should have been 'string' with format 'datetime' but was 'Goodbye'. (#/prefixItems/2, , #/2, ./testdoc.json#3:4)
```
### Multi-language code generator engine

- Brand new JSON Schema analyser engine, which is now language independent.
- Brand new code generation engine, which is more flexible and extensible, and uses the result of the schema analyser.
- An extensible C# language provider which generates code-using-code. No more T4 templates in the language engine.

### Additional features

- Opt-out of optional naming heuristics introduced in V3.0 with the `--disableOptionalNamingHeuristics` command line switch.
- Opt-out of specific naming heuristics by specifying `--disableNamingHeuristic`. You can list the available name heuristics with the new `generatejsonschematypes listNameHeuristics` command
- Safe truncation for extremely long file names
- Access to all JSON schema validation constants via the `CorvusValidation` nested static class.
- All formatted types (e.g. string or number formats) are now convertible to the equivalent core types (e.g. your custom `"format": "date"` type is freely convertible to and from `JsonDate`) and offer the same accessors and conversions as the core types.

### Upgrading to V4
- Code generated using V3.1 of the generator can still be built against V4 of Corvus.Json.ExtendedTypes, and used interoperably.

  This allows you to upgrade your code piecemeal to the new version of the generator. You do not need to update everything all at once.

- For the vast majority of schema, the new naming heuristics will continue to work as they did in V3.
  However, if you have a schema that is not generating the names you expect, you can inject `$corvusTypeName` into the schema to provide a hint to the generator.
  If you  hit one of these cases, please [open an issue in github](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues).

- We now generate fewer files for each type. You should delete your previous generated files before running the new version of the generator, to avoid leaving duplicate partial definitions.

### Breaking changes
- .NET 6 and .NET 7 are now out-of-support. We no longer support these versions. The `netstandard2.0` builds will fail at runtime.

- - We no longer generate the property 'default' accessors.

  Prior to V4 we emitted methods like `TryGetDefault(in JsonPropertyName name, out JsonAny value)` on objects whose properties had types with default values.

  This was somewhat redundant code, as
  a) it lacked strong typing and
  b) it had unncessary overhead - you could go directly to the property type to get the default value, rather than doing a lookup by the property name.

  If you want to discover the default value for a property, you must now do so by inspecting the `Default` static property of its type.
  If you are affected by this change, you can copy the `[typename].Default.cs` file for the relevant type from your V3 code base to provide the capability.

  However, we recommend refactoring to use the static `Default` property on the property type instead.

## V3.0 Updates

The big change with v3.0 is support for older (supported) versions of .NET, including the .NET Framework, through netstandard2.0.

As of v3.0.23 we also support draft4 and OpenAPI3.0 schema.

Additional changes include:

- Pattern matching methods for anyOf, oneOf and enum types.
- Implicit cast to bool for boolean types
- Specify an explicit type name hint for a schema with the $corvusTypeName keyword
- Improved heuristic for type naming based on `title` and `documentation` as fallbacks if no better name can be dervied.

## V2.0 Updates

There have been considerable breaking changes with V2.0 of the generator. This section will help you understand what has changed, and how to update your code.

### Json Schema Models

The JSON Schema Models have been broken out into separate projects.

  - Corvus.Json.JsonSchema.Draft6
  - Corvus.Json.JsonSchema.Draft7
  - Corvus.Json.JsonSchema.Draft201909
  - Corvus.Json.JsonSchema.Draft202012

### Code Generation

### Property Names

The static values for JSON Property Names have been moved from the root type, to a nested subtype called `PropertyNamesEntity`

### Conversions and operators

The implicit/explicit conversions and operators have been rationalised. More explicit conversions are required, at the expense of the implicit conversions.

However, most implicit conversions from/to intrinsic types are still supported.

One significant change is that there is *no* implicit conversion to `string` - this must be done explicitly, or directly through one of the comparison functions like `EqualsString()` or `EqualsUtf8String()`. This is to prevent a common source of accidental allocations and the corresponding performance hit.

## System.Text.Json support by other projects

There is a thriving ecosystem of System.Text.Json-based projects out there.

In particular I would point you at

[JsonEverything](https://github.com/gregsdennis/json-everything) by [@gregsdennis](https://github.com/gregsdennis)

- JSON Schema, drafts 6 and higher ([Specification](https://json-schema.org))
- JSON Path ([RFC in progress](https://github.com/ietf-wg-jsonpath/draft-ietf-jsonpath-jsonpath)) (.NET Standard 2.1)
- JSON Patch ([RFC 6902](https://tools.ietf.org/html/rfc6902))
- JsonLogic ([Website](https://jsonlogic.com)) (.NET Standard 2.1)
- JSON Pointer ([RFC 6901](https://tools.ietf.org/html/rfc6901))
- Relative JSON Pointer ([Specification](https://tools.ietf.org/id/draft-handrews-relative-json-pointer-00.html))
- Json.More.Net (Useful System.Text.Json extensions)
- Yaml2JsonNode

[JsonCons.Net](https://github.com/danielaparker/JsonCons.Net) by [@danielParker](https://github.com/danielaparker)

- JSON Pointer
- JSON Patch
- JSON Merge Patch
- JSON Path
- JMES Path
