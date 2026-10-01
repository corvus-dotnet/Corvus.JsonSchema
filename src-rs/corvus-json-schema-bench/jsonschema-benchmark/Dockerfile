# Corvus.JsonSchema Rust evaluator for https://github.com/sourcemeta-research/jsonschema-benchmark
# (copy this directory to implementations/corvus-rs). Until the crate is published to crates.io, the image builds it
# from the repository at CORVUS_REF.
FROM rust:1.90-alpine3.22

RUN apk add --no-cache git musl-dev \
 && apk add --no-cache --repository=https://dl-cdn.alpinelinux.org/alpine/edge/community time

ARG CORVUS_REPO=https://github.com/corvus-dotnet/Corvus.JsonSchema.git
ARG CORVUS_REF=main
RUN git clone --depth 1 --branch "$CORVUS_REF" "$CORVUS_REPO" /corvus

COPY . /app
RUN cargo build --release --locked --manifest-path /app/Cargo.toml
ENTRYPOINT ["/app/memory-wrapper.sh", "/app/target/release/corvus_rs_benchmark"]
CMD []
