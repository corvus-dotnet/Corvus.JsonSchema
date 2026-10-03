import io.github.corvusdotnet.jsonschema.JsonDocument;
import io.github.corvusdotnet.jsonschema.Validator;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Paths;
import java.util.List;

/**
 * corvus-json-schema's implementation of the jsonschema-benchmark protocol: parse every instance, compile, validate
 * once cold, warm up, validate once warm. Prints cold,warm,compile,parse in nanoseconds; exits 1 if an instance is
 * invalid.
 *
 * <p>The benchmark defines warm as steady state. The warm-up follows a rule that is the same for every engine whatever
 * its runtime: validation passes for a fixed time ({@value #WARMUP_TIME} ns), and at least {@value #MIN_WARMUP_PASSES}
 * passes.
 */
public final class Main {
    static final long WARMUP_TIME = 2_000_000_000L;
    static final int MIN_WARMUP_PASSES = 100;

    static boolean validateAll(Validator v, JsonDocument[] docs) {
        boolean valid = true;
        for (JsonDocument d : docs) {
            valid &= v.isValid(d);
        }
        return valid;
    }

    public static void main(String[] args) throws Exception {
        String schema = Files.readString(Paths.get(args[0]));
        List<String> lines = Files.readAllLines(Paths.get(args[1]));
        byte[][] texts = lines.stream()
                .filter(l -> !l.isBlank())
                .map(l -> l.getBytes(StandardCharsets.UTF_8))
                .toArray(byte[][]::new);

        long parseStart = System.nanoTime();
        JsonDocument[] docs = new JsonDocument[texts.length];
        for (int i = 0; i < texts.length; i++) {
            docs[i] = JsonDocument.parse(texts[i]);
        }
        long parseEnd = System.nanoTime();

        long compileStart = System.nanoTime();
        Validator v = Validator.compile(schema);
        long compileEnd = System.nanoTime();

        long coldStart = System.nanoTime();
        boolean valid = validateAll(v, docs);
        long coldEnd = System.nanoTime();
        if (!valid) {
            System.exit(1);
        }

        long warmupEnd = System.nanoTime() + WARMUP_TIME;
        for (int i = 0; i < MIN_WARMUP_PASSES || System.nanoTime() < warmupEnd; i++) {
            validateAll(v, docs);
        }

        long warmStart = System.nanoTime();
        validateAll(v, docs);
        long warmEnd = System.nanoTime();

        System.out.println((coldEnd - coldStart) + "," + (warmEnd - warmStart) + "," + (compileEnd - compileStart)
                + "," + (parseEnd - parseStart));
    }
}
