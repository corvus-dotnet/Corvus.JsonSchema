# JSON text parsed into a flat tape. Ported from the Go module's document.go.
#
# Indexes in this file are zero-based offsets, as in the Go source, and one is added where a vector is read. A value
# is the index of its node, and -1 is "no value".

"""
    MAX_DEPTH

The deepest nesting of arrays and objects a document may have.
"""
const MAX_DEPTH = 1000

# The kinds of a value. They are the evaluator's type bits, so a type test is one mask operation.
const KIND_NULL = 0x01
const KIND_BOOL = 0x02
const KIND_OBJECT = 0x04
const KIND_ARRAY = 0x08
const KIND_NUMBER = 0x10
const KIND_STRING = 0x20

# Number representations (the flags of a number). An integer that fits an Int64, an integer in [2^63, 2^64) held as
# a UInt64, and anything else, held as the bits of a Float64.
const NUM_INT = 0x00
const NUM_UINT = 0x01
const NUM_FLOAT = 0x02

# String flags. STR_TEXT says the string's bytes are in Document.text (it had escapes), not in Document.source.
# STR_WIDE says the string has bytes outside ASCII.
const STR_TEXT = 0x01
const STR_WIDE = 0x02

"""
    Bytes

A run of bytes inside a vector, without a copy. It is what a string value of a document is read as.
"""
struct Bytes <: AbstractVector{UInt8}
    b::Vector{UInt8}
    off::Int
    len::Int
end

Base.size(s::Bytes) = (s.len,)
Base.IndexStyle(::Type{Bytes}) = IndexLinear()
function Base.getindex(s::Bytes, i::Int)
    (1 <= i <= s.len) || throw(BoundsError(s, i))
    return s.b[s.off+i]
end

Bytes(b::Vector{UInt8}) = Bytes(b, 0, length(b))

# The byte at zero-based index i.
@inline at(s::Bytes, i::Int) = s.b[s.off+i+1]
@inline sub(s::Bytes, from::Int, to::Int) = Bytes(s.b, s.off + from, to - from)
@inline sub(s::Bytes, from::Int) = Bytes(s.b, s.off + from, s.len - from)

const EMPTY_BYTES = UInt8[]

# Throws for a read at a negative offset. It is kept out of line so that the readers stay small.
@noinline throw_negative_offset(b::Vector{UInt8}, i::Int) = throw(BoundsError(b, i + 1))

# Eight bytes at zero-based offset i as a little-endian word. Every byte is read with a checked index. The offset's
# sign is tested first and the last byte is read first, so the compiler can prove the other seven reads are in range
# from that one check, drop their checks and join the eight reads into one load (see OPTIMIZATIONS.md, "Bounds checks
# the compiler removes").
@inline function le64(b::Vector{UInt8}, i::Int)
    i >= 0 || throw_negative_offset(b, i)
    high = UInt64(b[i+8]) << 56
    return high | UInt64(b[i+7]) << 48 | UInt64(b[i+6]) << 40 | UInt64(b[i+5]) << 32 | UInt64(b[i+4]) << 24 |
           UInt64(b[i+3]) << 16 | UInt64(b[i+2]) << 8 | UInt64(b[i+1])
end

# Four bytes at zero-based offset i as a little-endian word, read as le64 reads eight.
@inline function le32(b::Vector{UInt8}, i::Int)
    i >= 0 || throw_negative_offset(b, i)
    high = UInt64(b[i+4]) << 24
    return high | UInt64(b[i+3]) << 16 | UInt64(b[i+2]) << 8 | UInt64(b[i+1])
end

# Eight bytes at zero-based offset i + k as a little-endian word, for a k that is a constant where this is inlined.
# The sign of i is tested and the last byte is read first, as in le64. A word at an offset the caller computed (the
# last eight bytes of a string, say) does not get its reads joined, since the compiler folds the caller's arithmetic
# into the indexes and no longer sees one base with a known sign. A word at a constant distance from a base does.
@inline function le64(b::Vector{UInt8}, i::Int, k::Int)
    i >= 0 || throw_negative_offset(b, i)
    high = UInt64(b[i+k+8]) << 56
    return high | UInt64(b[i+k+7]) << 48 | UInt64(b[i+k+6]) << 40 | UInt64(b[i+k+5]) << 32 |
           UInt64(b[i+k+4]) << 24 | UInt64(b[i+k+3]) << 16 | UInt64(b[i+k+2]) << 8 | UInt64(b[i+k+1])
end

# The low n bytes of a word, for n from 0 to 7.
@inline low_bytes(w::UInt64, n::Int) = w & ~(typemax(UInt64) << ((8 * n) & 63))

# The first min(n, 8) of the n bytes at zero-based offset off as a little-endian word, the rest of the word zero.
# Where eight bytes can be read at off it is one load and a mask: a string shorter than eight bytes inside a document
# has the document's text after it, which is read (with its index checked, as every read) and masked away. A string
# within eight bytes of the end of its vector is read byte by byte.
@inline function word_at(b::Vector{UInt8}, off::Int, n::Int)
    if off >= 0 && (off + 7) % UInt < length(b) % UInt
        w = le64(b, off)
        return n >= 8 ? w : low_bytes(w, n)
    end
    return word_at_end(b, off, n)
end

@noinline function word_at_end(b::Vector{UInt8}, off::Int, n::Int)
    w = UInt64(0)
    for i in 0:min(n, 8)-1
        w |= UInt64(b[off+i+1]) << (8 * i)
    end
    return w
end

# word_at for the bytes from off + 8 on, of which n belong to the string: its second word.
@inline function second_word_at(b::Vector{UInt8}, off::Int, n::Int)
    if off >= 0 && (off + 15) % UInt < length(b) % UInt
        w = le64(b, off, 8)
        return n >= 8 ? w : low_bytes(w, n)
    end
    return word_at_end(b, off + 8, n)
end

function bytes_equal(a::Bytes, b::Bytes)
    n = a.len
    n == b.len || return false
    x, y, p, q = a.b, b.b, a.off, b.off
    i = 0
    while i + 8 <= n
        le64(x, p + i) == le64(y, q + i) || return false
        i += 8
    end
    while i < n
        x[p+i+1] == y[q+i+1] || return false
        i += 1
    end
    return true
end

function bytes_equal(a::Bytes, s::Vector{UInt8})
    return bytes_equal(a, Bytes(s, 0, length(s)))
end

function bytes_equal(a::Bytes, s::String)
    n = ncodeunits(s)
    n == a.len || return false
    for i in 1:n
        codeunit(s, i) == a.b[a.off+i] || return false
    end
    return true
end

# Lexicographic comparison of two byte runs.
function bytes_cmp(a::Bytes, b::Bytes)
    n = min(a.len, b.len)
    for i in 0:n-1
        x, y = at(a, i), at(b, i)
        x == y || return x < y ? -1 : 1
    end
    return cmp(a.len, b.len)
end

Base.String(s::Bytes) = String(s.b[s.off+1:s.off+s.len])

"""
    Document

JSON text parsed for evaluation. It holds the UTF-8 text and one flat vector of values (a tape) that the evaluator
reads in place, with no object per value. Strings stay in the text where they have no escapes.

A `Document` made by [`parse_document`](@ref) is not modified afterwards and is safe to read from several tasks.
Parsing is strict RFC 8259. Anything but whitespace after the value, invalid UTF-8, lone surrogates in `\\u` escapes,
numbers out of the range of a `Float64` and nesting deeper than [`MAX_DEPTH`](@ref) are errors. Of duplicate property
names, the last value is kept, at the position of the first.

`String(document)` gives the document as compact JSON text, with numbers as they were written.
"""
mutable struct Document
    # Two words per value. The first is the header: the kind in bits 0 to 7, flags in bits 8 to 15 and a 32-bit
    # field in the high half (a string's byte length, a number's offset in the source, a container's count). The
    # second is the data: a string's offset, a number's bits, a container's first child, a boolean's 0 or 1. The
    # children of a container are consecutive. An object's children are key and value pairs.
    tape::Vector{UInt64}
    source::Vector{UInt8}
    # The unescaped strings.
    text::Vector{UInt8}
    root::Int
end

Document() = Document(UInt64[], EMPTY_BYTES, UInt8[], 0)

"""
    ParseError

Thrown for text that is not valid JSON. `message` says what was wrong and `offset` is the zero-based byte offset at
which the error was found.
"""
struct ParseError <: Exception
    message::String
    offset::Int
end

function Base.showerror(io::IO, e::ParseError)
    print(io, "invalid JSON at offset ", e.offset, ": ", e.message)
end

# ----------------------------------------------------------------------------------------------------------------------
# Reading values (the evaluator's side). A value is the index of its node.

@inline header(d::Document, n::Int) = d.tape[(n<<1)+1]
@inline data(d::Document, n::Int) = d.tape[(n<<1)+2]
@inline kind(d::Document, n::Int) = d.tape[(n<<1)+1] % UInt8
@inline flags(d::Document, n::Int) = (d.tape[(n<<1)+1] >> 8) % UInt8
# A container's item or property count, or a string's byte length.
@inline count(d::Document, n::Int) = (d.tape[(n<<1)+1] >> 32) % Int
# A container's first child.
@inline first(d::Document, n::Int) = d.tape[(n<<1)+2] % Int
@inline boolean(d::Document, n::Int) = d.tape[(n<<1)+2] != 0

@inline hkind(h::UInt64) = h % UInt8
@inline hflags(h::UInt64) = (h >> 8) % UInt8
@inline hcount(h::UInt64) = (h >> 32) % Int

# The bytes of a string value.
@inline function str(d::Document, n::Int)
    h = d.tape[(n<<1)+1]
    off = d.tape[(n<<1)+2] % Int
    return Bytes((h & (UInt64(STR_TEXT) << 8)) != 0 ? d.text : d.source, off, (h >> 32) % Int)
end

@inline str_ascii(d::Document, n::Int) = (d.tape[(n<<1)+1] & (UInt64(STR_WIDE) << 8)) == 0

# The value of an object's property, or -1.
function property(d::Document, object::Int, name::String)
    k = first(d, object)
    for _ in 1:count(d, object)
        if count(d, k) == ncodeunits(name) && bytes_equal(str(d, k), name)
            return k + 1
        end
        k += 2
    end
    return -1
end

# A number's value as a Float64.
function float(d::Document, n::Int)
    v = data(d, n)
    f = flags(d, n)
    if f == NUM_INT
        return Float64(v % Int64)
    elseif f == NUM_UINT
        return Float64(v)
    end
    return reinterpret(Float64, v)
end

# The end of the number whose text starts at j (already validated).
function number_end(b::Vector{UInt8}, j::Int)
    n = length(b)
    while j < n
        c = b[j+1]
        if (UInt8('0') <= c <= UInt8('9')) || c == UInt8('-') || c == UInt8('+') || c == UInt8('.') ||
           c == UInt8('e') || c == UInt8('E')
            j += 1
        else
            break
        end
    end
    return j
end

# A number's text as written.
function number_text(d::Document, n::Int)
    start = count(d, n)
    return Bytes(d.source, start, number_end(d.source, start) - start)
end

function append_json!(out::Vector{UInt8}, d::Document, n::Int)
    k = kind(d, n)
    if k == KIND_NULL
        append!(out, codeunits("null"))
    elseif k == KIND_BOOL
        append!(out, codeunits(boolean(d, n) ? "true" : "false"))
    elseif k == KIND_NUMBER
        append!(out, number_text(d, n))
    elseif k == KIND_STRING
        append_quoted!(out, str(d, n))
    elseif k == KIND_ARRAY
        push!(out, UInt8('['))
        c = first(d, n)
        for i in 0:count(d, n)-1
            i > 0 && push!(out, UInt8(','))
            append_json!(out, d, c + i)
        end
        push!(out, UInt8(']'))
    else
        push!(out, UInt8('{'))
        c = first(d, n)
        for i in 0:count(d, n)-1
            i > 0 && push!(out, UInt8(','))
            append_quoted!(out, str(d, c + 2i))
            push!(out, UInt8(':'))
            append_json!(out, d, c + 2i + 1)
        end
        push!(out, UInt8('}'))
    end
    return out
end

const HEX_DIGITS = codeunits("0123456789abcdef")

function append_quoted!(out::Vector{UInt8}, s::Bytes)
    push!(out, UInt8('"'))
    for i in 0:s.len-1
        c = at(s, i)
        if c == UInt8('"')
            push!(out, UInt8('\\'), UInt8('"'))
        elseif c == UInt8('\\')
            push!(out, UInt8('\\'), UInt8('\\'))
        elseif c == UInt8('\n')
            push!(out, UInt8('\\'), UInt8('n'))
        elseif c == UInt8('\r')
            push!(out, UInt8('\\'), UInt8('r'))
        elseif c == UInt8('\t')
            push!(out, UInt8('\\'), UInt8('t'))
        elseif c == 0x08
            push!(out, UInt8('\\'), UInt8('b'))
        elseif c == 0x0c
            push!(out, UInt8('\\'), UInt8('f'))
        elseif c < 0x20
            push!(out, UInt8('\\'), UInt8('u'), UInt8('0'), UInt8('0'), HEX_DIGITS[(c>>4)+1], HEX_DIGITS[(c&15)+1])
        else
            push!(out, c)
        end
    end
    push!(out, UInt8('"'))
    return out
end

Base.String(d::Document) = String(append_json!(UInt8[], d, d.root))

function Base.show(io::IO, d::Document)
    print(io, "Document(", String(d), ")")
end

# ----------------------------------------------------------------------------------------------------------------------
# UTF-8 validation (the library's own, so that the result does not depend on the Julia version).

# Reports whether b[from+1:to] is valid UTF-8 (no overlong forms, no surrogates, nothing above U+10FFFF).
function utf8_valid(b::Vector{UInt8}, from::Int, to::Int)
    i = from
    while i < to
        c = b[i+1]
        if c < 0x80
            i += 1
            continue
        end
        if c < 0xc2
            return false
        elseif c < 0xe0
            i + 1 < to || return false
            (b[i+2] & 0xc0) == 0x80 || return false
            i += 2
        elseif c < 0xf0
            i + 2 < to || return false
            c1 = b[i+2]
            (c1 & 0xc0) == 0x80 || return false
            (b[i+3] & 0xc0) == 0x80 || return false
            c == 0xe0 && c1 < 0xa0 && return false
            c == 0xed && c1 >= 0xa0 && return false
            i += 3
        elseif c < 0xf5
            i + 3 < to || return false
            c1 = b[i+2]
            (c1 & 0xc0) == 0x80 || return false
            (b[i+3] & 0xc0) == 0x80 || return false
            (b[i+4] & 0xc0) == 0x80 || return false
            c == 0xf0 && c1 < 0x90 && return false
            c == 0xf4 && c1 >= 0x90 && return false
            i += 4
        else
            return false
        end
    end
    return true
end

# Appends the UTF-8 encoding of a code point.
function append_rune!(out::Vector{UInt8}, cp::Int)
    if cp < 0x80
        push!(out, cp % UInt8)
    elseif cp < 0x800
        push!(out, (0xc0 | (cp >> 6)) % UInt8, (0x80 | (cp & 0x3f)) % UInt8)
    elseif cp < 0x10000
        push!(out, (0xe0 | (cp >> 12)) % UInt8, (0x80 | ((cp >> 6) & 0x3f)) % UInt8, (0x80 | (cp & 0x3f)) % UInt8)
    else
        push!(out, (0xf0 | (cp >> 18)) % UInt8, (0x80 | ((cp >> 12) & 0x3f)) % UInt8,
            (0x80 | ((cp >> 6) & 0x3f)) % UInt8, (0x80 | (cp & 0x3f)) % UInt8)
    end
    return out
end

# In-place sort of v[lo:hi] (one-based, inclusive). Base.sort! takes scratch memory for vectors of integers, and
# this must allocate nothing.
function sort_words!(v::Vector{UInt64}, lo::Int, hi::Int)
    while hi - lo > 16
        mid = lo + ((hi - lo) >> 1)
        a, b, c = v[lo], v[mid], v[hi]
        pivot = a < b ? (b < c ? b : (a < c ? c : a)) : (a < c ? a : (b < c ? c : b))
        i, j = lo, hi
        while i <= j
            while v[i] < pivot
                i += 1
            end
            while v[j] > pivot
                j -= 1
            end
            if i <= j
                v[i], v[j] = v[j], v[i]
                i += 1
                j -= 1
            end
        end
        if j - lo < hi - i
            sort_words!(v, lo, j)
            lo = i
        else
            sort_words!(v, i, hi)
            hi = j
        end
    end
    for i in lo+1:hi
        x = v[i]
        j = i - 1
        while j >= lo && v[j] > x
            v[j+1] = v[j]
            j -= 1
        end
        v[j+1] = x
    end
    return v
end

# ----------------------------------------------------------------------------------------------------------------------
# The parser

# Builds a Document. Its buffers are reused from one parse to the next.
mutable struct Parser
    b::Vector{UInt8}
    i::Int
    # Checking syntax only. No document is built and numbers are not converted.
    validating::Bool
    # Finished children of closed containers, each container's consecutive (two words per node).
    nodes::Vector{UInt64}
    # The values of the open containers, innermost last. A container's run moves to nodes when it closes.
    scratch::Vector{UInt64}
    # The scratch index (in nodes) at which each open container's children start, with the object flag in bit 31.
    frames::Vector{UInt32}
    text::Vector{UInt8}
    # Scratch for finding duplicate keys in large objects.
    hashes::Vector{UInt64}
    # Scratch for rebuilding an object with duplicate keys.
    pairs::Vector{UInt64}
    err_message::String
    err_offset::Int
end

Parser() = Parser(EMPTY_BYTES, 0, false, UInt64[], UInt64[], UInt32[], UInt8[], UInt64[], UInt64[], "", 0)

# The buffers a parser keeps between parses, in bytes, beyond which a pooled parser is dropped.
const RETAINED_LIMIT = 1 << 20

function retains_too_much(p::Parser)
    words = length(p.nodes) + length(p.scratch) + length(p.hashes) + length(p.pairs)
    return 8 * words + length(p.text) > RETAINED_LIMIT
end

@noinline function fail!(p::Parser, message::String, at::Int)
    p.err_message = message
    p.err_offset = at
    return false
end

parse_error(p::Parser) = ParseError(p.err_message, p.err_offset)

function reset!(p::Parser, b::Vector{UInt8}, validating::Bool)
    p.b = b
    p.i = 0
    p.validating = validating
    empty!(p.nodes)
    empty!(p.scratch)
    empty!(p.frames)
    empty!(p.text)
    return nothing
end

# Reports whether b is one valid JSON value. It allocates nothing once the buffers have grown.
function is_valid_json!(p::Parser, b::Vector{UInt8})
    reset!(p, b, true)
    ok = parse!(p)
    p.b = EMPTY_BYTES
    return ok
end

# Parses b into a new document, exactly sized. Throws ParseError.
function parse_new!(p::Parser, b::Vector{UInt8})
    reset!(p, b, false)
    ok = parse!(p)
    p.b = EMPTY_BYTES
    ok || throw(parse_error(p))
    n = length(p.nodes)
    tape = Vector{UInt64}(undef, n + 2)
    copyto!(tape, 1, p.nodes, 1, n)
    tape[n+1] = p.scratch[1]
    tape[n+2] = p.scratch[2]
    return Document(tape, b, copy(p.text), n >> 1)
end

# Parses b into d, reusing its arrays and the parser's (no allocation once they have grown). The finished values are
# written straight into the document's tape. The document is valid until the next parse into it.
function parse_into!(p::Parser, d::Document, b::Vector{UInt8})
    nodes, text = p.nodes, p.text
    p.nodes, p.text = d.tape, d.text
    reset!(p, b, false)
    ok = parse!(p)
    p.b = EMPTY_BYTES
    if ok
        d.root = length(p.nodes) >> 1
        push_words!(p.nodes, p.scratch[1], p.scratch[2])
        d.source = b
    else
        d.source = EMPTY_BYTES
    end
    p.nodes, p.text = nodes, text
    return ok
end

@inline function skip_ws!(p::Parser)
    b = p.b
    i = p.i
    n = length(b)
    while i < n
        c = b[i+1]
        if c != UInt8(' ') && c != UInt8('\n') && c != UInt8('\r') && c != UInt8('\t')
            break
        end
        i += 1
    end
    p.i = i
    return nothing
end

@inline peek(p::Parser) = p.i < length(p.b) ? Int(p.b[p.i+1]) : -1

# Appends two words to a vector. push! with two items is append! of a tuple, which copies the items through the
# general copyto!, more than fifty instructions a word. Two pushes of one item are a store each with a test of the
# capacity.
@inline function push_words!(v::Vector{UInt64}, a::UInt64, b::UInt64)
    push!(v, a)
    push!(v, b)
    return nothing
end

# Appends the words of from, from the one-based index first on, to a vector, as one copy.
@inline function append_words!(v::Vector{UInt64}, from::Vector{UInt64}, first::Int)
    count = length(from) - first + 1
    count > 0 || return nothing
    at = length(v)
    resize!(v, at + count)
    copyto!(v, at + 1, from, first, count)
    return nothing
end

@inline function push_value!(p::Parser, h::UInt64, d::UInt64)
    push_words!(p.scratch, h, d)
    return nothing
end

const OBJECT_FRAME = 0x80000000

function parse!(p::Parser)
    skip_ws!(p)
    while true
        # A value.
        c = peek(p)
        if c == Int('{')
            p.i += 1
            skip_ws!(p)
            length(p.frames) >= MAX_DEPTH && return fail!(p, "nesting too deep", p.i)
            if peek(p) == Int('}')
                p.i += 1
                push_value!(p, UInt64(KIND_OBJECT), UInt64(0))
            else
                push!(p.frames, (length(p.scratch) >> 1) % UInt32 | OBJECT_FRAME)
                key!(p) || return false
                continue
            end
        elseif c == Int('[')
            p.i += 1
            skip_ws!(p)
            length(p.frames) >= MAX_DEPTH && return fail!(p, "nesting too deep", p.i)
            if peek(p) == Int(']')
                p.i += 1
                push_value!(p, UInt64(KIND_ARRAY), UInt64(0))
            else
                push!(p.frames, (length(p.scratch) >> 1) % UInt32)
                continue
            end
        elseif c == Int('"')
            string!(p) || return false
        elseif c == Int('t')
            literal!(p, LIT_TRUE, KIND_BOOL, UInt64(1)) || return false
        elseif c == Int('f')
            literal!(p, LIT_FALSE, KIND_BOOL, UInt64(0)) || return false
        elseif c == Int('n')
            literal!(p, LIT_NULL, KIND_NULL, UInt64(0)) || return false
        elseif c == Int('-') || (Int('0') <= c <= Int('9'))
            number!(p) || return false
        elseif c == -1
            return fail!(p, "unexpected end of input", p.i)
        else
            return fail!(p, "expected a value", p.i)
        end
        # After a value: separators and closing brackets, until the next value or the end.
        next = false
        while !next
            if isempty(p.frames)
                skip_ws!(p)
                p.i != length(p.b) && return fail!(p, "trailing characters", p.i)
                return true
            end
            frame = p.frames[end]
            object = (frame & OBJECT_FRAME) != 0
            skip_ws!(p)
            c = peek(p)
            if c == Int(',')
                p.i += 1
                skip_ws!(p)
                if object && !key!(p)
                    return false
                end
                next = true
            elseif c == Int('}') && object
                p.i += 1
                close!(p, Int(frame & ~OBJECT_FRAME), true)
            elseif c == Int(']') && !object
                p.i += 1
                close!(p, Int(frame), false)
            elseif c == -1
                return fail!(p, "unexpected end of input", p.i)
            elseif object
                return fail!(p, "expected ',' or '}'", p.i)
            else
                return fail!(p, "expected ',' or ']'", p.i)
            end
        end
    end
end

# Reads a property name and its colon, leaving the parser at the value.
function key!(p::Parser)
    peek(p) != Int('"') && return fail!(p, "expected a property name", p.i)
    string!(p) || return false
    skip_ws!(p)
    peek(p) != Int(':') && return fail!(p, "expected ':'", p.i)
    p.i += 1
    skip_ws!(p)
    return true
end

# Moves the closed container's children to nodes and pushes the container in their place.
function close!(p::Parser, start::Int, object::Bool)
    pop!(p.frames)
    scratch = p.scratch
    if object && (length(scratch) >> 1) - start > 2 && !p.validating
        dedupe!(p, start)
    end
    children = (length(scratch) >> 1) - start
    nodes = p.nodes
    first_child = length(nodes) >> 1
    append_words!(nodes, scratch, 2 * start + 1)
    resize!(scratch, 2 * start)
    if object
        push_value!(p, UInt64(KIND_OBJECT) | UInt64(children >> 1) << 32, UInt64(first_child))
    else
        push_value!(p, UInt64(KIND_ARRAY) | UInt64(children) << 32, UInt64(first_child))
    end
    return nothing
end

@inline function key_bytes(p::Parser, h::UInt64, off::UInt64)
    return Bytes((h & (UInt64(STR_TEXT) << 8)) != 0 ? p.text : p.b, off % Int, (h >> 32) % Int)
end

# The bytes of the key at scratch value index a.
@inline scratch_key(p::Parser, a::Int) = key_bytes(p, p.scratch[2a+1], p.scratch[2a+2])

@inline function key_equals(p::Parser, a::Int, b::Int)
    (p.scratch[2a+1] >> 32) != (p.scratch[2b+1] >> 32) && return false
    return bytes_equal(scratch_key(p, a), scratch_key(p, b))
end

# Keeps, of duplicate property names in the object whose pairs start at start, the last value at the first position.
function dedupe!(p::Parser, start::Int)
    n = ((length(p.scratch) >> 1) - start) >> 1
    duplicate = false
    if n <= 16
        for j in 1:n-1
            for k in 0:j-1
                if key_equals(p, start + 2k, start + 2j)
                    duplicate = true
                    break
                end
            end
            duplicate && break
        end
    else
        # Sorted by hash in reused scratch. Only keys with equal hashes are compared.
        h = p.hashes
        empty!(h)
        for j in 0:n-1
            push!(h, (str_hash(scratch_key(p, start + 2j)) & ~UInt64(0xffffffff)) | UInt64(j))
        end
        sort_words!(h, 1, n)
        a = 0
        while a < n && !duplicate
            e = a + 1
            while e < n && (h[e+1] >> 32) == (h[a+1] >> 32)
                e += 1
            end
            for x in a+1:e-1
                for y in a:x-1
                    if key_equals(p, start + 2 * Int(h[x+1] % UInt32), start + 2 * Int(h[y+1] % UInt32))
                        duplicate = true
                        break
                    end
                end
                duplicate && break
            end
            a = e
        end
    end
    duplicate || return nothing
    pairs = p.pairs
    empty!(pairs)
    scratch = p.scratch
    append_words!(pairs, scratch, 2 * start + 1)
    resize!(scratch, 2 * start)
    for q in 0:n-1
        name = key_bytes(p, pairs[4q+1], pairs[4q+2])
        found = -1
        k = start
        while k < (length(scratch) >> 1)
            if bytes_equal(scratch_key(p, k), name)
                found = k
                break
            end
            k += 2
        end
        if found >= 0
            scratch[2*(found+1)+1] = pairs[4q+3]
            scratch[2*(found+1)+2] = pairs[4q+4]
        else
            push_words!(scratch, pairs[4q+1], pairs[4q+2])
            push_words!(scratch, pairs[4q+3], pairs[4q+4])
        end
    end
    return nothing
end

const LIT_TRUE = (UInt8('t'), UInt8('r'), UInt8('u'), UInt8('e'))
const LIT_FALSE = (UInt8('f'), UInt8('a'), UInt8('l'), UInt8('s'), UInt8('e'))
const LIT_NULL = (UInt8('n'), UInt8('u'), UInt8('l'), UInt8('l'))

@inline function literal!(p::Parser, word::NTuple{N,UInt8}, k::UInt8, d::UInt64) where {N}
    b = p.b
    i = p.i
    i + N > length(b) && return fail!(p, "expected a value", i)
    for x in 1:N
        b[i+x] == word[x] || return fail!(p, "expected a value", i)
    end
    p.i = i + N
    push_value!(p, UInt64(k), d)
    return true
end

# By byte: 0 for plain ASCII, 1 for a quote, backslash or control character, 2 for a non-ASCII byte.
const SCAN = let t = zeros(UInt8, 256)
    for c in 0:0x1f
        t[c+1] = 1
    end
    t[Int('"')+1] = 1
    t[Int('\\')+1] = 1
    for c in 0x80:0xff
        t[c+1] = 2
    end
    Tuple(t)
end

const SWAR_ONES = 0x0101010101010101
const SWAR_HIGHS = 0x8080808080808080

# Reads a string, from its opening quote. Eight bytes at a time while there are that many, then a byte at a time.
function string!(p::Parser)
    b = p.b
    n = length(b)
    start = p.i + 1
    j = start
    high = UInt64(0)
    stopped = false
    while j + 8 <= n
        w = le64(b, j)
        # The bytes that end the run: below 0x20, a quote or a backslash. Each test marks the high bit of a byte
        # that matches. A borrow can mark a byte wrongly only above one that matches, and the lowest mark is taken.
        quot = w ⊻ (SWAR_ONES * UInt64('"'))
        backslash = w ⊻ (SWAR_ONES * UInt64('\\'))
        stop = (((w - SWAR_ONES * 0x20) & ~w) | ((quot - SWAR_ONES) & ~quot) |
                ((backslash - SWAR_ONES) & ~backslash)) & SWAR_HIGHS
        if stop != 0
            # Only the non-ASCII bytes before the stop count.
            high |= w & SWAR_HIGHS & ((stop & (-stop)) - 1)
            j += trailing_zeros(stop) >> 3
            stopped = true
            break
        end
        high |= w & SWAR_HIGHS
        j += 8
    end
    wide = high != 0 ? 0x02 : 0x00
    # The last bytes of the text, fewer than eight. A run that ended in a word is not looked at again.
    if !stopped
        while j < n
            c = SCAN[b[j+1]+1]
            c == 1 && break
            wide |= c
            j += 1
        end
        j >= n && return fail!(p, "unterminated string", j)
    end
    c = b[j+1]
    if c == UInt8('"')
        h = UInt64(KIND_STRING) | UInt64(j - start) << 32
        if wide != 0
            utf8_valid(b, start, j) || return fail!(p, "invalid UTF-8", start)
            h |= UInt64(STR_WIDE) << 8
        end
        p.i = j + 1
        push_value!(p, h, UInt64(start))
        return true
    elseif c == UInt8('\\')
        return escaped!(p, start, j, wide != 0)
    end
    return fail!(p, "control character in a string", j)
end

@inline function append_run!(text::Vector{UInt8}, b::Vector{UInt8}, from::Int, to::Int)
    for k in from+1:to
        push!(text, b[k])
    end
    return nothing
end

# Reads the rest of a string with escapes, unescaped into the text buffer. j is at the first backslash.
function escaped!(p::Parser, start::Int, j::Int, wide::Bool)
    text = p.text
    offset = length(text)
    b = p.b
    n = length(b)
    run = start
    while true
        j >= n && return fail!(p, "unterminated string", j)
        c = b[j+1]
        if c == UInt8('"')
            if wide && !utf8_valid(b, run, j)
                return fail!(p, "invalid UTF-8", run)
            end
            append_run!(text, b, run, j)
            p.i = j + 1
            h = UInt64(KIND_STRING) | UInt64(STR_TEXT) << 8 | UInt64(length(text) - offset) << 32
            if wide
                h |= UInt64(STR_WIDE) << 8
            end
            push_value!(p, h, UInt64(offset))
            return true
        elseif c == UInt8('\\')
            if wide && !utf8_valid(b, run, j)
                return fail!(p, "invalid UTF-8", run)
            end
            append_run!(text, b, run, j)
            e = j + 1 < n ? b[j+2] : 0x00
            out = 0x00
            if e == UInt8('"') || e == UInt8('\\') || e == UInt8('/')
                out = e
            elseif e == UInt8('b')
                out = 0x08
            elseif e == UInt8('f')
                out = 0x0c
            elseif e == UInt8('n')
                out = UInt8('\n')
            elseif e == UInt8('r')
                out = UInt8('\r')
            elseif e == UInt8('t')
                out = UInt8('\t')
            elseif e == UInt8('u')
                cp = unicode_escape!(p, j)
                cp < 0 && return false
                j += cp >= 0x10000 ? 12 : 6
                if cp >= 0x80
                    wide = true
                end
                append_rune!(text, cp)
                run = j
                continue
            else
                return fail!(p, "invalid escape", j)
            end
            push!(text, out)
            j += 2
            run = j
        elseif c < 0x20
            return fail!(p, "control character in a string", j)
        else
            if c >= 0x80
                wide = true
            end
            j += 1
        end
    end
end

function hex4(b::Vector{UInt8}, at::Int)
    at + 4 > length(b) && return -1
    v = 0
    for k in at+1:at+4
        c = b[k]
        d = if UInt8('0') <= c <= UInt8('9')
            Int(c - UInt8('0'))
        elseif UInt8('a') <= c <= UInt8('f')
            Int(c - UInt8('a')) + 10
        elseif UInt8('A') <= c <= UInt8('F')
            Int(c - UInt8('A')) + 10
        else
            return -1
        end
        v = v * 16 + d
    end
    return v
end

# Reads a \u escape at j (and its low surrogate, for a high one). The code point, or -1 after recording the error.
function unicode_escape!(p::Parser, j::Int)
    b = p.b
    u = hex4(b, j + 2)
    if u < 0
        fail!(p, "invalid \\u escape", j)
        return -1
    end
    if 0xd800 <= u <= 0xdbff
        low = -1
        if j + 7 < length(b) && b[j+7] == UInt8('\\') && b[j+8] == UInt8('u')
            low = hex4(b, j + 8)
        end
        if 0xdc00 <= low <= 0xdfff
            return 0x10000 + ((u - 0xd800) << 10) + (low - 0xdc00)
        end
        fail!(p, "lone leading surrogate in hex escape", j)
        return -1
    end
    if 0xdc00 <= u <= 0xdfff
        fail!(p, "lone trailing surrogate in hex escape", j)
        return -1
    end
    return u
end

@inline is_digit_at(b::Vector{UInt8}, j::Int) = j < length(b) && UInt8('0') <= b[j+1] <= UInt8('9')

const POWERS10 = (1e0, 1e1, 1e2, 1e3, 1e4, 1e5, 1e6, 1e7, 1e8, 1e9, 1e10, 1e11, 1e12, 1e13, 1e14, 1e15, 1e16, 1e17,
    1e18, 1e19, 1e20, 1e21, 1e22)

# Reads a number: integers that fit 64 bits as integers (unsigned beyond an Int64), anything else as a Float64. The
# header keeps the offset of the text.
function number!(p::Parser)
    b = p.b
    n = length(b)
    start = p.i
    j = start
    negative = b[j+1] == UInt8('-')
    if negative
        j += 1
    end
    # The digits of the integer and the fraction as one integer, while they fit: mantissa. exact says no digit was
    # left out of it.
    mantissa = UInt64(0)
    exact = true
    int_start = j
    if j < n && b[j+1] == UInt8('0')
        j += 1
    else
        # Nineteen digits cannot overflow 64 bits.
        limit = min(n, j + 19)
        while j < limit
            c = b[j+1] - UInt8('0')
            c > 9 && break
            mantissa = mantissa * 10 + UInt64(c)
            j += 1
        end
        j == int_start && return fail!(p, "invalid number", j)
        while is_digit_at(b, j)
            if exact
                wide = widemul(mantissa, UInt64(10)) + UInt128(b[j+1] - UInt8('0'))
                if (wide >> 64) != 0
                    exact = false
                else
                    mantissa = wide % UInt64
                end
            end
            j += 1
        end
    end
    digits = j - int_start
    floating = false
    scale = 0
    if j < n && b[j+1] == UInt8('.')
        j += 1
        frac_start = j
        while j < n
            c = b[j+1] - UInt8('0')
            c > 9 && break
            digits += 1
            if digits <= 19
                mantissa = mantissa * 10 + UInt64(c)
            else
                exact = false
            end
            j += 1
        end
        j == frac_start && return fail!(p, "invalid number", j)
        scale = frac_start - j
        floating = true
    end
    exponent = 0
    exp_overflow = false
    if j < n && (b[j+1] == UInt8('e') || b[j+1] == UInt8('E'))
        j += 1
        exp_negative = false
        if j < n && (b[j+1] == UInt8('+') || b[j+1] == UInt8('-'))
            exp_negative = b[j+1] == UInt8('-')
            j += 1
        end
        is_digit_at(b, j) || return fail!(p, "invalid number", j)
        while is_digit_at(b, j)
            if exponent < 100000
                exponent = exponent * 10 + Int(b[j+1] - UInt8('0'))
            else
                exp_overflow = true
            end
            j += 1
        end
        if exp_negative
            exponent = -exponent
        end
        floating = true
    end
    p.i = j
    h = UInt64(KIND_NUMBER) | UInt64(start) << 32
    if !floating && exact
        if !negative
            if (mantissa >> 63) != 0
                push_value!(p, h | UInt64(NUM_UINT) << 8, mantissa)
            else
                push_value!(p, h | UInt64(NUM_INT) << 8, mantissa)
            end
            return true
        end
        # -0 is the float.
        if mantissa != 0 && mantissa <= UInt64(1) << 63
            push_value!(p, h | UInt64(NUM_INT) << 8, -mantissa)
            return true
        end
    end
    # A mantissa that a Float64 holds exactly and a power of ten that one does too: one exact conversion and one
    # correctly rounded operation give the correctly rounded value (Clinger's fast path).
    scale += exponent
    d = 0.0
    if exact && !exp_overflow && (mantissa >> 53) == 0 && -22 <= scale <= 22
        d = Float64(mantissa)
        if scale < 0
            d /= POWERS10[1-scale]
        else
            d *= POWERS10[1+scale]
        end
        if negative
            d = -d
        end
    elseif p.validating && !exp_overflow && digits + exponent < 300
        # Checking syntax only: the number is well within the range of a Float64, and its value is not needed.
    else
        d = decimal_to_float(b, start, j)
        isinf(d) && return fail!(p, "number out of range", start)
    end
    push_value!(p, h | UInt64(NUM_FLOAT) << 8, reinterpret(UInt64, d))
    return true
end

# ----------------------------------------------------------------------------------------------------------------------
# Entry points

const PARSERS = Parser[]
const PARSERS_LOCK = Base.Threads.SpinLock()

function take_parser()
    lock(PARSERS_LOCK)
    p = isempty(PARSERS) ? nothing : pop!(PARSERS)
    unlock(PARSERS_LOCK)
    return p === nothing ? Parser() : p
end

function return_parser(p::Parser)
    retains_too_much(p) && return nothing
    lock(PARSERS_LOCK)
    length(PARSERS) < 64 && push!(PARSERS, p)
    unlock(PARSERS_LOCK)
    return nothing
end

"""
    parse_document(json) -> Document

Parse UTF-8 JSON text, given as a string or as a vector of bytes, into a [`Document`](@ref). A vector of bytes is
kept, not copied, so do not modify it afterwards. Throws [`ParseError`](@ref) for text that is not valid JSON.
"""
function parse_document(json::Vector{UInt8})
    p = take_parser()
    try
        return parse_new!(p, json)
    finally
        return_parser(p)
    end
end

parse_document(json::AbstractString) = parse_document(Vector{UInt8}(codeunits(String(json))))
parse_document(json::AbstractVector{UInt8}) = parse_document(Vector{UInt8}(json))
