FROM maven:3.9-eclipse-temurin-25 AS build
COPY corvus-json-schema /src/corvus-json-schema
RUN cd /src/corvus-json-schema && mvn -q -B install -DskipTests
COPY Main.java Train.java /src/bench/
RUN mkdir -p /app/classes \
 && cp /root/.m2/repository/io/github/corvus-dotnet/corvus-json-schema/*/corvus-json-schema-*.jar /app/corvus-json-schema.jar \
 && javac -d /app/classes -cp /app/corvus-json-schema.jar /src/bench/Main.java /src/bench/Train.java \
 && jar cf /app/bench.jar -C /app/classes . && rm -rf /app/classes

FROM eclipse-temurin:25-jre
RUN apt-get update && apt-get install -y --no-install-recommends time && rm -rf /var/lib/apt/lists/*
COPY --from=build /app /app
# An ahead-of-time cache of the classes and method profiles of a training run on the standard metaschemas.
RUN java -XX:AOTCacheOutput=/app/app.aot -cp /app/bench.jar:/app/corvus-json-schema.jar Train
COPY memory-wrapper.sh /app/memory-wrapper.sh
ENTRYPOINT ["/app/memory-wrapper.sh", "java", "-XX:AOTCache=/app/app.aot", "-cp", "/app/bench.jar:/app/corvus-json-schema.jar", "Main"]
CMD []
