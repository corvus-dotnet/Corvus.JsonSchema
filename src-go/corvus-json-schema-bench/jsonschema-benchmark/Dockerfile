# Corvus.JsonSchema Go evaluator for https://github.com/sourcemeta-research/jsonschema-benchmark
# (copy this directory to implementations/corvus-go). The module comes from the Go module proxy at the version in
# go.mod. The module needs Go 1.25 or later and gives the same results with every Go release. The image builds it with
# the latest.
FROM golang:1.27-alpine

RUN apk add --no-cache --repository=https://dl-cdn.alpinelinux.org/alpine/edge/community time

WORKDIR /app

COPY . .

RUN go mod download

RUN go build -o corvus_go_benchmark .

ENTRYPOINT ["/app/memory-wrapper.sh", "/app/corvus_go_benchmark"]
CMD []
