# Writes the tree of a pattern as PCRE2 patterns with the same meaning, or refuses it.
#
# Nothing that is written depends on how PCRE2 reads a construct ECMA-262 reads differently:
#
# - every literal is a code point escape, and every class is its set of code points, written as ranges. So the dot,
#   \d, \w, \s, a negated class, a \p{...} escape and a class that holds a class escape all mean what ECMA-262 says,
#   in the Unicode version of unicode_data.jl. No \p, \w, \d, \s or \b of PCRE2 is written. A class of many
#   ranges is written once and called where it stands (see `writeclass`);
# - the modifiers of a group such as (?i:...), (?m:...) or (?s:...) were applied while reading: a case-insensitive
#   literal or class is the set of the characters equivalent to it under the Canonicalize of ECMA-262, ^ and $ are
#   assertions about ECMA-262's four line terminators, and the dot is every character. No option of PCRE2 stands
#   for them;
# - a group is written without capture unless a backreference needs it, and never by name, so a name may be any
#   ECMA-262 identifier and may be shared by groups in separate alternatives;
# - a lookbehind is written as it is when the length of its body is fixed, which is all PCRE2 10.42 accepts (10.43
#   and later accept more, within limits). Any other lookbehind is a callout (see `lookbehind`).
#
# A backreference means "the text the group matched when it last took part, or nothing". PCRE2 differs from ECMA-262
# on when a group stops having taken part: it keeps what a group captured in an earlier iteration of a repetition,
# and it runs a lookbehind forwards where ECMA-262 runs it backwards. The emitter shows, for each backreference, that
# these differences cannot be seen, and writes the backreference in a form that is exact. Where it cannot show it,
# the pattern is refused, and the error says why. The reasons are the REFUSED_ constants. A pattern with no
# backreference is never refused for them.

# The backreference is in a lookbehind, which ECMA-262 matches backwards.
const REFUSED_BACKREFERENCE_IN_LOOKBEHIND = "a backreference inside a lookbehind"
# The group is in a lookbehind, which ECMA-262 matches backwards, so it can capture other text.
const REFUSED_GROUP_IN_LOOKBEHIND = "a backreference to a group inside a lookbehind"
# The repetition that holds the backreference can skip the group, which unsets it only in ECMA-262.
const REFUSED_GROUP_IN_REPETITION = "a backreference to a group that an iteration of a repetition can skip"
# The group is in a repetition whose body can match nothing, which the two engines end differently.
const REFUSED_GROUP_IN_EMPTY_REPETITION = "a backreference to a group of a repetition that can match the empty string"
# The group is in a lookaround that may not have matched. A lookaround captures text without matching any, so an
# optional group that holds one can run an iteration that matches nothing and still sets the group. ECMA-262 refuses
# such an iteration and PCRE2 takes it.
const REFUSED_GROUP_IN_LOOKAROUND = "a backreference to a group of a lookaround that may not have matched"
# The backreference ignores case, and the group can hold a character PCRE2 compares differently.
const REFUSED_CASE_INSENSITIVE_BACKREFERENCE =
    "a case-insensitive backreference to text that PCRE2 compares differently"
# PCRE2 did not compile the translation. The error message carries what PCRE2 said.
const REFUSED_BY_ENGINE = "a pattern PCRE2 does not compile"

unsupported(E, reason::String) = throw(PatternError(E.pattern, reason, true))

# A length with no bound.
const INFINITE = typemax(Int64)
# Past this a length is as good as unbounded, and keeping below it keeps the sums exact.
const LENGTH_LIMIT = Int64(1) << 47
# The largest count PCRE2 takes in a quantifier, and the longest lookbehind it takes.
const PCRE_MAX_COUNT = 65535
# The callouts that end and begin the body of a lookbehind (see `lookbehind`). The others name a lookbehind.
const CALLOUT_AT_TARGET = 0
const CALLOUT_NOT_PAST_TARGET = 1
const CALLOUT_FIRST_LOOKBEHIND = 2
# How the body of a lookbehind that is a callout starts, which no other pattern does.
const LOOKBEHIND_PREFIX = "(?C1)"
# PCRE2 numbers the callouts it adds itself 255.
const PCRE_MAX_LOOKBEHIND_CALLOUT = 254

const LINE_TERMINATOR_CLASS = "[\\x{a}\\x{d}\\x{2028}\\x{2029}]"
const ANY_CHARACTER = "(?s:.)"
const NEVER_MATCHES = "(?!)"
const WORD_CLASS = "[a-zA-Z0-9_]"
const FOLD_WORD_CLASS = "[a-zA-Z0-9_\\x{17f}\\x{212a}]"

const SURROGATE_SET = Int32[0xD800, 0xDFFF]
const NOT_SURROGATE_SET = complement(SURROGATE_SET)
const ASCII_LETTER_SET = Int32['A', 'Z', 'a', 'z']
const NOT_ASCII_SET = Int32[0x80, MAX_CODE_POINT]
# The ASCII letters with the two characters that fold to one of them with the u flag grammar, and in PCRE2.
const FOLD_LETTER_SET = Int32['A', 'Z', 'a', 'z', 0x17F, 0x17F, 0x212A, 0x212A]
# The ASCII letters but for k and s, which U+212A and U+017F fold to in PCRE2 whatever the grammar.
const ASCII_LETTERS_FOLDING_IN_ASCII = Int32['A', 'J', 'L', 'R', 'T', 'Z', 'a', 'j', 'l', 'r', 't', 'z']

# The translation of a pattern. It is the pattern PCRE2 searches for, and the body of each lookbehind that is a
# callout, in the order of the callout numbers.
struct Translation
    main::String
    lookbehinds::Vector{String}
end

mutable struct Emitter
    pattern::String
    unicode::Bool
    root::Node
    groupnodes::Vector{Union{Nothing,Node}}
    # For each group, whether some backreference reads it, and whether one reads it unsure that it is set.
    referenced::Vector{Bool}
    needsmarker::Vector{Bool}
    # For each group: whether a lookbehind or any lookaround encloses it, and the repetitions that enclose it.
    inlookbehind::Vector{Bool}
    inlookaround::Vector{Bool}
    repeatsaround::Vector{Vector{Node}}
    # For each group, the number of its group in the translation, and of the group that marks it took part.
    pcregroup::Vector{Int}
    pcremarker::Vector{Int}
    out::IOBuffer
    pcregroups::Int
    # Whether some backreference is written, so that what a group captures can be seen.
    hasbackreference::Bool
    haslookbehind::Bool
    # Whether every lookbehind is written as a callout.
    searchinglookbehind::Bool
    lookbehinds::Vector{String}
    # The sets of many ranges of the pattern being written, which it calls by number (see `writeclass`).
    largesets::Vector{CodeSet}
end

function Emitter(parser::Parser, root::Node)
    n = parser.groupcount
    return Emitter(parser.pattern, parser.unicode, root, Vector{Union{Nothing,Node}}(nothing, n), fill(false, n),
        fill(false, n), fill(false, n), fill(false, n), [NO_NODES for _ in 1:n], zeros(Int, n), zeros(Int, n),
        IOBuffer(), 0, false, false, false, String[], CodeSet[])
end

# Works out what each backreference can rely on, and refuses the pattern when one cannot be written exactly.
function analyse!(E::Emitter)
    flow!(E, E.root, BitSet(), BitSet(), false)
    survey!(E, E.root, false, false, Node[])
    return E
end

# The translation, with every lookbehind as a callout when `searching`.
function translation(E::Emitter, searching::Bool)
    E.searchinglookbehind = searching
    E.out = IOBuffer()
    E.pcregroups = 0
    E.haslookbehind = false
    empty!(E.lookbehinds)
    fill!(E.pcregroup, 0)
    fill!(E.pcremarker, 0)
    empty!(E.largesets)
    node(E, E.root)
    definitions(E)
    return Translation(String(take!(E.out)), copy(E.lookbehinds))
end

# What a backreference can rely on.

# Works out, for every backreference, whether each group it names has taken part where the backreference stands: on
# every path (DEFINITE), on none (NEVER) or on some (MAYBE). The walk follows the order in which ECMA-262 evaluates
# the pattern, which is backwards inside a lookbehind. `definite` and `possible` hold the groups set on every path
# and on some path to the node, and are updated to hold the same for the paths that leave it.
#
# ECMA-262 unsets the groups of a quantified body at the start of each of its iterations, and unsets the groups of a
# negative lookaround when the lookaround is done.
function flow!(E::Emitter, n::Node, definite::BitSet, possible::BitSet, backward::Bool)
    kind = n.kind
    if kind == CAT
        for k in (backward ? reverse(eachindex(n.subs)) : eachindex(n.subs))
            flow!(E, n.subs[k], definite, possible, backward)
        end
    elseif kind == ALT
        alldefinite = nothing
        anypossible = BitSet()
        for sub in n.subs
            d = copy(definite)
            p = copy(possible)
            flow!(E, sub, d, p, backward)
            if alldefinite === nothing
                alldefinite = d
            else
                intersect!(alldefinite, d)
            end
            union!(anypossible, p)
        end
        empty!(definite)
        union!(definite, alldefinite::BitSet)
        empty!(possible)
        union!(possible, anypossible)
    elseif kind == GROUP
        flow!(E, n.subs[1], definite, possible, backward)
        if n.group != 0
            E.groupnodes[n.group] = n
            push!(definite, n.group)
            push!(possible, n.group)
        end
    elseif kind == REPEAT
        inside = (n.firstgroup + 1):n.lastgroup
        setdiff!(definite, inside)
        setdiff!(possible, inside)
        if n.max == 0
            # The body never runs, so nothing it sets is set after it.
            flow!(E, n.subs[1], copy(definite), copy(possible), backward)
        elseif n.min == 0
            flow!(E, n.subs[1], copy(definite), possible, backward)
        else
            flow!(E, n.subs[1], definite, possible, backward)
        end
    elseif kind == LOOK
        if n.negative
            flow!(E, n.subs[1], copy(definite), copy(possible), n.behind)
            inside = (n.firstgroup + 1):n.lastgroup
            setdiff!(definite, inside)
            setdiff!(possible, inside)
        else
            flow!(E, n.subs[1], definite, possible, n.behind)
        end
    elseif kind == BACKREF
        n.statuses = Int[g in definite ? DEFINITE : g in possible ? MAYBE : NEVER for g in n.groups]
        n.ignorecase = fill(false, length(n.groups))
    end
    return nothing
end

# Notes what encloses each group, then decides for each backreference whether PCRE2 gives it the meaning ECMA-262
# does, and refuses the pattern when that cannot be shown.
function survey!(E::Emitter, n::Node, lookbehind::Bool, lookaround::Bool, repeats::Vector{Node})
    kind = n.kind
    if kind == GROUP
        if n.group != 0
            E.inlookbehind[n.group] = lookbehind
            E.inlookaround[n.group] = lookaround
            E.repeatsaround[n.group] = copy(repeats)
        end
        survey!(E, n.subs[1], lookbehind, lookaround, repeats)
    elseif kind == REPEAT
        # A repetition of at most once has no earlier iteration.
        if n.max == UNBOUNDED || n.max > 1
            push!(repeats, n)
            survey!(E, n.subs[1], lookbehind, lookaround, repeats)
            pop!(repeats)
        else
            survey!(E, n.subs[1], lookbehind, lookaround, repeats)
        end
    elseif kind == LOOK
        survey!(E, n.subs[1], lookbehind || n.behind, true, repeats)
    elseif kind == BACKREF
        decide!(E, n, lookbehind, repeats)
    else
        for sub in n.subs
            survey!(E, sub, lookbehind, lookaround, repeats)
        end
    end
    return nothing
end

holds(repeats::Vector{Node}, n::Node) = any(r -> r === n, repeats)

# Decides one backreference. Outside a lookbehind ECMA-262 evaluates a pattern from left to right, so a group that
# can have taken part comes before the backreference, and what encloses it is known.
#
# A group that has taken part on every path to the backreference was set on the path PCRE2 is on, so its text is the
# one ECMA-262 means. The two engines differ there in one case, a repetition whose body can match the empty string:
# ECMA-262 refuses an empty iteration and PCRE2 takes one, which can leave a group of the body with other text.
#
# A group that may have taken part needs a test, and the test reads what PCRE2 kept. It is sound only where that
# engine forgets a group when ECMA-262 does, which is on backtracking out of the group. It is not shown to be sound
# for a group in a lookaround. The group can then be set by an iteration of an optional group that matches nothing,
# which ECMA-262 refuses and PCRE2 takes (the oracle has `^(?:(?=(a)))?\1b`, on which the two differ). Nor is it
# sound for a group in a repetition, as ECMA-262 unsets the group when an iteration starts and PCRE2 keeps it. A
# repetition that ends before the backreference is therefore written with its last iteration apart (see
# `writerepeat`), so that the group is one only the last iteration sets. A repetition that holds the backreference
# too cannot be, and is refused.
function decide!(E::Emitter, n::Node, lookbehind::Bool, repeats::Vector{Node})
    for k in eachindex(n.groups)
        if n.statuses[k] == DEFINITE
            # At most one of the groups of a name has taken part, so the others have not.
            fill!(n.statuses, NEVER)
            n.statuses[k] = DEFINITE
            break
        end
    end
    for k in eachindex(n.groups)
        g = n.groups[k]
        n.statuses[k] == NEVER && continue
        lookbehind && unsupported(E, REFUSED_BACKREFERENCE_IN_LOOKBEHIND)
        E.inlookbehind[g] && unsupported(E, REFUSED_GROUP_IN_LOOKBEHIND)
        if n.statuses[k] == DEFINITE
            for rep in E.repeatsaround[g]
                if !holds(repeats, rep) && minlength(rep.subs[1]) == 0
                    unsupported(E, REFUSED_GROUP_IN_EMPTY_REPETITION)
                end
            end
        else
            E.inlookaround[g] && unsupported(E, REFUSED_GROUP_IN_LOOKAROUND)
            for rep in E.repeatsaround[g]
                holds(repeats, rep) && unsupported(E, REFUSED_GROUP_IN_REPETITION)
                minlength(rep.subs[1]) == 0 && unsupported(E, REFUSED_GROUP_IN_EMPTY_REPETITION)
                rep.lastapart = true
            end
            E.needsmarker[g] = true
        end
        E.referenced[g] = true
        E.hasbackreference = true
        n.ignorecase[k] = n.fold && comparesignoringcase(E, g)
    end
    return nothing
end

# Decides how a case-insensitive backreference to a group is written, from the characters the group can capture.
# Returns false when none of them has a case variant, so that the backreference compares exactly.
#
# Otherwise the comparison is PCRE2's, which is Unicode simple case folding in the Unicode version of that PCRE2. It
# is the comparison ECMA-262 makes when the only captured characters with a variant, in either grammar, are ASCII
# letters. PCRE2 compares those the same way in every version. A character with no variant in the data has none in
# a PCRE2 of the same or an earlier Unicode version, as Unicode never takes a case pair away. A later PCRE2 may give
# such a character a variant, so with one the group may hold no other character outside ASCII at all (see
# `pcrefoldingisknown`). With the u flag grammar every ASCII letter qualifies, with U+212A and U+017F, which both
# ECMA-262 and PCRE2 fold to k and s. With no flag ECMA-262 does not fold them and PCRE2 still does, so k and s are
# left out. Any other group is refused.
function comparesignoringcase(E::Emitter, g::Int)
    captured = capturedset(E, E.groupnodes[g]::Node)
    intersects(captured, cased(E.unicode)) || return false
    letters = E.unicode ? FOLD_LETTER_SET : ASCII_LETTERS_FOLDING_IN_ASCII
    other = pcrefoldingisknown() ? setunion(cased(true), cased(false)) : NOT_ASCII_SET
    if intersects(captured, difference(setunion(other, ASCII_LETTER_SET), letters))
        unsupported(E, REFUSED_CASE_INSENSITIVE_BACKREFERENCE)
    end
    return true
end

# Every character the text a node matches can hold.
function capturedset(E::Emitter, n::Node)
    kind = n.kind
    if kind == CHAR
        return Int32[n.codepoint, n.codepoint]
    elseif kind == SET
        return n.set
    elseif kind == LOOK
        return NO_CHARACTERS
    elseif kind == BACKREF
        set = NO_CHARACTERS
        for k in eachindex(n.groups)
            if n.statuses[k] != NEVER
                target = capturedset(E, E.groupnodes[n.groups[k]]::Node)
                set = setunion(set, n.fold ? foldclosure(target, E.unicode) : target)
            end
        end
        return set
    end
    set = NO_CHARACTERS
    for sub in n.subs
        set = setunion(set, capturedset(E, sub))
    end
    return set
end

# Lengths, in characters.

function minlength(n::Node)
    n.minlength < 0 && measure!(n)
    return n.minlength
end

function maxlength(n::Node)
    n.minlength < 0 && measure!(n)
    return n.maxlength
end

plus(a::Int64, b::Int64) = a == INFINITE || b == INFINITE ? INFINITE : a + b

# A length some number of times over, where the lengths that are not INFINITE are below LENGTH_LIMIT.
function times(len::Int64, count::Int)
    (len == 0 || count == 0) && return Int64(0)
    if len == INFINITE || count == UNBOUNDED || len > LENGTH_LIMIT ÷ count
        return INFINITE
    end
    return len * count
end

function measure!(n::Node)
    lo = Int64(0)
    hi = Int64(0)
    kind = n.kind
    if kind == CHAR || kind == SET
        lo = 1
        hi = 1
    elseif kind == CAT
        for sub in n.subs
            lo = plus(lo, minlength(sub))
            hi = plus(hi, maxlength(sub))
        end
    elseif kind == ALT
        lo = INFINITE
        for sub in n.subs
            lo = min(lo, minlength(sub))
            hi = max(hi, maxlength(sub))
        end
    elseif kind == GROUP
        lo = minlength(n.subs[1])
        hi = maxlength(n.subs[1])
    elseif kind == REPEAT
        lo = times(minlength(n.subs[1]), n.min)
        hi = times(maxlength(n.subs[1]), n.max)
    elseif kind == BACKREF
        hi = INFINITE
    end
    n.minlength = lo > LENGTH_LIMIT ? INFINITE : lo
    n.maxlength = hi > LENGTH_LIMIT ? INFINITE : hi
    return nothing
end

# Writing.

function node(E::Emitter, n::Node)
    kind = n.kind
    out = E.out
    if kind == EMPTY
    elseif kind == CHAR
        literal(out, n.codepoint)
    elseif kind == SET
        writeclass(E, n.set)
    elseif kind == CAT
        for sub in n.subs
            node(E, sub)
        end
    elseif kind == ALT
        for k in eachindex(n.subs)
            k > 1 && print(out, '|')
            node(E, n.subs[k])
        end
    elseif kind == GROUP
        writegroup(E, n)
    elseif kind == REPEAT
        writerepeat(E, n)
    elseif kind == BOL
        print(out, "\\A")
    elseif kind == EOL
        print(out, "\\z")
    elseif kind == LINE_START
        print(out, "(?:\\A|(?<=", LINE_TERMINATOR_CLASS, "))")
    elseif kind == LINE_END
        print(out, "(?:\\z|(?=", LINE_TERMINATOR_CLASS, "))")
    elseif kind == WORD_BOUNDARY
        w = n.fold ? FOLD_WORD_CLASS : WORD_CLASS
        print(out, "(?:(?<=", w, ")(?!", w, ")|(?<!", w, ")(?=", w, "))")
    elseif kind == NOT_WORD_BOUNDARY
        w = n.fold ? FOLD_WORD_CLASS : WORD_CLASS
        print(out, "(?:(?<=", w, ")(?=", w, ")|(?<!", w, ")(?!", w, "))")
    elseif kind == LOOK
        if n.behind
            lookbehind(E, n)
        else
            print(out, n.negative ? "(?!" : "(?=")
            node(E, n.subs[1])
            print(out, ')')
        end
    elseif kind == BACKREF
        backreference(E, n)
    else
        error("unknown node")
    end
    return nothing
end

issurrogate(c::Integer) = c >= 0xD800 && c <= 0xDFFF

writecodepoint(out::IO, c::Integer) = print(out, "\\x{", string(c, base=16), '}')

# Writes one character. A surrogate is no character of a UTF-8 text, so it never matches.
function literal(out::IO, c::Int32)
    if isasciiletter(c) || isasciidigit(c)
        print(out, Char(c))
    elseif issurrogate(c)
        print(out, NEVER_MATCHES)
    else
        writecodepoint(out, c)
    end
    return nothing
end

# Writes a set as a class of its ranges, less the surrogates, which PCRE2 does not take in a pattern and which no
# UTF-8 text holds.
#
# A set that holds both U+00FF and U+0100 is written as the negated class of the characters it lacks. It means the
# same, as a negated class of PCRE2 matches every character of the text that is not listed. The reason is PCRE2
# 10.46, the one Julia 1.13 has. In a class of six or more ranges, a range that starts below U+0100 and ends above
# U+00FF does not match its characters from U+0100 to U+7FFF (or to U+FFFF when it ends beyond that). The class
# written here has no such range.
function writeset(out::IO, set::CodeSet)
    set = intersects(set, SURROGATE_SET) ? intersection(set, NOT_SURROGATE_SET) : set
    if isempty(set)
        # No character. That is an assertion that always fails.
        print(out, NEVER_MATCHES)
    elseif set == NOT_SURROGATE_SET
        print(out, ANY_CHARACTER)
    elseif inset(set, 0xFF) && inset(set, 0x100)
        print(out, "[^")
        writeranges(out, intersection(complement(set), NOT_SURROGATE_SET))
        print(out, ']')
    else
        print(out, '[')
        writeranges(out, set)
        print(out, ']')
    end
    return nothing
end

function writeranges(out::IO, set::CodeSet, from::Int=1, to::Int=length(set))
    for i in from:2:to
        writecodepoint(out, set[i])
        if set[i + 1] != set[i]
            print(out, '-')
            writecodepoint(out, set[i + 1])
        end
    end
    return nothing
end

# A set whose class has more ranges than this is written once in a pattern, however often the pattern holds it.
const LARGE_SET_RANGES = 64
# The ranges at a leaf of a search tree.
const TREE_LEAF_RANGES = 32
const LATIN1_SET = Int32[0, 0xFF]
const NOT_LATIN1_SET = Int32[0x100, MAX_CODE_POINT]
# Whether a set of many ranges is written as a search tree. With nothing, it is where the PCRE2 in use is slow on a
# class of many ranges. The tests set it to try both forms on one PCRE2.
const SEARCH_TREES = Ref{Union{Nothing,Bool}}(nothing)

usesearchtrees() = something(SEARCH_TREES[], !pcreclassesarefast())

# Writes the set of a SET node. A set of few ranges is written as its class, where it stands. A set of many ranges,
# such as that of \p{L}, is written once, as a named group after the pattern that the pattern never runs into (see
# `definitions`), and where the set stands the pattern calls the group, as (?&s1). A call matches what the group
# matches. Two things are gained. PCRE2, as Julia builds it, holds a compiled pattern of at most 64 KiB, which a
# dozen classes of the size of \p{L} fill. And the group can be a search tree (see `searchtree`), which is then of
# the members beyond U+00FF. Those up to U+00FF stay where the set stands, as a class that PCRE2 keeps as a bit map,
# so that a character of ASCII costs no call.
function writeclass(E::Emitter, set::CodeSet)
    out = E.out
    set = intersects(set, SURROGATE_SET) ? intersection(set, NOT_SURROGATE_SET) : set
    written = inset(set, 0xFF) && inset(set, 0x100) ? intersection(complement(set), NOT_SURROGATE_SET) : set
    if length(written) <= 2 * LARGE_SET_RANGES
        writeset(out, set)
    elseif !usesearchtrees()
        print(out, "(?&s", largeset(E, set), ')')
    else
        low = intersection(set, LATIN1_SET)
        high = intersection(set, NOT_LATIN1_SET)
        if isempty(high)
            writeset(out, set)
        elseif isempty(low)
            print(out, "(?&s", largeset(E, high), ')')
        else
            print(out, "(?:[")
            writeranges(out, low)
            print(out, "]|(?&s", largeset(E, high), "))")
        end
    end
    return nothing
end

# The number of a set of many ranges in the pattern being written.
function largeset(E::Emitter, set::CodeSet)
    number = findfirst(==(set), E.largesets)
    if number === nothing
        push!(E.largesets, set)
        number = length(E.largesets)
    end
    return number
end

# Writes the groups of the sets of many ranges of the pattern, inside a group PCRE2 only reads definitions from.
function definitions(E::Emitter)
    isempty(E.largesets) && return nothing
    out = E.out
    print(out, "(?(DEFINE)")
    for (number, set) in enumerate(E.largesets)
        print(out, "(?<s", number, '>')
        if usesearchtrees()
            searchtree(out, set, 1, length(set))
        else
            writeset(out, set)
        end
        print(out, ')')
    end
    print(out, ')')
    return nothing
end

# Writes a set of many ranges, all beyond U+00FF, as a search tree. PCRE2 before 10.45 tries the ranges of a class
# one after another for every character beyond U+00FF, which for \p{L} is some 680 comparisons. The tree splits the
# ranges in halves, (?:(?=[\x{a}-\x{b}])left|right), where a lookahead lets into the left half only a character
# between its first and its last member, down to classes of a few ranges. A character is then tried against a number
# of ranges that grows with the logarithm of the size of the set. PCRE2 10.45 and later search a class by halves
# themselves, and are faster on the class as it is.
function searchtree(out::IO, set::CodeSet, from::Int, to::Int)
    ranges = (to - from + 1) ÷ 2
    if ranges <= TREE_LEAF_RANGES
        print(out, '[')
        writeranges(out, set, from, to)
        print(out, ']')
        return nothing
    end
    mid = from + 2 * (ranges ÷ 2)
    print(out, "(?:(?=[")
    writecodepoint(out, set[from])
    print(out, '-')
    writecodepoint(out, set[mid - 1])
    print(out, "])")
    searchtree(out, set, from, mid - 1)
    print(out, '|')
    searchtree(out, set, mid, to)
    print(out, ')')
    return nothing
end

function writegroup(E::Emitter, n::Node)
    out = E.out
    if n.group == 0 || !E.referenced[n.group]
        print(out, "(?:")
        node(E, n.subs[1])
        print(out, ')')
        return nothing
    end
    E.pcregroups += 1
    E.pcregroup[n.group] = E.pcregroups
    if !E.needsmarker[n.group]
        print(out, '(')
        node(E, n.subs[1])
        print(out, ')')
        return nothing
    end
    # An empty group at the end marks that the group took part.
    print(out, "((?:")
    node(E, n.subs[1])
    E.pcregroups += 1
    E.pcremarker[n.group] = E.pcregroups
    print(out, ")())")
    return nothing
end

# Writes a repetition.
#
# When a backreference after the repetition reads a group of its body that an iteration can skip, the body X is
# written twice, as (?:(?:X){m-1,n-1}X), and made optional when the repetition can run no times. The backreference
# reads the groups of the second X, which only the last iteration sets, as in ECMA-262, where each iteration starts
# by unsetting the groups of the body. The first X has groups of its own for the backreferences inside it. The two
# forms match the same texts, as the body cannot match the empty string (such a repetition is refused), so every
# iteration is one ECMA-262 takes.
function writerepeat(E::Emitter, n::Node)
    body = n.subs[1]
    out = E.out
    if !n.lastapart
        counted(E, body, n.min, n.max, n.lazy)
        return nothing
    end
    print(out, "(?:")
    counted(E, body, max(n.min - 1, 0), n.max == UNBOUNDED ? UNBOUNDED : n.max - 1, n.lazy)
    node(E, body)
    print(out, ')')
    n.min == 0 && print(out, n.lazy ? "??" : "?")
    return nothing
end

# Writes a node repeated between `lo` and `hi` times.
#
# PCRE2 counts to 65535 and ECMA-262 counts further. A larger count is written as several repetitions that match the
# same texts and try the counts in the same order (see `exactly` and `upto`). The body is then written more than
# once, which a group that a backreference reads cannot be, so this is done only for a pattern with no
# backreference. With one, the count is written as it is and PCRE2 refuses the pattern.
function counted(E::Emitter, body::Node, lo::Int, hi::Int, lazy::Bool)
    out = E.out
    if E.hasbackreference || (lo <= PCRE_MAX_COUNT && (hi == UNBOUNDED || hi <= PCRE_MAX_COUNT))
        atom(E, body)
        quantifier(out, lo, hi, lazy)
        return nothing
    end
    print(out, "(?:")
    exactly(E, body, lo)
    if hi == UNBOUNDED
        atom(E, body)
        quantifier(out, 0, UNBOUNDED, lazy)
    elseif hi > lo
        upto(E, body, hi - lo, lazy)
    end
    print(out, ')')
    return nothing
end

# Writes a node repeated exactly `count` times, for any count, as X{65535} as often as it goes and then the rest.
function exactly(E::Emitter, body::Node, count::Int)
    out = E.out
    whole = count ÷ PCRE_MAX_COUNT
    rest = count % PCRE_MAX_COUNT
    if whole > 0
        print(out, "(?:")
        atom(E, body)
        quantifier(out, PCRE_MAX_COUNT, PCRE_MAX_COUNT, false)
        print(out, ')')
        quantifier(out, whole, whole, false)
    end
    if rest > 0
        atom(E, body)
        quantifier(out, rest, rest, false)
    end
    return nothing
end

# Writes a node repeated up to `count` times, for any count. Up to n times, for n beyond 65535, is either 65535
# times and then up to n - 65535 times, or up to 65534 times. Each number of times is reached in one way only, and
# the alternative with more of them comes first unless the repetition is lazy, so the numbers are tried in the order
# ECMA-262 tries them.
function upto(E::Emitter, body::Node, count::Int, lazy::Bool)
    out = E.out
    if count <= PCRE_MAX_COUNT
        atom(E, body)
        quantifier(out, 0, count, lazy)
        return nothing
    end
    print(out, "(?:")
    if lazy
        atom(E, body)
        quantifier(out, 0, PCRE_MAX_COUNT - 1, true)
        print(out, '|')
    end
    atom(E, body)
    quantifier(out, PCRE_MAX_COUNT, PCRE_MAX_COUNT, false)
    upto(E, body, count - PCRE_MAX_COUNT, lazy)
    if !lazy
        print(out, '|')
        atom(E, body)
        quantifier(out, 0, PCRE_MAX_COUNT - 1, false)
    end
    print(out, ')')
    return nothing
end

# Writes a node as one unit that a quantifier can follow.
function atom(E::Emitter, n::Node)
    unit = (n.kind == CHAR && !issurrogate(n.codepoint)) || n.kind == GROUP ||
        (n.kind == SET && intersects(n.set, NOT_SURROGATE_SET))
    unit || print(E.out, "(?:")
    node(E, n)
    unit || print(E.out, ')')
    return nothing
end

function quantifier(out::IO, lo::Int, hi::Int, lazy::Bool)
    if lo == 0 && hi == UNBOUNDED
        print(out, '*')
    elseif lo == 1 && hi == UNBOUNDED
        print(out, '+')
    elseif lo == 0 && hi == 1
        print(out, '?')
    else
        print(out, '{', lo)
        if hi != lo
            print(out, ',')
            hi != UNBOUNDED && print(out, hi)
        end
        print(out, '}')
    end
    lazy && print(out, '?')
    return nothing
end

# Whether PCRE2, in every version, takes a lookbehind with this body as it is. That is so when the body matches
# texts of one length,
# or it is a list of alternatives that each do.
function fixedlength(body::Node)
    if body.kind == ALT
        return all(sub -> minlength(sub) == maxlength(sub) && maxlength(sub) <= PCRE_MAX_COUNT, body.subs)
    end
    return minlength(body) == maxlength(body) && maxlength(body) <= PCRE_MAX_COUNT
end

# Writes a lookbehind. PCRE2 runs one by stepping back over as many characters as its body is long and matching the
# body from there, so it needs the length. That is exact, as running the body forwards where ECMA-262 runs it backwards
# finds a match exactly when ECMA-262 does. The two directions can differ only in what the groups of the body
# capture, and a backreference to a group of a lookbehind, or inside a lookbehind, is refused.
#
# PCRE2 10.42 takes a body only when its length is fixed, and later versions take one whose length is bounded and
# short. So that a pattern means the same on each, a body is written as it is only when its length is fixed. Any
# other body X is compiled apart, as (?C1)(?:X)(?C0), and the lookbehind is written as the callout (?Cn) that names
# it, inside (?!...) when it is negative. When the match reaches the callout, the matcher searches the whole text
# for X from the start. The callout 0 after X lets the search succeed only where X ends at the position of the
# lookbehind, and the callout 1 before X ends the search once it starts past that position. That is the definition
# of a lookbehind, which holds when its body matches some stretch of the text that ends where it stands. The search
# takes time that grows with the square of the length of the text.
function lookbehind(E::Emitter, n::Node)
    E.haslookbehind = true
    body = n.subs[1]
    out = E.out
    if !E.searchinglookbehind && fixedlength(body)
        print(out, n.negative ? "(?<!" : "(?<=")
        node(E, body)
        print(out, ')')
        return nothing
    end
    push!(E.lookbehinds, "")
    index = length(E.lookbehinds)
    number = CALLOUT_FIRST_LOOKBEHIND + index - 1
    if number > PCRE_MAX_LOOKBEHIND_CALLOUT
        unsupported(E, REFUSED_BY_ENGINE * " (more than 253 lookbehinds of no fixed length)")
    end
    largesets = E.largesets
    E.out = IOBuffer()
    E.largesets = CodeSet[]
    print(E.out, LOOKBEHIND_PREFIX, "(?:")
    node(E, body)
    print(E.out, ")(?C", CALLOUT_AT_TARGET, ')')
    definitions(E)
    E.lookbehinds[index] = String(take!(E.out))
    E.out = out
    E.largesets = largesets
    if n.negative
        print(out, "(?!(?C", number, "))")
    else
        print(out, "(?C", number, ')')
    end
    return nothing
end

# Writes a backreference, which in ECMA-262 matches the text the group captured when the group has taken part and
# the empty string when it has not. A group known to have taken part is read as it is. One that may have is read
# behind a test of the empty group that marks it, and the last branch is the case where none of the groups of the
# name took part. PCRE2 fails a reference to a group that is not set, which is what makes the test.
function backreference(E::Emitter, n::Node)
    out = E.out
    wrote = false
    anymaybe = false
    for k in eachindex(n.groups)
        g = n.groups[k]
        n.statuses[k] == NEVER && continue
        E.pcregroup[g] == 0 && error("backreference before its group")
        print(out, wrote ? "|" : "(?:")
        wrote = true
        if n.statuses[k] == MAYBE
            print(out, "(?=\\g{", E.pcremarker[g], "})")
            anymaybe = true
        end
        if n.ignorecase[k]
            print(out, "(?i:\\g{", E.pcregroup[g], "})")
        else
            print(out, "\\g{", E.pcregroup[g], '}')
        end
    end
    wrote || return nothing
    if anymaybe
        print(out, '|')
        for k in eachindex(n.groups)
            if n.statuses[k] == MAYBE
                print(out, "(?!\\g{", E.pcremarker[n.groups[k]], "})")
            end
        end
    end
    print(out, ')')
    return nothing
end
