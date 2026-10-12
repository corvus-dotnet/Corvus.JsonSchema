# Checks that a string is a valid ECMA-262 regular expression with the u flag (the regex format), reading it as the
# parser does but building nothing, so it allocates nothing once its buffers have grown. It reads the UTF-8 bytes of the
# pattern as they are. A validator is not safe to share between tasks. `isvalidpattern` keeps a pool of them.

# Thrown on the first error.
struct InvalidPattern <: Exception end

mutable struct Validator
    p::String
    # The position and the length, in bytes. The position counts from zero.
    i::Int
    n::Int
    groupcount::Int
    depth::Int
    # Group names: for each, its range of the pattern, then the place it stands at (see `places`).
    names::Vector{Int}
    # The disjunctions that enclose the current position, innermost last: for each, where it starts, where its
    # current alternative starts, and the place that stands for the two, or 0 until a group name needs one.
    opened::Vector{Int}
    # The places group names stand at: for each, where a disjunction starts, where one of its alternatives starts,
    # and the place of the alternative that encloses the disjunction, or 0. A place is made when a name first
    # stands in its alternative, so a pattern has at most one for each of its alternatives and none without names.
    places::Vector{Int}
    # The group names by name, and names that are the same in the order they are written in.
    order::Vector{Int}
    # Named references, as ranges of the pattern.
    refs::Vector{Int}
    # Where the character that `charat` or `namechar` last read ends.
    next::Int
end

Validator() = Validator("", 0, 0, 0, 0, sizehint!(Int[], 16), sizehint!(Int[], 16), sizehint!(Int[], 16),
    sizehint!(Int[], 8), sizehint!(Int[], 8), 0)

invalid() = throw(InvalidPattern())

function validate(v::Validator, pattern::String)
    v.p = pattern
    v.i = 0
    v.n = ncodeunits(pattern)
    v.depth = 0
    empty!(v.names)
    empty!(v.opened)
    empty!(v.places)
    empty!(v.order)
    empty!(v.refs)
    try
        v.groupcount = countgroups(v)
        disjunction(v)
        v.i < v.n && return false
        return namesvalid(v)
    catch e
        e isa InvalidPattern || rethrow()
        return false
    finally
        v.p = ""
        release!(v)
    end
end

# The most numbers a buffer keeps from one pattern to the next. A pattern of a megabyte may need a few million.
const MAX_KEPT_NUMBERS = 1 << 16

function release!(v::Validator)
    for buffer in (v.names, v.opened, v.places, v.order, v.refs)
        if length(buffer) > MAX_KEPT_NUMBERS
            empty!(buffer)
            sizehint!(buffer, 16)
        end
    end
    return nothing
end

# The byte at a position that counts from zero.
@inline byteat(v::Validator, k::Int) = codeunit(v.p, k + 1)

@inline iscontinuation(b::UInt8) = (b & 0xC0) == 0x80

# The code point at the byte position `at`, which is inside the pattern. Sets `next` to where it ends. A pattern
# that is not UTF-8 is not valid. A surrogate written as three bytes is taken as it is, as the parser takes it.
function charat(v::Validator, at::Int)
    b = byteat(v, at)
    if b < 0x80
        v.next = at + 1
        return Int32(b)
    end
    n = v.n
    if b >= 0xC2 && b <= 0xDF && at + 1 < n
        b1 = byteat(v, at + 1)
        if iscontinuation(b1)
            v.next = at + 2
            return (Int32(b & 0x1F) << 6) | Int32(b1 & 0x3F)
        end
    elseif b >= 0xE0 && b <= 0xEF && at + 2 < n
        b1 = byteat(v, at + 1)
        b2 = byteat(v, at + 2)
        if iscontinuation(b1) && iscontinuation(b2) && (b != 0xE0 || b1 >= 0xA0)
            v.next = at + 3
            return (Int32(b & 0x0F) << 12) | (Int32(b1 & 0x3F) << 6) | Int32(b2 & 0x3F)
        end
    elseif b >= 0xF0 && b <= 0xF4 && at + 3 < n
        b1 = byteat(v, at + 1)
        b2 = byteat(v, at + 2)
        b3 = byteat(v, at + 3)
        if iscontinuation(b1) && iscontinuation(b2) && iscontinuation(b3) && (b != 0xF0 || b1 >= 0x90) &&
           (b != 0xF4 || b1 <= 0x8F)
            v.next = at + 4
            return (Int32(b & 0x07) << 18) | (Int32(b1 & 0x3F) << 12) | (Int32(b2 & 0x3F) << 6) |
                   Int32(b3 & 0x3F)
        end
    end
    invalid()
end

# The code point at the position, or -1 at the end.
peekchar(v::Validator) = v.i < v.n ? charat(v, v.i) : Int32(-1)

# Whether the byte at the position is the ASCII character `c`.
@inline isat(v::Validator, c::Char) = v.i < v.n && byteat(v, v.i) == UInt8(c)

function eat(v::Validator, c::Char)
    if isat(v, c)
        v.i += 1
        return true
    end
    return false
end

function countgroups(v::Validator)
    count = 0
    inclass = false
    n = v.n
    k = 0
    while k < n
        c = byteat(v, k)
        if c == UInt8('\\')
            k += 1
        elseif c == UInt8('[')
            inclass = true
        elseif c == UInt8(']')
            inclass = false
        elseif c == UInt8('(') && !inclass
            if k + 1 >= n || byteat(v, k + 1) != UInt8('?')
                count += 1
            elseif k + 3 < n && byteat(v, k + 2) == UInt8('<') && byteat(v, k + 3) != UInt8('=') &&
                   byteat(v, k + 3) != UInt8('!')
                count += 1
            end
        end
        k += 1
    end
    return count
end

function disjunction(v::Validator)
    v.depth += 1
    v.depth > MAX_NESTING + 1 && invalid()
    push!(v.opened, v.i, v.i, 0)
    level = length(v.opened)
    alternative(v)
    while eat(v, '|')
        # The next alternative starts here, and has no place yet.
        v.opened[level - 1] = v.i
        v.opened[level] = 0
        alternative(v)
    end
    resize!(v.opened, level - 3)
    v.depth -= 1
    return nothing
end

function alternative(v::Validator)
    while v.i < v.n && !isat(v, '|') && !isat(v, ')')
        term(v)
    end
    return nothing
end

function term(v::Validator)
    c = peekchar(v)
    quantifiable = true
    if c == Int32('^') || c == Int32('$')
        v.i += 1
        quantifiable = false
    elseif c == Int32('(')
        quantifiable = group(v)
    elseif c == Int32('.')
        v.i += 1
    elseif c == Int32('[')
        characterclass(v)
    elseif c == Int32('\\')
        quantifiable = atomescape(v)
    elseif c == Int32('*') || c == Int32('+') || c == Int32('?') || c == Int32('{') || c == Int32(']') ||
           c == Int32('}')
        invalid()
    else
        v.i = v.next
    end
    if quantifier(v) && !quantifiable
        invalid()
    end
    return nothing
end

@inline isdigitbyte(b::UInt8) = b >= UInt8('0') && b <= UInt8('9')

function lookslikequantifier(v::Validator)
    n = v.n
    k = v.i + 1
    digits = 0
    while k < n && isdigitbyte(byteat(v, k))
        k += 1
        digits += 1
    end
    digits == 0 && return false
    k < n && byteat(v, k) == UInt8('}') && return true
    if k < n && byteat(v, k) == UInt8(',')
        k += 1
        while k < n && isdigitbyte(byteat(v, k))
            k += 1
        end
        return k < n && byteat(v, k) == UInt8('}')
    end
    return false
end

# Reads a run of decimal digits, which saturates as the parser's does.
function number(v::Validator)
    start = v.i
    value = 0
    while v.i < v.n && isdigitbyte(byteat(v, v.i))
        value = min(value * 10 + Int(byteat(v, v.i) - UInt8('0')), MAX_COUNT)
        v.i += 1
    end
    start == v.i && invalid()
    return value
end

# Whether the decimal number at the bytes from `a` to before `aend` is greater than the one from `b` to before
# `bend`, for numbers of any length.
function greaternumber(v::Validator, a::Int, aend::Int, b::Int, bend::Int)
    while a < aend - 1 && byteat(v, a) == UInt8('0')
        a += 1
    end
    while b < bend - 1 && byteat(v, b) == UInt8('0')
        b += 1
    end
    aend - a == bend - b || return aend - a > bend - b
    for k in 0:(aend - a - 1)
        x = byteat(v, a + k)
        y = byteat(v, b + k)
        x == y || return x > y
    end
    return false
end

function quantifier(v::Validator)
    if isat(v, '*') || isat(v, '+') || isat(v, '?')
        v.i += 1
    elseif isat(v, '{') && lookslikequantifier(v)
        v.i += 1
        first = v.i
        number(v)
        firstend = v.i
        if eat(v, ',') && !isat(v, '}')
            second = v.i
            number(v)
            # The digits are compared, as the parser compares them.
            greaternumber(v, first, firstend, second, v.i) && invalid()
        end
        eat(v, '}') || invalid()
    else
        return false
    end
    eat(v, '?')
    return true
end

# A group or a lookaround. Returns whether it may be quantified.
function group(v::Validator)
    v.i += 1
    if !eat(v, '?') || eat(v, ':')
        body(v)
        return true
    end
    if isat(v, 'i') || isat(v, 'm') || isat(v, 's') || isat(v, '-')
        modifiers(v)
        body(v)
        return true
    end
    if eat(v, '=') || eat(v, '!')
        body(v)
        # Lookaheads are not quantifiable with the u flag.
        return false
    end
    if eat(v, '<')
        if eat(v, '=') || eat(v, '!')
            body(v)
            return false
        end
        start = v.i
        groupname(v)
        # Whether another group has the name, and may have it, is decided when the whole pattern is read (see
        # `namesvalid`).
        push!(v.names, start, v.i - 1, place(v))
        body(v)
        return true
    end
    invalid()
end

function body(v::Validator)
    disjunction(v)
    eat(v, ')') || invalid()
    return nothing
end

# The modifiers of a group such as (?i: or (?s-i:, up to and including the colon.
function modifiers(v::Validator)
    seen = 0
    removing = false
    while !eat(v, ':')
        v.i < v.n || invalid()
        c = byteat(v, v.i)
        v.i += 1
        if c == UInt8('-')
            removing && invalid()
            removing = true
            continue
        end
        flag = c == UInt8('i') ? 1 : c == UInt8('m') ? 2 : c == UInt8('s') ? 4 : 0
        (flag == 0 || (seen & flag) != 0) && invalid()
        seen |= flag
    end
    seen == 0 && invalid()
    return nothing
end

# The place of the current position: the innermost alternative that encloses it, with those that enclose that one.
# The disjunctions that have no place yet are the innermost ones, as a place is made with those of every
# disjunction around it and an alternative that starts has none.
function place(v::Validator)
    opened = v.opened
    enclosing = 0
    for level in 3:3:length(opened)
        if opened[level] == 0
            push!(v.places, opened[level - 2], opened[level - 1], enclosing)
            opened[level] = length(v.places) ÷ 3
        end
        enclosing = opened[level]
    end
    return enclosing
end

# Whether the position `at`, which is before a place, lies in a different alternative of some disjunction than the
# place does. The innermost disjunction around the place that started at or before `at` holds both. They are apart
# when the alternative of the place started after `at`.
function separatealternatives(v::Validator, at::Int, place::Int)
    places = v.places
    while place != 0
        places[3place - 2] <= at && return places[3place - 1] > at
        place = places[3place]
    end
    return false
end

# Whether the group names of the pattern may stand together, and every named reference names a group.
#
# Two groups may share a name only in separate alternatives of one disjunction. The names are put in order (by name,
# and those that are the same in the order they are written in), which takes a number of comparisons in proportion
# to n log n for n names, where looking for each name among those before it took n squared. Of the groups of one
# name only each and the one written before it are compared. That decides every pair: when a group is apart from
# the one before it, and that one from an earlier one, the group is apart from the earlier one too. (Were it not,
# one alternative would hold the group and the earlier one. It would hold the one between them, which is written
# between them. The one between is apart from the group, so it is in another alternative of a disjunction inside
# that alternative, around the group. The earlier one is outside that disjunction, or it would be apart from the
# group as well, so it and the one between are in the one alternative and not apart.)
function namesvalid(v::Validator)
    names = v.names
    count = length(names) ÷ 3
    count == 0 && return isempty(v.refs)
    order = v.order
    resize!(order, count)
    for k in 1:count
        order[k] = k - 1
    end
    count > 1 && sortnames!(v)
    for k in 2:count
        a = order[k - 1]
        b = order[k]
        if comparenames(v, names[3a + 1], names[3a + 2], names[3b + 1], names[3b + 2]) == 0
            separatealternatives(v, names[3a + 1], names[3b + 3]) || return false
        end
    end
    for r in 1:2:length(v.refs)
        hasname(v, v.refs[r], v.refs[r + 1]) || return false
    end
    return true
end

# Whether the group `a` goes before the group `b` in the order of the names.
function namebefore(v::Validator, a::Int, b::Int)
    names = v.names
    c = comparenames(v, names[3a + 1], names[3a + 2], names[3b + 1], names[3b + 2])
    return c < 0 || (c == 0 && a < b)
end

# Puts `order` in the order of the names. It is a heap sort, which moves the numbers where they are and takes a
# number of comparisons in proportion to n log n whatever the names are.
function sortnames!(v::Validator)
    order = v.order
    count = length(order)
    for root in (count ÷ 2):-1:1
        sift!(v, root, count)
    end
    for last in count:-1:2
        order[1], order[last] = order[last], order[1]
        sift!(v, 1, last - 1)
    end
    return nothing
end

function sift!(v::Validator, root::Int, last::Int)
    order = v.order
    moved = order[root]
    while true
        child = 2root
        child > last && break
        if child < last && namebefore(v, order[child], order[child + 1])
            child += 1
        end
        namebefore(v, moved, order[child]) || break
        order[root] = order[child]
        root = child
    end
    order[root] = moved
    return nothing
end

# Whether a group has the name written at p[start, stop), by halving the names in their order.
function hasname(v::Validator, start::Int, stop::Int)
    names = v.names
    order = v.order
    low = 1
    high = length(order)
    while low <= high
        middle = (low + high) >>> 1
        k = order[middle]
        c = comparenames(v, names[3k + 1], names[3k + 2], start, stop)
        c == 0 && return true
        if c < 0
            low = middle + 1
        else
            high = middle - 1
        end
    end
    return false
end

# A group name up to and including its '>'.
function groupname(v::Validator)
    first = true
    while true
        v.i >= v.n && invalid()
        if byteat(v, v.i) == UInt8('>')
            first && invalid()
            v.i += 1
            return nothing
        end
        c = namechar(v, v.i)
        v.i = v.next
        if first ? !isnamestart(c) : !isnamepart(c)
            invalid()
        end
        first = false
    end
end

function hexdigit(b::UInt8)
    if b >= UInt8('0') && b <= UInt8('9')
        return Int(b - UInt8('0'))
    elseif b >= UInt8('a') && b <= UInt8('f')
        return Int(b - UInt8('a')) + 10
    elseif b >= UInt8('A') && b <= UInt8('F')
        return Int(b - UInt8('A')) + 10
    end
    return -1
end

# The value of the four hexadecimal digits at `from`, or -1.
function hex4(v::Validator, from::Int)
    from + 4 > v.n && return -1
    value = 0
    for k in 0:3
        d = hexdigit(byteat(v, from + k))
        d < 0 && return -1
        value = value * 16 + d
    end
    return value
end

# The character of a group name at `from`, which may be written as a \u escape. Sets `next` to where it ends.
function namechar(v::Validator, from::Int)
    c = charat(v, from)
    c != Int32('\\') && return c
    n = v.n
    k = v.next
    (k >= n || byteat(v, k) != UInt8('u')) && invalid()
    k += 1
    if k < n && byteat(v, k) == UInt8('{')
        value = 0
        k += 1
        start = k
        while k < n && hexdigit(byteat(v, k)) >= 0
            if value <= MAX_CODE_POINT
                value = value * 16 + hexdigit(byteat(v, k))
            end
            k += 1
        end
        if k == start || k >= n || byteat(v, k) != UInt8('}') || value > MAX_CODE_POINT
            invalid()
        end
        v.next = k + 1
        return Int32(value)
    end
    u = hex4(v, k)
    u < 0 && invalid()
    k += 4
    # A surrogate pair written as two escapes is one code point.
    if u >= 0xD800 && u <= 0xDBFF && k + 6 <= n && byteat(v, k) == UInt8('\\') && byteat(v, k + 1) == UInt8('u')
        low = hex4(v, k + 2)
        if low >= 0xDC00 && low <= 0xDFFF
            v.next = k + 6
            return Int32(0x10000 + ((u - 0xD800) << 10) + (low - 0xDC00))
        end
    end
    v.next = k
    return Int32(u)
end

# How two valid group names of the pattern compare, however their characters are written: less than 0 when the one
# at p[a, astop) is the lesser, 0 when they are the same name, more than 0 otherwise. A name that another starts
# with is the lesser.
function comparenames(v::Validator, a::Int, astop::Int, b::Int, bstop::Int)
    while a < astop && b < bstop
        ca = namechar(v, a)
        a = v.next
        cb = namechar(v, b)
        b = v.next
        ca != cb && return ca < cb ? -1 : 1
    end
    return (a < astop) - (b < bstop)
end

# An escape outside a class. Returns whether it may be quantified.
function atomescape(v::Validator)
    v.i += 1
    v.i >= v.n && invalid()
    c = byteat(v, v.i)
    if c == UInt8('b') || c == UInt8('B')
        v.i += 1
        return false
    end
    if c == UInt8('k')
        v.i += 1
        eat(v, '<') || invalid()
        start = v.i
        groupname(v)
        push!(v.refs, start, v.i - 1)
        return true
    end
    if c >= UInt8('1') && c <= UInt8('9')
        number(v) > v.groupcount && invalid()
        return true
    end
    classescape(v, c) && return true
    characterescape(v, false)
    return true
end

# A class escape at the position, which it moves past. Returns false when there is none.
function classescape(v::Validator, c::UInt8)
    if c == UInt8('d') || c == UInt8('D') || c == UInt8('w') || c == UInt8('W') || c == UInt8('s') ||
       c == UInt8('S')
        v.i += 1
        return true
    elseif c == UInt8('p') || c == UInt8('P')
        v.i += 1
        propertyname(v)
        return true
    end
    return false
end

function propertyname(v::Validator)
    eat(v, '{') || invalid()
    start = v.i
    while v.i < v.n && byteat(v, v.i) != UInt8('}')
        v.i += 1
    end
    stop = v.i
    if !eat(v, '}') || !isproperty(codeunits(v.p), start + 1, stop)
        invalid()
    end
    return nothing
end

function hex(v::Validator, digits::Int)
    v.i + digits > v.n && return false
    for k in 0:(digits - 1)
        hexdigit(byteat(v, v.i + k)) < 0 && return false
    end
    v.i += digits
    return true
end

issyntaxbyte(c::Integer) =
    c == UInt8('^') || c == UInt8('$') || c == UInt8('\\') || c == UInt8('.') || c == UInt8('*') ||
    c == UInt8('+') || c == UInt8('?') || c == UInt8('(') || c == UInt8(')') || c == UInt8('[') ||
    c == UInt8(']') || c == UInt8('{') || c == UInt8('}') || c == UInt8('|') || c == UInt8('/')

# A character escape after the backslash at the position, which it moves past.
function characterescape(v::Validator, inclass::Bool)
    c = peekchar(v)
    v.i = v.next
    if c == Int32('f') || c == Int32('n') || c == Int32('r') || c == Int32('t') || c == Int32('v')
        return nothing
    elseif c == Int32('c')
        if v.i < v.n && isasciiletter(byteat(v, v.i))
            v.i += 1
            return nothing
        end
        invalid()
    elseif c == Int32('0')
        v.i < v.n && isdigitbyte(byteat(v, v.i)) && invalid()
        return nothing
    elseif c == Int32('x')
        hex(v, 2) || invalid()
        return nothing
    elseif c == Int32('u')
        if eat(v, '{')
            value = 0
            start = v.i
            while v.i < v.n && hexdigit(byteat(v, v.i)) >= 0
                if value <= MAX_CODE_POINT
                    value = value * 16 + hexdigit(byteat(v, v.i))
                end
                v.i += 1
            end
            if start == v.i || !eat(v, '}') || value > MAX_CODE_POINT
                invalid()
            end
            return nothing
        end
        hex(v, 4) || invalid()
        return nothing
    end
    # Identity escapes: with the u flag, only syntax characters and '/' (and '-' in a class).
    if issyntaxbyte(c) || (inclass && c == Int32('-'))
        return nothing
    end
    invalid()
end

function characterclass(v::Validator)
    v.i += 1
    eat(v, '^')
    while true
        v.i >= v.n && invalid()
        if byteat(v, v.i) == UInt8(']')
            v.i += 1
            return nothing
        end
        lo = classatom(v)
        if isat(v, '-') && v.i + 1 < v.n && byteat(v, v.i + 1) != UInt8(']')
            v.i += 1
            hi = classatom(v)
            if lo < 0 || hi < 0 || hi < lo
                invalid()
            end
        end
    end
end

# A class atom: its code point, or -1 for a class escape.
function classatom(v::Validator)
    c = peekchar(v)
    if c == Int32('\\')
        v.i += 1
        v.i >= v.n && invalid()
        e = byteat(v, v.i)
        if e == UInt8('b')
            v.i += 1
            return Int32('\b')
        end
        if e == UInt8('-')
            v.i += 1
            return Int32('-')
        end
        classescape(v, e) && return Int32(-1)
        from = v.i
        characterescape(v, true)
        return escapedvalue(v, from)
    end
    v.i = v.next
    return c
end

# The code point of the character escape that starts at `from` (already validated).
function escapedvalue(v::Validator, from::Int)
    c = charat(v, from)
    if c == Int32('f')
        return Int32('\f')
    elseif c == Int32('n')
        return Int32('\n')
    elseif c == Int32('r')
        return Int32('\r')
    elseif c == Int32('t')
        return Int32('\t')
    elseif c == Int32('v')
        return Int32(0x0b)
    elseif c == Int32('c')
        return Int32(byteat(v, from + 1) % 32)
    elseif c == Int32('0')
        return Int32(0)
    elseif c == Int32('x')
        return hexrange(v, from + 1, from + 3)
    elseif c == Int32('u')
        if byteat(v, from + 1) == UInt8('{')
            return hexrange(v, from + 2, v.i - 1)
        end
        u = hexrange(v, from + 1, from + 5)
        # A surrogate pair written as two escapes is one code point.
        i = v.i
        if u >= 0xD800 && u <= 0xDBFF && i + 6 <= v.n && byteat(v, i) == UInt8('\\') && byteat(v, i + 1) == UInt8('u')
            low = hex4(v, i + 2)
            if low >= 0xDC00 && low <= 0xDFFF
                v.i += 6
                return Int32(0x10000 + ((u - 0xD800) << 10) + (low - 0xDC00))
            end
        end
        return u
    end
    return c
end

# The value of the hexadecimal digits of p[from, to), which saturates above the last code point.
function hexrange(v::Validator, from::Int, to::Int)
    value = 0
    for k in from:(to - 1)
        if value <= MAX_CODE_POINT
            value = value * 16 + hexdigit(byteat(v, k))
        end
    end
    return Int32(min(value, Int(MAX_CODE_POINT) + 1))
end
