# pattern and patternProperties matching with ECMA-262 semantics (the u flag), as JSON Schema specifies. Ported from
# pattern.go.
#
# A pattern gets the cheapest matcher that decides it exactly:
#
#   - patterns every string matches ("", ".*", "[\s\S]*" and the like), ".+", and line lengths ("^.{1,256}$")
#   - anchored sequences of quantified ASCII character classes and literals ("^[a-z][a-z0-9_]{0,29}$", "^x-",
#     "^[@$_#]"), matched in one pass over the string
#   - alternatives of such sequences once groups are multiplied out ("^([a|A]uto)|([n|N]one)$"), sets of literals,
#     and separated lists ("^([a-z]+)(\.[a-z]+)*$")
#   - anything else by the regular expression engine (see pattern_engine.jl).
#
# Patterns compile once per process (a pattern is immutable, so identical patterns share one matcher).

# Matches every string.
const MATCH_EVERYTHING = 0x00
# "^literal" (with "$": the whole string).
const MATCH_LITERAL = 0x01
const MATCH_SEQUENCE = 0x02
const MATCH_SEPARATED_LIST = 0x03
# ".+" ("^.+" with start): some (the first) character is not a line terminator.
const MATCH_HAS_CONTENT = 0x04
# "^.{min,max}$": between min and max characters, none a line terminator.
const MATCH_LINE = 0x05
# "^(a|b|...)$": one of a set of strings.
const MATCH_LITERALS = 0x06
# Top-level alternatives of literals or class sequences, each optionally anchored.
const MATCH_ALTERNATIVES = 0x07
# "^(?=[^SET]+$)(?=(.*\w)).+$": a non-empty line without a character of the set, containing a word character. With
# bangs ("^(?=!+[^SET]+$)..."), that line follows one or more "!".
const MATCH_EXCLUDED_CLASS_WITH_WORD = 0x08
const MATCH_ENGINE = 0x09

# ----------------------------------------------------------------------------------------------------------------------
# Class sequences

# A set of characters: ASCII by bit mask, then either all or none of the line separators U+2028 and U+2029, and
# either all or none of the other non-ASCII characters.
struct CharSet
    lo::UInt64
    hi::UInt64
    non_ascii::Bool
    separators::Bool
end

CharSet() = CharSet(0, 0, false, false)

# ASCII lo to hi inclusive (hi below 128).
function char_range(lo::UInt8, hi::UInt8)
    a, b = UInt64(0), UInt64(0)
    for c in Int(lo):Int(hi)
        if c < 64
            a |= UInt64(1) << c
        else
            b |= UInt64(1) << (c - 64)
        end
    end
    return CharSet(a, b, false, false)
end

char_single(c::Char) = char_range(UInt8(c), UInt8(c))

# ECMA-262's ".": everything but the line terminators.
function dot_set()
    lo = typemax(UInt64) & ~((UInt64(1) << UInt8('\n')) | (UInt64(1) << UInt8('\r')))
    return CharSet(lo, typemax(UInt64), true, false)
end

set_union(a::CharSet, b::CharSet) =
    CharSet(a.lo | b.lo, a.hi | b.hi, a.non_ascii || b.non_ascii, a.separators || b.separators)

set_negate(s::CharSet) = CharSet(~s.lo, ~s.hi, !s.non_ascii, !s.separators)

set_disjoint(a::CharSet, b::CharSet) =
    (a.lo & b.lo) == 0 && (a.hi & b.hi) == 0 && !(a.non_ascii && b.non_ascii) && !(a.separators && b.separators)

# Reports whether the set holds an ASCII character.
@inline has_ascii(s::CharSet, c::UInt8) = (((c & 0x40) == 0 ? s.lo : s.hi) >> (c & 0x3f)) & 1 != 0

@inline function set_contains(s::CharSet, c::UInt32)
    c < 0x80 && return has_ascii(s, c % UInt8)
    (c == 0x2028 || c == 0x2029) && return s.separators
    return s.non_ascii
end

# The set's character when it holds exactly one ASCII character and nothing else, or -1.
function set_single(s::CharSet)
    (s.non_ascii || s.separators || count_ones(s.lo) + count_ones(s.hi) != 1) && return -1
    return s.lo != 0 ? trailing_zeros(s.lo) : 64 + trailing_zeros(s.hi)
end

const DIGIT_SET = char_range(UInt8('0'), UInt8('9'))
# "\w".
const WORD_SET = set_union(set_union(char_range(UInt8('0'), UInt8('9')), char_range(UInt8('A'), UInt8('Z'))),
    set_union(char_range(UInt8('a'), UInt8('z')), char_single('_')))

struct SequenceItem
    set::CharSet
    min::UInt32
    # typemax(UInt32): unbounded.
    max::UInt32
end

# "^" then quantified character sets, optionally "$". Matched greedily, which is exact because every variable item's
# set is disjoint from the next item's (a character the item leaves cannot be taken by the next one either). Where it
# is not, a sequence anchored at both ends with a single variable item is still decided in one pass: the string's
# length fixes how many characters that item takes (pinned).
struct Sequence
    items::Vector{SequenceItem}
    to_end::Bool
    # The index of the one variable item of a sequence matched by length, or -1 for greedy matching.
    pinned::Int
    # For a pinned sequence, the characters the other items take.
    fixed_width::Int
end

const NO_SEQUENCE = Sequence(SequenceItem[], false, -1, 0)

greedy_sequence(items::Vector{SequenceItem}) = Sequence(items, false, -1, 0)

# Reads the character at zero-based i (a byte for ASCII). The text of a document is valid UTF-8. A byte that starts
# no character is read as U+FFFD.
@inline function decode_rune(s::Bytes, i::Int)
    c = at(s, i)
    c < 0x80 && return UInt32(c), 1
    return decode_rune_wide(s, i, c)
end

function decode_rune_wide(s::Bytes, i::Int, c::UInt8)
    n = s.len
    if c >= 0xc2 && c < 0xe0 && i + 1 < n
        return (UInt32(c & 0x1f) << 6) | UInt32(at(s, i + 1) & 0x3f), 2
    elseif c >= 0xe0 && c < 0xf0 && i + 2 < n
        return (UInt32(c & 0x0f) << 12) | (UInt32(at(s, i + 1) & 0x3f) << 6) | UInt32(at(s, i + 2) & 0x3f), 3
    elseif c >= 0xf0 && c < 0xf5 && i + 3 < n
        return (UInt32(c & 0x07) << 18) | (UInt32(at(s, i + 1) & 0x3f) << 12) |
               (UInt32(at(s, i + 2) & 0x3f) << 6) | UInt32(at(s, i + 3) & 0x3f), 4
    end
    return UInt32(0xfffd), 1
end

function is_ascii(s::Bytes)
    for i in 0:s.len-1
        at(s, i) >= 0x80 && return false
    end
    return true
end

is_ascii(s::String) = all(<(0x80), codeunits(s))

# The number of characters of UTF-8 text.
function rune_count(s::Bytes)
    n = 0
    for i in 0:s.len-1
        n += (at(s, i) & 0xc0) != 0x80
    end
    return n
end

# An ECMA-262 LineTerminator, which "." does not match.
@inline is_line_terminator(c::UInt32) = c == 0x0a || c == 0x0d || c == 0x2028 || c == 0x2029

# The number of characters, when none is a line terminator, else -1.
function line_length(s::Bytes)
    n = 0
    i = 0
    while i < s.len
        c, size = decode_rune(s, i)
        is_line_terminator(c) && return -1
        n += 1
        i += size
    end
    return n
end

# The text, when every item is one fixed character, or nothing.
function sequence_literal(q::Sequence)
    text = UInt8[]
    for item in q.items
        c = set_single(item.set)
        (c < 0 || item.min != 1 || item.max != 1) && return nothing
        push!(text, c % UInt8)
    end
    return text
end

# Matches a pinned sequence: every item but one takes a fixed number of characters, so the string's length says how
# many the variable one takes.
function match_pinned(q::Sequence, s::Bytes, ascii::Bool)
    len = ascii ? s.len : rune_count(s)
    variable = len - q.fixed_width
    v = q.items[q.pinned+1]
    (variable < 0 || UInt64(variable) < UInt64(v.min) || UInt64(variable) > UInt64(v.max)) && return false
    pos = 0
    items = q.items
    for i in eachindex(items)
        item = items[i]
        n = i - 1 == q.pinned ? variable : Int(item.min)
        while n > 0
            c, size = decode_rune(s, pos)
            set_contains(item.set, c) || return false
            pos += size
            n -= 1
        end
    end
    return true
end

# Matches over any text, a character at a time.
function match_chars(q::Sequence, s::Bytes)
    q.pinned >= 0 && return match_pinned(q, s, false)
    pos = consume(q, s)
    return pos >= 0 && (!q.to_end || pos == s.len)
end

# Matches over ASCII text, a byte per character.
function match_ascii(q::Sequence, s::Bytes)
    q.pinned >= 0 && return match_pinned(q, s, true)
    pos = 0
    n = s.len
    items = q.items
    for i in eachindex(items)
        item = items[i]
        start = pos
        stop = UInt64(item.max) < UInt64(n - start) ? start + Int(item.max) : n
        set = item.set
        while pos < stop && has_ascii(set, at(s, pos) & 0x7f)
            pos += 1
        end
        pos - start < Int(item.min) && return false
    end
    return !q.to_end || pos == n
end

match_sequence(q::Sequence, s::Bytes) = is_ascii(s) ? match_ascii(q, s) : match_chars(q, s)

# Greedily matches the items at the start of s (ignoring to_end): the bytes taken, or -1.
function consume(q::Sequence, s::Bytes)
    pos = 0
    items = q.items
    for i in eachindex(items)
        item = items[i]
        n = UInt32(0)
        while n < item.max && pos < s.len
            c, size = decode_rune(s, pos)
            set_contains(item.set, c) || break
            pos += size
            n += UInt32(1)
        end
        n < item.min && return -1
    end
    return pos
end

# Reports whether greedy matching is exact when next can follow the items: every variable item is disjoint from the
# items that can directly follow it (up to the first that cannot match nothing), next included.
function greedy_before(items::Vector{SequenceItem}, next::CharSet)
    for i in eachindex(items)
        item = items[i]
        item.min == item.max && continue
        settled = false
        for j in i+1:length(items)
            following = items[j]
            set_disjoint(item.set, following.set) || return false
            if following.min > 0
                settled = true
                break
            end
        end
        settled && continue
        set_disjoint(item.set, next) || return false
    end
    return true
end

# A list of items between separators: "^I(SR)*$" or "^I(SR)+$" (no final), or "^(RS)*F$" and "^(RS)+F$". The
# separator is a fixed sequence and no variable class can run into what follows it, so greedy matching splits the
# string exactly where the pattern does.
struct SeparatedList
    # I of the first form.
    has_first::Bool
    first::Sequence
    repeated::Sequence
    separator::Sequence
    # F of the second form, matched against the whole remainder.
    final::Sequence
    min_repeats::UInt32
end

function match_list(l::SeparatedList, s::Bytes)
    rest = s
    repeats = UInt32(0)
    if l.has_first
        n = consume(l.first, rest)
        n < 0 && return false
        rest = sub(rest, n)
        while true
            rest.len == 0 && return repeats >= l.min_repeats
            n = consume(l.separator, rest)
            n < 0 && return false
            rest = sub(rest, n)
            n = consume(l.repeated, rest)
            n < 0 && return false
            rest = sub(rest, n)
            repeats += UInt32(1)
        end
    end
    while true
        if repeats >= l.min_repeats && match_sequence(l.final, rest)
            return true
        end
        n = consume(l.repeated, rest)
        n < 0 && return false
        m = consume(l.separator, sub(rest, n))
        m < 0 && return false
        rest = sub(rest, n + m)
        repeats += UInt32(1)
    end
end

const ALT_LITERAL = 0x00
# "^sequence" (with "$" when the sequence runs to the end).
const ALT_START = 0x01
# "sequence$" of width characters: matched over the string's last width characters.
const ALT_END = 0x02
# An unanchored sequence of width characters: matched at each position.
const ALT_ANYWHERE = 0x03

# One alternative of MATCH_ALTERNATIVES: a literal anchored at either end (or neither), a class sequence anchored at
# the start, or a class sequence of fixed width anchored at the end or not at all.
struct Alternative
    kind::UInt8
    # ALT_LITERAL.
    text::Vector{UInt8}
    start::Bool
    stop::Bool
    seq::Sequence
    # ALT_END, ALT_ANYWHERE: the sequence's width in characters.
    width::Int
end

function has_prefix(s::Bytes, text::Vector{UInt8})
    n = length(text)
    return s.len >= n && bytes_equal(Bytes(s.b, s.off, n), text)
end

function has_suffix(s::Bytes, text::Vector{UInt8})
    n = length(text)
    return s.len >= n && bytes_equal(Bytes(s.b, s.off + s.len - n, n), text)
end

# The zero-based index of the first occurrence of text in s, or -1.
function index_text(s::Bytes, text::Vector{UInt8})
    n = length(text)
    i = 0
    while i + n <= s.len
        bytes_equal(Bytes(s.b, s.off + i, n), text) && return i
        i += 1
    end
    return -1
end

# Reports whether the alternative matches s, ascii saying whether s is ASCII.
function match_alternative(a::Alternative, s::Bytes, ascii::Bool)
    if a.kind == ALT_LITERAL
        if a.start && a.stop
            return bytes_equal(s, a.text)
        elseif a.start
            return has_prefix(s, a.text)
        elseif a.stop
            return has_suffix(s, a.text)
        end
        return index_text(s, a.text) >= 0
    elseif a.kind == ALT_START
        return ascii ? match_ascii(a.seq, s) : match_chars(a.seq, s)
    elseif a.kind == ALT_END
        if ascii
            return s.len >= a.width && match_ascii(a.seq, sub(s, s.len - a.width))
        end
        skip = rune_count(s) - a.width
        skip < 0 && return false
        pos = 0
        while skip > 0
            _, size = decode_rune(s, pos)
            pos += size
            skip -= 1
        end
        return match_chars(a.seq, sub(s, pos))
    end
    if ascii
        pos = 0
        while pos + a.width <= s.len
            match_ascii(a.seq, sub(s, pos)) && return true
            pos += 1
        end
        return false
    end
    positions = rune_count(s) - a.width + 1
    pos = 0
    while positions > 0
        match_chars(a.seq, sub(s, pos)) && return true
        _, size = decode_rune(s, pos)
        pos += size
        positions -= 1
    end
    return false
end

# A compiled pattern.
struct Pattern
    source::String
    kind::UInt8
    # MATCH_LITERAL: the text, and whether it is the whole string.
    text::Vector{UInt8}
    whole::Bool
    # MATCH_HAS_CONTENT: anchored at the start. MATCH_EXCLUDED_CLASS_WITH_WORD: bangs.
    flag::Bool
    # MATCH_LINE.
    min::UInt32
    max::UInt32
    seq::Sequence
    list::Union{Nothing,SeparatedList}
    few::Vector{Vector{UInt8}}
    many::Union{Nothing,Names}
    alts::Vector{Alternative}
    set::CharSet
    engine::Union{Nothing,EnginePattern}
end

function Pattern(kind::UInt8; source::String="", text::Vector{UInt8}=EMPTY_BYTES, whole::Bool=false,
    flag::Bool=false, min::UInt32=UInt32(0), max::UInt32=UInt32(0), seq::Sequence=NO_SEQUENCE,
    list::Union{Nothing,SeparatedList}=nothing, few::Vector{Vector{UInt8}}=Vector{UInt8}[],
    many::Union{Nothing,Names}=nothing, alts::Vector{Alternative}=Alternative[], set::CharSet=CharSet(),
    engine::Union{Nothing,EnginePattern}=nothing)
    return Pattern(source, kind, text, whole, flag, min, max, seq, list, few, many, alts, set, engine)
end

with_source(p::Pattern, source::String) = Pattern(source, p.kind, p.text, p.whole, p.flag, p.min, p.max, p.seq,
    p.list, p.few, p.many, p.alts, p.set, p.engine)

# match for a string that is not known to be ASCII.
function match_string(p::Pattern, s::String)
    b = Bytes(Vector{UInt8}(codeunits(s)))
    return pattern_match(p, b, is_ascii(b))
end

# Reports whether the pattern matches somewhere in s, ascii saying whether s is known to be ASCII (a document knows
# that of its strings).
function pattern_match(p::Pattern, s::Bytes, ascii::Bool)::Bool
    k = p.kind
    if k == MATCH_EVERYTHING
        return true
    elseif k == MATCH_LITERAL
        return p.whole ? bytes_equal(s, p.text) : has_prefix(s, p.text)
    elseif k == MATCH_SEQUENCE
        return ascii ? match_ascii(p.seq, s) : match_chars(p.seq, s)
    elseif k == MATCH_SEPARATED_LIST
        return match_list(p.list::SeparatedList, s)
    elseif k == MATCH_HAS_CONTENT
        if p.flag
            s.len == 0 && return false
            c, _ = decode_rune(s, 0)
            return !is_line_terminator(c)
        end
        i = 0
        while i < s.len
            c, size = decode_rune(s, i)
            is_line_terminator(c) || return true
            i += size
        end
        return false
    elseif k == MATCH_LINE
        ascii && UInt64(s.len) < UInt64(p.min) && return false
        n = line_length(s)
        return n >= 0 && UInt64(n) >= UInt64(p.min) && UInt64(n) <= UInt64(p.max)
    elseif k == MATCH_LITERALS
        many = p.many
        many === nothing || return find(many, s) >= 0
        for t in p.few
            bytes_equal(s, t) && return true
        end
        return false
    elseif k == MATCH_ALTERNATIVES
        for a in p.alts
            match_alternative(a, s, ascii) && return true
        end
        return false
    elseif k == MATCH_EXCLUDED_CLASS_WITH_WORD
        if p.flag
            skip = 0
            while skip < s.len && at(s, skip) == UInt8('!')
                skip += 1
            end
            (skip == 0 || skip == s.len) && return false
            s = sub(s, skip)
        end
        word = false
        i = 0
        while i < s.len
            c, size = decode_rune(s, i)
            (set_contains(p.set, c) || is_line_terminator(c)) && return false
            word = word || set_contains(WORD_SET, c)
            i += size
        end
        return word
    end
    return engine_match(p.engine::EnginePattern, s)
end

# ----------------------------------------------------------------------------------------------------------------------
# Compilation. Pattern text is handled as bytes at zero-based offsets, as in the Go source.

blen(s::String) = ncodeunits(s)
bat(s::String, i::Int) = codeunit(s, i + 1)
# The bytes from..to (zero-based, to exclusive) as text.
bsub(s::String, from::Int, to::Int) = from >= to ? "" : String(codeunits(s)[from+1:to])
bsub(s::String, from::Int) = bsub(s, from, blen(s))

function cut_prefix(s::String, prefix::String)
    startswith(s, prefix) || return s, false
    return bsub(s, blen(prefix)), true
end

function cut_suffix(s::String, suffix::String)
    endswith(s, suffix) || return s, false
    return bsub(s, 0, blen(s) - blen(suffix)), true
end

const PATTERN_CACHE = Dict{String,Pattern}()
const PATTERN_CACHE_LOCK = ReentrantLock()

# Compiles (or fetches from the process-wide cache) a pattern, or returns nothing when it is not a valid ECMA-262
# regular expression.
function compile_pattern(source::String)
    cached = lock(() -> get(PATTERN_CACHE, source, nothing), PATTERN_CACHE_LOCK)
    cached === nothing || return cached
    # Validity is ECMA-262's: a pattern the engine rejects is an error, whichever matcher would run it. A pattern
    # with a faster matcher that is valid with the u flag needs no engine at all. The engine reads a pattern that is
    # valid only without the u flag by code point too, so the faster matchers decide those the same way.
    p = choose_pattern(source)
    if p === nothing || !valid_regex(source)
        engine = compile_engine(source)
        engine === nothing && return nothing
        if p === nothing
            p = Pattern(MATCH_ENGINE; engine=engine)
        end
    end
    p = with_source(p, source)
    return lock(() -> get!(PATTERN_CACHE, source, p), PATTERN_CACHE_LOCK)
end

function choose_pattern(p::String)::Union{Nothing,Pattern}
    # Unanchored (or start-anchored) ".*" finds an empty match in any string. "^.*$" does not ("." stops at a line
    # terminator), so it is not listed.
    if p in ("", ".*", "^.*", ".*\$", "(.*)", "^(.*)", "[\\s\\S]*", "^[\\s\\S]*", "^[\\s\\S]*\$")
        return Pattern(MATCH_EVERYTHING)
    end
    p in (".+", ".", "(.+)") && return Pattern(MATCH_HAS_CONTENT)
    p in ("^.+", "^.") && return Pattern(MATCH_HAS_CONTENT; flag=true)
    # "^X.*" (no "$") matches exactly where "^X" does: ".*" can match nothing.
    rest, ok = cut_suffix(p, ".*")
    if ok && startswith(rest, "^") && blen(rest) > 1 && !ends_with_escape(rest) &&
       !(bat(rest, blen(rest) - 1) in codeunits("*+?}|(^"))
        m = choose_pattern(rest)
        m === nothing || return m
    end
    range = line_range(p)
    range === nothing || return Pattern(MATCH_LINE; min=range[1], max=range[2])
    texts = whole_alternatives(p)
    if texts !== nothing
        if length(texts) <= 8
            return Pattern(MATCH_LITERALS; few=[Vector{UInt8}(codeunits(t)) for t in texts])
        end
        return Pattern(MATCH_LITERALS; many=Names(unique(texts)))
    end
    alts = parse_alternatives(p)
    alts === nothing || return Pattern(MATCH_ALTERNATIVES; alts=alts)
    list = parse_separated_list(p)
    list === nothing || return Pattern(MATCH_SEPARATED_LIST; list=list)
    excluded = excluded_class_with_word(p)
    if excluded !== nothing
        return Pattern(MATCH_EXCLUDED_CLASS_WITH_WORD; set=excluded[1], flag=excluded[2])
    end
    seq = parse_sequence(p)
    if seq !== nothing
        text = sequence_literal(seq)
        text === nothing || return Pattern(MATCH_LITERAL; text=text, whole=seq.to_end)
        return Pattern(MATCH_SEQUENCE; seq=seq)
    end
    return nothing
end

# Reads "^.{m,n}$" (and "^.*$", "^.+$", "^.{m}$", "^.{m,}$", each also as "^(....)$"): the bounds on the length of a
# line, or nothing.
function line_range(p::String)
    q, ok = cut_prefix(p, "^.")
    grouped = false
    if !ok
        q, ok = cut_prefix(p, "^(.")
        ok || return nothing
        grouped = true
    end
    q, ok = cut_suffix(q, "\$")
    ok || return nothing
    if grouped
        q, ok = cut_suffix(q, ")")
        ok || return nothing
    end
    quantifier = parse_quantifier(q, 0)
    quantifier === nothing && return nothing
    lo, hi, next = quantifier
    (next != blen(q) || next == 0 || endswith(q, "?")) && return nothing
    return lo, hi
end

# Reads literal text: characters other than syntax characters, and identity or control escapes. Nothing otherwise.
function literal_text(p::String)
    out = UInt8[]
    n = blen(p)
    i = 0
    while i < n
        c = bat(p, i)
        if c == UInt8('\\')
            i += 1
            i >= n && return nothing
            e = bat(p, i)
            if e == UInt8('n')
                push!(out, UInt8('\n'))
            elseif e == UInt8('r')
                push!(out, UInt8('\r'))
            elseif e == UInt8('t')
                push!(out, UInt8('\t'))
            elseif e == UInt8('f')
                push!(out, 0x0c)
            elseif e == UInt8('v')
                push!(out, 0x0b)
            elseif e in codeunits("^\$\\.*+?()[]{}|/-")
                push!(out, e)
            else
                return nothing
            end
        elseif c in codeunits("^\$.*+?()[]{}|")
            return nothing
        else
            push!(out, c)
        end
        i += 1
    end
    return String(out)
end

# Splits at top-level "|"s (none inside a group, a class or after "\"). Nothing for an unmatched ")".
function split_alternatives(p::String)
    depth, in_class, start = 0, false, 0
    parts = String[]
    n = blen(p)
    i = 0
    while i < n
        c = bat(p, i)
        if c == UInt8('\\')
            i += 1
        elseif c == UInt8('[')
            in_class = true
        elseif c == UInt8(']')
            in_class = false
        elseif c == UInt8('(') && !in_class
            depth += 1
        elseif c == UInt8(')') && !in_class
            depth == 0 && return nothing
            depth -= 1
        elseif c == UInt8('|') && !in_class && depth == 0
            push!(parts, bsub(p, start, i))
            start = i + 1
        end
        i += 1
    end
    push!(parts, bsub(p, min(start, n)))
    return parts
end

# Reads "^(a|b|...)$" or "^(?:a|b|...)$" over literal alternatives.
function whole_alternatives(p::String)
    inner, ok = cut_prefix(p, "^(")
    ok || return nothing
    inner, ok = cut_suffix(inner, ")\$")
    ok || return nothing
    inner, _ = cut_prefix(inner, "?:")
    startswith(inner, "?") && return nothing
    parts = split_alternatives(inner)
    (parts === nothing || length(parts) < 2) && return nothing
    texts = String[]
    for part in parts
        t = literal_text(part)
        t === nothing && return nothing
        push!(texts, t)
    end
    return texts
end

# At most this many alternatives after expanding groups.
const MAX_ALTERNATIVES = 64

# At most this many alternatives when any is a class sequence: beyond it, trying each in turn is slower than one pass
# of the engine.
const MAX_SEQUENCE_ALTERNATIVES = 4

# Reads two or more alternatives once groups of alternatives (and optional groups) are expanded into whole
# alternatives ("^a|b|c$", "^([a|A]uto)|([n|N]one)$", "^[Ee][Ss]2015(\.([Cc]ore|[Pp]roxy))?$"), each a literal or a
# class sequence anchored where its own "^" and "$" say.
function parse_alternatives(p::String)
    c = collect(p)
    expanded = expand_alternatives(c, 0)
    expanded === nothing && return nothing
    parts, stop = expanded
    # A single alternative is only new here when a group was expanded (parse_sequence takes the rest).
    if stop != length(c) || isempty(parts) || (length(parts) == 1 && parts[1] == p)
        return nothing
    end
    alts = Alternative[]
    sequences = false
    for part in parts
        body, start = cut_prefix(part, "^")
        at_end = false
        r, ok = cut_suffix(body, "\$")
        if ok && !ends_with_escape(r)
            body, at_end = r, true
        end
        text = literal_text(body)
        if text !== nothing
            push!(alts, Alternative(ALT_LITERAL, Vector{UInt8}(codeunits(text)), start, at_end, NO_SEQUENCE, 0))
            continue
        end
        source = "^" * body * (at_end ? "\$" : "")
        seq = parse_sequence(source)
        seq === nothing && return nothing
        sequences = true
        if start
            push!(alts, Alternative(ALT_START, EMPTY_BYTES, false, false, seq, 0))
            continue
        end
        width = 0
        for item in seq.items
            item.min != item.max && return nothing
            width += Int(item.min)
        end
        push!(alts, Alternative(at_end ? ALT_END : ALT_ANYWHERE, EMPTY_BYTES, false, false, seq, width))
    end
    sequences && length(alts) > MAX_SEQUENCE_ALTERNATIVES && return nothing
    return alts
end

# Reports whether the text ends in an unpaired "\" (so a "$" after it would be escaped).
function ends_with_escape(t::String)
    n = 0
    i = blen(t) - 1
    while i >= 0 && bat(t, i) == UInt8('\\')
        n += 1
        i -= 1
    end
    return isodd(n)
end

rune_at(c::Vector{Char}, i::Int) = i < length(c) ? c[i+1] : '\0'
rune_text(c::Vector{Char}, from::Int, to::Int) = String(c[from+1:to])

# Every concatenation of one of a and one of b, or nothing when there are too many.
function product(a::Vector{String}, b::Vector{String})
    length(a) * length(b) > MAX_ALTERNATIVES && return nothing
    out = String[]
    for x in a, y in b
        push!(out, x * y)
    end
    return out
end

# Expands the alternatives from i up to an unmatched ")" or the end: groups of alternatives multiply out, a group
# quantified by "?" also contributes the empty alternative, and everything else is copied. Nothing for lookarounds,
# other quantified groups, or too many alternatives.
function expand_alternatives(c::Vector{Char}, i::Int)
    all = String[]
    branch = [""]
    n = length(c)
    while i < n
        ch = c[i+1]
        if ch == '|'
            append!(all, branch)
            branch = [""]
            i += 1
        elseif ch == ')'
            break
        elseif ch == '('
            i += 1
            if i < n && rune_at(c, i) == '?'
                (i + 1 < n && rune_at(c, i + 1) == ':') || return nothing
                i += 2
            end
            expanded = expand_alternatives(c, i)
            expanded === nothing && return nothing
            inner, next = expanded
            (next < n && rune_at(c, next) == ')') || return nothing
            i = next + 1
            q = i < n ? c[i+1] : '\0'
            if i < n && q == '?'
                inner = vcat(inner, [""])
                i += 1
                if i < n && rune_at(c, i) == '?'
                    i += 1
                end
            elseif i < n && q == '{'
                # An exact count repeats the group. Any other bound needs a real regular expression.
                close = i
                while close < n && c[close+1] != '}'
                    close += 1
                end
                close == n && return nothing
                count = parse_count(rune_text(c, i + 1, close))
                (count === nothing || count > 16) && return nothing
                repeated = [""]
                for _ in 1:count
                    repeated = product(repeated, inner)
                    repeated === nothing && return nothing
                end
                inner = repeated
                i = close + 1
                if i < n && rune_at(c, i) == '?'
                    i += 1
                end
            elseif i < n && (q == '*' || q == '+')
                return nothing
            end
            branch = product(branch, inner)
            branch === nothing && return nothing
        elseif ch == '['
            start = i
            i += 1
            if i < n && rune_at(c, i) == '^'
                i += 1
            end
            if i < n && rune_at(c, i) == ']'
                i += 1
            end
            while true
                i >= n && return nothing
                c[i+1] == ']' && break
                if c[i+1] == '\\'
                    i += 1
                end
                i += 1
            end
            i += 1
            text = rune_text(c, start, i)
            branch = [b * text for b in branch]
        elseif ch == '\\'
            i + 2 > n && return nothing
            text = rune_text(c, i, i + 2)
            branch = [b * text for b in branch]
            i += 2
        else
            branch = [b * string(ch) for b in branch]
            i += 1
        end
        length(all) + length(branch) > MAX_ALTERNATIVES && return nothing
    end
    append!(all, branch)
    return all, i
end

# Reads a decimal count that fits 32 bits, or nothing.
function parse_count(s::String)
    (s == "" || blen(s) > 10) && return nothing
    n = UInt64(0)
    for b in codeunits(s)
        is_ascii_digit(b) || return nothing
        n = n * 10 + UInt64(b - UInt8('0'))
    end
    return n <= typemax(UInt32) ? UInt32(n) : nothing
end

# Reads "^(?=[^SET]+$)(?=(.*\w)).+$" (with "(?:" or "(" around ".*\w"): the excluded set and whether it follows
# bangs. Nothing otherwise.
function excluded_class_with_word(p::String)
    # parse_class reads a class body from after "[", here "^SET]".
    body, bangs = cut_prefix(p, "^(?=!+[")
    if !bangs
        body, ok = cut_prefix(p, "^(?=[")
        ok || return nothing
    end
    startswith(body, "^") || return nothing
    class = parse_class(body, 0)
    class === nothing && return nothing
    negated, next = class
    excluded = CharSet(~negated.lo, ~negated.hi, false, false)
    # The run of "!" ends exactly where the class starts only when the class excludes "!".
    bangs && !has_ascii(excluded, UInt8('!')) && return nothing
    tail = bsub(body, next)
    if tail in ("+\$)(?=(.*\\w)).+\$", "+\$)(?=(?:.*\\w)).+\$", "+\$)(?=.*\\w).+\$")
        return excluded, bangs
    end
    return nothing
end

function parse_sequence(p::String)
    (blen(p) == 0 || bat(p, 0) != UInt8('^') || !is_ascii(p)) && return nothing
    n = blen(p)
    i = 1
    items = SequenceItem[]
    to_end = false
    while i < n
        c = bat(p, i)
        set = CharSet()
        if c == UInt8('$')
            i != n - 1 && return nothing
            to_end = true
            i += 1
            continue
        elseif c == UInt8('[')
            class = parse_class(p, i + 1)
            class === nothing && return nothing
            set, i = class
        elseif c == UInt8('\\')
            i + 1 >= n && return nothing
            escape = class_escape(bat(p, i + 1))
            escape === nothing && return nothing
            set = escape
            i += 2
        elseif c == UInt8('.')
            i += 1
            set = dot_set()
        elseif c in codeunits("()|^*+?{}]")
            return nothing
        else
            i += 1
            set = char_range(c, c)
        end
        quantifier = parse_quantifier(p, i)
        quantifier === nothing && return nothing
        lo, hi, i = quantifier
        push!(items, SequenceItem(set, lo, hi))
    end
    greedy_before(items, CharSet()) && return Sequence(items, to_end, -1, 0)
    # Greedy matching is not exact. With both ends anchored and one variable item, the length decides.
    to_end || return nothing
    pinned, fixed_width = -1, UInt64(0)
    for (k, item) in enumerate(items)
        if item.min == item.max
            fixed_width += UInt64(item.min)
        elseif pinned >= 0
            return nothing
        else
            pinned = k - 1
        end
    end
    fixed_width > typemax(Int32) && return nothing
    return Sequence(items, true, pinned, Int(fixed_width))
end

function parse_separated_list(p::String)
    body, ok = cut_prefix(p, "^")
    ok || return nothing
    body, ok = cut_suffix(body, "\$")
    (!ok || ends_with_escape(body)) && return nothing
    # Top-level groups: the index of each one's "(" and ")".
    groups = Tuple{Int,Int}[]
    depth, in_class, open = 0, false, 0
    n = blen(body)
    i = 0
    while i < n
        c = bat(body, i)
        if c == UInt8('\\')
            i += 1
        elseif c == UInt8('[') && !in_class
            in_class = true
        elseif c == UInt8(']') && in_class
            in_class = false
        elseif c == UInt8('(') && !in_class
            if depth == 0
                open = i
            end
            depth += 1
        elseif c == UInt8(')') && !in_class
            depth == 0 && return nothing
            depth -= 1
            depth == 0 && push!(groups, (open, i))
        end
        i += 1
    end
    quantified = [g for g in groups if g[2] + 1 < n && bat(body, g[2] + 1) in (UInt8('*'), UInt8('+'))]
    length(quantified) == 1 || return nothing
    gopen, gclose = quantified[1]
    min_repeats = bat(body, gclose + 1) == UInt8('+') ? UInt32(1) : UInt32(0)
    inner = strip_group(bsub(body, gopen, gclose + 1))
    inner === nothing && return nothing
    before, after = bsub(body, 0, gopen), bsub(body, gclose + 2)
    items_of(t::String) = (seq = parse_sequence("^" * t); seq === nothing ? nothing : seq.items)
    fixed(items) = !isempty(items) && all(item -> item.min == item.max && item.min != 0, items)
    group_items = items_of(inner)
    group_items === nothing && return nothing
    if before != "" && after == ""
        # ^I(SR)*$
        unwrapped = unwrap_group(before)
        unwrapped === nothing && return nothing
        first_items = items_of(unwrapped)
        first_items === nothing && return nothing
        for k in 1:length(group_items)-1
            separator, repeated = group_items[1:k], group_items[k+1:end]
            next = separator[1].set
            if fixed(separator) && greedy_before(first_items, next) && greedy_before(repeated, next)
                return SeparatedList(true, greedy_sequence(first_items), greedy_sequence(repeated),
                    greedy_sequence(separator), NO_SEQUENCE, min_repeats)
            end
        end
    elseif before == "" && after != ""
        # ^(RS)*F$
        unwrapped = unwrap_group(after)
        unwrapped === nothing && return nothing
        final = parse_sequence("^" * unwrapped * "\$")
        final === nothing && return nothing
        for k in 1:length(group_items)-1
            repeated, separator = group_items[1:k], group_items[k+1:end]
            if fixed(separator) && greedy_before(repeated, separator[1].set)
                return SeparatedList(false, NO_SEQUENCE, greedy_sequence(repeated), greedy_sequence(separator),
                    final, min_repeats)
            end
        end
    end
    return nothing
end

# The inside of "(...)" or "(?:...)". Nothing for other groups.
function strip_group(g::String)
    inner, ok = cut_prefix(g, "(")
    ok || return nothing
    inner, ok = cut_suffix(inner, ")")
    ok || return nothing
    rest, ok = cut_prefix(inner, "?:")
    ok && return rest
    return startswith(inner, "?") ? nothing : inner
end

# A text that is one group wrapping everything, unwrapped. Otherwise the text itself.
function unwrap_group(t::String)
    (startswith(t, "(") && endswith(t, ")")) || return t
    inner = strip_group(t)
    inner === nothing && return nothing
    # Only when the parentheses enclose the whole text ("(a)(b)" is two groups).
    depth, escaped = 0, false
    for c in codeunits(inner)
        if escaped
            escaped = false
        elseif c == UInt8('\\')
            escaped = true
        elseif c == UInt8('(')
            depth += 1
        elseif c == UInt8(')')
            depth -= 1
            depth < 0 && return nothing
        end
    end
    return inner
end

is_ascii_punctuation(c::UInt8) =
    (UInt8('!') <= c <= UInt8('/')) || (UInt8(':') <= c <= UInt8('@')) || (UInt8('[') <= c <= UInt8('`')) ||
    (UInt8('{') <= c <= UInt8('~'))

# The set of a class escape ("\d", "\w", their negations, or an escaped punctuation character). Nothing for "\s"
# (not ASCII-only) and anything else.
function class_escape(c::UInt8)
    c == UInt8('d') && return DIGIT_SET
    c == UInt8('D') && return set_negate(DIGIT_SET)
    c == UInt8('w') && return WORD_SET
    c == UInt8('W') && return set_negate(WORD_SET)
    c == UInt8('n') && return char_single('\n')
    c == UInt8('r') && return char_single('\r')
    c == UInt8('t') && return char_single('\t')
    is_ascii_punctuation(c) && return char_range(c, c)
    return nothing
end

# Reads a class body from after "[" to after "]", with ASCII members (a negated class also takes every non-ASCII
# character). The set and the next offset, or nothing.
function parse_class(b::String, i::Int)
    n = blen(b)
    negated = i < n && bat(b, i) == UInt8('^')
    if negated
        i += 1
    end
    set = CharSet()
    first_member = true
    while true
        i >= n && return nothing
        c = bat(b, i)
        if c == UInt8(']')
            # "[]" and "[^]"
            first_member && return nothing
            i += 1
            break
        end
        first_member = false
        # The set holds ASCII members only. A member outside ASCII leaves the pattern to the engine. (This is tested
        # before the set is built: a byte of 128 or more is not a bit of it.)
        c >= 0x80 && return nothing
        # One atom: a single character (for ranges) or a set escape.
        atom = char_range(c, c)
        lo, single = c, true
        if c == UInt8('\\')
            i + 1 >= n && return nothing
            e = bat(b, i + 1)
            escape = class_escape(e)
            escape === nothing && return nothing
            atom = escape
            one = set_single(atom)
            single = one >= 0 && (e == UInt8('n') || e == UInt8('r') || e == UInt8('t') || is_ascii_punctuation(e))
            lo = one >= 0 ? one % UInt8 : 0x00
            i += 2
        else
            i += 1
        end
        if i + 1 < n && bat(b, i) == UInt8('-') && bat(b, i + 1) != UInt8(']')
            single || return nothing
            hi = bat(b, i + 1)
            if hi == UInt8('\\')
                (i + 2 >= n || !is_ascii_punctuation(bat(b, i + 2))) && return nothing
                hi = bat(b, i + 2)
                i += 3
            else
                i += 2
            end
            (lo > hi || hi >= 0x80) && return nothing
            set = set_union(set, char_range(lo, hi))
        else
            set = set_union(set, atom)
        end
    end
    return (negated ? set_negate(set) : set), i
end

# Reads an optional quantifier ("*", "+", "?", "{n}", "{n,}", "{n,m}", each optionally lazy) at i: the bounds and the
# next offset, or nothing.
function parse_quantifier(b::String, i::Int)
    n = blen(b)
    i >= n && return UInt32(1), UInt32(1), i
    c = bat(b, i)
    lo, hi = UInt32(1), UInt32(1)
    if c == UInt8('*')
        i += 1
        lo, hi = UInt32(0), typemax(UInt32)
    elseif c == UInt8('+')
        i += 1
        lo, hi = UInt32(1), typemax(UInt32)
    elseif c == UInt8('?')
        i += 1
        lo, hi = UInt32(0), UInt32(1)
    elseif c == UInt8('{')
        close = -1
        for k in i:n-1
            if bat(b, k) == UInt8('}')
                close = k
                break
            end
        end
        close < 0 && return nothing
        body = bsub(b, i + 1, close)
        i = close + 1
        comma = index_byte(body, UInt8(','))
        low = parse_count(comma > 0 ? bsub(body, 0, comma - 1) : body)
        low === nothing && return nothing
        lo = low
        if comma == 0
            hi = lo
        elseif comma == blen(body)
            hi = typemax(UInt32)
        else
            high = parse_count(bsub(body, comma))
            high === nothing && return nothing
            hi = high
        end
    else
        return UInt32(1), UInt32(1), i
    end
    lo > hi && return nothing
    if i < n && bat(b, i) == UInt8('?')
        # Laziness does not change whether the whole pattern matches.
        i += 1
    end
    return lo, hi, i
end
