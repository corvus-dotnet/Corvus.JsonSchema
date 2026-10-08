# Results collection. The evaluator opens a context per subschema application and writes keyword rows into the open
# context. Closing a context either commits it (a summary row, then its own rows newest first, after its committed
# descendants) or pops it (everything it and its descendants wrote is discarded). Levels decide which rows exist and
# which carry message text. Ported from results.go.

"""
    ResultsLevel

How much a [`ResultsCollector`](@ref) records.

- `Basic` records failures only, without message text (the lowest overhead).
- `Detailed` records failures only, with message text.
- `Verbose` records every evaluation, passing and failing, with message text, including annotations.
"""
@enum ResultsLevel::Int8 begin
    Basic = 0
    Detailed = 1
    Verbose = 2
end

@doc "Record failures only, without message text. See [`ResultsLevel`](@ref)." Basic
@doc "Record failures only, with message text. See [`ResultsLevel`](@ref)." Detailed
@doc "Record every evaluation, with message text and annotations. See [`ResultsLevel`](@ref)." Verbose

"""
    SchemaResult

One result row of an evaluation.

- `is_match`: whether the keyword or subschema matched.
- `message`: the message, or `""` when the level records none or the keyword has none. Annotation rows carry raw
  JSON.
- `evaluation_location`: the path of keywords from the root schema (for example `/properties/name/type`).
- `schema_evaluation_location`: the JSON pointer of the evaluated schema (or keyword) within its document.
- `document_evaluation_location`: the JSON pointer of the instance location (for example `/name`).
"""
struct SchemaResult
    is_match::Bool
    message::String
    evaluation_location::String
    schema_evaluation_location::String
    document_evaluation_location::String
end

struct ResultsFrame
    eval_length::Int
    schema_path::String
    doc_length::Int
    commit_index::Int
    rows_start::Int
end

"""
    ResultsCollector(level::ResultsLevel=Detailed)

Collects the results of an evaluation, for [`evaluate`](@ref). A collector is used by one evaluation at a time.
[`results`](@ref) gives the rows, [`annotations`](@ref) the annotations of a `Verbose` collector, and `empty!` clears
the collector for another evaluation.
"""
mutable struct ResultsCollector
    level::ResultsLevel
    committed::Vector{SchemaResult}
    frames::Vector{ResultsFrame}
    # The rows written into open frames (each frame owns the tail from its rows_start).
    pending::Vector{SchemaResult}
    eval_path::Vector{UInt8}
    schema_path::String
    doc_path::Vector{UInt8}
end

ResultsCollector(level::ResultsLevel=Detailed) =
    ResultsCollector(level, SchemaResult[], ResultsFrame[], SchemaResult[], UInt8[], "", UInt8[])

"""
    results(collector::ResultsCollector) -> Vector{SchemaResult}

The results of the collector's evaluation, in commit order. The vector is valid until the collector is used again.
"""
results(c::ResultsCollector) = c.committed

"""
    empty!(collector::ResultsCollector)

Discard the results, so that the collector can be used for another evaluation.
"""
function Base.empty!(c::ResultsCollector)
    c.committed = SchemaResult[]
    empty!(c.frames)
    empty!(c.pending)
    empty!(c.eval_path)
    c.schema_path = ""
    empty!(c.doc_path)
    return c
end

# Reports whether a row with the given result carries its message.
with_text(c::ResultsCollector, is_match::Bool) = c.level == Verbose || (!is_match && c.level >= Detailed)

# Reports whether a keyword row with the given result is recorded at all.
records(c::ResultsCollector, is_match::Bool) = !is_match || c.level == Verbose

path_text(path::Vector{UInt8}) = String(copy(path))

# Opens a child context. The evaluation path is extended by eval_segment (verbatim), the schema path is replaced by
# schema_location, and the document path is extended by doc_segment (already pointer-encoded). has_eval and has_doc
# say whether there is a segment at all.
function begin_child_context!(c::ResultsCollector, has_eval::Bool, eval_segment::String, schema_location::String,
    has_doc::Bool, doc_segment::String)
    push!(c.frames, ResultsFrame(length(c.eval_path), c.schema_path, length(c.doc_path), length(c.committed),
        length(c.pending)))
    if has_eval
        push!(c.eval_path, UInt8('/'))
        append!(c.eval_path, codeunits(eval_segment))
    end
    c.schema_path = schema_location
    if has_doc
        push!(c.doc_path, UInt8('/'))
        append!(c.doc_path, codeunits(doc_segment))
    end
    return nothing
end

# Closes a child context. When the parent does not need the child's results (parent_is_match) they are discarded
# below Verbose. Otherwise the context's summary row is written and its rows are committed.
function commit_child_context!(c::ResultsCollector, parent_is_match::Bool, child_is_match::Bool, message::String)
    if parent_is_match && c.level != Verbose
        pop_child_context!(c)
        return nothing
    end
    text = with_text(c, child_is_match) ? message : ""
    push!(c.pending, SchemaResult(child_is_match, text, path_text(c.eval_path), c.schema_path,
        path_text(c.doc_path)))
    frame = pop!(c.frames)
    for i in length(c.pending):-1:frame.rows_start+1
        push!(c.committed, c.pending[i])
    end
    resize!(c.pending, frame.rows_start)
    restore!(c, frame)
    return nothing
end

# Closes a child context and discards everything it and its descendants wrote.
function pop_child_context!(c::ResultsCollector)
    frame = pop!(c.frames)
    resize!(c.committed, frame.commit_index)
    resize!(c.pending, frame.rows_start)
    restore!(c, frame)
    return nothing
end

row_text(c::ResultsCollector, is_match::Bool, message::String) = with_text(c, is_match) ? message : ""

# Records a keyword's result. The message is only looked at when with_text says so.
function evaluated_keyword!(c::ResultsCollector, is_match::Bool, message::String, keyword::String)
    if records(c, is_match)
        k = "/" * escape_pointer_token(keyword)
        push!(c.pending, SchemaResult(is_match, row_text(c, is_match, message), path_text(c.eval_path) * k,
            c.schema_path * k, path_text(c.doc_path)))
    end
    return nothing
end

function evaluated_keyword_for_property!(c::ResultsCollector, is_match::Bool, message::String,
    property_name::String, keyword::String)
    if records(c, is_match)
        k = "/" * escape_pointer_token(keyword)
        push!(c.pending, SchemaResult(is_match, row_text(c, is_match, message), path_text(c.eval_path) * k,
            c.schema_path * k, path_text(c.doc_path) * "/" * escape_pointer_token(property_name)))
    end
    return nothing
end

# Records an annotation: Verbose only. The keyword extends the evaluation path but not the schema path.
function ignored_keyword!(c::ResultsCollector, message::String, keyword::String)
    if c.level == Verbose
        push!(c.pending, SchemaResult(true, message, path_text(c.eval_path) * "/" * escape_pointer_token(keyword),
            c.schema_path, path_text(c.doc_path)))
    end
    return nothing
end

function evaluated_boolean_schema!(c::ResultsCollector, is_match::Bool)
    if records(c, is_match)
        push!(c.pending, SchemaResult(is_match, "", path_text(c.eval_path), c.schema_path, path_text(c.doc_path)))
    end
    return nothing
end

function restore!(c::ResultsCollector, frame::ResultsFrame)
    resize!(c.eval_path, frame.eval_length)
    c.schema_path = frame.schema_path
    resize!(c.doc_path, frame.doc_length)
    return nothing
end

"""
    Annotation

An annotation extracted from verbose results.

- `instance_location`: the instance location (a JSON pointer).
- `keyword`: the annotating keyword.
- `schema_location`: the JSON pointer of the schema object that holds the keyword.
- `value`: the annotation value as JSON text.
"""
struct Annotation
    instance_location::String
    keyword::String
    schema_location::String
    value::String
end

"""
    annotations(collector::ResultsCollector) -> Vector{Annotation}

The annotations in a `Verbose` collector's results.
"""
function annotations(c::ResultsCollector)
    out = Annotation[]
    for r in c.committed
        (!r.is_match || r.message == "") && continue
        slash = last_index_byte(r.evaluation_location, UInt8('/'))
        (slash == 0 || r.evaluation_location == r.schema_evaluation_location) && continue
        keyword = bytes_sub(r.evaluation_location, slash + 1, ncodeunits(r.evaluation_location))
        c1 = codeunit(r.message, 1)
        (keyword == "" || !(c1 in codeunits("\"{[tfn-") || is_ascii_digit(c1))) && continue
        push!(out, Annotation(r.document_evaluation_location, keyword, r.schema_evaluation_location, r.message))
    end
    return out
end

"""
    schema_location_fragment(schema_location) -> String

`"#"` followed by the schema location, percent-encoded as a URI fragment (upper-case hex, UTF-8).
"""
function schema_location_fragment(schema_location::AbstractString)
    hex = codeunits("0123456789ABCDEF")
    out = UInt8[UInt8('#')]
    for c in codeunits(String(schema_location))
        if is_ascii_alphanumeric(c) || c in codeunits("-._~!\$&'()*+,;=:@/?")
            push!(out, c)
        else
            push!(out, UInt8('%'), hex[(c>>4)+1], hex[(c&15)+1])
        end
    end
    return String(out)
end

"""
    collect_annotations(collector::ResultsCollector) -> Dict{String,Dict{String,Dict{String,String}}}

The annotations grouped by instance location, then keyword, then schema location fragment, with the values as JSON
text: `"/name" => "title" => "#/properties/name" => "\\"Name\\""`.
"""
function collect_annotations(c::ResultsCollector)
    out = Dict{String,Dict{String,Dict{String,String}}}()
    for a in annotations(c)
        by_keyword = get!(() -> Dict{String,Dict{String,String}}(), out, a.instance_location)
        by_location = get!(() -> Dict{String,String}(), by_keyword, a.keyword)
        by_location[schema_location_fragment(a.schema_location)] = a.value
    end
    return out
end
