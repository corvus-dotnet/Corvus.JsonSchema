# arazzo-microguest-sidecar

The warm micro-guest sidecar (issue #876, ADR 0063): the small Rust companion process on a runner's machine that holds one evolved, snapshotted Hyperlight/Unikraft micro-VM sandbox per (environment, version) and restores it hermetically for each run advance. Starting fast is the point of the micro-guest backend, so nothing boots per advance: the kernel boots and the guest loads once, at deploy, and each invocation is a snapshot restore (a pristine VM state, hypervisor-isolated) plus a run to completion.

## Surfaces

The **admin surface** (default `127.0.0.1:9411`, keep it loopback) is what the runner drives:

| Request | Effect |
| --- | --- |
| `PUT /sandboxes/{id}/initrd` | Stages the guest initrd CPIO the deployer baked. |
| `PUT /sandboxes/{id}` | Evolves the sandbox from the staged initrd (`{"memoryMib", "allowedHosts", "environment"}`), freezing the guest-surface URL and the environment pairs into argv, and returns `{"invokeUrl"}`. Re-PUT replaces the sandbox. |
| `POST /invoke/{id}` | One run advance: holds the invocation for the guest, restores the snapshot, runs the guest, and returns the outcome the guest posted — the same invocation/outcome contract a deployed cloud function speaks. |
| `DELETE /sandboxes/{id}` | Tears the sandbox down. |

The **guest surface** (default bind `0.0.0.0:9412`, advertised via `--guest-advertise`) is what the running guest reaches over its allowlisted host-proxied network: `GET /guest/{id}` hands it its invocation, `POST /guest/{id}` receives its outcome. It must be a routable address; the guest's network denies loopback by design. The sidecar adds its own guest host to every sandbox's egress allowlist, and strips the `:port` suffixes from the deployer's `allowedHosts` entries (the policy layer matches by host/IP).

Each sandbox lives on a dedicated owner thread (the VM never crosses threads), so invocations serialize per sandbox by construction; distinct sandboxes advance concurrently.

## Building and running

`./build-microguest-sidecar.ps1` compiles the static musl binary in the pinned `rust:1.89-alpine` container (`cargo build --release --locked`; the committed `Cargo.lock` pins the graph, including `hyperlight-unikraft` at exactly 0.12.1). `./build-microguest-kernel.ps1` builds the guest kernel from `kernel/kraft.yaml` — the kconfig the ADR 0063 spike proved for a .NET 10 Native-AOT static musl guest — using the pinned kernel-builder image (`kraft-hyperlight` from the `danbugs/kraftkit` `hyperlight-platform` branch).

```
arazzo-microguest-sidecar \
  --kernel kernel/.unikraft/build/arazzo-microguest_hyperlight-x86_64 \
  --guest-advertise 172.20.0.10:9412
```

The host needs a hypervisor (`/dev/kvm` on Linux). `cargo test --no-default-features` runs the full HTTP and lifecycle suite without KVM or the hyperlight dependency: the real VM sits behind the `VmFactory` seam, and the tests' stand-in guest speaks the same HTTP contract the baked guest does.

This process holds no workflow logic and no credentials: all policy is the sandbox lifecycle and the per-sandbox egress allowlist (ADR 0063), and the guest checkpoints straight back to the runner (ADR 0062).
