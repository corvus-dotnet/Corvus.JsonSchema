# Drives the harness over IHOP as Bowtie does (start, dialect, run with the suite's remotes as the registry, stop),
# in a separate process: every required case of the JSON-Schema-Test-Suite, and the annotation suite's assertions,
# compared with the expected results. No containers.
#
#     julia --project=. -t 4 test/runtests.jl
using Test
using CorvusJsonSchema
using CorvusJsonSchemaBowtie

const B = CorvusJsonSchemaBowtie
const ROOT = normpath(joinpath(@__DIR__, ".."))

const DRAFTS = [
    ("draft4", "http://json-schema.org/draft-04/schema#", "4"),
    ("draft6", "http://json-schema.org/draft-06/schema#", "6"),
    ("draft7", "http://json-schema.org/draft-07/schema#", "7"),
    ("draft2019-09", "https://json-schema.org/draft/2019-09/schema", "2019"),
    ("draft2020-12", "https://json-schema.org/draft/2020-12/schema", "2020"),
]

# The harness in its own process.
struct HarnessProcess
    process::Base.Process
end

# Writes one request and reads its response, one line each. The response is its bytes and the range of its value.
function send(h::HarnessProcess, request::String)
    occursin('\n', request) && error("a request is one line")
    write(h.process, request, '\n')
    flush(h.process)
    answer = readline(h.process)
    isempty(answer) && error("no response to " * first(request, 200))
    bytes = Vector{UInt8}(answer)
    return bytes, B.whole_value(bytes)
end

function start_harness()
    command = `$(Base.julia_cmd()[1]) --startup-file=no --project=$ROOT $(joinpath(ROOT, "bowtie.jl"))`
    h = HarnessProcess(open(pipeline(command; stderr=stderr), "r+"))
    b, response = send(h, """{"cmd":"start","version":1}""")
    implementation = B.member(b, response, "implementation")
    field(name) = B.text(b, B.member(b, implementation, name))
    @test B.raw(b, B.member(b, response, "version")) == "1"
    @test field("language") == "julia"
    @test field("name") == "corvus-jsonschema"
    @test field("version") == string(pkgversion(CorvusJsonSchema))
    @test field("language_version") == string(VERSION)
    @test [B.text(b, d) for d in B.items(b, B.member(b, implementation, "dialects"))] ==
          [d.first for d in B.DIALECTS]
    @test length(B.DIALECTS) == length(DRAFTS)
    for name in ["homepage", "documentation", "issues", "source", "os", "os_version"]
        @test B.member(b, implementation, name) !== nothing
    end
    return h
end

function set_dialect(h::HarnessProcess, dialect::String)
    b, response = send(h, """{"cmd":"dialect","dialect":$(B.quoted(dialect))}""")
    B.raw(b, B.member(b, response, "ok")) == "true" || error("the harness refused the dialect $dialect")
    return nothing
end

function stop(h::HarnessProcess)
    write(h.process, "{\"cmd\":\"stop\"}\n")
    flush(h.process)
    wait(h.process)
    return h.process.exitcode
end

# The JSON-Schema-Test-Suite checkout: the repository's submodule, or JSON_SCHEMA_TEST_SUITE.
function suite_root()
    root = get(ENV, "JSON_SCHEMA_TEST_SUITE", joinpath(ROOT, "..", "..", "JSON-Schema-Test-Suite"))
    isdir(joinpath(root, "tests")) ||
        error("JSON-Schema-Test-Suite not found at $root (set JSON_SCHEMA_TEST_SUITE, or check out the submodule)")
    return root
end

json_files(dir::String) = sort!([joinpath(dir, f) for f in readdir(dir) if endswith(f, ".json") &&
                                 isfile(joinpath(dir, f))])

# Bowtie's registry: every remote, keyed by its http://localhost:1234/ URI.
function registry(root::String)
    remotes = joinpath(root, "remotes")
    entries = String[]
    for (dir, _, files) in walkdir(remotes), file in sort(files)
        endswith(file, ".json") || continue
        path = joinpath(dir, file)
        bytes = read(path)
        relative = replace(relpath(path, remotes), '\\' => '/')
        push!(entries, B.quoted("http://localhost:1234/" * relative) * ":" * B.compact(bytes, B.whole_value(bytes)))
    end
    return "{" * join(sort!(entries), ",") * "}"
end

# The text of a member that is a string, or a default.
function text_of(b::Vector{UInt8}, object::UnitRange{Int}, name::String, default::String="")
    value = B.member(b, object, name)
    return value === nothing ? default : B.text(b, value)
end

run_request(seq::Int, output::String, description::String, schema::String, remotes::String, tests::Vector{String}) =
    "{\"cmd\":\"run\",\"seq\":$seq," * (output == "" ? "" : "\"output\":$(B.quoted(output)),") *
    "\"case\":{\"description\":$(B.quoted(description)),\"schema\":$schema,\"registry\":$remotes," *
    "\"tests\":[" * join(tests, ",") * "]}}"

# The results of a run, or nothing if the case errored.
function results_of(b::Vector{UInt8}, response::UnitRange{Int}, seq::Int)
    B.raw(b, B.member(b, response, "seq")) == string(seq) || error("the response to request $seq has another seq")
    results = B.member(b, response, "results")
    return results === nothing ? nothing : B.items(b, results)
end

# JSON equality, with numbers compared by value and objects whatever their members' order, which is what const is.
same_json(a::String, b::String) = isvalid(compile_schema("{\"const\":" * b * "}"), a)

const COMPATIBILITY_ORDER = ["3", "4", "6", "7", "2019", "2020"]

function compatible(level::String, compatibility::String)
    at = findfirst(==(level), COMPATIBILITY_ORDER)
    if startswith(compatibility, "<=")
        most = findfirst(==(compatibility[3:end]), COMPATIBILITY_ORDER)
        return most !== nothing && at <= most
    end
    least = findfirst(==(compatibility), COMPATIBILITY_ORDER)
    return least !== nothing && at >= least
end

@testset "Bowtie harness" begin
    @testset "the reader of requests" begin
        b = Vector{UInt8}(""" {"a": [1, {"b": "x\\"]}"}, null], "c\\u00e9\\n": -1.5e3, "d": {}, "e": "\\ud83d\\ude00"} """)
        value = B.whole_value(b)
        @test [name for (name, _) in B.members(b, value)] == ["a", "cé\n", "d", "e"]
        @test B.raw(b, B.member(b, value, "cé\n")) == "-1.5e3"
        @test [B.raw(b, item) for item in B.items(b, B.member(b, value, "a"))] == ["1", "{\"b\": \"x\\\"]}\"}", "null"]
        @test B.compact(b, B.member(b, value, "a")) == "[1,{\"b\":\"x\\\"]}\"},null]"
        @test B.text(b, B.member(b, value, "e")) == "\U1F600"
        @test isempty(B.members(b, B.member(b, value, "d")))
        @test B.member(b, value, "missing") === nothing
        @test B.quoted("a\"b\\c\né") == "\"a\\\"b\\\\c\\u000aé\""
        @test_throws B.NotJson B.whole_value(Vector{UInt8}("{\"a\": [1, 2}"))
        @test_throws B.NotJson B.whole_value(Vector{UInt8}("1 2"))
    end

    @testset "requests the harness cannot answer end it" begin
        for requests in ["{\"cmd\":\"run\"}\n", "{\"cmd\":\"start\",\"version\":2}\n", "not json\n",
            "{\"cmd\":\"start\",\"version\":1}\n{\"cmd\":\"dance\"}\n"]
            output = IOBuffer()
            code = redirect_stderr(devnull) do
                B.main(IOBuffer(requests), output)
            end
            @test code == 1
        end
        # The end of the input, and blank lines, are not errors.
        @test B.main(IOBuffer("\n{\"cmd\":\"start\",\"version\":1}\n\n"), IOBuffer()) == 0
    end

    @testset "the required suite over IHOP" begin
        root = suite_root()
        remotes = registry(root)
        h = start_harness()
        seq, total, failures = 0, 0, String[]
        for (directory, dialect, _) in DRAFTS
            set_dialect(h, dialect)
            for file in json_files(joinpath(root, "tests", directory))
                bytes = read(file)
                for group in B.items(bytes, B.whole_value(bytes))
                    description = text_of(bytes, group, "description")
                    tests = B.items(bytes, B.member(bytes, group, "tests"))
                    seq += 1
                    request = run_request(seq, "", description, B.compact(bytes, B.member(bytes, group, "schema")),
                        remotes, ["{\"description\":$(B.quoted(text_of(bytes, t, "description")))," *
                                  "\"instance\":$(B.compact(bytes, B.member(bytes, t, "data")))}" for t in tests])
                    b, response = send(h, request)
                    results = results_of(b, response, seq)
                    for (i, t) in enumerate(tests)
                        total += 1
                        want = B.raw(bytes, B.member(bytes, t, "valid"))
                        valid = results === nothing || i > length(results) ? nothing : B.member(b, results[i], "valid")
                        if valid === nothing || B.raw(b, valid) != want
                            push!(failures, "$directory/$(basename(file)): $description / " *
                                            text_of(bytes, t, "description"))
                        end
                    end
                end
            end
        end
        @test stop(h) == 0
        foreach(println, failures)
        @test isempty(failures)
        @test total > 4000
        println("$total tests in $seq cases")
    end

    @testset "the annotation suite over IHOP" begin
        root = suite_root()
        dir = joinpath(root, "annotations", "tests")
        isdir(dir) || error("annotation tests not found at $dir")
        remotes = registry(root)
        h = start_harness()
        seq, total, failures = 0, 0, String[]
        for (_, dialect, level) in DRAFTS
            set_dialect(h, dialect)
            for file in json_files(dir)
                bytes = read(file)
                for group in B.items(bytes, B.member(bytes, B.whole_value(bytes), "suite"))
                    compatibility = text_of(bytes, group, "compatibility")
                    compatibility != "" && !compatible(level, compatibility) && continue
                    description = text_of(bytes, group, "description")
                    tests = B.items(bytes, B.member(bytes, group, "tests"))
                    seq += 1
                    request = run_request(seq, "annotations", description,
                        B.compact(bytes, B.member(bytes, group, "schema")), remotes,
                        ["{\"description\":\"\",\"instance\":$(B.compact(bytes, B.member(bytes, t, "instance")))}"
                         for t in tests])
                    b, response = send(h, request)
                    results = results_of(b, response, seq)
                    if results === nothing || length(results) != length(tests)
                        error("$(basename(file)) ($dialect): $description: the case errored")
                    end
                    for (i, t) in enumerate(tests)
                        produced = B.member(b, results[i], "annotations")
                        produced = produced === nothing ? UnitRange{Int}[] : B.items(b, produced)
                        for assertion in B.items(bytes, B.member(bytes, t, "assertions"))
                            total += 1
                            location = text_of(bytes, assertion, "location")
                            keyword = text_of(bytes, assertion, "keyword")
                            # The annotations for this instance location and keyword, by the schema's location.
                            actual = Dict{String,String}()
                            for a in produced
                                if text_of(b, a, "instanceLocation") == location && text_of(b, a, "keyword") == keyword
                                    at = text_of(b, a, "keywordLocation")
                                    suffix = "/" * keyword
                                    endswith(at, suffix) && (at = at[1:prevind(at, ncodeunits(at) - ncodeunits(suffix) + 1)])
                                    actual[at] = B.raw(b, B.member(b, a, "annotation"))
                                end
                            end
                            expected = Dict{String,String}(name => B.compact(bytes, value)
                                                           for (name, value) in B.members(bytes, B.member(bytes, assertion, "expected")))
                            same = length(actual) == length(expected) &&
                                   all(haskey(actual, at) && same_json(actual[at], want) for (at, want) in expected)
                            same || push!(failures, "$(basename(file)) ($dialect): $location $keyword: " *
                                                    "expected $expected got $actual")
                        end
                    end
                end
            end
        end
        @test stop(h) == 0
        foreach(println, failures)
        @test isempty(failures)
        @test total > 0
        println("$total annotation assertions in $seq cases")
    end
end
