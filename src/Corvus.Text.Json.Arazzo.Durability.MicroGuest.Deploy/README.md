# Corvus.Text.Json.Arazzo.Durability.MicroGuest.Deploy

The micro-guest `IServerlessDeployer` (issue #876, ADR 0063). The runner's deploy worker drives it exactly as it drives the cloud deployers: the version's native artifact is verified against the trust store first (ADR 0059), then this deployer bakes the verified `linux-musl` static guest binary into a Unikraft initrd CPIO, and instructs the warm micro-guest **sidecar** on the runner's own machine to evolve a snapshotted Hyperlight micro-VM sandbox for the (environment, version). The recorded function URL is the sidecar's local invoke endpoint, so the dispatch-ready gate, the serverless execution backend, and the operator surface all work unchanged.

## The sidecar admin contract

The deployer drives (and the sidecar implements) this surface on `SidecarBaseUrl`. Every call carries `Authorization: Bearer <admin token>`, read by reference from the runner's secret store (`AdminToken`, for example `env://ARAZZO_SIDECAR_ADMIN_TOKEN`); the sidecar refuses a call without it, and `MicroGuestSidecarInvokeAuthenticator` presents the same token on each invoke (P1-10):

| Request | Body | Effect |
| --- | --- | --- |
| `PUT /sandboxes/{id}/initrd` | `application/octet-stream` (the initrd CPIO) | Stages the guest image for the sandbox. A failed upload never replaces a live snapshot. |
| `PUT /sandboxes/{id}` | `application/json` (`{"memoryMib": 64, "allowedHosts": ["host:port", ...], "environment": {"ARAZZO_SOURCE__name": "url", ...}, "attestation": "<base64>", "signature": {"algorithm", "keyId", "value"}}`) | Verifies the staged initrd against the attestation (the signature under the sidecar's own trust store, the guest binary's digest against the attestation) and refuses with a 403 otherwise; then builds the sandbox from it: bakes the environment pairs and the sidecar's own guest-facing invocation endpoint into the frozen argv, boots the kernel, loads the guest, snapshots ("evolve"), and returns `{"invokeUrl": "..."}`. Re-PUT replaces the sandbox (a redeploy). |

Per advance, the runner POSTs the standard invocation document (`{runId, environment, checkpointUrl, checkpointToken}`) to the returned `invokeUrl`; the sidecar restores the snapshot hermetically and calls the guest, which fetches that invocation from the sidecar over its allowlisted network, advances the run, checkpoints back to the runner over HTTP (Model B, ADR 0062), posts the outcome, and exits the VM.

`allowedHosts` is the sandbox's whole egress allowlist: the runner's checkpoint surface plus each configured source host, and nothing else. That is a tighter posture than the cloud targets (ADR 0063). The checkpoint surface must be routable: the guest's host-proxied network denies loopback and link-local by design.

## The initrd

The archive is `newc` CPIO in the exact shape the guest kernel's ELF loader consumes (`.`, each ancestor directory, the executable at the kernel's baked exec path (default `/bin/guest`), and the trailer), deterministic (fixed inodes, zero mtime, root ownership) so the same binary always bakes the same initrd.

The sidecar mints a per-sandbox guest token at evolve and freezes it into the sandbox's argv as `ARAZZO_GUEST_TOKEN`; the baked guest presents it on its invocation fetch and its outcome, so the guest surface answers only that sandbox.

## The attestation rides with the image

The runner's deploy service verifies the native attestation before this deployer is called, and the deploy request carries the same evidence on: `AttestationUtf8`, the attestation's exact signed bytes, and `SignatureUtf8`, the detached signature document, both as read from the package. The deployer puts them in the evolve document, the attestation as base64 so nothing re-serializes the signed message, and the sidecar verifies them for itself under its own trust store (`--trusted-key`), which holds the same executor-signing public key the runner's `Runner:ExecutorTrust:PublicKeyFile` does. A request without them is refused here with an `ArgumentException` before the sidecar is called: the sidecar boots only an attested image (P1-10, ADR 0065).

This is runner-side deploy tooling: the runner is the secure boundary (ADR 0059), and no control-plane secret or cloud credential is involved. The "platform" is the runner's own machine.