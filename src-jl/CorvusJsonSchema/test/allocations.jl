# Validation allocates nothing in the steady state: the evaluator's buffers are kept by the validator, JSON text is
# parsed into reused buffers, and the document is evaluated where it was parsed. Ported from allocations_test.go.
#
# Four asserted formats are outside this: regex (the pattern is parsed), idn-hostname and idn-email (the labels are
# decoded), and hostname for a label that starts with "xn--". A custom format receives a copy of the string.

struct AllocationCase
    name::String
    schema::String
    options::NamedTuple
    instances::Vector{String}
end

function allocation_cases()
    large = join(["\"p$i\": $i" for i in 0:299], ",")
    unique_items = join(["{\"id\": $i, \"name\": \"n$i\"}" for i in 0:99], ",")
    return [
        AllocationCase("objects and arrays", """{
                "type": "object",
                "properties": { "name": { "type": "string", "minLength": 1 }, "tags": { "type": "array", "items": { "type": "string" } } },
                "required": ["name"]
            }""", (;), [
                """{"name": "a", "tags": ["x", "y\\nz"]}""",
                """{"name": "", "tags": []}""",
                """{"name": "café", "tags": ["1", "2", "3", "4", "5", "6", "7", "8"], "other": {"deep": [1, [2, [3]]]}}"""]),
        AllocationCase("keywords of every kind", """{
                "type": "object",
                "properties": {
                    "n": { "type": "number", "minimum": 0, "exclusiveMaximum": 100, "multipleOf": 0.25 },
                    "s": { "type": "string", "maxLength": 5, "pattern": "^[a-z]+\$" },
                    "e": { "enum": ["a", "b", 1, null] },
                    "c": { "const": { "k": [1, 2] } },
                    "u": { "type": "array", "uniqueItems": true, "contains": { "type": "integer" }, "minContains": 1 },
                    "o": { "oneOf": [{ "type": "string" }, { "type": "integer" }, { "required": ["x"] }] },
                    "a": { "anyOf": [{ "minimum": 5 }, { "maxLength": 2 }] },
                    "i": { "if": { "type": "integer" }, "then": { "minimum": 1 }, "else": { "type": "string" } },
                    "x": { "not": { "type": "null" } }
                },
                "patternProperties": { "^x-": { "type": "boolean" } },
                "additionalProperties": { "type": "integer" },
                "propertyNames": { "maxLength": 10 },
                "dependentRequired": { "n": ["s"] },
                "minProperties": 1
            }""", (;), [
                """{"n": 1.5, "s": "abc", "e": "b", "c": {"k": [1, 2.0]}, "u": [1, "a", [2], {"b": 1}], "o": 3, "a": "ab", "i": 2, "x": 0, "x-flag": true, "extra": 7}""",
                """{"n": 1.3, "s": "abc"}""",
                "{\"u\": [" * unique_items * ", 5]}",
                "{\"u\": [" * unique_items * ", {\"name\": \"n3\", \"id\": 3}]}",
                """{"o": {"x": 1}, "a": 3, "i": "s", "x": null}""",
                """{"a-name-that-is-too-long": 1}"""]),
        AllocationCase("unevaluated properties and items", """{
                "\$defs": { "base": { "properties": { "a": { "type": "integer" } }, "patternProperties": { "^x-": true } } },
                "allOf": [{ "\$ref": "#/\$defs/base" }],
                "anyOf": [{ "properties": { "b": true }, "required": ["b"] }, { "properties": { "c": true } }],
                "oneOf": [{ "properties": { "d": { "type": "string" } } }, { "properties": { "d": { "type": "integer" } } }],
                "properties": { "list": { "prefixItems": [true], "contains": { "type": "string" }, "unevaluatedItems": false } },
                "unevaluatedProperties": false
            }""", (;), [
                """{"a": 1, "b": 2, "d": "s", "x-y": null, "list": [1, "a", "b"]}""",
                """{"a": 1, "c": 2, "d": 3, "list": [1, "a", 2]}""",
                """{"a": 1, "e": 2}""",
                "{" * large * "}"]),
        AllocationCase("fused objects with conditions", """{
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
        AllocationCase("a dynamic scope", """{
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
                        "properties": { "data": true, "children": { "type": "array", "items": { "\$dynamicRef": "#node" } } }
                    }
                }
            }""", (;), [
                """{"children": [{"data": 1, "children": [{"data": [1, 2, 3]}]}]}""",
                """{"children": [{"daat": 1}]}"""]),
        AllocationCase("patterns on the engine", """{
                "properties": { "a": { "pattern": "\\\\bfoo" }, "b": { "pattern": "^\\\\p{L}+\$" } },
                "patternProperties": { "^(?=a)a|b\$": { "type": "integer" } }
            }""", (;), [
                """{"a": "a foo", "b": "éa", "ab": 1}""",
                """{"a": "afoo"}""",
                """{"b": "é1", "bb": 1}"""]),
        AllocationCase("asserted formats and content", """{
                "properties": {
                    "date": { "format": "date-time" }, "ip": { "format": "ipv6" }, "host": { "format": "hostname" },
                    "id": { "format": "uuid" }, "n": { "format": "int32" }, "pointer": { "format": "json-pointer" },
                    "uri": { "format": "uri" }, "ref": { "format": "uri-reference" }, "iri": { "format": "iri" },
                    "template": { "format": "uri-template" }, "email": { "format": "email" },
                    "duration": { "format": "duration" }, "time": { "format": "time" }, "v4": { "format": "ipv4" },
                    "json": { "contentMediaType": "application/json", "contentEncoding": "base64" }
                }
            }""", (; default_dialect=Draft7, assert_format=true), [
                """{"date": "2020-01-02T03:04:05.678Z", "ip": "::ffff:192.168.0.1", "host": "example.com", "id": "2eb8aa08-aa98-11ea-b4aa-73b441d16380", "n": 12, "pointer": "/a/~0b", "json": "eyJhIjogWzEsIDIsIDNdfQ=="}""",
                """{"uri": "http://example.com/a/b?c=d#e", "ref": "../a/b?c#d", "iri": "http://\\u00e9xample.com/\\u00fc", "template": "http://example.com/{id}/x{?q,r}", "email": "joe.bloggs@example.com", "duration": "P4DT12H30M5S", "time": "08:30:06.283185+01:00", "v4": "1.2.3.4"}""",
                """{"n": 1e30}""",
                """{"json": "bm90IGpzb24="}"""]),
    ]
end

# One pass over the instances, by each entry point. The results are folded into one value so that no call is
# optimised away.
function validate_all(v::Validator, instances::Vector{T}) where {T}
    sink = false
    for instance in instances
        sink ⊻= isvalid(v, instance)
    end
    return sink
end

function validate_all_strict(v::Validator, instances::Vector{T}) where {T}
    sink = false
    for instance in instances
        sink ⊻= validate(v, instance)
    end
    return sink
end

# The bytes one pass allocates, after passes that size the buffers and compile the code.
function allocated(f, v::Validator, instances)
    f(v, instances)
    f(v, instances)
    return @allocated f(v, instances)
end

@testset "allocations" begin
    @testset "validation allocates nothing in the steady state" begin
        for case in allocation_cases()
            v = compile_schema(case.schema; case.options...)
            documents = [parse_document(instance) for instance in case.instances]
            texts = [Vector{UInt8}(instance) for instance in case.instances]
            expected = [isvalid(v, d) for d in documents]
            @testset "$(case.name)" begin
                @test [isvalid(v, t) for t in texts] == expected
                @test [isvalid(v, s) for s in case.instances] == expected
                @test allocated(validate_all, v, documents) == 0
                @test allocated(validate_all_strict, v, documents) == 0
                @test allocated(validate_all, v, texts) == 0
                @test allocated(validate_all_strict, v, texts) == 0
                @test allocated(validate_all, v, case.instances) == 0
                @test allocated(validate_all_strict, v, case.instances) == 0
            end
        end
    end

    @testset "text that is not JSON allocates nothing for isvalid" begin
        v = compile_schema("""{"type": "object"}""")
        @test allocated(validate_all, v, [Vector{UInt8}("""{"a": [1, 2, {"b": "c"}""")]) == 0
        @test allocated(validate_all, v, ["""{"a": [1, 2, {"b": "c"}"""]) == 0
    end
end
