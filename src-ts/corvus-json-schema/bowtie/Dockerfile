# Bowtie harness for the Corvus.JsonSchema TypeScript evaluator (copy this directory to
# bowtie/implementations/js-corvus-jsonschema). Until the package is published to npm, the image builds it from
# the repository at CORVUS_REF.
FROM node:22-alpine
RUN apk add --no-cache git
ARG CORVUS_REPO=https://github.com/corvus-dotnet/Corvus.JsonSchema.git
ARG CORVUS_REF=main
RUN git clone --depth 1 --branch "$CORVUS_REF" "$CORVUS_REPO" /corvus \
 && cd /corvus/src-ts/corvus-json-schema \
 && npm ci \
 && npm run build
COPY . /usr/app
WORKDIR /usr/app
ENV NODE_ENV=production
RUN npm install --omit=dev
CMD ["node", "bowtie_corvus.js"]
