FROM maven:3.9-eclipse-temurin-25 AS build

# The library's Maven Central metadata: Docker downloads it on every build and reuses the layers below only while it is
# unchanged, so a new corvus-json-schema release is picked up by the next build.
ADD https://repo1.maven.org/maven2/io/github/corvus-dotnet/corvus-json-schema/maven-metadata.xml /tmp/corvus-metadata.xml

COPY pom.xml /app/pom.xml
COPY src /app/src
RUN cd /app && mvn -B -q -ntp package \
 && mvn -B -q -ntp dependency:list -DoutputFile=/app/deps.txt \
 && sed -n 's/.*io.github.corvus-dotnet:corvus-json-schema:jar:\([^:]*\):.*/\1/p' /app/deps.txt > /app/corvus-version

FROM eclipse-temurin:25-jre
RUN apt-get update && apt-get install -y --no-install-recommends time && rm -rf /var/lib/apt/lists/*
COPY --from=build /app/target/bench.jar /app/target/lib/ /app/
COPY --from=build /app/corvus-version /app/corvus-version
# An ahead-of-time cache (JDK 25) of the classes and method profiles of a training run on the standard metaschemas.
RUN java -XX:AOTCacheOutput=/app/app.aot -cp /app/bench.jar:/app/corvus-json-schema.jar Train
COPY memory-wrapper.sh /app/memory-wrapper.sh
ENTRYPOINT ["/app/memory-wrapper.sh", "java", "-XX:AOTCache=/app/app.aot", "-cp", "/app/bench.jar:/app/corvus-json-schema.jar", "Main"]
CMD []
