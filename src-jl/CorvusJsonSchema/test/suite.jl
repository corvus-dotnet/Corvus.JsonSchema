# Runs the JSON-Schema-Test-Suite (the repository's submodule) against the evaluator, as the C#, Rust and Go suite
# runners do: required and optional tests with format as an annotation, optional/format with format asserted. Every
# case runs fail-fast (as a document, as bytes and as a string) and through a results collector at each level.
#
# Set JSON_SCHEMA_TEST_SUITE to use a different checkout, SUITE_DRAFT and SUITE_FILTER to narrow the run.

const SUITE_DRAFTS = [("draft4", Draft4), ("draft6", Draft6), ("draft7", Draft7), ("draft2019-09", Draft201909),
    ("draft2020-12", Draft202012)]

# Exclusions, matching the C#, Rust and Go runners: zero-terminated floats.
const SUITE_EXCLUDED_FILES = Set(["draft4/optional/zeroTerminatedFloats.json"])

const RESULTS_LEVELS = [Basic, Detailed, Verbose]

# Evaluates one instance every way the API offers and checks that they agree. It returns the result, or a message
# when something went wrong.
function run_suite_case(v::Validator, data::String)
    try
        document = parse_document(data)
        fast = validate(v, document)
        isvalid(v, document) == fast || return "isvalid disagrees with validate ($fast)"
        verbose = SchemaResult[]
        for level in RESULTS_LEVELS
            c = ResultsCollector(level)
            ok = evaluate(v, document, c)
            ok == fast || return "$level returned $ok, fast returned $fast"
            summary = any(r -> r.evaluation_location == "" && r.document_evaluation_location == "" &&
                                   r.is_match == ok, results(c))
            summary || return "$level: no root summary row matching the result"
            verbose = results(c)
        end
        # The same instance straight from the text, in the validator's reused buffers.
        bytes = Vector{UInt8}(data)
        from_bytes = validate(v, bytes)
        (from_bytes == fast && isvalid(v, bytes) == fast) ||
            return "validate(bytes) returned $from_bytes, the document $fast"
        from_string = validate(v, data)
        (from_string == fast && isvalid(v, data) == fast) ||
            return "validate(string) returned $from_string, the document $fast"
        c = ResultsCollector(Verbose)
        evaluate(v, bytes, c)
        verbose == results(c) || return "evaluate(bytes)'s verbose results differ from the document's"
        return fast
    catch err
        return "exception during evaluation: " * sprint(showerror, err)
    end
end

mutable struct SuiteRunner
    resolver::Any
    filter::String
    total::Int
    failures::Vector{String}
    # Failing leap second cases of the format run, which are not counted as failures.
    skipped::Vector{String}
    areas::Vector{String}
    passed::Dict{String,Int}
    counted::Dict{String,Int}
end

function run_suite_file!(r::SuiteRunner, dialect::Dialect, file::String, label::String, area::String,
    assert_format::Bool)
    d = parse_document(read(file))
    if !haskey(r.counted, area)
        push!(r.areas, area)
        r.counted[area] = 0
        r.passed[area] = 0
    end
    for group in items_of(d, d.root)
        description = text_of(d, member(d, group, "description"))
        if r.filter != "" && !occursin(r.filter, description) && !occursin(r.filter, label)
            continue
        end
        schema = json_of(d, member(d, group, "schema"))
        validator, compile_error = nothing, ""
        try
            validator = assert_format ?
                        compile_schema(schema; default_dialect=dialect, resolver=r.resolver, assert_format=true) :
                        compile_schema(schema; default_dialect=dialect, resolver=r.resolver)
        catch err
            compile_error = "compile error: " * sprint(showerror, err)
        end
        for test in items_of(d, member(d, group, "tests"))
            r.total += 1
            r.counted[area] += 1
            test_description = text_of(d, member(d, test, "description"))
            expected = is_true(d, member(d, test, "valid"))
            actual = validator === nothing ? compile_error :
                     run_suite_case(validator, json_of(d, member(d, test, "data")))
            if actual === expected
                r.passed[area] += 1
                continue
            end
            # Leap seconds are skipped in the format run, as in the C# runner.
            if assert_format && occursin("leap second", lowercase(test_description))
                r.passed[area] += 1
                push!(r.skipped, "$label [$description] $test_description")
                continue
            end
            push!(r.failures, "$label [$description] $test_description: expected $expected, got $actual")
        end
    end
    return nothing
end

@testset "JSON-Schema-Test-Suite" begin
    root = suite_root()
    tests = joinpath(root, "tests")
    if !isdir(tests)
        error("JSON-Schema-Test-Suite not found at $root (set JSON_SCHEMA_TEST_SUITE, or check out the submodule)")
    end
    runner = SuiteRunner(remote_resolver(root), get(ENV, "SUITE_FILTER", ""), 0, String[], String[], String[],
        Dict{String,Int}(), Dict{String,Int}())
    draft_filter = get(ENV, "SUITE_DRAFT", "")
    for (name, dialect) in SUITE_DRAFTS
        (draft_filter != "" && draft_filter != name) && continue
        dir = joinpath(tests, name)
        for f in json_files(dir)
            run_suite_file!(runner, dialect, f, name * "/" * basename(f), name, false)
        end
        for f in json_files(joinpath(dir, "optional"))
            label = name * "/optional/" * basename(f)
            label in SUITE_EXCLUDED_FILES || run_suite_file!(runner, dialect, f, label, name * "/optional", false)
        end
        for f in json_files(joinpath(dir, "optional", "format"))
            label = name * "/optional/format/" * basename(f)
            run_suite_file!(runner, dialect, f, label, name * "/optional/format", true)
        end
    end
    foreach(println, runner.failures)
    for line in runner.skipped
        println("skipped: ", line)
    end
    for area in runner.areas
        println(rpad(area, 34), " ", lpad(runner.passed[area], 5), "/", runner.counted[area])
    end
    failed = length(runner.failures)
    println("JSON-Schema-Test-Suite: ", runner.total - failed, "/", runner.total, " passed")
    @test failed == 0
    @test runner.total > 0
end
