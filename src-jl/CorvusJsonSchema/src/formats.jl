# Format assertions, applied only when format is asserted (the format-assertion vocabulary or the assert_format
# option). They match the C# evaluator's format checks. Ported from formats.go. The checks read UTF-8 bytes in place.
# Where the Go module uses its linear-time regular expressions (URI, IRI, URI template, e-mail local part), this file
# reads the same grammars directly, so no check here needs the pattern engine but the regex format.

# The format a dialect recognises for a format value.
const FORMAT_UNKNOWN = 0x00
const FORMAT_DATE = 0x01
const FORMAT_TIME = 0x02
const FORMAT_DATE_TIME = 0x03
const FORMAT_DURATION = 0x04
const FORMAT_UUID = 0x05
const FORMAT_IPV4 = 0x06
const FORMAT_IPV6 = 0x07
const FORMAT_HOSTNAME = 0x08
const FORMAT_IDN_HOSTNAME = 0x09
const FORMAT_EMAIL = 0x0a
const FORMAT_IDN_EMAIL = 0x0b
const FORMAT_URI = 0x0c
const FORMAT_URI_REFERENCE = 0x0d
const FORMAT_IRI = 0x0e
const FORMAT_IRI_REFERENCE = 0x0f
const FORMAT_URI_TEMPLATE = 0x10
const FORMAT_JSON_POINTER = 0x11
const FORMAT_RELATIVE_JSON_POINTER = 0x12
const FORMAT_REGEX = 0x13
# Numeric formats (a Corvus extension).
const FORMAT_BYTE = 0x14
const FORMAT_UINT16 = 0x15
const FORMAT_UINT32 = 0x16
const FORMAT_UINT64 = 0x17
const FORMAT_UINT128 = 0x18
const FORMAT_SBYTE = 0x19
const FORMAT_INT16 = 0x1a
const FORMAT_INT32 = 0x1b
const FORMAT_INT64 = 0x1c
const FORMAT_INT128 = 0x1d
const FORMAT_HALF = 0x1e
const FORMAT_SINGLE = 0x1f
const FORMAT_DOUBLE = 0x20
const FORMAT_DECIMAL = 0x21

function format_kind_of(format::String, dialect::Dialect)
    at_least(d::Dialect, k::UInt8) = dialect >= d ? k : FORMAT_UNKNOWN
    (format == "float" || format == "single") && return FORMAT_SINGLE
    format == "byte" && return FORMAT_BYTE
    format == "uint16" && return FORMAT_UINT16
    format == "uint32" && return FORMAT_UINT32
    format == "uint64" && return FORMAT_UINT64
    format == "uint128" && return FORMAT_UINT128
    format == "sbyte" && return FORMAT_SBYTE
    format == "int16" && return FORMAT_INT16
    format == "int32" && return FORMAT_INT32
    format == "int64" && return FORMAT_INT64
    format == "int128" && return FORMAT_INT128
    format == "half" && return FORMAT_HALF
    format == "double" && return FORMAT_DOUBLE
    format == "decimal" && return FORMAT_DECIMAL
    format == "date-time" && return FORMAT_DATE_TIME
    format == "email" && return FORMAT_EMAIL
    format == "hostname" && return FORMAT_HOSTNAME
    format == "ipv4" && return FORMAT_IPV4
    format == "ipv6" && return FORMAT_IPV6
    format == "uri" && return FORMAT_URI
    format == "uri-reference" && return at_least(Draft6, FORMAT_URI_REFERENCE)
    format == "uri-template" && return at_least(Draft6, FORMAT_URI_TEMPLATE)
    format == "json-pointer" && return at_least(Draft6, FORMAT_JSON_POINTER)
    format == "date" && return at_least(Draft7, FORMAT_DATE)
    format == "time" && return at_least(Draft7, FORMAT_TIME)
    format == "regex" && return at_least(Draft7, FORMAT_REGEX)
    format == "relative-json-pointer" && return at_least(Draft7, FORMAT_RELATIVE_JSON_POINTER)
    format == "idn-email" && return at_least(Draft7, FORMAT_IDN_EMAIL)
    format == "idn-hostname" && return at_least(Draft7, FORMAT_IDN_HOSTNAME)
    format == "iri" && return at_least(Draft7, FORMAT_IRI)
    format == "iri-reference" && return at_least(Draft7, FORMAT_IRI_REFERENCE)
    format == "duration" && return at_least(Draft201909, FORMAT_DURATION)
    format == "uuid" && return at_least(Draft201909, FORMAT_UUID)
    return FORMAT_UNKNOWN
end

is_numeric_format(k::UInt8) = k >= FORMAT_BYTE

const FORMAT_NAMES = ("unknown", "date", "time", "date-time", "duration", "uuid", "ipv4", "ipv6", "hostname",
    "idn-hostname", "email", "idn-email", "uri", "uri-reference", "iri", "iri-reference", "uri-template",
    "json-pointer", "relative-json-pointer", "regex", "byte", "uint16", "uint32", "uint64", "uint128", "sbyte",
    "int16", "int32", "int64", "int128", "half", "single", "double", "decimal")

# The canonical name, for messages.
format_name(k::UInt8) = FORMAT_NAMES[k+1]

# The message for a string format failure (the C# evaluator's text), or "".
function format_message(k::UInt8)
    k == FORMAT_DATE && return "Expected an ISO8601 Date string."
    k == FORMAT_DATE_TIME && return "Expected an ISO8601 Offset DateTime string."
    k == FORMAT_TIME && return "Expected an ISO8601 Offset Time string."
    k == FORMAT_DURATION && return "Expected an ISO8601 Duration string."
    k == FORMAT_EMAIL && return "Expected an RFC5321 Section-4.1.2 Email string."
    k == FORMAT_IDN_EMAIL && return "Expected an RFC6531 IDN Email string."
    k == FORMAT_HOSTNAME && return "Expected an RFC1035 hostname."
    k == FORMAT_IDN_HOSTNAME && return "Expected an RFC5890 Section-2.3.2.3 IDN hostname."
    k == FORMAT_IPV4 && return "Expected an RFC2673 IP V4 address."
    k == FORMAT_IPV6 && return "Expected an RFC2373 IP V6 address."
    k == FORMAT_URI && return "Expected an absolute URI."
    k == FORMAT_URI_REFERENCE && return "Expected a URI reference."
    k == FORMAT_IRI && return "Expected an absolute IRI."
    k == FORMAT_IRI_REFERENCE && return "Expected an IRI reference."
    k == FORMAT_UUID && return "Expected an RFC4122 UUID."
    k == FORMAT_URI_TEMPLATE && return "Expected an RFC6570 URI Template."
    k == FORMAT_JSON_POINTER && return "Expected an RFC6901 JSON Pointer."
    k == FORMAT_RELATIVE_JSON_POINTER &&
        return "Expected a Relative JSON Pointer. (https://json-schema.org/draft/2020-12/relative-json-pointer)."
    k == FORMAT_REGEX && return "Expected a regular expression specification."
    return ""
end

# Asserts a string format. legacy_hostname selects the RFC 1123 host name rules of draft 4 and 6.
function check_string_format(k::UInt8, s::Bytes, legacy_hostname::Bool)::Bool
    k == FORMAT_DATE && return is_date(s)
    k == FORMAT_TIME && return is_time(s)
    k == FORMAT_DATE_TIME && return is_date_time(s)
    k == FORMAT_DURATION && return is_duration(s)
    k == FORMAT_UUID && return is_uuid(s)
    k == FORMAT_IPV4 && return is_ipv4(s)
    k == FORMAT_IPV6 && return is_ipv6(s)
    k == FORMAT_HOSTNAME && return legacy_hostname ? is_legacy_hostname(s) : is_hostname(s)
    k == FORMAT_IDN_HOSTNAME && return is_idn_hostname(s)
    k == FORMAT_EMAIL && return is_email(s, false)
    k == FORMAT_IDN_EMAIL && return is_email(s, true)
    k == FORMAT_URI && return is_uri(s, false, false)
    k == FORMAT_URI_REFERENCE && return is_uri(s, false, true)
    k == FORMAT_IRI && return is_uri(s, true, false)
    k == FORMAT_IRI_REFERENCE && return is_uri(s, true, true)
    k == FORMAT_URI_TEMPLATE && return is_uri_template(s)
    k == FORMAT_JSON_POINTER && return is_json_pointer(s)
    k == FORMAT_RELATIVE_JSON_POINTER && return is_relative_json_pointer(s)
    k == FORMAT_REGEX && return valid_regex(s)
    return true
end

# Asserts a numeric format.
function check_number_format(k::UInt8, d::Document, n::Int)::Bool
    flag, dat = flags(d, n), data(d, n)
    f = float(d, n)
    integral(lo::Float64, hi::Float64) = !isinf(f) && f == floor(f) && f >= lo && f <= hi
    function int_range(lo::Int64, hi::UInt64)
        if flag == NUM_INT
            i = dat % Int64
            return i >= lo && (i < 0 || (i % UInt64) <= hi)
        elseif flag == NUM_UINT
            return dat <= hi
        end
        return integral(Float64(lo), Float64(hi))
    end
    magnitude(limit::Float64) = !isinf(f) && abs(f) <= limit
    k == FORMAT_BYTE && return int_range(Int64(0), UInt64(255))
    k == FORMAT_UINT16 && return int_range(Int64(0), UInt64(65535))
    k == FORMAT_UINT32 && return int_range(Int64(0), UInt64(4294967295))
    k == FORMAT_UINT64 && return int_range(Int64(0), typemax(UInt64))
    if k == FORMAT_UINT128
        flag == NUM_INT && return (dat % Int64) >= 0
        flag == NUM_UINT && return true
        return integral(0.0, 3.402823669209385e38)
    end
    k == FORMAT_SBYTE && return int_range(Int64(-128), UInt64(127))
    k == FORMAT_INT16 && return int_range(Int64(-32768), UInt64(32767))
    k == FORMAT_INT32 && return int_range(Int64(-2147483648), UInt64(2147483647))
    k == FORMAT_INT64 && return int_range(typemin(Int64), UInt64(typemax(Int64)))
    k == FORMAT_INT128 && return flag != NUM_FLOAT || integral(-1.7014118346046923e38, 1.7014118346046923e38)
    k == FORMAT_HALF && return magnitude(65504.0)
    k == FORMAT_SINGLE && return magnitude(3.4028234663852886e38)
    k == FORMAT_DOUBLE && return magnitude(floatmax(Float64))
    k == FORMAT_DECIMAL && return magnitude(7.922816251426434e28)
    return true
end

is_leap_year(y::Int) = y % 4 == 0 && (y % 100 != 0 || y % 400 == 0)

# The value of the run of ASCII digits s[from:to) (zero-based), or -1.
function digits_value(s::Bytes, from::Int, to::Int)
    from >= to && return -1
    v = 0
    for i in from:to-1
        c = at(s, i)
        is_ascii_digit(c) || return -1
        v = v * 10 + Int(c - UInt8('0'))
    end
    return v
end

digits_value(s::Bytes) = digits_value(s, 0, s.len)

const MONTH_DAYS = (0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31)

function is_date(s::Bytes)
    (s.len != 10 || at(s, 4) != UInt8('-') || at(s, 7) != UInt8('-')) && return false
    y, m, d = digits_value(s, 0, 4), digits_value(s, 5, 7), digits_value(s, 8, 10)
    (y < 0 || m < 1 || m > 12 || d < 1) && return false
    m == 2 && is_leap_year(y) && return d <= 29
    return d <= MONTH_DAYS[m+1]
end

function is_time(s::Bytes)
    n = s.len
    (n < 9 || at(s, 2) != UInt8(':') || at(s, 5) != UInt8(':')) && return false
    h, mi, sec = digits_value(s, 0, 2), digits_value(s, 3, 5), digits_value(s, 6, 8)
    (h < 0 || mi < 0 || sec < 0) && return false
    i = 8
    if at(s, i) == UInt8('.')
        i += 1
        start = i
        while i < n && is_ascii_digit(at(s, i))
            i += 1
        end
        i == start && return false
    end
    i >= n && return false
    oh, om, sign = 0, 0, 0
    c = at(s, i)
    if c == UInt8('z') || c == UInt8('Z')
        i + 1 != n && return false
    elseif c == UInt8('+') || c == UInt8('-')
        (n - i != 6 || at(s, i + 3) != UInt8(':')) && return false
        sign = c == UInt8('-') ? 1 : -1
        oh, om = digits_value(s, i + 1, i + 3), digits_value(s, i + 4, i + 6)
        (oh < 0 || om < 0 || oh > 23 || om > 59) && return false
    else
        return false
    end
    (h > 23 || mi > 59 || sec > 60) && return false
    if sec == 60
        # A leap second is only valid at 23:59:60 UTC.
        utc = mod(h * 60 + mi + sign * (oh * 60 + om), 1440)
        return utc == 23 * 60 + 59
    end
    return true
end

function is_date_time(s::Bytes)
    return s.len > 11 && (at(s, 10) == UInt8('T') || at(s, 10) == UInt8('t')) && is_date(sub(s, 0, 10)) &&
           is_time(sub(s, 11))
end

# Reads digits and one of the designators from position `from` of the designators, in order, at i. It returns the
# designator's index (or -1) and the next offset.
function duration_part(s::Bytes, i::Int, designators::NTuple{N,UInt8}, from::Int) where {N}
    start = i
    while i < s.len && is_ascii_digit(at(s, i))
        i += 1
    end
    (i == start || i >= s.len) && return -1, i
    c = at(s, i)
    for k in from:N-1
        designators[k+1] == c && return k, i + 1
    end
    return -1, i
end

# Reads one or more consecutive parts in designator order. It returns whether it did and the next offset.
function duration_run(s::Bytes, i::Int, designators::NTuple{N,UInt8}) where {N}
    found, i = duration_part(s, i, designators, 0)
    found < 0 && return false, i
    while found + 1 < N && i < s.len && is_ascii_digit(at(s, i))
        next, i = duration_part(s, i, designators, found + 1)
        next != found + 1 && return false, i
        found = next
    end
    return true, i
end

const DURATION_TIME = (UInt8('H'), UInt8('M'), UInt8('S'))
const DURATION_DATE = (UInt8('Y'), UInt8('M'), UInt8('D'))
const DURATION_WEEK = (UInt8('W'),)

# Reads an ISO 8601 duration: P then weeks, or date parts in order then an optional time, or a time.
function is_duration(s::Bytes)
    (s.len < 2 || at(s, 0) != UInt8('P')) && return false
    i = 1
    if at(s, i) == UInt8('T')
        ok, i = duration_run(s, i + 1, DURATION_TIME)
        return ok && i == s.len
    end
    week, after = duration_part(s, i, DURATION_WEEK, 0)
    week == 0 && return after == s.len
    ok, i = duration_run(s, i, DURATION_DATE)
    ok || return false
    if i < s.len && at(s, i) == UInt8('T')
        ok, i = duration_run(s, i + 1, DURATION_TIME)
        return ok && i == s.len
    end
    return i == s.len
end

is_hex_digit(c::UInt8) = hex_value(c) >= 0

function is_uuid(s::Bytes)
    s.len != 36 && return false
    for i in 0:35
        c = at(s, i)
        if i == 8 || i == 13 || i == 18 || i == 23
            c != UInt8('-') && return false
        elseif !is_hex_digit(c)
            return false
        end
    end
    return true
end

# The zero-based index of a byte in s from an offset, or -1.
function index_of(s::Bytes, c::UInt8, from::Int=0)
    for i in from:s.len-1
        at(s, i) == c && return i
    end
    return -1
end

function last_index_of(s::Bytes, c::UInt8)
    for i in s.len-1:-1:0
        at(s, i) == c && return i
    end
    return -1
end

function is_ipv4(s::Bytes)
    parts = 0
    start = 0
    while true
        dot = index_of(s, UInt8('.'), start)
        stop = dot >= 0 ? dot : s.len
        len = stop - start
        parts += 1
        (parts > 4 || len == 0 || len > 3 || (len > 1 && at(s, start) == UInt8('0'))) && return false
        v = digits_value(s, start, stop)
        (v < 0 || v > 255) && return false
        dot < 0 && return parts == 4
        start = dot + 1
    end
end

# The number of colon-separated groups of one to four hex digits, or -1 when one is not valid.
function hex_groups(s::Bytes)
    s.len == 0 && return 0
    n = 0
    start = 0
    while true
        colon = index_of(s, UInt8(':'), start)
        stop = colon >= 0 ? colon : s.len
        len = stop - start
        (len == 0 || len > 4) && return -1
        for i in start:stop-1
            is_hex_digit(at(s, i)) || return -1
        end
        n += 1
        colon < 0 && return n
        start = colon + 1
    end
end

# The zero-based index of "::" in s from an offset, or -1.
function index_double_colon(s::Bytes, from::Int=0)
    for i in from:s.len-2
        at(s, i) == UInt8(':') && at(s, i + 1) == UInt8(':') && return i
    end
    return -1
end

function is_ipv6(s::Bytes)
    # A valid address is at most 51 characters (an IPv4 tail after six groups), so longer text fails at once.
    (s.len < 2 || s.len > 64) && return false
    has_dot = false
    for i in 0:s.len-1
        c = at(s, i)
        (is_hex_digit(c) || c == UInt8(':') || c == UInt8('.')) || return false
        has_dot = has_dot || c == UInt8('.')
    end
    if !has_dot
        dbl = index_double_colon(s)
        if dbl >= 0
            index_double_colon(s, dbl + 1) >= 0 && return false
            l, r = hex_groups(sub(s, 0, dbl)), hex_groups(sub(s, dbl + 2))
            return l >= 0 && r >= 0 && l + r < 8
        end
        return hex_groups(s) == 8
    end
    # An IPv4 tail counts as two groups. It is checked, then the address is read as the text before it (which ends
    # in a colon) followed by two groups.
    last_colon = last_index_of(s, UInt8(':'))
    (last_colon < 0 || !is_ipv4(sub(s, last_colon + 1))) && return false
    head = sub(s, 0, last_colon + 1)
    dbl = index_double_colon(head)
    if dbl >= 0
        index_double_colon(head, dbl + 1) >= 0 && return false
        l = hex_groups(sub(head, 0, dbl))
        right = sub(head, dbl + 2)
        r = right.len == 0 ? 0 : hex_groups(sub(right, 0, right.len - 1))
        return l >= 0 && r >= 0 && l + r + 2 < 8
    end
    return hex_groups(sub(head, 0, head.len - 1)) + 2 == 8
end

is_ascii_alphanumeric(c::UInt8) = is_ascii_letter(c) || is_ascii_digit(c)

function is_ldh_label(l::Bytes)
    n = l.len
    (n == 0 || n > 63 || !is_ascii_alphanumeric(at(l, 0)) || !is_ascii_alphanumeric(at(l, n - 1))) && return false
    for i in 0:n-1
        c = at(l, i)
        (is_ascii_alphanumeric(c) || c == UInt8('-')) || return false
    end
    return true
end

function starts_with_xn(l::Bytes)
    return l.len >= 4 && (at(l, 0) | 0x20) == UInt8('x') && (at(l, 1) | 0x20) == UInt8('n') &&
           at(l, 2) == UInt8('-') && at(l, 3) == UInt8('-')
end

function hostname_label_ok(label::Bytes)
    is_ldh_label(label) || return false
    # "--" in the third and fourth positions is reserved for A-labels (xn--).
    return !(label.len >= 4 && at(label, 2) == UInt8('-') && at(label, 3) == UInt8('-') && !starts_with_xn(label))
end

# Checks RFC 1123 host names (draft 4 and 6): no IDNA rules for "--" or A-labels.
function is_legacy_hostname(s::Bytes)
    (s.len == 0 || s.len > 253) && return false
    start = 0
    while true
        dot = index_of(s, UInt8('.'), start)
        dot < 0 && return is_ldh_label(sub(s, start))
        is_ldh_label(sub(s, start, dot)) || return false
        start = dot + 1
    end
end

function is_hostname(s::Bytes)
    (s.len == 0 || s.len > 253) && return false
    start = 0
    while true
        dot = index_of(s, UInt8('.'), start)
        dot < 0 && return hostname_label(sub(s, start))
        hostname_label(sub(s, start, dot)) || return false
        start = dot + 1
    end
end

hostname_label(l::Bytes) = hostname_label_ok(l) && (!starts_with_xn(l) || punycode_label_ok(sub(l, 4)))

# The Unicode properties the host name checks read. They are built from the package's own Unicode tables (see
# unicode.jl), which hold one fixed version of Unicode, and never from Julia's Unicode data, which follows the Julia
# version. A format therefore accepts the same strings whichever Julia runs the program.
#
# RFC 5892 defines the IDNA2008 code point classes by rules over Unicode properties and not by a list for one version
# of Unicode, so that they extend to each new version. The checks here apply those rules to the properties of the
# tables' version.
struct IdnTables
    # The general categories that IDNA2008 never makes PVALID: controls, private use, unassigned, spaces, uppercase
    # and titlecase letters (mapped away), mathematical and other symbols, and punctuation.
    disallowed::CodeRanges
    # The controls, the format characters, the spaces and the unassigned code points.
    invisible::CodeRanges
    # The group M, Mn, and Mn with Me, the stand-in for Bidi class NSM.
    mark::CodeRanges
    nonspacing::CodeRanges
    nonspacing_or_enclosing::CodeRanges
    # The group L and Mc, the stand-in for Bidi class L.
    letter_or_spacing_mark::CodeRanges
    # The scripts the contextual rules of RFC 5892 and the Bidi rule of RFC 5893 name.
    hebrew::CodeRanges
    greek::CodeRanges
    arabic_like::CodeRanges
    kana_or_han::CodeRanges
    # The groups L, M and N together, for the local part of an internationalized e-mail address.
    letter_mark_number::CodeRanges
end

const IDN_TABLES = Ref{Union{Nothing,IdnTables}}(nothing)
const IDN_TABLES_LOCK = ReentrantLock()

# Builds the tables when a check first needs them. A union of properties is one table, so a code point is tested
# against it with one search.
function idn()
    t = IDN_TABLES[]
    t === nothing || return t
    return lock(IDN_TABLES_LOCK) do
        existing = IDN_TABLES[]
        existing === nothing || return existing
        gc, sc = unicode_category, unicode_script
        built = IdnTables(
            union_ranges(gc("Cc"), gc("Co"), gc("Zs"), gc("Zl"), gc("Zp"), gc("Lu"), gc("Lt"), gc("Sm"), gc("So"),
                gc("P"), gc("Cn")),
            union_ranges(gc("Cc"), gc("Cf"), gc("Zs"), gc("Cn")),
            gc("M"), gc("Mn"), union_ranges(gc("Mn"), gc("Me")), union_ranges(gc("L"), gc("Mc")),
            sc("Hebrew"), sc("Greek"), union_ranges(sc("Arabic"), sc("Syriac"), sc("Thaana"), sc("Nko")),
            union_ranges(sc("Hiragana"), sc("Katakana"), sc("Han")), union_ranges(gc("L"), gc("M"), gc("N")))
        IDN_TABLES[] = built
        return built
    end::IdnTables
end

is_disallowed_exception(r::UInt32) =
    r == 0x06fd || r == 0x06fe || r == 0x0f0b || r == 0x00b7 || r == 0x05f3 || r == 0x05f4 || r == 0x30fb

# Reports code points IDNA2008 disallows that the tests exercise: controls, private use, unassigned, spaces,
# uppercase and titlecase letters (mapped away, never PVALID), symbols and punctuation.
function disallowed(label::Vector{UInt32})
    table = idn().disallowed
    for r in label
        (r == UInt32('-') || is_disallowed_exception(r)) && continue
        ranges_contain(table, r) && return true
    end
    return false
end

# The code points of UTF-8 text.
function code_points(s::Bytes)
    out = UInt32[]
    i = 0
    while i < s.len
        c, size = decode_rune(s, i)
        push!(out, c)
        i += size
    end
    return out
end

function punycode_label_ok(encoded::Bytes)
    decoded = punycode_decode(encoded)
    (decoded === nothing || isempty(decoded)) && return false
    all(<(0x80), decoded) && return false
    # The encoding must be canonical: encoding the U-label again gives the same A-label.
    again = punycode_encode(decoded)
    length(again) == encoded.len || return false
    for i in 0:encoded.len-1
        c = at(encoded, i)
        if UInt8('A') <= c <= UInt8('Z')
            c += 0x20
        end
        again[i+1] == c || return false
    end
    return idn_label_ok(decoded) && !disallowed(decoded) && bidi_label_ok(decoded, bidi_domain(decoded))
end

const PUNY_BASE = UInt32(36)
const PUNY_TMIN = UInt32(1)
const PUNY_TMAX = UInt32(26)
const PUNY_SKEW = UInt32(38)
const PUNY_DAMP = UInt32(700)

function puny_adapt(delta::UInt32, num_points::UInt32, first_time::Bool)
    delta = first_time ? delta ÷ PUNY_DAMP : delta >> 1
    delta += delta ÷ num_points
    k = UInt32(0)
    while delta > ((PUNY_BASE - PUNY_TMIN) * PUNY_TMAX) >> 1
        delta = delta ÷ (PUNY_BASE - PUNY_TMIN)
        k += PUNY_BASE
    end
    return k + ((PUNY_BASE - PUNY_TMIN + UInt32(1)) * delta) ÷ (delta + PUNY_SKEW)
end

function puny_threshold(k::UInt32, bias::UInt32)
    k <= bias && return PUNY_TMIN
    k >= bias + PUNY_TMAX && return PUNY_TMAX
    return k - bias
end

saturating_add(a::UInt32, b::UInt32) = a > typemax(UInt32) - b ? typemax(UInt32) : a + b

puny_digit(d::UInt32) = d < 26 ? (d + UInt32(97)) % UInt8 : (d + UInt32(22)) % UInt8

# The RFC 3492 encoding, for the canonical round trip and A-label length checks.
function punycode_encode(cps::Vector{UInt32})
    out = UInt8[]
    for c in cps
        c < 0x80 && push!(out, c % UInt8)
    end
    basic_length = UInt32(length(out))
    h = basic_length
    basic_length > 0 && push!(out, UInt8('-'))
    n, delta, bias = UInt32(128), UInt32(0), UInt32(72)
    while Int(h) < length(cps)
        m = typemax(UInt32)
        for c in cps
            if c >= n && c < m
                m = c
            end
        end
        step = min(UInt64(m - n) * UInt64(h + 1), UInt64(typemax(UInt32)))
        delta = saturating_add(delta, step % UInt32)
        n = m
        for c in cps
            if c < n
                delta = saturating_add(delta, UInt32(1))
            end
            if c == n
                q = delta
                k = PUNY_BASE
                while true
                    t = puny_threshold(k, bias)
                    q < t && break
                    push!(out, puny_digit(t + (q - t) % (PUNY_BASE - t)))
                    q = (q - t) ÷ (PUNY_BASE - t)
                    k += PUNY_BASE
                end
                push!(out, puny_digit(q))
                bias = puny_adapt(delta, h + UInt32(1), h == basic_length)
                delta = UInt32(0)
                h += UInt32(1)
            end
        end
        delta += UInt32(1)
        n += UInt32(1)
    end
    return out
end

# The RFC 3492 decoding, enough to validate A-labels. Nothing when the text is not Punycode.
function punycode_decode(input::Bytes)
    n, i, bias = UInt32(128), UInt32(0), UInt32(72)
    output = UInt32[]
    basic = max(last_index_of(input, UInt8('-')), 0)
    for j in 0:basic-1
        c = at(input, j)
        c >= 0x80 && return nothing
        push!(output, UInt32(c))
    end
    index = basic > 0 ? basic + 1 : 0
    while index < input.len
        oldi = i
        w = UInt64(1)
        k = PUNY_BASE
        while true
            index >= input.len && return nothing
            c = at(input, index)
            index += 1
            digit = if UInt8('0') <= c <= UInt8('9')
                UInt32(c) - UInt32(22)
            elseif UInt8('A') <= c <= UInt8('Z')
                UInt32(c) - UInt32(65)
            elseif UInt8('a') <= c <= UInt8('z')
                UInt32(c) - UInt32(97)
            else
                return nothing
            end
            next = UInt64(i) + UInt64(digit) * w
            next > typemax(UInt32) && return nothing
            i = next % UInt32
            t = puny_threshold(k, bias)
            digit < t && break
            w *= UInt64(PUNY_BASE - t)
            w > typemax(UInt32) && return nothing
            k += PUNY_BASE
        end
        len = UInt32(length(output)) + UInt32(1)
        bias = puny_adapt(i - oldi, len, oldi == 0)
        UInt64(n) + UInt64(i ÷ len) > typemax(UInt32) && return nothing
        n += i ÷ len
        i %= len
        (n > 0x10ffff || (0xd800 <= n <= 0xdfff)) && return nothing
        insert!(output, Int(i) + 1, n)
        i += UInt32(1)
    end
    return output
end

is_arabic_like(r::UInt32) = ranges_contain(idn().arabic_like, r)

const BIDI_L = 0
const BIDI_R = 1
const BIDI_AL = 2
const BIDI_AN = 3
const BIDI_EN = 4
const BIDI_NSM = 5
const BIDI_ON = 6

# Approximates the Bidi classes of the RFC 5893 Bidi rule by script and general category.
function bidi_class(c::UInt32)
    t = idn()
    ranges_contain(t.nonspacing_or_enclosing, c) && return BIDI_NSM
    ((0x660 <= c <= 0x669) || c == 0x66b || c == 0x66c) && return BIDI_AN
    ((0x30 <= c <= 0x39) || (0x6f0 <= c <= 0x6f9)) && return BIDI_EN
    ranges_contain(t.hebrew, c) && return BIDI_R
    is_arabic_like(c) && return BIDI_AL
    ranges_contain(t.letter_or_spacing_mark, c) && return BIDI_L
    return BIDI_ON
end

function bidi_domain(label::Vector{UInt32})
    rtl, strong = false, false
    hebrew = idn().hebrew
    for c in label
        rtl = rtl || ranges_contain(hebrew, c) || is_arabic_like(c) || (0x660 <= c <= 0x669) || c == 0x66b ||
              c == 0x66c
        class = bidi_class(c)
        strong = strong || class == BIDI_R || class == BIDI_AL || class == BIDI_AN
    end
    return rtl && strong
end

function bidi_label_ok(label::Vector{UInt32}, is_bidi_domain::Bool)
    is_bidi_domain || return true
    classes = [bidi_class(c) for c in label]
    isempty(classes) && return false
    has(class::Int) = class in classes
    last = length(classes)
    while last > 1 && classes[last] == BIDI_NSM
        last -= 1
    end
    stop = classes[last]
    first_class = classes[1]
    if first_class == BIDI_R || first_class == BIDI_AL
        return !has(BIDI_L) && (stop == BIDI_R || stop == BIDI_AL || stop == BIDI_EN || stop == BIDI_AN) &&
               !(has(BIDI_EN) && has(BIDI_AN))
    elseif first_class == BIDI_L
        return !has(BIDI_R) && !has(BIDI_AL) && !has(BIDI_AN) && (stop == BIDI_L || stop == BIDI_EN)
    end
    return false
end

const VIRAMAS = (0x094d, 0x09cd, 0x0a4d, 0x0acd, 0x0b4d, 0x0bcd, 0x0c4d, 0x0ccd, 0x0d3b, 0x0d3c, 0x0d4d, 0x0dca,
    0x0e3a, 0x0eba, 0x0f84, 0x1039, 0x103a, 0x1714, 0x1734, 0x17d2, 0x1a60, 0x1b44, 0x1baa, 0x1bab, 0x1bf2, 0x1bf3,
    0x2d7f, 0xa806, 0xa8c4, 0xa953, 0xa9c0, 0xaaf6, 0xabed)

is_virama(r::UInt32) = r in VIRAMAS

# Approximates (Joining_Type:{L,D})(Joining_Type:T)*ZWNJ(Joining_Type:T)*(Joining_Type:{R,D}) with Arabic letters.
# i is one-based.
function zwnj_joining_context(cps::Vector{UInt32}, i::Int)
    is_joiner(c::UInt32) = (0x0620 <= c <= 0x064a) || (0x066e <= c <= 0x06d3)
    nonspacing = idn().nonspacing
    l = i - 1
    while l >= 1 && ranges_contain(nonspacing, cps[l])
        l -= 1
    end
    r = i + 1
    while r <= length(cps) && ranges_contain(nonspacing, cps[r])
        r += 1
    end
    return l >= 1 && r <= length(cps) && is_joiner(cps[l]) && is_joiner(cps[r])
end

# Checks the contextual and disallowed code points from RFC 5892 that the test suite exercises.
function idn_label_ok(cps::Vector{UInt32})
    n = length(cps)
    (n == 0 || cps[1] == UInt32('-') || cps[n] == UInt32('-')) && return false
    n >= 4 && cps[3] == UInt32('-') && cps[4] == UInt32('-') && return false
    t = idn()
    ranges_contain(t.mark, cps[1]) && return false
    has(lo::UInt32, hi::UInt32) = any(c -> lo <= c <= hi, cps)
    has(UInt32(0x660), UInt32(0x669)) && has(UInt32(0x6f0), UInt32(0x6f9)) && return false
    for i in 1:n
        c = cps[i]
        if c == 0x302e || c == 0x302f || c == 0x0640 || c == 0x07fa || (0x3031 <= c <= 0x3035) || c == 0x303b
            return false
        elseif c == 0x00b7
            (i > 1 && i < n && cps[i-1] == UInt32('l') && cps[i+1] == UInt32('l')) || return false
        elseif c == 0x0375
            (i < n && ranges_contain(t.greek, cps[i+1])) || return false
        elseif c == 0x05f3 || c == 0x05f4
            (i > 1 && ranges_contain(t.hebrew, cps[i-1])) || return false
        elseif c == 0x30fb
            any(d -> d != 0x30fb && ranges_contain(t.kana_or_han, d), cps) || return false
        elseif c == 0x200d
            (i == 1 || !is_virama(cps[i-1])) && return false
        elseif c == 0x200c
            (i == 1 || !is_virama(cps[i-1])) && !zwnj_joining_context(cps, i) && return false
        end
    end
    return true
end

# Full stop, ideographic full stop, fullwidth full stop, halfwidth ideographic full stop.
is_label_separator(r::UInt32) = r == UInt32('.') || r == 0x3002 || r == 0xff0e || r == 0xff61

function is_idn_hostname(s::Bytes)
    s.len == 0 && return false
    # The labels, split at every label separator, keeping empty labels.
    labels = Vector{UInt32}[UInt32[]]
    for c in code_points(s)
        if is_label_separator(c)
            push!(labels, UInt32[])
        else
            push!(labels[end], c)
        end
    end
    unicode_labels = Vector{Vector{UInt32}}(undef, length(labels))
    is_bidi = false
    for (i, l) in enumerate(labels)
        unicode_labels[i] = l
        if length(l) >= 4 && all(<(0x80), l)
            ascii = Bytes(UInt8[c % UInt8 for c in l])
            if starts_with_xn(ascii)
                decoded = punycode_decode(sub(ascii, 4))
                decoded === nothing || (unicode_labels[i] = decoded)
            end
        end
        is_bidi = is_bidi || bidi_domain(unicode_labels[i])
    end
    ascii_length = 0
    invisible = idn().invisible
    for (i, label) in enumerate(labels)
        isempty(label) && return false
        if all(<(0x80), label)
            ascii = Bytes(UInt8[c % UInt8 for c in label])
            hostname_label_ok(ascii) || return false
            starts_with_xn(ascii) && !punycode_label_ok(sub(ascii, 4)) && return false
            bidi_label_ok(unicode_labels[i], is_bidi) || return false
            ascii_length += length(label) + 1
        else
            idn_label_ok(label) || return false
            without_joiners = filter(r -> r != 0x200c && r != 0x200d, label)
            any(r -> ranges_contain(invisible, r), without_joiners) && return false
            disallowed(without_joiners) && return false
            bidi_label_ok(label, is_bidi) || return false
            a_label_length = 4 + length(punycode_encode(label))
            a_label_length > 63 && return false
            ascii_length += a_label_length + 1
        end
    end
    return ascii_length - 1 <= 253
end

# The ASCII characters of an e-mail atom: letters, digits and !#$%&'*+/=?^_`{|}~-
const EMAIL_ATOM_SET = let s = set_union(set_union(char_range(UInt8('0'), UInt8('9')),
        char_range(UInt8('A'), UInt8('Z'))), char_range(UInt8('a'), UInt8('z')))
    for c in "!#\$%&'*+/=?^_`{|}~-"
        s = set_union(s, char_single(c))
    end
    s
end

# Reads the local part of an e-mail address: atoms separated by single dots, or a quoted string. An atom of an
# internationalized local part (RFC 6531 extends atext of RFC 5322) is taken here to be a letter, a mark or a number
# of any script, or one of the ASCII atom characters, as in the other ports of the evaluator.
function email_local_ok(s::Bytes, idn_local::Bool)
    n = s.len
    n == 0 && return false
    if at(s, 0) == UInt8('"')
        # "(?:[^"\\\r\n]|\\.)*" where "." is any character but a line feed.
        i = 1
        while i < n
            c = at(s, i)
            if c == UInt8('"')
                return i == n - 1
            elseif c == UInt8('\\')
                (i + 1 >= n || at(s, i + 1) == UInt8('\n')) && return false
                _, size = decode_rune(s, i + 1)
                i += 1 + size
            elseif c == UInt8('\r') || c == UInt8('\n')
                return false
            else
                i += 1
            end
        end
        return false
    end
    atom_length = 0
    i = 0
    while i < n
        c = at(s, i)
        if c == UInt8('.')
            atom_length == 0 && return false
            atom_length = 0
            i += 1
        elseif c < 0x80
            has_ascii(EMAIL_ATOM_SET, c) || return false
            atom_length += 1
            i += 1
        else
            idn_local || return false
            r, size = decode_rune(s, i)
            ranges_contain(idn().letter_mark_number, r) || return false
            atom_length += 1
            i += size
        end
    end
    return atom_length != 0
end

function is_email(s::Bytes, idn_address::Bool)
    split = last_index_of(s, UInt8('@'))
    split <= 0 && return false
    local_part, domain = sub(s, 0, split), sub(s, split + 1)
    email_local_ok(local_part, idn_address) || return false
    if domain.len >= 2 && at(domain, 0) == UInt8('[') && at(domain, domain.len - 1) == UInt8(']')
        inner = sub(domain, 1, domain.len - 1)
        if inner.len >= 5 && (at(inner, 0) | 0x20) == UInt8('i') && (at(inner, 1) | 0x20) == UInt8('p') &&
           (at(inner, 2) | 0x20) == UInt8('v') && at(inner, 3) == UInt8('6') && at(inner, 4) == UInt8(':')
            return is_ipv6(sub(inner, 5))
        end
        return is_ipv4(inner)
    end
    return idn_address ? is_idn_hostname(domain) : is_hostname(domain)
end

# ----------------------------------------------------------------------------------------------------------------------
# The RFC 3986 (URI) and RFC 3987 (IRI) grammars, read directly. The grammar is deterministic once the fragment and
# the query are cut off at the first "#" and the first "?", so each part is one pass.

const URI_SUB_DELIMS = let s = CharSet()
    for c in "!\$&'()*+,;="
        s = set_union(s, char_single(c))
    end
    s
end

const URI_UNRESERVED = let s = set_union(set_union(char_range(UInt8('0'), UInt8('9')),
        char_range(UInt8('A'), UInt8('Z'))), char_range(UInt8('a'), UInt8('z')))
    for c in "-._~"
        s = set_union(s, char_single(c))
    end
    s
end

const URI_REG_NAME = set_union(URI_UNRESERVED, URI_SUB_DELIMS)
const URI_USERINFO = set_union(URI_REG_NAME, char_single(':'))
const URI_PCHAR = set_union(URI_USERINFO, char_single('@'))
const URI_SEGMENT_NC = set_union(URI_REG_NAME, char_single('@'))
const URI_QUERY = set_union(set_union(URI_PCHAR, char_single('/')), char_single('?'))

is_ucschar(c::UInt32) = (0xa0 <= c <= 0xd7ff) || (0xf900 <= c <= 0xfdcf) || (0xfdf0 <= c <= 0xffef) ||
                        (0x10000 <= c <= 0xefffd)
is_iprivate(c::UInt32) = (0xe000 <= c <= 0xf8ff) || (0xf0000 <= c <= 0xffffd) || (0x100000 <= c <= 0x10fffd)

# Reports whether s[from:to) is characters of the set, percent-encoded triplets and, for an IRI, the characters
# beyond ASCII that RFC 3987 allows (with the private ranges too, for a query).
function uri_chars_ok(s::Bytes, from::Int, to::Int, set::CharSet, iri::Bool, private::Bool)
    i = from
    while i < to
        c = at(s, i)
        if c == UInt8('%')
            (i + 2 < to && is_hex_digit(at(s, i + 1)) && is_hex_digit(at(s, i + 2))) || return false
            i += 3
        elseif c < 0x80
            has_ascii(set, c) || return false
            i += 1
        else
            iri || return false
            r, size = decode_rune(Bytes(s.b, s.off, to), i)
            (is_ucschar(r) || (private && is_iprivate(r))) || return false
            i += size
        end
    end
    return true
end

# Reads an authority in s[from:to): optional user information, a host (an IP literal, or a registered name, of which
# an IPv4 address is one) and an optional port.
function uri_authority_ok(s::Bytes, from::Int, to::Int, iri::Bool)
    host = from
    for i in from:to-1
        if at(s, i) == UInt8('@')
            # The host and the port have no "@", so the user information ends at the only one.
            host > from && return false
            uri_chars_ok(s, from, i, URI_USERINFO, iri, false) || return false
            host = i + 1
        end
    end
    port = to
    if host < to && at(s, host) == UInt8('[')
        close = -1
        for i in host+1:to-1
            if at(s, i) == UInt8(']')
                close = i
                break
            end
        end
        close < 0 && return false
        inner = sub(s, host + 1, close)
        if inner.len > 0 && at(inner, 0) == UInt8('v')
            # "v" hex digits "." then unreserved characters, sub-delimiters or ":".
            dot = index_of(inner, UInt8('.'))
            dot < 2 && return false
            for i in 1:dot-1
                is_hex_digit(at(inner, i)) || return false
            end
            dot + 1 < inner.len || return false
            for i in dot+1:inner.len-1
                c = at(inner, i)
                (c < 0x80 && has_ascii(URI_USERINFO, c)) || return false
            end
        else
            is_ipv6(inner) || return false
        end
        port = close + 1
    else
        for i in host:to-1
            if at(s, i) == UInt8(':')
                port = i
                break
            end
        end
        uri_chars_ok(s, host, port, URI_REG_NAME, iri, false) || return false
    end
    if port < to
        at(s, port) == UInt8(':') || return false
        for i in port+1:to-1
            is_ascii_digit(at(s, i)) || return false
        end
    end
    return true
end

# Reads the part of a URI between the scheme and the query in s[from:to): "//" authority and a path, or a path. A
# relative reference's first segment has no ":" (no_scheme).
function uri_hier_ok(s::Bytes, from::Int, to::Int, iri::Bool, no_scheme::Bool)
    from == to && return true
    if to - from >= 2 && at(s, from) == UInt8('/') && at(s, from + 1) == UInt8('/')
        stop = to
        for i in from+2:to-1
            if at(s, i) == UInt8('/')
                stop = i
                break
            end
        end
        uri_authority_ok(s, from + 2, stop, iri) || return false
        return uri_chars_ok(s, stop, to, URI_QUERY_PATH, iri, false)
    end
    if at(s, from) == UInt8('/')
        # An absolute path: "/" alone, or a first segment that is not empty.
        from + 1 == to && return true
        at(s, from + 1) == UInt8('/') && return false
        return uri_chars_ok(s, from + 1, to, URI_QUERY_PATH, iri, false)
    end
    # A path with no root: a first segment that is not empty.
    stop = to
    for i in from:to-1
        if at(s, i) == UInt8('/')
            stop = i
            break
        end
    end
    uri_chars_ok(s, from, stop, no_scheme ? URI_SEGMENT_NC : URI_PCHAR, iri, false) || return false
    return uri_chars_ok(s, stop, to, URI_QUERY_PATH, iri, false)
end

# The characters of path segments and the "/" between them.
const URI_QUERY_PATH = set_union(URI_PCHAR, char_single('/'))

# Reads a URI or an IRI, and with reference a relative reference too.
function is_uri(s::Bytes, iri::Bool, reference::Bool)
    stop = s.len
    hash = index_of(s, UInt8('#'))
    if hash >= 0
        uri_chars_ok(s, hash + 1, stop, URI_QUERY, iri, false) || return false
        stop = hash
    end
    path_end = stop
    for i in 0:stop-1
        if at(s, i) == UInt8('?')
            uri_chars_ok(s, i + 1, stop, URI_QUERY, iri, iri) || return false
            path_end = i
            break
        end
    end
    # The scheme: a letter, then letters, digits, "+", "-" and ".", then ":".
    colon = -1
    if path_end > 0 && is_ascii_letter(at(s, 0))
        for i in 1:path_end-1
            c = at(s, i)
            if c == UInt8(':')
                colon = i
                break
            end
            (is_ascii_letter(c) || is_ascii_digit(c) || c == UInt8('+') || c == UInt8('-') || c == UInt8('.')) ||
                break
        end
    end
    colon >= 0 && uri_hier_ok(s, colon + 1, path_end, iri, false) && return true
    return reference && uri_hier_ok(s, 0, path_end, iri, true)
end

# Reads one variable of a URI template expression at i: name characters (letters, digits, "_" and percent-encoded
# triplets) with single dots between them, then an optional prefix length or "*". The next offset, or -1.
function uri_template_var(s::Bytes, i::Int)
    n = s.len
    need = true
    while true
        if i < n && at(s, i) == UInt8('%')
            (i + 2 < n && is_hex_digit(at(s, i + 1)) && is_hex_digit(at(s, i + 2))) || return -1
            i += 3
        elseif i < n && (is_ascii_alphanumeric(at(s, i)) || at(s, i) == UInt8('_'))
            i += 1
        elseif need
            return -1
        else
            break
        end
        need = false
        if i < n && at(s, i) == UInt8('.')
            i += 1
            need = true
        end
    end
    if i < n && at(s, i) == UInt8('*')
        return i + 1
    elseif i < n && at(s, i) == UInt8(':')
        (i + 1 < n && UInt8('1') <= at(s, i + 1) <= UInt8('9')) || return -1
        i += 2
        digits = 0
        while i < n && is_ascii_digit(at(s, i)) && digits < 3
            i += 1
            digits += 1
        end
    end
    return i
end

# Reads an RFC 6570 URI template: literal characters and expressions in braces.
function is_uri_template(s::Bytes)
    n = s.len
    i = 0
    while i < n
        c = at(s, i)
        if c == UInt8('{')
            i += 1
            if i < n && at(s, i) in codeunits("+#./;?&=,!@|")
                i += 1
            end
            while true
                i = uri_template_var(s, i)
                i < 0 && return false
                i < n || return false
                if at(s, i) == UInt8(',')
                    i += 1
                elseif at(s, i) == UInt8('}')
                    i += 1
                    break
                else
                    return false
                end
            end
        elseif c <= 0x20 || c in codeunits("\"'<>\\^`|}")
            return false
        else
            i += 1
        end
    end
    return true
end

# Reads an RFC 6901 JSON pointer: segments that each start with "/", where "~" is followed by 0 or 1.
function is_json_pointer(s::Bytes)
    n = s.len
    n != 0 && at(s, 0) != UInt8('/') && return false
    for i in 0:n-1
        if at(s, i) == UInt8('~')
            (i + 1 >= n || (at(s, i + 1) != UInt8('0') && at(s, i + 1) != UInt8('1'))) && return false
        end
    end
    return true
end

# Reads a non-negative integer without leading zeros, then "#" or a JSON pointer.
function is_relative_json_pointer(s::Bytes)
    n = s.len
    i = 0
    while i < n && is_ascii_digit(at(s, i))
        i += 1
    end
    (i == 0 || (i > 1 && at(s, 0) == UInt8('0'))) && return false
    rest = sub(s, i)
    return (rest.len == 1 && at(rest, 0) == UInt8('#')) || is_json_pointer(rest)
end
