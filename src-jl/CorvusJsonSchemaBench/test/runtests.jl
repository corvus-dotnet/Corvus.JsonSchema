# The benchmark harnesses run and print what their readers expect. Nothing here measures anything.
#
#     julia --project=. test/runtests.jl
using Test
using CorvusJsonSchemaBenchmark

const ROOT = normpath(joinpath(@__DIR__, ".."))
const JULIA = Base.julia_cmd()[1]

# Runs a program of this project in a process of its own. It returns the exit code, standard output and standard
# error.
function run_program(arguments::Vector{String})
    out, err = IOBuffer(), IOBuffer()
    command = `$JULIA --startup-file=no --project=$ROOT $arguments`
    process = run(pipeline(ignorestatus(command); stdout=out, stderr=err))
    return process.exitcode, String(take!(out)), String(take!(err))
end

@testset "benchmark harnesses" begin
    @testset "the lines of an instances file" begin
        lines = CorvusJsonSchemaBenchmark.instance_lines(Vector{UInt8}("{\"a\": 1}\n\n  \t\r\n[1, 2]\r\n3"))
        @test String.(lines) == ["{\"a\": 1}", "[1, 2]\r", "3"]
        @test isempty(CorvusJsonSchemaBenchmark.instance_lines(UInt8[]))
    end

    @testset "the jsonschema-benchmark protocol program" begin
        main = joinpath(ROOT, "jsonschema-benchmark", "main.jl")
        mktempdir() do dir
            schema = joinpath(dir, "schema.json")
            instances = joinpath(dir, "instances.jsonl")
            write(schema, """{"type": "object", "properties": {"id": {"type": "integer"}}, "required": ["id"]}""")

            # Four positive figures on one line: cold,warm,compile,parse.
            write(instances, "{\"id\": 1}\n{\"id\": 2}\n")
            code, out, err = run_program([main, schema, instances])
            @test code == 0
            @test occursin(r"^[1-9][0-9]*,[1-9][0-9]*,[1-9][0-9]*,[1-9][0-9]*\n$", out)
            @test err == ""

            # Exit code 1, and no figures, for an instance that is not valid.
            write(instances, "{\"id\": 1}\n{\"id\": \"two\"}\n")
            code, out, _ = run_program([main, schema, instances])
            @test code == 1
            @test out == ""

            # Exit code 2 for a mistake: the arguments, a missing file, text that is not JSON, a schema that does
            # not compile.
            code, out, err = run_program([main, schema])
            @test code == 2 && out == "" && occursin("Usage", err)
            code, out, err = run_program([main, schema, joinpath(dir, "missing.jsonl")])
            @test code == 2 && out == "" && err != ""
            write(instances, "{\"id\": 1\n")
            code, out, err = run_program([main, schema, instances])
            @test code == 2 && out == "" && occursin("invalid JSON", err)
            write(instances, "{\"id\": 1}\n")
            write(schema, """{"\$ref": "#/nowhere"}""")
            code, out, err = run_program([main, schema, instances])
            @test code == 2 && out == "" && occursin("CompileError", err)
        end
    end

    @testset "the comparison in one process" begin
        compare = joinpath(ROOT, "compare.jl")
        mktempdir() do dir
            # One corpus as a jsonschema-benchmark checkout has made it, and one with only schema.json, whose
            # format keyword the program removes.
            mkpath(joinpath(dir, "first"))
            write(joinpath(dir, "first", "schema-noformat.json"),
                """{"\$schema": "http://json-schema.org/draft-07/schema#", "type": "array", "items": {"type": "integer"}}""")
            write(joinpath(dir, "first", "instances.jsonl"), "[1, 2, 3]\n[]\n")
            mkpath(joinpath(dir, "second"))
            write(joinpath(dir, "second", "schema.json"),
                """{"\$schema": "http://json-schema.org/draft-07/schema#", "type": "string", "format": "date"}""")
            write(joinpath(dir, "second", "instances.jsonl"), "\"not a date\"\n")
            # A corpus with an instance that is not valid is reported, not timed.
            mkpath(joinpath(dir, "third"))
            write(joinpath(dir, "third", "schema-noformat.json"),
                """{"\$schema": "http://json-schema.org/draft-07/schema#", "type": "string"}""")
            write(joinpath(dir, "third", "instances.jsonl"), "1\n\"a\"\n")

            code, out, err = run_program([compare, "--schemas", dir, "--budget-ms", "20"])
            @test code == 0
            lines = split(chomp(out), '\n')
            @test length(lines) == 5
            @test occursin(r"^corpus +count +corvus +jsonschema$", lines[1])
            @test occursin(r"^first +2 +[0-9.]+ us +[0-9.]+ us$", lines[2])
            @test occursin(r"^second +1 +[0-9.]+ us +[0-9.]+ us$", lines[3])
            @test occursin(r"^third +2 +invalid 1 +invalid 1$", lines[4])
            @test occursin(r"^corvus / jsonschema: geomean [0-9.]+, faster on [0-9] of 2$", lines[5])

            code, out, _ = run_program([compare, "--schemas", dir, "--only", "first", "--engines", "corvus",
                "--budget-ms=20"])
            @test code == 0
            @test length(split(chomp(out), '\n')) == 2

            code, _, err = run_program([compare, "--schemas", dir, "--engines", "nobody"])
            @test code == 2 && occursin("unknown engine", err)
        end
    end
end
