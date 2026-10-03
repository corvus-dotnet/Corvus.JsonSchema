package io.github.corvusdotnet.jsonschema;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;
import java.util.function.Supplier;

/**
 * Collects the results of an evaluation: a port of Corvus.Text.Json's {@code JsonSchemaResultsCollector} and
 * {@code JsonSchemaAnnotationProducer}.
 *
 * <p>The evaluator opens a context per subschema application and writes keyword rows into the open context. Closing a
 * context either commits it (a summary row, then its own rows newest first, after its committed descendants) or pops
 * it (everything it and its descendants wrote is discarded). The level decides which rows exist and which carry
 * message text: {@link ResultsLevel#BASIC} has failures without text, {@link ResultsLevel#DETAILED} adds text to
 * failures, {@link ResultsLevel#VERBOSE} keeps every row with text, annotations included.
 *
 * <p>A collector is used by one evaluation at a time.
 */
public final class JsonSchemaResultsCollector {
    private static final class Frame {
        final int evalLength;
        final String schemaPath;
        final int docLength;
        final int commitIndex;
        final int rowsStart;

        Frame(int evalLength, String schemaPath, int docLength, int commitIndex, int rowsStart) {
            this.evalLength = evalLength;
            this.schemaPath = schemaPath;
            this.docLength = docLength;
            this.commitIndex = commitIndex;
            this.rowsStart = rowsStart;
        }
    }

    private final ResultsLevel level;
    private final List<SchemaResult> committed = new ArrayList<>();
    private final List<Frame> frames = new ArrayList<>();
    /** The rows written into open frames (each frame owns the tail from its rowsStart). */
    private final List<SchemaResult> pending = new ArrayList<>();
    private final StringBuilder evalPath = new StringBuilder();
    private String schemaPath = "";
    private final StringBuilder docPath = new StringBuilder();

    private JsonSchemaResultsCollector(ResultsLevel level) {
        this.level = level;
    }

    /**
     * Creates a collector.
     *
     * @param level what to record
     * @return the collector
     */
    public static JsonSchemaResultsCollector create(ResultsLevel level) {
        return new JsonSchemaResultsCollector(level);
    }

    /**
     * The level.
     *
     * @return the level
     */
    public ResultsLevel level() {
        return level;
    }

    /**
     * The results, in commit order.
     *
     * @return the results
     */
    public List<SchemaResult> results() {
        return Collections.unmodifiableList(committed);
    }

    /** Clears the results, for reuse with another evaluation. */
    public void reset() {
        committed.clear();
        frames.clear();
        pending.clear();
        evalPath.setLength(0);
        schemaPath = "";
        docPath.setLength(0);
    }

    // ----------------------------------------------------------------------------------------------------------------
    // The evaluator's side

    /**
     * Opens a child context. The evaluation path is extended by evalSegment (verbatim), the schema path is replaced by
     * schemaLocation, and the document path is extended by docSegment (already pointer-encoded); null leaves each as
     * it is.
     */
    void beginChildContext(String evalSegment, String schemaLocation, String docSegment) {
        frames.add(new Frame(evalPath.length(), schemaPath, docPath.length(), committed.size(), pending.size()));
        if (evalSegment != null) {
            evalPath.append('/').append(evalSegment);
        }
        if (schemaLocation != null) {
            schemaPath = schemaLocation;
        }
        if (docSegment != null) {
            docPath.append('/').append(docSegment);
        }
    }

    /**
     * Closes a child context. When the parent does not need the child's results (parentIsMatch) they are discarded
     * below Verbose; otherwise the context's summary row is written and its rows are committed.
     */
    void commitChildContext(boolean parentIsMatch, boolean childIsMatch, String message) {
        if (parentIsMatch && level != ResultsLevel.VERBOSE) {
            popChildContext();
            return;
        }
        pending.add(row(childIsMatch, message, null, evalPath.toString(), schemaPath, docPath.toString()));
        Frame frame = frames.remove(frames.size() - 1);
        List<SchemaResult> rows = pending.subList(frame.rowsStart, pending.size());
        for (int i = rows.size() - 1; i >= 0; i--) {
            committed.add(rows.get(i));
        }
        rows.clear();
        restore(frame);
    }

    /** Closes a child context and discards everything it and its descendants wrote. */
    void popChildContext() {
        Frame frame = frames.remove(frames.size() - 1);
        committed.subList(frame.commitIndex, committed.size()).clear();
        pending.subList(frame.rowsStart, pending.size()).clear();
        restore(frame);
    }

    boolean recordsPasses() {
        return level == ResultsLevel.VERBOSE;
    }

    void evaluatedKeyword(boolean isMatch, String message, Supplier<String> lazy, String keyword) {
        if (!isMatch || level == ResultsLevel.VERBOSE) {
            String k = Uris.escapePointerToken(keyword);
            pending.add(row(isMatch, message, lazy, evalPath + "/" + k, schemaPath + "/" + k, docPath.toString()));
        }
    }

    void evaluatedKeywordForProperty(
            boolean isMatch, String message, Supplier<String> lazy, String propertyName, String keyword) {
        if (!isMatch || level == ResultsLevel.VERBOSE) {
            String k = Uris.escapePointerToken(keyword);
            pending.add(row(
                    isMatch,
                    message,
                    lazy,
                    evalPath + "/" + k,
                    schemaPath + "/" + k,
                    docPath + "/" + Uris.escapePointerToken(propertyName)));
        }
    }

    /** An annotation: Verbose only; the keyword extends the evaluation path but not the schema path. */
    void ignoredKeyword(Supplier<String> lazy, String keyword) {
        if (level == ResultsLevel.VERBOSE) {
            pending.add(row(
                    true,
                    null,
                    lazy,
                    evalPath + "/" + Uris.escapePointerToken(keyword),
                    schemaPath,
                    docPath.toString()));
        }
    }

    void evaluatedBooleanSchema(boolean isMatch) {
        if (!isMatch || level == ResultsLevel.VERBOSE) {
            pending.add(row(isMatch, null, null, evalPath.toString(), schemaPath, docPath.toString()));
        }
    }

    private SchemaResult row(
            boolean isMatch,
            String message,
            Supplier<String> lazy,
            String evaluationLocation,
            String schemaEvaluationLocation,
            String documentEvaluationLocation) {
        boolean withText = level == ResultsLevel.VERBOSE || (!isMatch && level != ResultsLevel.BASIC);
        String text = "";
        if (withText) {
            if (message != null) {
                text = message;
            } else if (lazy != null) {
                text = lazy.get();
            }
        }
        return new SchemaResult(isMatch, text, evaluationLocation, schemaEvaluationLocation, documentEvaluationLocation);
    }

    private void restore(Frame frame) {
        evalPath.setLength(frame.evalLength);
        schemaPath = frame.schemaPath;
        docPath.setLength(frame.docLength);
    }

    // ----------------------------------------------------------------------------------------------------------------
    // Annotations

    /**
     * The annotations in a verbose collector's results ({@code JsonSchemaAnnotationProducer.EnumerateAnnotations}).
     *
     * @return the annotations, in result order
     */
    public List<Annotation> annotations() {
        List<Annotation> out = new ArrayList<>();
        for (SchemaResult r : committed) {
            if (!r.isMatch() || r.message().isEmpty()) {
                continue;
            }
            String eval = r.evaluationLocation();
            int slash = eval.lastIndexOf('/');
            if (slash < 0 || eval.equals(r.schemaEvaluationLocation())) {
                continue;
            }
            String keyword = eval.substring(slash + 1);
            char first = r.message().charAt(0);
            if (keyword.isEmpty()
                    || !(first == '"' || first == '{' || first == '[' || first == 't' || first == 'f' || first == 'n'
                            || first == '-' || (first >= '0' && first <= '9'))) {
                continue;
            }
            out.add(new Annotation(r.documentEvaluationLocation(), keyword, r.schemaEvaluationLocation(), r.message()));
        }
        return out;
    }

    /**
     * Annotations grouped by instance location, then keyword, then schema location fragment, with each value as JSON
     * text ({@code JsonSchemaAnnotationProducer.WriteAnnotationsTo}).
     *
     * @return the grouped annotations
     */
    public Map<String, Map<String, Map<String, String>>> collectAnnotations() {
        Map<String, Map<String, Map<String, String>>> out = new TreeMap<>();
        for (Annotation a : annotations()) {
            out.computeIfAbsent(a.instanceLocation(), k -> new TreeMap<>())
                    .computeIfAbsent(a.keyword(), k -> new TreeMap<>())
                    .put(schemaLocationFragment(a.schemaLocation()), a.value());
        }
        return out;
    }

    /**
     * {@code #} followed by the schema location, percent-encoded as a URI fragment (upper-case hex, UTF-8).
     *
     * @param schemaLocation a JSON pointer
     * @return the fragment
     */
    public static String schemaLocationFragment(String schemaLocation) {
        StringBuilder out = new StringBuilder("#");
        for (byte b : schemaLocation.getBytes(StandardCharsets.UTF_8)) {
            int c = b & 0xff;
            if (c < 128 && (Character.isLetterOrDigit(c) || "-._~!$&'()*+,;=:@/?".indexOf(c) >= 0)) {
                out.append((char) c);
            } else {
                out.append('%').append(String.format("%02X", c));
            }
        }
        return out.toString();
    }
}