# Corvus.JsonSchema Go evaluator for https://github.com/sourcemeta-research/jsonschema-benchmark
# (copy this directory to implementations/corvus-go). The module needs Go 1.25 or later and gives the same results
# with every Go release. The image builds it with the latest.
FROM golang:1.27-alpine

RUN apk add --no-cache --repository=https://dl-cdn.alpinelinux.org/alpine/edge/community time

# The module's latest version on the Go module proxy: Docker downloads this on every build and reuses the layers below
# only while it is unchanged, so a new release is picked up by the next build.
ADD https://proxy.golang.org/github.com/corvus-dotnet/!corvus.!json!schema/src-go/corvus-json-schema/@latest /tmp/corvus-latest.json

WORKDIR /app

COPY . .

RUN go get github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema@latest \
 && go build -o corvus_go_benchmark . \
 && go list -m -f '{{.Version}}' github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema | sed 's/^v//' > /app/corvus-version

ENTRYPOINT ["/app/memory-wrapper.sh", "/app/corvus_go_benchmark"]
CMD []
