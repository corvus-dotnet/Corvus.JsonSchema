# GitHub OAuth App: development, local testing, and deployment

How to stand up the designer's Git integration with your own GitHub OAuth App. This covers registering
an app for development, testing the flow against the local composition, and deploying the same
integration in the cloud. The design rationale lives in the
[workflow designer guide §4.7](workflow-designer.md); the short version is that the control plane
brokers a classic OAuth App (the model VS Code and the `gh` CLI use) because a user authorization is
all it needs. There is no installation step, and the signed-in user reaches whatever they can see on
GitHub. That is the right shape for this product: sources may live in any repository the user can
read, not one they control.

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

   ```bash
   cp github-oauth.local.json.example github-oauth.local.json
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

   ```bash
   curl -H "X-Api-Key: demo-admin-key" http://localhost:8090/arazzo/v1/github/session
   # {"connected":false}
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
  reached from the control plane. Check outbound TLS and any proxy. One known local cause: Aspire's
  orchestrator hands each child process an `SSL_CERT_DIR` containing only its dev certificate, which
  on Linux replaces the system CA store and breaks all outbound public TLS. The demo host repairs
  this at startup by appending `/etc/ssl/certs`; a custom host composition needs the same repair or
  its own CA configuration.
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
2. **Configure the broker** in your host composition:

   ```csharp
   var broker = new GitHubBroker(
       new HttpClient(),
       new GitHubBrokerOptions
       {
           ClientId = configuration["GitHubOAuth:ClientId"],
           ClientSecretRef = "keyvault://arazzo-kv/github-oauth-client-secret",
           CallbackUrl = "https://arazzo.example.com/arazzo/v1/github/auth/callback",
       },
       secretResolver);
   app.MapArazzoControlPlane(..., gitHubBroker: broker);
   ```

   `ClientSecretRef` is a secret REFERENCE in the same scheme set as source credentials
   (`keyvault://`, `awssm://`, `vault://`, `env://`, `file://`; see the
   [source credentials guide](source-credentials.md)). The deployment's `ISecretResolver` composes
   the stores it uses, and the secret is dereferenced only at exchange time.
3. **Token custody is in-process.** The broker holds each principal's user token in memory. A
   multi-instance deployment therefore needs session affinity for the `/github/*` surface, or a
   deployment-provided custody store; treat that as part of your scale-out design.
4. **Rotation.** Generate a new client secret on the app, update the secret in the store the
   reference points at, and recycle the host. Users' existing sessions are unaffected; new sign-ins
   exchange with the new secret. GitHub allows two active client secrets, so rotation needs no
   downtime window.
5. **GitHub Enterprise Server** is configuration, not new design: set
   `BaseUrl = "https://github.example.com"` and `ApiBaseUrl = "https://github.example.com/api/v3"`.

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
