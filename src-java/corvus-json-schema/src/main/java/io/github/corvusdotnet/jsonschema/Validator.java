package io.github.corvusdotnet.jsonschema;

import java.util.Objects;

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
    /** Set (to true) to evaluate with the interpreter instead of compiled code. */
    static final boolean INTERPRET = Boolean.getBoolean("corvus.jsonschema.interpret");

    private final Program program;
    /** The compiled code, or null to interpret. */
    private final CodeGen.Compiled code;
    private final ThreadLocal<Evaluator> evaluators;

    private Validator(Program program) {
        this.program = program;
        this.code = INTERPRET ? null : CodeGen.compile(program);
        this.evaluators = ThreadLocal.withInitial(() -> new Evaluator(program, null));
    }

    /**
     * Validates, failing fast. An evaluation that recursed in place beyond the maximum depth is not valid, whatever it
     * came to: the branch that was abandoned counts as false, which a not above it turns into true.
     */
    private boolean run(Evaluator e, JsonDocument instance) {
        boolean ok =
                code != null ? e.validate(code, instance, instance.root()) : e.validate(instance, instance.root());
        return ok && !e.depthExceeded;
    }

    /** The evaluator last acquired, checked by its owning thread before the (slower) thread-local lookup. */
    private Evaluator last;

    /** The thread's evaluator, or a new one when it is already in use (a validation nested in a format callback). */
    private Evaluator acquire() {
        Evaluator e = last;
        if (e != null && e.owner == Thread.currentThread() && !e.busy) {
            e.busy = true;
            return e;
        }
        e = evaluators.get();
        last = e;
        if (e.busy) {
            return new Evaluator(program, null);
        }
        e.busy = true;
        return e;
    }

    /** The compiled code, or null (for tests). */
    CodeGen.Compiled code() {
        return code;
    }

    /** Whether the schema runs as compiled code (for tests). */
    boolean isCompiled() {
        return code != null;
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
        Evaluator e = acquire();
        try {
            return run(e, instance);
        } finally {
            e.busy = false;
        }
    }

    /**
     * Whether JSON text is a valid instance. The text is parsed into buffers the validator reuses on this thread, so
     * a validation allocates nothing in the steady state.
     *
     * @param json the instance as JSON text
     * @return whether it is valid
     * @throws JsonParseException if the text is not JSON
     */
    public boolean isValid(String json) {
        Evaluator e = acquire();
        try {
            return run(e, e.parseReused(json));
        } finally {
            e.busy = false;
        }
    }

    /**
     * Whether UTF-8 JSON text is a valid instance. The text is parsed into buffers the validator reuses on this
     * thread, so a validation allocates nothing in the steady state.
     *
     * @param utf8 the instance as UTF-8 JSON text
     * @return whether it is valid
     * @throws JsonParseException if the text is not JSON
     */
    public boolean isValid(byte[] utf8) {
        Evaluator e = acquire();
        try {
            return run(e, e.parseReused(utf8, 0, utf8.length));
        } finally {
            e.busy = false;
        }
    }

    /**
     * Whether a range of an array is UTF-8 JSON text that is a valid instance: one document in a larger buffer, such
     * as a record after its framing with more records behind it, validated where it is without a copy. The range must
     * be the whole of one JSON value, with nothing but white space around it, as the array is for
     * {@link #isValid(byte[])}: nothing outside the range is read, and anything else inside it is an error. The text
     * is parsed into buffers the validator reuses on this thread, so a validation allocates nothing in the steady
     * state, whatever the length of the range.
     *
     * <p>The validator keeps a reference to the array (not a copy) until this thread next validates text with it.
     *
     * @param utf8 the array that holds the instance
     * @param offset the index of the first byte of the instance
     * @param length the number of bytes of the instance
     * @return whether it is valid
     * @throws IndexOutOfBoundsException if the range is not within the array
     * @throws JsonParseException if the range is not JSON; its {@link JsonParseException#offset() offset} is counted
     *     from {@code offset}
     */
    public boolean isValid(byte[] utf8, int offset, int length) {
        Objects.checkFromIndexSize(offset, length, utf8.length);
        Evaluator e = acquire();
        try {
            return run(e, e.parseReused(utf8, offset, offset + length));
        } finally {
            e.busy = false;
        }
    }

    /**
     * Whether an instance is valid.
     *
     * @param instance the instance
     * @return whether it is valid
     * @throws SchemaEvaluationDepthException if evaluation recursed in place beyond the maximum depth
     */
    public boolean validate(JsonDocument instance) {
        Evaluator e = acquire();
        try {
            boolean ok = run(e, instance);
            if (e.depthExceeded) {
                throw new SchemaEvaluationDepthException();
            }
            return ok;
        } finally {
            e.busy = false;
        }
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