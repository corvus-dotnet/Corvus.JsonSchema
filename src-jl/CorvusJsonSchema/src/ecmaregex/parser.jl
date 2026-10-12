# Reads an ECMA-262 pattern by recursive descent into a tree. The grammar is that of ECMAScript 2025. With `unicode`
# the parser applies the grammar of the u flag, and without it the Annex B grammar of a pattern with no flag.

# Groups may nest this deep. It bounds the recursion of the readers, which count the pattern itself as one more.
const MAX_NESTING = 200
# A pattern may have this many terms and alternatives, which bounds the tree it is read into. A term is some 300
# bytes of tree while the pattern is compiled, so a pattern of a megabyte of single characters was read into 450 MB
# before it was found to be more than PCRE2 holds. PCRE2, as Julia builds it, holds a compiled pattern of at most
# 64 KiB, which is at most 65,536 terms, so no pattern it takes has more than these.
const MAX_PATTERN_TERMS = 1 << 17

# The classes ECMA-262 defines, as sets. \d and \w are ASCII only, \s is WhiteSpace and LineTerminator, and the dot
# is everything but the four line terminators.
const DIGIT_SET = Int32['0', '9']
const WORD_SET = Int32['0', '9', 'A', 'Z', '_', '_', 'a', 'z']
const SPACE_SET = Int32[
    0x9, 0xD, 0x20, 0x20, 0xA0, 0xA0, 0x1680, 0x1680, 0x2000, 0x200A, 0x2028, 0x2029, 0x202F, 0x202F, 0x205F, 0x205F,
    0x3000, 0x3000, 0xFEFF, 0xFEFF,
]
const LINE_TERMINATOR_SET = Int32['\n', '\n', '\r', '\r', 0x2028, 0x2029]
const DOT_SET = complement(LINE_TERMINATOR_SET)
# With the u flag grammar and case-insensitive matching, \w and \b also count the two characters that fold to an
# ASCII letter (U+017F, the long s, and U+212A, the Kelvin sign) as word characters.
const FOLD_WORD_SET = Int32['0', '9', 'A', 'Z', '_', '_', 'a', 'z', 0x17F, 0x17F, 0x212A, 0x212A]
const NOT_DIGIT_SET = complement(DIGIT_SET)
const NOT_WORD_SET = complement(WORD_SET)
const NOT_FOLD_WORD_SET = complement(FOLD_WORD_SET)
const NOT_SPACE_SET = complement(SPACE_SET)

# The kinds of node.
const EMPTY = 0
const CHAR = 1
const SET = 2
const CAT = 3
const ALT = 4
const GROUP = 5
const REPEAT = 6
const BOL = 7
const EOL = 8
# ^ and $ inside a group with the m modifier.
const LINE_START = 9
const LINE_END = 10
const WORD_BOUNDARY = 11
const NOT_WORD_BOUNDARY = 12
const LOOK = 13
const BACKREF = 14

# The max of a quantifier with no upper bound.
const UNBOUNDED = -1
# The largest bound of a counted quantifier. No string reaches a larger count.
const MAX_COUNT = Int(typemax(Int32)) - 1

# What is known, where a backreference stands, of whether a group it names has taken part: it never has, or it can
# have.
const NEVER = 0
const LIVE = 1

# One element of the syntax tree of a pattern.
mutable struct Node
    kind::Int
    # The code point of a CHAR.
    codepoint::Int32
    # The set of a SET.
    set::CodeSet
    # The children of CAT and ALT, and the one child of GROUP, REPEAT and LOOK.
    subs::Vector{Node}
    # The bounds of a REPEAT, and whether it is lazy.
    min::Int
    max::Int
    lazy::Bool
    # Marks a REPEAT or a lookbehind with a group inside that a backreference outside it reads.
    exports::Bool
    # Marks a lookbehind that holds a backreference to a group that can have taken part.
    haslive::Bool
    # Marks a lookbehind that was written again as a fixed end and a lookbehind for the rest (see `split!`).
    split::Bool
    # Marks a lookbehind that is written as a search with the rest of the pattern inside it (see `continuation`).
    continued::Bool
    # The number of the capturing group of a GROUP, counted from 1 as ECMA-262 does, or 0.
    group::Int
    # The groups a BACKREF names. A name can belong to several groups in separate alternatives, of which at most one
    # has taken part at any time.
    groups::Vector{Int}
    # For each of the groups, whether it can have taken part where the reference stands (NEVER or LIVE).
    statuses::Vector{Int}
    # For each of the groups, whether PCRE2 is to compare its text ignoring case.
    ignorecase::Vector{Bool}
    # The group name of a named BACKREF until it is resolved.
    name::String
    # Marks a BACKREF, WORD_BOUNDARY or NOT_WORD_BOUNDARY inside a case-insensitive group.
    fold::Bool
    # The direction and the sense of a LOOK.
    behind::Bool
    negative::Bool
    # The capturing groups inside the node are those numbered above firstgroup, up to lastgroup.
    firstgroup::Int
    lastgroup::Int
    # The least and the greatest number of characters the node matches, once measured.
    minlength::Int64
    maxlength::Int64
end

const NO_NODES = Node[]
const NO_INTS = Int[]
const NO_BOOLS = Bool[]

Node(kind::Int) = Node(kind, Int32(0), NO_CHARACTERS, NO_NODES, 0, 0, false, false, false, false, false, 0, NO_INTS,
    NO_INTS, NO_BOOLS, "", false, false, false, 0, 0, Int64(-1), Int64(0))

# What peek answers at the end of the pattern. It is not a code point.
const END_OF_PATTERN = Char(0x110000)

cpof(c::Char) = Int32(UInt32(c))

mutable struct Parser
    pattern::String
    p::Vector{Char}
    unicode::Bool
    # The position, which counts characters from zero.
    i::Int
    depth::Int
    # The number of groups and whether any is named, from a scan of the whole pattern before it is read, as the
    # meaning of \1 and \k depends on groups that may follow them.
    groupcount::Int
    hasnames::Bool
    groups::Int
    # For each group name: where the last group of the name is written (the position of the end of its name), then
    # the numbers of its groups.
    names::Dict{String,Vector{Int}}
    namedrefs::Vector{Node}
    # The disjunctions that enclose the current position, innermost last: for each, where it starts and where its
    # current alternative starts.
    path::Vector{Int}
    # The modifiers in force, which only a group such as (?i:...) changes.
    ignorecase::Bool
    multiline::Bool
    dotall::Bool
    # The ranges a class has gathered so far.
    pairs::Vector{Int32}
    # The set of the class atom `classatom` last read, when it was a class escape, and nothing otherwise.
    atomset::Union{Nothing,CodeSet}
    # The terms and the alternatives read so far.
    terms::Int
end

function Parser(pattern::String, unicode::Bool)
    p = Char[]
    for c in pattern
        if Base.ismalformed(c) || Base.isoverlong(c) || UInt32(c) > 0x10FFFF
            throw(PatternError(pattern, "it is not UTF-8", false))
        end
        if !unicode && UInt32(c) > 0xFFFF
            # Without the u flag a pattern is a sequence of UTF-16 code units, each an atom of its own.
            v = UInt32(c) - 0x10000
            push!(p, Char(0xD800 + (v >> 10)))
            push!(p, Char(0xDC00 + (v & 0x3FF)))
        else
            push!(p, c)
        end
    end
    return Parser(pattern, p, unicode, 0, 0, 0, false, 0, Dict{String,Vector{Int}}(), Node[], Int[], false,
        false, false, Int32[], nothing, 0)
end

syntaxerror(P::Parser, message::String) = throw(PatternError(P.pattern, string(message, " at ", P.i), false))

function parse!(P::Parser)
    scangroups!(P)
    root = disjunction(P)
    if P.i < length(P.p)
        syntaxerror(P, P.p[P.i + 1] == ')' ? "unmatched ')'" : "unexpected character")
    end
    for ref in P.namedrefs
        named = get(P.names, ref.name, nothing)
        named === nothing && syntaxerror(P, "reference to an unknown group name")
        ref.groups = named[2:end]
    end
    return root
end

# Counts the capturing groups of the pattern and notes whether any is named.
function scangroups!(P::Parser)
    p = P.p
    n = length(p)
    inclass = false
    k = 0
    while k < n
        c = p[k + 1]
        if c == '\\'
            k += 1
        elseif c == '['
            inclass = true
        elseif c == ']'
            inclass = false
        elseif c == '(' && !inclass
            if k + 1 >= n || p[k + 2] != '?'
                P.groupcount += 1
            elseif k + 3 < n && p[k + 3] == '<' && p[k + 4] != '=' && p[k + 4] != '!'
                P.groupcount += 1
                P.hasnames = true
            end
        end
        k += 1
    end
    return nothing
end

more(P::Parser) = P.i < length(P.p)

peekchar(P::Parser) = P.i < length(P.p) ? P.p[P.i + 1] : END_OF_PATTERN

# The character at a position that counts from zero, which must be inside the pattern.
charat(P::Parser, k::Int) = P.p[k + 1]

function eat(P::Parser, c::Char)
    if P.i < length(P.p) && P.p[P.i + 1] == c
        P.i += 1
        return true
    end
    return false
end

function disjunction(P::Parser)
    P.depth += 1
    # Not a syntax error: nothing is wrong with the pattern, and nothing is known of the rest of it.
    P.depth > MAX_NESTING + 1 && throw(TooLargeError(P.pattern,
        string("its groups nest over the limit of ", MAX_NESTING, " deep"), false))
    push!(P.path, P.i, P.i)
    level = length(P.path)
    result = alternative(P)
    if peekchar(P) == '|'
        alternatives = Node[result]
        while eat(P, '|')
            P.path[level] = P.i
            push!(alternatives, alternative(P))
        end
        result = Node(ALT)
        result.subs = alternatives
    end
    resize!(P.path, level - 2)
    P.depth -= 1
    return result
end

# Counts a term or an alternative. Past the limit the pattern is too large to read, which is not a syntax error.
function counted!(P::Parser)
    P.terms += 1
    P.terms > MAX_PATTERN_TERMS && throw(TooLargeError(P.pattern,
        string("it has over the limit of ", MAX_PATTERN_TERMS, " terms and alternatives"), false))
    return nothing
end

function alternative(P::Parser)
    terms = Node[]
    counted!(P)
    while more(P) && peekchar(P) != '|' && peekchar(P) != ')'
        push!(terms, term(P))
        counted!(P)
    end
    isempty(terms) && return Node(EMPTY)
    length(terms) == 1 && return terms[1]
    node = Node(CAT)
    node.subs = terms
    return node
end

# Where the translation of a pattern without the u flag keeps the surrogates: the code unit U+D800 + n is the
# character U+F0000 + n, in the pattern and in the text (see `codeunits!` in EcmaRegex.jl).
const UNIT_BASE = Int32(0xF0000)
const SURROGATES = Int32[0xD800, 0xDFFF]
const UNITS = Int32[UNIT_BASE, UNIT_BASE + 0x7FF]

# The set of a pattern without the u flag as the translation holds it. Such a pattern matches UTF-16 code units, and
# the text it is matched against has each half of a surrogate pair written as one character from UNIT_BASE. So the
# surrogates of the set go there. What the set holds of the other characters beyond the Basic Multilingual Plane is
# left as it is: the text has none of them. A set with every surrogate and every unit, or with none of either, is
# not touched, so a pattern that names no surrogate is written as it was.
function unitset(set::CodeSet)
    surrogates = covered(set, SURROGATES)
    if surrogates == covered(set, UNITS) && surrogates != SOME
        return set
    end
    moved = Int32[c - Int32(0xD800) + UNIT_BASE for c in intersection(set, SURROGATES)]
    return setunion(difference(set, UNITS), moved)
end

const NONE = 0
const SOME = 1
const EVERY = 2

# How much of one range a set holds: none of it, some of it or every character of it.
function covered(set::CodeSet, range::CodeSet)
    lo = range[1]
    hi = range[2]
    for k in 1:2:length(set)
        (set[k+1] < lo || set[k] > hi) && continue
        return set[k] <= lo && set[k+1] >= hi ? EVERY : SOME
    end
    return NONE
end

# A node for a set as the translation holds it.
function setnode(set::CodeSet)
    node = Node(SET)
    node.set = set
    return node
end

# A node for a set the parser read.
setnode(P::Parser, set::CodeSet) = setnode(P.unicode ? set : unitset(set))

# The node for one literal character. Inside a case-insensitive group that is the set of the characters equivalent
# to it.
function charnode(P::Parser, c::Char)
    cp = cpof(c)
    if P.ignorecase
        set = foldclosure(Int32[cp, cp], P.unicode)
        if length(set) != 2 || set[1] != set[2]
            return setnode(P, set)
        end
    end
    node = Node(CHAR)
    node.codepoint = !P.unicode && cp >= 0xD800 && cp <= 0xDFFF ? cp - Int32(0xD800) + UNIT_BASE : cp
    return node
end

# The set that also holds, inside a case-insensitive group, every character equivalent to a member.
folded(P::Parser, set::CodeSet) = P.ignorecase ? foldclosure(set, P.unicode) : set

function term(P::Parser)
    c = peekchar(P)
    groupsbefore = P.groups
    at = P.i
    quantifiable = true
    if c == '^'
        P.i += 1
        atom = Node(P.multiline ? LINE_START : BOL)
        quantifiable = false
    elseif c == '$'
        P.i += 1
        atom = Node(P.multiline ? LINE_END : EOL)
        quantifiable = false
    elseif c == '('
        atom = group(P)
        # Annex B lets a quantifier follow a lookahead. The u flag does not, and neither lets one follow a
        # lookbehind.
        quantifiable = atom.kind != LOOK || (!P.unicode && !atom.behind)
    elseif c == '.'
        P.i += 1
        atom = setnode(P, P.dotall ? ANY_SET : DOT_SET)
    elseif c == '['
        atom = characterclass(P)
    elseif c == '\\'
        atom = atomescape(P)
        quantifiable = atom.kind != WORD_BOUNDARY && atom.kind != NOT_WORD_BOUNDARY
    elseif c == '*' || c == '+' || c == '?'
        syntaxerror(P, "nothing to repeat")
    elseif c == '{'
        if P.unicode || lookslikequantifier(P)
            syntaxerror(P, "nothing to repeat")
        end
        P.i += 1
        atom = charnode(P, '{')
    elseif c == ']' || c == '}'
        P.unicode && syntaxerror(P, "lone bracket")
        P.i += 1
        atom = charnode(P, c)
    else
        P.i += 1
        atom = charnode(P, c)
    end
    atom.firstgroup = groupsbefore
    atom.lastgroup = P.groups
    rep = quantifier(P)
    rep === nothing && return atom
    if !quantifiable
        P.i = at
        syntaxerror(P, "nothing to repeat")
    end
    rep.subs = Node[atom]
    rep.firstgroup = groupsbefore
    rep.lastgroup = P.groups
    return rep
end

isdigitchar(c::Char) = c >= '0' && c <= '9'

# Whether the text at the current '{' reads as {n}, {n,} or {n,m}.
function lookslikequantifier(P::Parser)
    p = P.p
    n = length(p)
    k = P.i + 1
    start = k
    while k < n && isdigitchar(p[k + 1])
        k += 1
    end
    if k == start || k >= n
        return false
    end
    p[k + 1] == '}' && return true
    p[k + 1] != ',' && return false
    k += 1
    while k < n && isdigitchar(p[k + 1])
        k += 1
    end
    return k < n && p[k + 1] == '}'
end

# Reads a run of decimal digits, which saturates at MAX_COUNT.
function number(P::Parser)
    start = P.i
    v = 0
    while more(P) && isdigitchar(peekchar(P))
        v = min(v * 10 + (peekchar(P) - '0'), MAX_COUNT)
        P.i += 1
    end
    start == P.i && syntaxerror(P, "expected a number")
    return v
end

# Whether the decimal number at the positions from `a` to before `aend` is greater than the one from `b` to before
# `bend`, for numbers of any length.
function greaternumber(P::Parser, a::Int, aend::Int, b::Int, bend::Int)
    while a < aend - 1 && charat(P, a) == '0'
        a += 1
    end
    while b < bend - 1 && charat(P, b) == '0'
        b += 1
    end
    aend - a == bend - b || return aend - a > bend - b
    for k in 0:(aend - a - 1)
        x = charat(P, a + k)
        y = charat(P, b + k)
        x == y || return x > y
    end
    return false
end

# Reads a quantifier as a REPEAT with no child yet, or returns nothing when there is none.
function quantifier(P::Parser)
    c = peekchar(P)
    rep = Node(REPEAT)
    if c == '*'
        P.i += 1
        rep.max = UNBOUNDED
    elseif c == '+'
        P.i += 1
        rep.min = 1
        rep.max = UNBOUNDED
    elseif c == '?'
        P.i += 1
        rep.max = 1
    elseif c == '{' && lookslikequantifier(P)
        P.i += 1
        first = P.i
        rep.min = number(P)
        firstend = P.i
        rep.max = rep.min
        if eat(P, ',') && peekchar(P) == '}'
            rep.max = UNBOUNDED
        elseif P.i > firstend
            second = P.i
            rep.max = number(P)
            # The digits are compared, as a count is kept only up to MAX_COUNT: a{2147483648,2147483647} is an error.
            if greaternumber(P, first, firstend, second, P.i)
                syntaxerror(P, "numbers out of order in quantifier")
            end
        end
        eat(P, '}') || syntaxerror(P, "malformed quantifier")
    else
        return nothing
    end
    rep.lazy = eat(P, '?')
    return rep
end

function closegroup(P::Parser)
    eat(P, ')') || syntaxerror(P, "unterminated group")
    return nothing
end

# Reads a group or a lookaround.
function group(P::Parser)
    P.i += 1
    eat(P, '?') || return capture(P, nothing)
    if eat(P, ':')
        node = Node(GROUP)
        node.subs = Node[disjunction(P)]
        closegroup(P)
        return node
    end
    c = peekchar(P)
    if c == 'i' || c == 'm' || c == 's' || c == '-'
        return modifiergroup(P)
    end
    if eat(P, '=') || eat(P, '!')
        look = Node(LOOK)
        look.negative = charat(P, P.i - 1) == '!'
        look.subs = Node[disjunction(P)]
        closegroup(P)
        return look
    end
    if eat(P, '<')
        if eat(P, '=') || eat(P, '!')
            look = Node(LOOK)
            look.behind = true
            look.negative = charat(P, P.i - 1) == '!'
            look.subs = Node[disjunction(P)]
            closegroup(P)
            return look
        end
        name = groupname(P)
        # Two groups may share a name only when they are in separate alternatives of one disjunction, so that no
        # match can go through both. The group is compared with the last group of the name, which decides it for
        # every group of the name: each was apart from the one before it, and a group that is apart from the last
        # one is apart from those too. (Were it not apart from an earlier one, one alternative would hold the two,
        # and the last one, which is written between them. The last one is apart from the group, so it is in
        # another alternative of a disjunction inside that alternative, around the group. The earlier one is
        # outside that disjunction, or it would be apart from the group as well, so it and the last one would be
        # in the one alternative, and not apart.) Comparing with every group of the name took time in proportion
        # to the square of their number.
        others = get(P.names, name, nothing)
        if others !== nothing
            separatealternatives(P, others[1]) || syntaxerror(P, "duplicate group name")
        end
        return capture(P, name)
    end
    syntaxerror(P, "invalid group")
end

# Whether the position `at`, which is before the current one, lies in a different alternative of some disjunction
# than the current position does. The innermost disjunction around the current position that started at or before
# `at` holds both. They are apart when its current alternative started after `at`.
function separatealternatives(P::Parser, at::Int)
    path = P.path
    for level in length(path):-2:2
        path[level - 1] <= at && return path[level] > at
    end
    return false
end

# Reads a group that turns modifiers on or off for its body, such as (?i:...) or (?s-i:...). The position is after
# the "(?".
function modifiergroup(P::Parser)
    savedignorecase = P.ignorecase
    savedmultiline = P.multiline
    saveddotall = P.dotall
    seen = 0
    removing = false
    while !eat(P, ':')
        c = peekchar(P)
        P.i += 1
        if c == '-'
            removing && syntaxerror(P, "invalid group modifier")
            removing = true
            continue
        end
        flag = c == 'i' ? 1 : c == 'm' ? 2 : c == 's' ? 4 : 0
        flag == 0 && syntaxerror(P, "invalid group modifier")
        (seen & flag) != 0 && syntaxerror(P, "repeated group modifier")
        seen |= flag
        if flag == 1
            P.ignorecase = !removing
        elseif flag == 2
            P.multiline = !removing
        else
            P.dotall = !removing
        end
    end
    seen == 0 && syntaxerror(P, "invalid group modifier")
    node = Node(GROUP)
    node.subs = Node[disjunction(P)]
    closegroup(P)
    P.ignorecase = savedignorecase
    P.multiline = savedmultiline
    P.dotall = saveddotall
    return node
end

function capture(P::Parser, name::Union{Nothing,String})
    node = Node(GROUP)
    P.groups += 1
    node.group = P.groups
    if name !== nothing
        # The body of the group starts after this position, so the group is not in its own body.
        named = get!(() -> Int[0], P.names, name)
        named[1] = P.i - 1
        push!(named, node.group)
    end
    node.subs = Node[disjunction(P)]
    closegroup(P)
    return node
end

# Reads a group name up to and including its '>'. A character of the name may be written as a Unicode escape.
function groupname(P::Parser)
    name = IOBuffer()
    count = 0
    while !eat(P, '>')
        more(P) || syntaxerror(P, "invalid group name")
        c = cpof(peekchar(P))
        P.i += 1
        if c == Int32('\\')
            eat(P, 'u') || syntaxerror(P, "invalid group name")
            c = unicodeescape(P, true, true)
            c < 0 && syntaxerror(P, "invalid group name")
        elseif !P.unicode && c >= 0xD800 && c <= 0xDBFF && more(P) && cpof(peekchar(P)) >= 0xDC00 &&
               cpof(peekchar(P)) <= 0xDFFF
            # A name is read by character in both grammars.
            c = Int32(0x10000) + ((c - Int32(0xD800)) << 10) + (cpof(peekchar(P)) - Int32(0xDC00))
            P.i += 1
        end
        if count == 0 ? !isnamestart(c) : !isnamepart(c)
            syntaxerror(P, "invalid group name")
        end
        print(name, Char(UInt32(c)))
        count += 1
    end
    count == 0 && syntaxerror(P, "invalid group name")
    return String(take!(name))
end

# Reads an escape outside a class.
function atomescape(P::Parser)
    P.i += 1
    more(P) || syntaxerror(P, "\\ at end of pattern")
    c = peekchar(P)
    if c == 'b' || c == 'B'
        P.i += 1
        boundary = Node(c == 'b' ? WORD_BOUNDARY : NOT_WORD_BOUNDARY)
        # With the u flag grammar a case-insensitive word boundary counts two more characters as word characters.
        boundary.fold = P.unicode && P.ignorecase
        return boundary
    end
    if c == 'k'
        P.i += 1
        # Annex B reads \k as the letter k in a pattern with no named group.
        if !P.unicode && !P.hasnames
            return charnode(P, 'k')
        end
        eat(P, '<') || syntaxerror(P, "invalid named reference")
        ref = Node(BACKREF)
        ref.name = groupname(P)
        ref.fold = P.ignorecase
        push!(P.namedrefs, ref)
        return ref
    end
    if c >= '1' && c <= '9'
        start = P.i
        n = number(P)
        if n <= P.groupcount
            ref = Node(BACKREF)
            ref.groups = Int[n]
            ref.fold = P.ignorecase
            return ref
        end
        P.unicode && syntaxerror(P, "reference to a group that does not exist")
        # Annex B reads it as an octal escape, or as the digit itself.
        P.i = start
        return charnode(P, Char(UInt32(legacyoctal(P))))
    end
    set = classescape(P, c)
    set === nothing || return setnode(P, folded(P, set))
    return charnode(P, Char(UInt32(characterescape(P, false))))
end

# Reads an Annex B octal escape of up to three digits with a value below 256. A digit that cannot start one (8 or 9)
# stands for itself.
function legacyoctal(P::Parser)
    v = Int32(0)
    k = 0
    while k < 3 && more(P) && peekchar(P) >= '0' && peekchar(P) <= '7' && v * 8 + (peekchar(P) - '0') <= 0o377
        v = v * Int32(8) + Int32(peekchar(P) - '0')
        P.i += 1
        k += 1
    end
    if k == 0
        c = cpof(peekchar(P))
        P.i += 1
        return c
    end
    return v
end

# Reads a class escape such as \d or \p{...} whose letter is `c`, as a set. Returns nothing, reading nothing, when
# `c` does not start one.
function classescape(P::Parser, c::Char)
    if c == 'd'
        P.i += 1
        return DIGIT_SET
    elseif c == 'D'
        P.i += 1
        return NOT_DIGIT_SET
    elseif c == 'w'
        P.i += 1
        return P.unicode && P.ignorecase ? FOLD_WORD_SET : WORD_SET
    elseif c == 'W'
        P.i += 1
        return P.unicode && P.ignorecase ? NOT_FOLD_WORD_SET : NOT_WORD_SET
    elseif c == 's'
        P.i += 1
        return SPACE_SET
    elseif c == 'S'
        P.i += 1
        return NOT_SPACE_SET
    elseif c == 'p' || c == 'P'
        # Without the u flag \p is the letter p.
        P.unicode || return nothing
        P.i += 1
        eat(P, '{') || syntaxerror(P, "invalid property name")
        start = P.i
        while more(P) && peekchar(P) != '}'
            P.i += 1
        end
        expression = UInt32[UInt32(P.p[k]) for k in (start + 1):P.i]
        eat(P, '}') || syntaxerror(P, "invalid property name")
        set = property(expression, 1, length(expression))
        set === nothing && syntaxerror(P, "invalid property name")
        return c == 'P' ? complement(set) : set
    end
    return nothing
end

function hexvalue(c::Char)
    if c >= '0' && c <= '9'
        return c - '0'
    elseif c >= 'a' && c <= 'f'
        return c - 'a' + 10
    elseif c >= 'A' && c <= 'F'
        return c - 'A' + 10
    end
    return -1
end

# Reads exactly `digits` hexadecimal digits. Returns -1, reading nothing, when they are not there.
function hex(P::Parser, digits::Int)
    P.i + digits > length(P.p) && return Int32(-1)
    v = 0
    for k in 0:(digits - 1)
        d = hexvalue(charat(P, P.i + k))
        d < 0 && return Int32(-1)
        v = v * 16 + d
    end
    P.i += digits
    return Int32(v)
end

const SYNTAX_CHARACTERS = "^\$\\.*+?()[]{}|/"

# Reads the escape after a backslash, in or out of a class, and returns the code point it names.
function characterescape(P::Parser, inclass::Bool)
    c = charat(P, P.i)
    P.i += 1
    if c == 'f'
        return Int32('\f')
    elseif c == 'n'
        return Int32('\n')
    elseif c == 'r'
        return Int32('\r')
    elseif c == 't'
        return Int32('\t')
    elseif c == 'v'
        return Int32(0x0b)
    elseif c == 'c'
        l = peekchar(P)
        if (l >= 'a' && l <= 'z') || (l >= 'A' && l <= 'Z')
            P.i += 1
            return cpof(l) % Int32(32)
        end
        if !P.unicode && inclass && (isdigitchar(l) || l == '_')
            P.i += 1
            return cpof(l) % Int32(32)
        end
        P.unicode && syntaxerror(P, "invalid control escape")
        # Annex B reads a \c that is not a control escape as a backslash followed by the letter c.
        P.i -= 1
        return Int32('\\')
    elseif c == '0'
        if more(P) && isdigitchar(peekchar(P))
            P.unicode && syntaxerror(P, "invalid decimal escape")
            P.i -= 1
            return legacyoctal(P)
        end
        return Int32(0)
    elseif c == 'x'
        h = hex(P, 2)
        if h < 0
            P.unicode && syntaxerror(P, "invalid \\x escape")
            return Int32('x')
        end
        return h
    elseif c == 'u'
        u = unicodeescape(P, P.unicode, P.unicode)
        if u < 0
            P.unicode && syntaxerror(P, "invalid \\u escape")
            return Int32('u')
        end
        return u
    end
    if c >= '1' && c <= '9' && !P.unicode && inclass
        P.i -= 1
        return legacyoctal(P)
    end
    # Identity escapes. The u flag allows only the syntax characters and '/', and '-' in a class. Annex B allows any
    # character, but for k in a pattern with a named group, where \k is a reference to one and nothing else. So
    # `(?<n>a)[\k]` is an error, and `(a)[\k]` is a class of the letter k.
    if P.unicode && !(c in SYNTAX_CHARACTERS) && !(inclass && c == '-')
        syntaxerror(P, "invalid escape")
    end
    if c == 'k' && P.hasnames
        syntaxerror(P, "invalid escape")
    end
    return cpof(c)
end

# Reads what follows the u of a Unicode escape and returns the code point, or -1, reading nothing, when it is not a
# well-formed escape. With `braces` it accepts the form with braces. With `pairs` a surrogate pair written as two
# escapes is one code point. Without the u flag it is that in a group name only, and two code units anywhere else.
function unicodeescape(P::Parser, braces::Bool, pairs::Bool)
    start = P.i
    if braces && eat(P, '{')
        digits = P.i
        v = 0
        while more(P) && hexvalue(peekchar(P)) >= 0
            if v <= MAX_CODE_POINT
                v = v * 16 + hexvalue(peekchar(P))
            end
            P.i += 1
        end
        if digits == P.i || !eat(P, '}') || v > MAX_CODE_POINT
            P.i = start
            return Int32(-1)
        end
        return Int32(v)
    end
    u = hex(P, 4)
    u < 0 && return Int32(-1)
    if pairs && u >= 0xD800 && u <= 0xDBFF && P.i + 6 <= length(P.p) && charat(P, P.i) == '\\' && charat(P, P.i + 1) == 'u'
        save = P.i
        P.i += 2
        low = hex(P, 4)
        if low >= 0xDC00 && low <= 0xDFFF
            return Int32(0x10000) + ((u - Int32(0xD800)) << 10) + (low - Int32(0xDC00))
        end
        P.i = save
    end
    return u
end

function addclassatom!(P::Parser, c::Int32, set::Union{Nothing,CodeSet})
    if set === nothing
        push!(P.pairs, c, c)
    else
        append!(P.pairs, set)
    end
    return nothing
end

# Reads a character class from its '[' to its ']'.
function characterclass(P::Parser)
    P.i += 1
    negated = eat(P, '^')
    empty!(P.pairs)
    while true
        more(P) || syntaxerror(P, "unterminated character class")
        eat(P, ']') && break
        lo = classatom(P)
        loset = P.atomset
        if peekchar(P) == '-' && P.i + 1 < length(P.p) && charat(P, P.i + 1) != ']'
            P.i += 1
            hi = classatom(P)
            hiset = P.atomset
            if loset !== nothing || hiset !== nothing
                P.unicode && syntaxerror(P, "invalid character class range")
                # Annex B reads the '-' as itself when a class escape is at either end.
                addclassatom!(P, lo, loset)
                push!(P.pairs, Int32('-'), Int32('-'))
                addclassatom!(P, hi, hiset)
                continue
            end
            hi < lo && syntaxerror(P, "range out of order in character class")
            push!(P.pairs, lo, hi)
            continue
        end
        addclassatom!(P, lo, loset)
    end
    # Inside a case-insensitive group a character matches the class when it is equivalent to a member, and a
    # negated class excludes exactly those characters.
    set = folded(P, setof(P.pairs))
    return setnode(P, negated ? complement(set) : set)
end

# Reads one member of a class. Returns its code point, or sets `atomset` for a class escape.
function classatom(P::Parser)
    P.atomset = nothing
    c = charat(P, P.i)
    P.i += 1
    c != '\\' && return cpof(c)
    more(P) || syntaxerror(P, "\\ at end of pattern")
    e = peekchar(P)
    if e == 'b'
        P.i += 1
        return Int32('\b')
    end
    P.atomset = classescape(P, e)
    P.atomset === nothing || return Int32(0)
    return characterescape(P, true)
end
