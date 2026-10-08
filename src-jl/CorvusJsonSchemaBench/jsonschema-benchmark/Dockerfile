# Corvus.JsonSchema Julia evaluator for https://github.com/sourcemeta-research/jsonschema-benchmark
# (copy this directory to implementations/corvus-jl). The package needs Julia 1.10 or later and gives the same results
# with every Julia release. The image runs it on the latest.
FROM julia:1.13

RUN apt-get update && apt-get install -y --no-install-recommends time && rm -rf /var/lib/apt/lists/*

# The package's versions in Julia's General registry: Docker downloads this on every build and reuses the layers below
# only while it is unchanged, so a new release is picked up by the next build.
ADD https://raw.githubusercontent.com/JuliaRegistries/General/master/C/CorvusJsonSchema/Versions.toml /tmp/corvus-versions.toml

WORKDIR /app

COPY . .

# Installs the latest version the registry lists (and fails if the package server does not have it yet, which it can
# take some minutes to), then compiles the package and this program into package images. A container then starts
# with both compiled, as a process does on a machine where the package is installed. The images are compiled for the
# processor of the machine that builds them, so build the image on the machine that runs it.
RUN julia --startup-file=no --project=/app -e 'using Pkg, TOML; \
        versions = TOML.parsefile("/tmp/corvus-versions.toml"); \
        latest = maximum(VersionNumber(v) for (v, entry) in versions if !get(entry, "yanked", false)); \
        Pkg.add(name="CorvusJsonSchema", version=latest); \
        Pkg.precompile(strict=true); \
        write("/app/corvus-version", string(latest))'

ENTRYPOINT ["/app/memory-wrapper.sh", "julia", "--startup-file=no", "--project=/app", "/app/main.jl"]
CMD []
