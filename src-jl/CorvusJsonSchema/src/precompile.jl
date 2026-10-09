# The precompile workload. Julia compiles a method the first time it runs, so without this the first validation in a
# process would wait for the compiler. The workload compiles schemas that between them use every kind of plan, and
# validates and evaluates instances against them by every entry point, while the package is being precompiled. The
# code this compiles is kept in the package image, and a process that loads the package starts with it.
#
# The workload runs only during precompilation. It leaves nothing behind but compiled code: the caches it filled
# are emptied, since a compiled pattern of the engine holds memory of the process that made it.

function precompile_workload()
    large = join(["\"p$i\": $i" for i in 0:79], ",")
    cases = Tuple{String,NamedTuple,Vector{String}}[
        ("""{
            "type": "object",
            "properties": {
                "name": { "type": "string", "minLength": 1, "title": "Name" },
                "tags": { "type": "array", "items": { "type": "string" }, "maxItems": 20 },
                "position": { "type": "array", "items": { "type": "array", "minItems": 2, "items": {
                    "type": "number" } } },
                "nested": { "type": "object", "properties": { "id": { "type": "integer" } }, "required": ["id"] },
                "map": { "additionalProperties": { "type": "integer" } },
                "kind": { "enum": ["a", "b", "c"] }
            },
            "required": ["name"],
            "additionalProperties": false
        }""", (;), [
            """{"name": "a", "tags": ["x", "y\\nz"], "position": [[1, 2.5]], "nested": {"id": 1}, "map": {"k": 1},
                "kind": "b"}""",
            """{"name": "", "tags": []}""",
            """{"name": "café", "tags": ["1", 2], "other": {"deep": [1, [2, [3]]]}}""",
            """{"nested": {"id": 1.5, "a-property-name-that-is-long": null}, "kind": "z"}"""]),
        ("""{
            "type": "object",
            "properties": {
                "n": { "type": "number", "minimum": 0, "exclusiveMaximum": 100, "multipleOf": 0.25 },
                "m": { "type": "integer", "maximum": 10, "exclusiveMinimum": -1, "multipleOf": 2 },
                "s": { "type": "string", "maxLength": 5, "pattern": "^[a-z]+\$" },
                "t": { "pattern": "^(ab|cd)\$" },
                "w": { "pattern": "^([a-z]+)(,[a-z]+)*\$" },
                "v": { "pattern": "^a|[0-9]{2}\$" },
                "e": { "enum": ["a", "b", 1, null] },
                "c": { "const": { "k": [1, 2] } },
                "u": { "type": "array", "uniqueItems": true, "contains": { "type": "integer" }, "minContains": 1 },
                "o": { "oneOf": [{ "type": "string" }, { "type": "integer" }, { "required": ["x"] }] },
                "a": { "anyOf": [{ "minimum": 5 }, { "maxLength": 2 }] },
                "i": { "if": { "type": "integer" }, "then": { "minimum": 1 }, "else": { "type": "string" } },
                "x": { "not": { "type": "null" } },
                "d": { "oneOf": [
                    { "properties": { "kind": { "const": "circle" }, "r": { "type": "number" } },
                        "required": ["kind"] },
                    { "properties": { "kind": { "const": "square" }, "side": { "type": "number" } },
                        "required": ["kind"] }] },
                "p": { "prefixItems": [{ "type": "string" }], "items": { "type": "integer" } }
            },
            "patternProperties": { "^x-": { "type": "boolean" } },
            "additionalProperties": { "type": "integer" },
            "propertyNames": { "maxLength": 10 },
            "dependentRequired": { "n": ["s"] },
            "dependentSchemas": { "m": { "required": ["n"] } },
            "minProperties": 1
        }""", (;), [
            """{"n": 1.5, "m": 4, "s": "abc", "t": "ab", "w": "a,b", "v": "x12", "e": "b", "c": {"k": [1, 2.0]},
                "u": [1, "a", [2], {"b": 1}], "o": 3, "a": "ab", "i": 2, "x": 0, "d": {"kind": "circle", "r": 1},
                "p": ["a", 1], "x-flag": true, "extra": 7}""",
            """{"n": 1.3, "s": "abc"}""",
            """{"u": [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 18, "a", "a"]}""",
            """{"o": {"x": 1}, "a": 3, "i": "s", "x": null, "d": {"kind": "square", "side": "1"}}""",
            """{"a-name-that-is-too-long": 1, "s": "é", "e": 12345678901234567890, "n": 1e300}"""]),
        ("""{
            "\$defs": { "base": { "properties": { "a": { "type": "integer" } }, "patternProperties": {
                "^x-": true } } },
            "allOf": [{ "\$ref": "#/\$defs/base" }],
            "anyOf": [{ "properties": { "b": true }, "required": ["b"] }, { "properties": { "c": true } }],
            "oneOf": [{ "properties": { "d": { "type": "string" } } }, { "properties": { "d": {
                "type": "integer" } } }],
            "properties": {
                "list": { "prefixItems": [true], "contains": { "type": "string" }, "unevaluatedItems": false },
                "tuple": { "prefixItems": [{ "type": "integer" }], "unevaluatedItems": { "type": "string" } }
            },
            "unevaluatedProperties": false
        }""", (;), [
            """{"a": 1, "b": 2, "d": "s", "x-y": null, "list": [1, "a", "b"], "tuple": [1, "a"]}""",
            """{"a": 1, "c": 2, "d": 3, "list": [1, "a", 2]}""",
            """{"a": 1, "e": 2}"""]),
        ("""{
            "type": "object",
            "properties": { "kind": { "enum": ["a", "b"] }, "value": true, "other": { "type": "string" } },
            "allOf": [{ "\$ref": "#/\$defs/ext" }, { "properties": { "c": { "type": "integer" } } }],
            "if": { "properties": { "kind": { "const": "a" } }, "required": ["kind"] },
            "then": { "required": ["value"], "properties": { "extra": { "type": "integer" } } },
            "else": { "not": { "required": ["value", "other"] } },
            "dependentSchemas": { "c": { "properties": { "d": true } } },
            "unevaluatedProperties": false,
            "\$defs": { "ext": { "patternProperties": { "^x-": true } } }
        }""", (;), [
            """{"kind": "a", "value": 1, "extra": 2, "x-a": 1}""",
            """{"kind": "b", "value": 1, "c": 3, "d": 4}""",
            """{"kind": "b", "value": 1, "other": "x"}""",
            """{"kind": "a"}""",
            "{\"kind\": \"a\", \"value\": 1, " * large * "}"]),
        ("""{
            "allOf": [
                { "properties": { "a": { "type": "string" }, "b": true }, "required": ["a"], "maxProperties": 90 },
                { "properties": { "c": { "type": "integer" } } }],
            "properties": { "e": { "type": "boolean" } }
        }""", (;), ["""{"a": "x", "c": 1, "e": true}""", """{"c": "1"}""", "{" * large * "}"]),
        ("""{
            "\$schema": "https://json-schema.org/draft/2020-12/schema",
            "\$id": "https://example.com/strict-tree",
            "\$dynamicAnchor": "node",
            "\$ref": "tree",
            "unevaluatedProperties": false,
            "\$defs": {
                "tree": {
                    "\$id": "https://example.com/tree",
                    "\$dynamicAnchor": "node",
                    "type": "object",
                    "properties": { "data": true, "children": { "type": "array", "items": {
                        "\$dynamicRef": "#node" } } }
                }
            }
        }""", (;), [
            """{"children": [{"data": 1, "children": [{"data": [1, 2, 3]}]}]}""",
            """{"children": [{"daat": 1}]}"""]),
        ("""{
            "properties": {
                "date": { "format": "date-time" }, "day": { "format": "date" }, "ip": { "format": "ipv6" },
                "host": { "format": "hostname" }, "id": { "format": "uuid" }, "n": { "format": "int32" },
                "pointer": { "format": "json-pointer" }, "relative": { "format": "relative-json-pointer" },
                "uri": { "format": "uri" }, "ref": { "format": "uri-reference" }, "iri": { "format": "iri" },
                "template": { "format": "uri-template" }, "email": { "format": "email" },
                "duration": { "format": "duration" }, "time": { "format": "time" }, "v4": { "format": "ipv4" },
                "regex": { "format": "regex" }, "idn": { "format": "idn-hostname" }, "mail": { "format": "idn-email" },
                "json": { "contentMediaType": "application/json", "contentEncoding": "base64" },
                "text": { "contentMediaType": "application/json" }
            },
            "dependencies": { "date": ["day"], "id": { "required": ["n"] } },
            "definitions": { "x": { "id": "#x" } }
        }""", (; default_dialect=Draft7, assert_format=true), [
            """{"date": "2020-01-02T03:04:05.678Z", "day": "2020-02-29", "ip": "::ffff:192.168.0.1",
                "host": "example.com", "id": "2eb8aa08-aa98-11ea-b4aa-73b441d16380", "n": 12, "pointer": "/a/~0b",
                "relative": "1/a", "json": "eyJhIjogWzEsIDIsIDNdfQ==", "text": "[1]"}""",
            """{"uri": "http://example.com/a/b?c=d#e", "ref": "../a/b?c#d", "iri": "http://\\u00e9xample.com/\\u00fc",
                "template": "http://example.com/{id}/x{?q,r}", "email": "joe.bloggs@example.com",
                "duration": "P4DT12H30M5S", "time": "08:30:06.283185+01:00", "v4": "1.2.3.4", "regex": "^a+\$",
                "idn": "ελληνικά.example", "mail": "δοκιμή@example.com"}""",
            """{"n": 1e30, "date": "x", "id": "y"}""",
            """{"json": "bm90IGpzb24="}"""]),
    ]
    for (schema, options, instances) in cases
        v = compile_schema(schema; options...)
        for instance in instances
            document = parse_document(instance)
            bytes = Vector{UInt8}(instance)
            isvalid(v, document)
            isvalid(v, instance)
            isvalid(v, bytes)
            validate(v, document)
            validate(v, instance)
            validate(v, bytes)
            for level in (Basic, Detailed, Verbose)
                collector = ResultsCollector(level)
                evaluate(v, document, collector)
                evaluate(v, instance, collector)
                results(collector)
                annotations(collector)
                collect_annotations(collector)
                empty!(collector)
            end
            String(document)
        end
    end
    # A schema validated against its metaschema, which also loads the embedded metaschemas and their vocabularies.
    for uri in ("https://json-schema.org/draft/2020-12/schema", "http://json-schema.org/draft-07/schema",
        "http://json-schema.org/draft-04/schema")
        meta = compile_schema_uri(uri)
        isvalid(meta, cases[1][1])
        isvalid(meta, """{"type": 12}""")
    end
    # A pattern that takes the engine, and the errors a caller may see.
    engine = compile_schema("""{"pattern": "\\\\bfoo", "patternProperties": {"^\\\\p{L}+\$": true}}""")
    isvalid(engine, "\"a foo\"")
    isvalid(engine, """{"é": 1}""")
    try
        validate(engine, "[1, 2")
    catch err
        err isa ParseError || rethrow()
        sprint(showerror, err)
    end
    isvalid(engine, "[1, 2")
    for broken in ("""{"\$ref": "#/missing"}""", """{"pattern": "a("}""", """{"type": """)
        try
            compile_schema(broken)
        catch err
            (err isa CompileError || err isa ParseError) || rethrow()
            sprint(showerror, err)
        end
    end
    # Nothing of the workload's is kept but the code it compiled.
    lock(() -> empty!(PATTERN_CACHE), PATTERN_CACHE_LOCK)
    lock(PARSERS_LOCK)
    empty!(PARSERS)
    unlock(PARSERS_LOCK)
    IDN_TABLES[] = nothing
    return nothing
end

# True while Julia is writing the package image, which is when the workload must run.
if ccall(:jl_generating_output, Cint, ()) == 1
    precompile_workload()
end
