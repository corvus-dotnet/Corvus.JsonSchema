# The Arazzo AOT build image (ADR 0055): Amazon Linux 2023 (matching the AWS Lambda provided.al2023 runtime's
# glibc) plus the .NET 10 SDK and the Native AOT prerequisites (clang, the linker, zlib). The workflow AOT builder
# runs `dotnet publish -p:PublishAot=true` inside this image, per (environment, version), to produce the native
# `bootstrap` binary a Lambda function is deployed from. Build it with build-aot-builder-image.ps1.
FROM amazonlinux:2023

# Native AOT on Linux needs a C toolchain and zlib; libicu and tzdata are needed by the .NET SDK itself
# (globalization and timezone data) or it faults; tar and gzip are for the dotnet-install script.
RUN dnf -y install clang zlib-devel libicu tzdata tar gzip findutils which && dnf clean all

# Install the .NET 10 SDK. The feature band tracks the repo's, so the ILCompiler matches what the code targets.
RUN curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet \
 && ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet \
 && rm /tmp/dotnet-install.sh

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    PATH="/usr/share/dotnet:${PATH}"

RUN dotnet --info
WORKDIR /work
