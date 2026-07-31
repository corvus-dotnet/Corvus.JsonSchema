# Corvus.Text.Json.Arazzo.Durability.Aot

The workflow AOT builder (issue #876, ADR 0055). It assembles a thin serverless host-app around a workflow version's already-signed executor IL assembly and native-AOT compiles it in a build container, producing one native binary per runtime target.

It does not re-run the generator or Roslyn: the executor is compiled and signed once, at catalog-add, and the native binary is derived from that signed IL. The builder verifies the signature, references the runtime at the executor's recorded engine version, and refuses a mismatch rather than mislink.

The form-factor is per target (ADR 0055 amendment): AWS Lambda gets a self-contained Native-AOT `bootstrap` (built in the AL2023 image, `aot-builder.Dockerfile`), Azure Functions a framework-dependent ReadyToRun isolated-worker app (ADR 0061, same image), and the micro-guest a fully static Native-AOT musl `guest` for the Unikraft ELF loader (ADR 0063, built in the alpine image, `aot-builder-alpine.Dockerfile`). Build the images with `build-aot-builder-image.ps1` (`-Variant al2023|alpine`).

This is control-plane build tooling, not part of the AOT-compiled function.