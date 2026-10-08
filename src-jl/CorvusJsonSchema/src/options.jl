# Compilation options and the errors of compilation and evaluation. Ported from options.go.

# The options for compiling a schema. The keyword arguments of compile_schema fill it.
struct CompileOptions
    default_dialect::Dialect
    # Whether format is asserted: 1 always, -1 never, 0 as the vocabularies say.
    assert_format::Int8
    assert_format_in_legacy_drafts::Bool
    assert_content::Bool
    # Custom format assertions, by format name. Each is called with a String and returns a Bool.
    formats::Dict{String,Any}
    # Resolves a schema document by absolute URI, or nothing.
    resolver::Any
    base_uri::String
    entry_point::String
    has_entry_point::Bool
    max_depth::Int
end

function CompileOptions(; default_dialect::Dialect=Draft202012, assert_format::Union{Nothing,Bool}=nothing,
    assert_format_in_legacy_drafts::Bool=false, assert_content::Bool=true, formats=nothing, resolver=nothing,
    base_uri::AbstractString="", entry_point::Union{Nothing,AbstractString}=nothing, max_depth::Integer=128)
    custom = Dict{String,Any}()
    if formats !== nothing
        for (name, validator) in pairs(formats)
            custom[String(name)] = validator
        end
    end
    return CompileOptions(default_dialect, assert_format === nothing ? Int8(0) : (assert_format ? Int8(1) : Int8(-1)),
        assert_format_in_legacy_drafts, assert_content, custom, resolver, String(base_uri),
        entry_point === nothing ? "" : String(entry_point), entry_point !== nothing,
        max_depth >= 1 ? Int(max_depth) : 128)
end

"""
    CompileError

Thrown for a schema that could not be compiled (an unresolvable reference, an invalid pattern). `message` is the
reason.
"""
struct CompileError <: Exception
    message::String
end

Base.showerror(io::IO, e::CompileError) = print(io, "CompileError: ", e.message)

"""
    DepthExceededError

Thrown by [`validate`](@ref) and [`evaluate`](@ref) when evaluation recursed in place beyond the maximum depth (a
schema that loops without consuming the instance).
"""
struct DepthExceededError <: Exception end

Base.showerror(io::IO, ::DepthExceededError) =
    print(io, "DepthExceededError: the schema recursed in place beyond the maximum depth")
