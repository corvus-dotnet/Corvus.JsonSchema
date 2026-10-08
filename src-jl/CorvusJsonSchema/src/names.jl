# Property name lookup for the fail-fast plans. Ported from names.go. Name indexes are zero-based, and -1 is "not
# found".

# A word that tells names of one length apart cheaply. For a name of at most eight bytes it is unique among names of
# the same length: the first and last four bytes (overlapping, so every byte is in one of them), or for shorter names
# the first, middle and last byte. For a longer name it is the first eight bytes, so names with different words
# differ, and names with the same word are compared in full.
@inline function name_word(s::Bytes)
    n = s.len
    b, off = s.b, s.off
    if n > 8
        return le64(b, off)
    elseif n >= 4
        return le32(b, off) | le32(b, off + n - 4) << 32
    elseif n > 0
        return UInt64(b[off+1]) | UInt64(b[off+(n>>1)+1]) << 8 | UInt64(b[off+n]) << 16
    end
    return UInt64(0)
end

# What decides whether a name is a given one: its length, its word, and for a name longer than eight bytes its last
# eight bytes. That is the whole name up to sixteen bytes, so only a longer name has its text compared.
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

# The last eight bytes of a name longer than eight bytes.
@inline tail_word(s::Bytes) = le64(s.b, s.off + s.len - 8)

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
    const bytes::Vector{Vector{UInt8}}
    const keys::Vector{NameKey}
    # The index + 1 of the name in each slot (0: empty). The length is a power of two.
    const table::Vector{UInt16}
    # The multiplier of the hash. A slot is the high bits of the product, cut to the table's length.
    const mul::UInt64
    # A set too large for the table's indexes (which no schema in practice has) is a map instead.
    const is_large::Bool
    const large::Dict{String,Int}
    # For a hint h (the index after the previous match), the name after that match in sorted order (entry 0: the
    # first name in sorted order). NO_NAME after the last.
    const sorted_next::Vector{UInt32}
end

@inline length_bit(len::Int) = UInt64(1) << min(len, 63)

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
    bytes = [Vector{UInt8}(codeunits(n)) for n in list]
    keys = Vector{NameKey}(undef, length(list))
    lengths = UInt64(0)
    for (i, b) in enumerate(bytes)
        s = Bytes(b)
        keys[i] = NameKey(name_word(s), s.len > 8 ? tail_word(s) : UInt64(0), s.len)
        lengths |= length_bit(s.len)
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
        return Names(lengths, list, bytes, keys, UInt16[], NAME_HASH_MULTIPLIER, true, large, sorted_next)
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
    return Names(lengths, list, bytes, keys, table, best, false, Dict{String,Int}(), sorted_next)
end

Base.length(ns::Names) = length(ns.names)

# Reports whether a name longer than sixteen bytes is the given text of the same length, when their first and last
# eight bytes are known to be equal: only the bytes between them are compared, a word at a time, the last word
# overlapping the one before it.
function middle_equal(name::Bytes, text::Vector{UInt8})
    x, p = name.b, name.off
    stop = name.len - 8
    i = 8
    while i + 8 <= stop
        le64(x, p + i) == le64(text, i) || return false
        i += 8
    end
    return i >= stop || le64(x, p + stop - 8) == le64(text, stop - 8)
end

# Reports whether name i is the given name, which is longer than eight bytes, when their lengths and words are equal.
@inline function name_rest(ns::Names, i::Int, name::Bytes)
    return tail_word(name) == ns.keys[i+1].tail && (name.len <= 16 || middle_equal(name, ns.bytes[i+1]))
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
    tail = name.len > 8 ? tail_word(name) : UInt64(0)
    table, keys = ns.table, ns.keys
    mask = UInt64(length(table) - 1)
    slot = name_hash(ns.mul, w, tail, name.len) >> NAME_HASH_SHIFT
    while true
        i = table[(slot&mask)+1]
        i == 0 && return -1
        k = keys[i]
        if k.word == w && k.tail == tail && k.length == name.len && (name.len <= 16 || middle_equal(name, ns.bytes[i]))
            return Int(i) - 1
        end
        slot += 1
    end
end

# The index of a name, without the ordering hint, or -1.
function find(ns::Names, name::Bytes)
    (ns.lengths & length_bit(name.len)) == 0 && return -1
    w = name_word(name)
    if name.len > 16 || ns.is_large
        return find_long(ns, name, w)
    end
    tail = name.len > 8 ? tail_word(name) : UInt64(0)
    table, keys = ns.table, ns.keys
    mask = UInt64(length(table) - 1)
    slot = name_hash(ns.mul, w, tail, name.len) >> NAME_HASH_SHIFT
    while true
        i = table[(slot&mask)+1]
        i == 0 && return -1
        k = keys[i]
        if k.word == w && k.tail == tail && k.length == name.len
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
# schema's order, so the next name is usually the next one declared. It returns the index (or -1) and the hint for
# the next call.
@inline function find_from(ns::Names, name::Bytes, hint::Int)
    w = name_word(name)
    if name_at(ns, hint, name.len, w) && (name.len <= 8 || name_rest(ns, hint, name))
        return hint, hint + 1
    end
    return find_after(ns, name, w, hint)
end

# find_from for a name (whose word is w) that is not the one at the hint. It tries the name after the previous match
# in sorted order (instances written by tools that sort their keys), then searches the table.
function find_after(ns::Names, name::Bytes, w::UInt64, hint::Int)
    (ns.lengths & length_bit(name.len)) == 0 && return -1, hint
    if name.len > 16 || ns.is_large
        return find_after_long(ns, name, w, hint)
    end
    tail = name.len > 8 ? tail_word(name) : UInt64(0)
    table, keys = ns.table, ns.keys
    if hint < length(ns.sorted_next)
        next = ns.sorted_next[hint+1]
        if next != NO_NAME
            k = keys[next+1]
            if k.word == w && k.tail == tail && k.length == name.len
                return Int(next), Int(next) + 1
            end
        end
    end
    mask = UInt64(length(table) - 1)
    slot = name_hash(ns.mul, w, tail, name.len) >> NAME_HASH_SHIFT
    while true
        i = table[(slot&mask)+1]
        i == 0 && return -1, hint
        k = keys[i]
        if k.word == w && k.tail == tail && k.length == name.len
            return Int(i) - 1, Int(i)
        end
        slot += 1
    end
end

# find_after for a name longer than sixteen bytes, whose text is compared, and for a set held in a map.
@noinline function find_after_long(ns::Names, name::Bytes, w::UInt64, hint::Int)
    if hint < length(ns.sorted_next)
        next = ns.sorted_next[hint+1]
        if next != NO_NAME && name_equal(ns, Int(next), name, w)
            return Int(next), Int(next) + 1
        end
    end
    i = find_long(ns, name, w)
    return i >= 0 ? (i, i + 1) : (-1, hint)
end
