# A reader of JSON text that keeps each value as the text it arrived as. The harness hands schemas and instances to
# the library as that text, so nothing is parsed twice or written again in another form. Julia has no JSON reader in
# its standard library, and the library's own reader is not part of its API.
#
# A value is a range of the bytes of the text. Nothing checks that a value is well formed beyond what finding its end
# needs: the library parses what it is given.

struct NotJson <: Exception
    at::Int
end

Base.showerror(io::IO, e::NotJson) = print(io, "not JSON at byte ", e.at)

is_space(b::UInt8) = b == 0x20 || b == 0x09 || b == 0x0a || b == 0x0d

function skip_space(b::Vector{UInt8}, i::Int)
    while i <= length(b) && is_space(b[i])
        i += 1
    end
    return i
end

# The index after the string that starts at i.
function string_end(b::Vector{UInt8}, i::Int)
    j = i + 1
    while j <= length(b)
        if b[j] == UInt8('\\')
            j += 2
        elseif b[j] == UInt8('"')
            return j + 1
        else
            j += 1
        end
    end
    throw(NotJson(i))
end

# The index after the value that starts at i.
function value_end(b::Vector{UInt8}, i::Int)
    i <= length(b) || throw(NotJson(i))
    c = b[i]
    c == UInt8('"') && return string_end(b, i)
    if c == UInt8('{') || c == UInt8('[')
        depth = 0
        j = i
        while j <= length(b)
            c = b[j]
            if c == UInt8('"')
                j = string_end(b, j)
                continue
            elseif c == UInt8('{') || c == UInt8('[')
                depth += 1
            elseif c == UInt8('}') || c == UInt8(']')
                depth -= 1
                depth == 0 && return j + 1
            end
            j += 1
        end
        throw(NotJson(i))
    end
    # A number, true, false or null.
    j = i
    while j <= length(b) && !is_space(b[j]) && !(b[j] in (UInt8(','), UInt8('}'), UInt8(']')))
        j += 1
    end
    j > i || throw(NotJson(i))
    return j
end

# The value in the text, as its range.
function whole_value(b::Vector{UInt8})
    i = skip_space(b, 1)
    j = value_end(b, i)
    skip_space(b, j) > length(b) || throw(NotJson(j))
    return i:j-1
end

# The members of the object at the range: each name, and the range of its value.
function members(b::Vector{UInt8}, r::UnitRange{Int})
    out = Pair{String,UnitRange{Int}}[]
    (!isempty(r) && b[first(r)] == UInt8('{')) || throw(NotJson(first(r)))
    i = skip_space(b, first(r) + 1)
    while b[i] != UInt8('}')
        b[i] == UInt8('"') || throw(NotJson(i))
        j = string_end(b, i)
        name = text(b, i:j-1)
        i = skip_space(b, j)
        b[i] == UInt8(':') || throw(NotJson(i))
        i = skip_space(b, i + 1)
        j = value_end(b, i)
        push!(out, name => (i:j-1))
        i = skip_space(b, j)
        if b[i] == UInt8(',')
            i = skip_space(b, i + 1)
        elseif b[i] != UInt8('}')
            throw(NotJson(i))
        end
    end
    return out
end

# The value of a member of the object at the range, or nothing.
function member(b::Vector{UInt8}, r::UnitRange{Int}, name::String)
    for (key, value) in members(b, r)
        key == name && return value
    end
    return nothing
end

# The items of the array at the range.
function items(b::Vector{UInt8}, r::UnitRange{Int})
    out = UnitRange{Int}[]
    (!isempty(r) && b[first(r)] == UInt8('[')) || throw(NotJson(first(r)))
    i = skip_space(b, first(r) + 1)
    while b[i] != UInt8(']')
        j = value_end(b, i)
        push!(out, i:j-1)
        i = skip_space(b, j)
        if b[i] == UInt8(',')
            i = skip_space(b, i + 1)
        elseif b[i] != UInt8(']')
            throw(NotJson(i))
        end
    end
    return out
end

function hex4(b::Vector{UInt8}, i::Int)
    i + 3 <= length(b) || throw(NotJson(i))
    value = tryparse(UInt16, String(b[i:i+3]); base=16)
    value === nothing && throw(NotJson(i))
    return value
end

# The text the string at the range stands for.
function text(b::Vector{UInt8}, r::UnitRange{Int})
    (length(r) >= 2 && b[first(r)] == UInt8('"')) || throw(NotJson(first(r)))
    out = IOBuffer()
    i = first(r) + 1
    stop = last(r) - 1
    while i <= stop
        c = b[i]
        if c != UInt8('\\')
            write(out, c)
            i += 1
            continue
        end
        e = b[i+1]
        i += 2
        if e == UInt8('u')
            unit = UInt32(hex4(b, i))
            i += 4
            # A surrogate pair is one character.
            if 0xd800 <= unit <= 0xdbff && i + 5 <= stop && b[i] == UInt8('\\') && b[i+1] == UInt8('u')
                low = UInt32(hex4(b, i + 2))
                if 0xdc00 <= low <= 0xdfff
                    unit = 0x10000 + ((unit - 0xd800) << 10) + (low - 0xdc00)
                    i += 6
                end
            end
            print(out, Char(unit))
        else
            write(out, e == UInt8('n') ? 0x0a : e == UInt8('t') ? 0x09 : e == UInt8('r') ? 0x0d :
                       e == UInt8('b') ? 0x08 : e == UInt8('f') ? 0x0c : e)
        end
    end
    return String(take!(out))
end

# The value at the range as text of its own.
raw(b::Vector{UInt8}, r::UnitRange{Int}) = String(b[r])

# The value at the range on one line: the white space between its tokens is left out.
function compact(b::Vector{UInt8}, r::UnitRange{Int})
    out = UInt8[]
    i = first(r)
    while i <= last(r)
        c = b[i]
        if c == UInt8('"')
            j = string_end(b, i)
            append!(out, view(b, i:j-1))
            i = j
        else
            is_space(c) || push!(out, c)
            i += 1
        end
    end
    return String(out)
end

const HEX = "0123456789abcdef"

# A string as JSON text.
function quoted(s::AbstractString)
    out = IOBuffer()
    write(out, '"')
    for c in codeunits(String(s))
        if c == UInt8('"') || c == UInt8('\\')
            write(out, '\\', c)
        elseif c < 0x20
            write(out, "\\u00", HEX[(c>>4)+1], HEX[(c&15)+1])
        else
            write(out, c)
        end
    end
    write(out, '"')
    return String(take!(out))
end
