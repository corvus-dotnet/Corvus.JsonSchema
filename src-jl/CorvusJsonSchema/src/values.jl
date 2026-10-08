# JSON equality, hashing and uniqueness over values in documents (an instance against a schema's constant). Ported
# from values.go.

# JSON equality: numbers by value, objects by their property sets, arrays element by element.
function values_equal(a::Document, x::Int, b::Document, y::Int)
    k = kind(a, x)
    k != kind(b, y) && return false
    if k == KIND_NULL
        return true
    elseif k == KIND_BOOL
        return boolean(a, x) == boolean(b, y)
    elseif k == KIND_NUMBER
        return compare_numbers(flags(a, x), data(a, x), flags(b, y), data(b, y)) == 0
    elseif k == KIND_STRING
        return count(a, x) == count(b, y) && bytes_equal(str(a, x), str(b, y))
    elseif k == KIND_ARRAY
        n = count(a, x)
        n != count(b, y) && return false
        p, q = first(a, x), first(b, y)
        for i in 0:n-1
            values_equal(a, p + i, b, q + i) || return false
        end
        return true
    end
    n = count(a, x)
    n != count(b, y) && return false
    # Objects usually list their members in the same order. Compare position by position, and look names up only
    # from the first position where the names differ.
    p, q = first(a, x), first(b, y)
    for i in 0:n-1
        k1, l = p + 2i, q + 2i
        if count(a, k1) != count(b, l) || !bytes_equal(str(a, k1), str(b, l))
            for j in i:n-1
                key = p + 2j
                w = find_property(b, y, str(a, key))
                (w < 0 || !values_equal(a, key + 1, b, w)) && return false
            end
            return true
        end
        values_equal(a, k1 + 1, b, l + 1) || return false
    end
    return true
end

# The value of the property of object named by key, or -1.
function find_property(d::Document, object::Int, key::Bytes)
    k = first(d, object)
    for _ in 1:count(d, object)
        if count(d, k) == key.len && bytes_equal(str(d, k), key)
            return k + 1
        end
        k += 2
    end
    return -1
end

const HASH_K = 0x9e3779b97f4a7c15

# A hash that agrees with JSON equality (object hashing is independent of the member order).
function value_hash(d::Document, v::Int)
    k = kind(d, v)
    if k == KIND_NULL
        return UInt64(0x53)
    elseif k == KIND_BOOL
        return boolean(d, v) ? UInt64(0x52) : UInt64(0x51)
    elseif k == KIND_NUMBER
        flag, dat = flags(d, v), data(d, v)
        if flag == NUM_FLOAT
            f = reinterpret(Float64, dat)
            # Integral floats hash as the integer they equal.
            if f == floor(f) && abs(f) < TWO63
                return (unsafe_trunc(Int64, f) % UInt64) * HASH_K ⊻ 0x1234
            end
            return reinterpret(UInt64, f) * HASH_K ⊻ 0x4321
        end
        if flag == NUM_UINT
            # Floats at or above 2^63 are integers, and never equal a UInt64 exactly unless the UInt64 is a float.
            # Hash both by the float.
            return reinterpret(UInt64, Float64(dat)) * HASH_K ⊻ 0x4321
        end
        return dat * HASH_K ⊻ 0x1234
    elseif k == KIND_STRING
        return str_hash(str(d, v))
    elseif k == KIND_ARRAY
        n = count(d, v)
        h = UInt64(0x54 + n)
        c = first(d, v)
        for i in 0:n-1
            h = h * 31 + value_hash(d, c + i)
        end
        return h
    end
    # Equal objects have the same member values, so a sum of the values' hashes (whatever the order) agrees with
    # equality.
    n = count(d, v)
    h = UInt64(0x55 + n)
    c = first(d, v)
    for i in 0:n-1
        h += value_hash(d, c + 2i + 1) * 0x2c1b3c6d
    end
    return h
end

# Hashes a string, eight bytes at a time.
function str_hash(s::Bytes)
    n = s.len
    b, off = s.b, s.off
    h = UInt64(n) * HASH_K
    i = 0
    while i + 8 <= n
        h = (bitrotate(h, 5) ⊻ le64(b, off + i)) * HASH_K
        i += 8
    end
    # The tail as one word: the last eight bytes when there are that many (overlapping the words already hashed),
    # else the overlapping first and last four, else the bytes themselves.
    tail = UInt64(0)
    if n >= 8
        tail = le64(b, off + n - 8)
    elseif n >= 4
        tail = le32(b, off) | le32(b, off + n - 4) << 32
    else
        for k in 0:n-1
            tail = tail << 8 | UInt64(b[off+k+1])
        end
    end
    return (bitrotate(h, 5) ⊻ tail) * HASH_K
end

# Decides uniqueItems: pairwise for short arrays, otherwise sorted by hash in scratch, each entry the hash's high
# half and the item's index, so that only items with equal hashes are compared. It allocates nothing once scratch
# has grown.
function all_unique(d::Document, array::Int, scratch::Vector{UInt64})
    n = count(d, array)
    n < 2 && return true
    c = first(d, array)
    if n <= 32 && all_strings(d, c, n)
        # Lists of names, the common case: pairwise, comparing lengths first.
        for i in 1:n-1
            for j in 0:i-1
                if count(d, c + i) == count(d, c + j) && bytes_equal(str(d, c + i), str(d, c + j))
                    return false
                end
            end
        end
        return true
    end
    if n <= 16
        for i in 1:n-1
            for j in 0:i-1
                values_equal(d, c + i, d, c + j) && return false
            end
        end
        return true
    end
    s = scratch
    empty!(s)
    for i in 0:n-1
        push!(s, (value_hash(d, c + i) & ~UInt64(0xffffffff)) | UInt64(i))
    end
    sort_words!(s, 1, n)
    start = 0
    for stop in 1:n
        if stop == n || (s[stop+1] >> 32) != (s[start+1] >> 32)
            for i in start+1:stop-1
                for j in start:i-1
                    if values_equal(d, c + Int(s[i+1] % UInt32), d, c + Int(s[j+1] % UInt32))
                        return false
                    end
                end
            end
            start = stop
        end
    end
    return true
end

function all_strings(d::Document, first_child::Int, n::Int)
    for i in 0:n-1
        kind(d, first_child + i) != KIND_STRING && return false
    end
    return true
end
