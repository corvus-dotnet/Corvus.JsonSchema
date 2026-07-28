# The Arazzo AOT build image (ADR 0055): Amazon Linux 2023 (matching the AWS Lambda provided.al2023 runtime's
# glibc — LocalStack runs the real provided.al2023 runtime, so the demo needs the same base too) plus the .NET 10
# SDK and the Native AOT link prerequisites. The workflow AOT builder runs `dotnet publish -p:PublishAot=true`
# inside this image, per (environment, version), to produce the native `bootstrap` binary a Lambda function is
# deployed from. Build it with build-aot-builder-image.ps1.
#
# Kept minimal: this starts from the bare AL2023 base and adds ONLY the Native-AOT toolchain plus the SDK — it is
# never a fat SDK image trimmed down. Two hygiene choices keep it at its practical floor for a self-contained
# AL2023 AOT compiler (~1.4 GB, dominated by the irreducible SDK + the gcc/clang/llvm link toolchain):
#   * --setopt=install_weak_deps=False skips RPM "Recommends", so the clang stack does not drag in packages the
#     native link never uses. (python3 stays regardless — it is AL2023's dnf implementation, part of the base.)
#   * The toolchain install, the SDK install, and all cleanup are one layer, so the dnf caches and the SDK-install
#     tarball never persist in a lower layer.
# Everything below is what a Native-AOT publish actually needs: clang + zlib for the link, libicu + tzdata for the
# SDK's own globalization/timezone data (or it faults), and tar + gzip + curl for the dotnet-install script.
FROM amazonlinux:2023

RUN dnf -y --setopt=install_weak_deps=False install clang zlib-devel libicu tzdata tar gzip findutils which \
 && curl -fsSL https://dot.net/v1/dotnet-install.sh -o /tmp/dotnet-install.sh \
 && bash /tmp/dotnet-install.sh --channel 10.0 --install-dir /usr/share/dotnet \
 && ln -sf /usr/share/dotnet/dotnet /usr/bin/dotnet \
 && dnf clean all \
 && rm -rf /tmp/* /var/cache/dnf /var/log/dnf* \
 && dotnet --info

ENV DOTNET_CLI_TELEMETRY_OPTOUT=1 \
    DOTNET_NOLOGO=1 \
    DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1 \
    PATH="/usr/share/dotnet:${PATH}"
WORKDIR /work
