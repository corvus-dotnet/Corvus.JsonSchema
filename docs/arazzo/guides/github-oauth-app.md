# GitHub OAuth App: development, local testing, and deployment

How to stand up the designer's Git integration with your own GitHub OAuth App. This covers registering
an app for development, testing the flow against the local composition, and deploying the same
integration in the cloud. The design rationale lives in the
[workflow designer guide §4.7](workflow-designer.md); the short version is that the control plane
brokers a classic OAuth App (the model VS Code and the `gh` CLI use) because a user authorization is
all it needs. There is no installation step, and the signed-in user reaches whatever they can see on
GitHub. That is the right shape for this product: sources may live in any repository the user can
read, not one they control.

GitHub is provider #1 of the control plane's **connected providers**
([ADR 0052](../adr/0052-source-fetch-authenticates-as-the-user.md)): the
authorize/callback/custody machinery described here is the shared `ProviderBroker`, and the same
registration, custody, and rotation guidance applies to every provider a deployment registers — an
SSO'd internal portal, the demo's own Keycloak, a partner's OIDC issuer. The
[connected-providers section below](#beyond-github-connected-providers-adr-0052) covers registering
the others; the GitHub-specific parts of this guide are the app registration on GitHub's side and
the Git panel's repos/browse surface.

## How the flow works

1. The designer's Git panel calls `POST /arazzo/v1/github/auth`. The control plane mints a single-use,
   principal-bound `state` and returns the GitHub authorize URL, which the kit opens in a popup.
2. The user approves the requested scopes (`repo read:user user:email`) on GitHub.
3. GitHub redirects the popup to the callback (`GET /arazzo/v1/github/auth/callback?code=…&state=…`).
   The callback is a top-level navigation carrying no bearer token. The state IS the authentication:
   it is unguessable, short-lived, and consumed on first use.
4. The control plane exchanges the code for a user token server-side, resolving the client secret
   through its secret resolver at that moment. The token is held server-side, keyed by control-plane
   principal. The browser never sees a GitHub credential.
5. Every subsequent Git operation (session status, browse, pull, commit, branches, history) runs on
   the calling principal's own token. Commits are authored as the signed-in user's GitHub-held git
   identity. One caller's session is unreachable from another's.

## Registering an app for development

GitHub → Settings → Developer settings → **OAuth Apps** → *New OAuth App*. One registration serves
one callback URL, so register separate apps for local development and each deployed environment.

| Field | Value for local development |
|-------|------------------------------|
| Application name | Anything you like, e.g. `arazzo-designer-dev`. Users see it on the consent page. |
| Homepage URL | `http://localhost:8090/` |
| Authorization callback URL | `http://localhost:8090/arazzo/v1/github/auth/callback` |

Leave **Enable Device Flow** unchecked. The broker uses the web application flow only (popup,
redirect, server-side exchange); device flow is for clients that cannot receive a redirect, and
enabling it would widen the app's grant surface with nothing using it.

The demo composition pins the control plane to port 8090 precisely so this callback is stable across
runs. After registering, note the **Client ID** and **generate a client secret**. The client id is
public (it rides the authorize URL). The secret is a real secret and never goes in the repo.

## Testing locally with the demo composition

1. In `samples/arazzo/Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost/`:

   ```powershell
   Copy-Item github-oauth.local.json.example github-oauth.local.json
   ```

2. Edit `github-oauth.local.json` with your app's values:

   ```json
   {
     "GitHubOAuth": {
       "ClientId": "Ov23li...",
       "ClientSecret": "..."
     }
   }
   ```

   The file is `.gitignore`d. The AppHost reads it at startup and injects the client id as
   configuration (`GitHubOAuth__ClientId`) and the secret as an environment variable the broker
   resolves via `env://GITHUB_OAUTH_CLIENT_SECRET`. The secret is never written to the repo, Vault,
   or a container image.

3. Restart the composition (the file is read at AppHost startup).

4. Verify. Before sign-in, the session endpoint reports disconnected:

   ```powershell
   (Invoke-RestMethod 'http://localhost:8090/arazzo/v1/github/session' -Headers @{ 'X-Api-Key' = 'demo-admin-key' }).connected
   # False
   ```

   In the designer, open a working copy's **Git** panel and click **Connect GitHub**. Approve the
   consent page in the popup. The session then reports your identity and a first page of your
   repositories, and the panel's pickers seed from it. Any repository you can see on GitHub is
   addressable by owner/repo whether it appears in that first page or not.

Without `github-oauth.local.json` the control plane brokers no OAuth App. The Git panel reports that
Git is unavailable and every `/github/*` operation refuses with a typed problem. Everything else
works unchanged.

## Troubleshooting

- **`github-invalid-state` (400).** The state is single-use and short-lived. A replayed or expired
  callback refuses. Begin the sign-in again from the Git panel.
- **`github-exchange-failed` (400).** GitHub refused the code exchange, or github.com could not be
  reached from the control plane. The control-plane log names the cause (GitHub's error code, the
  HTTP status, or the transport exception). One known local cause: Aspire's orchestrator hands each
  child process an `SSL_CERT_DIR` containing only its dev certificate, which on Linux replaces the
  system CA store and breaks all outbound public TLS with `PartialChain`. The fix must be applied at
  launch (the root store is read before in-process env changes can land): the demo AppHost sets
  `SSL_CERT_FILE=/etc/ssl/certs/ca-certificates.crt` on the control-plane resource, restoring the
  public roots alongside the injected dev certificate. A custom composition needs the same setting
  or its own CA configuration.
- **Organisation repositories missing.** Organisations that enable OAuth App access restrictions
  must approve the app before members' tokens reach org-private repositories (GitHub → the org →
  Settings → Third-party access). This is the OAuth model's counterpart of an app installation.
- **The popup never returns.** Check the app's callback URL exactly matches the control plane's
  externally visible `/arazzo/v1/github/auth/callback` URL, scheme and port included. GitHub
  redirects only to the registered value.

## Deploying in the cloud

The same integration, with three differences: the callback is your deployment's public URL, the
secret lives in a real secret store, and you register a distinct OAuth App per environment.

1. **Register one app per environment** (the callback URL must match exactly, and you do not want a
   staging consent page authorizing production). Callback:
   `https://<your-control-plane-host>/arazzo/v1/github/auth/callback`.
2. **Configure the brokers** in your host composition. The GitHub App registration folds into the
   shared connected-provider registry as its `github` entry, and the `GitHubBroker` (the Git
   panel's repos/browse surface) rides that shared machinery:

   ```csharp
   var gitHubOptions = new GitHubBrokerOptions
   {
       ClientId = configuration["GitHubOAuth:ClientId"],
       ClientSecretRef = "keyvault://arazzo-kv/github-oauth-client-secret",
       CallbackUrl = "https://arazzo.example.com/arazzo/v1/github/auth/callback",
   };
   var httpClient = new HttpClient();
   var providers = new ProviderBroker(
       httpClient,
       [gitHubOptions.ToProviderEntry() /*, …any other connected providers (ADR 0052) */],
       secretResolver);
   var broker = new GitHubBroker(httpClient, gitHubOptions, providers);
   app.MapArazzoControlPlane(..., gitHubBroker: broker, providerBroker: providers);
   ```

   `ClientSecretRef` is a secret REFERENCE in the same scheme set as source credentials
   (`keyvault://`, `awssm://`, `vault://`, `env://`, `file://`; see the
   [source credentials guide](source-credentials.md)). The deployment's `ISecretResolver` composes
   the stores it uses, and the secret is dereferenced only at exchange time.
3. **Token custody is in-process.** The shared provider broker holds each principal's user token in
   memory, keyed by `(principal, provider)` — one GitHub sign-in serves the Git panel and the
   source-fetch pane alike. A multi-instance deployment therefore needs session affinity for the
   `/github/*` and `/providers/*` surfaces, or a deployment-provided `IProviderTokenStore`; treat
   that as part of your scale-out design.
4. **Rotation.** Generate a new client secret on the app, update the secret in the store the
   reference points at, and recycle the host. Users' existing sessions are unaffected; new sign-ins
   exchange with the new secret. GitHub allows two active client secrets, so rotation needs no
   downtime window.
5. **GitHub Enterprise Server** is configuration, not new design: set
   `BaseUrl = "https://github.example.com"` and `ApiBaseUrl = "https://github.example.com/api/v3"`.

## Beyond GitHub: connected providers (ADR 0052)

Adding an SSO'd portal is configuration, not code: one `ConnectedProviderOptions` entry in the same
registry the GitHub App folds into. The fetch pane resolves a pasted URL's host against each entry's
`hosts` patterns to offer **Connect**, the sign-in is the same brokered popup, and the server only
attaches a user's token to a host the provider covers.

```csharp
var providers = new ProviderBroker(
    httpClient,
    [
        gitHubOptions.ToProviderEntry(),
        new ConnectedProviderOptions
        {
            Name = "portal",
            DisplayName = "Developer Portal",
            Issuer = "https://sso.example.com/realms/engineering",   // endpoints via OIDC discovery
            ClientId = configuration["Portal:ClientId"],
            ClientSecretRef = "keyvault://arazzo-kv/portal-oauth-client-secret",
            Scopes = "openid profile",
            CallbackUrl = "https://arazzo.example.com/arazzo/v1/providers/portal/auth/callback",
            Hosts = ["portal.example.com", "*.docs.example.com"],
        },
    ],
    secretResolver);
```

- **Endpoints.** An OIDC `Issuer` resolves the authorize/token endpoints from its discovery
  document (cached). A provider without discovery configures `AuthorizeEndpoint` + `TokenEndpoint`
  explicitly instead — exactly one of the two forms.
- **The callback is per provider**: `/arazzo/v1/providers/{name}/auth/callback`, registered
  verbatim as a redirect URI on the provider-side client. The exchange is a standard
  authorization-code grant (`grant_type` + `redirect_uri`), which Keycloak and every
  spec-compliant issuer require and GitHub tolerates.
- **Host coverage** (`Hosts`, exact names or `*.suffix`) is the gate that keeps a principal's
  token from riding to an arbitrary URL; a fetch against an uncovered host refuses with
  `provider-host-not-covered` and the pane falls back to a one-shot secret or a workload binding.
- **Registration, custody, and rotation** follow this guide's GitHub guidance unchanged: the
  client id is public configuration, the secret is a reference resolved only at exchange time,
  custody is in-process per `(principal, provider)`, and rotation is a store update plus a host
  recycle.

The demo composition registers its own Keycloak this way (`arazzo-portal` in the realm import) over
a secured sample spec endpoint (`/portal/specs/petstore.json`), so the interactive flow is
live-testable with the seeded realm users and no external account.

## Options reference

| Option | Meaning | Default |
|--------|---------|---------|
| `ClientId` | The OAuth App's client id (public; rides the authorize URL). | required |
| `ClientSecretRef` | Secret reference the resolver dereferences at exchange time. The secret itself is never configured or held. | required |
| `CallbackUrl` | The externally visible `/arazzo/v1/github/auth/callback` URL, exactly as registered on the app. | required |
| `BaseUrl` | The GitHub web origin. | `https://github.com` |
| `ApiBaseUrl` | The GitHub API origin. | `https://api.github.com` |

## See also

- The [workflow designer guide §4.7](workflow-designer.md) for the model's rationale and the
  ratified identity rules (attribution, custody, no impersonation).
- The [AppHost README](../../../samples/arazzo/Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost/README.md)
  for the demo composition's wiring of the same configuration.
