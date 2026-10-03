# Bowtie harness for the Corvus.JsonSchema Java evaluator (copy this directory to
# bowtie/implementations/java-corvus-jsonschema). Until the library is on Maven Central, the image builds it from the
# repository at CORVUS_REF, with the harness beside it.
FROM maven:3.9-eclipse-temurin-21 AS build
RUN apt-get update && apt-get install -y --no-install-recommends git && rm -rf /var/lib/apt/lists/*
ARG CORVUS_REPO=https://github.com/corvus-dotnet/Corvus.JsonSchema.git
ARG CORVUS_REF=main
RUN git clone --depth 1 --branch "$CORVUS_REF" "$CORVUS_REPO" /corvus
RUN cd /corvus/src-java/corvus-json-schema && mvn -B -q -ntp install -DskipTests
COPY . /corvus/src-java/corvus-json-schema-bowtie
RUN cd /corvus/src-java/corvus-json-schema-bowtie && mvn -B -q -ntp package -DskipTests

FROM eclipse-temurin:21-jre
COPY --from=build /corvus/src-java/corvus-json-schema-bowtie/target/*.jar /harness/
COPY --from=build /corvus/src-java/corvus-json-schema-bowtie/target/lib/ /harness/lib/
CMD ["java", "-cp", "/harness/*:/harness/lib/*", "io.github.corvusdotnet.jsonschema.BowtieHarness"]
