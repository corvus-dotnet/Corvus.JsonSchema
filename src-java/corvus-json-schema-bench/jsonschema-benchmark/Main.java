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
 */
public final class Main {
    static final int WARMUP_ITERATIONS = 1000;
    static final long MAX_WARMUP_TIME = 10_000_000_000L;

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

        long iterations = (long) Math.ceil((double) MAX_WARMUP_TIME / (coldEnd - coldStart));
        for (long i = 0; i < Math.min(iterations, WARMUP_ITERATIONS); i++) {
            validateAll(v, docs);
        }

        long warmStart = System.nanoTime();
        validateAll(v, docs);
        long warmEnd = System.nanoTime();

        System.out.println((coldEnd - coldStart) + "," + (warmEnd - warmStart) + "," + (compileEnd - compileStart)
                + "," + (parseEnd - parseStart));
    }
}
