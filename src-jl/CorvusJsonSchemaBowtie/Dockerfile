# Bowtie harness for the Corvus.JsonSchema Julia evaluator (the julia-corvus-jsonschema harness). The image takes the
# package from the repository at CORVUS_REF (a branch or a tag, such as CorvusJsonSchema-v0.1.0), with the harness
# beside it. The package needs Julia 1.10 or later and gives the same results with every Julia release. The image
# runs it on the latest. Julia publishes no build for musl, so the image is the official one, on Debian.
FROM julia:1.13 AS source
RUN apt-get update && apt-get install -y --no-install-recommends git ca-certificates && rm -rf /var/lib/apt/lists/*
ARG CORVUS_REPO=https://github.com/corvus-dotnet/Corvus.JsonSchema.git
ARG CORVUS_REF=main
# Only the package's directory of the repository.
RUN git clone --depth 1 --filter=blob:none --sparse --branch "$CORVUS_REF" "$CORVUS_REPO" /corvus \
 && git -C /corvus sparse-checkout set src-jl/CorvusJsonSchema \
 && rm -rf /corvus/.git /corvus/src-jl/CorvusJsonSchema/test
COPY . /corvus/src-jl/CorvusJsonSchemaBowtie

FROM julia:1.13
# Package images are compiled for the processor of the machine that builds them unless told otherwise, and Julia
# compiles them again at start-up on any other. A Bowtie image is built on one machine and run on many, so the
# images are compiled for any processor of the architecture.
ENV JULIA_CPU_TARGET=generic
COPY --from=source /corvus /corvus
# Compiles the package and the harness into package images, so that a container starts with both compiled.
RUN julia --startup-file=no --project=/corvus/src-jl/CorvusJsonSchemaBowtie -e 'using Pkg; \
        Pkg.develop(path="/corvus/src-jl/CorvusJsonSchema"); \
        Pkg.precompile(strict=true)'
CMD ["julia", "--startup-file=no", "--project=/corvus/src-jl/CorvusJsonSchemaBowtie", "/corvus/src-jl/CorvusJsonSchemaBowtie/bowtie.jl"]
