# Validates the jsonschema-benchmark corpora (https://github.com/sourcemeta-research/jsonschema-benchmark) with
# CorvusJsonSchema and with JSONSchema.jl, the validator of the JuliaIO organisation, in one process. The engines'
# timed passes are interleaved, so drift in the machine's speed affects them alike.
#
#     julia --project=. compare.jl --schemas ../../../jsonschema-benchmark/schemas [--only a,b]
#         [--engines corvus,jsonschema] [--budget-ms 1000]
#
# For each corpus and engine, the schema is compiled, every instance is validated once, and then whole passes over
# the instances are repeated for the time budget after a warm-up. The schema is the benchmark's
# schema-noformat.json when the checkout has made it, and otherwise schema.json with its format keywords removed,
# which is what that file is. Every instance in the corpora is valid, so an engine that rejects one is reported
# instead of timed. The table shows the median warm pass per engine.
#
# Each engine is used as its documentation says. CorvusJsonSchema validates documents parsed by parse_document.
# JSONSchema.jl validates values parsed by JSON.jl. JSONSchema.jl implements draft 4, 6 and 7, so a corpus whose
# schema needs a later draft is reported for it and not timed.
using CorvusJsonSchema
using Printf
import JSON
import JSONSchema

# An engine on a corpus, compiled and with its instances parsed, is a function that validates every instance and
# returns how many were valid.

function prepare_corvus(schema::Vector{UInt8}, schema_file::String, lines::Vector{Vector{UInt8}})
    v = compile_schema(schema)
    documents = Document[parse_document(line) for line in lines]
    return function ()
        valid = 0
        for d in documents
            if isvalid(v, d)
                valid += 1
            end
        end
        return valid
    end
end

# A schema of a draft the engine does not implement.
struct LaterDraft <: Exception end

function prepare_jsonschema(schema::Vector{UInt8}, schema_file::String, lines::Vector{Vector{UInt8}})
    parsed = JSON.parse(String(copy(schema)))
    # JSONSchema.jl implements draft 4, 6 and 7. It does not refuse a later draft's schema: it leaves out the
    # keywords it does not know, accepts every instance, and would be timed for work it did not do.
    dialect = parsed isa AbstractDict ? get(parsed, "\$schema", "") : ""
    dialect isa AbstractString && occursin(r"draft/20(19|20)-", dialect) && throw(LaterDraft())
    compiled = JSONSchema.Schema(parsed; parent_dir=dirname(schema_file))
    instances = Any[JSON.parse(String(copy(line))) for line in lines]
    return function ()
        valid = 0
        for instance in instances
            if JSONSchema.validate(compiled, instance) === nothing
                valid += 1
            end
        end
        return valid
    end
end

const ENGINES = ["corvus" => prepare_corvus, "jsonschema" => prepare_jsonschema]

# Removes string-valued format members, as the benchmark's schema-noformat.json does.
function strip_format!(value)
    if value isa AbstractDict
        if get(value, "format", nothing) isa AbstractString
            delete!(value, "format")
        end
        foreach(strip_format!, values(value))
    elseif value isa AbstractVector
        foreach(strip_format!, value)
    end
    return value
end

# A corpus's schema without format keywords, and the file it stands for.
function read_schema(dir::String)
    file = abspath(joinpath(dir, "schema-noformat.json"))
    isfile(file) && return read(file), file
    schema = JSON.parse(read(joinpath(dir, "schema.json"), String); dicttype=Dict{String,Any})
    return Vector{UInt8}(JSON.json(strip_format!(schema))), file
end

const SPACE = (0x20, 0x09, 0x0a, 0x0b, 0x0c, 0x0d)

# The lines of a file that are not blank.
function read_lines(file::String)
    lines = Vector{UInt8}[]
    for line in eachsplit(read(file, String), '\n')
        any(b -> !(b in SPACE), codeunits(line)) && push!(lines, Vector{UInt8}(line))
    end
    return lines
end

split_list(list::String) = String[item for item in split(list, ',') if !isempty(item)]

function fail(message)
    println(stderr, message)
    exit(2)
end

function options(args::Vector{String})
    values = Dict("schemas" => "../../../jsonschema-benchmark/schemas", "only" => "", "engines" => "corvus,jsonschema",
        "budget-ms" => "1000")
    i = 1
    while i <= length(args)
        name = startswith(args[i], "--") ? args[i][3:end] : fail("unknown argument $(args[i])")
        if occursin('=', name)
            name, value = split(name, '='; limit=2)
        else
            i < length(args) || fail("--$name needs a value")
            i += 1
            value = args[i]
        end
        haskey(values, name) || fail("unknown option --$name")
        values[name] = value
        i += 1
    end
    return values
end

function main(args::Vector{String})
    opts = options(args)
    selected = Pair{String,Function}[]
    for name in split_list(opts["engines"])
        at = findfirst(e -> e.first == name, ENGINES)
        at === nothing && fail("unknown engine $name")
        push!(selected, ENGINES[at])
    end
    isdir(opts["schemas"]) || fail("no directory $(opts["schemas"])")
    budget = parse(Int, opts["budget-ms"]) * 1_000_000
    corpora = split_list(opts["only"])

    @printf("%-24s %8s", "corpus", "count")
    foreach(e -> @printf(" %14s", e.first), selected)
    println()
    ratios = [Float64[] for _ in selected]
    for name in sort!(readdir(opts["schemas"]))
        dir = joinpath(opts["schemas"], name)
        (isdir(dir) && (isempty(corpora) || name in corpora)) || continue
        schema, schema_file = read_schema(dir)
        lines = read_lines(joinpath(dir, "instances.jsonl"))
        passes = Vector{Union{Nothing,Function}}(nothing, length(selected))
        notes = fill("", length(selected))
        for (i, engine) in enumerate(selected)
            pass = try
                engine.second(schema, schema_file, lines)
            catch err
                notes[i] = err isa LaterDraft ? "later draft" : "error"
                continue
            end
            valid = try
                pass()
            catch
                notes[i] = "error"
                continue
            end
            if valid != length(lines)
                notes[i] = "invalid $(length(lines) - valid)"
                continue
            end
            passes[i] = pass
        end
        # Warm up each engine for the budget, then interleave timed passes for the budget again.
        for pass in passes
            pass === nothing && continue
            stop = time_ns() + budget
            n = 0
            while n < 1000 && time_ns() < stop
                pass()
                n += 1
            end
        end
        samples = [UInt64[] for _ in selected]
        stop = time_ns() + budget * length(selected)
        rounds = 0
        while (time_ns() < stop || rounds < 5) && rounds < 2000
            for (i, pass) in enumerate(passes)
                pass === nothing && continue
                start = time_ns()
                pass()
                push!(samples[i], time_ns() - start)
            end
            rounds += 1
        end
        @printf("%-24s %8d", name, length(lines))
        reference = NaN
        for i in eachindex(selected)
            if passes[i] === nothing
                @printf(" %14s", notes[i])
                continue
            end
            sort!(samples[i])
            median = samples[i][length(samples[i])÷2+1] / 1000
            @printf(" %11.1f us", median)
            if i == 1
                reference = median
            elseif !isnan(reference)
                push!(ratios[i], reference / median)
            end
        end
        println()
    end
    for i in 2:length(selected)
        isempty(ratios[i]) && continue
        geomean = exp(sum(log, ratios[i]) / length(ratios[i]))
        @printf("%s / %s: geomean %.3f, faster on %d of %d\n", selected[1].first, selected[i].first, geomean,
            count(<(1), ratios[i]), length(ratios[i]))
    end
    return nothing
end

main(ARGS)
