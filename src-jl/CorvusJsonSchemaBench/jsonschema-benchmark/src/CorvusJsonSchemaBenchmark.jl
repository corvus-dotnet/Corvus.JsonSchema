# CorvusJsonSchema's implementation of the jsonschema-benchmark protocol
# (https://github.com/sourcemeta-research/jsonschema-benchmark):
#
#     julia --project=. main.jl <schema.json> <instances.jsonl>
#
# It parses every instance, compiles the schema, validates every instance once cold, warms up, and validates once
# warm. It prints one line, cold,warm,compile,parse in nanoseconds, and exits 1 if an instance is invalid.
#
# The benchmark defines warm as steady state. The warm-up follows a rule that is the same for every engine whatever
# its runtime: validation passes for a fixed time (WARMUP_NS), and at least MIN_WARMUP_PASSES passes. The warm figure
# is the last of those passes.
#
# The program is a package so that Julia compiles it when the image is built. A script is compiled again by every
# process that runs it.
module CorvusJsonSchemaBenchmark

using CorvusJsonSchema

const WARMUP_NS = UInt64(2_000_000_000)
const MIN_WARMUP_PASSES = 100

function validate_all(v::Validator, documents::Vector{Document})
    valid = true
    for d in documents
        if !isvalid(v, d)
            valid = false
        end
    end
    return valid
end

const SPACE = (0x20, 0x09, 0x0a, 0x0b, 0x0c, 0x0d)

# The lines of the instances file that are not blank, each as bytes of its own.
function instance_lines(contents::Vector{UInt8})
    texts = Vector{UInt8}[]
    from = 1
    n = length(contents)
    while from <= n
        to = from
        while to <= n && contents[to] != 0x0a
            to += 1
        end
        if any(b -> !(b in SPACE), view(contents, from:to-1))
            push!(texts, contents[from:to-1])
        end
        from = to + 1
    end
    return texts
end

# Runs the protocol and prints its line. It returns the exit code of the process.
function run_protocol(io::IO, schema_file::String, instances_file::String, warmup_ns::UInt64, min_passes::Int)
    schema = read(schema_file)
    texts = instance_lines(read(instances_file))

    parse_start = time_ns()
    documents = Vector{Document}(undef, length(texts))
    for i in eachindex(texts)
        documents[i] = parse_document(texts[i])
    end
    parse = time_ns() - parse_start

    # The benchmark's schema-noformat.json has no format keywords, and the defaults leave format as an annotation.
    compile_start = time_ns()
    v = compile_schema(schema)
    compile = time_ns() - compile_start

    cold_start = time_ns()
    valid = validate_all(v, documents)
    cold = time_ns() - cold_start
    valid || return 1

    # The warm pass is the last pass of the warm-up loop, timed at the same call site as the passes before it, as in
    # the harnesses of the other runtimes that compile while they run.
    deadline = time_ns() + warmup_ns
    warm = UInt64(0)
    passes = 0
    while passes < min_passes || time_ns() < deadline
        start = time_ns()
        valid = validate_all(v, documents) && valid
        warm = time_ns() - start
        passes += 1
    end
    valid || return 1

    println(io, Int(cold), ",", Int(warm), ",", Int(compile), ",", Int(parse))
    return 0
end

# The program. It returns the exit code of the process: 0, 1 for an instance that is not valid, and 2 for a mistake
# in the arguments, a file that cannot be read, text that is not JSON, or a schema that does not compile.
function main(args::Vector{String})
    if length(args) != 2
        println(stderr, "Usage: julia --project=. main.jl <schema> <instances>")
        return 2
    end
    try
        return run_protocol(stdout, args[1], args[2], WARMUP_NS, MIN_WARMUP_PASSES)
    catch err
        showerror(stderr, err)
        println(stderr)
        return 2
    end
end

# While Julia writes the package image, the protocol runs once on a small corpus with no warm-up time, so that the
# image holds the compiled program. The library's own image holds the evaluator.
function precompile_workload()
    mktempdir() do dir
        schema = joinpath(dir, "schema.json")
        instances = joinpath(dir, "instances.jsonl")
        write(schema, """{"type": "object", "properties": {"id": {"type": "integer"}}, "required": ["id"]}""")
        write(instances, "{\"id\": 1}\n\n{\"id\": 2}\n")
        run_protocol(devnull, schema, instances, UInt64(0), 2)
        write(instances, "{\"id\": \"two\"}\n")
        run_protocol(devnull, schema, instances, UInt64(0), 2)
    end
    precompile(run_protocol, (typeof(stdout), String, String, UInt64, Int))
    precompile(run_protocol, (Base.PipeEndpoint, String, String, UInt64, Int))
    precompile(run_protocol, (Base.TTY, String, String, UInt64, Int))
    precompile(run_protocol, (IOStream, String, String, UInt64, Int))
    precompile(main, (Vector{String},))
    return nothing
end

if ccall(:jl_generating_output, Cint, ()) == 1
    precompile_workload()
end

end
