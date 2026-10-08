# Unicode for ECMA-262 patterns: the sets of the \p{...} escapes, the characters of a group name, and the case folding
# of a case-insensitive group. The data is unicode_data.jl, of one Unicode version, so a pattern means the same
# whichever Julia runs it. Julia's own tables (utf8proc) and those of the PCRE2 it bundles change between Julia
# versions, and neither has every property ECMA-262 lists.
#
# A set of code points is a Vector{Int32} of inclusive ranges, sorted, that neither overlap nor touch. A set is read
# from its table the first time it is asked for and then kept, so the set of a property is always the same array.
# Nothing here runs while a string is matched.

const CodeSet = Vector{Int32}

# The last code point.
const MAX_CODE_POINT = Int32(0x10FFFF)

# Every code point.
const ANY_SET = Int32[0, MAX_CODE_POINT]

const NO_CHARACTERS = Int32[]

# Guards the tables below, which are filled on first use. Patterns may be compiled from several tasks at once.
const DATA_LOCK = ReentrantLock()
const TABLES = Vector{Union{Nothing,CodeSet}}(nothing, length(TABLE_TEXTS))
const CATEGORIES = Dict{UInt32,CodeSet}()

const DIGIT_VALUES = let values = zeros(Int, 128)
    alphabet = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz+/"
    for (i, c) in enumerate(alphabet)
        values[Int(c) + 1] = i - 1
    end
    values
end

# Sets.

# The set of the first `count` numbers of `pairs`, read as inclusive ranges in any order.
function setof(pairs::AbstractVector{Int32}, count::Int=length(pairs))
    n = count ÷ 2
    keys = Vector{Int64}(undef, n)
    for i in 1:n
        keys[i] = (Int64(pairs[2i - 1]) << 32) | Int64(pairs[2i])
    end
    sort!(keys)
    merged = CodeSet()
    sizehint!(merged, count)
    for key in keys
        lo = Int32(key >> 32)
        hi = Int32(key & 0xFFFFFFFF)
        if !isempty(merged) && lo <= merged[end] + 1
            if hi > merged[end]
                merged[end] = hi
            end
            continue
        end
        push!(merged, lo)
        push!(merged, hi)
    end
    return merged
end

# Every code point that is not in the set.
function complement(set::CodeSet)
    out = CodeSet()
    next = Int32(0)
    for i in 1:2:length(set)
        if set[i] > next
            push!(out, next)
            push!(out, set[i] - Int32(1))
        end
        next = set[i + 1] + Int32(1)
    end
    if next <= MAX_CODE_POINT
        push!(out, next)
        push!(out, MAX_CODE_POINT)
    end
    return out
end

# Every code point that is in either set.
setunion(a::CodeSet, b::CodeSet) = setof(vcat(a, b))

# The code points of `a` that are not in `b`.
difference(a::CodeSet, b::CodeSet) = complement(setunion(complement(a), b))

# The code points that are in both sets.
intersection(a::CodeSet, b::CodeSet) = complement(setunion(complement(a), complement(b)))

# Whether the set holds the code point.
function inset(set::CodeSet, c::Integer)
    lo = 0
    hi = length(set) ÷ 2
    while lo < hi
        mid = (lo + hi) >>> 1
        if c > set[2mid + 2]
            lo = mid + 1
        elseif c < set[2mid + 1]
            hi = mid
        else
            return true
        end
    end
    return false
end

# Whether the two sets share a code point.
function intersects(a::CodeSet, b::CodeSet)
    i = 1
    j = 1
    while i < length(a) && j < length(b)
        if a[i + 1] < b[j]
            i += 2
        elseif b[j + 1] < a[i]
            j += 2
        else
            return true
        end
    end
    return false
end

# Tables.

# Reads one number of a table's text at `at`. Returns the number and the position after it.
@inline function tablenumber(text::String, at::Int)
    value = 0
    shift = 0
    while true
        digit = DIGIT_VALUES[codeunit(text, at) + 1]
        at += 1
        value |= (digit & 31) << shift
        digit < 32 && return value, at
        shift += 5
    end
end

# The set of a table of unicode_data.jl, by its number, which counts from zero.
function table(number::Int)
    lock(DATA_LOCK)
    try
        set = TABLES[number + 1]
        if set === nothing
            text = TABLE_TEXTS[number + 1]
            set = CodeSet()
            at = 1
            next = 0
            while at <= ncodeunits(text)
                gap, at = tablenumber(text, at)
                len, at = tablenumber(text, at)
                lo = next + gap
                hi = lo + len
                push!(set, Int32(lo))
                push!(set, Int32(hi))
                next = hi + 1
            end
            TABLES[number + 1] = set
        end
        return set
    finally
        unlock(DATA_LOCK)
    end
end

# A name, as ASCII, against the code points or the bytes p[start:stop] (a pattern as either). Allocates nothing.
function isname(name::String, p::AbstractVector{<:Integer}, start::Int, stop::Int)
    stop - start + 1 == ncodeunits(name) || return false
    for k in 1:ncodeunits(name)
        p[start + k - 1] == codeunit(name, k) || return false
    end
    return true
end

# The index in `names` of the name p[start:stop], or 0. Allocates nothing.
function findname(names::Vector{String}, p::AbstractVector{<:Integer}, start::Int, stop::Int)
    for i in eachindex(names)
        isname(names[i], p, start, stop) && return i
    end
    return 0
end

# What a property expression names: nothing, a General_Category value, a binary property, a script by its Script
# property, or a script by its Script_Extensions property. The index of the name is in the low bits.
const PROPERTY_NONE = -1
const PROPERTY_CATEGORY = 1 << 16
const PROPERTY_BINARY = 2 << 16
const PROPERTY_SCRIPT = 3 << 16
const PROPERTY_SCRIPT_EXTENSIONS = 4 << 16

# Resolves the expression p[start:stop] between the braces of \p{...}, as ECMA-262 does: a name and a value of
# General_Category, Script or Script_Extensions joined by =, or the lone name of a General_Category value or of a
# binary property. Names are matched exactly. Allocates nothing.
function resolveproperty(p::AbstractVector{<:Integer}, start::Int, stop::Int)
    eq = 0
    for i in start:stop
        if p[i] == UInt8('=')
            eq = i
            break
        end
    end
    if eq == 0
        category = findname(CATEGORY_NAMES, p, start, stop)
        category > 0 && return PROPERTY_CATEGORY | category
        binary = findname(BINARY_NAMES, p, start, stop)
        return binary > 0 ? PROPERTY_BINARY | binary : PROPERTY_NONE
    end
    if isname("General_Category", p, start, eq - 1) || isname("gc", p, start, eq - 1)
        category = findname(CATEGORY_NAMES, p, eq + 1, stop)
        return category > 0 ? PROPERTY_CATEGORY | category : PROPERTY_NONE
    end
    if isname("Script", p, start, eq - 1) || isname("sc", p, start, eq - 1)
        script = findname(SCRIPT_NAMES, p, eq + 1, stop)
        return script > 0 ? PROPERTY_SCRIPT | script : PROPERTY_NONE
    end
    if isname("Script_Extensions", p, start, eq - 1) || isname("scx", p, start, eq - 1)
        script = findname(SCRIPT_NAMES, p, eq + 1, stop)
        return script > 0 ? PROPERTY_SCRIPT_EXTENSIONS | script : PROPERTY_NONE
    end
    return PROPERTY_NONE
end

# Whether p[start:stop] is a property expression ECMA-262 defines. Allocates nothing.
isproperty(p::AbstractVector{<:Integer}, start::Int, stop::Int) = resolveproperty(p, start, stop) != PROPERTY_NONE
isproperty(expression::String) = isproperty(codeunits(expression), 1, ncodeunits(expression))

# The set of the property expression p[start:stop], or nothing when ECMA-262 does not define it.
function property(p::AbstractVector{<:Integer}, start::Int, stop::Int)
    resolved = resolveproperty(p, start, stop)
    resolved == PROPERTY_NONE && return nothing
    index = resolved & 0xFFFF
    kind = resolved - index
    if kind == PROPERTY_CATEGORY
        return category(CATEGORY_TABLES[index])
    elseif kind == PROPERTY_BINARY
        return table(BINARY_TABLES[index])
    elseif kind == PROPERTY_SCRIPT
        return table(SCRIPT_TABLES[index])
    end
    return table(SCRIPT_EXTENSION_TABLES[index])
end

property(expression::String) = property(codeunits(expression), 1, ncodeunits(expression))

# The union of the General_Category tables whose bits are set.
function category(tables::UInt32)
    count_ones(tables) == 1 && return table(trailing_zeros(tables))
    lock(DATA_LOCK)
    try
        set = get(CATEGORIES, tables, nothing)
        if set === nothing
            pairs = CodeSet()
            bits = tables
            while bits != 0
                append!(pairs, table(trailing_zeros(bits)))
                bits &= bits - UInt32(1)
            end
            set = setof(pairs)
            CATEGORIES[tables] = set
        end
        return set
    finally
        unlock(DATA_LOCK)
    end
end

# Group names.

const ID_START = Ref{Union{Nothing,CodeSet}}(nothing)
const ID_CONTINUE = Ref{Union{Nothing,CodeSet}}(nothing)

isasciiletter(c::Integer) = (c >= UInt8('a') && c <= UInt8('z')) || (c >= UInt8('A') && c <= UInt8('Z'))
isasciidigit(c::Integer) = c >= UInt8('0') && c <= UInt8('9')

# Whether a code point may start a group name: ID_Start, $ or _.
function isnamestart(c::Integer)
    if c < 128
        return isasciiletter(c) || c == UInt8('$') || c == UInt8('_')
    end
    set = ID_START[]
    if set === nothing
        set = property("ID_Start")::CodeSet
        ID_START[] = set
    end
    return inset(set, c)
end

# Whether a code point may continue a group name: ID_Continue, $, U+200C or U+200D.
function isnamepart(c::Integer)
    if c < 128
        return isasciiletter(c) || isasciidigit(c) || c == UInt8('$') || c == UInt8('_')
    end
    if c == 0x200C || c == 0x200D
        return true
    end
    set = ID_CONTINUE[]
    if set === nothing
        set = property("ID_Continue")::CodeSet
        ID_CONTINUE[] = set
    end
    return inset(set, c)
end

# Case folding. ECMA-262 defines case-insensitive matching through a Canonicalize function: two characters match when
# they canonicalize to the same character. With the u flag grammar it is Unicode simple case folding. With no flag it
# is the uppercase mapping of one UTF-16 code unit, when that is again one code unit, and it never maps a character
# outside ASCII into ASCII.

# The characters of the Basic Multilingual Plane whose full uppercase form is more than one character, which the
# Canonicalize of a pattern with no flag leaves alone (they are the single characters of SpecialCasing.txt).
const MULTIPLE_UPPERCASE = Int32[
    0xDF, 0xDF, 0x149, 0x149, 0x1F0, 0x1F0, 0x390, 0x390, 0x3B0, 0x3B0, 0x587, 0x587, 0x1E96, 0x1E9A, 0x1F50, 0x1F50,
    0x1F52, 0x1F52, 0x1F54, 0x1F54, 0x1F56, 0x1F56, 0x1F80, 0x1FAF, 0x1FB2, 0x1FB4, 0x1FB6, 0x1FB7, 0x1FBC, 0x1FBC,
    0x1FC2, 0x1FC4, 0x1FC6, 0x1FC7, 0x1FCC, 0x1FCC, 0x1FD2, 0x1FD3, 0x1FD6, 0x1FD7, 0x1FE2, 0x1FE4, 0x1FE6, 0x1FE7,
    0x1FF2, 0x1FF4, 0x1FF6, 0x1FF7, 0x1FFC, 0x1FFC, 0xFB00, 0xFB06, 0xFB13, 0xFB17,
]

# The characters that are equivalent to some other character, each with every member of its class.
struct Folding
    # The characters, sorted.
    points::Vector{Int32}
    # For each of the points, the members of its class, itself among them.
    classes::Vector{Vector{Int32}}
    # The points as a set.
    set::CodeSet
end

const FOLDINGS = Union{Nothing,Folding}[nothing, nothing]

# A case mapping of unicode_data.jl as runs of start, length, delta and stride.
function casemapping(text::String)
    out = Int[]
    at = 1
    next = 0
    while at <= ncodeunits(text)
        gap, at = tablenumber(text, at)
        len, at = tablenumber(text, at)
        delta, at = tablenumber(text, at)
        stride, at = tablenumber(text, at)
        start = next + gap
        push!(out, start, len + 1, (delta & 1) != 0 ? -(delta >> 1) : delta >> 1, stride + 1)
        next = start + len + 1
    end
    return out
end

# What a mapping makes of a code point.
function mapcodepoint(runs::Vector{Int}, c::Int)
    lo = 0
    hi = length(runs) ÷ 4
    while lo < hi
        mid = (lo + hi) >>> 1
        start = runs[4mid + 1]
        if c < start
            hi = mid
        elseif c >= start + runs[4mid + 2]
            lo = mid + 1
        else
            return (c - start) % runs[4mid + 4] == 0 ? c + runs[4mid + 3] : c
        end
    end
    return c
end

function addmember!(bykey::Dict{Int32,Vector{Int32}}, key::Integer, member::Integer)
    members = get!(() -> Int32[], bykey, Int32(key))
    member in members || push!(members, Int32(member))
    return nothing
end

function folding(unicode::Bool)
    lock(DATA_LOCK)
    try
        f = FOLDINGS[unicode ? 1 : 2]
        f === nothing || return f
        bykey = Dict{Int32,Vector{Int32}}()
        if unicode
            runs = casemapping(SIMPLE_FOLDING)
            for r in 1:4:length(runs)
                for c in runs[r]:runs[r + 3]:(runs[r] + runs[r + 1] - 1)
                    key = c + runs[r + 2]
                    addmember!(bykey, key, key)
                    addmember!(bykey, key, c)
                end
            end
        else
            runs = casemapping(UPPERCASE)
            for c in 0:0xFFFF
                upper = inset(MULTIPLE_UPPERCASE, c) ? c : mapcodepoint(runs, c)
                if (c >= 128 && upper < 128) || upper > 0xFFFF
                    upper = c
                end
                addmember!(bykey, upper, c)
            end
        end
        points = Int32[]
        for members in values(bykey)
            length(members) > 1 && append!(points, members)
        end
        sort!(points)
        classes = Vector{Vector{Int32}}(undef, length(points))
        for members in values(bykey)
            if length(members) > 1
                for member in members
                    classes[searchsortedfirst(points, member)] = members
                end
            end
        end
        pairs = Int32[]
        for point in points
            push!(pairs, point, point)
        end
        f = Folding(points, classes, setof(pairs))
        FOLDINGS[unicode ? 1 : 2] = f
        return f
    finally
        unlock(DATA_LOCK)
    end
end

# The set of every character equivalent to some member of the set, under the Canonicalize of the u flag grammar
# (`unicode`) or of a pattern with no flag.
function foldclosure(set::CodeSet, unicode::Bool)
    f = folding(unicode)
    extra = Int32[]
    for i in 1:2:length(set)
        k = searchsortedfirst(f.points, set[i])
        while k <= length(f.points) && f.points[k] <= set[i + 1]
            for member in f.classes[k]
                if !inset(set, member)
                    push!(extra, member, member)
                end
            end
            k += 1
        end
    end
    isempty(extra) && return set
    return setunion(set, extra)
end

# The characters that are equivalent to some other character under the Canonicalize of a grammar.
cased(unicode::Bool) = folding(unicode).set
