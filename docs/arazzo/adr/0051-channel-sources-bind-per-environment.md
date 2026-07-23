# ADR 0051. Channel sources bind per environment, like HTTP sources

Date: 2026-07-23. Status: **Accepted**. Scope: how an AsyncAPI (channel) source connects to its broker and how
that connection is secured, per environment. This records why a channel source gets a real §13 credential
binding — reusing the existing auth-kind taxonomy rather than inventing a broker kind — with the environment's
broker URL as non-secret config and the transport built by a protocol-dispatched factory; and why the control
plane's system notifications API is its own source in the `system` environment rather than a namesake of the
application's KYC notifications source.

## Context

An OpenAPI source is bound per `(source, environment)`: the §13 `SourceCredentialBinding` carries the
credential reference, and the per-environment base-URL override rides the binding as a non-secret `config`
entry (`baseUrl`), resolved runner-side at bind time ([ADR 0048](0048-source-credentials-are-references.md)).
A channel source had no equivalent. The baked executor carries channel *addresses* (subjects) from the
workflow's AsyncAPI copy, but the broker *endpoint* came from host process configuration (`Nats:Url`), one
shared `NatsMessageTransport` handed to every workflow the runner executes, and `NatsTransportOptions` had no
authentication fields at all. The AsyncAPI document's `servers` section was never consulted at run time.

The consequences were structural, not cosmetic:

- **No governed environment separation for channels.** Staging's channel and production's channel were the same
  broker and the same subjects unless each environment's runner *deployment* happened to configure a different
  URL — a fact invisible to the control plane, with no readiness computation over it and no rotation story.
- **An unsecurable connection.** The transport could not authenticate even if the broker demanded it.
- **Gate/seed theater.** The promotion gates correctly demanded a binding for every referenced source, but the
  only binding kinds were HTTP-shaped — so the demo seeded a placeholder `apiKey` binding for the channel
  source (its Vault secret provisioned but never read) purely to satisfy the gate, and the debug-run gate was
  special-cased to skip non-HTTP sources.
- **A name collision hiding the model.** The access-approval system workflow's own notifications API (channels
  `accessNotify`/`accessDecisions`) was packaged under the source name `notifications` — the same name as the
  application's KYC notifications source (`kyc.requests`/`kyc.verdict`), two unrelated APIs presenting as one.

## Options

**Keep host-configured transports and filter the gates to HTTP sources.** Small: exempt channel sources from
every credential gate and delete the placeholder bindings. Rejected: it ratifies the gap — channel
infrastructure stays ungoverned per environment, cross-environment crosstalk stays possible, brokers stay
unauthenticated, and the credentials surface stays wrong-by-omission.

**Per-environment broker settings in runner configuration only.** Each environment's runner deployment carries
its own broker URL and credentials. Rejected: invisible to the control plane — no readiness gate can compute
over it, the credentials UI cannot show it, rotation has no worklist, and nothing stops two environments
sharing a broker by accident.

**A new `messageBroker` auth kind.** One enum member marks a binding as "the broker connection", with
NATS-flavoured secret slots. Rejected as a category error: `authKind` is a taxonomy of *credential shapes* (how
a secret becomes an authenticator), and "message broker" names a *transport family*, not an auth scheme. It
collapses on contact with reality — a broker is not one auth mechanism (NATS takes tokens, user/password, or
NKeys; Kafka takes SASL/PLAIN, SCRAM, OAUTHBEARER, or SSL client certs; Azure Service Bus takes SAS or Entra
client-credentials), so the single kind would either sprout per-protocol sub-shapes or privilege one broker's
mechanisms in a supposedly protocol-neutral model.

**A first-class channel binding reusing the auth-kind taxonomy (chosen).** The §13 binding slot a channel
source already occupies carries a real credential in an existing shape, the environment's broker URL as
non-secret config, and the transport is built by a factory dispatched on the *protocol the AsyncAPI document
already declares*.

## Decision

- **A channel source binds per `(source, environment)` with the same `authKind` taxonomy, read as credential
  shapes.** A NATS token or Service Bus SAS is `bearer` (one opaque secret presented at connect); NATS
  user/password and Kafka SASL/PLAIN or SCRAM are `basic` (mechanism selection such as `SCRAM-SHA-256` is
  non-secret `config`); Kafka OAUTHBEARER and Entra client-credentials are `oauth2ClientCredentials`; SSL
  client certificates are `mtls`. A genuinely new credential *shape* (an NKey challenge-response, say) earns a
  new kind on those grounds — never because its target is a broker.
- **The endpoint is config, exactly as HTTP already does it.** The binding carries the environment's broker
  `serverUrl` as a non-secret `config` entry — the channel analogue of the HTTP `baseUrl` override,
  protocol-interpreted (a Kafka value may be a bootstrap list).
- **The protocol is the source document's fact, not the binding's.** The registered source's AsyncAPI
  `servers[].protocol` (`nats`, `kafka`, `amqp`, `mqtt`, …) names the transport family. The binding never
  re-states it.
- **The runner builds the message transport per `(source, environment)` through a protocol-dispatched factory
  set**, mirroring the bring-your-own `ISecretResolver` composition of ADR 0048: each protocol's factory ships
  in its own package with its own SDK (`.AddNats(…)` today; `.AddKafka(…)`, `.AddAzureServiceBus(…)` when
  needed), receives the serverUrl, auth kind, resolved secrets, and config, and builds the connection. An
  unregistered protocol or an unsupported (protocol, kind) combination fails closed. The executor descriptor
  names its channel sources exactly as it names its API sources, and transports bind per source — a workflow's
  two channel sources may live on two brokers.
- **The gates are uniform again.** Readiness — promotion and debug alike — requires a binding for *every*
  referenced source in the target environment, with no kind filtering. The debug-gate HTTP-only special case
  is reverted.
- **Correct by construction in the UI.** For an AsyncAPI source the credentials editor offers the
  connection-capable kinds and requires `serverUrl`; for an OpenAPI source it offers the HTTP kinds; the
  server enforces the same compatibility on create.
- **The system notifications API is its own source.** The access-approval workflow's channel source is renamed
  `access-notifications` (title "Access Approval Notifications") and registered and bound in the `system`
  environment alongside `controlplane`; the application's KYC channel source is renamed `kyc-notifications` —
  disentangling two unrelated APIs that shared the name `notifications`.
- **Transports gain authentication.** `NatsTransportOptions` grows token, username/password, and NKey JWT/seed
  support; the demo composition turns broker authentication on and provisions the broker credential in Vault
  like every other secret.

## Channel bindings are connection-scoped, and why that is not a new rule

Within HTTP, the model already distinguishes two attachment moments. The header kinds (`apiKey`, `bearer`,
`basic`, `oauth2ClientCredentials`) attach **per request**: the host's one `HttpClient` per source shares only
the connection pool, the credential cache resolves per `(source, environment, run-tags)`, and each run's
entitlement is checked even though every run's requests share sockets. Because the credential is presented with
each request, *which* credential gets presented can be decided per run — that is what makes usage-scoping
meaningful. `mtls` is the exception: the certificate is established at the TLS handshake — connection-time —
so there is no per-request moment to vary it, it semantically authenticates the deployment rather than a run,
and the binding is therefore connection-scoped, shared, and rejects usage tags.

Every mainstream broker protocol authenticates the way `mtls` does, not the way a bearer header does: NATS
presents its token, user/password, or NKey in the CONNECT handshake; Kafka runs SASL (PLAIN, SCRAM,
OAUTHBEARER) at connection establishment; MQTT carries credentials in CONNECT; AMQP performs SASL at connection
open. (Azure Service Bus's CBS mechanism — refreshable tokens PUT per link — is the one partial nuance, but the
client identity remains effectively connection-scoped in every SDK.) There is no per-publish moment at which a
different run could present a different credential over the same connection.

So the rule is derived, not invented: connection-scoped credentials cannot be usage-scoped (the existing
invariant, previously with `mtls` as its only occupant); all channel bindings are connection-scoped (a property
of broker protocols); therefore a channel binding rejects usage tags, whatever its kind. Caching the built
transport per `(source, environment)` is then the same efficiency choice as the shared mTLS-configured
`HttpClient` — a consequence of connection-time auth, not an independent decision.

One design door this deliberately leaves closed: usage tags on a connection-scoped binding *could* be read as a
pure access gate — "may this run use the shared pipe at all" — without varying the credential. That is coherent
and fail-closed, but it is a different meaning of usage-scoping than the HTTP header kinds have ("which
credential does this run present"), and the model already declined that reading for `mtls`. If gating access to
shared connection-scoped credentials ever matters, it should arrive as a deliberate extension covering `mtls`
and channel bindings together, not as a channel-only reinterpretation.

## Consequences

- Environment separation for channels is a governed, visible control-plane fact: staging's channel and
  production's channel are the same only if an operator binds them to the same broker on purpose.
- The auth-kind taxonomy is stable under new brokers: Kafka or Azure Service Bus arrive as transport-factory
  packages, not as credential-model changes; new kinds appear only for genuinely new credential shapes.
- Promotion readiness for a channel-using workflow is real, not seeded theater: the placeholder `apiKey`
  binding and its dead Vault secret are deleted, replaced by genuine bindings in the shapes the broker accepts.
- Rotation, expiry metadata, management reach, and the two-plane tags apply to broker credentials exactly as to
  HTTP credentials — one lifecycle, one worklist, one mental model. Usage-scoping is the one §13 feature that
  does not apply, by the connection-scoped rule above.
- Broker connections are long-lived and shared per `(source, environment)`, so binding resolution stays off the
  publish hot path; rotation invalidates the cached transport, and the next bind reconnects with the current
  secret.
- Host-level broker configuration (`Nats:Url`) remains only as the bootstrap seam for host-owned durable
  consumers, which read the same binding where one exists.
