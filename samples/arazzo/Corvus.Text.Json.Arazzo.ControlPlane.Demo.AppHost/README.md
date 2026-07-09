# Arazzo control-plane demo — Aspire deployment

This AppHost is the single launch point for the *real* multi-process topology, orchestrated by
.NET Aspire so the dashboard becomes the OpenTelemetry viewer and the one place you start and stop
everything. It composes:

- **Vault** (HashiCorp, dev mode) — the source-credential secret store (design §13), with a one-shot
  **vault-init** provisioner that writes a read-only, path-scoped policy, mints the runner's read-only
  token, and seeds the demo secrets. The provisioner is the *only* write-capable identity.
- **Keycloak** — the OIDC identity provider (design §16), bootstrapped from `realms/` (the `arazzo`
  realm, groups, seed users, and the UI + CLI clients).
- **controlplane** — the ASP.NET control-plane host (catalog, runs, credentials, administrators,
  security) plus the build-free web UI and the demo `/svc` backends. It stores credential *references*
  only, never binds to Vault.
- **runner** — the execution host: a separate process that claims and executes catalogued runs and §18
  `$draft` debug runs, resolving each source's credential as its own read-only Vault identity at bind
  time. The control plane never executes; the runner does.

## Prerequisites

- **.NET 10 SDK** and the **Aspire CLI** (`dotnet tool install -g aspire` or the workload).
- **A container runtime the Aspire DCP supports: Docker 20.10+ or podman 4.x+.** This is not optional —
  Aspire's orchestrator (DCP) drives the runtime, and **podman 3.x does not work** (the DCP creates
  containers but hangs before starting them; there is no error, it just stalls). If you are on a distro
  whose packaged podman is 3.x (for example Ubuntu 22.04 ships 3.4.4), follow the WSL2/podman appendix
  below to get a working 5.x first.

## Run it

From the repository root:

```bash
dotnet run --project samples/arazzo/Corvus.Text.Json.Arazzo.ControlPlane.Demo.AppHost \
  -c Debug --launch-profile http
```

The console prints a dashboard URL with a login token, e.g. `http://localhost:15154/login?t=...`.
Open it. You will see every resource (vault, vault-init, keycloak, controlplane, runner) with its logs,
traces, and endpoints. Follow the **controlplane** resource's HTTP endpoint to reach the designer at
`/ui/demo/designer.html?live`.

`--launch-profile http` runs the dashboard and services over HTTP (the profile sets
`ASPIRE_ALLOW_UNSECURED_TRANSPORT`), sidestepping the HTTPS developer certificate. On a box with a
trusted dev cert you could drop it — but read the next note first, because on some SDKs you cannot get a
trusted dev cert at all.

### HTTPS dev certificate: a .NET SDK 10.0.103 regression (use `--launch-profile http`)

Trusting the ASP.NET Core HTTPS dev certificate — `dotnet dev-certs https --trust`, or the Aspire wrapper
`aspire certs trust` — is **broken in .NET SDK 10.0.103**. It fails with an EventSource id mismatch
(`Event WslWindowsTrustSucceeded was assigned event ID 115 but 113 was passed to WriteEvent`) that surfaces
as `MapOpenSsl30Code reported unhandled error code '30'` / `PartiallyFailedToTrustTheCertificate`. This is a
confirmed SDK regression, not a machine problem: **10.0.102 works, 10.0.103 does not**
([dotnet/aspnetcore#65391](https://github.com/dotnet/aspnetcore/issues/65391),
[dotnet/sdk#52978](https://github.com/dotnet/sdk/issues/52978)).

The consequence for Aspire: **`aspire run` runs a dev-cert trust check at startup, so it also fails on
10.0.103** (`AppHost process exited with code 2`). On that SDK, start the AppHost with
`dotnet run --launch-profile http` (as above), not `aspire run`. If you specifically need the HTTPS
dashboard or native `aspire run`, either pin the working SDK in `global.json`
(`"sdk": { "version": "10.0.102", "rollForward": "disable" }`) and run `aspire certs trust`, or wait for the
patched SDK (10.0.104+). Note that on WSL the browser runs on **Windows**, so a Linux-trusted cert must
*also* be trusted in the Windows certificate store — another reason the HTTP profile is the least-friction
path here.

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

Then run the AppHost with that podman on `PATH` (and `SUPPRESS_BOLTDB_WARNING=1` to quieten migration
noise). Every choice above was necessary on WSL2: short-name registry, cgroupfs, runc-not-crun, and the
SQLite backend. Missing any one of them stalls the DCP or breaks container start.

### Stopping it cleanly — do NOT `kill -9`

Because `aspire run` can't be used on this box (the SDK 10.0.103 dev-cert regression above), the AppHost is
started with `dotnet run` — and that changes how you stop it. `aspire stop` can *see* a `dotnet run`-started
AppHost (`aspire ps` lists it) but **cannot reliably stop it**: the `CLI PID` is unknown, so it reports
"stopped successfully" without actually signalling the process. Stop it yourself with SIGTERM to the AppHost
process — `kill -15 <apphost-pid>` — or Ctrl+C if you started it in a foreground terminal. (`aspire stop`
*is* the right tool, but only for an AppHost you started with `aspire run`.)

The teardown is **asynchronous**: the AppHost hands off to the DCP, which removes its containers and the
`aspire-session-network-*` networks over the next several seconds. **Wait for that to finish before
restarting** — poll until `podman ps -a` is empty and no `aspire-session-network-*` remains. Restarting into
a half-torn-down state races the network setup, and the first container up (Vault) can fail to start, which
cascades to everything that waits on it.

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
