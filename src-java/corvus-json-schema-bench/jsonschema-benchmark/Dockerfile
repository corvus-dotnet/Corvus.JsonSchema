FROM maven:3.9-eclipse-temurin-21 AS build
COPY corvus-json-schema /src/corvus-json-schema
RUN cd /src/corvus-json-schema && mvn -q -B install -DskipTests
COPY Main.java /src/bench/Main.java
RUN mkdir -p /app/classes && cp /root/.m2/repository/io/github/corvus-dotnet/corvus-json-schema/*/corvus-json-schema-*.jar /app/corvus-json-schema.jar \
 && javac -d /app/classes -cp /app/corvus-json-schema.jar /src/bench/Main.java

FROM eclipse-temurin:21-jre
RUN apt-get update && apt-get install -y --no-install-recommends time && rm -rf /var/lib/apt/lists/*
COPY --from=build /app /app
COPY memory-wrapper.sh /app/memory-wrapper.sh
ENTRYPOINT ["/app/memory-wrapper.sh", "java", "-cp", "/app/classes:/app/corvus-json-schema.jar", "Main"]
CMD []
