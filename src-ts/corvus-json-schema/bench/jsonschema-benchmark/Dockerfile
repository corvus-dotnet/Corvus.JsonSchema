# Corvus.JsonSchema TypeScript evaluator for https://github.com/sourcemeta-research/jsonschema-benchmark
# (copy this directory to implementations/corvus-ts). Until the package is published to npm, the image builds it
# from the repository at CORVUS_REF.
FROM node:22-alpine

RUN apk add --no-cache git time

ARG CORVUS_REPO=https://github.com/corvus-dotnet/Corvus.JsonSchema.git
ARG CORVUS_REF=main
RUN git clone --depth 1 --branch "$CORVUS_REF" "$CORVUS_REPO" /corvus \
 && cd /corvus/src-ts/corvus-json-schema \
 && npm ci \
 && npm run build

COPY . /app
RUN npm install --prefix /app --omit=dev
ENTRYPOINT ["/app/memory-wrapper.sh", "node", "/app/main.mjs"]
CMD []
