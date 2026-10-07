package io.github.corvusdotnet.jsonschema.bench;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import com.networknt.schema.JsonSchemaFactory;
import com.networknt.schema.OutputFormat;
import com.networknt.schema.SchemaValidatorsConfig;
import com.networknt.schema.SpecVersion;
import com.networknt.schema.regex.GraalJSRegularExpressionFactory;
import io.github.corvusdotnet.jsonschema.JsonDocument;
import io.github.corvusdotnet.jsonschema.Validator;
import io.github.optimumcode.json.schema.JsonSchema;
import io.github.optimumcode.json.schema.OutputCollector;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.stream.Collectors;
import java.util.stream.Stream;
import kotlinx.serialization.json.Json;
import kotlinx.serialization.json.JsonElement;

/**
 * Validates the jsonschema-benchmark corpora with corvus-json-schema and the other Java validators the benchmark runs
 * (networknt, kmp), in one process. The engines' timed passes are interleaved, so drift in the machine's speed affects
 * them alike.
 *
 * <pre>
 * java -cp ... Compare --schemas ../../../jsonschema-benchmark/schemas [--only a,b] [--engines corvus,networknt]
 *     [--budget-ms 1000]
 * </pre>
 */
public final class Compare {
    /** One engine on one corpus: compiled and with its instances parsed. */
    interface Prepared {
        /** Validates every instance; returns how many were valid. */
        int pass();
    }

    interface Engine {
        String name();

        Prepared prepare(String schema, List<String> lines) throws Exception;
    }

    static final class Corvus implements Engine {
        @Override
        public String name() {
            return "corvus";
        }

        @Override
        public Prepared prepare(String schema, List<String> lines) {
            Validator v = Validator.compile(schema);
            JsonDocument[] docs = lines.stream()
                    .map(l -> JsonDocument.parse(l.getBytes(StandardCharsets.UTF_8)))
                    .toArray(JsonDocument[]::new);
            return () -> {
                int ok = 0;
                for (JsonDocument d : docs) {
                    if (v.isValid(d)) {
                        ok++;
                    }
                }
                return ok;
            };
        }
    }

    static final class Networknt implements Engine {
        @Override
        public String name() {
            return "networknt";
        }

        @Override
        public Prepared prepare(String schema, List<String> lines) throws Exception {
            JsonSchemaFactory factory = JsonSchemaFactory.getInstance(SpecVersion.VersionFlag.V202012);
            SchemaValidatorsConfig config = SchemaValidatorsConfig.builder()
                    .regularExpressionFactory(GraalJSRegularExpressionFactory.getInstance())
                    .build();
            com.networknt.schema.JsonSchema s = factory.getSchema(schema, config);
            ObjectMapper mapper = new ObjectMapper();
            List<JsonNode> docs = new ArrayList<>();
            for (String l : lines) {
                docs.add(mapper.readTree(l));
            }
            return () -> {
                int ok = 0;
                for (JsonNode d : docs) {
                    if (s.validate(d, OutputFormat.BOOLEAN)) {
                        ok++;
                    }
                }
                return ok;
            };
        }
    }

    static final class Kmp implements Engine {
        @Override
        public String name() {
            return "kmp";
        }

        @Override
        public Prepared prepare(String schema, List<String> lines) {
            JsonSchema s = JsonSchema.Companion.fromDefinition(schema);
            List<JsonElement> docs = lines.stream()
                    .map(l -> Json.Default.parseToJsonElement(l))
                    .collect(Collectors.toList());
            return () -> {
                int ok = 0;
                for (JsonElement d : docs) {
                    if (s.validate(d, OutputCollector.Companion.flag()).getValid()) {
                        ok++;
                    }
                }
                return ok;
            };
        }
    }

    static final Map<String, Engine> ENGINES = new LinkedHashMap<>();

    static {
        for (Engine e : new Engine[] {new Corvus(), new Networknt(), new Kmp()}) {
            ENGINES.put(e.name(), e);
        }
    }

    public static void main(String[] args) throws Exception {
        Path schemas = Paths.get("../../../jsonschema-benchmark/schemas");
        List<String> only = null;
        List<String> engines = new ArrayList<>(ENGINES.keySet());
        long budgetMs = 1000;
        for (int i = 0; i < args.length; i++) {
            switch (args[i]) {
                case "--schemas":
                    schemas = Paths.get(args[++i]);
                    break;
                case "--only":
                    only = Arrays.asList(args[++i].split(","));
                    break;
                case "--engines":
                    engines = Arrays.asList(args[++i].split(","));
                    break;
                case "--budget-ms":
                    budgetMs = Long.parseLong(args[++i]);
                    break;
                default:
                    throw new IllegalArgumentException("unknown argument " + args[i]);
            }
        }
        List<Path> corpora;
        try (Stream<Path> s = Files.list(schemas)) {
            corpora = s.filter(Files::isDirectory).sorted().collect(Collectors.toList());
        }
        Map<String, List<Double>> ratios = new LinkedHashMap<>();
        System.out.printf(Locale.ROOT, "%-24s %8s", "corpus", "count");
        for (String e : engines) {
            System.out.printf(Locale.ROOT, " %14s", e);
        }
        System.out.println();
        for (Path dir : corpora) {
            String name = dir.getFileName().toString();
            if (only != null && !only.contains(name)) {
                continue;
            }
            String schema = Files.readString(dir.resolve("schema-noformat.json"));
            List<String> lines = Files.readAllLines(dir.resolve("instances.jsonl")).stream()
                    .filter(l -> !l.isBlank())
                    .collect(Collectors.toList());
            Prepared[] prepared = new Prepared[engines.size()];
            String[] notes = new String[engines.size()];
            for (int i = 0; i < engines.size(); i++) {
                try {
                    prepared[i] = ENGINES.get(engines.get(i)).prepare(schema, lines);
                    int valid = prepared[i].pass();
                    if (valid != lines.size()) {
                        notes[i] = "invalid " + (lines.size() - valid);
                        prepared[i] = null;
                    }
                } catch (Throwable t) {
                    notes[i] = "error";
                    prepared[i] = null;
                }
            }
            // Warm up each engine for the budget, then interleave timed passes for the budget again.
            for (Prepared p : prepared) {
                if (p != null) {
                    long end = System.nanoTime() + budgetMs * 1_000_000L;
                    int n = 0;
                    while (System.nanoTime() < end && n < 1000) {
                        p.pass();
                        n++;
                    }
                }
            }
            List<List<Long>> samples = new ArrayList<>();
            for (int i = 0; i < prepared.length; i++) {
                samples.add(new ArrayList<>());
            }
            long end = System.nanoTime() + budgetMs * 1_000_000L * prepared.length;
            int rounds = 0;
            while ((System.nanoTime() < end || rounds < 5) && rounds < 2000) {
                for (int i = 0; i < prepared.length; i++) {
                    if (prepared[i] != null) {
                        long t0 = System.nanoTime();
                        prepared[i].pass();
                        samples.get(i).add(System.nanoTime() - t0);
                    }
                }
                rounds++;
            }
            System.out.printf(Locale.ROOT, "%-24s %8d", name, lines.size());
            double corvus = Double.NaN;
            for (int i = 0; i < prepared.length; i++) {
                if (prepared[i] == null) {
                    System.out.printf(Locale.ROOT, " %14s", notes[i]);
                    continue;
                }
                List<Long> s = samples.get(i);
                s.sort(null);
                double median = s.get(s.size() / 2) / 1000.0;
                System.out.printf(Locale.ROOT, " %11.1f us", median);
                if (i == 0) {
                    corvus = median;
                } else if (!Double.isNaN(corvus)) {
                    ratios.computeIfAbsent(engines.get(i), k -> new ArrayList<>()).add(corvus / median);
                }
            }
            System.out.println();
        }
        for (Map.Entry<String, List<Double>> e : ratios.entrySet()) {
            double log = 0;
            int faster = 0;
            for (double r : e.getValue()) {
                log += Math.log(r);
                if (r < 1) {
                    faster++;
                }
            }
            System.out.printf(Locale.ROOT, "%s / %s: geomean %.3f, faster on %d of %d%n", engines.get(0), e.getKey(),
                    Math.exp(log / e.getValue().size()), faster, e.getValue().size());
        }
    }

    private Compare() {
    }

    static {
        // Keep GraalJS from warning about running on the interpreter.
        System.setProperty("polyglot.engine.WarnInterpreterOnly", "false");
    }

    static String read(Path p) throws IOException {
        return Files.readString(p);
    }
}