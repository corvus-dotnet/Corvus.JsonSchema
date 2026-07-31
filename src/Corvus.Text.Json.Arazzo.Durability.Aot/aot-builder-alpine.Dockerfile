# The Arazzo AOT build image for the micro-guest target (ADR 0063): the alpine .NET 10 SDK plus the static-musl
# Native AOT link toolchain. The workflow AOT builder runs `dotnet publish` inside this image, per (environment,
# version), to produce the fully static `guest` binary the micro-guest deployer bakes into a Unikraft initrd.
# Build it with build-aot-builder-image.ps1 -Variant alpine.
#
# Kept minimal, like the AL2023 image: the SDK base plus ONLY what a static musl Native-AOT link needs — clang,
# lld (the LinkerFlavor the generated guest project pins), musl-dev for the static libc, and zlib-dev for the
# compression the ILC-linked runtime references. This is the recipe the ADR 0063 spike proved.
FROM mcr.microsoft.com/dotnet/sdk:10.0-alpine

RUN apk add --no-cache clang lld musl-dev zlib-dev

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
WORKDIR /work
