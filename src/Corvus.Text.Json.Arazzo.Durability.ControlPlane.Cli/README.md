# arazzo-runs

`arazzo-runs` is a command-line client for the Arazzo durability **control plane** (plan §11). It is generated
from the same OpenAPI 3.2 contract as the server
([`docs/arazzo/reference/arazzo-control-plane.openapi.json`](../../docs/arazzo/reference/arazzo-control-plane.openapi.json))
using this repo's `openapi-client` generator (output under `Generated/`), and drives it over HTTP via
`HttpClientTransport`. Built on [Spectre.Console.Cli](https://spectreconsole.net/cli/); results print as JSON
(pipe to `jq`), and errors print the RFC 9457 problem to stderr with a non-zero exit code.

## Usage

```
arazzo-runs <command> [args] --server <url> [--token <bearer>]
```

| Command | Description |
|---------|-------------|
| `list [--status <s>] [--workflow-id <id>] [--limit <n>] [--page-token <t>] [--output table\|json]` | List runs (default a table, with a faulted run's error type in the Error column; `--output json` for piping). Follow `nextPageToken` to page. |
| `get <runId> [--output json\|detail]` | Show a run's management detail. The default is the run as the API returns it, which includes the `budget` frozen into it at start. `--output detail` lays the run out to read, with its budget, and explains a platform fault type (`budget-fuel`, `executor-unresolvable` and the rest): what happened and what to do. On a run faulted on its budget it shows whether a `resume` would run now, and the budget the resume would give it. It shows `rerunOf` on a re-run. |
| `resume <runId> --mode <mode> …` | Resume a faulted run. `--mode` is `RetryFaultedStep` (default), `Rewind` (`--target-cursor`), `Skip` (`--target-cursor`, `--skip-outputs-file <path>`), or `StatePatch` (`--patch-file <path>`, validated against RFC 6902 before sending). JSON-valued inputs are read from files, not passed on the command line. |
| `rerun <runId> [--idempotency-key <key>]` | Start a new run of the same workflow version, in the same environment, with the same inputs, which the server reads from the original. The remedy when a run cannot or should not be resumed. Prints the new run's id. |
| `cancel <runId> --reason <text>` | Cancel a non-terminal run. |
| `delete <runId>` | Permanently delete a single run. |
| `purge --older-than <rfc3339> [--limit <n>]` | Reap old terminal runs in bulk. |
| `login [--use-device-code]` | Sign in interactively and cache an access token. |
| `logout` | Remove the cached access token. |

`--server` is the control plane's **base URL** — origin plus any base path the deployment mounts the API under,
e.g. `https://host:8080` (API at the root) or `https://host/arazzo/v1`. The generated request paths are absolute
(`/runs`); the CLI prepends the `--server` base path to them, so it adapts to wherever the API is served.
`--server`/`--token` may also come from `ARAZZO_RUNS_SERVER` / `ARAZZO_RUNS_TOKEN`.

The runs commands above sit at the top level; the other control-plane resources are grouped under noun branches
(run `arazzo-runs <group> --help` for each):

| Group | Purpose |
|-------|---------|
| `catalog` | Pack/verify workflow packages and manage catalogued versions (governance, status). |
| `security` | Author the row-security policy: `rule` and claim→rule `binding` subcommands. |
| `credentials` | Manage source credential bindings — **references and non-secret metadata only, never secret material**. `list` is a status-first table (`--status`/`--source`); `update` is a merge (re-point a `--ref` to rotate; unspecified fields are preserved). |
| `environments` | Manage governed, reach-scoped deployment environments: `list`/`get`/`create`/`update`/`delete`, their `administrators`, and their execution budget. `budget <name>` shows each of the six limits as the environment's override, the budget in effect, and the deployment's ceiling. `create` and `update` take the limits as options (`--max-steps`, `--wall-clock-seconds`, `--max-sub-workflow-depth`, `--retry-after-ceiling-seconds`, `--step-timeout-seconds`, `--max-response-bytes`); `update` lays the limits named over the override already authored, and `--clear-budget` removes the override. The server refuses a limit wider than the ceiling. |
| `administrators` | Manage a workflow's administrator set (`list`/`add`/`remove`/`transfer`); administrators are named by the deployment-mapped grant `{dimension, value}`. |
| `access-requests` | Request elevated capability on a workflow, and — as a §15 administrator — decide requests (§16.5): `submit`/`list`/`get`/`approve`/`approve-as-eligible`/`deny`/`withdraw`/`revoke`. |
| `schedules` | Manage durable schedules (#896) — a cadence that starts a target workflow on each occurrence: `list`/`get`/`create`/`run-now`/`delete`. Creating one needs a runner serving schedules in the target environment. Reuses the `runs:read`/`runs:write` scopes. |
| `scenarios` | Run workflow scenario suites (workflow-designer design §4.5) — the CI story; see below. |

```bash
arazzo-runs credentials list --status expiring --server https://host:8080
arazzo-runs credentials update petstore production --ref value=keyvault://petstore-key#4 --server https://host:8080
arazzo-runs administrators add billing tenant acme --server https://host:8080
arazzo-runs environments update production --max-steps 100 --step-timeout-seconds 30 --server https://host:8080
arazzo-runs environments budget production --server https://host:8080
arazzo-runs schedules create nightly-reconcile development nightly-reconcile 2 --cron "0 3 * * *" --inputs '{"date":"2026-07-20"}' --server https://host:8080
arazzo-runs schedules run-now nightly-reconcile --server https://host:8080
```

### Examples

```bash
arazzo-runs list --status Faulted --server https://host:8080
arazzo-runs resume run-42 --mode Rewind --target-cursor 2 --server https://host:8080
arazzo-runs resume run-42 --mode StatePatch --patch-file fix.json --server https://host:8080
arazzo-runs purge --older-than 2026-01-01T00:00:00Z --server https://host:8080
```

## Scenario suites (`scenarios run`)

Runs a workflow's scenario suite (workflow-designer design §4.5) with CI-grade behaviour:
deterministic ordering, a non-zero exit on any failed expectation (1) or when the suite cannot run
(2), and console/JUnit/JSON reports — **the JSON report is the same suite-report shape `publish`
embeds as evidence**, so a pipeline can finish with publish-with-evidence.

**Standalone (default)** needs no control plane: the CLI hosts the deterministic simulator
in-process — the workflow document and its source documents compile locally, and every matched
scenario runs against the mock transport and virtual clock, exactly as the designer does
interactively. Workflows, specs, and scenarios live in the repo as globbable files
(`<name>.scenario.json`, one scenario per file):

```bash
arazzo-runs scenarios run \
  --workflow ./workflows/nightly-reconcile.arazzo.json \
  --sources ./specs \
  --scenarios "./scenarios/nightly-reconcile/**/*.scenario.json" \
  --filter "payment-*" \
  --report junit=out/scenarios.xml --report json=out/suite.json \
  --github-annotations
```

`--sources` is the directory the document's `sourceDescriptions` urls resolve against (default: the
workflow file's directory); `http(s)` urls are fetched. `--github-annotations` emits `::error`
annotations for failed expectations and appends a job-summary table when `GITHUB_STEP_SUMMARY` is
set.

**Remote** runs a working copy's stored suite on the control plane (the same run-all the designer's
Scenarios tab uses) — e.g. re-verifying from a pipeline before publishing:

```bash
arazzo-runs scenarios run --working-copy <id> --server https://host/arazzo/v1
```

## Verifying the audit (`audit verify`)

`audit verify` checks a deployment's audit chains ([ADR 0069](../../docs/arazzo/adr/0069-audit-as-evidence-append-only-chained-signed-sink.md)) from their stored bytes. It calls no server: the control plane that wrote the evidence is not in the path that checks it. A runner keeps a chain of its own, for the secrets it resolves, and the same command checks that too, against the runner's public key. Give it a chain file, a directory of chain files (`*.jsonl`, searched through its subdirectories, which are one to a writer), or `-` to read one chain from standard input, for example a blob downloaded from the audit container.

```pwsh
# Every chain in the sink, against the audit's public key, and an anchor the collector holds
arazzo-runs audit verify ./audit --trust-key audit-head-key=./audit-head-key.pub.pem `
    --anchor 5f0c2f6d1b7e4c0e9a3d8b7c6a5f4e3d:64:9b1c...e07a

# One chain from the audit container
az storage blob download --container-name arazzo-audit --name 5f0c2f6d1b7e4c0e9a3d8b7c6a5f4e3d.jsonl --file - |
    arazzo-runs audit verify - --trust-key audit-head-key=./audit-head-key.pub.pem
```

It checks, and names the first failure of, each of these:

| Check | What a failure means |
|-------|----------------------|
| Every line is an audit record, numbered from zero with no gap, carrying the hash of the line before it | A record was altered, removed, added or reordered |
| Every head's signature verifies against `--trust-key` | The chain's tail was rewritten and re-linked, which the hashes alone cannot show, or the head was signed with a key you did not give |
| A chain that says it continues another finds that chain, holding the hash it names | The earlier chain was removed whole, cut short or rewritten |
| A chain's first record is its open record, and no other record is | The chain has lost its beginning, or had one spliced in |
| A torn last line is followed by a chain that continues from the record before it | With a successor, this is what a failed append leaves, and the whole records stand. Without one, the chain was cut short |
| Every `--anchor` names a chain that was given and that holds it | The sink was rewritten after the anchor was published, or the chain was removed |

`--anchor` takes `<chain>:<sequence>:<hash>`, which are the `corvus.arazzo.audit.chain`, `corvus.arazzo.audit.sequence` and `corvus.arazzo.audit.previous_hash` tags of an `audit.head` span, or the same fields of an `Audit anchor` log record. Anchors are what make the check independent of whoever holds the sink, so keep them where the sink's owner cannot rewrite them.

A chain's `unsigned tail` is the number of its records after its last head, which no signature vouches for. Where a later chain continues it, the output says `frozen by chain …, not signed`: the tail cannot have changed since that chain's first head, and nothing authenticated it before then.

With no `--trust-key` the command refuses to run, so that an unchecked chain never reads as verified by accident. `--no-signature-check` verifies the links only, and says in its output that the signatures were not checked. The exit code is `0` only when every chain stands and every anchor is held.

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
  openapi-client docs/arazzo/reference/arazzo-control-plane.openapi.json \
  --rootNamespace Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli.Client \
  --outputPath src/Corvus.Text.Json.Arazzo.Durability.ControlPlane.Cli/Generated
```
