# URI handling for schema identification and reference resolution (RFC 3986 section 5). Reference resolution is
# implemented directly so that opaque bases (urn:, tag:) resolve the same way in every port of the evaluator. Ported
# from uri.go.

struct UriParts
    scheme::String
    authority::String
    path::String
    query::String
    has_scheme::Bool
    has_authority::Bool
    has_query::Bool
end

is_ascii_letter(b::UInt8) = UInt8('a') <= (b | 0x20) <= UInt8('z')
is_ascii_digit(b::UInt8) = UInt8('0') <= b <= UInt8('9')

# The one-based index of the ':' ending a URI scheme, or 0 if the text does not start with one.
function scheme_end(s::String)
    n = ncodeunits(s)
    (n == 0 || !is_ascii_letter(codeunit(s, 1))) && return 0
    for i in 2:n
        b = codeunit(s, i)
        b == UInt8(':') && return i
        if !(is_ascii_letter(b) || is_ascii_digit(b) || b == UInt8('+') || b == UInt8('-') || b == UInt8('.'))
            return 0
        end
    end
    return 0
end

# The byte index of the first of the given bytes in s, or 0.
function index_byte(s::String, c::UInt8, from::Int=1)
    for i in from:ncodeunits(s)
        codeunit(s, i) == c && return i
    end
    return 0
end

function last_index_byte(s::String, c::UInt8)
    for i in ncodeunits(s):-1:1
        codeunit(s, i) == c && return i
    end
    return 0
end

# Text by byte positions (one-based, inclusive), which are always at character boundaries here.
bytes_sub(s::String, from::Int, to::Int) = from > to ? "" : String(codeunits(s)[from:to])

# Splits a URI into scheme, authority, path and query (the fragment must already be removed).
function parse_uri(uri::String)
    scheme, authority, query = "", "", ""
    has_scheme, has_authority, has_query = false, false, false
    rest = uri
    colon = scheme_end(rest)
    if colon > 0
        scheme, has_scheme = bytes_sub(rest, 1, colon - 1), true
        rest = bytes_sub(rest, colon + 1, ncodeunits(rest))
    end
    if startswith(rest, "//")
        after = bytes_sub(rest, 3, ncodeunits(rest))
        stop = ncodeunits(after) + 1
        for i in 1:ncodeunits(after)
            b = codeunit(after, i)
            if b == UInt8('/') || b == UInt8('?')
                stop = i
                break
            end
        end
        authority, has_authority = bytes_sub(after, 1, stop - 1), true
        rest = bytes_sub(after, stop, ncodeunits(after))
    end
    q = index_byte(rest, UInt8('?'))
    path = rest
    if q > 0
        path, query, has_query = bytes_sub(rest, 1, q - 1), bytes_sub(rest, q + 1, ncodeunits(rest)), true
    end
    return UriParts(scheme, authority, path, query, has_scheme, has_authority, has_query)
end

# s with the letters A to Z in lower case and everything else as it is. RFC 3986 and RFC 3987 make a scheme and a
# host case-insensitive for those letters only, and the mapping does not depend on the Unicode data of the Julia
# version as lowercase does.
function ascii_lower(s::String)
    any(b -> UInt8('A') <= b <= UInt8('Z'), codeunits(s)) || return s
    out = Vector{UInt8}(codeunits(s))
    for i in eachindex(out)
        if UInt8('A') <= out[i] <= UInt8('Z')
            out[i] += 0x20
        end
    end
    return String(out)
end

# Reports whether the reference starts with a URI scheme.
has_scheme(reference::String) = scheme_end(reference) > 0

# Splits a reference at its first '#'.
function split_fragment(reference::String)
    i = index_byte(reference, UInt8('#'))
    i > 0 || return reference, ""
    return bytes_sub(reference, 1, i - 1), bytes_sub(reference, i + 1, ncodeunits(reference))
end

function uri_string(p::UriParts)
    io = IOBuffer()
    if p.has_scheme
        print(io, p.scheme, ':')
    end
    if p.has_authority
        print(io, "//", p.authority)
    end
    print(io, p.path)
    if p.has_query
        print(io, '?', p.query)
    end
    return String(take!(io))
end

function remove_dot_segments(path::String)
    occursin(".", path) || return path
    input = String.(split(path, '/'))
    out = String[]
    last = length(input)
    for (i, seg) in enumerate(input)
        if seg == "."
            i == last && push!(out, "")
        elseif seg == ".."
            if length(out) > 1 || (length(out) == 1 && out[1] != "")
                pop!(out)
            end
            i == last && push!(out, "")
        else
            push!(out, seg)
        end
    end
    return join(out, "/")
end

function merge_paths(base::UriParts, ref_path::String)
    if base.has_authority && base.path == ""
        return "/" * ref_path
    end
    i = last_index_byte(base.path, UInt8('/'))
    return i > 0 ? bytes_sub(base.path, 1, i) * ref_path : ref_path
end

function trim_suffix(s::String, suffix::String)
    return endswith(s, suffix) ? bytes_sub(s, 1, ncodeunits(s) - ncodeunits(suffix)) : s
end

function normalize_parts(p::UriParts)
    scheme = p.has_scheme ? ascii_lower(p.scheme) : p.scheme
    path = remove_dot_segments(p.path)
    authority = p.authority
    if p.has_authority
        authority = ascii_lower(authority)
        if scheme == "http"
            authority = trim_suffix(authority, ":80")
        elseif scheme == "https"
            authority = trim_suffix(authority, ":443")
        end
        if path == ""
            path = "/"
        end
    end
    return uri_string(UriParts(scheme, authority, path, p.query, p.has_scheme, p.has_authority, p.has_query))
end

# Normalises an absolute URI (dropping its fragment) so that equivalent spellings compare equal.
function normalize_uri(uri::String)
    u, _ = split_fragment(uri)
    has_scheme(u) || return u
    return normalize_parts(parse_uri(u))
end

# Resolves a reference (without fragment) against a base URI, returning the normalised absolute URI.
function resolve_uri(base_uri::String, reference::String)
    reference == "" && return base_uri
    r = parse_uri(reference)
    r.has_scheme && return normalize_parts(r)
    base_uri == "" && return reference
    b = parse_uri(base_uri)
    authority, has_authority, path, query, has_query = "", false, "", "", false
    if r.has_authority
        authority, has_authority = r.authority, true
        path = remove_dot_segments(r.path)
        query, has_query = r.query, r.has_query
    else
        if r.path == ""
            path = b.path
            if r.has_query
                query, has_query = r.query, true
            else
                query, has_query = b.query, b.has_query
            end
        else
            if startswith(r.path, "/")
                path = remove_dot_segments(r.path)
            else
                path = remove_dot_segments(merge_paths(b, r.path))
            end
            query, has_query = r.query, r.has_query
        end
        authority, has_authority = b.authority, b.has_authority
    end
    t = UriParts(b.scheme, authority, path, query, b.has_scheme, has_authority, has_query)
    return b.has_scheme ? normalize_parts(t) : uri_string(t)
end

function hex_value(c::UInt8)
    UInt8('0') <= c <= UInt8('9') && return Int(c - UInt8('0'))
    UInt8('a') <= c <= UInt8('f') && return Int(c - UInt8('a')) + 10
    UInt8('A') <= c <= UInt8('F') && return Int(c - UInt8('A')) + 10
    return -1
end

# Percent-decodes a fragment (invalid escapes leave the text unchanged).
function decode_fragment(fragment::String)
    occursin("%", fragment) || return fragment
    out = UInt8[]
    n = ncodeunits(fragment)
    i = 1
    while i <= n
        c = codeunit(fragment, i)
        if c == UInt8('%')
            i + 2 > n && return fragment
            h, l = hex_value(codeunit(fragment, i + 1)), hex_value(codeunit(fragment, i + 2))
            (h < 0 || l < 0) && return fragment
            push!(out, (h * 16 + l) % UInt8)
            i += 3
            continue
        end
        push!(out, c)
        i += 1
    end
    utf8_valid(out, 0, length(out)) || return fragment
    return String(out)
end

# Escapes a JSON pointer token (~ as ~0, / as ~1).
function escape_pointer_token(token::String)
    (occursin("~", token) || occursin("/", token)) || return token
    return replace(replace(token, "~" => "~0"), "/" => "~1")
end

# Resolves an RFC 6901 JSON pointer against a value of a document, returning the value and its normalised pointer,
# or -1.
function resolve_pointer(d::Document, root::Int, pointer::String)
    pointer == "" && return root, ""
    codeunit(pointer, 1) != UInt8('/') && return -1, ""
    current = root
    path = IOBuffer()
    for raw in split(bytes_sub(pointer, 2, ncodeunits(pointer)), '/')
        token = replace(replace(String(raw), "~1" => "/"), "~0" => "~")
        k = kind(d, current)
        if k == KIND_ARRAY
            valid = token == "0" ||
                    (token != "" && codeunit(token, 1) != UInt8('0') && all(is_ascii_digit, codeunits(token)))
            valid || return -1, ""
            (ncodeunits(token) > 18) && return -1, ""
            i = parse(Int, token)
            i >= count(d, current) && return -1, ""
            current = first(d, current) + i
            print(path, '/', token)
        elseif k == KIND_OBJECT
            current = property(d, current, token)
            current < 0 && return -1, ""
            print(path, '/', escape_pointer_token(token))
        else
            return -1, ""
        end
    end
    return current, String(take!(path))
end
