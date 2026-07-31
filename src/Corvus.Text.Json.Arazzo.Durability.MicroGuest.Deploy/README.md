# Corvus.Text.Json.Arazzo.Durability.MicroGuest.Deploy

The micro-guest `IServerlessDeployer` (issue #876, ADR 0063). The runner's deploy worker drives it exactly as it drives the cloud deployers: the version's native artifact is verified against the trust store first (ADR 0059), then this deployer bakes the verified `linux-musl` static guest binary into a Unikraft initrd CPIO, and instructs the warm micro-guest **sidecar** on the runner's own machine to evolve a snapshotted Hyperlight micro-VM sandbox for the (environment, version). The recorded function URL is the sidecar's local invoke endpoint, so the dispatch-ready gate, the serverless execution backend, and the operator surface all work unchanged.

## The sidecar admin contract

The deployer drives (and the sidecar implements) this surface on `SidecarBaseUrl`:

| Request | Body | Effect |
| --- | --- | --- |
| `PUT /sandboxes/{id}/initrd` | `application/octet-stream` (the initrd CPIO) | Stages the guest image for the sandbox. A failed upload never replaces a live snapshot. |
| `PUT /sandboxes/{id}` | `application/json` (`{"memoryMib": 64, "allowedHosts": ["host:port", ...], "environment": {"ARAZZO_SOURCE__name": "url", ...}}`) | Builds the sandbox from the staged initrd: bakes the environment pairs and the sidecar's own guest-facing invocation endpoint into the frozen argv, boots the kernel, loads the guest, snapshots ("evolve"), and returns `{"invokeUrl": "..."}`. Re-PUT replaces the sandbox (a redeploy). |

Per advance, the runner POSTs the standard invocation document (`{runId, environment, checkpointUrl, checkpointToken}`) to the returned `invokeUrl`; the sidecar restores the snapshot hermetically and calls the guest, which fetches that invocation from the sidecar over its allowlisted network, advances the run, checkpoints back to the runner over HTTP (Model B, ADR 0062), posts the outcome, and exits the VM.

`allowedHosts` is the sandbox's whole egress allowlist: the runner's checkpoint surface plus each configured source host, and nothing else. That is a tighter posture than the cloud targets (ADR 0063). The checkpoint surface must be routable: the guest's host-proxied network denies loopback and link-local by design.

## The initrd

The archive is `newc` CPIO in the exact shape the guest kernel's ELF loader consumes (`.`, each ancestor directory, the executable at the kernel's baked exec path (default `/bin/guest`), and the trailer), deterministic (fixed inodes, zero mtime, root ownership) so the same binary always bakes the same initrd.

This is runner-side deploy tooling: the runner is the secure boundary (ADR 0059), and no control-plane secret or cloud credential is involved. The "platform" is the runner's own machine.