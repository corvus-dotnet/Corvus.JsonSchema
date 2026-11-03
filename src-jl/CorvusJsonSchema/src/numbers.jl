# Exact numeric comparisons over the parser's number representations (an Int64, a UInt64 beyond an Int64, or a
# Float64). Integers stay exact and floats compare by value, so 1 == 1.0 and 9007199254740993 > 9007199254740992.0.
# multipleOf is decided exactly on decimal forms, so 0.0075 is a multiple of 0.0001. Ported from numbers.go.

const TWO63 = 9223372036854775808.0
const TWO64 = 18446744073709551616.0

@inline compare_int(a::Int64, b::Int64) = a < b ? -1 : (a > b ? 1 : 0)

# Compares an Int64 with a (finite) Float64 exactly.
function compare_int_float(a::Int64, d::Float64)
    d >= TWO63 && return -1
    d < -TWO63 && return 1
    fl = floor(d)
    c = compare_int(a, unsafe_trunc(Int64, fl))
    c != 0 && return c
    return d > fl ? -1 : 0
end

# Compares a UInt64 at or above 2^63 with a (finite) Float64 exactly.
function compare_uint_float(u::UInt64, d::Float64)
    d >= TWO64 && return -1
    d < TWO63 && return 1
    # Floats in [2^63, 2^64) are integers.
    v = unsafe_trunc(UInt64, d - TWO63) | (UInt64(1) << 63)
    return u < v ? -1 : (u > v ? 1 : 0)
end

# Compares the numbers x of a and y of b exactly, as the decimals their texts write: negative, zero or positive. The
# parsed representations decide it when they can: two integers always, and a float with a float or with an integer
# that a Float64 holds exactly when they differ (rounding to a Float64 keeps the order, so different floats come from
# different decimals in the same order). Otherwise, the texts decide it, so 0.123456789123456788 is less than
# 0.123456789123456789 although both read as the same Float64.
function compare_values(a::Document, x::Int, b::Document, y::Int)
    fa, va, fb, vb = flags(a, x), data(a, x), flags(b, y), data(b, y)
    c = compare_numbers(fa, va, fb, vb)
    (fa != NUM_FLOAT && fb != NUM_FLOAT) && return c
    (c != 0 && holds_exactly(fa, va) && holds_exactly(fb, vb)) && return c
    as, bs = count(a, x), count(b, y)
    return compare_number_text(a.source, as, number_end(a.source, as), b.source, bs, number_end(b.source, bs))
end

# Reports whether a Float64 holds the value of a number's representation exactly (always for a float).
@inline function holds_exactly(flag::UInt8, v::UInt64)
    if flag == NUM_INT
        i = v % Int64
        return -(Int64(1) << 53) <= i <= Int64(1) << 53
    elseif flag == NUM_UINT
        # Floats in [2^63, 2^64) are multiples of 2^11.
        return v & ((UInt64(1) << 11) - 1) == 0
    end
    return true
end

# Compares two JSON number texts exactly, as the decimals they write: negative, zero or positive. Each text is the
# bytes from the offset start (0-based) to the offset stop, read as sign * 0.d1d2d3... * 10^exponent, with d1 its
# first non-zero digit.
function compare_number_text(a::Vector{UInt8}, as::Int, ae::Int, b::Vector{UInt8}, bs::Int, be::Int)
    fa = first_nonzero(a, as, ae)
    fb = first_nonzero(b, bs, be)
    sa = fa < 0 ? 0 : (a[as+1] == UInt8('-') ? -1 : 1)
    sb = fb < 0 ? 0 : (b[bs+1] == UInt8('-') ? -1 : 1)
    (sa != sb || sa == 0) && return compare_int(Int64(sa), Int64(sb))
    ea = decimal_exponent(a, as, ae, fa)
    eb = decimal_exponent(b, bs, be, fb)
    ea != eb && return compare_int(ea, eb) * sa
    la = last_nonzero(a, fa, ae)
    lb = last_nonzero(b, fb, be)
    i, k = fa, fb
    while true
        while i <= la && a[i+1] == UInt8('.')
            i += 1
        end
        while k <= lb && b[k+1] == UInt8('.')
            k += 1
        end
        if i > la || k > lb
            return (i > la ? (k > lb ? 0 : -1) : 1) * sa
        end
        a[i+1] != b[k+1] && return (a[i+1] < b[k+1] ? -1 : 1) * sa
        i += 1
        k += 1
    end
end

# The offset of the first non-zero digit of a number text's significand, or -1 if it writes zero.
function first_nonzero(t::Vector{UInt8}, start::Int, stop::Int)
    for j in start:stop-1
        c = t[j+1]
        UInt8('1') <= c <= UInt8('9') && return j
        (c == UInt8('-') || c == UInt8('0') || c == UInt8('.')) || return -1
    end
    return -1
end

# The offset of the last non-zero digit of a number text's significand, at or after its first.
function last_nonzero(t::Vector{UInt8}, first::Int, stop::Int)
    last = first
    for j in first:stop-1
        c = t[j+1]
        if UInt8('1') <= c <= UInt8('9')
            last = j
        elseif c != UInt8('0') && c != UInt8('.')
            break
        end
    end
    return last
end

# The exponent of a number text written as 0.d1d2d3... * 10^exponent: its integer digits, less the digits before d1
# (at the offset first), plus its exponent part.
function decimal_exponent(t::Vector{UInt8}, start::Int, stop::Int, first::Int)
    integer_digits = Int64(0)
    position = Int64(0)
    first_position = Int64(0)
    fraction = false
    j = t[start+1] == UInt8('-') ? start + 1 : start
    while j < stop
        c = t[j+1]
        if c == UInt8('.')
            fraction = true
            j += 1
            continue
        end
        UInt8('0') <= c <= UInt8('9') || break
        fraction || (integer_digits += 1)
        j == first && (first_position = position)
        position += 1
        j += 1
    end
    e, _ = text_exponent(t, j, stop)
    return integer_digits - first_position + e
end

# Compares two numbers' representations: negative, zero or positive. It is exact for two integers; compare_values
# decides the cases a float's rounding hides.
function compare_numbers(fa::UInt8, a::UInt64, fb::UInt8, b::UInt64)
    if fa == NUM_INT
        if fb == NUM_INT
            return compare_int(a % Int64, b % Int64)
        elseif fb == NUM_FLOAT
            return compare_int_float(a % Int64, reinterpret(Float64, b))
        end
        return -1
    elseif fa == NUM_FLOAT
        x = reinterpret(Float64, a)
        if fb == NUM_FLOAT
            y = reinterpret(Float64, b)
            return x < y ? -1 : (x > y ? 1 : 0)
        elseif fb == NUM_INT
            return -compare_int_float(b % Int64, x)
        end
        return -compare_uint_float(b, x)
    end
    # A UInt64 beyond an Int64.
    if fb == NUM_UINT
        return a < b ? -1 : (a > b ? 1 : 0)
    elseif fb == NUM_INT
        return 1
    end
    return compare_uint_float(a, reinterpret(Float64, b))
end

# Reports whether a number is an integer (what the integer type accepts).
@inline function is_integer_number(flag::UInt8, v::UInt64)
    flag != NUM_FLOAT && return true
    d = reinterpret(Float64, v)
    return d == floor(d) && !isinf(d)
end

# A multipleOf divisor, as the decimal its JSON text writes: a significand without trailing zeros and an exponent. x
# is a multiple when x / divisor is an integer, decided exactly on the decimal digits of x's own text, with integer
# arithmetic only (no allocation). These are the C# evaluator's decimal semantics.
mutable struct Divisor
    const is_int::Bool
    const value::Int64
    # The significand (at most 18 digits), or -1 when the divisor's digits do not fit.
    const significand::Int64
    const exponent::Int
    # The exact value, for divisors and instances the integer arithmetic cannot decide.
    const rational::Rational{BigInt}
end

# The exact value of a number's text.
function rational_of(text::Bytes)
    j = 0
    negative = false
    if at(text, 0) == UInt8('-')
        negative = true
        j = 1
    end
    m = BigInt(0)
    exponent = 0
    fraction = false
    while j < text.len
        c = at(text, j)
        if c == UInt8('.')
            fraction = true
        elseif UInt8('0') <= c <= UInt8('9')
            m = m * 10 + (c - UInt8('0'))
            fraction && (exponent -= 1)
        else
            break
        end
        j += 1
    end
    e, _ = text_exponent(text.b, text.off + j, text.off + text.len)
    exponent += e
    negative && (m = -m)
    abs(exponent) > 100000 && return nothing
    return exponent >= 0 ? Rational{BigInt}(m * BigInt(10)^exponent) : m // BigInt(10)^(-exponent)
end

function Divisor(d::Document, n::Int)
    significand, exponent = Int64(-1), 0
    m, e, ok = decimal_of(d.source, count(d, n))
    if ok
        significand, exponent = m, e
    end
    r = rational_of(number_text(d, n))
    return Divisor(flags(d, n) == NUM_INT, data(d, n) % Int64, significand, exponent,
        r === nothing ? Rational{BigInt}(0) : r)
end

# The exact multipleOf of the number value x of a document.
function divides(v::Divisor, d::Document, x::Int)
    if v.is_int && flags(d, x) == NUM_INT
        return v.value != 0 && rem(data(d, x) % Int64, v.value) == 0
    end
    v.significand == 0 && return false
    if v.significand > 0
        r = divides_text(d.source, count(d, x), v.significand % UInt64, v.exponent)
        r >= 0 && return r == 1
    end
    return divides_slow(v, d, x)
end

# A divisor of more than 18 significant digits, or an exponent out of range.
@noinline function divides_slow(v::Divisor, d::Document, x::Int)
    n = rational_of(number_text(d, x))
    (n === nothing || iszero(v.rational)) && return false
    return iszero(n) || isinteger(n // v.rational)
end

# Reads the decimal of the number text at start: the significand without trailing zeros (at most 18 digits) and the
# exponent. Not ok when the significand does not fit or the exponent is out of range.
function decimal_of(b::Vector{UInt8}, start::Int)
    j = start
    n = length(b)
    if b[j+1] == UInt8('-')
        j += 1
    end
    m = Int64(0)
    digits = 0
    exponent = Int64(0)
    pending_zeros = 0
    fraction = false
    while j < n
        c = b[j+1]
        if c == UInt8('.')
            fraction = true
            j += 1
            continue
        end
        (c < UInt8('0') || c > UInt8('9')) && break
        if fraction
            exponent -= 1
        end
        if c == UInt8('0')
            # Trailing zeros are held back, so that they move into the exponent if no other digit follows.
            if digits > 0
                pending_zeros += 1
            end
            j += 1
            continue
        end
        while pending_zeros > 0
            digits >= 18 && return Int64(0), 0, false
            m *= 10
            digits += 1
            pending_zeros -= 1
        end
        digits >= 18 && return Int64(0), 0, false
        m = m * 10 + Int64(c - UInt8('0'))
        digits += 1
        j += 1
    end
    exponent += pending_zeros
    e, _ = text_exponent(b, j, n)
    exponent += e
    if exponent > typemax(Int32) ÷ 2 || exponent < typemin(Int32) ÷ 2
        return Int64(0), 0, false
    end
    return m, Int(exponent), true
end

# Reads the exponent part of a number's text at j, if there is one.
function text_exponent(b::Vector{UInt8}, j::Int, n::Int)
    if j >= n || (b[j+1] != UInt8('e') && b[j+1] != UInt8('E'))
        return Int64(0), j
    end
    j += 1
    negative = false
    if b[j+1] == UInt8('+') || b[j+1] == UInt8('-')
        negative = b[j+1] == UInt8('-')
        j += 1
    end
    e = Int64(0)
    while j < n && UInt8('0') <= b[j+1] <= UInt8('9')
        if e < 1_000_000_000
            e = e * 10 + Int64(b[j+1] - UInt8('0'))
        end
        j += 1
    end
    return negative ? -e : e, j
end

# Reports whether the number text at start is a multiple of dm * 10^de (dm positive, no trailing zeros): 1 or 0, or
# -1 when the exponents are out of range. The text's digits are streamed modulo what remains of the divisor, so the
# text may have any number of digits.
function divides_text(b::Vector{UInt8}, start::Int, dm::UInt64, de::Int)
    # x = xm * 10^xe with xm the digit string (trailing zeros moved into xe). x / d is an integer exactly when dm
    # divides xm * 10^(xe - de).
    n = length(b)
    j = start
    if b[j+1] == UInt8('-')
        j += 1
    end
    digits_start = j
    exponent = Int64(0)
    last_non_zero = -1
    fraction = false
    stop = j
    while stop < n
        c = b[stop+1]
        if c == UInt8('.')
            fraction = true
            stop += 1
            continue
        end
        (c < UInt8('0') || c > UInt8('9')) && break
        if fraction
            exponent -= 1
        end
        if c != UInt8('0')
            last_non_zero = stop
        end
        stop += 1
    end
    # Zero is a multiple of everything.
    last_non_zero < 0 && return 1
    e, _ = text_exponent(b, stop, n)
    exponent += e
    # Digits after the last non-zero one are trailing zeros: they move into the exponent.
    for t in last_non_zero+1:stop-1
        if b[t+1] != UInt8('.')
            exponent += 1
        end
    end
    shift = exponent - Int64(de)
    # dm * 10^-shift must divide xm, which has no trailing zero, so is not a multiple of 10.
    shift < 0 && return 0
    shift > typemax(Int32) && return -1
    # Remove from dm the factors of 2 and 5 that 10^shift supplies. What remains must divide xm.
    rest = dm
    twos = Int64(0)
    while twos < shift && (rest & 1) == 0
        rest >>= 1
        twos += 1
    end
    fives = Int64(0)
    while fives < shift && rest % 5 == 0
        rest = rest ÷ 5
        fives += 1
    end
    rest == 1 && return 1
    r = UInt64(0)
    for t in digits_start:last_non_zero
        c = b[t+1]
        c == UInt8('.') && continue
        # r < rest <= 10^18, so r * 10 + 9 < 2^64.
        r = (r * 10 + UInt64(c - UInt8('0'))) % rest
    end
    return r == 0 ? 1 : 0
end

# ----------------------------------------------------------------------------------------------------------------------
# Decimal to Float64 conversion without allocation: the Eisel-Lemire algorithm (Daniel Lemire, "Number Parsing at a
# Gigabyte per Second", and the fast_float library), which is exact for a decimal significand of up to 19 digits
# (Noble Mushtak and Daniel Lemire, "Fast Number Parsing Without Fallback"). A longer significand is truncated and
# the result accepted when the truncated and the next value round to the same Float64. Otherwise the text is given to
# Base.parse. Ported from the Java port's FastDouble.

const SMALLEST_POWER_OF_TEN = -342
const LARGEST_POWER_OF_TEN = 308

# The 128-bit truncated powers of five from 5^-342 to 5^308, high word then low word, normalised.
const POWERS5 = let t = UInt64[]
    mask = (BigInt(1) << 64) - 1
    for q in SMALLEST_POWER_OF_TEN:LARGEST_POWER_OF_TEN
        c = BigInt(0)
        if q < 0
            power5 = BigInt(5)^(-q)
            z = 0
            while (BigInt(1) << z) < power5
                z += 1
            end
            if q >= -27
                c = (BigInt(1) << (z + 127)) ÷ power5 + 1
            else
                c = (BigInt(1) << (2z + 128)) ÷ power5 + 1
                while c >= (BigInt(1) << 128)
                    c = c ÷ 2
                end
            end
        else
            c = BigInt(5)^q
            while c < (BigInt(1) << 127)
                c *= 2
            end
            while c >= (BigInt(1) << 128)
                c = c ÷ 2
            end
        end
        push!(t, UInt64(c >> 64), UInt64(c & mask))
    end
    t
end

@inline mulhi(a::UInt64, b::UInt64) = (widemul(a, b) >> 64) % UInt64

const FLOAT_INF_BITS = UInt64(0x7ff) << 52

# The bits of the Float64 nearest w * 10^q.
function float_bits(w::UInt64, q::Int)
    (w == 0 || q < SMALLEST_POWER_OF_TEN) && return UInt64(0)
    q > LARGEST_POWER_OF_TEN && return FLOAT_INF_BITS
    lz = leading_zeros(w)
    w <<= lz
    index = 2 * (q - SMALLEST_POWER_OF_TEN)
    high = mulhi(w, POWERS5[index+1])
    low = w * POWERS5[index+1]
    precision_mask = typemax(UInt64) >> 55
    if (high & precision_mask) == precision_mask
        second_high = mulhi(w, POWERS5[index+2])
        low += second_high
        if second_high > low
            high += 1
        end
    end
    upperbit = (high >> 63) % Int
    shift = upperbit + 64 - 52 - 3
    mantissa = high >> shift
    power2 = (((152170 + 65536) * q) >> 16) + 63 + upperbit - lz + 1023
    if power2 <= 0
        # Subnormal.
        -power2 + 1 >= 64 && return UInt64(0)
        mantissa >>= -power2 + 1
        mantissa += mantissa & 1
        mantissa >>= 1
        power2 = mantissa < (UInt64(1) << 52) ? 0 : 1
        return (UInt64(power2) << 52) | (mantissa & ((UInt64(1) << 52) - 1))
    end
    # A product exactly halfway between two values rounds to even.
    if low <= 1 && -4 <= q <= 23 && (mantissa & 3) == 1
        if (mantissa << shift) == high
            mantissa &= ~UInt64(1)
        end
    end
    mantissa += mantissa & 1
    mantissa >>= 1
    if mantissa >= (UInt64(2) << 52)
        mantissa = UInt64(1) << 52
        power2 += 1
    end
    mantissa &= ~(UInt64(1) << 52)
    power2 >= 0x7ff && return FLOAT_INF_BITS
    return (UInt64(power2) << 52) | mantissa
end

# The Float64 of the JSON number text in b[start+1:stop] (already validated). Infinite when out of range.
function decimal_to_float(b::Vector{UInt8}, start::Int, stop::Int)
    j = start
    negative = b[j+1] == UInt8('-')
    if negative
        j += 1
    end
    w = UInt64(0)
    digits = 0
    exponent = 0
    truncated = false
    fraction = false
    while j < stop
        c = b[j+1]
        if c == UInt8('.')
            fraction = true
            j += 1
            continue
        end
        (c < UInt8('0') || c > UInt8('9')) && break
        if digits == 0 && c == UInt8('0')
            # Leading zeros count only as a fraction's places.
            fraction && (exponent -= 1)
        elseif digits < 19
            w = w * 10 + UInt64(c - UInt8('0'))
            digits += 1
            fraction && (exponent -= 1)
        else
            # Beyond 19 digits: dropped, but an integer part's dropped digits still scale the value.
            truncated |= c != UInt8('0')
            fraction || (exponent += 1)
        end
        j += 1
    end
    e, _ = text_exponent(b, j, stop)
    exponent += clamp(e, -1_000_000, 1_000_000)
    q = clamp(exponent, -100_000, 100_000)
    bits = float_bits(w, q)
    if truncated && float_bits(w + 1, q) != bits
        return decimal_to_float_slow(b, start, stop, bits)
    end
    d = reinterpret(Float64, bits)
    return negative ? -d : d
end

# A significand of more than 19 digits whose truncation is ambiguous. The text is a validated JSON number, which
# Base.parse reads the same way, correctly rounded.
# Base.parse gives nothing for a value out of range at either end, and the truncated value says which end.
@noinline function decimal_to_float_slow(b::Vector{UInt8}, start::Int, stop::Int, bits::UInt64)
    d = tryparse(Float64, String(b[start+1:stop]))
    d === nothing || return d
    return bits < (UInt64(1) << 62) ? (b[start+1] == UInt8('-') ? -0.0 : 0.0) : Inf
end
