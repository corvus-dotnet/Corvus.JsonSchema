/* The C library from C99: every part of the API once, linked against the shared or the static library. */

#include <stdio.h>
#include <stdlib.h>
#include <string.h>

#include "corvus_json_schema.h"

static int failures = 0;

#define CHECK(condition)                                                                   \
    do {                                                                                   \
        if (!(condition)) {                                                                \
            fprintf(stderr, "%s:%d: check failed: %s\n", __FILE__, __LINE__, #condition); \
            failures++;                                                                    \
        }                                                                                  \
    } while (0)

static int equals(cjs_str s, const char *text) {
    return s.len == strlen(text) && (s.len == 0 || memcmp(s.ptr, text, s.len) == 0);
}

static int contains(cjs_str s, const char *text) {
    size_t n = strlen(text);
    for (size_t i = 0; i + n <= s.len; i++) {
        if (memcmp(s.ptr + i, text, n) == 0) {
            return 1;
        }
    }
    return 0;
}

static cjs_validator *compile(const char *schema, const cjs_options *options) {
    cjs_validator *v = NULL;
    cjs_status status = cjs_compile(schema, strlen(schema), options, &v);
    if (status != CJS_OK) {
        cjs_str m = cjs_last_error_message();
        fprintf(stderr, "compile failed (%u): %.*s\n", (unsigned)status, (int)m.len, m.ptr);
        failures++;
    }
    return v;
}

static cjs_status validate(const cjs_validator *v, const char *json, bool *valid) {
    return cjs_validator_validate_json(v, json, strlen(json), valid);
}

/* A format: strings of even length. Counts its calls; freed once. */
typedef struct {
    int calls;
    int *freed;
} counter;

static bool even_length(void *user_data, const char *value, size_t len) {
    (void)value;
    ((counter *)user_data)->calls++;
    return len % 2 == 0;
}

static void free_counter(void *user_data) {
    counter *c = (counter *)user_data;
    (*c->freed)++;
    free(c);
}

static cjs_status resolve(void *user_data, const char *uri, size_t len, cjs_resolved *out) {
    static const char known[] = "http://example.com/positive.json";
    static const char schema[] = "{\"type\": \"integer\", \"minimum\": 1}";
    (void)user_data;
    if (len == sizeof known - 1 && memcmp(uri, known, len) == 0) {
        return cjs_resolved_set_json(out, schema, sizeof schema - 1);
    }
    return CJS_OK;
}

int main(void) {
    bool valid = false;

    /* Version. */
    CHECK(cjs_version() == ((CJS_VERSION_MAJOR << 16) | (CJS_VERSION_MINOR << 8) | CJS_VERSION_PATCH));
    CHECK(cjs_version_string().len > 0);

    /* Compile and validate. */
    cjs_validator *v = compile("{\"type\": \"array\", \"items\": {\"type\": \"integer\"}}", NULL);
    CHECK(validate(v, "[1, 2, 3]", &valid) == CJS_OK && valid);
    CHECK(validate(v, "[1, \"2\"]", &valid) == CJS_OK && !valid);

    /* Errors: invalid JSON with its offset, NULL arguments, invalid UTF-8. */
    CHECK(validate(v, "[1, 2", &valid) == CJS_INVALID_JSON);
    CHECK(cjs_last_error_offset() == 5);
    CHECK(contains(cjs_last_error_message(), "invalid JSON"));
    CHECK(cjs_validator_validate_json(NULL, "1", 1, &valid) == CJS_INVALID_ARGUMENT);
    CHECK(cjs_validator_validate_json(v, "1", 1, NULL) == CJS_INVALID_ARGUMENT);
    CHECK(cjs_validator_validate_json(v, "\"\xff\"", 3, &valid) == CJS_INVALID_UTF8);
    cjs_validator *missing = NULL;
    const char *bad_ref = "{\"$ref\": \"http://example.com/missing.json\"}";
    CHECK(cjs_compile(bad_ref, strlen(bad_ref), NULL, &missing) == CJS_COMPILATION_FAILED && missing == NULL);

    /* A clone shares the schema and outlives the original. */
    cjs_validator *copy = cjs_validator_clone(v);
    cjs_validator_free(v);
    CHECK(validate(copy, "[4]", &valid) == CJS_OK && valid);
    cjs_validator_free(copy);

    /* Options, a format callback and a resolver. */
    int freed = 0;
    counter *c = (counter *)malloc(sizeof *c);
    c->calls = 0;
    c->freed = &freed;
    cjs_options *options = cjs_options_new();
    CHECK(cjs_options_set_assert_format(options, CJS_TRUE) == CJS_OK);
    CHECK(cjs_options_add_format(options, "even", 4, even_length, c, free_counter) == CJS_OK);
    CHECK(cjs_options_set_resolver(options, resolve, NULL, NULL) == CJS_OK);
    CHECK(cjs_options_set_default_dialect(options, 99) == CJS_INVALID_ARGUMENT);
    cjs_validator *f = compile(
        "{\"properties\": {\"code\": {\"format\": \"even\"}, \"count\": {\"$ref\": \"http://example.com/positive.json\"}}}",
        options);
    cjs_options_free(options);
    CHECK(validate(f, "{\"code\": \"ab\", \"count\": 3}", &valid) == CJS_OK && valid);
    CHECK(validate(f, "{\"code\": \"abc\"}", &valid) == CJS_OK && !valid);
    CHECK(validate(f, "{\"count\": 0}", &valid) == CJS_OK && !valid);
    CHECK(c->calls == 2);
    CHECK(freed == 0);
    cjs_validator_free(f);
    CHECK(freed == 1);

    /* Documents and a collector. */
    cjs_validator *named = compile("{\"properties\": {\"name\": {\"type\": \"string\", \"title\": \"Name\"}}}", NULL);
    const char *text = "{\"name\": 1}";
    cjs_document *d = NULL;
    CHECK(cjs_document_parse_borrowed(text, strlen(text), &d) == CJS_OK);
    CHECK(cjs_validator_validate_document(named, d, &valid) == CJS_OK && !valid);
    cjs_collector *results = cjs_collector_new(CJS_DETAILED);
    CHECK(cjs_validator_evaluate_document(named, d, results, &valid) == CJS_OK && !valid);
    /* Among the failures at /name, the type keyword's, with a message. */
    int found = 0;
    for (size_t i = 0; i < cjs_collector_count(results); i++) {
        if (!cjs_collector_is_match(results, i) && equals(cjs_collector_instance_location(results, i), "/name") &&
            equals(cjs_collector_evaluation_location(results, i), "/properties/name/type")) {
            found = 1;
            CHECK(cjs_collector_message(results, i).len > 0);
        }
    }
    CHECK(found);
    cjs_collector_free(results);
    cjs_document_free(d);

    cjs_collector *verbose = cjs_collector_new(CJS_VERBOSE);
    const char *ok = "{\"name\": \"a\"}";
    CHECK(cjs_validator_evaluate_json(named, ok, strlen(ok), verbose, &valid) == CJS_OK && valid);
    cjs_str annotations = {0};
    CHECK(cjs_collector_annotations_json(verbose, &annotations) == CJS_OK);
    CHECK(contains(annotations, "\"#/properties/name\":\"Name\""));
    cjs_collector_free(verbose);
    cjs_validator_free(named);
    CHECK(cjs_collector_new(7) == NULL);

    /* Freeing NULL is allowed. */
    cjs_validator_free(NULL);
    cjs_options_free(NULL);
    cjs_document_free(NULL);
    cjs_collector_free(NULL);

    if (failures == 0) {
        printf("C: all checks passed (corvus_json_schema %.*s)\n", (int)cjs_version_string().len, cjs_version_string().ptr);
    }
    return failures == 0 ? 0 : 1;
}
