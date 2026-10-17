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
# - a group is written without capture unless a backreference reads it, and under a name the writer gives it, so a
#   name of the pattern may be any ECMA-262 identifier and may be shared by groups in separate alternatives;
# - a lookbehind is written as it is when the length of its body is fixed, which is all PCRE2 10.42 accepts (10.43
#   and later accept more, within limits). Any other lookbehind is a callout (see `lookbehind`).
#
# # Backreferences
#
# In ECMA-262 a backreference matches the text its group captured when the group last took part, and the empty
# string when the group has not taken part. The groups of a repeated body stop having taken part when an iteration
# starts, and those of a negative lookaround when it is done. PCRE2 fails a backreference to a group that is not
# set, and keeps what a group captured in an earlier iteration. The writer makes the two agree in three steps.
#
# 1. `flow!` finds, for each backreference, whether its group can have taken part where the backreference stands.
#    It cannot when the backreference is inside the group, before it, in another alternative, after a negative
#    lookaround that holds the group, or after a repetition that holds it and never runs. Such a backreference
#    matches the empty string and is written as nothing. So is one to a group that can only capture the empty
#    string. Every other backreference is LIVE.
# 2. A LIVE backreference stands after its group in a sequence. On every way from the start of the iteration they
#    share to the backreference, the group is given a value. Where the way goes through the group, PCRE2 sets it.
#    Where it goes round the group, the writer sets it to the empty string, which a backreference reads as it
#    reads no value. An alternation with a group in one alternative is written (?|(?<g1>a)|b(?<g1>)): the group
#    (?|...) of PCRE2 numbers the groups of each alternative from the same number, so the two groups are one. A
#    repetition X? or X* that can run no times is written (?|(?:X)+|(?<g1>)) for the same reason (see `alternatives`
#    and `writerepeat`). So the group PCRE2 reads always has the value of the iteration the backreference is in,
#    and never one an earlier iteration left.
# 3. ECMA-262 refuses an iteration that matches the empty string once the minimum count is met. PCRE2 takes one
#    and leaves the repetition, with the groups as that iteration set them. `writerepeat` says how a repetition
#    whose groups are read after it is written so that no such iteration is taken.
#
# What a group captured is the same in both engines otherwise, as both put a group back when they backtrack out of
# it, and a lookaround is atomic in both.
#
# # Lookbehinds and backreferences
#
# ECMA-262 matches the body of a lookbehind backwards, from its end to its start. PCRE2 matches it forwards from a
# place it steps back to. Whether the body matches is the same either way. What its groups capture is not, and a
# backreference inside the body reads the groups in the other order.
#
# - A lookbehind of a fixed length is written as it is, and a backreference after it may read its groups. Every
#   element of such a body is at a place the length fixes and none of them reads a group, so each captures the same
#   text in either direction. The one exception is a repetition, whose groups are left as its last iteration set
#   them, which is the leftmost in ECMA-262 (see `writerepeat`).
# - A lookbehind that holds a LIVE backreference to a group outside it is written as a search that PCRE2 runs
#   itself (see `walk`), as the body compiled apart for a callout could not read the group.
# - A backreference inside a lookbehind to a group of the same lookbehind that ECMA-262 matches before it (one to
#   its right), such as (?<=\1(a)), is written by matching the end of the lookbehind first, where that end has a
#   fixed length (see `split!`).
# - A lookbehind of no fixed length with a group that a backreference after it reads is written so that PCRE2
#   sets the group to what ECMA-262 captures, where that can be told without matching backwards. A group in an end
#   of the lookbehind of a fixed length is at a place the length fixes, whatever stands before it, as in
#   (?<=a*(x))-\1 (see `rewrite!`). A lookbehind that is a term of the pattern itself may also start with a
#   repetition of a body of a fixed length, or with alternatives of fixed lengths, or be alternatives of such
#   forms, as in (?<=([abc])+)x\1. It is then a search for where it starts, with the rest of the pattern inside
#   the search (see `continuation` and `writeedge`).
# - A part of the pattern that can never match is taken out before any of this, and so are the alternatives ECMA-262
#   never takes in what a lookbehind matches last (see `prune!` and `trim!`). A group in such a part never takes
#   part.
# - Two cases are refused, and the error says which. A backreference inside a lookbehind to a group of it that
#   ECMA-262 matches before it, where the group is not in an end of a fixed length, such as (?<=\1(a+)), as PCRE2
#   would read the group before setting it. And a backreference to a group of a lookbehind of no fixed length
#   where none of the forms above applies, such as (?<=(a*)(x+))-\1 or (?:(?<=(a)+)x\1)+: what the group holds
#   then depends on the order in which ECMA-262 tries the ways through the lookbehind from its end.
#
# None of this is written for a pattern with no LIVE backreference, and none of it adds to its matching.

# The backreference is in a lookbehind, with a group that ECMA-262 matches before it and PCRE2 after it.
const REFUSED_BACKREFERENCE_IN_LOOKBEHIND =
    "a backreference inside a lookbehind to a group that the lookbehind matches before it"
# The group is in a lookbehind of no fixed length, which ECMA-262 matches backwards, and what it captures that way is
# not what any of the forms of `rewrite!` and `writeedge` captures.
const REFUSED_GROUP_IN_LOOKBEHIND =
    "a backreference to a group inside a lookbehind of no fixed length, where the group holds what only matching the lookbehind backwards can tell"
# The backreference ignores case, the pattern is only valid with no flag, and the group can hold a character that
# ECMA-262 compares differently there than PCRE2 does.
const REFUSED_CASE_INSENSITIVE_BACKREFERENCE =
    "a case-insensitive backreference, in a pattern that is not valid with the u flag, to text that PCRE2 compares differently"
# PCRE2 did not compile the translation, and not for its size. The error message carries what PCRE2 said.
const REFUSED_BY_ENGINE = "a pattern PCRE2 does not compile"

unsupported(E, reason::String) = throw(PatternError(E.pattern, reason, true))

# The limits on the size of what is written for a pattern. A pattern over one is valid, and is a TooLargeError.
#
# A repetition whose groups are read after it is written twice (see `writerepeat`), and the copies multiply. Or a
# repetition has a minimum no text reaches and a body that can match the empty string.
const TOO_LARGE_REPETITION = "a repetition that is written out is over the limit of 65536 bytes"
const TOO_LARGE_COUNT = "a repetition of what can match the empty string has a count over the limit of 2147483646"
# The translation is at most this many bytes, with the bodies of its lookbehinds, and so is each part of it as it
# is written. It is the limit on what PCRE2 is given. PCRE2, as Julia builds it, holds a compiled pattern of at
# most 64 KiB, so it refuses most translations of this length itself, and that is a TooLargeError too.
const MAX_TRANSLATION_LENGTH = 1 << 20
const TOO_LARGE_TRANSLATION = "its translation is over the limit of 1048576 bytes"
# The nodes of the pattern the emitter may write, counting each as often as it is written. A pattern is at most
# MAX_PATTERN_LENGTH bytes, and its translation writes a node a few times at most, but for a repetition that is
# written twice, where the copies multiply with the depth: seventy of them, one inside another, would be written
# 2^70 times before the translation was found too large. PCRE2 holds a compiled pattern of at most 64 KiB, which
# is 65,536 items at most, so a pattern PCRE2 takes is nowhere near this many.
const MAX_TRANSLATION_STEPS = 1 << 19
const TOO_LARGE_STEPS = "its translation takes over the limit of 524288 steps to write"
const TOO_MANY_LOOKBEHINDS = "it has over the limit of 252 lookbehinds of no fixed length"

toolarge(E, limit::String) = throw(TooLargeError(E.pattern, limit, true))

# A length with no bound.
const INFINITE = typemax(Int64)
# Past this a length is as good as unbounded, and keeping below it keeps the sums exact.
const LENGTH_LIMIT = Int64(1) << 47
# The largest count PCRE2 takes in a quantifier, and the longest lookbehind it takes.
const PCRE_MAX_COUNT = 65535
# The most text a translation may have where a repetition was written twice.
const MAX_COPIED_TRANSLATION = 65536
# The callouts that end and begin the body of a lookbehind (see `lookbehind`). The others name a lookbehind.
const CALLOUT_AT_TARGET = 0
const CALLOUT_AT_START = 1
const CALLOUT_FIRST_LOOKBEHIND = 2
# How the body of a lookbehind that is a callout starts, which no other pattern does.
const LOOKBEHIND_PREFIX = "(?C1)"
# PCRE2 numbers the callouts it adds itself 255. The one before it is the callout that stands before each character
# the body of a lookbehind takes, and lets it be taken only before the position of the lookbehind.
const CALLOUT_BEFORE_TARGET = 254
const PCRE_MAX_LOOKBEHIND_CALLOUT = 253
# The body of a lookbehind that is a callout is tried only from where every character up to the lookbehind is one
# it can take, when those characters are a set of up to this many ranges.
const MAX_REACH_RANGES = 8
# The largest count of a repetition in such a body under which each character is held to the position.
const MAX_HELD_COUNT = 64

const LINE_TERMINATOR_CLASS = "[\\x{a}\\x{d}\\x{2028}\\x{2029}]"
const ANY_CHARACTER = "(?s:.)"
const NEVER_MATCHES = "(?!)"
const WORD_CLASS = "[a-zA-Z0-9_]"
const FOLD_WORD_CLASS = "[a-zA-Z0-9_\\x{17f}\\x{212a}]"
# Captures the rest of the text under a name, which says where the match stands: a reference to it matches at
# that place and at no other, as only there is the rest of the text as long.
const REST_OF_TEXT = "(?s:.*)"

const SURROGATE_SET = Int32[0xD800, 0xDFFF]
const NOT_SURROGATE_SET = complement(SURROGATE_SET)

# The translation of a pattern. It is the pattern PCRE2 searches for, and the body of each lookbehind that is a
# callout, in the order of the callout numbers.
struct Translation
    main::String
    lookbehinds::Vector{String}
end

# A backreference where the tree is walked, with the lookarounds and the repetitions that enclose it.
struct Found
    reference::Node
    looks::Vector{Node}
    repeats::Vector{Node}
end

mutable struct Emitter
    pattern::String
    unicode::Bool
    root::Node
    groupnodes::Vector{Union{Nothing,Node}}
    # Every backreference and every lookbehind of the pattern.
    references::Vector{Node}
    lookbehindnodes::Vector{Node}
    # For each group, whether some LIVE backreference reads it, and the lookarounds and the repetitions that
    # enclose it, outermost first.
    referenced::Vector{Bool}
    # For each group, the least number of the lookarounds around it that also hold a LIVE backreference to it. A
    # lookaround further in than that has the group read from outside it.
    sharedlooks::Vector{Int}
    looksaround::Vector{Vector{Node}}
    repeatsaround::Vector{Vector{Node}}
    out::IOBuffer
    # The names of the groups written to `out`, in the order PCRE2 numbers them.
    names::Vector{String}
    # For each group, how a backreference reads it where the writer stands (see `writerepeat`). Empty until the
    # group is written.
    read::Vector{String}
    # For each group, the times it has been written, and the number of names made for other things.
    written::Vector{Int}
    serial::Int
    # Whether the writer is in the body of a lookbehind that PCRE2 matches forwards and ECMA-262 backwards, and
    # how many times over it is writing a body again with groups that do not capture (see `writerepeat`).
    backwards::Bool
    uncaptured::Int
    # Whether some backreference is LIVE, so that what a group captures can be seen.
    hasbackreference::Bool
    haslookbehind::Bool
    # Whether every lookbehind is written as a callout.
    searchinglookbehind::Bool
    lookbehinds::Vector{String}
    # The sets of many ranges of the pattern being written, which it calls by number (see `writeclass`), and the
    # searches for its lookbehinds, which it calls by name (see `walk`).
    largesets::Vector{CodeSet}
    walks::Vector{String}
    # The characters and classes that the pattern being written counts further than PCRE2 can be given, each as
    # it is written. The pattern calls their blocks by number (see `blocks`).
    blocks::Vector{String}
    # The name of the group that holds the rest of the text where the lookbehind being written as a search stands.
    edgerest::String
    # Whether the writer is in the body of a lookbehind that is a callout, outside the lookarounds of that body.
    within::Bool
    # The bytes of the sets of many ranges that the pattern being written holds where they stand, under a
    # repetition with no upper count (see `unbounded`).
    inlined::Int
    # The nodes written for the pattern so far, in every form that was tried.
    steps::Int
end

function Emitter(parser::Parser, root::Node)
    n = parser.groupcount
    return Emitter(parser.pattern, parser.unicode, root, Vector{Union{Nothing,Node}}(nothing, n), Node[], Node[],
        fill(false, n), fill(typemax(Int), n), [NO_NODES for _ in 1:n], [NO_NODES for _ in 1:n], IOBuffer(), String[],
        fill("", n), zeros(Int, n), 0, false, 0, false, false, false, String[], CodeSet[], String[], String[], "",
        false, 0, 0)
end

# Works out what each backreference reads, and refuses the pattern when one cannot be written exactly.
function analyse!(E::Emitter)
    # None of this changes a pattern with no backreference.
    if holdsbackreference(E.root)
        E.root = prune!(E.root)
        trim!(E.root, false, true)
    else
        trim!(E.root, false, false)
    end
    # A lookbehind that is written again changes what the backreferences read, so they are worked out again.
    while true
        while !resolve!(E)
        end
        again = false
        for look in E.lookbehindnodes
            if look.exports && !fixedlength(look.subs[1]) && rewrite!(E, look)
                again = true
                break
            end
        end
        again || break
    end
    for look in E.lookbehindnodes
        look.continued = false
        (look.exports && !fixedlength(look.subs[1])) || continue
        # What is left is a lookbehind whose groups can only be told by a search for where it starts.
        if continuable(E, look)
            look.continued = true
        else
            unsupported(E, REFUSED_GROUP_IN_LOOKBEHIND)
        end
    end
    return E
end

# A node that never matches.
function nevernode()
    n = setnode(NO_CHARACTERS)
    n.minlength = 1
    n.maxlength = 1
    return n
end

isnever(n::Node) = n.kind == SET && isempty(n.set)

# Whether a node matches the empty string wherever it stands, whatever the text. (A backreference is not asked: what
# it reads is not known yet.)
function alwaysempty(n::Node)
    kind = n.kind
    if kind == EMPTY
        return true
    elseif kind == CAT
        return all(alwaysempty, n.subs)
    elseif kind == ALT
        return any(alwaysempty, n.subs)
    elseif kind == GROUP
        return alwaysempty(n.subs[1])
    elseif kind == REPEAT
        return n.min == 0 || alwaysempty(n.subs[1])
    elseif kind == LOOK
        return n.negative ? isnever(n.subs[1]) : alwaysempty(n.subs[1])
    end
    return false
end

# Takes out of the tree what can never match: an alternative that cannot, a sequence with a term that cannot, a
# negative lookaround whose body matches the empty string anywhere, as (?<!|a) does. A group in such a part never
# takes part, so a backreference to it reads nothing, and that is known only once the part is gone. Returns the
# node to stand where `n` stood.
function prune!(n::Node)
    for k in eachindex(n.subs)
        n.subs[k] = prune!(n.subs[k])
    end
    kind = n.kind
    if kind == ALT
        any(isnever, n.subs) || return n
        ways = Node[sub for sub in n.subs if !isnever(sub)]
        isempty(ways) && return nevernode()
        length(ways) == 1 && return ways[1]
        n.subs = ways
    elseif kind == CAT
        any(isnever, n.subs) && return nevernode()
    elseif kind == GROUP
        isnever(n.subs[1]) && return nevernode()
    elseif kind == REPEAT
        # No iteration of it can run.
        isnever(n.subs[1]) && return n.min == 0 ? Node(EMPTY) : nevernode()
    elseif kind == LOOK
        (n.negative ? alwaysempty(n.subs[1]) : isnever(n.subs[1])) && return nevernode()
    end
    return n
end

# Works out what each backreference reads. Returns false when a lookbehind had to be written again for one.
function resolve!(E::Emitter)
    empty!(E.references)
    empty!(E.lookbehindnodes)
    fill!(E.referenced, false)
    fill!(E.sharedlooks, typemax(Int))
    E.hasbackreference = false
    index!(E, E.root)
    isempty(E.references) && return true
    # Whether a backreference is LIVE depends on which repetitions never run, which depends on which bodies can
    # only match the empty string, which depends on which backreferences are LIVE. Each round can only take
    # backreferences from LIVE to NEVER, so the rounds end.
    while true
        clearlengths!(E.root)
        before = sum(n -> count(==(LIVE), n.statuses), E.references)
        flow!(E, E.root, BitSet(), false, false)
        sum(n -> count(==(LIVE), n.statuses), E.references) == before && break
    end
    found = Found[]
    survey!(E, E.root, Node[], Node[], found)
    # The lookbehinds that hold a backreference and a group ECMA-262 matches before it.
    backwards = Node[]
    for f in found
        decide!(E, f, backwards)
    end
    isempty(backwards) && return true
    progress = false
    for look in backwards
        progress |= split!(look)
    end
    progress || unsupported(E, REFUSED_BACKREFERENCE_IN_LOOKBEHIND)
    return false
end

# What ECMA-262 matches last in a lookbehind is the leftmost of its body. What follows it is the end of the
# lookbehind, which holds, and nothing comes back into a lookbehind that has held. So it takes the first of its ways
# that matches, and no other. The same goes for a term with nothing to its left but terms that match the empty
# string wherever they stand, as those cannot fail and send the match back into it: in (?<=(a)*x+?) the x+? is such
# a term.
#
# - a lazy repetition there runs its minimum and no more, so (?<=(a|b)+?) is (?<=(a|b)), which has a fixed length;
# - of alternatives there, none after one that matches the empty string anywhere is ever taken, so (?<=(|a)x) is
#   (?<=()x).
#
# `last` says that nothing that can fail follows the node in its lookbehind. Alternatives are taken away, and terms
# after the leftmost are looked at, only with `alternatives`, which is when the pattern has a backreference: nothing
# else can tell which way was taken.
function trim!(n::Node, last::Bool, alternatives::Bool)
    kind = n.kind
    if kind == LOOK
        trim!(n.subs[1], n.behind, alternatives)
    elseif kind == CAT
        for k in eachindex(n.subs)
            trim!(n.subs[k], last, alternatives)
            # The next term is matched last too when this one cannot fail.
            last = last && alternatives && alwaysempty(n.subs[k])
        end
    elseif kind == ALT
        if last && alternatives
            first = findfirst(alwaysempty, n.subs)
            first === nothing || resize!(n.subs, first)
        end
        for sub in n.subs
            trim!(sub, last, alternatives)
        end
    elseif kind == GROUP
        trim!(n.subs[1], last, alternatives)
    elseif kind == REPEAT
        if last && n.lazy
            n.max = n.min
        end
        trim!(n.subs[1], false, alternatives)
    end
    return nothing
end

# Whether a node holds a backreference, or a group that captures.
holdsbackreference(n::Node) = n.kind == BACKREF || any(holdsbackreference, n.subs)
holdsgroup(n::Node) = (n.kind == GROUP && n.group != 0) || any(holdsgroup, n.subs)

# The least and the greatest number of a group inside a node, or zeros.
function groupsinside(n::Node, least::Int, greatest::Int)
    if n.kind == GROUP && n.group != 0
        least = least == 0 ? n.group : min(least, n.group)
        greatest = max(greatest, n.group)
    end
    for sub in n.subs
        least, greatest = groupsinside(sub, least, greatest)
    end
    return least, greatest
end

# A lookbehind for a body, or for a sequence of terms.
function lookbehindfor(body::Node)
    look = Node(LOOK)
    look.behind = true
    look.subs = Node[body]
    least, greatest = groupsinside(body, 0, 0)
    look.firstgroup = max(least - 1, 0)
    look.lastgroup = greatest
    return look
end

function lookbehindfor(terms::Vector{Node})
    length(terms) == 1 && return lookbehindfor(terms[1])
    sequence = Node(CAT)
    sequence.subs = terms
    return lookbehindfor(sequence)
end

# Writes a lookbehind again so that a backreference inside it can read a group that ECMA-262 matches before it,
# which is one to its right. Returns false when it cannot be.
#
# A lookbehind for alternatives is a lookahead for a lookbehind of each: (?<=A|B) is (?=(?<=A)|(?<=B)), which is
# as atomic.
#
# A lookbehind for a sequence P S, where S is the terms at the end that have a fixed length and no backreference,
# is (?<=S(?<=(?<=P).{n})), where n is the length of S. PCRE2 steps back over S, matches it, and where S ends looks
# back over it again for P. So S is matched before P, as in ECMA-262, and its groups are set where P reads them.
# The terms of S are each at a place its length fixes, so they capture the same text in either direction. At most
# one of them may hold a group: with two, the two engines would try their ways in a different order.
function split!(look::Node)
    body = look.subs[1]
    if body.kind == ALT
        each!(look)
        return true
    end
    (body.kind == CAT && !look.split) || return false
    terms = body.subs
    from = length(terms) + 1
    groups = 0
    while from > 1
        term = terms[from - 1]
        rigid(term) || break
        groups += holdsgroup(term) ? 1 : 0
        groups > 1 && break
        from -= 1
    end
    return splitat!(look, from)
end

# Whether a node matches texts of one length, each part of it at a place that length fixes, and reads no group. What
# its groups capture is then the same in whichever direction it is matched.
rigid(n::Node) = minlength(n) == maxlength(n) && maxlength(n) <= PCRE_MAX_COUNT && !holdsbackreference(n)

# Writes a lookbehind for alternatives as a lookahead for a lookbehind of each.
function each!(look::Node)
    alternation = Node(ALT)
    alternation.subs = Node[lookbehindfor(sub) for sub in look.subs[1].subs]
    look.behind = false
    look.subs = Node[alternation]
    return nothing
end

# Writes a lookbehind for a sequence as its terms from `from` on, and then a lookbehind for the terms before them.
# Returns false when there are no terms on one side.
function splitat!(look::Node, from::Int)
    terms = look.subs[1].subs
    (from == 1 || from > length(terms)) && return false
    len = Int64(0)
    for k in from:length(terms)
        len = plus(len, minlength(terms[k]))
    end
    len > PCRE_MAX_COUNT && return false
    step = lookbehindfor(terms[1:(from - 1)])
    if len > 0
        over = Node(REPEAT)
        over.min = Int(len)
        over.max = Int(len)
        over.subs = Node[setnode(ANY_SET)]
        step = lookbehindfor(Node[step, over])
    end
    sequence = Node(CAT)
    sequence.subs = vcat(terms[from:end], Node[step])
    look.subs = Node[sequence]
    look.split = true
    return true
end

# Whether a backreference outside a lookbehind reads a group inside it.
function exportedfrom(E::Emitter, g::Int, look::Node)
    E.referenced[g] || return false
    depth = findfirst(l -> l === look, E.looksaround[g])
    return depth !== nothing && E.sharedlooks[g] < depth
end

# Writes again a lookbehind of no fixed length with a group that is read after it, so that PCRE2 sets the group to
# what ECMA-262 captures. Returns false when it is left as it was.
#
# - A group around the whole body that nothing reads is taken away.
# - A lookbehind for alternatives is a lookahead for a lookbehind of each, as in `split!`. ECMA-262 tries the
#   alternatives in order too.
# - A lookbehind for a sequence P S, where S is the terms at the end that have a fixed length and no backreference
#   and P holds no group that is read after the lookbehind, is (?<=S(?<=(?<=P).{n})), as in `split!`. Whether P
#   matches does not depend on S, and each term of S is at a place the length fixes, so S captures what it captures
#   in ECMA-262.
function rewrite!(E::Emitter, look::Node)
    body = look.subs[1]
    if body.kind == GROUP && (body.group == 0 || !E.referenced[body.group])
        look.subs = Node[body.subs[1]]
        return true
    elseif body.kind == ALT
        # Unless it is written whole, as a search (see `continuation`).
        continuable(E, look) && return false
        each!(look)
        return true
    end
    (body.kind == CAT && !look.split) || return false
    terms = body.subs
    from = length(terms) + 1
    while from > 1 && rigid(terms[from - 1])
        from -= 1
    end
    for k in 1:(from - 1)
        exports(E, look, terms[k]) && return false
    end
    return splitat!(look, from)
end

# Whether a backreference outside a lookbehind reads a group inside a node of it.
function exports(E::Emitter, look::Node, n::Node)
    least, greatest = groupsinside(n, 0, 0)
    return least != 0 && any(g -> exportedfrom(E, g, look), least:greatest)
end

# Whether a node holds a LIVE backreference to a group inside the node.
function readsitself(n::Node, least::Int=groupsinside(n, 0, 0)[1], greatest::Int=groupsinside(n, 0, 0)[2])
    if n.kind == BACKREF
        return any(k -> n.statuses[k] == LIVE && least <= n.groups[k] <= greatest, eachindex(n.groups))
    end
    return least != 0 && any(sub -> readsitself(sub, least, greatest), n.subs)
end

# Whether a lookbehind of no fixed length whose groups are read after it can be written as a search with the rest
# of the pattern inside it (see `continuation`). It has to be a term of the pattern itself, and each of its
# alternatives one that `writeedge` writes. An alternative that another follows is also written with groups that
# do not capture, to say that it does not match, so it may not read a group of its own.
function continuable(E::Emitter, look::Node)
    (E.root.kind == CAT && holds(E.root.subs, look) && !look.negative) || return false
    body = look.subs[1]
    if body.kind == ALT
        return all(sub -> edgeable(E, look, sub, Int64(0), false), body.subs) &&
            !any(readsitself, view(body.subs, 1:(length(body.subs) - 1)))
    end
    return edgeable(E, look, body, Int64(0), false)
end

# Whether `writeedge` writes a node that is the leftmost of a lookbehind, with `after` characters of the lookbehind
# after it. `captured` says that the node is inside a group that is read.
function edgeable(E::Emitter, look::Node, n::Node, after::Int64, captured::Bool)
    rigid(n) && return true
    after > PCRE_MAX_COUNT && return false
    (!captured && !exports(E, look, n)) && return true
    kind = n.kind
    if kind == GROUP
        return edgeable(E, look, n.subs[1], after, captured || (n.group != 0 && E.referenced[n.group]))
    elseif kind == CAT
        from = fixedend(n)
        from == 1 && return false
        tail = Int64(0)
        for k in from:length(n.subs)
            tail = plus(tail, minlength(n.subs[k]))
        end
        from == 2 && return edgeable(E, look, n.subs[1], plus(after, tail), captured)
        return !captured && !any(k -> exports(E, look, n.subs[k]), 1:(from - 1))
    elseif kind == REPEAT
        body = n.subs[1]
        return !n.lazy && rigid(body) && minlength(body) > 0 && n.min <= PCRE_MAX_COUNT &&
            (n.max == UNBOUNDED || n.max >= MAX_COUNT || n.max <= PCRE_MAX_COUNT)
    elseif kind == ALT
        return all(rigid, n.subs)
    end
    return false
end

# The number of the first of the terms at the end of a sequence that are all of a fixed length and read no group.
function fixedend(n::Node)
    from = length(n.subs) + 1
    while from > 1 && rigid(n.subs[from - 1])
        from -= 1
    end
    return from
end

# The translation, with every lookbehind as a callout when `searching`.
function translation(E::Emitter, searching::Bool)
    E.searchinglookbehind = searching
    E.out = IOBuffer()
    E.haslookbehind = false
    E.backwards = false
    E.uncaptured = 0
    E.within = false
    E.serial = 0
    empty!(E.names)
    empty!(E.lookbehinds)
    fill!(E.read, "")
    fill!(E.written, 0)
    empty!(E.largesets)
    empty!(E.walks)
    empty!(E.blocks)
    E.inlined = 0
    node(E, E.root)
    definitions(E)
    main = String(take!(E.out))
    ncodeunits(main) + sum(ncodeunits, E.lookbehinds; init=0) > MAX_TRANSLATION_LENGTH &&
        toolarge(E, TOO_LARGE_TRANSLATION)
    return Translation(main, copy(E.lookbehinds))
end

# What a backreference reads.

# Notes the node of each group, every backreference and every lookbehind. A backreference starts LIVE.
function index!(E::Emitter, n::Node)
    kind = n.kind
    if kind == GROUP && n.group != 0
        E.groupnodes[n.group] = n
    elseif kind == BACKREF
        n.statuses = fill(LIVE, length(n.groups))
        n.ignorecase = fill(false, length(n.groups))
        push!(E.references, n)
    elseif kind == LOOK
        n.exports = false
        n.haslive = false
        n.behind && push!(E.lookbehindnodes, n)
    elseif kind == REPEAT
        n.exports = false
    end
    for sub in n.subs
        index!(E, sub)
    end
    return nothing
end

# Whether a repetition runs no iteration. One with a maximum of zero does not. Nor does one with a minimum of zero
# whose body can only match the empty string, as ECMA-262 refuses an iteration that matches the empty string once
# the minimum is met.
neverruns(n::Node) = n.max == 0 || (n.min == 0 && maxlength(n.subs[1]) == 0)

# Works out, for every backreference, whether each group it names can have taken part where the backreference
# stands (LIVE) or cannot (NEVER). The walk follows the order in which ECMA-262 evaluates the pattern, which is
# backwards inside a lookbehind. `possible` holds the groups that have taken part on some way to the node, and is
# updated to hold the same for the ways that leave it. Inside `dead`, a repetition that never runs, every
# backreference is NEVER, as it is never evaluated.
#
# ECMA-262 unsets the groups of a quantified body at the start of each of its iterations, and unsets the groups of a
# negative lookaround when the lookaround is done.
function flow!(E::Emitter, n::Node, possible::BitSet, backward::Bool, dead::Bool)
    kind = n.kind
    if kind == CAT
        for k in (backward ? reverse(eachindex(n.subs)) : eachindex(n.subs))
            flow!(E, n.subs[k], possible, backward, dead)
        end
    elseif kind == ALT
        anypossible = BitSet()
        for sub in n.subs
            p = copy(possible)
            flow!(E, sub, p, backward, dead)
            union!(anypossible, p)
        end
        union!(possible, anypossible)
    elseif kind == GROUP
        flow!(E, n.subs[1], possible, backward, dead)
        n.group != 0 && push!(possible, n.group)
    elseif kind == REPEAT
        setdiff!(possible, (n.firstgroup + 1):n.lastgroup)
        if neverruns(n)
            # The body never runs, so nothing it sets is set after it.
            flow!(E, n.subs[1], copy(possible), backward, true)
        else
            flow!(E, n.subs[1], possible, backward, dead)
        end
    elseif kind == LOOK
        # PCRE2 matches the body of a lookbehind that was split forwards, in the order ECMA-262 matches it in.
        if n.negative
            flow!(E, n.subs[1], copy(possible), n.behind && !n.split, dead)
            setdiff!(possible, (n.firstgroup + 1):n.lastgroup)
        else
            flow!(E, n.subs[1], possible, n.behind && !n.split, dead)
        end
    elseif kind == BACKREF
        for k in eachindex(n.groups)
            g = n.groups[k]
            # A group that can only capture the empty string reads as one that has not taken part.
            live = !dead && g in possible && maxlength((E.groupnodes[g]::Node).subs[1]) != 0
            if !live && n.statuses[k] == LIVE
                n.statuses[k] = NEVER
            end
        end
    end
    return nothing
end

# Notes what encloses each group, and finds each backreference that is LIVE with what encloses it.
function survey!(E::Emitter, n::Node, looks::Vector{Node}, repeats::Vector{Node}, found::Vector{Found})
    kind = n.kind
    if kind == GROUP
        if n.group != 0
            E.looksaround[n.group] = copy(looks)
            E.repeatsaround[n.group] = copy(repeats)
        end
        survey!(E, n.subs[1], looks, repeats, found)
    elseif kind == REPEAT
        # Nothing of a repetition that never runs is written.
        neverruns(n) && return nothing
        push!(repeats, n)
        survey!(E, n.subs[1], looks, repeats, found)
        pop!(repeats)
    elseif kind == LOOK
        push!(looks, n)
        survey!(E, n.subs[1], looks, repeats, found)
        pop!(looks)
    elseif kind == BACKREF
        any(==(LIVE), n.statuses) && push!(found, Found(n, copy(looks), copy(repeats)))
    else
        for sub in n.subs
            survey!(E, sub, looks, repeats, found)
        end
    end
    return nothing
end

holds(nodes::Vector{Node}, n::Node) = any(r -> r === n, nodes)

# Decides one LIVE backreference: marks what it reads through, and notes in `backwards` a lookbehind that holds
# the backreference and a group ECMA-262 matches before it (see the note on lookbehinds at the top of the file).
function decide!(E::Emitter, f::Found, backwards::Vector{Node})
    n = f.reference
    for k in eachindex(n.groups)
        n.statuses[k] == LIVE || continue
        g = n.groups[k]
        grouplooks = E.looksaround[g]
        # The lookarounds that hold both the backreference and the group. The innermost says in which direction
        # ECMA-262 matches the two.
        common = 0
        while common < length(f.looks) && common < length(grouplooks) && f.looks[common + 1] === grouplooks[common + 1]
            common += 1
        end
        if common > 0 && f.looks[common].behind && !f.looks[common].split
            holds(backwards, f.looks[common]) || push!(backwards, f.looks[common])
            continue
        end
        for k2 in eachindex(f.looks)
            f.looks[k2].behind && (f.looks[k2].haslive = true)
        end
        for k2 in (common + 1):length(grouplooks)
            grouplooks[k2].behind && (grouplooks[k2].exports = true)
        end
        for rep in E.repeatsaround[g]
            holds(f.repeats, rep) || (rep.exports = true)
        end
        E.referenced[g] = true
        E.sharedlooks[g] = min(E.sharedlooks[g], common)
        E.hasbackreference = true
        n.ignorecase[k] = n.fold && comparesignoringcase(E, g)
    end
    return nothing
end

# Decides how a case-insensitive backreference to a group is written, from the characters the group can capture.
# Returns false when none of them has a case variant, so that the backreference compares exactly.
#
# Otherwise the comparison is PCRE2's, which is Unicode simple case folding in the Unicode version of that PCRE2.
#
# - With the u flag grammar that is the comparison ECMA-262 makes, so the backreference is always written. The
#   two agree on every pair of characters where the Unicode version of PCRE2 is that of the data or later, as
#   Unicode does not take a case pair away. An earlier PCRE2 lacks the pairs Unicode has added since (the PCRE2
#   10.42 of Julia 1.10 has Unicode 14 and lacks thirty pairs, which the README lists), and holds two characters of
#   such a pair to be different. Nothing is written to make up for that.
# - With no flag ECMA-262 compares by the uppercase mapping of one UTF-16 code unit, which is not what PCRE2 does
#   for every character: PCRE2 holds U+212A equal to k, and ECMA-262 does not. So a pattern that is only valid with
#   no flag is refused when the group can capture a character with a variant and a character the two compare
#   differently (see `comparedotherwise`).
function comparesignoringcase(E::Emitter, g::Int)
    captured = capturedset(E, E.groupnodes[g]::Node)
    intersects(captured, cased(E.unicode)) || return false
    if !E.unicode && intersects(captured, comparedotherwise())
        unsupported(E, REFUSED_CASE_INSENSITIVE_BACKREFERENCE)
    end
    return true
end

const COMPARED_OTHERWISE = Ref{Union{Nothing,CodeSet}}(nothing)

# The characters that are not equivalent to the same characters under the two grammars: those whose class under
# Unicode simple case folding, which is what PCRE2 compares by, is not their class under the Canonicalize of a
# pattern with no flag. They are U+212A and k, U+017F and s and the like, and every character beyond the Basic
# Multilingual Plane that has a case variant, which a pattern with no flag does not fold.
function comparedotherwise()
    lock(DATA_LOCK)
    try
        set = COMPARED_OTHERWISE[]
        set === nothing || return set
        pairs = Int32[]
        for (f, other) in ((folding(true), folding(false)), (folding(false), folding(true)))
            for (k, point) in enumerate(f.points)
                at = searchsortedfirst(other.points, point)
                same = at <= length(other.points) && other.points[at] == point &&
                    sort(other.classes[at]) == sort(f.classes[k])
                same || push!(pairs, point, point)
            end
        end
        set = setof(pairs)
        COMPARED_OTHERWISE[] = set
        return set
    finally
        unlock(DATA_LOCK)
    end
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
            if n.statuses[k] == LIVE
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
        # A backreference that reads no group matches the empty string.
        hi = any(==(LIVE), n.statuses) ? INFINITE : 0
    end
    n.minlength = lo > LENGTH_LIMIT ? INFINITE : lo
    n.maxlength = hi > LENGTH_LIMIT ? INFINITE : hi
    return nothing
end


# Forgets the lengths measured below a node, which depend on which backreferences are LIVE.
function clearlengths!(n::Node)
    n.minlength = -1
    for sub in n.subs
        clearlengths!(sub)
    end
    return nothing
end

# Writing.

function node(E::Emitter, n::Node)
    kind = n.kind
    out = E.out
    # A set is as many steps as it has ranges, by eight: what is worked out of a set of many ranges where it is
    # written takes that much longer (25 microseconds for the set of \p{L}, which is 85 steps).
    E.steps += kind == SET ? 1 + length(n.set) >> 4 : 1
    E.steps > MAX_TRANSLATION_STEPS && toolarge(E, TOO_LARGE_STEPS)
    position(out) > MAX_TRANSLATION_LENGTH && toolarge(E, TOO_LARGE_TRANSLATION)
    if kind == EMPTY
    elseif kind == CHAR || kind == SET
        # In a group, so that a quantifier after the character repeats the callout with it.
        E.within && print(out, "(?:(?C", CALLOUT_BEFORE_TARGET, ')')
        if kind == CHAR
            literal(out, n.codepoint)
        else
            writeclass(E, n.set)
        end
        E.within && print(out, ')')
    elseif kind == CAT
        if n === E.root
            terms(E, n.subs, 1)
        else
            for sub in n.subs
                node(E, sub)
            end
        end
    elseif kind == ALT
        alternatives(E, n.subs, "", "")
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
        # A lookaround inside the body of a lookbehind is not held to the position of that lookbehind.
        within = E.within
        E.within = false
        if n.behind
            lookbehind(E, n)
        else
            backwards = E.backwards
            E.backwards = false
            print(out, n.negative ? "(?!" : "(?=")
            node(E, n.subs[1])
            print(out, ')')
            E.backwards = backwards
        end
        E.within = within
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
    if !manyranges(set)
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

# Whether a set, which holds no surrogate, is one of many ranges: its class, as `writeset` writes it, has more than
# LARGE_SET_RANGES.
function manyranges(set::CodeSet)
    written = inset(set, 0xFF) && inset(set, 0x100) ? intersection(complement(set), NOT_SURROGATE_SET) : set
    return length(written) > 2 * LARGE_SET_RANGES
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

# Writes what the pattern calls and never runs into, inside a group PCRE2 only reads definitions from: the searches
# for its lookbehinds (see `walk`) and the groups of its sets of many ranges. A search can hold such a set, so the
# searches come first.
function definitions(E::Emitter)
    isempty(E.largesets) && isempty(E.walks) && isempty(E.blocks) && return nothing
    out = E.out
    print(out, "(?(DEFINE)")
    for (number, text) in enumerate(E.blocks)
        # A block is so many characters, which is matched in one way, so nothing is kept to come back into it.
        for (level, size) in enumerate(blocksizes(text)[2])
            print(out, "(?<", BLOCK_LETTERS[level], number, ">(?>")
            level == 1 ? print(out, text) : print(out, "(?&", BLOCK_LETTERS[level - 1], number, ')')
            print(out, '{', size, "}))")
        end
    end
    for text in E.walks
        print(out, text)
    end
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

# Writes a node to a buffer of its own. Returns the text and the names of the groups written to it.
function apart(E::Emitter, n::Node)
    out = E.out
    names = E.names
    E.out = IOBuffer()
    E.names = String[]
    node(E, n)
    text = String(take!(E.out))
    inner = E.names
    E.out = out
    E.names = names
    return text, inner
end

# Writes the groups of the names with nothing in them, which sets each to the empty string.
function pad(out::IO, names::Vector{String}, from::Int=1, to::Int=length(names))
    for k in from:to
        print(out, "(?<", names[k], ">)")
    end
    return nothing
end

# A name for a group PCRE2 needs and the pattern does not have.
function helpername(E::Emitter, letter::Char)
    E.serial += 1
    return string(letter, E.serial)
end

# Writes alternatives, each between `before` and `after`.
#
# When none of them writes a group, they are written as they are. Otherwise each alternative also sets, to the empty
# string, the groups the others write, so that after the alternation every one of those groups has the value of
# this pass: its text in the alternative that was taken, and the empty string, which reads as no value, in the
# others. They are written inside (?|...), in which PCRE2 numbers the groups of every alternative from the same
# number. Each alternative writes the same names in the same order (the empty groups for the alternatives before
# it, its own, the empty groups for those after it), so a name has one number.
function alternatives(E::Emitter, subs::Vector{Node}, before::String, after::String)
    out = E.out
    if !E.hasbackreference
        for k in eachindex(subs)
            k > 1 && print(out, '|')
            print(out, before)
            node(E, subs[k])
            print(out, after)
        end
        return nothing
    end
    texts = String[]
    all = String[]
    # Where the names of each alternative start in `all`.
    starts = Int[]
    for sub in subs
        text, names = apart(E, sub)
        push!(texts, text)
        push!(starts, length(all) + 1)
        append!(all, names)
    end
    push!(starts, length(all) + 1)
    padded(E, texts, all, starts, before, after)
    return nothing
end

# Writes alternatives that were written apart, each between `before` and `after`, with the empty groups that make
# every one of them write the same names. `all` is the names of all of them, and those of the k-th start at
# `starts[k]`.
function padded(E::Emitter, texts::Vector{String}, all::Vector{String}, starts::Vector{Int}, before::String,
        after::String)
    out = E.out
    plain = isempty(all)
    plain || print(out, "(?|")
    for k in eachindex(texts)
        k > 1 && print(out, '|')
        pad(out, all, 1, starts[k] - 1)
        print(out, before, texts[k], after)
        pad(out, all, starts[k + 1], length(all))
    end
    plain || print(out, ')')
    append!(E.names, all)
    return nothing
end

function writegroup(E::Emitter, n::Node)
    out = E.out
    g = n.group
    if g == 0 || !E.referenced[g] || E.uncaptured > 0
        print(out, "(?:")
        node(E, n.subs[1])
        print(out, ')')
        return nothing
    end
    # A group that is written again (see `writerepeat`) has another name there.
    E.written[g] += 1
    name = E.written[g] == 1 ? string('g', g) : string('g', g, '_', E.written[g])
    push!(E.names, name)
    print(out, "(?<", name, '>')
    node(E, n.subs[1])
    print(out, ')')
    E.read[g] = string("\\k<", name, '>')
    return nothing
end

# Whether a backreference reads a group inside the node.
function holdsreferenced(E::Emitter, n::Node)
    E.uncaptured > 0 && return false
    for g in (n.firstgroup + 1):n.lastgroup
        E.referenced[g] && return true
    end
    return false
end

# A node that matches nothing, which stands for a way that is not there.
const NO_WAY = let n = setnode(NO_CHARACTERS)
    n.minlength = 1
    n.maxlength = 1
    n
end

# The most nodes one body may be rewritten into by `nonempty`.
const MAX_REWRITTEN_NODES = 256

# A copy of a node with other children.
function withsubs(n::Node, subs::Vector{Node})
    c = Node(n.kind)
    c.min = n.min
    c.max = n.max
    c.lazy = n.lazy
    c.exports = n.exports
    c.group = n.group
    c.firstgroup = n.firstgroup
    c.lastgroup = n.lastgroup
    c.subs = subs
    return c
end

# Counts a node made by `nonempty` or `emptyonly`. False when the body has been given as many as it may have.
function spend!(budget::Base.RefValue{Int})
    budget[] -= 1
    return budget[] >= 0
end

# The node that matches what `n` matches, but for the empty string. Returns NO_WAY when that is nothing, and
# nothing when it is not plain to write.
#
# With `strict`, no part of `n` is written twice and the ways through the result are tried in the order ECMA-262
# tries the ways through `n`. That is needed where `n` holds a group a backreference reads. Without it the result
# matches the same texts and may try them in another order, which nothing can see where no group is read.
function nonempty(E::Emitter, n::Node, strict::Bool, budget::Base.RefValue{Int})::Union{Node,Nothing}
    minlength(n) > 0 && return n
    maxlength(n) == 0 && return NO_WAY
    spend!(budget) || return nothing
    kind = n.kind
    if kind == GROUP
        body = nonempty(E, n.subs[1], strict, budget)
        (body === nothing || body === NO_WAY) && return body
        # A group that no backreference reads and that is left with one character is that character. PCRE2 keeps
        # the count of a repeated character as a number and writes a repeated group out once for each iteration,
        # so (?:(?:a|){1,300}x){1,300} is "too large" for it with (?:a){0,299} inside and not with a{0,299}.
        if (n.group == 0 || !E.referenced[n.group]) && (body.kind == CHAR || body.kind == SET)
            return body
        end
        return withsubs(n, Node[body])
    elseif kind == ALT
        subs = Node[]
        for sub in n.subs
            way = nonempty(E, sub, strict, budget)
            way === nothing && return nothing
            way === NO_WAY || push!(subs, way)
        end
        isempty(subs) && return NO_WAY
        length(subs) == 1 && return subs[1]
        if all(sub -> sub.kind == CHAR || sub.kind == SET, subs)
            # Alternatives of one character each are a class, which PCRE2 counts as it counts a character.
            set = NO_CHARACTERS
            for sub in subs
                set = setunion(set, sub.kind == CHAR ? Int32[sub.codepoint, sub.codepoint] : sub.set)
            end
            return setnode(set)
        end
        return withsubs(n, subs)
    elseif kind == CAT
        long = count(sub -> maxlength(sub) > 0, n.subs)
        if long == 1
            # Every other element matches the empty string only.
            subs = copy(n.subs)
            k = findfirst(sub -> maxlength(sub) > 0, subs)::Int
            way = nonempty(E, subs[k], strict, budget)
            (way === nothing || way === NO_WAY) && return way
            subs[k] = way
            return withsubs(n, subs)
        end
        strict && return nothing
        # The first element that takes a character is the k-th: the ones before it match the empty string, it
        # does not, and the ones after it match anything.
        ways = Node[]
        before = Node[]
        for k in eachindex(n.subs)
            sub = n.subs[k]
            if maxlength(sub) > 0
                way = nonempty(E, sub, false, budget)
                way === nothing && return nothing
                if way !== NO_WAY
                    spend!(budget) || return nothing
                    push!(ways, withsubs(n, vcat(before, Node[way], n.subs[(k + 1):end])))
                end
            end
            empty = emptyonly(E, sub, budget)
            empty === nothing && return nothing
            empty === NO_WAY && break
            push!(before, empty)
        end
        isempty(ways) && return NO_WAY
        length(ways) == 1 && return ways[1]
        alternation = Node(ALT)
        alternation.firstgroup = n.firstgroup
        alternation.lastgroup = n.lastgroup
        alternation.subs = ways
        return alternation
    elseif kind == REPEAT
        body = n.subs[1]
        # With a minimum of one or more the body can match the empty string here, and so can its first iterations.
        n.min == 0 || return nothing
        if minlength(body) > 0
            n.max == 1 && return body
            atleastonce = withsubs(n, n.subs)
            atleastonce.min = 1
            return atleastonce
        end
        # Every iteration is past the minimum, so each takes a character: an iteration that matches the empty
        # string ends the repetition, and ECMA-262 then tries the next way through the body. So the repetition
        # is one or more iterations of the body as it is when it takes a character, up to as many as it had, and
        # the ways are tried in the same order. The body is written once. (It was written twice, as the first
        # iteration and then the repetition of the rest, and so was the body of a repetition inside it, so that
        # `(?:(?:(?:a*)*)*)*` doubled with each level and twelve levels were more than PCRE2 holds.)
        way = nonempty(E, body, strict, budget)
        (way === nothing || way === NO_WAY) && return way
        n.max == 1 && return way
        (strict && holdsreferenced(E, n)) && return nothing
        spend!(budget) || return nothing
        atleastonce = withsubs(n, Node[way])
        atleastonce.min = 1
        return atleastonce
    end
    return nothing
end

# The node that matches the empty string where `n` does and nothing else, for a body no group of which is read.
# Returns NO_WAY when `n` never matches the empty string, and nothing when it is not plain to write.
function emptyonly(E::Emitter, n::Node, budget::Base.RefValue{Int})::Union{Node,Nothing}
    maxlength(n) == 0 && return n
    minlength(n) > 0 && return NO_WAY
    spend!(budget) || return nothing
    kind = n.kind
    if kind == GROUP
        body = emptyonly(E, n.subs[1], budget)
        (body === nothing || body === NO_WAY) && return body
        return withsubs(n, Node[body])
    elseif kind == ALT
        subs = Node[]
        for sub in n.subs
            way = emptyonly(E, sub, budget)
            way === nothing && return nothing
            way === NO_WAY || push!(subs, way)
        end
        isempty(subs) && return NO_WAY
        return length(subs) == 1 ? subs[1] : withsubs(n, subs)
    elseif kind == CAT
        subs = Node[]
        for sub in n.subs
            way = emptyonly(E, sub, budget)
            (way === nothing || way === NO_WAY) && return way
            push!(subs, way)
        end
        return withsubs(n, subs)
    elseif kind == REPEAT
        # No iteration past the minimum matches the empty string, and the iterations up to it that all do are one.
        n.min == 0 && return Node(EMPTY)
        return emptyonly(E, n.subs[1], budget)
    end
    return nothing
end

# What the writer has written, to go back to.
struct Written
    at::Int
    names::Int
    serial::Int
    lookbehinds::Int
    walks::Int
    written::Vector{Int}
    read::Vector{String}
end

Written(E::Emitter) = Written(position(E.out), length(E.names), E.serial, length(E.lookbehinds), length(E.walks),
    copy(E.written), copy(E.read))

function restore!(E::Emitter, w::Written)
    truncate(E.out, w.at)
    seekend(E.out)
    resize!(E.names, w.names)
    resize!(E.lookbehinds, w.lookbehinds)
    resize!(E.walks, w.walks)
    E.serial = w.serial
    copyto!(E.written, w.written)
    copyto!(E.read, w.read)
    return nothing
end

# Writes a repetition X{m,n}.
#
# A repetition that never runs is not written at all. It matches the empty string, and its groups are never set.
# (PCRE2 10.42 and 10.46 are also wrong about X{0} when it is written: when an alternative of X starts with \A they
# take the pattern for one that can only match at the start of the text, so (?:b|\A){0}a does not match "ba".)
#
# A repetition whose body can only match the empty string, and that runs, is its body once. Every iteration starts
# at the same place with the groups of the body unset, so each can do exactly what the first can, and what is left
# after the last is what one leaves.
#
# # An iteration that matches the empty string
#
# ECMA-262 refuses an iteration past the minimum that matches the empty string. PCRE2 takes one. Where no group is
# read, the two then match the same texts, but PCRE2 tries every way of putting empty iterations among the others,
# which for a repetition inside a repetition is a number of ways that grows with a power of the length of the
# text. Where a group of the body is read after the repetition, they differ: PCRE2 leaves the repetition after an
# empty iteration and shows the groups as that iteration set them. So for a body that can match the empty string
# the iterations past the minimum are written as ones that take a character. X{m,n} is (?:X){m}(?:N){0,n-m}:
#
# - N is X without its empty matches where that is plain to write (see `nonempty`): (?:a*)* is (?:a+)*, and
#   (?:a?b?)* is (?:ab?|b)*;
# - otherwise N is (?=(?<e1>(?s:.*)))X(?!\k<e1>). The group e1 is the rest of the text where the iteration
#   starts. The reference to it matches only where the rest of the text is still that, which is where X has taken
#   nothing.
#
# # A group that a backreference reads
#
# A body with such a group is written once, as two groups of one name would be two groups. (?:X){m,n} serves for
# a minimum of one or more. For a minimum of zero it is (?|(?:X){1,n}|(?<g1>)...), so that the groups of X are set
# when it runs no times too (see `alternatives`).
#
# The exception is a repetition with a minimum of one or more whose body can match the empty string and whose
# groups are read after it. Its iterations up to the minimum may match the empty string and the later ones may not,
# so the two parts are written apart, the second with groups of other names, and a backreference after the
# repetition reads the groups of the part that ran last: (?(?=(?s:.*+)\k<e1>)\k<g1>|\k<g1_2>). The condition asks
# whether e1 is the empty string, which is all that matches at the end of the text. The empty alternative of the
# second part sets e1 to that, and an iteration of the second part sets it to the rest of the text where it
# started, which is not empty as the iteration took a character. (Asking whether e1 matches where the
# backreference stands would not do: a backreference after a lookahead stands where the lookahead started.) The
# copies multiply when such repetitions are inside one another, and a pattern whose translation passes
# MAX_COPIED_TRANSLATION is refused.
function writerepeat(E::Emitter, n::Node)
    # PCRE2 writes a repeated group out once for each iteration, and the callout before a character puts it in
    # a group. So under a large count the characters are written without it, as a{70000} is, which PCRE2 keeps as
    # a count. The callout only cuts short what cannot match.
    if E.within && (n.min > MAX_HELD_COUNT || (n.max != UNBOUNDED && n.max < MAX_COUNT && n.max > MAX_HELD_COUNT))
        E.within = false
        repeated(E, n)
        E.within = true
    else
        repeated(E, n)
    end
    return nothing
end

function repeated(E::Emitter, n::Node)
    neverruns(n) && return nothing
    body = n.subs[1]
    out = E.out
    start = position(out)
    lo = n.min
    # A maximum no text reaches is no maximum.
    hi = n.max >= MAX_COUNT ? UNBOUNDED : n.max
    if maxlength(body) == 0
        atom(E, body)
        return nothing
    end
    if lo >= MAX_COUNT
        # More iterations than a text has characters.
        minlength(body) > 0 || toolarge(E, TOO_LARGE_COUNT)
        print(out, NEVER_MATCHES)
        return nothing
    end
    more = hi == UNBOUNDED ? UNBOUNDED : hi - lo
    # Whether an iteration past the minimum could match the empty string.
    optional = hi != lo && minlength(body) == 0
    if !holdsreferenced(E, n)
        optional ? writeoptional(E, n, lo, more) : counted(E, body, lo, hi, n.lazy)
        return nothing
    end
    if !optional
        if lo > 1 && E.backwards
            # In a lookbehind PCRE2 matches the iterations from the left and ECMA-262 from the right, so the last
            # iteration, whose groups are the ones left set, is the leftmost. (The count is exact here, as the
            # length is fixed.) The body is written first with its groups, then the rest without.
            atom(E, body)
            E.uncaptured += 1
            atom(E, body)
            lo == 2 || quantifier(out, lo - 1, lo - 1, false)
            E.uncaptured -= 1
        elseif lo > 0
            atom(E, body)
            (lo == 1 && hi == 1) || quantifier(out, lo, hi, n.lazy)
        else
            text, inner = apart(E, body, more, n.lazy)
            zerotimes(E, text, inner, n.lazy)
        end
        return nothing
    end
    if lo > 0 && !n.exports
        # No backreference reads the groups after the repetition, so the two parts need no telling apart, and the
        # count alone is exact too. It is what is written when the two parts are too much text.
        state = Written(E)
        atom(E, body)
        lo == 1 || quantifier(out, lo, lo, false)
        mark, text, inner = guardedapart(E, body, more, n.lazy)
        print(out, "(?:", text, ')', n.lazy ? "??" : "?")
        append!(E.names, inner)
        if position(out) - start > MAX_COPIED_TRANSLATION
            restore!(E, state)
            atom(E, body)
            quantifier(out, lo, hi, n.lazy)
        end
        return nothing
    end
    first = (n.firstgroup + 1):n.lastgroup
    before = String[]
    if lo > 0
        atom(E, body)
        lo == 1 || quantifier(out, lo, lo, false)
        before = E.read[first]
    else
        plain = nonempty(E, body, true, Ref(MAX_REWRITTEN_NODES))
        if plain !== nothing
            text, inner = apart(E, plain, more, n.lazy)
            zerotimes(E, text, inner, n.lazy)
            return nothing
        end
    end
    mark, text, inner = guardedapart(E, body, more, n.lazy)
    zerotimes(E, text, inner, n.lazy)
    if lo > 0
        for (k, g) in enumerate(first)
            isempty(before[k]) && continue
            E.read[g] = string("(?(?=(?s:.*+)\\k<", mark, ">)", before[k], '|', E.read[g], ')')
        end
        position(out) - start > MAX_COPIED_TRANSLATION && toolarge(E, TOO_LARGE_REPETITION)
    end
    return nothing
end

# Writes a node repeated one to `hi` times to a buffer of its own. Returns the text and the names of its groups.
function apart(E::Emitter, n::Node, hi::Int, lazy::Bool)
    out = E.out
    names = E.names
    E.out = IOBuffer()
    E.names = String[]
    atom(E, n)
    hi == 1 || quantifier(E.out, 1, hi, lazy)
    text = String(take!(E.out))
    inner = E.names
    E.out = out
    E.names = names
    return text, inner
end

# Writes a node repeated one to `hi` times, each time taking a character, to a buffer of its own. Returns the name
# of the group that holds the rest of the text where the last iteration started, the text, and the names of its
# groups.
function guardedapart(E::Emitter, n::Node, hi::Int, lazy::Bool)
    out = E.out
    names = E.names
    E.out = IOBuffer()
    E.names = String[]
    mark = helpername(E, 'e')
    push!(E.names, mark)
    print(E.out, "(?:(?=(?<", mark, '>', REST_OF_TEXT, "))")
    node(E, n)
    print(E.out, "(?!\\k<", mark, ">))")
    hi == 1 || quantifier(E.out, 1, hi, lazy)
    text = String(take!(E.out))
    inner = E.names
    E.out = out
    E.names = names
    return mark, text, inner
end

# Writes a repetition that runs once or more as `text`, with the alternative of running no times, which sets its
# groups to the empty string.
function zerotimes(E::Emitter, text::String, inner::Vector{String}, lazy::Bool)
    out = E.out
    print(out, "(?|")
    if lazy
        pad(out, inner)
        print(out, '|', text)
    else
        print(out, text, '|')
        pad(out, inner)
    end
    print(out, ')')
    append!(E.names, inner)
    return nothing
end

# Writes a repetition with no group that is read, whose body can match the empty string and that has `more`
# iterations past its minimum `lo`, so that those take a character (see `writerepeat`). When the two parts are too
# much text, the repetition is written with its count. That is exact too, as no group is read, and slower.
function writeoptional(E::Emitter, n::Node, lo::Int, more::Int)
    body = n.subs[1]
    out = E.out
    state = Written(E)
    fits = true
    print(out, "(?:")
    if lo == 1
        atom(E, body)
    elseif lo > 1
        counted(E, body, lo, lo, false)
    end
    plain = nonempty(E, body, false, Ref(MAX_REWRITTEN_NODES))
    if plain !== nothing
        counted(E, plain, 0, more, n.lazy)
    elseif more == UNBOUNDED || more <= PCRE_MAX_COUNT
        mark = helpername(E, 'e')
        push!(E.names, mark)
        print(out, "(?:(?=(?<", mark, '>', REST_OF_TEXT, "))")
        node(E, body)
        print(out, "(?!\\k<", mark, ">))")
        quantifier(out, 0, more, n.lazy)
    else
        fits = false
    end
    print(out, ')')
    if !fits || position(out) - state.at > MAX_COPIED_TRANSLATION
        restore!(E, state)
        counted(E, body, lo, more == UNBOUNDED ? UNBOUNDED : lo + more, n.lazy)
    end
    return nothing
end

# Writes a node repeated between `lo` and `hi` times.
#
# PCRE2 counts to 65535 and ECMA-262 counts further. A larger count is written as several repetitions that match the
# same texts and try the counts in the same order (see `exactly` and `upto`). The body is then written more than
# once, which a body with a group that a backreference reads is not: `writerepeat` writes such a body itself.
function counted(E::Emitter, body::Node, lo::Int, hi::Int, lazy::Bool)
    out = E.out
    hi == UNBOUNDED && unbounded(E, body, lo, lazy) && return nothing
    largest = hi == UNBOUNDED ? lo : hi
    # A count that PCRE2 can be given, of a node that is not written in blocks from it.
    if largest <= PCRE_MAX_COUNT &&
       (largest < min(BLOCK_SIZES[][1][1], BLOCK_SIZES[][2][1]) || largest < blockcount(E, body))
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

# Counts of one character or class beyond what PCRE2 can be given.
#
# PCRE2 keeps the count of a character or class as a number up to 65535, writes a counted group out once for each
# time, takes a nest of groups to a depth of 250, and holds a compiled pattern of at most 64 KiB. So
# `(?:a{65535}){15259}`, which is a{1000000000}, is too large for it, and so is the nest that `upto` writes for
# a{0,1000000000}. A class of many ranges is a call of a group (see `writeclass`), which PCRE2 writes out for each
# time like any group, so \p{L}{30000} is too large as well.
#
# Such a count is written in blocks. A block is a group among the definitions of the pattern that the pattern
# calls: (?<C1>(?>a{32768})) is 32,768 characters, and (?<D1>(?>(?&C1){256})) is 256 of those. A count below 2^31
# is then at most 255 calls of the larger block, 255 of the smaller and a count of the character, and it is that
# much text whatever the count. A class that is a call is itself written out for each time, and with search trees
# it is a class and a call, so it has six sizes of block, each 32 of the one before, and is never written more
# than 32 times together. A call matches what the group matches, and one character at a time is matched in one
# way only, so the blocks change nothing of what is matched or of the order in which the numbers of times are
# tried (see `uptoblocks`). For the same reason a block is an atomic group: PCRE2 keeps nothing to come back into it, so the
# memory of a match does not grow with the count.
#
# For each kind, the count from which blocks are written and the sizes of the blocks, each in blocks of the size
# before it. A smaller count is written as it was before there were blocks.
const PLAIN_BLOCKS = (1 << 20, [1 << 15, 1 << 8])
const CALLED_BLOCKS = (1 << 10, fill(1 << 5, 6))
# The two in use, for a character or class that PCRE2 keeps a count of and for one that is a call. The tests set
# small ones, to try every form on texts of a few characters.
const BLOCK_SIZES = Ref((PLAIN_BLOCKS, CALLED_BLOCKS))
# The letters of the names of the blocks, smallest first. They are capitals, which no other name the emitter gives
# a group starts with: `g1` is the first group of the pattern and `e1` a mark of where an iteration started, and a
# block of those names was a group of the same name twice, or a call of the wrong one.
const BLOCK_LETTERS = "CDEFGH"

# What a character or class is written as, or nothing for any other node. Only one character at a time is counted
# in blocks.
function unittext(E::Emitter, body::Node)
    unit = (body.kind == CHAR && !issurrogate(body.codepoint)) ||
        (body.kind == SET && intersects(body.set, NOT_SURROGATE_SET))
    unit || return nothing
    out = E.out
    E.out = IOBuffer()
    node(E, body)
    text = String(take!(E.out))
    E.out = out
    return text
end

# The count from which a character or class with this text is written in blocks, and the sizes of its blocks.
blocksizes(text::String) = startswith(text, '(') ? BLOCK_SIZES[][2] : BLOCK_SIZES[][1]

# The count from which the node is written in blocks, or a count no pattern has when it never is.
function blockcount(E::Emitter, body::Node)
    text = unittext(E, body)
    return text === nothing ? typemax(Int) : blocksizes(text)[1]
end

# The number of the blocks of a character or class in the pattern being written and their sizes in characters,
# smallest first, when `count` is one that is written in blocks. Otherwise 0.
function blocks(E::Emitter, body::Node, count::Int)
    count >= min(BLOCK_SIZES[][1][1], BLOCK_SIZES[][2][1]) || return 0, NO_INTS
    text = unittext(E, body)
    text === nothing && return 0, NO_INTS
    from, sizes = blocksizes(text)
    count >= from || return 0, NO_INTS
    number = findfirst(==(text), E.blocks)
    if number === nothing
        push!(E.blocks, text)
        number = length(E.blocks)
    end
    return number, cumprod(sizes)
end

# Writes from `least` to `most` calls of the block of a level, most first unless `lazy`.
function calls(out::IO, level::Int, number::Int, least::Int, most::Int, lazy::Bool)
    most == 0 && return nothing
    print(out, "(?&", BLOCK_LETTERS[level], number, ')')
    quantifier(out, least, most, lazy)
    return nothing
end

# Writes from `least` to `most` times the character or class itself.
function units(E::Emitter, body::Node, least::Int, most::Int, lazy::Bool)
    most == 0 && return nothing
    node(E, body)
    quantifier(E.out, least, most, lazy)
    return nothing
end

# Writes a character or class, whose blocks have the number and the sizes given, repeated exactly `count` times.
function exactblocks(E::Emitter, body::Node, number::Int, sizes::Vector{Int}, count::Int)
    for level in length(sizes):-1:1
        calls(E.out, level, number, count ÷ sizes[level], count ÷ sizes[level], false)
        count %= sizes[level]
    end
    units(E, body, count, count, false)
    return nothing
end

# Writes a character or class repeated up to `count` times, with the blocks of the levels up to `level`. A number
# of times is so many of the largest block, fewer of each smaller one than make the next, and fewer of the character
# than make the smallest, in one way only. The alternatives and the counts are in the order that tries the numbers
# of times from the largest down, or from the smallest up when the repetition is lazy, which is the order ECMA-262
# tries them in. Every number of times up to the length of the run of the character can be taken, so the first that
# is tried and fits is the one ECMA-262 takes first.
function uptoblocks(E::Emitter, body::Node, number::Int, sizes::Vector{Int}, level::Int, count::Int, lazy::Bool)
    out = E.out
    if level == 0
        units(E, body, 0, count, lazy)
        return nothing
    end
    most = count ÷ sizes[level]
    rest = count % sizes[level]
    if most == 0
        uptoblocks(E, body, number, sizes, level - 1, rest, lazy)
        return nothing
    end
    if rest == sizes[level] - 1
        # Any number of blocks up to the most, each with any number of times below one more.
        calls(out, level, number, 0, most, lazy)
        uptoblocks(E, body, number, sizes, level - 1, rest, lazy)
        return nothing
    end
    # Every block of this level and up to the rest, or fewer blocks and any number of times below one more.
    function all()
        calls(out, level, number, most, most, false)
        uptoblocks(E, body, number, sizes, level - 1, rest, lazy)
    end
    function fewer()
        calls(out, level, number, 0, most - 1, lazy)
        uptoblocks(E, body, number, sizes, level - 1, sizes[level] - 1, lazy)
    end
    print(out, "(?:")
    if lazy
        fewer()
        print(out, '|')
        all()
    else
        all()
        print(out, '|')
        fewer()
    end
    print(out, ')')
    return nothing
end

# The most bytes of sets of many ranges that a pattern holds where they stand. PCRE2, as Julia builds it, holds a
# compiled pattern of at most 64 KiB, and the class of \p{L} is some 6,000 bytes as it is written and 4,000
# compiled.
const MAX_INLINED_SETS = 24_000
# The same, for the tests to set, which try the form of the sets past it on short patterns.
const INLINED_SETS = Ref(MAX_INLINED_SETS)

# Writes a set of many ranges repeated `lo` times or more with no upper count, and says whether it did. (Any other
# node is written by `counted`.)
#
# Such a set is a call of a group where it stands (see `writeclass`), and PCRE2 keeps a place to come back to for
# every turn of a repeated group, which a call is to it. So `(?&s1)+`, which is what \p{L}+ was written as, kept a
# place for each character of the text, on the megabyte of stack JIT code has and then in the 64 MiB the
# interpreter may take, and ^\p{L}+$ was a MatchError on a text of 150,000 letters. A class that stands where it
# is repeated has no such cost: PCRE2 keeps a count for it.
#
# So the set is written as its class, where it stands, for the first sets of a pattern up to MAX_INLINED_SETS
# bytes together. That is so with either PCRE2. One that tries the ranges of a class one after another (before
# 10.45) was measured on 100,000 characters at 1 to 2 ns a character up to U+00FF and 250 to 530 ns beyond, where
# the search tree under a repetition took 170 ns and 1,500 to 2,800 ns, as each character is then several steps,
# and a match of many steps is timed (jl-probe/r5/inline10.txt).
#
# For the sets past those bytes the repetition is written with the blocks of a count: any number of the largest
# block, then up to one fewer than fills it, as `uptoblocks` writes that. Every number of times is reached in one
# way, and the numbers are tried from the largest down, or from the smallest up when the repetition is lazy, as
# before. A block is an atomic group, so what PCRE2 keeps is a place for each block of a level, at most 31 for each
# level, and one for each of the largest, a block of 2^30 characters.
function unbounded(E::Emitter, body::Node, lo::Int, lazy::Bool)
    (body.kind == SET && !E.within) || return false
    set = intersects(body.set, SURROGATE_SET) ? intersection(body.set, NOT_SURROGATE_SET) : body.set
    manyranges(set) || return false
    out = E.out
    class = sprint(writeset, set)
    if E.inlined + ncodeunits(class) <= INLINED_SETS[]
        E.inlined += ncodeunits(class)
        print(out, class)
        quantifier(out, lo, UNBOUNDED, lazy)
        return true
    end
    # With search trees a set with no member beyond U+00FF is a class where it stands.
    text = unittext(E, body)
    (text === nothing || !startswith(text, '(')) && return false
    number = findfirst(==(text), E.blocks)
    if number === nothing
        push!(E.blocks, text)
        number = length(E.blocks)
    end
    sizes = cumprod(blocksizes(text)[2])
    top = length(sizes)
    print(out, "(?:")
    exactblocks(E, body, number, sizes, lo)
    if lazy
        uptoblocks(E, body, number, sizes, top - 1, sizes[top] - 1, true)
        calls(out, top, number, 0, UNBOUNDED, true)
    else
        calls(out, top, number, 0, UNBOUNDED, false)
        uptoblocks(E, body, number, sizes, top - 1, sizes[top] - 1, false)
    end
    print(out, ')')
    return true
end

# Writes a node repeated exactly `count` times, for any count, as X{65535} as often as it goes and then the rest.
function exactly(E::Emitter, body::Node, count::Int)
    out = E.out
    number, sizes = blocks(E, body, count)
    number > 0 && return exactblocks(E, body, number, sizes, count)
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
    number, sizes = blocks(E, body, count)
    number > 0 && return uptoblocks(E, body, number, sizes, length(sizes), count, lazy)
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
# capture and in what a backreference inside the body reads (see the note at the top of the file).
#
# PCRE2 10.42 takes a body only when its length is fixed, and later versions take one whose length is bounded and
# short. So that a pattern means the same on each, a body is written as it is only when its length is fixed. Any
# other body X is compiled apart, as (?C1)S(?:X)(?C0), and the lookbehind is written as the callout (?Cn) that names
# it, inside (?!...) when it is negative. When the match reaches the callout, the matcher searches the text for X.
# S takes the text up to a start (see below), and the callout 0 after X lets the search succeed only where X ends
# at the position of the lookbehind. That is the definition of a lookbehind, which holds when its body matches some
# stretch of the text that ends where it stands. The search takes time that grows with the square of the length of
# the text.
#
# X can match only up to the position of the lookbehind, and left to itself it runs on past that position and comes
# back, which for a repetition inside a repetition, as in (?<=(?:[ax]+){2,})\$, is every way to split the text after
# the position before the first way that ends at it. ECMA-262 runs the body backwards from the position and never
# sees the text after it. So each character X takes is written after the callout 254, which lets it be taken only
# before the position: (?:(?C254)[ax]). A lookaround inside X is not held to the position.
#
# For the same reason the starts are tried from the position backwards, the nearest first, as ECMA-262 comes to
# them. The callout 1 holds the search to the start of the text, and what stands before X takes the text up to the
# start that is tried: (?:(?C254)(?s:.))*, which goes as far as the position and gives back one character at a
# time. When the characters X can take are a set T of few ranges, no start before a character outside T is tried,
# as X cannot match from one and would try every way to split the text up to that character. What stands before X
# is then (?>(?:(?:(?C254)(?s:.))*(?C254)[^T])?)(?:(?C254)T)*: up to just after the last character before the
# position that is not in T, once, and then over characters of T. An X that begins with ^ has one start, the start
# of the text, and nothing stands before it.
#
# ECMA-262 comes first to the last character of X. So when X takes at least one character and the characters it
# can end with are a set of few ranges, the callout stands after a lookbehind for one character of that set, and
# X is not searched for where it cannot end: (?<=[x])(?C2) for (?<=(?:a+)+x).
#
# A body that holds a LIVE backreference is a search that PCRE2 runs (see `walk`).
# The characters a node can take, outside the lookarounds in it, or nothing when that is any character (a
# backreference takes what its group holds).
function taken(n::Node)::Union{Nothing,CodeSet}
    kind = n.kind
    kind == CHAR && return Int32[n.codepoint, n.codepoint]
    kind == SET && return n.set
    kind == LOOK && return NO_CHARACTERS
    if kind == BACKREF
        # One that never reads a group takes nothing.
        return all(==(NEVER), n.statuses) ? NO_CHARACTERS : nothing
    end
    set = NO_CHARACTERS
    for sub in n.subs
        one = taken(sub)
        one === nothing && return nothing
        set = setunion(set, one)
    end
    return set
end

# The characters that can be the last a node takes when it takes one, or nothing when that is not known (a
# backreference ends with what its group holds).
function ends(n::Node)::Union{Nothing,CodeSet}
    kind = n.kind
    kind == CHAR && return Int32[n.codepoint, n.codepoint]
    kind == SET && return n.set
    if kind == BACKREF
        return all(==(NEVER), n.statuses) ? NO_CHARACTERS : nothing
    elseif kind == GROUP || kind == REPEAT
        return ends(n.subs[1])
    elseif kind == ALT
        set = NO_CHARACTERS
        for sub in n.subs
            one = ends(sub)
            one === nothing && return nothing
            set = setunion(set, one)
        end
        return set
    elseif kind == CAT
        # The last term, and the one before it when the last can match the empty string, and so on.
        set = NO_CHARACTERS
        for k in length(n.subs):-1:1
            sub = n.subs[k]
            one = ends(sub)
            one === nothing && return nothing
            set = setunion(set, one)
            minlength(sub) > 0 && break
        end
        return set
    end
    # An assertion takes nothing.
    return NO_CHARACTERS
end

# Whether a node can match only from the start of the text: it begins with ^ outside a group with the m modifier.
function startsthetext(n::Node)
    kind = n.kind
    kind == BOL && return true
    kind == GROUP && return startsthetext(n.subs[1])
    kind == CAT && return !isempty(n.subs) && startsthetext(n.subs[1])
    kind == ALT && return all(startsthetext, n.subs)
    return false
end

function lookbehind(E::Emitter, n::Node)
    E.haslookbehind = true
    body = n.subs[1]
    out = E.out
    if !E.searchinglookbehind && fixedlength(body)
        backwards = E.backwards
        E.backwards = true
        state = Written(E)
        print(out, n.negative ? "(?<!" : "(?<=")
        node(E, body)
        print(out, ')')
        if body.kind == ALT && length(E.names) > state.names
            # The alternatives write groups, so `alternatives` put them in (?|...). PCRE2 takes alternatives of
            # different lengths only at the top of a lookbehind, where that group cannot stand (10.42 refuses
            # them inside a group, and later versions answer some of them wrongly). A lookbehind for each
            # alternative means the same, inside a lookahead so that the whole is as atomic.
            restore!(E, state)
            print(out, n.negative ? "(?!" : "(?=")
            alternatives(E, body.subs, "(?<=", ")")
            print(out, ')')
        end
        E.backwards = backwards
        return nothing
    end
    # Neither a search nor a callout sets a group of the pattern.
    n.exports && unsupported(E, REFUSED_GROUP_IN_LOOKBEHIND)
    if n.haslive
        walk(E, n)
        return nothing
    end
    push!(E.lookbehinds, "")
    index = length(E.lookbehinds)
    number = CALLOUT_FIRST_LOOKBEHIND + index - 1
    if number > PCRE_MAX_LOOKBEHIND_CALLOUT
        toolarge(E, TOO_MANY_LOOKBEHINDS)
    end
    # The body is a pattern of its own, with its own definitions. It holds no group that is read and no LIVE
    # backreference, and the names it writes for itself are its own.
    largesets = E.largesets
    walks = E.walks
    blocks = E.blocks
    names = E.names
    backwards = E.backwards
    E.backwards = false
    E.out = IOBuffer()
    E.largesets = CodeSet[]
    E.walks = String[]
    E.blocks = String[]
    E.names = String[]
    print(E.out, LOOKBEHIND_PREFIX)
    before = string("(?C", CALLOUT_BEFORE_TARGET, ')')
    reach = taken(body)
    if startsthetext(body)
        # X can match only from the start of the text, which is where the search starts.
    elseif reach !== nothing && length(reach) <= 2 * MAX_REACH_RANGES
        # To just after the last character before the position that X cannot take, and then over those it can.
        print(E.out, "(?>(?:(?:", before, "(?s:.))*", before)
        writeclass(E, complement(reach))
        print(E.out, ")?)")
        if !isempty(reach)
            print(E.out, "(?:", before)
            writeclass(E, reach)
            print(E.out, ")*")
        end
    else
        print(E.out, "(?:", before, "(?s:.))*")
    end
    print(E.out, "(?:")
    E.within = true
    node(E, body)
    E.within = false
    print(E.out, ")(?C", CALLOUT_AT_TARGET, ')')
    definitions(E)
    E.lookbehinds[index] = String(take!(E.out))
    E.out = out
    E.largesets = largesets
    E.walks = walks
    E.blocks = blocks
    E.names = names
    E.backwards = backwards
    # The body is searched for only where the character before the lookbehind is one the body can end with.
    last = minlength(body) > 0 ? ends(body) : nothing
    test = IOBuffer()
    if last !== nothing && min(length(last), length(complement(last))) <= 2 * MAX_REACH_RANGES
        print(test, "(?<=")
        writeset(test, last)
        print(test, ')')
    end
    if n.negative
        print(out, "(?!", String(take!(test)), "(?C", number, "))")
    else
        print(out, String(take!(test)), "(?C", number, ')')
    end
    return nothing
end

# Writes a lookbehind whose body X holds a backreference to a group outside it, as a search for X that PCRE2 runs
# itself, so that X reads the group.
#
# Where the lookbehind stands, (?=(?<r1>(?s:.*))) captures the rest of the text, which says where that is. The
# search is the group w1, written once among the definitions of the pattern:
#
#     (?<w1>(?=(?:X)\k<r1>\z)|(?<=(?=(?&w1))(?s:.)))
#
# It holds where X matches from here and ends where the lookbehind stands (the rest of the text after X is r1), or
# where the search holds one character back. The lookbehind of one character is the step back, and PCRE2 takes it
# in every version. So the search tries every start from the lookbehind back to the start of the text. It calls
# itself once for each character it steps back, and a text that is too long for that ends in a MatchError, never in
# a wrong answer.
#
# No group of X is read outside the lookbehind (the pattern is refused otherwise), so what the search captures does
# not matter, and neither does the direction X is matched in.
function walk(E::Emitter, n::Node)
    out = E.out
    rest = helpername(E, 'r')
    search = helpername(E, 'w')
    push!(E.names, rest)
    print(out, "(?=(?<", rest, '>', REST_OF_TEXT, "))")
    names = E.names
    backwards = E.backwards
    E.backwards = false
    E.out = IOBuffer()
    E.names = String[]
    print(E.out, "(?<", search, ">(?=(?:")
    node(E, n.subs[1])
    print(E.out, ")\\k<", rest, ">\\z)|(?<=(?=(?&", search, "))", ANY_CHARACTER, "))")
    push!(E.walks, String(take!(E.out)))
    E.out = out
    E.names = names
    E.backwards = backwards
    print(out, n.negative ? "(?!(?&" : "(?=(?&", search, "))")
    return nothing
end

# Writes the terms of the pattern from the one numbered `from`. A lookbehind that is a search for where it starts
# takes the terms after it into the search.
function terms(E::Emitter, subs::Vector{Node}, from::Int)
    for k in from:length(subs)
        sub = subs[k]
        if sub.kind == LOOK && sub.continued
            continuation(E, sub, subs, k + 1)
            return nothing
        end
        node(E, sub)
    end
    return nothing
end

# Writes a lookbehind of no fixed length whose groups are read after it, with the rest of the pattern.
#
# PCRE2 sets a group only where the match passes over its text, and it steps back only by a fixed number of
# characters or in a search like that of `walk`, which is a call of a group, and a call gives back no group it set.
# So the lookbehind is written as such a search and what follows it in the pattern is written inside the search,
# where the groups are still set. For (?<=X)Y, with r1 the rest of the text where the lookbehind stands:
#
#     (?<w2>(?(?=(?<m3>F(?=\k<r1>\z)))\k<m3>Y|(?<=(?=(?&w2))(?s:.))))
#
# F is X written forwards so that it captures what ECMA-262 captures matching X backwards (see `writeedge`). The
# search steps back from the lookbehind until F matches from where it is to where the lookbehind stands. There it
# stops: F is the condition of the group, which PCRE2 does not come back into, as nothing comes back into a
# lookbehind that has held. The reference to m3, the text F matched, goes back to where the lookbehind stands, and
# Y is matched from there. Y runs to the end of the pattern, and whether it matches is all that is asked. So this is
# written only for a lookbehind that is a term of the pattern itself, where what follows it is the rest of the
# pattern. Anywhere else such a lookbehind is refused.
function continuation(E::Emitter, n::Node, subs::Vector{Node}, from::Int)
    E.haslookbehind = true
    out = E.out
    rest = helpername(E, 'r')
    search = helpername(E, 'w')
    push!(E.names, rest)
    print(out, "(?=(?<", rest, '>', REST_OF_TEXT, "))")
    names = E.names
    backwards = E.backwards
    edgerest = E.edgerest
    E.out = IOBuffer()
    E.names = String[]
    E.backwards = true
    E.edgerest = rest
    taken = helpername(E, 'm')
    print(E.out, "(?<", search, ">(?(?=(?<", taken, '>')
    body = n.subs[1]
    if body.kind == ALT
        # ECMA-262 takes the first alternative that matches. Each is written to end where the lookbehind stands,
        # and there to find that none of those before it matches.
        texts = String[]
        all = String[]
        starts = Int[]
        inner = E.out
        outer = E.names
        for k in eachindex(body.subs)
            E.out = IOBuffer()
            E.names = String[]
            writeedge(E, n, body.subs[k], Int64(0), false)
            print(E.out, "(?=\\k<", rest, ">\\z)")
            for j in 1:(k - 1)
                notbehind(E, body.subs[j])
            end
            push!(texts, String(take!(E.out)))
            push!(starts, length(all) + 1)
            append!(all, E.names)
        end
        push!(starts, length(all) + 1)
        E.out = inner
        E.names = outer
        padded(E, texts, all, starts, "", "")
    else
        writeedge(E, n, body, Int64(0), false)
        print(E.out, "(?=\\k<", rest, ">\\z)")
    end
    print(E.out, "))\\k<", taken, '>')
    E.backwards = false
    terms(E, subs, from)
    print(E.out, "|(?<=(?=(?&", search, "))")
    # The search steps back a character at a time. Where the lookbehind is a repetition and nothing else, it
    # can only start a whole number of copies of the body back, so the search steps back over copies of the body
    # and ends where one does not match.
    repeated = body
    while repeated.kind == GROUP
        repeated = repeated.subs[1]
    end
    if repeated.kind == REPEAT && !rigid(repeated) && rigid(repeated.subs[1])
        uncaptured(E, repeated.subs[1])
    else
        print(E.out, ANY_CHARACTER)
    end
    print(E.out, ")))")
    push!(E.walks, String(take!(E.out)))
    E.out = out
    E.names = names
    E.backwards = backwards
    E.edgerest = edgerest
    print(out, "(?=(?&", search, "))")
    return nothing
end

# Writes a negative lookbehind for a node, with groups that do not capture.
function notbehind(E::Emitter, body::Node)
    look = Node(LOOK)
    look.behind = true
    look.negative = true
    look.subs = Node[body]
    look.haslive = any(found -> any(==(LIVE), found.statuses), backreferences(body))
    backwards = E.backwards
    E.backwards = false
    E.uncaptured += 1
    lookbehind(E, look)
    E.uncaptured -= 1
    E.backwards = backwards
    return nothing
end

# The backreferences inside a node.
function backreferences(n::Node, found::Vector{Node}=Node[])
    n.kind == BACKREF && push!(found, n)
    for sub in n.subs
        backreferences(sub, found)
    end
    return found
end

# Writes, forwards, a node that is the leftmost of a lookbehind, so that it captures what ECMA-262 captures matching
# it backwards. `after` is the number of characters of the lookbehind after the node, and `captured` says that the
# node is inside a group that is read. The node is what ECMA-262 matches last, so nothing can fail after it and send
# the match back into it, and it takes the first of its ways that matches:
#
# - a node of a fixed length that reads no group has one place, and is written as it is (see `rigid`);
# - a node with no group that is read after the lookbehind, outside a group that is read, is written as it is too.
#   It may match from more than one place, and nothing tells them apart;
# - a group is its body;
# - a sequence is its first term, written this way, and then terms of a fixed length. Or it is terms with no group
#   that is read after the lookbehind, and then terms of a fixed length;
# - a greedy repetition X{m,n} of a body of a fixed length runs as many times as it can. The iteration ECMA-262
#   runs last is the leftmost, so the first copy is written with the groups and the others without:
#   (?<!X)(X)X{m-1,}. The lookbehind before it says that X does not match once more to the left, so that these
#   are all the iterations. With a maximum the repetition also stops there, at any place from which that many
#   copies reach the end of the repetition: (?:(?<!X)|(?=X{n}.{after}\k<r1>\z))(X)X{m-1,n-1};
# - of alternatives that each have a fixed length, the first that matches is taken. They end at one place, so each
#   is written with a lookbehind after it for each one before it, which says that one does not match:
#   (?:A|B(?<!A)|C(?<!A)(?<!B)).
#
# `edgeable` says whether a node is one of these. Any other is refused.
function writeedge(E::Emitter, look::Node, n::Node, after::Int64, captured::Bool)
    out = E.out
    if rigid(n)
        node(E, n)
        return nothing
    end
    after > PCRE_MAX_COUNT && unsupported(E, REFUSED_GROUP_IN_LOOKBEHIND)
    if !captured && !exports(E, look, n)
        E.backwards = false
        node(E, n)
        E.backwards = true
        return nothing
    end
    kind = n.kind
    if kind == GROUP
        g = n.group
        if g == 0 || !E.referenced[g] || E.uncaptured > 0
            print(out, "(?:")
            writeedge(E, look, n.subs[1], after, captured)
            print(out, ')')
            return nothing
        end
        E.written[g] += 1
        name = E.written[g] == 1 ? string('g', g) : string('g', g, '_', E.written[g])
        push!(E.names, name)
        print(out, "(?<", name, '>')
        writeedge(E, look, n.subs[1], after, true)
        print(out, ')')
        E.read[g] = string("\\k<", name, '>')
    elseif kind == CAT && fixedend(n) > 1
        from = fixedend(n)
        tail = Int64(0)
        for k in from:length(n.subs)
            tail = plus(tail, minlength(n.subs[k]))
        end
        if from == 2
            writeedge(E, look, n.subs[1], plus(after, tail), captured)
        else
            (captured || any(k -> exports(E, look, n.subs[k]), 1:(from - 1))) &&
                unsupported(E, REFUSED_GROUP_IN_LOOKBEHIND)
            E.backwards = false
            for k in 1:(from - 1)
                node(E, n.subs[k])
            end
            E.backwards = true
        end
        for k in from:length(n.subs)
            node(E, n.subs[k])
        end
    elseif kind == REPEAT && edgeable(E, look, n, after, true)
        body = n.subs[1]
        hi = n.max >= MAX_COUNT ? UNBOUNDED : n.max
        if hi == UNBOUNDED
            print(out, "(?<!")
            uncaptured(E, body)
            print(out, ')')
        else
            print(out, "(?:(?<!")
            uncaptured(E, body)
            print(out, ")|(?=")
            uncaptured(E, body)
            quantifier(out, hi, hi, false)
            after > 0 && print(out, ANY_CHARACTER, '{', after, '}')
            print(out, "\\k<", E.edgerest, ">\\z))")
        end
        more = hi == UNBOUNDED ? UNBOUNDED : hi - 1
        if n.min > 0
            atom(E, body)
            if more != 0
                uncaptured(E, body)
                quantifier(out, n.min - 1, more, false)
            end
            return nothing
        end
        names = E.names
        E.out = IOBuffer()
        E.names = String[]
        atom(E, body)
        if more != 0
            uncaptured(E, body)
            quantifier(E.out, 0, more, false)
        end
        text = String(take!(E.out))
        inner = E.names
        E.out = out
        E.names = names
        zerotimes(E, text, inner, false)
    elseif kind == ALT && all(rigid, n.subs)
        texts = String[]
        every = String[]
        starts = Int[]
        names = E.names
        for k in eachindex(n.subs)
            E.out = IOBuffer()
            E.names = String[]
            node(E, n.subs[k])
            for j in 1:(k - 1)
                print(E.out, "(?<!")
                uncaptured(E, n.subs[j])
                print(E.out, ')')
            end
            push!(texts, String(take!(E.out)))
            push!(starts, length(every) + 1)
            append!(every, E.names)
        end
        push!(starts, length(every) + 1)
        E.out = out
        E.names = names
        print(out, "(?:")
        padded(E, texts, every, starts, "", "")
        print(out, ')')
    else
        unsupported(E, REFUSED_GROUP_IN_LOOKBEHIND)
    end
    return nothing
end

# Writes a node as one unit with groups that do not capture.
function uncaptured(E::Emitter, n::Node)
    E.uncaptured += 1
    atom(E, n)
    E.uncaptured -= 1
    return nothing
end

# Writes a backreference. One to a group that cannot have taken part matches the empty string and is not written.
# A LIVE one reads its group, which PCRE2 has set on every way here (see the note at the top of the file). A name
# of several groups reads each of them: at most one has taken part, and the others hold the empty string.
function backreference(E::Emitter, n::Node)
    out = E.out
    for k in eachindex(n.groups)
        n.statuses[k] == LIVE || continue
        reading = E.read[n.groups[k]]
        # A group that was not written is in a part of a repetition that only matches the empty string in an
        # iteration ECMA-262 refuses (see `nonempty`), so it never takes part.
        isempty(reading) && continue
        if n.ignorecase[k]
            print(out, "(?i:", reading, ')')
        else
            print(out, reading)
        end
    end
    return nothing
end
