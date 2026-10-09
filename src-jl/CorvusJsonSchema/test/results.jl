# Results collection: the expectations of the C# evaluator's ResultsTests and ResultPathTests, plus worked examples
# traced through the C# collecting path (row order, locations, messages, levels). Ported from results_test.go.

function dump_results(v::Validator, instance::String, level::ResultsLevel)
    c = ResultsCollector(level)
    evaluate(v, instance, c)
    return [join([r.is_match ? "match" : "fail", r.schema_evaluation_location, r.evaluation_location,
            r.document_evaluation_location, r.message], "|") for r in results(c)]
end

const PERSON_SCHEMA = """{
    "\$schema": "https://json-schema.org/draft/2020-12/schema",
    "type": "object",
    "title": "Person",
    "properties": {
        "name": { "type": "string", "minLength": 1, "description": "The name" },
        "age": { "type": "integer", "minimum": 0 }
    },
    "required": ["name"],
    "additionalProperties": false
}"""

const REFS_SCHEMA = """{
    "\$schema": "https://json-schema.org/draft/2020-12/schema",
    "\$defs": {
        "fooId": { "type": "integer", "minimum": 0 },
        "holder": { "type": "object", "properties": { "fooId": { "\$ref": "#/\$defs/fooId" } } },
        "viaRef": { "\$ref": "#/\$defs/fooId" }
    }
}"""

const EXAMPLE_SCHEMA = """{
    "type": "object",
    "properties": { "a": { "type": "string" } },
    "required": ["b"],
    "anyOf": [{ "required": ["a"] }, { "minProperties": 5 }]
}"""

const SUBSCHEMA = "The value was expected to match the subschema."

@testset "results" begin
    @testset "flag and collecting evaluation agree at every level" begin
        v = compile_schema(PERSON_SCHEMA)
        for (instance, expected) in ["""{ "name": "a", "age": 3 }""" => true, """{ "name": "", "age": 3 }""" => false,
            """{ "age": 3 }""" => false, """{ "name": "a", "extra": 1 }""" => false,
            """{ "name": "a", "age": -1 }""" => false, "[]" => false]
            @test isvalid(v, instance) == expected
            for level in (Basic, Detailed, Verbose)
                @test evaluate(v, instance, ResultsCollector(level)) == expected
            end
        end
    end

    @testset "basic results report failing keywords with locations" begin
        c = ResultsCollector(Basic)
        @test !evaluate(compile_schema(PERSON_SCHEMA), """{ "name": "", "age": -1 }""", c)
        rows = results(c)
        @test all(r -> r.message == "", rows)
        failed = filter(r -> !r.is_match, rows)
        @test any(r -> endswith(r.evaluation_location, "/minLength") && r.document_evaluation_location == "/name",
            failed)
        @test any(r -> endswith(r.evaluation_location, "/minimum") && r.document_evaluation_location == "/age", failed)
        @test any(r -> r.schema_evaluation_location == "/properties/name", failed)
    end

    @testset "verbose annotations are produced" begin
        c = ResultsCollector(Verbose)
        @test evaluate(compile_schema(PERSON_SCHEMA), """{ "name": "a" }""", c)
        produced = collect_annotations(c)
        @test produced[""]["title"] == Dict("#" => "\"Person\"")
        @test produced["/name"]["description"]["#/properties/name"] == "\"The name\""
        @test Annotation("", "title", "", "\"Person\"") in annotations(c)
    end

    @testset "verbose output follows the C# row order" begin
        @test dump_results(compile_schema(PERSON_SCHEMA), """{ "name": "a" }""", Verbose) == [
            "match|/properties/name|/properties/name|/name|$SUBSCHEMA",
            "match|/properties/name|/properties/name/description|/name|\"The name\"",
            "match|/properties/name/minLength|/properties/name/minLength|/name|Expected the length of the value " *
            "to be greater than or equal to '1'",
            "match|/properties/name/type|/properties/name/type|/name|The value was expected to be of type 'string'",
            "match||||$SUBSCHEMA",
            "match||/title||\"Person\"",
            "match|/required|/required|/name|Required property present 'name'",
            "match|/type|/type||The value was expected to be of type 'object'"]
    end

    @testset "matching keywords carry their message in verbose output" begin
        v = compile_schema("""{
            "\$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": ["integer", "array"],
            "uniqueItems": true,
            "properties": { "n": { "type": "integer" } }
        }""")
        function rows(validator, instance)
            c = ResultsCollector(Verbose)
            valid = evaluate(validator, instance, c)
            return valid, results(c)
        end
        has(list, is_match, location, message) = any(r -> r.is_match == is_match &&
                                                               r.evaluation_location == location && r.message != "" &&
                                                               (message == "" || r.message == message), list)
        valid, r = rows(v, "[1, 2]")
        @test valid
        @test has(r, true, "/type", "The value was expected to be of type '[\"array\", \"integer\"]'")
        @test has(r, true, "/uniqueItems", "")
        valid, r = rows(v, "[1, 1]")
        @test !valid && has(r, false, "/uniqueItems", "")
        valid, r = rows(compile_schema("""{ "type": "integer" }"""), "3")
        @test valid && has(r, true, "/type", "The value was expected to be of type 'integer'")
    end

    @testset "an entry point reports its own schema location" begin
        v = compile_schema(REFS_SCHEMA; entry_point="#/\$defs/fooId")
        @test dump_results(v, "\"notAnInteger\"", Detailed) == [
            "fail|/\$defs/fooId|||$SUBSCHEMA",
            "fail|/\$defs/fooId/type|/type||The value was expected to be of type 'integer'"]
    end

    @testset "a pure \$ref property is elided with \$ref in the evaluation path" begin
        v = compile_schema(REFS_SCHEMA; entry_point="#/\$defs/holder")
        @test dump_results(v, """{ "fooId": "notAnInteger" }""", Detailed) == [
            "fail|/\$defs/fooId|/properties/fooId/\$ref|/fooId|$SUBSCHEMA",
            "fail|/\$defs/fooId/type|/properties/fooId/\$ref/type|/fooId|The value was expected to be of type " *
            "'integer'",
            "fail|/\$defs/holder|||$SUBSCHEMA"]
    end

    @testset "a pure \$ref root reports against its target" begin
        v = compile_schema(REFS_SCHEMA; entry_point="#/\$defs/viaRef")
        @test dump_results(v, "\"notAnInteger\"", Detailed) == [
            "fail|/\$defs/fooId|||$SUBSCHEMA",
            "fail|/\$defs/fooId/type|/type||The value was expected to be of type 'integer'"]
    end

    @testset "a required failure carries the property name" begin
        v = compile_schema("""{ "type": "object", "required": ["name"] }""")
        @test dump_results(v, "{}", Detailed) == [
            "fail||||$SUBSCHEMA",
            "fail|/required|/required|/name|Required property not present 'name'"]
    end

    @testset "detailed output keeps failures only, with messages" begin
        expected = [
            "fail|/properties/a|/properties/a|/a|$SUBSCHEMA",
            "fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'",
            "fail||||$SUBSCHEMA",
            "fail|/required|/required|/b|Required property not present 'b'"]
        v = compile_schema(EXAMPLE_SCHEMA)
        @test dump_results(v, """{ "a": 1 }""", Detailed) == expected
        @test dump_results(v, """{ "a": 1 }""", Basic) == [line[1:findlast('|', line)] for line in expected]
    end

    @testset "verbose output reverses a context's own rows after its summary" begin
        @test dump_results(compile_schema(EXAMPLE_SCHEMA), """{ "a": 1 }""", Verbose) == [
            "fail|/properties/a|/properties/a|/a|$SUBSCHEMA",
            "fail|/properties/a/type|/properties/a/type|/a|The value was expected to be of type 'string'",
            "match|/anyOf/0|/anyOf/0||$SUBSCHEMA",
            "match|/anyOf/0/required|/anyOf/0/required|/a|Required property present 'a'",
            "fail||||$SUBSCHEMA",
            "match|/anyOf|/anyOf||The value matched at least one subschema.",
            "fail|/required|/required|/b|Required property not present 'b'",
            "match|/type|/type||The value was expected to be of type 'object'"]
    end

    @testset "not subtrees and boolean schemas" begin
        @test dump_results(compile_schema("""{ "not": { "type": "string" } }"""), "\"x\"", Detailed) == [
            "fail||||$SUBSCHEMA",
            "fail|/not|/not||The value matched the subschema in a not composition, which means the evaluation was " *
            "not a match."]
        @test dump_results(compile_schema("false"), "1", Detailed) == ["fail||||$SUBSCHEMA", "fail||||"]
    end

    @testset "a valid instance at detailed level yields only the passing root row" begin
        @test dump_results(compile_schema(PERSON_SCHEMA), """{ "name": "a" }""", Detailed) == ["match||||"]
    end

    @testset "propertyNames keeps the object location and adds a failure row per name" begin
        v = compile_schema("""{ "propertyNames": { "maxLength": 2 } }""")
        @test dump_results(v, """{ "abc": 1 }""", Detailed) == [
            "fail|/propertyNames|/propertyNames||$SUBSCHEMA",
            "fail|/propertyNames/maxLength|/propertyNames/maxLength||Expected the length of the value to be less " *
            "than or equal to '2'",
            "fail||||$SUBSCHEMA",
            "fail|/propertyNames|/propertyNames||The property name did not match the schema."]
    end

    @testset "draft 4 exclusive bounds report under exclusiveMaximum with the maximum" begin
        v = compile_schema("""{ "\$schema": "http://json-schema.org/draft-04/schema#", "maximum": 3, """ *
                           """"exclusiveMaximum": true }""")
        @test dump_results(v, "3", Detailed) == [
            "fail||||$SUBSCHEMA",
            "fail|/exclusiveMaximum|/exclusiveMaximum||The value was expected to be less than '3'"]
    end

    @testset "failing anyOf branches are discarded and unevaluatedProperties has no message" begin
        v = compile_schema("""{ "anyOf": [{ "properties": { "a": true } }, { "required": ["zz"] }], """ *
                           """"unevaluatedProperties": false }""")
        @test dump_results(v, """{ "a": 1, "b": 2 }""", Detailed) == [
            "fail|/unevaluatedProperties|/unevaluatedProperties|/b|$SUBSCHEMA",
            "fail|/unevaluatedProperties|/unevaluatedProperties|/b|",
            "fail||||$SUBSCHEMA",
            "fail|/unevaluatedProperties|/unevaluatedProperties||"]
    end

    @testset "a collector accumulates across evaluations" begin
        v = compile_schema(PERSON_SCHEMA)
        c = ResultsCollector(Detailed)
        evaluate(v, """{ "age": "x" }""", c)
        first_count = length(results(c))
        evaluate(v, """{ "age": "x" }""", c)
        @test first_count != 0 && length(results(c)) == 2 * first_count
        empty!(c)
        @test isempty(results(c))
    end

    @testset "dependencies reports under its own name in every dialect" begin
        v = compile_schema("""{
            "\$schema": "https://json-schema.org/draft/2020-12/schema",
            "dependencies": { "a": ["b"], "c": { "required": ["d"] } }
        }""")
        @test dump_results(v, """{ "a": 1, "c": 1 }""", Detailed) == [
            "fail|/dependencies/c|/dependencies/c||$SUBSCHEMA",
            "fail|/dependencies/c/required|/dependencies/c/required|/d|Required property not present 'd'",
            "fail||||$SUBSCHEMA",
            "fail|/dependencies|/dependencies|/c|The value did match the schema applied because it contained the " *
            "property 'c'",
            "fail|/dependencies|/dependencies|/b|Required property not present 'b'"]
        modern = compile_schema("""{ "dependentRequired": { "a": ["b"] }, "dependentSchemas": """ *
                                """{ "c": { "required": ["d"] } } }""")
        rows = dump_results(modern, """{ "a": 1, "c": 1 }""", Detailed)
        @test "fail|/dependentRequired|/dependentRequired|/b|Required property not present 'b'" in rows
        @test any(startswith("fail|/dependentSchemas/c|/dependentSchemas/c||"), rows)
    end

    @testset "a statically resolved dynamic reference hop is named \$dynamicRef in the evaluation path" begin
        v = compile_schema("""{
            "\$schema": "https://json-schema.org/draft/2020-12/schema",
            "properties": { "p": { "\$dynamicRef": "#/\$defs/n" } },
            "\$defs": { "n": { "type": "integer" } }
        }""")
        @test dump_results(v, """{ "p": "x" }""", Detailed) == [
            "fail|/\$defs/n|/properties/p/\$dynamicRef|/p|$SUBSCHEMA",
            "fail|/\$defs/n/type|/properties/p/\$dynamicRef/type|/p|The value was expected to be of type 'integer'",
            "fail||||$SUBSCHEMA"]
    end
end
