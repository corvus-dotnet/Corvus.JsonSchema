# Arazzo control-plane demo — Aspire deployment

This AppHost is the single launch point for the *real* multi-process topology, orchestrated by
.NET Aspire so the dashboard becomes the OpenTelemetry viewer and the one place you start and stop
everything. It composes:

- **Vault** (HashiCorp, dev mode) — the source-credential secret store (design §13), with a one-shot
  **vault-init** provisioner (the *only* write-capable identity) that writes a read-only, path-scoped
  policy, provisions the runner's **AppRole**, and seeds the demo secrets. The runner is not handed a
  pre-minted token: it authenticates via AppRole and receives a dynamically-issued, short-TTL token (see
  *Secure introduction* below).
- **Keycloak** — the OIDC identity provider (design §16), bootstrapped from `realms/` (the `arazzo`
  realm, groups, seed users, and the UI + CLI clients).
- **controlplane** — the ASP.NET control-plane host (catalog, runs, credentials, administrators,
  security) plus the build-free web UI and the demo `/svc` backends. It stores credential *references*
  only, never binds to Vault.
- **runner** — the execution host: a separate process that claims and executes catalogued runs and §18
  `$draft` debug runs, resolving each source's credential as its own read-only Vault identity at bind
  time. The control plane never executes; the runner does.

## Secure introduction — a simulation of the runtime host's identity (design §13.5.1)

In production the runner's identity — its Vault **AppRole SecretID**, or equivalently a cloud/Kubernetes
**workload identity** — is supplied by the **runtime host's service principal / platform attestation**:
the platform vouches for the workload and delivers (or lets it fetch) a short-lived credential the app
never has to embed. There is no such platform on this dev box, so **this sample simulates that delivery**
with the trusted orchestrator (the AppHost) standing in for the runtime host:

1. **vault-init** (the CI/IaC identity) enables AppRole, creates the `arazzo-runner` role bound to the
   read-only policy (short token TTL), pins a fixed, non-secret **RoleID** (like a username), and generates
   a **response-wrapped SecretID** — the SecretID itself never leaves Vault in plaintext, only a single-use,
   short-TTL *wrapping token* does. That wrapping token is written to a shared handoff directory.
2. The AppHost injects the runner's non-secret RoleID and the path to the wrapping token (this stands in for
   the runtime host handing the workload its identity material).
3. The **runner** reads the wrapping token, **unwraps** it to obtain the SecretID (still never in its env),
   then authenticates via **AppRole** for a dynamically-issued, short-TTL, renewable token scoped to
   read-only. A startup self-check then proves that token resolves the seeded secrets **and is refused a
   write** (HTTP 403) — separation of duties, demonstrated.

So the *mechanism* (AppRole, response-wrapping, unwrap-then-login, least privilege) is real; only the
**delivery of the workload's identity** is simulated by the orchestrator rather than a real service
principal / attestation. The dev root token on the provisioner is likewise a stand-in for a CI/IaC identity.

## GitHub OAuth App — the designer's Git integration (workflow-designer §4.7)

The workflow designer can bind a working copy to a Git branch and commit/pull **as the signed-in
user's own GitHub identity** — a classic OAuth App flow, the same model VS Code and the `gh` CLI
use (each user authorizes in a popup; the token is exchanged server-side and never reaches the
browser). The OAuth model means **no installation step**: the signed-in user reaches whatever they
can see on GitHub — the right shape for browsing sources that may live in any repository, not one
the user controls. It is **off by default**; enable it for your own instance by registering an
OAuth App and dropping its credentials into a local, **uncommitted** file.

1. **Register an OAuth App** — GitHub → Settings → Developer settings → **OAuth Apps** → *New OAuth App*:
   - **Authorization callback URL**: `http://localhost:8090/arazzo/v1/github/auth/callback`
     (the control plane is pinned to port **8090** precisely so this callback is stable across runs).
   - Homepage URL can be `http://localhost:8090/`. No installation exists for OAuth Apps — the
     broker requests the `repo read:user user:email` scopes at sign-in.
2. **Generate a client secret** on the App, and note its **Client ID**.
3. **Copy the credentials into a local file** (never committed — it is `.gitignore`d):
   ```bash
   cp github-oauth.local.json.example github-oauth.local.json
   # then edit github-oauth.local.json with your App's ClientId + ClientSecret
   ```
   The client id is public (it rides the authorize URL); the secret stays in this local file only.
   The AppHost injects the secret into the control plane as an environment variable it resolves via
   `env://GITHUB_OAUTH_CLIENT_SECRET` — it is never written to the repo, Vault, or a container image.

Note for organisation-owned repos: orgs that enable **OAuth App access restrictions** must approve
the App before members' tokens can reach org-private repositories (GitHub → org → Settings →
Third-party access) — the OAuth model's counterpart of an App installation.

Without `github-oauth.local.json` the control plane brokers no OAuth App and the designer's **Git**
panel reports that Git is unavailable — everything else works unchanged. The full walkthrough
(registration, local testing, troubleshooting, cloud deployment) is the
[GitHub OAuth App guide](../../../docs/arazzo/guides/github-oauth-app.md).

## Prerequisites

- **.NET 10 SDK** and the **Aspire CLI** (`dotnet tool install -g Aspire.Cli`).
- **A container runtime the Aspire DCP supports: Docker 20.10+ or podman 4.x+.** This is not optional —
  Aspire's orchestrator (DCP) drives the runtime, and **podman 3.x does not work** (the DCP creates
  containers but hangs before starting them; there is no error, it just stalls). If you are on a distro
  whose packaged podman is 3.x (for example Ubuntu 22.04 ships 3.4.4), follow the WSL2/podman appendix
  below to get a working 5.x first.

## Run it

The preferred launcher is the Aspire CLI. Build the AppHost first, then from the repository root:

```bash
dotnet build samples/arazzo/Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost -c Debug
ASPIRE_ENABLE_CONTAINER_TUNNEL=false aspire start --no-build --non-interactive \
  --apphost samples/arazzo/Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost/Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost.csproj
```

It prints `✅ AppHost started successfully` with the dashboard at the **fixed** `https://localhost:17245` — a
bookmarkable address that stays the same across rebuilds, with **no `?t=` login token** (the AppHost sets
`DOTNET_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS`, so on localhost the dashboard opens straight in — one tab you
keep open and just refresh after a restart). Open it (on WSL the browser runs on Windows, which does not trust
the Linux dev cert, so click through the warning). You will see every resource (vault, vault-init, keycloak,
controlplane, runner) with its logs, traces, and endpoints. Follow the **controlplane** resource's HTTP
endpoint to reach the designer at `/ui/demo/designer.html?live`. Check status any time with `aspire ps`.

Two things matter on this box:

- **`--no-build` is required** (hence the separate `dotnet build` first). Without it the detached AppHost
  spends a couple of minutes building, the parent CLI's wait-for-backchannel times out mid-build, and the
  child self-cancels at `DashboardServiceHost.StartAsync` — exiting 0 having never come up.
- **No `--isolated`.** Omitting it lets the launchSettings ports hold steady — the control plane on the pinned
  8090, the dashboard on 17245, OTLP/resource on their fixed ports — so the dashboard URL never changes between
  rebuilds. The trade-off is that only ONE composition may run at a time (this worktree OR another checkout);
  the AppHost keeps no meaningful state in user secrets, so nothing else needs the isolation. Add `--isolated`
  back only when you deliberately want to run two compositions side by side (it re-randomizes every port).

`aspire start` needs a trusted dev certificate and podman 5.x on the default `PATH` — see the two notes below
if either is missing. To avoid the certificate entirely, the fallback launcher runs the dashboard over HTTP:

```bash
dotnet run --project samples/arazzo/Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost \
  -c Debug --launch-profile http
```

The `http` profile sets `ASPIRE_ALLOW_UNSECURED_TRANSPORT` and `ASPIRE_ENABLE_CONTAINER_TUNNEL=false`,
sidestepping the certificate, and serves the dashboard at the fixed `http://localhost:15154` (also token-free).

### HTTPS dev certificate on WSL2 — regenerate it if `aspire start` crashes

The Aspire CLI runs a dev-cert trust check at startup. Two things went wrong with it on WSL2; both are resolved.

1. **A .NET SDK 10.0.103 regression** broke `dotnet dev-certs https --trust` (an EventSource id mismatch,
   `Event WslWindowsTrustSucceeded was assigned event ID 115 but 113 was passed to WriteEvent`). Fixed in
   **10.0.104+** ([dotnet/aspnetcore#65391](https://github.com/dotnet/aspnetcore/issues/65391),
   [dotnet/sdk#52978](https://github.com/dotnet/sdk/issues/52978)) — on 10.0.109 with ASP.NET Core runtime
   10.0.9, `dotnet dev-certs` works again.

2. **The CLI crashed with `MapOpenSsl30Code reported unhandled error code '30'`** (`AppHost process exited
   with code 2`), from `X509Chain.Build` in `GetTrustLevel`, tracked as
   [microsoft/aspire#18703](https://github.com/microsoft/aspire/issues/18703). The root cause turned out to be
   a **corrupt/stale dev-cert file** — not purely a .NET/OpenSSL bug (the CLI just isn't guarded against
   `X509Chain.Build` throwing). The fix is to **regenerate the certificate**:

   ```bash
   aspire certs clean && aspire certs trust
   ```

   That rewrites the three files Linux needs — two `.pfx` under
   `~/.dotnet/corefx/cryptography/x509stores/{my,root}` and a `.pem` (with its OpenSSL symlink) under
   `~/.aspnet/dev-certs/trust`. Afterwards `aspire doctor` no longer throws (it reports the cert "partially
   trusted"), and `aspire start` sets `SSL_CERT_DIR` itself and comes up over HTTPS. If `aspire certs` errors,
   fall back to `dotnet dev-certs https --clean` then `--trust`.

On WSL the browser runs on **Windows**, which trusts certs from the Windows store, so the Linux dev cert is
never fully trusted there — browsers warn on the HTTPS dashboard regardless; click through. To skip the
certificate entirely, use the `dotnet run --launch-profile http` fallback above.

### The DCP container tunnel is disabled (`ASPIRE_ENABLE_CONTAINER_TUNNEL=false`)

Aspire 13.4 added an on-by-default **container tunnel** — an `aspire-container-network-tunnelproxy`
container that provides container-to-host connectivity. On rootless podman under WSL2 the DCP's start of
the *first* container (Vault) times out while setting that tunnel up (`context deadline exceeded`), even
though `podman start` runs the container instantly — and because vault-init and the runner depend on Vault,
the whole composition stalls. This composition doesn't need the tunnel (the host processes reach the
containers by their published ports; nothing dials back to the host), so it is turned off with
`ASPIRE_ENABLE_CONTAINER_TUNNEL=false` in the launch profile. With it off, Vault starts in ~20s and the
full composition is up in ~50s. If you run on Docker or native-Linux podman you can leave the tunnel on.

## Appendix — podman 5.x on WSL2 without a distro upgrade

Ubuntu 22.04's packaged podman (3.4.4) is too old for the Aspire DCP, the OpenSUSE/Kubic repo is gone,
and the podman snap is strictly confined (it can't bind-mount arbitrary host files, which the Keycloak
realm import needs). A **self-contained static podman 5.x bundle** is the reliable path. It needs no
distro upgrade; the only `sudo` is the one-time helper packages.

**1. Rootless helpers + shared mount propagation (one-time, needs sudo):**

```bash
sudo apt-get update && sudo apt-get install -y uidmap slirp4netns fuse-overlayfs
loginctl enable-linger "$USER"    # a persistent user session for rootless container lifecycle
sudo mount --make-rshared /       # CRITICAL on WSL2 — see below
```

The `mount --make-rshared /` is the single most important step. WSL2 mounts `/` with
**private** propagation, and rootless podman then does its per-container mount/namespace work
slowly — `podman ps` stalls for 25–40s, which makes the Aspire DCP's container inspects time
out and the composition stall. Making `/` a shared mount fixes it (idle `podman ps` drops to
sub-second). It applies instantly with no restart. Persist it across WSL restarts by adding it
to `/etc/wsl.conf`:

```toml
[boot]
command = mount --make-rshared /
```

Verify with `findmnt -no PROPAGATION /` — it must read `shared`.

**2. The static podman 5.x bundle (no sudo):**

```bash
mkdir -p ~/podman5
curl -fSL https://github.com/mgoltzsche/podman-static/releases/download/v5.8.4/podman-linux-amd64.tar.gz \
  | tar xz -C ~/podman5 --strip-components=1
export PATH=$HOME/podman5/usr/local/bin:$PATH   # put in ~/.bashrc so the DCP inherits it
```

**3. Config — `~/.config/containers/registries.conf`:**

```toml
unqualified-search-registries = ["docker.io"]
```

**4. Config — `~/.config/containers/containers.conf`** (the settings that matter on WSL2):

```toml
[engine]
cgroup_manager = "cgroupfs"          # no systemd user cgroup manager under WSL
runtime = "runc"                     # crun errors "unknown version specified" on the WSL kernel; runc works
database_backend = "sqlite"          # BoltDB is slow enough that the DCP's container inspects time out
helper_binaries_dir = [
  "/home/<you>/podman5/usr/local/lib/podman",     # conmon, netavark, aardvark-dns
  "/home/<you>/podman5/usr/local/bin",            # crun, runc, pasta, fuse-overlayfs
  "/home/<you>/podman5/usr/local/libexec/podman",
]

[engine.runtimes]
runc = ["/home/<you>/podman5/usr/local/bin/runc"]
```

**5. Initialise the storage once (fresh SQLite):**

```bash
podman system reset -f            # only needed if you changed database_backend on an existing store
podman run --rm docker.io/library/hello-world     # should print "Hello from Docker!" in a couple of seconds
```

**6. Make it the system default (required for `aspire start`):**

Steps above put podman 5.x on `PATH` only for shells that source `~/.bashrc`. The Aspire CLI's dev-cert check
and DCP need podman 5.x on the *default* `PATH`, or they find the distro's `/usr/bin/podman` 3.4.4. Promote
the bundle into `/usr/local` so it wins for every shell and service:

```bash
sudo cp -a ~/podman5/usr/local/* /usr/local/     # /usr/local/bin is ahead of /usr/bin on the default PATH
sudo apt remove -y podman                         # drop the distro's 3.4.4 so nothing shadows it
hash -r; podman --version                          # -> 5.8.4 from /usr/local/bin
```

Then repoint the `helper_binaries_dir` and `runc` paths in `containers.conf` (step 4) from
`~/podman5/usr/local/...` to `/usr/local/...`.

Then run the AppHost with that podman on `PATH` (and `SUPPRESS_BOLTDB_WARNING=1` to quieten migration
noise). Every choice above was necessary on WSL2: short-name registry, cgroupfs, runc-not-crun, and the
SQLite backend. Missing any one of them stalls the DCP or breaks container start.

### Stopping it cleanly — do NOT `kill -9`

**`aspire stop` is unreliable on this box — do not trust its output.** It prints "stopped successfully" but
regularly leaves the AppHost, its `dcp` processes, and the containers running. This was verified even for an
`aspire start`-launched host: it reported success while the AppHost, 7 `dcp` processes, and 2 containers were
still up 45s later. So stop it yourself the same way regardless of how you launched it — SIGTERM the AppHost
process (`kill -15 <apphost-pid>`, or Ctrl+C for a foreground `dotnet run`), then reap what the DCP leaves
behind.

The teardown is **asynchronous**, and — importantly — **the DCP is not a child of the AppHost**. The `dcp`
process and its `dcptun` network helper reparent to init and live in their *own* process group, so SIGTERM to
the AppHost does *not* directly signal them; the AppHost asks them to stop over a control channel, which can be
slow or incomplete. So a clean stop is three steps: signal, wait, **reap survivors**:

```bash
kill -15 <apphost-pid>                                  # ask the AppHost to shut down
while podman ps -aq | grep -q .; do sleep 1; done       # wait until its containers are gone
pkill -9 -x dcp; pkill -9 -f dcptun; pkill -9 -x aardvark-dns   # reap any DCP/tunnel/helper that outlived it
podman network prune -f                                 # drop leftover aspire-session-network-*
```

Restarting into a half-torn-down state — or, more often, **with an orphaned `dcp`/`dcptun` from a previous run
still alive** — races the network setup, and the first container up (Vault) fails to start
(`context deadline exceeded`), cascading to everything that waits on it. `ps -eo pid,etime,comm | grep -E
'dcp|dcptun'` reveals orphans by their long elapsed time.

Never `kill -9` the AppHost or DCP: it orphans the containers and networks outright, along with their
aardvark-dns / conmon / rootlessport helper processes, and the podman store fills with ghost `Terminated`
records. That bloat makes even an idle `podman ps` take 30s+, at which point the DCP can no longer drive the
runtime. If you get into that state, reset the rootless store:

```bash
pkill -9 -x aardvark-dns; pkill -9 -x conmon; pkill -9 -x rootlessport; pkill -9 -x slirp4netns
rm -rf ~/.local/share/containers /run/user/1000/containers /run/user/1000/libpod
# then re-pull the three images on the next run
```

`podman ps` should be back to sub-second afterwards.

### Notes

With the shared mount (above) and a graceful stop, the whole five-resource composition — Vault,
vault-init, Keycloak, the control plane, and the **runner** (which registers in the control plane's
runner registry and hosts the catalogued workflow versions) — comes up and stays up on WSL2. Docker
Desktop or a native-Linux podman host avoids the WSL-specific mount/teardown fragility entirely and is
the easiest path if you would rather not manage the rootless podman details.
