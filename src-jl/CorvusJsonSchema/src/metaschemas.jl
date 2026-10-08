# The standard metaschemas, copied from src/Corvus.Text.Json/metaschema (a test keeps the copy current). They are
# read when the package is precompiled and held in the package image. Ported from metaschemas.go.

const METASCHEMA_DIR = joinpath(@__DIR__, "metaschemas")

const METASCHEMA_FILES = let files = Dict{String,String}()
    for (root, _, names) in walkdir(METASCHEMA_DIR)
        for name in names
            endswith(name, ".json") || continue
            path = joinpath(root, name)
            include_dependency(path)
            files[replace(relpath(path, METASCHEMA_DIR), '\\' => '/')] = read(path, String)
        end
    end
    files
end

# The embedded file of a standard metaschema, by its canonical URI (no trailing empty fragment), or nothing.
function metaschema_file(uri::String)
    uri == "http://json-schema.org/draft-04/schema" && return "draft4/schema.json"
    uri == "http://json-schema.org/draft-06/schema" && return "draft6/schema.json"
    uri == "http://json-schema.org/draft-07/schema" && return "draft7/schema.json"
    rest, ok = cut_prefix(uri, "https://json-schema.org/draft/")
    ok || return nothing
    slash = index_byte(rest, UInt8('/'))
    draft = slash > 0 ? bytes_sub(rest, 1, slash - 1) : rest
    name = slash > 0 ? bytes_sub(rest, slash + 1, ncodeunits(rest)) : ""
    (draft == "2019-09" || draft == "2020-12") || return nothing
    name == "schema" && return "draft" * draft * "/schema.json"
    vocabulary, ok = cut_prefix(name, "meta/")
    if ok && vocabulary != "" && !occursin("/", vocabulary) && !occursin(".", vocabulary)
        return "draft" * draft * "/meta/" * vocabulary * ".json"
    end
    return nothing
end

# The text of a standard metaschema, by its canonical URI, or nothing.
function metaschema(uri::String)
    file = metaschema_file(uri)
    file === nothing && return nothing
    return get(METASCHEMA_FILES, file, nothing)
end
