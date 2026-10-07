# Bowtie harness for the Corvus.JsonSchema Go evaluator (copy this directory to
# bowtie/implementations/go-corvus-jsonschema). The image builds the library from the repository at CORVUS_REF (a
# branch or a tag, such as src-go/corvus-json-schema/v0.1.0), with the harness beside it. The module needs Go 1.25 or
# later and gives the same results with every Go release. The image builds it with the latest.
FROM golang:1.27-alpine AS build
RUN apk add --no-cache git
ARG CORVUS_REPO=https://github.com/corvus-dotnet/Corvus.JsonSchema.git
ARG CORVUS_REF=main
# Only the library's directory of the repository.
RUN git clone --depth 1 --filter=blob:none --sparse --branch "$CORVUS_REF" "$CORVUS_REPO" /corvus \
 && git -C /corvus sparse-checkout set src-go/corvus-json-schema
COPY . /corvus/src-go/corvus-json-schema-bowtie
RUN cd /corvus/src-go/corvus-json-schema-bowtie && CGO_ENABLED=0 go build -trimpath -o /bowtie-corvus-jsonschema .

FROM alpine:3.22
COPY --from=build /bowtie-corvus-jsonschema /usr/local/bin/
CMD ["bowtie-corvus-jsonschema"]
