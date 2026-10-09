# The Unicode properties the format checks read, as sorted inclusive ranges. This file is the only one that names
# where they come from: the tables of the pattern engine (src/ecmaregex), which hold one fixed version of Unicode.
# Nothing here reads Julia's own Unicode data, which changes with the Julia version.

# A set of code points: inclusive ranges as pairs, in ascending order, that neither overlap nor touch.
const CodeRanges = Vector{Int32}

# The table of a General_Category value or group, by its short name, such as "Lu" or "L".
unicode_category(name::String) = unicode_property(name)

# The table of a Script value, by its long name, such as "Greek".
unicode_script(name::String) = unicode_property("Script=" * name)

# Reports whether the ranges hold the code point.
function ranges_contain(set::CodeRanges, c::UInt32)
    r = c % Int32
    lo, hi = 0, length(set) >> 1
    while lo < hi
        mid = (lo + hi) >>> 1
        if r > set[2mid+2]
            lo = mid + 1
        elseif r < set[2mid+1]
            hi = mid
        else
            return true
        end
    end
    return false
end

# The code points that any of the sets holds. A caller that tests one code point against several properties builds
# their union once and searches one table.
function union_ranges(sets::CodeRanges...)
    pairs = Tuple{Int32,Int32}[]
    for set in sets
        for i in 1:2:length(set)
            push!(pairs, (set[i], set[i+1]))
        end
    end
    sort!(pairs)
    merged = Int32[]
    for (lo, hi) in pairs
        if !isempty(merged) && lo <= merged[end] + 1
            if hi > merged[end]
                merged[end] = hi
            end
            continue
        end
        push!(merged, lo, hi)
    end
    return merged
end
