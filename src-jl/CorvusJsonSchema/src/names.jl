# Property name lookup for the fail-fast plans. Ported from names.go. Name indexes are zero-based, and -1 is "not
# found".

# The first eight bytes of a name as a little-endian word, zero padded for a shorter name. For a name of at most
# eight bytes it is the name, so two names of one length with the same word are the same name. For a longer name,
# names with different words differ, and names with the same word have their second word compared.
@inline name_word(s::Bytes) = word_at(s.b, s.off, s.len)

# Bytes 8 to 15 of a name longer than eight bytes, zero padded for a name shorter than sixteen.
@inline second_word(s::Bytes) = second_word_at(s.b, s.off, s.len - 8)

# What decides whether a name is a given one: its length, its word, and for a name longer than eight bytes its
# second word. That is the whole name up to sixteen bytes, so only a longer name has its text compared.
struct NameKey
    word::UInt64
    tail::UInt64
    length::Int
end

# The first multiplier tried for a map's hash, and the number tried.
const NAME_HASH_MULTIPLIER = 0x9e3779b97f4a7c15
const NAME_HASH_MULTIPLIERS = 24
# The high bits of the hash that give a slot.
const NAME_HASH_SHIFT = 64 - 18
# More names than this are held in a map (the table has four to eight slots for each name).
const MAX_TABLE_NAMES = 1 << 15

@inline name_hash(mul::UInt64, word::UInt64, tail::UInt64, len::Int) =
    ((word ⊻ bitrotate(tail, 29)) + UInt64(len)) * mul

const NO_NAME = typemax(UInt32)

# Maps declared property names to their index, through a hash table from a name's key to its index, with open
# addressing. The table has at least four slots for each name and its hash is the one of a few that leaves the fewest
# names away from their first slot (for most sets, none), so a search is one multiplication and one read to the
# index, then the comparison of the key.
mutable struct Names
    # Bit n set: some name has length n (lengths of 63 and more share bit 63). A name whose length is not in the set
    # is not declared, which settles most misses without a search.
    const lengths::UInt64
    const names::Vector{String}
    # Each name's bytes followed by eight zero bytes, so that any eight bytes that start inside a name can be read
    # as one word.
    const padded::Vector{Vector{UInt8}}
    const keys::Vector{NameKey}
    # The index + 1 of the name in each slot (0: empty). The length is a power of two.
    const table::Vector{UInt16}
    # The multiplier of the hash. A slot is the high bits of the product, cut to the table's length.
    const mul::UInt64
    # A set too large for the table's indexes (which no schema in practice has) is a map instead.
    const is_large::Bool
    # The longest name the table finds from its key alone: 16, or -1 for a set held in a map.
    const short::Int
    const large::Dict{String,Int}
    # For a hint h (the index after the previous match), the name after that match in sorted order (entry 0: the
    # first name in sorted order). NO_NAME after the last.
    const sorted_next::Vector{UInt32}
end

# The shift is masked so that it compiles to one instruction: Julia's shift by a count that may be negative or 64
# and more is several.
@inline length_bit(len::Int) = UInt64(1) << (min(len, 63) & 63)

# Puts the names in a table under a hash, each in the first free slot from its own. It returns how many are not in
# their own slot.
function fill_table!(table::Vector{UInt16}, names::Vector{String}, keys::Vector{NameKey}, mul::UInt64)
    mask = UInt64(length(table) - 1)
    moved = 0
    for i in eachindex(keys)
        k = keys[i]
        home = name_hash(mul, k.word, k.tail, k.length) >> NAME_HASH_SHIFT
        slot = home
        duplicate = false
        while table[(slot&mask)+1] != 0
            # The first of equal names keeps the slot (a set built from a schema has no equal names).
            if names[table[(slot&mask)+1]] == names[i]
                duplicate = true
                break
            end
            slot += 1
        end
        duplicate && continue
        table[(slot&mask)+1] = i % UInt16
        if slot != home
            moved += 1
        end
    end
    return moved
end

function Names(list::Vector{String})
    padded = Vector{Vector{UInt8}}(undef, length(list))
    keys = Vector{NameKey}(undef, length(list))
    lengths = UInt64(0)
    for (i, n) in enumerate(list)
        len = ncodeunits(n)
        b = zeros(UInt8, len + 8)
        copyto!(b, 1, codeunits(n), 1, len)
        padded[i] = b
        s = Bytes(b, 0, len)
        keys[i] = NameKey(name_word(s), len > 8 ? second_word(s) : UInt64(0), len)
        lengths |= length_bit(len)
    end
    order = sortperm(list; alg=MergeSort)
    sorted_next = fill(NO_NAME, length(list) + 1)
    for (i, at) in enumerate(order)
        if i == 1
            sorted_next[1] = (at - 1) % UInt32
        else
            sorted_next[order[i-1]+1] = (at - 1) % UInt32
        end
    end
    if length(list) > MAX_TABLE_NAMES
        large = Dict{String,Int}()
        for i in length(list):-1:1
            large[list[i]] = i - 1
        end
        return Names(lengths, list, padded, keys, UInt16[], NAME_HASH_MULTIPLIER, true, -1, large, sorted_next)
    end
    size = 4
    while size < 4 * length(list)
        size <<= 1
    end
    # The multiplier that leaves the fewest names away from their first slot.
    best, best_moved = NAME_HASH_MULTIPLIER, -1
    table = zeros(UInt16, size)
    mul = NAME_HASH_MULTIPLIER
    current = mul
    for _ in 1:NAME_HASH_MULTIPLIERS
        fill!(table, 0)
        current = mul
        moved = fill_table!(table, list, keys, mul)
        if best_moved < 0 || moved < best_moved
            best, best_moved = mul, moved
            moved == 0 && break
        end
        # The next odd multiplier (a step of an xorshift generator).
        mul ⊻= mul << 13
        mul ⊻= mul >> 7
        mul ⊻= mul << 17
        mul |= 1
    end
    if current != best
        fill!(table, 0)
        fill_table!(table, list, keys, best)
    end
    return Names(lengths, list, padded, keys, table, best, false, 16, Dict{String,Int}(), sorted_next)
end

Base.length(ns::Names) = length(ns.names)

# Reports whether a name longer than sixteen bytes is the given name of the same length, when their first sixteen
# bytes are known to be equal: the bytes after them are compared a word at a time, the last word zero padded on
# both sides.
function rest_equal(name::Bytes, padded::Vector{UInt8})
    b, off, n = name.b, name.off, name.len
    i = 16
    while i < n
        word_at(b, off + i, n - i) == le64(padded, i) || return false
        i += 8
    end
    return true
end

# Reports whether name i is the given name, which is longer than eight bytes, when their lengths and words are equal.
@inline function name_rest(ns::Names, i::Int, name::Bytes)
    return second_word(name) == ns.keys[i+1].tail && (name.len <= 16 || rest_equal(name, ns.padded[i+1]))
end

# Reports whether name i is the given name, whose word is w.
function name_equal(ns::Names, i::Int, name::Bytes, w::UInt64)
    k = ns.keys[i+1]
    return k.length == name.len && k.word == w && (name.len <= 8 || name_rest(ns, i, name))
end

# find for a name longer than sixteen bytes, whose text is compared as well, and for a set held in a map.
@noinline function find_long(ns::Names, name::Bytes, w::UInt64)
    if ns.is_large
        return get(ns.large, String(name), -1)
    end
    tail = name.len > 8 ? second_word(name) : UInt64(0)
    table, keys = ns.table, ns.keys
    mask = UInt64(length(table) - 1)
    slot = name_hash(ns.mul, w, tail, name.len) >> NAME_HASH_SHIFT
    while true
        i = table[(slot&mask)+1]
        i == 0 && return -1
        k = keys[i]
        if k.word == w && k.tail == tail && k.length == name.len && (name.len <= 16 || rest_equal(name, ns.padded[i]))
            return Int(i) - 1
        end
        slot += 1
    end
end

# The index of a name, without the ordering hint, or -1.
function find(ns::Names, name::Bytes)
    len = name.len
    (ns.lengths & length_bit(len)) == 0 && return -1
    w = name_word(name)
    len > ns.short && return find_long(ns, name, w)
    tail = len > 8 ? second_word(name) : UInt64(0)
    table, keys = ns.table, ns.keys
    mask = UInt64(length(table) - 1)
    slot = name_hash(ns.mul, w, tail, len) >> NAME_HASH_SHIFT
    while true
        i = table[(slot&mask)+1]
        i == 0 && return -1
        k = keys[i]
        if k.word == w && k.tail == tail && k.length == len
            return Int(i) - 1
        end
        slot += 1
    end
end

# find for a name held as a string.
find(ns::Names, name::String) = find(ns, Bytes(Vector{UInt8}(codeunits(name))))

# Reports whether the name at a hint (the index after the previous match) has the given length and word. For a name
# of at most eight bytes that is the name.
@inline function name_at(ns::Names, hint::Int, len::Int, w::UInt64)
    keys = ns.keys
    (hint % UInt) < (length(keys) % UInt) || return false
    k = keys[hint+1]
    return k.length == len && k.word == w
end

# Finds a name, trying the one after the previous match first: instances tend to list their properties in the
# schema's order, so the next name is usually the next one declared. It returns the index or -1. The hint for the
# next call is the index + 1 of a name that was found, and the same hint after one that was not.
#
# This is inlined into the property loops, where it is the test of the expected name, then the test that some name
# has this length, then one call. The call for a name of at most sixteen bytes takes the name's key and no bytes,
# so the loop passes it numbers and keeps nothing alive for it.
@inline function find_next(ns::Names, name::Bytes, hint::Int)
    w = name_word(name)
    len = name.len
    if name_at(ns, hint, len, w) && (len <= 8 || name_rest(ns, hint, name))
        return hint
    end
    (ns.lengths & length_bit(len)) == 0 && return -1
    len > ns.short && return find_after_long(ns, name, w, hint)
    return find_after(ns, len, w, len > 8 ? second_word(name) : UInt64(0), hint)
end

# find_next for a name of at most sixteen bytes that is not the one at the hint, by its key. It tries the name after
# the previous match in sorted order (instances written by tools that sort their keys), then searches the table.
function find_after(ns::Names, len::Int, w::UInt64, tail::UInt64, hint::Int)
    keys, sorted = ns.keys, ns.sorted_next
    if (hint % UInt) < length(sorted) % UInt
        next = sorted[hint+1]
        if next != NO_NAME
            k = keys[next+1]
            if k.word == w && k.tail == tail && k.length == len
                return Int(next)
            end
        end
    end
    table = ns.table
    mask = UInt64(length(table) - 1)
    slot = name_hash(ns.mul, w, tail, len) >> NAME_HASH_SHIFT
    while true
        i = table[(slot&mask)+1]
        i == 0 && return -1
        k = keys[i]
        if k.word == w && k.tail == tail && k.length == len
            return Int(i) - 1
        end
        slot += 1
    end
end

# find_next for a name longer than sixteen bytes, whose text is compared, and for a set held in a map.
@noinline function find_after_long(ns::Names, name::Bytes, w::UInt64, hint::Int)
    sorted = ns.sorted_next
    if (hint % UInt) < length(sorted) % UInt
        next = sorted[hint+1]
        if next != NO_NAME && name_equal(ns, Int(next), name, w)
            return Int(next)
        end
    end
    return find_long(ns, name, w)
end
