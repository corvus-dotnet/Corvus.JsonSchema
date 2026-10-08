# A Bowtie (https://github.com/bowtie-json-schema/bowtie) harness for CorvusJsonSchema. It speaks IHOP (one JSON
# request per line on standard input, one response per line on standard output):
#
#   - start reports the implementation and its dialects;
#   - dialect sets the dialect for schemas without $schema;
#   - run compiles the case's schema with the case's registry as the document resolver and validates each instance
#     (for annotations output, through a verbose results collector, reporting each annotation with its instance
#     location and #... keyword location);
#   - stop exits.
#
# A compilation error, an exception, or an evaluation beyond the maximum depth is reported for that case or instance
# as an error, not a crash.
module CorvusJsonSchemaBowtie

using CorvusJsonSchema

include("rawjson.jl")

const DIALECTS = [
    "https://json-schema.org/draft/2020-12/schema" => Draft202012,
    "https://json-schema.org/draft/2019-09/schema" => Draft201909,
    "http://json-schema.org/draft-07/schema#" => Draft7,
    "http://json-schema.org/draft-06/schema#" => Draft6,
    "http://json-schema.org/draft-04/schema#" => Draft4,
]

mutable struct Harness
    started::Bool
    dialect::Dialect
end

Harness() = Harness(false, Draft202012)

# A request the harness cannot answer. The process ends with it.
struct ProtocolError <: Exception
    message::String
end

Base.showerror(io::IO, e::ProtocolError) = print(io, e.message)

strip_fragment(uri::AbstractString) = String(first(split(uri, '#'; limit=2)))

# A JSON pointer token as the text it stands for.
unescape_token(token::AbstractString) = replace(replace(token, "~1" => "/"), "~0" => "~")

describe(err) = err isa DepthExceededError ? "evaluation recursed beyond the maximum depth" : sprint(showerror, err)

errored(message::AbstractString) = "{\"errored\":true,\"context\":{\"message\":" * quoted(message) * "}}"

function start(h::Harness, b::Vector{UInt8}, request::UnitRange{Int})
    version = member(b, request, "version")
    if version === nothing || raw(b, version) != "1"
        throw(ProtocolError("unsupported IHOP version " * (version === nothing ? "null" : raw(b, version))))
    end
    h.started = true
    # The kernel's release on Linux, which is where Bowtie runs harnesses.
    release = try
        strip(read("/proc/sys/kernel/osrelease", String))
    catch
        ""
    end
    implementation = [
        "language" => quoted("julia"),
        "name" => quoted("corvus-jsonschema"),
        "version" => quoted(string(pkgversion(CorvusJsonSchema))),
        "homepage" => quoted("https://github.com/corvus-dotnet/Corvus.JsonSchema"),
        "documentation" => quoted("https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-jl/CorvusJsonSchema"),
        "issues" => quoted("https://github.com/corvus-dotnet/Corvus.JsonSchema/issues"),
        "source" => quoted("https://github.com/corvus-dotnet/Corvus.JsonSchema"),
        "dialects" => "[" * join((quoted(d.first) for d in DIALECTS), ",") * "]",
        "os" => quoted(lowercase(string(Sys.KERNEL))),
        "os_version" => quoted(release),
        "language_version" => quoted(string(VERSION)),
    ]
    return "{\"version\":1,\"implementation\":{" * join((quoted(k) * ":" * v for (k, v) in implementation), ",") * "}}"
end

function set_dialect(h::Harness, b::Vector{UInt8}, request::UnitRange{Int})
    value = member(b, request, "dialect")
    uri = value === nothing ? "" : text(b, value)
    for (known, dialect) in DIALECTS
        if known == uri
            h.dialect = dialect
            return "{\"ok\":true}"
        end
    end
    return "{\"ok\":false}"
end

# Compiles the case's schema, with the case's registry as the document resolver.
function compile(h::Harness, b::Vector{UInt8}, case::UnitRange{Int})
    registry = Dict{String,String}()
    entries = member(b, case, "registry")
    if entries !== nothing && raw(b, entries) != "null"
        for (uri, schema) in members(b, entries)
            registry[strip_fragment(uri)] = raw(b, schema)
        end
    end
    resolver = function (uri::String)
        schema = get(registry, strip_fragment(uri), nothing)
        schema === nothing && return nothing
        return try
            parse_document(schema)
        catch
            nothing
        end
    end
    schema = member(b, case, "schema")
    schema === nothing && throw(ProtocolError("a case with no schema"))
    return compile_schema(raw(b, schema); default_dialect=h.dialect, resolver=resolver)
end

# Evaluates one instance. An exception is an error of that test.
function test(validator::Validator, instance::String, with_annotations::Bool)
    try
        if !with_annotations
            return validate(validator, instance) ? "{\"valid\":true}" : "{\"valid\":false}"
        end
        collector = ResultsCollector(Verbose)
        valid = evaluate(validator, instance, collector)
        found = String[]
        for a in annotations(collector)
            push!(found, "{\"keyword\":" * quoted(unescape_token(a.keyword)) *
                         ",\"instanceLocation\":" * quoted(a.instance_location) *
                         ",\"keywordLocation\":" * quoted(schema_location_fragment(a.schema_location * "/" * a.keyword)) *
                         ",\"annotation\":" * a.value * "}")
        end
        return "{\"valid\":" * (valid ? "true" : "false") * ",\"annotations\":[" * join(found, ",") * "]}"
    catch err
        return errored(describe(err))
    end
end

function run_case(h::Harness, b::Vector{UInt8}, request::UnitRange{Int})
    value = member(b, request, "seq")
    seq = value === nothing ? "null" : raw(b, value)
    case = member(b, request, "case")
    case === nothing && throw(ProtocolError("a run with no case"))
    validator = try
        compile(h, b, case)
    catch err
        err isa ProtocolError && rethrow()
        return "{\"seq\":" * seq * ",\"errored\":true,\"context\":{\"message\":" * quoted(describe(err)) * "}}"
    end
    output = member(b, request, "output")
    with_annotations = output !== nothing && raw(b, output) == "\"annotations\""
    tests = member(b, case, "tests")
    results = String[]
    if tests !== nothing
        for item in items(b, tests)
            instance = member(b, item, "instance")
            push!(results, instance === nothing ? errored("a test with no instance") :
                           test(validator, raw(b, instance), with_annotations))
        end
    end
    return "{\"seq\":" * seq * ",\"results\":[" * join(results, ",") * "]}"
end

# The response to one request, or nothing for stop.
function handle(h::Harness, line::Vector{UInt8})
    request = try
        whole_value(line)
    catch err
        throw(ProtocolError(sprint(showerror, err)))
    end
    value = member(line, request, "cmd")
    cmd = value === nothing ? "" : text(line, value)
    cmd != "start" && !h.started && throw(ProtocolError("not started"))
    cmd == "start" && return start(h, line, request)
    cmd == "dialect" && return set_dialect(h, line, request)
    cmd == "run" && return run_case(h, line, request)
    cmd == "stop" && return nothing
    throw(ProtocolError("unknown command \"$cmd\""))
end

# The harness. It returns the exit code of the process.
function main(input::IO, output::IO)
    h = Harness()
    try
        for line in eachline(input)
            isempty(strip(line)) && continue
            response = handle(h, Vector{UInt8}(line))
            response === nothing && return 0
            # One line for each response, written at once.
            write(output, response, '\n')
            flush(output)
        end
    catch err
        showerror(stderr, err)
        println(stderr)
        return 1
    end
    return 0
end

# While Julia writes the package image, the harness answers a short exchange, so that the image holds its compiled
# code. The library's own image holds the evaluator.
function precompile_workload()
    requests = """
        {"cmd":"start","version":1}
        {"cmd":"dialect","dialect":"https://json-schema.org/draft/2020-12/schema"}
        {"cmd":"dialect","dialect":"urn:unknown"}
        {"cmd":"run","seq":1,"case":{"description":"d","schema":{"title":"T","type":"object","properties":{"a\\u00e9":{"\$ref":"http://localhost:1234/i.json"}}},"registry":{"http://localhost:1234/i.json":{"type":"integer"}},"tests":[{"description":"t","instance":{"a\\u00e9":1}},{"description":"u","instance":{"a\\u00e9":"x"}}]}}
        {"cmd":"run","seq":"two","output":"annotations","case":{"description":"d","schema":{"title":"T"},"tests":[{"description":"t","instance":[1,"a",null,true]}]}}
        {"cmd":"run","seq":3,"case":{"description":"d","schema":{"\$ref":"#/nowhere"},"tests":[{"description":"t","instance":1}]}}
        {"cmd":"stop"}
        """
    main(IOBuffer(requests), IOBuffer())
    precompile(main, (typeof(stdin), typeof(stdout)))
    precompile(main, (Base.PipeEndpoint, Base.PipeEndpoint))
    precompile(main, (Base.TTY, Base.TTY))
    precompile(main, (IOStream, IOStream))
    return nothing
end

if ccall(:jl_generating_output, Cint, ()) == 1
    precompile_workload()
end

end
