# Bowtie harness for the Corvus.JsonSchema Rust evaluator (copy this directory to
# bowtie/implementations/rust-corvus-jsonschema). Until the crate is published to crates.io, the image builds it from
# the repository at CORVUS_REF, with the harness beside the crate.
FROM rust:1.90-alpine3.22 AS build
RUN apk add --no-cache git musl-dev
ARG CORVUS_REPO=https://github.com/corvus-dotnet/Corvus.JsonSchema.git
ARG CORVUS_REF=main
RUN git clone --depth 1 --branch "$CORVUS_REF" "$CORVUS_REPO" /corvus
COPY . /corvus/src-rs/corvus-json-schema-bowtie
RUN cargo build --release --locked --manifest-path /corvus/src-rs/corvus-json-schema-bowtie/Cargo.toml

FROM alpine:3.22
COPY --from=build /corvus/src-rs/corvus-json-schema-bowtie/target/release/bowtie-corvus-jsonschema /usr/local/bin/
CMD ["bowtie-corvus-jsonschema"]
