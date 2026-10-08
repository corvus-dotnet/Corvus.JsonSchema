# Reads an ECMA-262 pattern by recursive descent into a tree. The grammar is that of ECMAScript 2025. With `unicode`
# the parser applies the grammar of the u flag, and without it the Annex B grammar of a pattern with no flag.

# Groups may nest this deep. It bounds the recursion of the readers.
const MAX_NESTING = 200

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

# What is known, where a backreference stands, of whether a group it names has taken part.
const NEVER = 0
const MAYBE = 1
const DEFINITE = 2

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
    # Marks a REPEAT whose last iteration is written apart from the ones before it.
    lastapart::Bool
    # The number of the capturing group of a GROUP, counted from 1 as ECMA-262 does, or 0.
    group::Int
    # The groups a BACKREF names. A name can belong to several groups in separate alternatives, of which at most one
    # has taken part at any time.
    groups::Vector{Int}
    # For each of the groups, what is known of whether it has taken part (NEVER, MAYBE or DEFINITE).
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

Node(kind::Int) = Node(kind, Int32(0), NO_CHARACTERS, NO_NODES, 0, 0, false, false, 0, NO_INTS, NO_INTS, NO_BOOLS, "",
    false, false, false, 0, 0, Int64(-1), Int64(0))

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
    # For each group name, its groups: the number of each, then the alternatives that enclose it.
    names::Dict{String,Vector{Vector{Int}}}
    namedrefs::Vector{Node}
    # The alternatives that enclose the current position, innermost last, as disjunction and alternative.
    path::Vector{Int}
    disjunctions::Int
    # The modifiers in force, which only a group such as (?i:...) changes.
    ignorecase::Bool
    multiline::Bool
    dotall::Bool
    # The ranges a class has gathered so far.
    pairs::Vector{Int32}
    # The set of the class atom `classatom` last read, when it was a class escape, and nothing otherwise.
    atomset::Union{Nothing,CodeSet}
end

function Parser(pattern::String, unicode::Bool)
    p = Char[]
    for c in pattern
        if Base.ismalformed(c) || Base.isoverlong(c) || UInt32(c) > 0x10FFFF
            throw(PatternError(pattern, "it is not UTF-8", false))
        end
        push!(p, c)
    end
    return Parser(pattern, p, unicode, 0, 0, 0, false, 0, Dict{String,Vector{Vector{Int}}}(), Node[], Int[], 0, false,
        false, false, Int32[], nothing)
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
        ref.groups = Int[other[1] for other in named]
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

peekchar(P::Parser) = P.i < length(P.p) ? @inbounds(P.p[P.i + 1]) : END_OF_PATTERN

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
    P.depth > MAX_NESTING && syntaxerror(P, "groups nested too deeply")
    P.disjunctions += 1
    push!(P.path, P.disjunctions, 0)
    level = length(P.path)
    result = alternative(P)
    if peekchar(P) == '|'
        alternatives = Node[result]
        while eat(P, '|')
            P.path[level] += 1
            push!(alternatives, alternative(P))
        end
        result = Node(ALT)
        result.subs = alternatives
    end
    resize!(P.path, level - 2)
    P.depth -= 1
    return result
end

function alternative(P::Parser)
    terms = Node[]
    while more(P) && peekchar(P) != '|' && peekchar(P) != ')'
        push!(terms, term(P))
    end
    isempty(terms) && return Node(EMPTY)
    length(terms) == 1 && return terms[1]
    node = Node(CAT)
    node.subs = terms
    return node
end

function setnode(set::CodeSet)
    node = Node(SET)
    node.set = set
    return node
end

# The node for one literal character. Inside a case-insensitive group that is the set of the characters equivalent
# to it.
function charnode(P::Parser, c::Char)
    cp = cpof(c)
    if P.ignorecase
        set = foldclosure(Int32[cp, cp], P.unicode)
        if length(set) != 2 || set[1] != set[2]
            return setnode(set)
        end
    end
    node = Node(CHAR)
    node.codepoint = cp
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
        atom = setnode(P.dotall ? ANY_SET : DOT_SET)
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
        rep.min = number(P)
        rep.max = rep.min
        if eat(P, ',')
            rep.max = peekchar(P) == '}' ? UNBOUNDED : number(P)
        end
        eat(P, '}') || syntaxerror(P, "malformed quantifier")
        if rep.max != UNBOUNDED && rep.max < rep.min
            syntaxerror(P, "numbers out of order in quantifier")
        end
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
        # match can go through both.
        others = get(P.names, name, nothing)
        if others !== nothing
            for other in others
                separatealternatives(P, other) || syntaxerror(P, "duplicate group name")
            end
        end
        return capture(P, name)
    end
    syntaxerror(P, "invalid group")
end

# Whether a named group (its number, then the alternatives that enclose it) and the current position lie in
# different alternatives of some disjunction.
function separatealternatives(P::Parser, other::Vector{Int})
    path = P.path
    k = 0
    while 2k + 2 < length(other) && 2k < length(path) && other[2k + 2] == path[2k + 1]
        other[2k + 3] != path[2k + 2] && return true
        k += 1
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
        named = Int[node.group]
        append!(named, P.path)
        push!(get!(() -> Vector{Int}[], P.names, name), named)
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
            c = unicodeescape(P, true)
            c < 0 && syntaxerror(P, "invalid group name")
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
    set === nothing || return setnode(folded(P, set))
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
        u = unicodeescape(P, P.unicode)
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
    # character.
    if P.unicode && !(c in SYNTAX_CHARACTERS) && !(inclass && c == '-')
        syntaxerror(P, "invalid escape")
    end
    return cpof(c)
end

# Reads what follows the u of a Unicode escape and returns the code point, or -1, reading nothing, when it is not a
# well-formed escape. With `braces` it accepts the form with braces. A surrogate pair written as two escapes is one
# code point.
function unicodeescape(P::Parser, braces::Bool)
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
    if u >= 0xD800 && u <= 0xDBFF && P.i + 6 <= length(P.p) && charat(P, P.i) == '\\' && charat(P, P.i + 1) == 'u'
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
    return setnode(negated ? complement(set) : set)
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
