import io.github.corvusdotnet.jsonschema.JsonDocument;
import io.github.corvusdotnet.jsonschema.Validator;
import java.io.InputStream;
import java.util.ArrayList;
import java.util.List;

/**
 * The training run for the image's ahead-of-time cache (JDK 25): compiles each embedded standard metaschema and
 * validates every metaschema against it, so that the cache holds the classes and profiles of parsing, compiling and
 * validating. It uses nothing from the benchmark's corpora.
 */
public final class Train {
    private static final String[] METASCHEMAS = {
        "draft4/schema.json", "draft6/schema.json", "draft7/schema.json", "draft2019-09/schema.json",
        "draft2020-12/schema.json", "draft2019-09/meta/applicator.json", "draft2019-09/meta/content.json",
        "draft2019-09/meta/core.json", "draft2019-09/meta/format.json", "draft2019-09/meta/meta-data.json",
        "draft2019-09/meta/validation.json", "draft2020-12/meta/applicator.json", "draft2020-12/meta/content.json",
        "draft2020-12/meta/core.json", "draft2020-12/meta/format-annotation.json",
        "draft2020-12/meta/meta-data.json", "draft2020-12/meta/unevaluated.json",
        "draft2020-12/meta/validation.json",
    };

    public static void main(String[] args) throws Exception {
        List<byte[]> texts = new ArrayList<>();
        for (String m : METASCHEMAS) {
            try (InputStream in = Train.class.getClassLoader()
                    .getResourceAsStream("io/github/corvusdotnet/jsonschema/metaschemas/" + m)) {
                texts.add(in.readAllBytes());
            }
        }
        long end = System.nanoTime() + 3_000_000_000L;
        int valid = 0;
        while (System.nanoTime() < end) {
            List<JsonDocument> docs = new ArrayList<>();
            for (byte[] t : texts) {
                docs.add(JsonDocument.parse(t));
            }
            for (JsonDocument schema : docs) {
                Validator v = Validator.compile(schema);
                for (JsonDocument d : docs) {
                    if (v.isValid(d)) {
                        valid++;
                    }
                }
            }
        }
        System.out.println(valid);
    }
}
