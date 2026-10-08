# The public entry points: compiling a schema into a validator, and validating and evaluating instances with it.
# Ported from validator.go.

"""
    Validator

A compiled schema, made by [`compile_schema`](@ref) or [`compile_schema_uri`](@ref). It is not modified by
validation and is safe to use from several tasks at once.

- `isvalid(validator, instance)` reports whether an instance is valid.
- [`validate`](@ref) does the same and throws for text that is not JSON or a schema that recursed too deep.
- [`evaluate`](@ref) evaluates exhaustively and reports to a [`ResultsCollector`](@ref).

An instance is a [`Document`](@ref), JSON text as a string, or JSON text as a vector of UTF-8 bytes. In the steady
state a validation allocates nothing.
"""
mutable struct Validator
    const program::Program
    # The evaluation state kept for the common case of one evaluation at a time, and whether it is in use.
    const primary::Evaluator
    @atomic busy::Bool
    # Evaluation states not in use, for evaluations that overlap, with what they have grown to.
    const pool::Vector{Evaluator}
    const pool_lock::Base.Threads.SpinLock
end

function Validator(compiled::CompiledSchema, options::CompileOptions)
    program = Program(compiled, options)
    return Validator(program, Evaluator(program), false, Evaluator[], Base.Threads.SpinLock())
end

Base.show(io::IO, v::Validator) = print(io, "Validator(", length(v.program.nodes), " nodes)")

"""
    compile_schema(schema; kwargs...) -> Validator

Compile a JSON Schema (draft 4, 6, 7, 2019-09 or 2020-12) given as JSON text in a string, as a vector of UTF-8
bytes, or as a parsed [`Document`](@ref). A vector of bytes is kept, not copied, so do not modify it afterwards.

Throws [`ParseError`](@ref) when the text is not JSON and [`CompileError`](@ref) when the schema cannot be compiled.

# Keyword arguments
- `default_dialect::Dialect = Draft202012`: the dialect of documents without `\$schema`.
- `assert_format::Union{Nothing,Bool} = nothing`: whether `format` is asserted. `true` always, `false` never. With
  `nothing` the vocabularies decide (the 2020-12 format-assertion vocabulary asserts, anything else annotates).
- `assert_format_in_legacy_drafts::Bool = false`: also assert `format` in drafts 4 to 7, when `assert_format` is
  `nothing`.
- `assert_content::Bool = true`: whether `contentEncoding` and `contentMediaType` are asserted in draft 7, the only
  draft that asserts them.
- `formats = nothing`: custom format assertions, as a dictionary (or pairs) from the format name to a function that
  takes the value as a `String` (a number's JSON text, for numbers) and returns a `Bool`. A custom format takes
  precedence over a built-in one of the same name.
- `resolver = nothing`: a function that resolves a schema document by absolute URI. It returns a
  [`Document`](@ref), JSON text, or `nothing` if the document is unknown. The standard metaschemas are always
  available.
- `base_uri::AbstractString = ""`: the base URI of the root document.
- `entry_point = nothing`: a reference, relative to the root, to evaluate from (for example `"#/\$defs/item"`). The
  default is the root.
- `max_depth::Integer = 128`: the maximum depth of in-place recursion on a cycle before evaluation is abandoned.
  Values below 1 are ignored.

# Examples
```julia
validator = compile_schema(\"\"\"{"type": "object", "required": ["id"]}\"\"\")
isvalid(validator, \"\"\"{"id": 3}\"\"\")  # true
```
"""
function compile_schema(schema::Document; kwargs...)
    options = CompileOptions(; kwargs...)
    return Validator(compile_document(schema, options), options)
end

compile_schema(schema::Union{AbstractString,AbstractVector{UInt8}}; kwargs...) =
    compile_schema(parse_document(schema); kwargs...)

"""
    compile_schema_uri(uri; kwargs...) -> Validator

Compile the schema document at a URI, fetched through the `resolver` given in the keyword arguments (or one of the
standard metaschemas). The keyword arguments are those of [`compile_schema`](@ref).
"""
function compile_schema_uri(uri::AbstractString; kwargs...)
    options = CompileOptions(; kwargs...)
    return Validator(compile_from_uri(String(uri), options), options)
end

# The buffers an evaluation state keeps between validations, in bytes, beyond which they are dropped.
const SCRATCH_RETAINED_LIMIT = 4 << 20

# Takes an evaluation state: the validator's own when it is free, otherwise one from the pool.
@inline function acquire(v::Validator)
    _, taken = @atomicreplace :acquire_release :monotonic v.busy false => true
    taken && return v.primary
    return acquire_pooled(v)
end

@noinline function acquire_pooled(v::Validator)
    lock(v.pool_lock)
    e = isempty(v.pool) ? nothing : pop!(v.pool)
    unlock(v.pool_lock)
    return e === nothing ? Evaluator(v.program) : e
end

# Gives an evaluation state back after the validation of a document.
@inline function release(v::Validator, e::Evaluator)
    # Let go of the instance, which the caller owns.
    e.d = EMPTY_DOCUMENT
    if 8 * (e.arena_high + length(e.unique)) > SCRATCH_RETAINED_LIMIT
        drop_buffers!(e)
    end
    if e === v.primary
        @atomic :release v.busy = false
    else
        release_pooled(v, e)
    end
    return nothing
end

# Gives an evaluation state back after the validation of JSON text, which was parsed into its buffers.
function release_text(v::Validator, e::Evaluator)
    e.text.source = EMPTY_BYTES
    words = length(e.text.tape) + e.arena_high + length(e.unique)
    if retains_too_much(e.parser) || 8 * words + length(e.text.text) + length(e.source) > SCRATCH_RETAINED_LIMIT
        drop_buffers!(e)
    end
    release(v, e)
    return nothing
end

@noinline function drop_buffers!(e::Evaluator)
    e.text = Document()
    e.parser = Parser()
    e.source = UInt8[]
    e.arena = UInt64[]
    e.unique = UInt64[]
    e.arena_high = 0
    return nothing
end

@noinline function release_pooled(v::Validator, e::Evaluator)
    lock(v.pool_lock)
    length(v.pool) < 256 && push!(v.pool, e)
    unlock(v.pool_lock)
    return nothing
end

# Prepares an evaluation state for an instance. A validation leaves the scope, the arena and the passes as it found
# them, unless an exception ended it (a custom format may throw), so they are checked and not cleared.
@inline function start!(e::Evaluator, instance::Document)
    e.d = instance
    e.depth = 0
    e.depth_exceeded = false
    e.pass_depth = 0
    isempty(e.scope) || empty!(e.scope)
    isempty(e.arena) || empty!(e.arena)
    return nothing
end

# Validates a document, failing fast. It reports the result and whether evaluation recursed in place beyond the
# maximum depth.
function run_validation(v::Validator, instance::Document)
    e = acquire(v)
    start!(e, instance)
    v.program.may_throw && return run_guarded(v, e, false)
    ok = validate!(e)
    exceeded = e.depth_exceeded
    release(v, e)
    return ok, exceeded
end

# run_validation for a program whose evaluation may throw: the evaluation state is given back all the same.
@noinline function run_guarded(v::Validator, e::Evaluator, text::Bool)
    try
        ok = validate!(e)
        return ok, e.depth_exceeded
    finally
        text ? release_text(v, e) : release(v, e)
    end
end

# Parses JSON text into the buffers of an evaluation state. It reports whether the text was JSON.
@inline parse_text!(e::Evaluator, json::Vector{UInt8}) = parse_into!(e.parser, e.text, json)

function parse_text!(e::Evaluator, json::String)
    n = ncodeunits(json)
    source = e.source
    resize!(source, n)
    copyto!(source, 1, codeunits(json), 1, n)
    return parse_into!(e.parser, e.text, source)
end

function parse_text!(e::Evaluator, json::AbstractVector{UInt8})
    source = e.source
    resize!(source, length(json))
    copyto!(source, json)
    return parse_into!(e.parser, e.text, source)
end

parse_text!(e::Evaluator, json::AbstractString) = parse_text!(e, String(json))

const JsonText = Union{AbstractString,AbstractVector{UInt8}}

function run_text_validation(v::Validator, json::JsonText)
    e = acquire(v)
    if !parse_text!(e, json)
        message, offset = e.parser.err_message, e.parser.err_offset
        release_text(v, e)
        return false, false, message, offset
    end
    start!(e, e.text)
    if v.program.may_throw
        ok, exceeded = run_guarded(v, e, true)
        return ok, exceeded, "", 0
    end
    ok = validate!(e)
    exceeded = e.depth_exceeded
    release_text(v, e)
    return ok, exceeded, "", 0
end

"""
    isvalid(validator::Validator, instance) -> Bool

Report whether an instance is valid. The instance is a [`Document`](@ref), JSON text as a string, or JSON text as a
vector of UTF-8 bytes. Text that is not JSON is not valid. A schema that recursed in place beyond the maximum depth
is reported as invalid. Use [`validate`](@ref) to tell these apart.

JSON text is parsed into buffers the validator reuses, so a validation allocates nothing in the steady state.
"""
function Base.isvalid(v::Validator, instance::Document)
    ok, _ = run_validation(v, instance)
    return ok
end

function Base.isvalid(v::Validator, json::JsonText)
    ok, _, _, _ = run_text_validation(v, json)
    return ok
end

"""
    validate(validator::Validator, instance) -> Bool

Report whether an instance is valid. The instance is a [`Document`](@ref), JSON text as a string, or JSON text as a
vector of UTF-8 bytes.

Throws [`ParseError`](@ref) when the text is not JSON, and [`DepthExceededError`](@ref) when evaluation recursed in
place beyond the maximum depth.
"""
function validate(v::Validator, instance::Document)
    ok, exceeded = run_validation(v, instance)
    exceeded && throw(DepthExceededError())
    return ok
end

function validate(v::Validator, json::JsonText)
    ok, exceeded, message, offset = run_text_validation(v, json)
    message == "" || throw(ParseError(message, offset))
    exceeded && throw(DepthExceededError())
    return ok
end

"""
    evaluate(validator::Validator, instance, collector::ResultsCollector) -> Bool
    evaluate(validator::Validator, instance) -> Bool

Evaluate an instance exhaustively, reporting to the collector, and report whether the instance is valid. The
instance is a [`Document`](@ref), JSON text as a string, or JSON text as a vector of UTF-8 bytes. Without a collector
it is [`validate`](@ref).

Throws [`ParseError`](@ref) when the text is not JSON, and [`DepthExceededError`](@ref) when evaluation recursed in
place beyond the maximum depth.
"""
function evaluate(v::Validator, instance::Document, collector::ResultsCollector)
    e = acquire(v)
    start!(e, instance)
    e.c = collector
    ok = try
        evaluate!(e, collector)
    finally
        e.c = nothing
        release(v, e)
    end
    e.depth_exceeded && throw(DepthExceededError())
    return ok
end

evaluate(v::Validator, json::JsonText, collector::ResultsCollector) = evaluate(v, parse_document(json), collector)
evaluate(v::Validator, instance) = validate(v, instance)
evaluate(v::Validator, instance, ::Nothing) = validate(v, instance)
