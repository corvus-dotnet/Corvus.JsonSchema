package io.github.corvusdotnet.jsonschema;

/**
 * A compiled JSON Schema (draft 4, 6, 7, 2019-09 or 2020-12). Compile once with {@link #compile(String)} (or one of
 * its overloads), then validate any number of instances. A validator is immutable and safe to share between threads.
 *
 * <pre>{@code
 * Validator validator = Validator.compile("""
 *     {"type": "object", "properties": {"id": {"type": "integer", "minimum": 1}}, "required": ["id"]}
 *     """);
 * validator.isValid("{\"id\": 3}"); // true
 * validator.isValid("{\"id\": 0}"); // false
 * }</pre>
 */
public final class Validator {
    private final Program program;
    private final ThreadLocal<Evaluator> evaluators;

    private Validator(Program program) {
        this.program = program;
        this.evaluators = ThreadLocal.withInitial(() -> new Evaluator(program, null));
    }

    /**
     * Compiles a schema with the default options.
     *
     * @param schema the schema as JSON text
     * @return the validator
     * @throws JsonParseException if the text is not JSON
     * @throws SchemaCompilationException if the schema cannot be compiled
     */
    public static Validator compile(String schema) {
        return compile(JsonDocument.parse(schema), CompileOptions.DEFAULT);
    }

    /**
     * Compiles a schema.
     *
     * @param schema the schema as JSON text
     * @param options the options
     * @return the validator
     * @throws JsonParseException if the text is not JSON
     * @throws SchemaCompilationException if the schema cannot be compiled
     */
    public static Validator compile(String schema, CompileOptions options) {
        return compile(JsonDocument.parse(schema), options);
    }

    /**
     * Compiles a schema with the default options.
     *
     * @param schema the schema document
     * @return the validator
     * @throws SchemaCompilationException if the schema cannot be compiled
     */
    public static Validator compile(JsonDocument schema) {
        return compile(schema, CompileOptions.DEFAULT);
    }

    /**
     * Compiles a schema.
     *
     * @param schema the schema document
     * @param options the options
     * @return the validator
     * @throws SchemaCompilationException if the schema cannot be compiled
     */
    public static Validator compile(JsonDocument schema, CompileOptions options) {
        return new Validator(new Program(SchemaCompiler.compile(schema, options), options));
    }

    /**
     * Compiles the schema document at a URI, fetched through the options' {@link DocumentResolver} (or one of the
     * standard metaschemas).
     *
     * @param uri the absolute URI of the schema
     * @param options the options
     * @return the validator
     * @throws SchemaCompilationException if the schema cannot be resolved or compiled
     */
    public static Validator compileFromUri(String uri, CompileOptions options) {
        return new Validator(new Program(SchemaCompiler.compileFromUri(uri, options), options));
    }

    /**
     * Whether an instance is valid. A schema that recursed in place beyond the maximum depth is reported as invalid;
     * use {@link #validate(JsonDocument)} to tell the two apart.
     *
     * @param instance the instance
     * @return whether it is valid
     */
    public boolean isValid(JsonDocument instance) {
        return evaluators.get().validate(instance, instance.root());
    }

    /**
     * Whether JSON text is a valid instance.
     *
     * @param json the instance as JSON text
     * @return whether it is valid
     * @throws JsonParseException if the text is not JSON
     */
    public boolean isValid(String json) {
        return isValid(JsonDocument.parse(json));
    }

    /**
     * Whether UTF-8 JSON text is a valid instance.
     *
     * @param utf8 the instance as UTF-8 JSON text
     * @return whether it is valid
     * @throws JsonParseException if the text is not JSON
     */
    public boolean isValid(byte[] utf8) {
        return isValid(JsonDocument.parse(utf8));
    }

    /**
     * Whether an instance is valid.
     *
     * @param instance the instance
     * @return whether it is valid
     * @throws SchemaEvaluationDepthException if evaluation recursed in place beyond the maximum depth
     */
    public boolean validate(JsonDocument instance) {
        Evaluator e = evaluators.get();
        boolean ok = e.validate(instance, instance.root());
        if (e.depthExceeded) {
            throw new SchemaEvaluationDepthException();
        }
        return ok;
    }

    /**
     * Evaluates an instance exhaustively, reporting to a collector.
     *
     * @param instance the instance
     * @param collector the collector
     * @return whether the instance is valid
     * @throws SchemaEvaluationDepthException if evaluation recursed in place beyond the maximum depth
     */
    public boolean evaluate(JsonDocument instance, JsonSchemaResultsCollector collector) {
        Evaluator e = new Evaluator(program, collector);
        boolean ok = e.evaluate(instance, instance.root());
        if (e.depthExceeded) {
            throw new SchemaEvaluationDepthException();
        }
        return ok;
    }
}