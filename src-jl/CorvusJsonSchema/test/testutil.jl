# Helpers shared by the tests. Test data is read with the package's own parser, since the package has no dependency
# on a JSON library.

const C = CorvusJsonSchema

# The value of a property of an object value, or -1.
member(d::Document, n::Int, name::String) = C.member(d, n, name)

# The text of a string value.
text_of(d::Document, n::Int) = String(C.str(d, n))

# A value as JSON text.
json_of(d::Document, n::Int) = String(C.append_json!(UInt8[], d, n))

# The items of an array value.
items_of(d::Document, n::Int) = [C.first(d, n) + i for i in 0:C.count(d, n)-1]

# The names and values of an object value.
members_of(d::Document, n::Int) =
    [(text_of(d, C.first(d, n) + 2i), C.first(d, n) + 2i + 1) for i in 0:C.count(d, n)-1]

is_true(d::Document, n::Int) = C.kind(d, n) == C.KIND_BOOL && C.boolean(d, n)

const REPOSITORY_ROOT = normpath(joinpath(@__DIR__, "..", "..", ".."))

# The JSON-Schema-Test-Suite checkout: the repository's submodule, or JSON_SCHEMA_TEST_SUITE.
suite_root() = get(ENV, "JSON_SCHEMA_TEST_SUITE", joinpath(REPOSITORY_ROOT, "JSON-Schema-Test-Suite"))

json_files(dir::String) = isdir(dir) ? sort!([joinpath(dir, f) for f in readdir(dir) if endswith(f, ".json") &&
                                              isfile(joinpath(dir, f))]) : String[]

# A small deterministic generator for the tests that compare against a reference.
mutable struct Xorshift
    x::UInt64
end

function next!(g::Xorshift, n::Int)
    x = g.x
    x ⊻= x << 13
    x ⊻= x >> 7
    x ⊻= x << 17
    g.x = x
    return Int(x % UInt64(n))
end

# Reports whether the expression throws an exception of the type.
function throws(f, ::Type{T}) where {T}
    try
        f()
    catch err
        return err isa T
    end
    return false
end

quote_json(text::String) = String(C.append_quoted!(UInt8[], C.Bytes(Vector{UInt8}(text))))

# Checks that fail-fast evaluation (through the plans) and collecting evaluation (through the general evaluator)
# give the same answer for each instance. It returns the instances on which they differ.
function disagreements(v::Validator, instances)
    out = String[]
    for instance in instances
        collected = evaluate(v, instance, ResultsCollector(Basic))
        isvalid(v, instance) == collected || push!(out, instance)
    end
    return out
end

# Reports whether a validator gives the expected answer for an instance as text and as a document.
expect_valid(v::Validator, instance::String, want::Bool) =
    isvalid(v, instance) == want && isvalid(v, parse_document(instance)) == want

# The plan of the root node of a validator's program.
root_body(v::Validator) = C.plan(v.program, v.program.root).body

# Resolves the suite's remotes (http://localhost:1234/...) from its remotes directory.
function remote_resolver(root::String)
    remotes = joinpath(root, "remotes")
    cache = Dict{String,Union{Nothing,Document}}()
    guard = ReentrantLock()
    return function (uri::String)
        prefix = "http://localhost:1234/"
        startswith(uri, prefix) || return nothing
        rest = uri[ncodeunits(prefix)+1:end]
        return lock(guard) do
            get!(cache, rest) do
                path = joinpath(remotes, split(rest, '/')...)
                isfile(path) || return nothing
                try
                    parse_document(read(path))
                catch
                    nothing
                end
            end
        end
    end
end
