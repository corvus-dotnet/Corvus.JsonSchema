package io.github.corvusdotnet.jsonschema;

import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.io.PrintStream;
import java.nio.charset.StandardCharsets;
import java.util.HashMap;
import java.util.Map;

/**
 * A <a href="https://github.com/bowtie-json-schema/bowtie">Bowtie</a> harness for corvus-json-schema. It speaks IHOP
 * (one JSON request per line on standard input, one response per line on standard output):
 *
 * <ul>
 *   <li>{@code start} reports the implementation and its dialects;
 *   <li>{@code dialect} sets the dialect for schemas without {@code $schema};
 *   <li>{@code run} compiles the case's schema with the case's {@code registry} as the document resolver and
 *       validates each instance (for {@code annotations} output, through a verbose results collector, reporting each
 *       annotation with its instance location and {@code #…} keyword location);
 *   <li>{@code stop} exits.
 * </ul>
 *
 * <p>It lives in the library's package to read requests through the document's own accessors.
 */
public final class BowtieHarness {
    static final String[][] DIALECTS = {
        {"https://json-schema.org/draft/2020-12/schema", "DRAFT202012"},
        {"https://json-schema.org/draft/2019-09/schema", "DRAFT201909"},
        {"http://json-schema.org/draft-07/schema#", "DRAFT7"},
        {"http://json-schema.org/draft-06/schema#", "DRAFT6"},
        {"http://json-schema.org/draft-04/schema#", "DRAFT4"},
    };

    /** The version of the library, from its jar's manifest. */
    static final String VERSION = version();

    private boolean started;
    private Dialect dialect = Dialect.DRAFT202012;

    private static String version() {
        String v = Validator.class.getPackage().getImplementationVersion();
        return v != null ? v : "0.1.0";
    }

    public static void main(String[] args) throws IOException {
        BowtieHarness harness = new BowtieHarness();
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in, StandardCharsets.UTF_8));
        PrintStream out = new PrintStream(System.out, false, StandardCharsets.UTF_8);
        String line;
        while ((line = in.readLine()) != null) {
            if (line.isBlank()) {
                continue;
            }
            String response = harness.handle(line);
            if (response == null) {
                return;
            }
            out.println(response);
            out.flush();
        }
    }

    /** The response to one request; null for {@code stop}. */
    String handle(String line) {
        JsonDocument request = JsonDocument.parse(line);
        int root = request.root();
        String cmd = text(request, request.property(root, "cmd"));
        switch (cmd == null ? "" : cmd) {
            case "start":
                return start(request, root);
            case "dialect":
                requireStarted();
                return dialect(text(request, request.property(root, "dialect")));
            case "run":
                requireStarted();
                return run(request, root);
            case "stop":
                requireStarted();
                return null;
            default:
                throw new IllegalStateException("Unknown command " + cmd);
        }
    }

    private void requireStarted() {
        if (!started) {
            throw new IllegalStateException("Not started");
        }
    }

    private static String text(JsonDocument d, int n) {
        return n >= 0 && d.kind(n) == JsonDocument.STRING ? d.string(n) : null;
    }

    private static String quoted(String s) {
        StringBuilder sb = new StringBuilder();
        JsonDocument.appendQuoted(sb, s);
        return sb.toString();
    }

    private String start(JsonDocument request, int root) {
        int version = request.property(root, "version");
        if (version < 0 || request.kind(version) != JsonDocument.NUMBER || request.doubleValue(version) != 1) {
            throw new IllegalStateException("Unsupported IHOP version");
        }
        started = true;
        StringBuilder dialects = new StringBuilder("[");
        for (int i = 0; i < DIALECTS.length; i++) {
            dialects.append(i == 0 ? "" : ",").append(quoted(DIALECTS[i][0]));
        }
        dialects.append(']');
        return "{\"version\":1,\"implementation\":{"
                + "\"language\":\"java\","
                + "\"name\":\"corvus-jsonschema\","
                + "\"version\":" + quoted(VERSION) + ","
                + "\"homepage\":\"https://github.com/corvus-dotnet/Corvus.JsonSchema\","
                + "\"documentation\":\"https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-java/"
                + "corvus-json-schema\","
                + "\"issues\":\"https://github.com/corvus-dotnet/Corvus.JsonSchema/issues\","
                + "\"source\":\"https://github.com/corvus-dotnet/Corvus.JsonSchema\","
                + "\"dialects\":" + dialects + ","
                + "\"os\":" + quoted(System.getProperty("os.name")) + ","
                + "\"os_version\":" + quoted(System.getProperty("os.version")) + ","
                + "\"language_version\":" + quoted(Runtime.version().toString())
                + "}}";
    }

    private String dialect(String uri) {
        for (String[] d : DIALECTS) {
            if (d[0].equals(uri)) {
                dialect = Dialect.valueOf(d[1]);
                return "{\"ok\":true}";
            }
        }
        return "{\"ok\":false}";
    }

    private static String errored(String message) {
        return "{\"errored\":true,\"context\":{\"message\":" + quoted(String.valueOf(message)) + "}}";
    }

    private static String stripFragment(String uri) {
        int hash = uri.indexOf('#');
        return hash >= 0 ? uri.substring(0, hash) : uri;
    }

    private String run(JsonDocument request, int root) {
        int seq = request.property(root, "seq");
        String seqJson = seq >= 0 ? request.toJson(seq) : "null";
        int c = request.property(root, "case");
        Map<String, String> registry = new HashMap<>();
        int r = request.property(c, "registry");
        if (r >= 0 && request.kind(r) == JsonDocument.OBJECT) {
            int k = request.first(r);
            for (int i = 0; i < request.count(r); i++, k += 2) {
                registry.put(stripFragment(request.string(k)), request.toJson(k + 1));
            }
        }
        CompileOptions options = CompileOptions.builder()
                .defaultDialect(dialect)
                .documentResolver(uri -> {
                    String schema = registry.get(stripFragment(uri));
                    return schema != null ? JsonDocument.parse(schema) : null;
                })
                .build();
        Validator validator;
        try {
            validator = Validator.compile(JsonDocument.parse(request.toJson(request.property(c, "schema"))), options);
        } catch (RuntimeException | StackOverflowError e) {
            return "{\"seq\":" + seqJson + "," + errored(e.toString()).substring(1);
        }
        int output = request.property(root, "output");
        boolean annotations = "annotations".equals(text(request, output));
        StringBuilder results = new StringBuilder("[");
        int tests = request.property(c, "tests");
        for (int i = 0; i < request.count(tests); i++) {
            int test = request.first(tests) + i;
            JsonDocument instance = JsonDocument.parse(request.toJson(request.property(test, "instance")));
            results.append(i == 0 ? "" : ",");
            try {
                if (!annotations) {
                    results.append("{\"valid\":").append(validator.validate(instance)).append('}');
                    continue;
                }
                JsonSchemaResultsCollector collector = JsonSchemaResultsCollector.create(ResultsLevel.VERBOSE);
                boolean valid = validator.evaluate(instance, collector);
                StringBuilder found = new StringBuilder("[");
                boolean first = true;
                for (Annotation a : collector.annotations()) {
                    found.append(first ? "" : ",");
                    first = false;
                    String location = a.schemaLocation() + "/" + a.keyword();
                    found.append("{\"keyword\":").append(quoted(a.keyword().replace("~1", "/").replace("~0", "~")))
                            .append(",\"instanceLocation\":").append(quoted(a.instanceLocation()))
                            .append(",\"keywordLocation\":")
                            .append(quoted(JsonSchemaResultsCollector.schemaLocationFragment(location)))
                            .append(",\"annotation\":").append(a.value()).append('}');
                }
                found.append(']');
                results.append("{\"valid\":").append(valid).append(",\"annotations\":").append(found).append('}');
            } catch (SchemaEvaluationDepthException e) {
                results.append(errored("evaluation recursed beyond the maximum depth"));
            } catch (RuntimeException | StackOverflowError e) {
                results.append(errored(e.toString()));
            }
        }
        results.append(']');
        return "{\"seq\":" + seqJson + ",\"results\":" + results + "}";
    }
}
