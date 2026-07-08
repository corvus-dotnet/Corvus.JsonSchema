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

`--launch-profile http` avoids the HTTPS dev-certificate (which does not import cleanly on WSL/OpenSSL);
the profile sets `ASPIRE_ALLOW_UNSECURED_TRANSPORT` for the dashboard. On a box with a trusted dev cert
you can drop it and use the default profile.

## Appendix — podman 5.x on WSL2 without a distro upgrade

Ubuntu 22.04's packaged podman (3.4.4) is too old for the Aspire DCP, the OpenSUSE/Kubic repo is gone,
and the podman snap is strictly confined (it can't bind-mount arbitrary host files, which the Keycloak
realm import needs). A **self-contained static podman 5.x bundle** is the reliable path. It needs no
distro upgrade; the only `sudo` is the one-time helper packages.

**1. Rootless helpers (one-time, needs sudo):**

```bash
sudo apt-get update && sudo apt-get install -y uidmap slirp4netns fuse-overlayfs
loginctl enable-linger "$USER"    # a persistent user session for rootless container lifecycle
```

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

### Known limitation on this setup

Under the full composition, rootless podman on WSL2 can become slow while several containers are running
and the DCP is polling them, and an occasional container inspect exceeds the DCP's timeout — most often
the one-shot **vault-init**, which leaves the **runner** waiting on it. The control plane, Keycloak, and
Vault come up and the control plane serves; the credentialed-runner tail is the fragile part. If you hit
it, a Docker Desktop or a native-Linux podman host (no WSL storage/lock contention) runs the whole
composition cleanly. This is an environment performance limit, not a wiring problem — the same AppHost
runs end-to-end on Docker.
