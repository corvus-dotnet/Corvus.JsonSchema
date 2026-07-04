# OpenAPI client authentication

> This is the authentication companion to the [OpenAPI client guide](./openapi.md) — start there
> for generating and consuming the client, then use this reference to wire a token provider, a signing
> scheme, or session cookies onto the transport.

Almost every provider below ends the same way: it hands you a bearer JWT, and you feed that to
`bearerToken(...)`. The differences are in how you *acquire* the token, so each subsection is a small
acquisition snippet dropped into the same canonical wiring.

## How it works

The transport takes an optional `AuthenticationProvider`. The runtime runs it **once per logical
`send`** — after the wire request has been composed (URL, headers, body) and **before** any middleware
or the network dispatch. An async `TokenFactory` therefore runs once per `send` (so a token refreshed
between calls is used on the next one); retries within a send reuse the credential applied at the top:

```typescript
import { FetchApiTransport, bearerToken } from "@endjin/corvus-json-client-runtime";
import { ApiStatusClient } from "./api/ApiStatusClient.js";

const transport = new FetchApiTransport({
  baseUrl: "https://api.example.com/v1",
  authenticationProvider: bearerToken(async (signal) => await getAccessToken(signal)),
});
const client = new ApiStatusClient(transport);
```

An `AuthenticationProvider` is just a request mutator:

```typescript
interface AuthenticationProvider {
  authenticate(request: WireRequest, signal: AbortSignal): void | Promise<void>;
}
```

It mutates the composed `WireRequest` in place — set a header (`request.headers.set(...)`), append a
query parameter (`request.url`), or add a `Cookie` header. That is all a built-in provider does, and it
is the seam you implement directly for request signing (see [Request signing](#request-signing)). The
`signal` is the request's `AbortSignal`, so an async token fetch cancels with the request.

**Redirects strip credentials cross-origin.** When you enable `redirectHandler` (or use the browser's
own redirect following), the runtime removes the `Authorization` header on a redirect to a different
origin, so a token minted for your API is never leaked to a redirect target.

**The `fetchImpl` seam.** For auth mechanisms that live *below* the request (session cookies, mTLS,
a custom `undici` dispatcher) there is no header to set — you override the transport's `fetch`:

```typescript
const transport = new FetchApiTransport({
  baseUrl: "https://api.example.com/v1",
  fetchImpl: (input, init) => fetch(input, { ...init, credentials: "include" }),
});
```

**Scope constants come from the spec.** For an OpenAPI 3.0, 3.1, or 3.2 spec that declares
`securitySchemes`, the generated client exposes a `static readonly securitySchemes` (token/authorization
URLs, available scopes, API-key name and location) and a `static readonly securityRequirements`
(per-operation and unioned OAuth2 scopes) block, both `as const`. Read scopes from these rather than
hardcoding them, so a spec change flows through to your token requests.

```typescript
ApiStatusClient.securitySchemes.oauth2TokenUrl;          // "https://auth.example.com/token"
ApiStatusClient.securitySchemes.oauth2AvailableScopes;   // ["read:pets", "write:pets"]
ApiStatusClient.securityRequirements.allOauth2Scopes;    // ["read:pets", "write:pets"]
ApiStatusClient.securityRequirements.updatePetOauth2Scopes; // ["write:pets"]
```

## Built-in providers

Three providers cover the OpenAPI `http` (bearer / basic) and `apiKey` schemes directly:

```typescript
import { bearerToken, apiKey, basicAuth } from "@endjin/corvus-json-client-runtime";

// Bearer — a static token, or a factory refreshed once per send:
bearerToken("eyJ…");
bearerToken(async (signal) => await getFreshToken(signal));

// API key — in a header, query parameter, or cookie:
apiKey("secret", "X-Api-Key", "header");
apiKey("secret", "api_key", "query");

// HTTP Basic:
basicAuth("user", "pass");
```

For an `apiKey` scheme the generated `securitySchemes` block carries the name and location straight from
the spec, so you can keep those in sync too:

```typescript
apiKey(
  process.env.API_KEY!,
  ApiStatusClient.securitySchemes.apiKeyKeyName,      // "X-API-Key"
  ApiStatusClient.securitySchemes.apiKeyKeyLocation,  // "header"
);
```

## Scopes from the spec

When the token source lets you specify scopes, take them from the generated union so a spec change can
never silently drop one:

```typescript
import { bearerToken } from "@endjin/corvus-json-client-runtime";
import { ApiStatusClient } from "./api/ApiStatusClient.js";

const provider = bearerToken(async (signal) =>
  (await cred.getToken(ApiStatusClient.securityRequirements.allOauth2Scopes, { abortSignal: signal })).token,
);
```

Use `securityRequirements.{operation}Oauth2Scopes` when you want a minimal, per-operation token instead
of the union.

## Browser / SPA providers

These are browser SDKs — they run the interactive/silent flow and hand you a token to wrap. In every
case, request the **access-token scopes your API validates**, not the OIDC `openid`/`profile` scopes
(those yield an ID token, which is for identity, not for calling an API). Where the token is an ID token
(Firebase, optionally Cognito), only send it if your API is designed to verify ID tokens.

### Microsoft Entra ID — MSAL.js

`@azure/msal-browser`. Silent first, interactive on `InteractionRequiredAuthError`:

```typescript
import { PublicClientApplication, InteractionRequiredAuthError } from "@azure/msal-browser";

const pca = new PublicClientApplication({
  auth: { clientId: "<client-id>", authority: "https://login.microsoftonline.com/<tenant>" },
});
await pca.initialize();                               // required in msal-browser v5
const scopes = ["api://<app-id-uri>/access_as_user"]; // API scopes, NOT ".default" for a delegated flow

bearerToken(async () => {
  const account = pca.getActiveAccount() ?? pca.getAllAccounts()[0];
  try {
    return (await pca.acquireTokenSilent({ scopes, account })).accessToken;
  } catch (e) {
    if (e instanceof InteractionRequiredAuthError) return (await pca.acquireTokenPopup({ scopes })).accessToken;
    throw e;
  }
});
```

`acquireTokenSilent` reads from cache / a hidden iframe, so it is cheap to call per request. Request
access-token scopes (`api://…/scope`) that match the spec's OAuth2 flow — `openid`/`profile` return an
ID token, which is not for calling APIs.

Targets `@azure/msal-browser` v5 — call `await pca.initialize()` before the first token call. See the
[MSAL.js browser docs](https://learn.microsoft.com/entra/msal/javascript/browser/about-msal-browser).

### Auth0 — auth0-spa-js

`@auth0/auth0-spa-js`. The **`audience` is mandatory** to get a real JWT access token for your API:

```typescript
import { createAuth0Client } from "@auth0/auth0-spa-js";

const auth0 = await createAuth0Client({
  domain: "<tenant>.auth0.com",
  clientId: "<client-id>",
  authorizationParams: { audience: "https://api.example.com", redirect_uri: window.location.origin },
});

bearerToken(() =>
  auth0.getTokenSilently({ authorizationParams: { audience: "https://api.example.com", scope: "read:pets" } }),
);
```

Without an `audience`, Auth0 returns an opaque token only usable against `/userinfo`.
`getTokenSilently` throws `login_required` / `consent_required` when interaction is needed — catch those
and call `loginWithRedirect` / `loginWithPopup`.

Targets `@auth0/auth0-spa-js` v2 (the current major) — the `createAuth0Client` factory and
`authorizationParams` shape shown are v2. See the [Auth0 SPA SDK docs](https://auth0.com/docs/libraries/auth0-single-page-app-sdk).

### Okta — okta-auth-js

`@okta/okta-auth-js`. Use the async, refresh-aware `getOrRenewAccessToken()`, **not** the synchronous
`getAccessToken()` (which returns the cached string without renewing):

```typescript
import { OktaAuth } from "@okta/okta-auth-js";

const oktaAuth = new OktaAuth({
  issuer: "https://<org>.okta.com/oauth2/default",
  clientId: "<client-id>",
  redirectUri: window.location.origin + "/callback",
  scopes: ["openid", "profile", "pets:read"],
  tokenManager: { autoRenew: true },
});

bearerToken(async () => (await oktaAuth.getOrRenewAccessToken()) ?? "");
```

Ensure the API scope (`pets:read`) is in `scopes` and matches the spec. With `autoRenew: true` the token
manager renews shortly before expiry. (Pin your `@okta/okta-auth-js` version and check its typings — the
synchronous-vs-async getter split is easy to get wrong.)

Targets `@okta/okta-auth-js` v8 (the current major), where `getOrRenewAccessToken` is present. The
synchronous `getAccessToken()` vs async `getOrRenewAccessToken()` distinction is the easy trap — call the
async one so the token is renewed rather than returned stale. See the [Okta Auth JS docs](https://developer.okta.com/docs/guides/auth-js/main/).

### Firebase Authentication — firebase/auth

`firebase` (modular `firebase/auth`). Firebase gives you an **ID token**, not an OAuth access token:

```typescript
import { getAuth, getIdToken } from "firebase/auth";

const auth = getAuth();
bearerToken(async () => {
  const user = auth.currentUser;
  if (!user) throw new Error("not signed in");
  return getIdToken(user);            // getIdToken(user, /* forceRefresh */ true) to force
});
```

Only appropriate if your API verifies Firebase ID tokens (there are no OAuth scopes). ID tokens expire
in ~1 hour; `getIdToken(user)` auto-refreshes near expiry, or pass `true` to force. The server must
verify with the Firebase Admin SDK / JWKS.

Targets `firebase` v12 (the current major), using the modular `firebase/auth` imports shown (the
namespaced `firebase.auth()` API was removed after v8). See the [Firebase JS SDK setup docs](https://firebase.google.com/docs/web/setup).

### AWS Cognito — aws-amplify/auth

`aws-amplify` (import from `aws-amplify/auth`, Amplify v6 / Gen 2). Choose the **access** or **ID** token
per what your API's authorizer expects:

```typescript
import { fetchAuthSession } from "aws-amplify/auth";

bearerToken(async () => {
  const { tokens } = await fetchAuthSession();     // { forceRefresh: true } to force
  return tokens?.accessToken?.toString() ?? "";    // or tokens?.idToken?.toString()
});
```

Amplify v6 renamed the whole auth surface — it is `fetchAuthSession()` from `aws-amplify/auth`, not v5's
`Auth.currentSession()`. A Cognito **access token** carries `scope` (API Gateway / custom scopes); the
**ID token** carries user-identity claims. `fetchAuthSession()` auto-refreshes when expired if a refresh
token exists.

Targets `aws-amplify` v6 (Gen 2, the current major): the v5→v6 rename replaced `Auth.currentSession()`
with the functional `fetchAuthSession()` shown, so a v5 project needs the [migration guide](https://docs.amplify.aws/gen1/javascript/build-a-backend/auth/auth-migration-guide/).
See the [Amplify auth docs](https://docs.amplify.aws/).

### Google Identity Services (GIS)

The GIS script (`https://accounts.google.com/gsi/client`). `initTokenClient` is **callback-based**, so
wrap it in a promise:

```typescript
// <script src="https://accounts.google.com/gsi/client" async></script>
bearerToken(
  () =>
    new Promise<string>((resolve, reject) => {
      const tokenClient = google.accounts.oauth2.initTokenClient({
        client_id: "<client-id>.apps.googleusercontent.com",
        scope: "https://www.googleapis.com/auth/drive.readonly",
        callback: (r) => (r.error ? reject(r) : resolve(r.access_token)),
      });
      tokenClient.requestAccessToken();   // may show a consent popup on first use
    }),
);
```

The GIS **token model** yields a Google-API **access token** (~1 hour, no browser refresh token) — good
for calling Google APIs, and for your own API only if it accepts Google access tokens. The callback
fires on every `requestAccessToken()` and the first call may prompt for consent, so don't call it
outside a user gesture expecting silence.

Loaded from the versionless GIS script (`https://accounts.google.com/gsi/client`) — there is no npm
package to pin, but the `google.accounts.oauth2` token-model API shown replaced the retired
`gapi.auth2`/Google Sign-In JS library, so older `gapi`-based snippets no longer apply. See the
[GIS token-model guide](https://developers.google.com/identity/oauth2/web/guides/use-token-model).

### Clerk — @clerk/clerk-js

`@clerk/clerk-js`. `session.getToken()` returns a short-lived, self-caching session JWT:

```typescript
import { Clerk } from "@clerk/clerk-js";

const clerk = new Clerk("<publishable-key>");
await clerk.load();

bearerToken(async () => (await clerk.session?.getToken()) ?? "");
// getToken({ template: "my-api" }) mints a JWT shaped for a specific downstream API.
```

`getToken()` caches and refreshes in the background, so `await` it fresh per request rather than caching
it yourself. `clerk.session` is `null` until `await clerk.load()` completes and a user is signed in — the
`?.` returns `undefined` when signed out, which the `?? ""` handles. In v6, `getToken()` now **throws** a
`ClerkOfflineError` when the device is offline (rather than returning `null`), so wrap the call if you
need to tolerate that.

Targets `@clerk/clerk-js` v6 ("Core 3", the current major): the `new Clerk(publishableKey)` +
`await clerk.load()` + `session.getToken()` shape shown is v6. See the [Clerk docs](https://clerk.com/docs).

### Supabase — @supabase/supabase-js

`@supabase/supabase-js`. `auth.getSession()` returns `access_token` (a JWT), refreshing if needed:

```typescript
import { createClient } from "@supabase/supabase-js";

const supabase = createClient("<project-url>", "<anon-key>");

bearerToken(async () => {
  const { data } = await supabase.auth.getSession();
  return data.session?.access_token ?? "";
});
```

For a pure browser SPA calling your own API, `getSession().access_token` is the intended path. Supabase
warns the stored session may be inauthentic in cookie-based/server contexts — use `getUser()` (which
revalidates with the server) when authenticity matters.

Targets `@supabase/supabase-js` v2 (the current major) — the async `auth.getSession()`/`getUser()`
methods shown are v2. See the [Supabase getSession docs](https://supabase.com/docs/reference/javascript/auth-getsession).

### Session cookies

Cookie-based sessions (a same-site API, or auth handled by a reverse proxy) aren't a token provider —
skip `authenticationProvider` entirely and send credentials via `fetchImpl`:

```typescript
const transport = new FetchApiTransport({
  baseUrl: "https://api.example.com/v1",
  fetchImpl: (input, init) => fetch(input, { ...init, credentials: "include" }),
});
const client = new ApiStatusClient(transport);
```

Cross-origin cookies require the server to send `Access-Control-Allow-Credentials: true` **and** a
specific `Access-Control-Allow-Origin` (not `*`), and the cookie needs `SameSite=None; Secure` to travel
cross-site. Cookie auth also needs CSRF defence (an anti-forgery token, or an `Origin` / `Sec-Fetch-Site`
check server-side) that bearer flows don't. Same-site deployments are far simpler.

## Node / server providers

On a server the credential libraries cache and refresh internally, so calling their token accessor per
request (inside the `bearerToken` factory) is cheap — don't re-create the credential object per request.

### Azure Identity (incl. Managed Identity)

`@azure/identity`. `DefaultAzureCredential` falls back through Managed Identity, environment, and CLI
credentials; the SDK caches and refreshes, so calling `getToken` per request is fine:

```typescript
import { DefaultAzureCredential } from "@azure/identity";
import { FetchApiTransport, bearerToken } from "@endjin/corvus-json-client-runtime";

const cred = new DefaultAzureCredential();            // or ManagedIdentityCredential / ClientSecretCredential(...)
const scope = "api://<app-id>/.default";

const transport = new FetchApiTransport({
  baseUrl: "https://api.example.com/v1",
  authenticationProvider: bearerToken(async (signal) => {
    const t = await cred.getToken(scope, { abortSignal: signal });
    if (!t) throw new Error("No Azure token");
    return t.token;
  }),
});
```

Managed Identity works only on Azure hosts that inject it (App Service, Functions, Container Apps, AKS
with workload identity, VMs) — locally `DefaultAzureCredential` uses env / CLI / VS credentials. Use the
resource's `/.default` scope for an app-only token; a bare resource scope throws `AADSTS70011`.

Targets `@azure/identity` v4 (the current major): `getToken` returns `AccessToken | null` with
`expiresOnTimestamp`. See the [Azure Identity for JavaScript docs](https://learn.microsoft.com/javascript/api/overview/azure/identity-readme).

### Google Auth Library

`google-auth-library`. Application Default Credentials come from the environment or the GCP metadata
server. For a Google **access token**:

```typescript
import { GoogleAuth } from "google-auth-library";
import { bearerToken } from "@endjin/corvus-json-client-runtime";

const auth = new GoogleAuth({ scopes: "https://www.googleapis.com/auth/cloud-platform" });
bearerToken(async () => {
  const { token } = await auth.getAccessToken();      // v10 returns { token }
  if (!token) throw new Error("No Google access token");
  return token;
});
```

For a private Cloud Run service or an IAP-protected resource you need an **ID token** whose `aud` is the
service URL / IAP client ID:

```typescript
const audience = "https://my-svc-xxxx-uc.a.run.app"; // Cloud Run URL, or the IAP OAuth client ID
const auth = new GoogleAuth();
const idClient = await auth.getIdTokenClient(audience);
bearerToken(() => idClient.idTokenProvider.fetchIdToken(audience)); // the client caches until near expiry
```

Access token vs ID token is the trap: Google APIs want the access token; Cloud Run / IAP want the ID
token. On GCP hosts ADC comes from the metadata server; locally set `GOOGLE_APPLICATION_CREDENTIALS` or
`gcloud auth application-default login`.

Targets `google-auth-library` v10 (the current major): `auth.getAccessToken()` returns `{ token }` (read
`.token`, as shown). See the [google-auth-library docs](https://github.com/googleapis/google-auth-library-nodejs)
and the [Cloud Run service-to-service auth guide](https://cloud.google.com/run/docs/authenticating/service-to-service).

### Generic OAuth2 client-credentials

No SDK required — POST to the token endpoint with `fetch`, cache the token until shortly before it
expires, and read the endpoint and scopes from the generated constants:

```typescript
import { bearerToken } from "@endjin/corvus-json-client-runtime";
import { ApiStatusClient } from "./api/ApiStatusClient.js";

const tokenUrl = ApiStatusClient.securitySchemes.oauth2TokenUrl;               // the spec's tokenUrl
const scope = ApiStatusClient.securityRequirements.allOauth2Scopes.join(" ");  // space-separated
let cache: { token: string; exp: number } | undefined;

const provider = bearerToken(async (signal) => {
  if (cache && Date.now() < cache.exp) return cache.token;
  const body = new URLSearchParams({
    grant_type: "client_credentials", scope,
    client_id: process.env.CLIENT_ID!, client_secret: process.env.CLIENT_SECRET!,
  });
  const res = await fetch(tokenUrl, {
    method: "POST", body, signal,
    headers: { "content-type": "application/x-www-form-urlencoded" },
  });
  if (!res.ok) throw new Error(`token endpoint ${res.status}`);
  const j = (await res.json()) as { access_token: string; expires_in: number };
  cache = { token: j.access_token, exp: Date.now() + (j.expires_in - 60) * 1000 }; // 60s skew buffer
  return cache.token;
});
```

Cache until shortly *before* `expires_in` (a 30–60 s skew buffer) — a per-request POST to the IdP will
rate-limit you. Scopes are a **space-separated** string in the form body. For multi-tenant use, key the
cache by `(clientId, scope)` and guard against a concurrent-refresh stampede.

No SDK to version — the request body follows the OAuth 2.0 client-credentials grant. See
[RFC 6749 §4.4](https://datatracker.ietf.org/doc/html/rfc6749#section-4.4) for the request/response shape.

### mTLS (client certificate)

There is no header to set, so mTLS goes through `fetchImpl` with an `undici` `Agent`, or through the
Node transport with an `https.Agent`.

Option A — `undici` `Agent` via `fetchImpl`:

```typescript
import { Agent, fetch as undiciFetch } from "undici";
import { readFileSync } from "node:fs";
import { FetchApiTransport } from "@endjin/corvus-json-client-runtime";

const agent = new Agent({ connect: {
  cert: readFileSync("client.crt", "utf8"),
  key: readFileSync("client.key", "utf8"),
  // ca: readFileSync("ca.crt", "utf8"),   // custom server CA if needed
  rejectUnauthorized: true,
} });

const transport = new FetchApiTransport({
  baseUrl: "https://mtls.example.com",
  // undici's fetch accepts a per-request `dispatcher`; the DOM RequestInit type omits it, so cast.
  fetchImpl: ((input, init) =>
    undiciFetch(input as any, { ...(init as any), dispatcher: agent })) as unknown as typeof fetch,
});
```

Option B — `NodeApiTransport` with an `https.Agent` (no undici dependency):

```typescript
import { Agent } from "node:https";
import { readFileSync } from "node:fs";
import { NodeApiTransport } from "@endjin/corvus-json-client-runtime/node";

const httpsAgent = new Agent({ cert: readFileSync("client.crt"), key: readFileSync("client.key") });
const transport = new NodeApiTransport({ baseUrl: "https://mtls.example.com", httpsAgent });
```

Import `fetch` and `Agent` from the **same `undici` you installed** rather than mixing the global
`fetch` with a standalone-undici `Agent` — undici 8 changed the dispatcher interface, so a per-request
`dispatcher` from a standalone `undici` is incompatible with a `fetch` backed by a different undici.
Option B avoids undici entirely. Combine mTLS with a token provider by passing `authenticationProvider`
alongside the transport.

Option A targets `undici` v8 (the current major): its `dispatcher` interface differs from the undici
built into Node's global `fetch`, so the standalone `undici` you import must match the one backing your
`fetch`. Option B uses only the built-in `node:https` `Agent`. See the [undici `Agent` docs](https://github.com/nodejs/undici/blob/main/docs/docs/api/Agent.md) and
[Node.js `https.Agent` docs](https://nodejs.org/api/https.html#class-httpsagent).

## Request signing

When the credential depends on the request itself — the method, path, query, and body — implement an
`AuthenticationProvider` directly and mutate the composed `WireRequest`. The runtime buffers `bytes`
bodies before dispatch, so the body is available to hash (a `stream` body cannot be signed as-is).

### AWS SigV4

`@smithy/signature-v4` + `@aws-crypto/sha256-js` + `@smithy/protocol-http` +
`@aws-sdk/credential-providers`. SigV4 signs the method, host, path, query, headers, **and** body, so it
must run in the provider where those are known:

```typescript
import { SignatureV4 } from "@smithy/signature-v4";
import { Sha256 } from "@aws-crypto/sha256-js";
import { HttpRequest } from "@smithy/protocol-http";
import { fromNodeProviderChain } from "@aws-sdk/credential-providers";
import type { AuthenticationProvider } from "@endjin/corvus-json-client-runtime";

const signer = new SignatureV4({
  service: "execute-api", region: "eu-west-1",
  credentials: fromNodeProviderChain(), sha256: Sha256,
});

const sigv4Provider: AuthenticationProvider = {
  async authenticate(request) {
    const u = new URL(request.url);
    const query = Object.fromEntries(u.searchParams);         // SigV4 signs the query too
    const bodyBytes = request.body.kind === "bytes" ? request.body.content : undefined;
    const signed = await signer.sign(new HttpRequest({
      method: request.method, protocol: u.protocol, hostname: u.hostname,
      path: u.pathname, query,
      headers: { host: u.host, ...Object.fromEntries(request.headers) },   // host header is mandatory
      body: bodyBytes,
    }));
    for (const [k, v] of Object.entries(signed.headers)) request.headers.set(k, v as string);
  },
};
```

The `host` header is **mandatory** (a missing one is a 403) and you must sign the query string, not just
the path. Signatures are time-boxed (~5 min), so **clock skew** on the host causes
`SignatureDoesNotMatch` — sync the clock (NTP). `service` is `execute-api` for API Gateway, `lambda` for
Lambda function URLs. Temporary/role credentials carry a `sessionToken`, which `fromNodeProviderChain()`
supplies automatically. Because the provider already runs once per send, the signature is never replayed
across a retry.

Package caveat: use **`@smithy/signature-v4`** v5 with `HttpRequest` from `@smithy/protocol-http` v5,
`fromNodeProviderChain` from `@aws-sdk/credential-providers` v3, and `Sha256` from `@aws-crypto/sha256-js`
v5. The older `@aws-sdk/signature-v4` and `@aws-sdk/protocol-http` packages are **deprecated** (frozen at
3.374.0) — don't use them. See the
[`@smithy/signature-v4` docs](https://docs.aws.amazon.com/AWSJavaScriptSDK/v3/latest/Package/-smithy-signature-v4/).

### HMAC (shared secret)

Node's built-in `crypto`. The **string-to-sign must match the server byte-for-byte** — canonicalize the
path/query exactly as the server does:

```typescript
import { createHmac, createHash } from "node:crypto";
import type { AuthenticationProvider } from "@endjin/corvus-json-client-runtime";

function hmacProvider(keyId: string, secret: string): AuthenticationProvider {
  return {
    authenticate(request) {
      const u = new URL(request.url);
      const ts = new Date().toISOString();
      const body = request.body.kind === "bytes" ? request.body.content : new Uint8Array();
      const bodyHash = createHash("sha256").update(body).digest("hex");
      const stringToSign = `${request.method}\n${u.pathname}${u.search}\n${ts}\n${bodyHash}`;
      const sig = createHmac("sha256", secret).update(stringToSign).digest("base64");
      request.headers.set("X-Date", ts);
      request.headers.set("Authorization", `HMAC ${keyId}:${sig}`);
    },
  };
}
```

Include the timestamp header **inside** the signature and validate a tight window server-side to defend
against replay — that makes clock skew a live failure mode. Sign the body *hash* (keeps the header
small), keep the secret out of source (env / secret store). For an edge runtime without `node:crypto`,
use `crypto.subtle.importKey(...)` + `crypto.subtle.sign("HMAC", key, ...)` and base64 the result.

No package to pin — this is Node's built-in `node:crypto` (or the platform `crypto.subtle`). See the
[`node:crypto` docs](https://nodejs.org/api/crypto.html) / [Web Crypto `subtle.sign` docs](https://developer.mozilla.org/docs/Web/API/SubtleCrypto/sign).

## See also

- [OpenAPI client generation](./openapi.md) — generating and consuming the TypeScript client.
- [The runtime](./runtime.md) — the model runtime the generated clients build on.
- [OpenAPI Code Generation (C#)](/docs/open-api.html) — the C# reference, whose `Authentication` section
  mirrors these providers with `HttpClient` / `DelegatingHandler` snippets.