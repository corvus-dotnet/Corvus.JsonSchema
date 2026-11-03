# The mapping step of UTS #46 (Unicode IDNA Compatibility Processing, section 4 step 1), which the idn-hostname and
# idn-email formats apply to a name before they check it.
#
# An "ignored" code point is removed and a "mapped" one is replaced by its mapping. Every other code point is kept:
# "valid" and "deviation" ones (nontransitional processing, so ZERO WIDTH JOINER and ZERO WIDTH NON-JOINER stay and meet
# the contextual rules of RFC 5892), and "disallowed" ones, which the IDNA2008 checks that follow refuse. ASCII is kept
# as it is: its mapping only lowercases the letters, which does not change whether a name is valid. The normalization
# step (step 2) is not applied: a name that is not in Normalization Form C is valid as a host name, as the
# JSON-Schema-Test-Suite expects. The table is the one the .NET engine and the Rust crate use.

include("uts46_data.jl")

const UTS46_ENTRY_SIZE = 8

# The index (0-based) of the entry for a code point outside ASCII, or -1 if it is neither ignored nor mapped.
function uts46_find(c::UInt32)
    low, high = 0, length(UTS46_ENTRIES) ÷ UTS46_ENTRY_SIZE
    @inbounds while low < high
        middle = (low + high) >>> 1
        at = middle * UTS46_ENTRY_SIZE
        start = UInt32(UTS46_ENTRIES[at + 1]) << 16 | UInt32(UTS46_ENTRIES[at + 2]) << 8 | UInt32(UTS46_ENTRIES[at + 3])
        stop = start + (UInt32(UTS46_ENTRIES[at + 4]) << 8 | UInt32(UTS46_ENTRIES[at + 5]))
        if c < start
            high = middle
        elseif c > stop
            low = middle + 1
        else
            return at
        end
    end
    return -1
end

# The mapped form of a name; the name itself when nothing in it is mapped or ignored.
function uts46_map(s::Bytes)
    # The first scan allocates nothing, so a name with nothing to map costs no memory.
    changes = false
    i = 0
    while i < s.len
        if at(s, i) < 0x80
            i += 1
        else
            c, size = decode_rune(s, i)
            if uts46_find(c) >= 0
                changes = true
                break
            end
            i += size
        end
    end
    changes || return s
    out = UInt8[]
    sizehint!(out, s.len)
    i = 0
    while i < s.len
        b = at(s, i)
        if b < 0x80
            push!(out, b)
            i += 1
            continue
        end
        c, size = decode_rune(s, i)
        entry = uts46_find(c)
        if entry < 0
            append!(out, view(s.b, s.off + i + 1:s.off + i + size))
        else
            offset = Int(UTS46_ENTRIES[entry + 6]) << 8 | Int(UTS46_ENTRIES[entry + 7])
            length = Int(UTS46_ENTRIES[entry + 8])
            length > 0 && append!(out, view(UTS46_POOL, offset + 1:offset + length))
        end
        i += size
    end
    return Bytes(out)
end
