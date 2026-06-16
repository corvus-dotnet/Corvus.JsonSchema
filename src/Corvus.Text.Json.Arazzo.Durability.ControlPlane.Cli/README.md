# arazzo-runs

`arazzo-runs` is a command-line client for the Arazzo durability **control plane** (plan §11). It is generated
from the same OpenAPI 3.2 contract as the server
([`docs/control-plane/arazzo-control-plane.openapi.json`](../../docs/control-plane/arazzo-control-plane.openapi.json))
using this repo's `openapi-client` generator (output under `Generated/`), and drives it over HTTP via
`HttpClientTransport`. Built on [Spectre.Console.Cli](https://spectreconsole.net/cli/); results print as JSON
(pipe to `jq`), and errors print the RFC 9457 problem to stderr with a non-zero exit code.

## Usage

```
arazzo-runs <command> [args] --server <url> [--token <bearer>]
```

| Command | Description |
|---------|-------------|
| `list [--status <s>] [--workflow-id <id>] [--limit <n>] [--page-token <t>] [--output table\|json]` | List runs (default a table; `--output json` for piping). Follow `nextPageToken` to page. |
| `get <runId>` | Show a run's management detail. |
| `resume <runId> --mode <mode> …` | Resume a faulted run. `--mode` is `RetryFaultedStep` (default), `Rewind` (`--target-cursor`), `Skip` (`--target-cursor`, `--skip-outputs-file <path>`), or `StatePatch` (`--patch-file <path>`, validated against RFC 6902 before sending). JSON-valued inputs are read from files, not passed on the command line. |
| `cancel <runId> --reason <text>` | Cancel a non-terminal run. |
| `delete <runId>` | Permanently delete a single run. |
| `purge --older-than <rfc3339> [--limit <n>]` | Reap old terminal runs in bulk. |
| `login [--use-device-code]` | Sign in interactively and cache an access token. |
| `logout` | Remove the cached access token. |

`--server` is the control plane's **base origin** (e.g. `https://host:8080`); the generated request paths are
absolute (`/runs`). `--server`/`--token` may also come from `ARAZZO_RUNS_SERVER` / `ARAZZO_RUNS_TOKEN`.

The runs commands above sit at the top level; the other control-plane resources are grouped under noun branches
(run `arazzo-runs <group> --help` for each):

| Group | Purpose |
|-------|---------|
| `catalog` | Pack/verify workflow packages and manage catalogued versions (governance, status). |
| `security` | Author the row-security policy: `rule` and claim→rule `binding` subcommands. |
| `credentials` | Manage source credential bindings — **references and non-secret metadata only, never secret material**. `list` is a status-first table (`--status`/`--source`); `update` is a merge (re-point a `--ref` to rotate; unspecified fields are preserved). |
| `administrators` | Manage a workflow's administrator set (`list`/`add`/`remove`/`transfer`); administrators are named by the deployment-mapped grant `{dimension, value}`. |

```bash
arazzo-runs credentials list --status expiring --server https://host:8080
arazzo-runs credentials update petstore production --ref value=keyvault://petstore-key#4 --server https://host:8080
arazzo-runs administrators add billing tenant acme --server https://host:8080
```

### Examples

```bash
arazzo-runs list --status Faulted --server https://host:8080
arazzo-runs resume run-42 --mode Rewind --target-cursor 2 --server https://host:8080
arazzo-runs resume run-42 --mode StatePatch --patch-file fix.json --server https://host:8080
arazzo-runs purge --older-than 2026-01-01T00:00:00Z --server https://host:8080
```

## Authentication

For unattended use, pass a bearer token with `--token` (or `ARAZZO_RUNS_TOKEN`).

For interactive use, `arazzo-runs login` performs an OAuth2 flow against a deployment-chosen OIDC provider and
caches the resulting tokens (under the user's app-data folder), refreshing them automatically:

- **Browser loopback (default)** — Authorization Code + PKCE with a `127.0.0.1` redirect (RFC 8252); opens the
  system browser. Best when a local browser is available.
- **Device code (`--use-device-code`)** — the Device Authorization Grant (RFC 8628); prints a URL and a user
  code to enter on any device. Best for headless/SSH sessions.

Both need the provider configured via `--authority` / `ARAZZO_RUNS_AUTHORITY` (the OIDC issuer; discovery at
`{authority}/.well-known/openid-configuration`) and `--client-id` / `ARAZZO_RUNS_CLIENT_ID`. After `login`,
commands use the cached token automatically; `logout` clears it.

Token resolution order for every command: `--token` → `ARAZZO_RUNS_TOKEN` → the login cache (refreshed if
stale) → unauthenticated.

## Regenerating the client

```bash
dotnet run --project src/Corvus.Json.Cli -f net10.0 -- \
  openapi-client docs/control-plane/arazzo-control-plane.openapi.json \
  --rootNamespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client \
  --outputPath src/Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli/Generated
```
