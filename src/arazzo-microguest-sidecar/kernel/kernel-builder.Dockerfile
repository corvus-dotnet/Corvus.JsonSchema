# The micro-guest kernel builder (ADR 0063): kraft-hyperlight from the danbugs/kraftkit fork's
# hyperlight-platform branch (the upstream unikraft/kraftkit plat-hyperlight branch is stale) on Go >= 1.26,
# plus the Unikraft build prerequisites. Everything upstream is pre-1.0; this image pins the toolchain so
# kernel builds are deliberate, reproducible moves. Build the kernel with ../build-microguest-kernel.ps1.
FROM golang:1.26-bookworm
RUN apt-get update && apt-get install -y --no-install-recommends \
    build-essential flex bison git wget unzip ca-certificates \
    libncurses-dev python3 pkg-config uuid-runtime rsync cpio \
    && rm -rf /var/lib/apt/lists/*
RUN git clone --depth 1 --branch hyperlight-platform https://github.com/danbugs/kraftkit.git /kraftkit \
    && cd /kraftkit && go build -o /usr/local/bin/kraft-hyperlight ./cmd/kraft \
    && rm -rf /kraftkit /root/go /root/.cache
WORKDIR /work
