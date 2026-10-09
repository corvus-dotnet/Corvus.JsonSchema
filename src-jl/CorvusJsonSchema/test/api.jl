# Public API behaviour. Ported from api_test.go.

@testset "api" begin
    @testset "keeps the dynamic scope" begin
        tree = parse_document("""{
            "\$schema": "https://json-schema.org/draft/2020-12/schema",
            "\$id": "https://example.com/tree",
            "\$dynamicAnchor": "node",
            "type": "object",
            "properties": { "data": true, "children": { "type": "array", "items": { "\$dynamicRef": "#node" } } }
        }""")
        strict = """{
            "\$schema": "https://json-schema.org/draft/2020-12/schema",
            "\$id": "https://example.com/strict-tree",
            "\$dynamicAnchor": "node",
            "\$ref": "tree",
            "unevaluatedProperties": false
        }"""
        v = compile_schema(strict; resolver=uri -> uri == "https://example.com/tree" ? tree : nothing)
        @test expect_valid(v, """{ "children": [{ "data": 1, "children": [] }] }""", true)
        @test expect_valid(v, """{ "children": [{ "daat": 1 }] }""", false)
    end

    @testset "custom formats are asserted when format assertion is on" begin
        v = compile_schema("""{ "type": "string", "format": "even-length" }"""; assert_format=true,
            formats=Dict("even-length" => s -> iseven(length(s))))
        @test expect_valid(v, "\"ab\"", true)
        @test expect_valid(v, "\"abc\"", false)
        @test isempty(disagreements(v, ["\"ab\"", "\"abc\"", "1"]))
        # A custom format on a number receives its JSON text.
        digits = compile_schema("""{ "format": "int32" }"""; assert_format=true, formats=["int32" => s -> s == "12"])
        @test expect_valid(digits, "12", true)
        @test expect_valid(digits, "12.0", false)
        @test isempty(disagreements(digits, ["12", "13", "\"12\""]))
    end

    @testset "format is an annotation by default and asserted on request" begin
        schema = """{ "format": "ipv4" }"""
        @test expect_valid(compile_schema(schema), "\"not an address\"", true)
        @test expect_valid(compile_schema(schema; assert_format=true), "\"not an address\"", false)
        @test expect_valid(compile_schema(schema; assert_format=true), "\"10.0.0.1\"", true)
        draft7 = """{ "\$schema": "http://json-schema.org/draft-07/schema#", "format": "ipv4" }"""
        @test expect_valid(compile_schema(draft7), "\"not an address\"", true)
        @test expect_valid(compile_schema(draft7; assert_format_in_legacy_drafts=true), "\"not an address\"", false)
        @test expect_valid(compile_schema(draft7; assert_format_in_legacy_drafts=true, assert_format=false), "\"x\"",
            true)
    end

    @testset "content is asserted in draft 7 only" begin
        schema = """{ "contentEncoding": "base64", "contentMediaType": "application/json" }"""
        draft7 = compile_schema(schema; default_dialect=Draft7)
        @test expect_valid(draft7, "\"eyJhIjogMX0=\"", true)
        @test expect_valid(draft7, "\"bm90IGpzb24=\"", false)
        @test expect_valid(draft7, "\"not base64\"", false)
        @test expect_valid(draft7, "12", true)
        @test isempty(disagreements(draft7, ["\"eyJhIjogMX0=\"", "\"bm90IGpzb24=\"", "\"not base64\"", "12"]))
        @test expect_valid(compile_schema(schema; default_dialect=Draft7, assert_content=false), "\"not base64\"",
            true)
        @test expect_valid(compile_schema(schema), "\"not base64\"", true)
        json = compile_schema("""{ "contentMediaType": "application/json" }"""; default_dialect=Draft7)
        @test expect_valid(json, "\"[1, 2]\"", true)
        @test expect_valid(json, "\"[1, 2\"", false)
    end

    @testset "an entry point evaluates from a subschema" begin
        schema = """{ "\$defs": { "positive": { "type": "number", "exclusiveMinimum": 0 } }, "type": "string" }"""
        v = compile_schema(schema; entry_point="#/\$defs/positive")
        @test expect_valid(v, "3", true)
        @test expect_valid(v, "-3", false)
        @test expect_valid(v, "\"s\"", false)
        @test throws(() -> compile_schema(schema; entry_point="#/\$defs/missing"), CompileError)
    end

    @testset "the base URI resolves relative references" begin
        item = parse_document("""{ "type": "integer" }""")
        resolver = uri -> uri == "https://example.com/schemas/item.json" ? item : nothing
        v = compile_schema("""{ "items": { "\$ref": "item.json" } }""";
            base_uri="https://example.com/schemas/root.json", resolver=resolver)
        @test expect_valid(v, "[1, 2]", true)
        @test expect_valid(v, "[1, \"2\"]", false)
        from_uri = compile_schema_uri("https://example.com/schemas/item.json"; resolver=resolver)
        @test expect_valid(from_uri, "1", true)
        @test expect_valid(from_uri, "\"1\"", false)
        @test throws(() -> compile_schema_uri("https://example.com/schemas/other.json"; resolver=resolver),
            CompileError)
        # A resolver may return JSON text.
        text = compile_schema("""{ "\$ref": "https://example.com/t" }""";
            resolver=uri -> uri == "https://example.com/t" ? "{\"type\": \"null\"}" : nothing)
        @test expect_valid(text, "null", true)
        @test expect_valid(text, "1", false)
    end

    @testset "the default dialect applies to schemas without \$schema" begin
        schema = """{ "items": [{ "type": "string" }], "additionalItems": false }"""
        @test expect_valid(compile_schema(schema; default_dialect=Draft7), "[\"a\", \"b\"]", false)
        # In 2020-12 an array-valued "items" is not the tuple form, so neither keyword applies.
        @test expect_valid(compile_schema(schema), "[\"a\", \"b\"]", true)
    end

    @testset "compilation errors" begin
        @test throws(() -> compile_schema("""{ "\$ref": "https://example.com/missing.json" }"""), CompileError)
        @test throws(() -> compile_schema("""{ "pattern": "a(" }"""), CompileError)
        @test throws(() -> compile_schema("""{ "type": """), ParseError)
    end

    # A not whose subschema leads back to the schema it is in recurses in place like any other applicator. It stops
    # at the maximum depth: the validation is an error, and isvalid reports the instance as invalid. (Evaluating not
    # went around the depth guard, so the first of these schemas overflowed the stack, and for the others the not
    # turned the abandoned evaluation's false into true.)
    @testset "a not on an in-place cycle stops at the maximum depth" begin
        looping = """{ "allOf": [{ "\$ref": "#/\$defs/loop" }] }"""
        schemas = [
            """{ "not": { "\$ref": "#" } }""",
            """{ "not": { "not": { "\$ref": "#" } } }""",
            """{ "type": "integer", "not": { "\$ref": "#" } }""",
            """{ "allOf": [{ "not": { "\$ref": "#" } }] }""",
            """{ "\$defs": { "a": { "not": { "\$ref": "#/\$defs/b" } }, "b": { "not": { "\$ref": "#/\$defs/a" } } },
                 "\$ref": "#/\$defs/a" }""",
            """{ "\$defs": { "loop": $looping }, "not": { "\$ref": "#/\$defs/loop" } }""",
            """{ "\$defs": { "loop": $looping }, "not": { "not": { "\$ref": "#/\$defs/loop" } } }""",
            """{ "\$defs": { "loop": $looping }, "properties": { "a": { "not": { "\$ref": "#/\$defs/loop" } } } }""",
            """{ "unevaluatedProperties": false, "not": { "\$ref": "#" } }""",
        ]
        for (s, schema) in enumerate(schemas)
            v = compile_schema(schema; max_depth=16)
            for instance in ["1", "\"a\"", """{ "a": 1 }""", "[1]"]
                # Only an object with the property reaches the loop of the eighth schema, and anything but an
                # integer fails the type of the third before its not is reached, when failing fast.
                (s == 8 && instance != """{ "a": 1 }""") && continue
                (s == 3 && instance != "1") && continue
                document = parse_document(instance)
                @test !isvalid(v, instance)
                @test !isvalid(v, document)
                @test throws(() -> validate(v, instance), DepthExceededError)
                @test throws(() -> validate(v, document), DepthExceededError)
                for level in (Basic, Detailed, Verbose)
                    @test throws(() -> evaluate(v, document, ResultsCollector(level)), DepthExceededError)
                end
            end
        end
    end

    @testset "in-place recursion beyond the maximum depth is an error" begin
        schema = """{ "\$defs": { "loop": { "allOf": [{ "\$ref": "#/\$defs/loop" }] } }, "\$ref": "#/\$defs/loop" }"""
        v = compile_schema(schema; max_depth=16)
        @test throws(() -> validate(v, "1"), DepthExceededError)
        @test throws(() -> validate(v, parse_document("1")), DepthExceededError)
        @test !isvalid(v, "1")
        @test throws(() -> evaluate(v, "1", ResultsCollector(Detailed)), DepthExceededError)
        # The evaluator that gave up is fit for the next validation.
        @test throws(() -> validate(v, "2"), DepthExceededError)
    end

    @testset "numbers are compared exactly for multipleOf" begin
        v = compile_schema("""{ "multipleOf": 0.01 }""")
        @test expect_valid(v, "0.07", true)
        @test expect_valid(v, "19.99", true)
        @test expect_valid(v, "0.075", false)
        @test expect_valid(compile_schema("""{ "multipleOf": 0.0001 }"""), "0.0075", true)
    end

    # A task may move to another thread where it yields. A custom format that yields does that in the middle of a
    # validation, which then finishes on another thread than the one whose evaluation state it took.
    @testset "a validation that moves between threads" begin
        v = compile_schema("""{ "type": "array", "items": { "type": "string", "format": "slow", "minLength": 2 } }""";
            assert_format=true, formats=Dict("slow" => s -> (yield(); !startswith(s, "x"))))
        failures = Threads.Atomic{Int}(0)
        @sync for g in 0:15
            Threads.@spawn begin
                valid = "[" * join(["\"g$(g)i$(i)\"" for i in 0:19], ",") * "]"
                invalid = "[" * join(["\"g$(g)i$(i)\"" for i in 0:19], ",") * ",\"x$(g)\"]"
                document = parse_document(valid)
                for _ in 1:100
                    if !isvalid(v, valid) || isvalid(v, invalid) || !isvalid(v, document)
                        Threads.atomic_add!(failures, 1)
                        break
                    end
                end
            end
        end
        @test failures[] == 0
        @test !(@atomic v.busy)
        @test expect_valid(v, """["ab"]""", true)
    end

    @testset "validators are safe for concurrent use" begin
        v = compile_schema("""{ "type": "array", "items": { "type": "integer", "minimum": 0 }, "uniqueItems": true }""")
        failures = Threads.Atomic{Int}(0)
        @sync for g in 0:7
            Threads.@spawn begin
                items = join([string(g * 1000 + i) for i in 0:99], ",")
                valid, invalid = "[" * items * "]", "[" * items * ",-1]"
                for _ in 1:200
                    if !isvalid(v, valid) || isvalid(v, invalid)
                        Threads.atomic_add!(failures, 1)
                        break
                    end
                    yield()
                end
            end
        end
        @test failures[] == 0
    end

    @testset "discriminated oneOf and anyOf agree with exhaustive evaluation" begin
        shape(kind, extra) = """{ "type": "object", "properties": { "kind": { "const": "$kind" }, "$extra": """ *
                             """{ "type": "number" } }, "required": ["kind", "$extra"] }"""
        branches = "[" * shape("circle", "r") * "," * shape("square", "side") *
                   """, { "properties": { "kind": { "enum": [1, true] } } }]"""
        for keyword in ["oneOf", "anyOf"]
            v = compile_schema("""{ "$keyword": $branches }""")
            @test isempty(disagreements(v, ["""{ "kind": "circle", "r": 1 }""", """{ "kind": "circle", "side": 1 }""",
                """{ "kind": "square", "side": 1 }""", """{ "kind": "triangle" }""", """{ "kind": 1.0 }""",
                """{ "kind": true }""", """{ "kind": false }""", "{}", "\"circle\""]))
        end
    end

    @testset "discriminators key null and numbers by value" begin
        branch(kind, extra) = """{ "type": "object", "properties": { "kind": { "const": $kind }, "$extra": """ *
                              """{ "type": "string" } }, "required": ["kind", "$extra"] }"""
        schema = """{ "oneOf": [""" * branch("null", "a") * "," * branch("1", "b") * "," * branch("1.0", "c") * "," *
                 branch("\"x\"", "d") * "] }"
        v = compile_schema(schema)
        @test isempty(disagreements(v, ["""{ "kind": null, "a": "s" }""", """{ "kind": null, "b": "s" }""",
            """{ "kind": 1, "b": "s" }""", """{ "kind": 1, "b": "s", "c": "t" }""", """{ "kind": 1.0, "c": "t" }""",
            """{ "kind": "x", "d": "s" }""", """{ "kind": "y", "d": "s" }""", """{ "d": "s" }"""]))
        # Both numeric branches match 1 when it has both properties: oneOf fails.
        @test expect_valid(v, """{ "kind": 1, "b": "s", "c": "t" }""", false)
    end

    @testset "arrays of simple arrays match the general path" begin
        position = """{ "type": "array", "minItems": 2, "maxItems": 3, "items": { "type": "number" } }"""
        for schema in ["""{ "type": "array", "items": $position }""",
            """{ "type": "array", "items": { "type": ["array", "string"], "minItems": 2, "items": {
                "type": "integer" } } }""",
            """{ "type": "array", "items": { "type": "object", "minItems": 2 } }""",
            """{ "type": "array", "items": { "type": "array", "items": { "type": "array", "items": {
                "type": "number" } } } }"""]
            @test isempty(disagreements(compile_schema(schema), ["[]", "[[1, 2]]", "[[1, 2], [3, 4, 5]]", "[[1]]",
                "[[1, 2, 3, 4]]", "[[1, \"a\"]]", "[[1.5, 2]]", "[[\"x\", \"y\"]]", "[\"s\", [1, 2]]", "[{}, [1, 2]]",
                "[[[1, 2]], [[3]]]", "[[[1, \"a\"]]]"]))
        end
    end

    @testset "fused not-required and absent-pattern conditions match the general path" begin
        extensions = """{ "patternProperties": { "^x-": true } }"""
        for schema in [
            # not: {required} alongside unevaluatedProperties (the OpenAPI example object).
            """{
                "type": "object",
                "properties": { "value": true, "externalValue": { "type": "string" }, "summary": { "type": "string" } },
                "not": { "required": ["value", "externalValue"] },
                "\$ref": "#/\$defs/ext",
                "unevaluatedProperties": false,
                "\$defs": { "ext": $extensions }
            }""",
            # An if deciding on no name matching a pattern (the OpenAPI responses object).
            """{
                "type": "object",
                "properties": { "default": { "type": "integer" } },
                "patternProperties": { "^[1-5](?:[0-9]{2}|XX)\$": { "type": "integer" } },
                "\$ref": "#/\$defs/ext",
                "unevaluatedProperties": false,
                "if": { "patternProperties": { "^[1-5](?:[0-9]{2}|XX)\$": false } },
                "then": { "required": ["default"] },
                "\$defs": { "ext": $extensions }
            }""",
            # Both, gated by another condition, with a name the pattern also matches.
            """{
                "type": "object",
                "properties": { "kind": true, "a": true, "b": true, "x-a": true },
                "allOf": [{ "\$ref": "#/\$defs/ext" }, { "properties": { "c": true } }],
                "if": { "properties": { "kind": { "const": "k" } }, "required": ["kind"] },
                "then": {
                    "not": { "required": ["a", "b"] },
                    "if": { "patternProperties": { "^x-": false } },
                    "then": { "required": ["c"] },
                    "else": { "properties": { "d": true } }
                },
                "unevaluatedProperties": false,
                "\$defs": { "ext": $extensions }
            }"""]
            v = compile_schema(schema)
            b = root_body(v)
            @test b !== nothing && b.fused !== nothing
            @test isempty(disagreements(v, ["{}", """{ "value": 1 }""", """{ "value": 1, "externalValue": "u" }""",
                """{ "externalValue": 2 }""", """{ "x-y": 1, "summary": "s" }""", """{ "other": 1 }""",
                """{ "default": 1 }""", """{ "200": 1 }""", """{ "2XX": 1, "x-a": 1 }""", """{ "600": 1 }""",
                """{ "default": "a", "404": 1 }""", """{ "kind": "k" }""", """{ "kind": "k", "c": 1 }""",
                """{ "kind": "k", "a": 1, "b": 1, "c": 1 }""", """{ "kind": "k", "a": 1, "c": 1 }""",
                """{ "kind": "k", "x-a": 1, "d": 1 }""", """{ "kind": "k", "x-z": 1, "d": 1 }""",
                """{ "kind": "k", "d": 1, "c": 1 }""", """{ "kind": "j", "a": 1, "b": 1 }"""]))
        end
    end

    @testset "flat fused objects match the general path" begin
        shared = """{ "properties": { "a": { "type": "string" }, "b": true }, "required": ["a"], "maxProperties": 3 }"""
        for (i, schema) in enumerate([
            """{ "allOf": [{ "\$ref": "#/\$defs/s" }], "properties": { "c": { "type": "integer" } }, "\$defs": {
                "s": $shared } }""",
            """{
                "allOf": [{ "\$ref": "#/\$defs/s" }, { "properties": { "a": { "type": "string" } },
                    "required": ["c"] }],
                "properties": { "c": { "type": "integer" } },
                "minProperties": 2,
                "\$defs": { "s": $shared }
            }""",
            # The same name with different schemas stays a fused plan.
            """{ "allOf": [{ "\$ref": "#/\$defs/s" }], "properties": { "a": { "minLength": 2 } }, "\$defs": {
                "s": $shared } }"""])
            v = compile_schema(schema)
            b = root_body(v)
            @test b !== nothing && b.fused !== nothing && (b.fused.flat !== nothing) == (i < 3)
            @test isempty(disagreements(v, ["{}", """{ "a": "x" }""", """{ "a": "xy", "c": 1 }""",
                """{ "a": 1, "c": 1 }""", """{ "a": "x", "c": "1" }""", """{ "a": "x", "b": null, "c": 1 }""",
                """{ "a": "x", "b": 1, "c": 1, "d": 1 }""", """{ "c": 1 }""", "[]"]))
        end
    end

    @testset "fused objects below a dynamic reference match the general path" begin
        # The items' $dynamicRef resolves (through the scope) to "strict", whose allOf contributor is in its own
        # resource.
        v = compile_schema("""{
            "\$schema": "https://json-schema.org/draft/2020-12/schema",
            "\$id": "https://example.com/root",
            "\$ref": "strict",
            "\$defs": {
                "strict": {
                    "\$id": "https://example.com/strict",
                    "\$dynamicAnchor": "node",
                    "type": "object",
                    "properties": { "data": true, "y": true, "children": { "type": "array", "items": {
                        "\$ref": "tree#/\$defs/kids" } } },
                    "allOf": [{ "\$ref": "#/\$defs/extra" }],
                    "unevaluatedProperties": false,
                    "\$defs": { "extra": { "properties": { "x": { "type": "integer" } } } }
                },
                "tree": {
                    "\$id": "https://example.com/tree",
                    "\$dynamicAnchor": "node",
                    "type": "object",
                    "\$defs": { "kids": { "\$dynamicRef": "#node" } }
                }
            }
        }""")
        @test isempty(disagreements(v, ["{}", """{ "data": 1, "x": 2 }""", """{ "x": "a" }""",
            """{ "y": 1, "children": [{ "y": 1 }] }""", """{ "children": [{ "z": 1 }] }""",
            """{ "children": [{ "x": 1, "children": [{ "y": 2, "data": 3 }] }] }""",
            """{ "children": [{ "children": [{ "x": "no" }] }] }""", """{ "z": 1 }"""]))
        @test expect_valid(v, """{ "children": [{ "x": 1, "children": [{ "y": 2 }] }] }""", true)
        @test expect_valid(v, """{ "children": [{ "z": 1 }] }""", false)
    end

    # Small objects are decided by looking the few declared and required names up in them, large ones by visiting
    # their properties. Both agree, top-level and nested.
    @testset "few names are looked up in small and large objects" begin
        v = compile_schema("""{
            "properties": { "a": { "type": "string" }, "n": { "properties": { "x": { "type": "integer" } } } },
            "required": ["b"]
        }""")
        for pad in [0, 3, 40, 300]
            p = join([",\"p$i\": $i" for i in 0:pad-1])
            @test expect_valid(v, "{\"b\": 1, \"a\": \"x\"$p}", true)
            @test expect_valid(v, "{\"a\": \"x\"$p}", false)
            @test expect_valid(v, "{\"a\": 1, \"b\": 1$p}", false)
            @test expect_valid(v, "{\"b\": null$p, \"n\": {\"x\": 2$p}}", true)
            @test expect_valid(v, "{\"b\": null$p, \"n\": {\"x\": \"2\"$p}}", false)
        end
    end

    # JSON text is validated in place. Invalid JSON and runaway recursion are told apart.
    @testset "validates JSON text" begin
        v = compile_schema("""{ "type": "array", "items": { "type": "integer" } }""")
        @test validate(v, "[1, 2, 3]")
        @test !validate(v, Vector{UInt8}("[1, \"2\"]"))
        @test validate(v, codeunits("[1, 2, 3]"))
        @test validate(v, SubString("x[1, 2, 3]", 2))
        offset = try
            validate(v, "[1, 2")
            -1
        catch err
            err isa ParseError ? err.offset : -2
        end
        @test offset == 5
        @test !isvalid(v, "[1, 2")
        @test !isvalid(v, UInt8[])
        c = ResultsCollector(Detailed)
        @test !evaluate(v, "[\"x\"]", c)
        @test any(r -> !r.is_match && r.document_evaluation_location == "/0", results(c))
        @test throws(() -> evaluate(v, "[", c), ParseError)
        @test evaluate(v, "[1]")
        @test evaluate(v, "[1]", nothing)
    end

    # A format callback that validates JSON text itself, during a validation of JSON text on the same task, gets
    # buffers of its own.
    @testset "validating JSON from a format callback works" begin
        inner = compile_schema("""{ "type": "object", "required": ["a"] }""")
        holder = Ref{Validator}()
        # The same validator too: an evaluation nested in its own callback.
        v = compile_schema("""{ "type": "array", "items": { "format": "embedded-json" } }"""; assert_format=true,
            formats=Dict("embedded-json" => s -> isvalid(inner, s) && isvalid(holder[], "[]")))
        holder[] = v
        @test expect_valid(v, """["{\\"a\\": 1}", "{\\"a\\": 2}"]""", true)
        @test expect_valid(v, """["{\\"a\\": 1}", "{\\"b\\": 2}"]""", false)
        @test expect_valid(v, """["{\\"a\\": 1}", "not json"]""", false)
    end

    # An exception from a custom format reaches the caller, and the validator is fit for the next validation.
    @testset "a custom format that throws" begin
        v = compile_schema("""{ "items": { "format": "fragile" } }"""; assert_format=true,
            formats=Dict("fragile" => s -> s == "boom" ? error("boom") : true))
        @test expect_valid(v, "[\"a\", \"b\"]", true)
        @test throws(() -> isvalid(v, "[\"a\", \"boom\"]"), ErrorException)
        @test throws(() -> isvalid(v, parse_document("[\"boom\"]")), ErrorException)
        @test throws(() -> evaluate(v, "[\"boom\"]", ResultsCollector(Basic)), ErrorException)
        @test !(@atomic v.busy)
        @test expect_valid(v, "[\"a\", 1]", true)
    end

    # A number in a schema and the same number in an instance are the same Float64, however it is written.
    @testset "schema and instance numbers parse alike" begin
        for (keyword, literal) in ["exclusiveMaximum" => "972783798187987123879878123.18878137",
            "exclusiveMinimum" => "-972783798187987123879878123.18878137"]
            short = replace(literal, "972783798187987123879878123.18878137" => "9.727837981879871e+26")
            for text in [literal, short]
                v = compile_schema("{\"$keyword\": $text}")
                @test expect_valid(v, text, false)
                @test expect_valid(v, literal, false)
            end
        end
    end

    @testset "unevaluatedItems with a static prefix match the general path" begin
        for schema in [
            """{ "prefixItems": [{ "type": "integer" }], "unevaluatedItems": { "type": "string" } }""",
            """{ "allOf": [{ "prefixItems": [true, { "type": "integer" }] }], "prefixItems": [{ "type": "integer" }],
                "unevaluatedItems": false }""",
            """{ "allOf": [{ "items": { "type": "integer" } }], "unevaluatedItems": false }""",
            """{ "anyOf": [{ "prefixItems": [true, true] }, { "prefixItems": [{ "type": "integer" }] }],
                "unevaluatedItems": false }""",
            """{ "contains": { "type": "integer" }, "unevaluatedItems": { "type": "string" } }""",
            """{ "if": { "prefixItems": [{ "const": 1 }] }, "then": { "prefixItems": [true, true] },
                "unevaluatedItems": false }"""]
            @test isempty(disagreements(compile_schema(schema), ["[]", "[1]", "[1, 2]", "[1, \"a\"]", "[\"a\"]",
                "[1, 2, 3]", "[1, \"a\", \"b\"]", "[\"a\", 1, \"b\"]", "{}", "[2, 2]"]))
        end
    end

    @testset "uniqueItems over large arrays" begin
        v = compile_schema("""{ "uniqueItems": true }""")
        items = join(["{\"id\": $i, \"tags\": [\"a\", $(i % 3)]}" for i in 0:199], ",")
        @test expect_valid(v, "[" * items * "]", true)
        @test expect_valid(v, "[" * items * ",{\"tags\": [\"a\", 1.0], \"id\": 4e0}]", false)
    end

    @testset "every exported name has a docstring" begin
        for name in names(CorvusJsonSchema)
            @test haskey(Docs.meta(CorvusJsonSchema), Docs.Binding(CorvusJsonSchema, name))
        end
    end
end
