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

    # A pattern has limits of size, and a schema with a pattern over one does not compile. The error is a
    # PatternTooLargeError, which names the limit, and never the CompileError of a pattern that is not valid.
    @testset "a pattern over a limit of size is too large, and not invalid" begin
        function compiling(schema)
            started = time()
            error = try
                compile_schema(schema)
                nothing
            catch e
                e
            end
            return error, time() - started
        end
        megabyte = "a"^1_048_577
        for (pattern, valid, limit) in [
                (megabyte, false, "its length is over the limit of 1048576 bytes"),
                ("(?=a)" * "a"^140_000, false, "it has over the limit of 131072 terms and alternatives"),
                ("("^201 * "a" * ")"^201, false, "its groups nest over the limit of 200 deep"),
                ("(?:(?:a{1000}){1000}){1000}", true, "PCRE2 does not hold it"),
                ("^(a|)" * "(?:"^60 * "\\\\1" * "|b){1,2}"^60 * "\$", true, "steps to write"),
                ("(?:a|){2147483648}", true, "a count over the limit of 2147483646")],
            schema in ("""{ "pattern": "$pattern" }""", """{ "patternProperties": { "$pattern": true } }""",
                """{ "items": { "anyOf": [{ "type": "null" }, { "pattern": "$pattern" }] } }""")
            error, took = compiling(schema)
            @test error isa PatternTooLargeError
            @test !(error isa CompileError)
            @test error isa PatternTooLargeError && error.valid == valid
            @test error isa PatternTooLargeError && occursin(limit, error.limit)
            message = sprint(showerror, error)
            @test occursin(valid ? "is a valid ECMA-262 regular expression, and is too large: " : "is too large to read: ",
                message)
            @test !occursin("nvalid", message) && !occursin("not an ECMA-262", message)
            @test ncodeunits(message) < 600
            @test took < 30
        end
        # A pattern that is not valid is a CompileError still, whatever its size is under the limits.
        error, _ = compiling("""{ "pattern": "a(" }""")
        @test error isa CompileError && occursin("Invalid regular expression", error.message)
        error, _ = compiling("""{ "pattern": "$("(?:"^150)a$(")"^149)" }""")
        @test error isa CompileError
        # A pattern of ordinary size is not touched by the limits, and neither is a large one that is within them.
        words = join(("word$k" for k in 1:3000), "|")
        v = compile_schema("""{ "pattern": "^(?:$words)\$", "patternProperties": { "^(?:[a-z]{2,8}-){0,40}[a-z]+\$": true } }""")
        @test expect_valid(v, "\"word2999\"", true)
        @test expect_valid(v, "\"word3001\"", false)
        @test expect_valid(v, """{"ab-cd-ef": 1}""", true)
        # The regex format reads a string of the instance as a pattern with the limits of length and of nesting,
        # and a string over one is not a valid regular expression to it.
        format = compile_schema("""{ "format": "regex" }"""; assert_format=true)
        @test expect_valid(format, "\"" * "("^200 * "a" * ")"^200 * "\"", true)
        @test expect_valid(format, "\"" * "("^201 * "a" * ")"^201 * "\"", false)
        @test expect_valid(format, "\"" * "a"^1_048_576 * "\"", true)
        @test expect_valid(format, "\"" * megabyte * "\"", false)
        # (It does not read a pattern into a tree, so it has no limit on terms.)
    end

    # The patterns a process has compiled are kept, for the next schema that has them, up to 4,096 patterns and
    # 32 MiB. A schema may come from outside, and each has patterns of its own.
    @testset "the cache of compiled patterns is bounded" begin
        C = CorvusJsonSchema
        @test C.MAX_CACHED_PATTERNS == 4096 && C.MAX_CACHED_PATTERN_BYTES == 32 << 20
        for k in 1:5000
            compile_schema("""{ "pattern": "^cached\\\\d{$k}x+\$" }""")
        end
        @test 0 < length(C.PATTERN_CACHE) <= C.MAX_CACHED_PATTERNS
        @test 0 < C.PATTERN_CACHE_BYTES[] <= C.MAX_CACHED_PATTERN_BYTES
        # A pattern that is still in the cache is the one a second schema gets.
        first = compile_schema("""{ "pattern": "^kept\\\\d+x*\$" }""")
        second = compile_schema("""{ "pattern": "^kept\\\\d+x*\$" }""")
        pattern_of(v) = only(n.pattern for n in v.program.nodes if n.pattern !== nothing)
        @test pattern_of(first) === pattern_of(second)
        # A pattern counts as the bytes of its text and what the engine keeps for it.
        before = C.PATTERN_CACHE_BYTES[]
        source = "^" * "\\p{L}"^50 * "y{7}\$"
        compiled = C.compile_pattern(source)
        @test C.PATTERN_CACHE_BYTES[] - before == ncodeunits(source) + C.engine_bytes(compiled.engine)
        @test C.engine_bytes(compiled.engine) > 5_000
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

    # One match of a pattern is limited in time, to one second unless pattern_match_limit says another time. A
    # match that reaches the limit is an error from every entry point, and never "valid" or "invalid". The pattern
    # tries 2^40 ways on the string before it finds no match, which no machine does in the time the tests allow.
    @testset "a match of a pattern is limited in time" begin
        slow = "^(?:a|a)+\$"
        hostile = "\"" * "a"^40 * "b\""
        function given(f)
            started = time()
            error = try
                f()
            catch e
                e
            end
            return error, time() - started
        end
        v = compile_schema("""{ "pattern": "$(escape_string(slow))" }""")
        @test expect_valid(v, "\"aaaa\"", true)
        error, took = given(() -> isvalid(v, hostile))
        @test error isa PatternMatchError
        @test error.limit == :time && error.pattern == slow
        @test occursin("which is 1 second", sprint(showerror, error))
        @test 0.9 < took < 20
        @test !(@atomic v.busy)
        # The option, for each entry point, failing fast and collecting, for pattern and for patternProperties.
        for schema in ["""{ "pattern": "$(escape_string(slow))" }""",
                """{ "items": { "type": "string", "pattern": "$(escape_string(slow))" } }""",
                """{ "patternProperties": { "$(escape_string(slow))": true }, "additionalProperties": false }""",
                """{ "propertyNames": { "pattern": "$(escape_string(slow))" } }""",
                """{ "unevaluatedProperties": false, "patternProperties": { "$(escape_string(slow))": true } }"""]
            v = compile_schema(schema; pattern_match_limit=0.05)
            instance = occursin("ropert", schema) ? "{" * hostile * ": 1}" :
                occursin("items", schema) ? "[" * hostile * "]" : hostile
            for check in [() -> isvalid(v, instance), () -> validate(v, instance),
                    () -> isvalid(v, parse_document(instance)), () -> validate(v, parse_document(instance)),
                    () -> isvalid(v, Vector{UInt8}(instance)), () -> evaluate(v, instance),
                    () -> evaluate(v, instance, ResultsCollector(Basic)),
                    () -> evaluate(v, parse_document(instance), ResultsCollector(Verbose))]
                error, took = given(check)
                @test error isa PatternMatchError && error.limit == :time
                @test error isa PatternMatchError && occursin("which is 0.05 seconds", error.message)
                @test took < 5
                # The validator is fit for the next validation.
                @test !(@atomic v.busy)
            end
            @test isvalid(v, replace(instance, "b\"" => "\""))
        end
        # A limit that leaves the match its time, and no limit: 2^22 ways are tried in less than two minutes.
        mild = "\"" * "a"^22 * "b\""
        @test expect_valid(compile_schema("""{ "pattern": "$(escape_string(slow))" }"""; pattern_match_limit=120), mild, false)
        @test expect_valid(compile_schema("""{ "pattern": "$(escape_string(slow))" }"""; pattern_match_limit=nothing), mild,
            false)
        @test throws(() -> isvalid(compile_schema("""{ "pattern": "$(escape_string(slow))" }""";
            pattern_match_limit=0.001), mild), PatternMatchError)
        # A limit is a positive time. No time is not "no limit".
        @test throws(() -> compile_schema("{}"; pattern_match_limit=0), ArgumentError)
        @test throws(() -> compile_schema("{}"; pattern_match_limit=-1), ArgumentError)
        # A pattern with a faster matcher that is valid only without the u flag is decided by the engine for a
        # string beyond the Basic Multilingual Plane, and the validator is fit after its error too.
        units = compile_schema("""{ "pattern": "^[a-z]+\\\\-\$" }""")
        @test units.program.may_throw
        @test expect_valid(units, "\"ab-\"", true)
        @test expect_valid(units, "\"\U0001F600-\"", false)
        # Two validators of one pattern have each their own limit.
        quick = compile_schema("""{ "pattern": "$(escape_string(slow))" }"""; pattern_match_limit=0.001)
        patient = compile_schema("""{ "pattern": "$(escape_string(slow))" }"""; pattern_match_limit=120)
        @test throws(() -> isvalid(quick, mild), PatternMatchError)
        @test expect_valid(patient, mild, false)
        @test throws(() -> isvalid(quick, mild), PatternMatchError)
        # A property name the schema declares is matched against its patterns when the schema is compiled, with the
        # same limit, so compile_schema throws the error.
        declared = """{ "properties": { "$("a"^40)b": true }, "patternProperties": { "$(escape_string(slow))": true } }"""
        error, took = given(() -> compile_schema(declared; pattern_match_limit=0.05))
        @test error isa PatternMatchError && error.limit == :time
        @test took < 5
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
