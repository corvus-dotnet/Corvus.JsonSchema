//! Format assertions, applied only when `format` is asserted (the format-assertion vocabulary or `assert_format`).
//! A port of the TypeScript port's `formats.ts`, itself matching the C# `JsonSchemaEvaluation` format checks.

use std::sync::LazyLock;

use regex::Regex;
use serde_json::Number;

use crate::dialect::Dialect;

/// The format a dialect recognises for a `format` value (`SchemaCompiler.GetFormatKind`).
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
pub(crate) enum FormatKind {
    Unknown,
    Date,
    Time,
    DateTime,
    Duration,
    Uuid,
    Ipv4,
    Ipv6,
    Hostname,
    IdnHostname,
    Email,
    IdnEmail,
    Uri,
    UriReference,
    Iri,
    IriReference,
    UriTemplate,
    JsonPointer,
    RelativeJsonPointer,
    Regex,
    // Numeric formats (a Corvus extension).
    Byte,
    UInt16,
    UInt32,
    UInt64,
    UInt128,
    SByte,
    Int16,
    Int32,
    Int64,
    Int128,
    Half,
    Single,
    Double,
    Decimal,
}

impl FormatKind {
    pub fn of(format: &str, dialect: Dialect) -> FormatKind {
        use FormatKind::*;
        let at_least = |d: Dialect, k: FormatKind| if dialect >= d { k } else { Unknown };
        match format {
            "float" | "single" => Single,
            "byte" => Byte,
            "uint16" => UInt16,
            "uint32" => UInt32,
            "uint64" => UInt64,
            "uint128" => UInt128,
            "sbyte" => SByte,
            "int16" => Int16,
            "int32" => Int32,
            "int64" => Int64,
            "int128" => Int128,
            "half" => Half,
            "double" => Double,
            "decimal" => Decimal,
            "date-time" => DateTime,
            "email" => Email,
            "hostname" => Hostname,
            "ipv4" => Ipv4,
            "ipv6" => Ipv6,
            "uri" => Uri,
            "uri-reference" => at_least(Dialect::Draft6, UriReference),
            "uri-template" => at_least(Dialect::Draft6, UriTemplate),
            "json-pointer" => at_least(Dialect::Draft6, JsonPointer),
            "date" => at_least(Dialect::Draft7, Date),
            "time" => at_least(Dialect::Draft7, Time),
            "regex" => at_least(Dialect::Draft7, Regex),
            "relative-json-pointer" => at_least(Dialect::Draft7, RelativeJsonPointer),
            "idn-email" => at_least(Dialect::Draft7, IdnEmail),
            "idn-hostname" => at_least(Dialect::Draft7, IdnHostname),
            "iri" => at_least(Dialect::Draft7, Iri),
            "iri-reference" => at_least(Dialect::Draft7, IriReference),
            "duration" => at_least(Dialect::Draft201909, Duration),
            "uuid" => at_least(Dialect::Draft201909, Uuid),
            _ => Unknown,
        }
    }

    pub fn is_numeric(self) -> bool {
        use FormatKind::*;
        matches!(
            self,
            Byte | UInt16
                | UInt32
                | UInt64
                | UInt128
                | SByte
                | Int16
                | Int32
                | Int64
                | Int128
                | Half
                | Single
                | Double
                | Decimal
        )
    }

    /// The canonical name, for messages.
    pub fn name(self) -> &'static str {
        use FormatKind::*;
        match self {
            Unknown => "unknown",
            Date => "date",
            Time => "time",
            DateTime => "date-time",
            Duration => "duration",
            Uuid => "uuid",
            Ipv4 => "ipv4",
            Ipv6 => "ipv6",
            Hostname => "hostname",
            IdnHostname => "idn-hostname",
            Email => "email",
            IdnEmail => "idn-email",
            Uri => "uri",
            UriReference => "uri-reference",
            Iri => "iri",
            IriReference => "iri-reference",
            UriTemplate => "uri-template",
            JsonPointer => "json-pointer",
            RelativeJsonPointer => "relative-json-pointer",
            Regex => "regex",
            Byte => "byte",
            UInt16 => "uint16",
            UInt32 => "uint32",
            UInt64 => "uint64",
            UInt128 => "uint128",
            SByte => "sbyte",
            Int16 => "int16",
            Int32 => "int32",
            Int64 => "int64",
            Int128 => "int128",
            Half => "half",
            Single => "single",
            Double => "double",
            Decimal => "decimal",
        }
    }

    /// The message for a string format failure (Corvus.Text.Json Strings.resx).
    pub fn message(self) -> Option<&'static str> {
        use FormatKind::*;
        Some(match self {
            Date => "Expected an ISO8601 Date string.",
            DateTime => "Expected an ISO8601 Offset DateTime string.",
            Time => "Expected an ISO8601 Offset Time string.",
            Duration => "Expected an ISO8601 Duration string.",
            Email => "Expected an RFC5321 Section-4.1.2 Email string.",
            IdnEmail => "Expected an RFC6531 IDN Email string.",
            Hostname => "Expected an RFC1035 hostname.",
            IdnHostname => "Expected an RFC5890 Section-2.3.2.3 IDN hostname.",
            Ipv4 => "Expected an RFC2673 IP V4 address.",
            Ipv6 => "Expected an RFC2373 IP V6 address.",
            Uri => "Expected an absolute URI.",
            UriReference => "Expected a URI reference.",
            Iri => "Expected an absolute IRI.",
            IriReference => "Expected an IRI reference.",
            Uuid => "Expected an RFC4122 UUID.",
            UriTemplate => "Expected an RFC6570 URI Template.",
            JsonPointer => "Expected an RFC6901 JSON Pointer.",
            RelativeJsonPointer => {
                "Expected a Relative JSON Pointer. (https://json-schema.org/draft/2020-12/relative-json-pointer)."
            }
            Regex => "Expected a regular expression specification.",
            _ => return None,
        })
    }

    /// Asserts a string format. `legacy_hostname` selects the RFC 1123 host name rules of draft 4 and 6.
    pub fn check_string(self, s: &str, legacy_hostname: bool) -> bool {
        use FormatKind::*;
        match self {
            Date => date(s),
            Time => time(s),
            DateTime => date_time(s),
            Duration => DURATION_RE.is_match(s),
            Uuid => uuid(s),
            Ipv4 => ipv4(s),
            Ipv6 => ipv6(s),
            Hostname => {
                if legacy_hostname {
                    legacy_host_name(s)
                } else {
                    hostname(s)
                }
            }
            IdnHostname => idn_hostname(s),
            Email => email(s, false),
            IdnEmail => email(s, true),
            Uri => URI_RE.is_match(s),
            UriReference => URI_REF_RE.is_match(s),
            Iri => IRI_RE.is_match(s),
            IriReference => IRI_REF_RE.is_match(s),
            UriTemplate => URI_TEMPLATE_RE.is_match(s),
            JsonPointer => JSON_POINTER_RE.is_match(s),
            RelativeJsonPointer => RELATIVE_JSON_POINTER_RE.is_match(s),
            Regex => crate::pattern::is_valid_ecma_regex(s),
            _ => true,
        }
    }

    /// Asserts a numeric format.
    pub fn check_number(self, n: &Number) -> bool {
        use FormatKind::*;
        let int_range = |min: i128, max: i128| -> bool {
            if let Some(i) = n.as_i64() {
                (i as i128) >= min && (i as i128) <= max
            } else if let Some(u) = n.as_u64() {
                (u as i128) >= min && (u as i128) <= max
            } else {
                let f = n.as_f64().unwrap_or(f64::NAN);
                f.is_finite() && f.fract() == 0.0 && f >= min as f64 && f <= max as f64
            }
        };
        let magnitude = |max: f64| n.as_f64().is_some_and(|f| f.is_finite() && f.abs() <= max);
        match self {
            Byte => int_range(0, 255),
            UInt16 => int_range(0, 65535),
            UInt32 => int_range(0, 4294967295),
            UInt64 => int_range(0, u64::MAX as i128),
            UInt128 => {
                if let Some(i) = n.as_i64() {
                    i >= 0
                } else if n.as_u64().is_some() {
                    true
                } else {
                    let f = n.as_f64().unwrap_or(f64::NAN);
                    f.is_finite() && f.fract() == 0.0 && (0.0..=3.402_823_669_209_385e38).contains(&f)
                }
            }
            SByte => int_range(-128, 127),
            Int16 => int_range(-32768, 32767),
            Int32 => int_range(-2147483648, 2147483647),
            Int64 => int_range(i64::MIN as i128, i64::MAX as i128),
            Int128 => {
                if n.as_i64().is_some() || n.as_u64().is_some() {
                    true
                } else {
                    let f = n.as_f64().unwrap_or(f64::NAN);
                    f.is_finite() && f.fract() == 0.0 && f.abs() <= 1.701_411_834_604_692_3e38
                }
            }
            Half => magnitude(65504.0),
            Single => magnitude(3.402_823_466_385_288_6e38),
            Double => magnitude(f64::MAX),
            Decimal => magnitude(7.922_816_251_426_434e28),
            _ => true,
        }
    }
}

fn is_leap_year(y: u32) -> bool {
    y % 4 == 0 && (y % 100 != 0 || y % 400 == 0)
}

fn digits(s: &[u8]) -> Option<u32> {
    if s.is_empty() || !s.iter().all(u8::is_ascii_digit) {
        return None;
    }
    Some(s.iter().fold(0u32, |a, &b| a * 10 + (b - b'0') as u32))
}

pub(crate) fn date(s: &str) -> bool {
    let b = s.as_bytes();
    if b.len() != 10 || b[4] != b'-' || b[7] != b'-' {
        return false;
    }
    let (Some(y), Some(m), Some(d)) = (digits(&b[0..4]), digits(&b[5..7]), digits(&b[8..10])) else {
        return false;
    };
    const DAYS: [u32; 13] = [0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];
    (1..=12).contains(&m) && d >= 1 && d <= if m == 2 && is_leap_year(y) { 29 } else { DAYS[m as usize] }
}

pub(crate) fn time(s: &str) -> bool {
    let b = s.as_bytes();
    if b.len() < 9 || b[2] != b':' || b[5] != b':' {
        return false;
    }
    let (Some(h), Some(mi), Some(sec)) = (digits(&b[0..2]), digits(&b[3..5]), digits(&b[6..8])) else {
        return false;
    };
    let mut i = 8;
    if b[i] == b'.' {
        i += 1;
        let start = i;
        while i < b.len() && b[i].is_ascii_digit() {
            i += 1;
        }
        if i == start {
            return false;
        }
    }
    if i >= b.len() {
        return false;
    }
    let (mut oh, mut om, mut sign) = (0u32, 0u32, 0i32);
    match b[i] {
        b'z' | b'Z' => {
            if i + 1 != b.len() {
                return false;
            }
        }
        b'+' | b'-' => {
            if b.len() - i != 6 || b[i + 3] != b':' {
                return false;
            }
            sign = if b[i] == b'-' { 1 } else { -1 };
            let (Some(x), Some(y)) = (digits(&b[i + 1..i + 3]), digits(&b[i + 4..i + 6])) else {
                return false;
            };
            if x > 23 || y > 59 {
                return false;
            }
            oh = x;
            om = y;
        }
        _ => return false,
    }
    if h > 23 || mi > 59 || sec > 60 {
        return false;
    }
    if sec == 60 {
        // A leap second is only valid at 23:59:60 UTC.
        let utc = (h * 60 + mi) as i32 + sign * (oh * 60 + om) as i32;
        return utc.rem_euclid(1440) == 23 * 60 + 59;
    }
    true
}

pub(crate) fn date_time(s: &str) -> bool {
    let b = s.as_bytes();
    b.len() > 11 && (b[10] == b'T' || b[10] == b't') && s.is_char_boundary(10) && date(&s[..10]) && time(&s[11..])
}

static DURATION_RE: LazyLock<Regex> = LazyLock::new(|| {
    let t = r"(?:[0-9]+H(?:[0-9]+M(?:[0-9]+S)?)?|[0-9]+M(?:[0-9]+S)?|[0-9]+S)";
    Regex::new(&format!(
        r"^P(?:[0-9]+W|(?:[0-9]+Y(?:[0-9]+M(?:[0-9]+D)?)?|[0-9]+M(?:[0-9]+D)?|[0-9]+D)(?:T{t})?|T{t})$"
    ))
    .unwrap()
});

pub(crate) fn uuid(s: &str) -> bool {
    let b = s.as_bytes();
    b.len() == 36
        && b.iter()
            .enumerate()
            .all(|(i, &c)| if matches!(i, 8 | 13 | 18 | 23) { c == b'-' } else { c.is_ascii_hexdigit() })
}

pub(crate) fn ipv4(s: &str) -> bool {
    let mut parts = 0;
    for p in s.split('.') {
        parts += 1;
        let b = p.as_bytes();
        if parts > 4
            || b.is_empty()
            || b.len() > 3
            || !b.iter().all(u8::is_ascii_digit)
            || (b.len() > 1 && b[0] == b'0')
        {
            return false;
        }
        if b.iter().fold(0u32, |v, &c| v * 10 + u32::from(c - b'0')) > 255 {
            return false;
        }
    }
    parts == 4
}

pub(crate) fn ipv6(s: &str) -> bool {
    // A valid address is at most 51 characters (an IPv4 tail after six groups), so longer text fails without a copy.
    if s.len() < 2 || s.len() > 64 || !s.bytes().all(|c| c.is_ascii_hexdigit() || c == b':' || c == b'.') {
        return false;
    }
    // An IPv4 tail counts as two groups: check it, then read the address with "0:0" in its place.
    let mut buf = [0u8; 67];
    let tail = if s.contains('.') {
        let Some(last_colon) = s.rfind(':') else {
            return false;
        };
        if !ipv4(&s[last_colon + 1..]) {
            return false;
        }
        let head = &s.as_bytes()[..=last_colon];
        buf[..head.len()].copy_from_slice(head);
        buf[head.len()..head.len() + 3].copy_from_slice(b"0:0");
        std::str::from_utf8(&buf[..head.len() + 3]).unwrap()
    } else {
        s
    };
    let hex_ok = |p: &str| !p.is_empty() && p.len() <= 4 && p.bytes().all(|c| c.is_ascii_hexdigit());
    // The number of groups, when every one is valid.
    let groups = |x: &str| -> Option<usize> {
        if x.is_empty() {
            return Some(0);
        }
        let mut n = 0;
        for p in x.split(':') {
            if !hex_ok(p) {
                return None;
            }
            n += 1;
        }
        Some(n)
    };
    if let Some(dbl) = tail.find("::") {
        if tail[dbl + 1..].contains("::") {
            return false;
        }
        return matches!((groups(&tail[..dbl]), groups(&tail[dbl + 2..])), (Some(l), Some(r)) if l + r < 8);
    }
    groups(tail) == Some(8)
}

fn ldh_label(l: &str) -> bool {
    let b = l.as_bytes();
    !b.is_empty()
        && b.len() <= 63
        && b[0].is_ascii_alphanumeric()
        && b[b.len() - 1].is_ascii_alphanumeric()
        && b.iter().all(|&c| c.is_ascii_alphanumeric() || c == b'-')
}

fn starts_with_xn(l: &str) -> bool {
    l.len() >= 4 && l.as_bytes()[..4].eq_ignore_ascii_case(b"xn--")
}

fn hostname_label_ok(label: &str) -> bool {
    if !ldh_label(label) {
        return false;
    }
    // "--" in the third and fourth positions is reserved for A-labels (xn--).
    let b = label.as_bytes();
    !(b.len() >= 4 && b[2] == b'-' && b[3] == b'-' && !starts_with_xn(label))
}

/// RFC 1123 host names (draft 4 and 6): no IDNA rules for "--" or A-labels.
pub(crate) fn legacy_host_name(s: &str) -> bool {
    !s.is_empty() && s.len() <= 253 && s.split('.').all(ldh_label)
}

pub(crate) fn hostname(s: &str) -> bool {
    if s.is_empty() || s.len() > 253 {
        return false;
    }
    s.split('.').all(|l| hostname_label_ok(l) && (!starts_with_xn(l) || punycode_label_ok(&l[4..])))
}

// Code points IDNA2008 disallows that the tests exercise: controls, format characters, spaces, unassigned,
// uppercase and titlecase letters (mapped away, never PVALID), and symbols.
static DISALLOWED_RE: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"[\p{Cc}\p{Co}\p{Cn}\p{Zs}\p{Zl}\p{Zp}\p{Lu}\p{Lt}\p{Sm}\p{So}\p{P}]").unwrap());
const DISALLOWED_EXCEPTIONS: [char; 7] =
    ['\u{06fd}', '\u{06fe}', '\u{0f0b}', '\u{00b7}', '\u{05f3}', '\u{05f4}', '\u{30fb}'];

fn disallowed(label: &str) -> bool {
    let stripped: String = label.chars().filter(|c| !DISALLOWED_EXCEPTIONS.contains(c) && *c != '-').collect();
    DISALLOWED_RE.is_match(&stripped)
}

fn punycode_label_ok(encoded: &str) -> bool {
    let Some(decoded) = punycode_decode(encoded) else {
        return false;
    };
    if decoded.is_empty() || decoded.is_ascii() {
        return false;
    }
    // The encoding must be canonical: re-encoding the U-label gives the same A-label.
    if punycode_encode(&decoded) != encoded.to_ascii_lowercase() {
        return false;
    }
    idn_label_ok(&decoded) && !disallowed(&decoded) && bidi_label_ok(&decoded, bidi_domain(&decoded))
}

const BASE: u32 = 36;
const T_MIN: u32 = 1;
const T_MAX: u32 = 26;
const SKEW: u32 = 38;
const DAMP: u32 = 700;

fn adapt(mut delta: u32, num_points: u32, first_time: bool) -> u32 {
    delta = if first_time { delta / DAMP } else { delta >> 1 };
    delta += delta / num_points;
    let mut k = 0;
    while delta > ((BASE - T_MIN) * T_MAX) >> 1 {
        delta /= BASE - T_MIN;
        k += BASE;
    }
    k + ((BASE - T_MIN + 1) * delta) / (delta + SKEW)
}

// RFC 3492 encoding, for the canonical round-trip and A-label length checks.
fn punycode_encode(input: &str) -> String {
    let cps: Vec<u32> = input.chars().map(|c| c as u32).collect();
    let mut out: String = input.chars().filter(|c| c.is_ascii()).collect();
    let basic_length = out.len() as u32;
    let mut h = basic_length;
    if basic_length > 0 {
        out.push('-');
    }
    let (mut n, mut delta, mut bias) = (128u32, 0u32, 72u32);
    let digit = |d: u32| (if d < 26 { d + 97 } else { d + 22 }) as u8 as char;
    while (h as usize) < cps.len() {
        let m = cps.iter().copied().filter(|&c| c >= n).min().unwrap();
        delta = delta.saturating_add((m - n).saturating_mul(h + 1));
        n = m;
        for &c in &cps {
            if c < n {
                delta = delta.saturating_add(1);
            }
            if c == n {
                let mut q = delta;
                let mut k = BASE;
                loop {
                    let t = if k <= bias {
                        T_MIN
                    } else if k >= bias + T_MAX {
                        T_MAX
                    } else {
                        k - bias
                    };
                    if q < t {
                        break;
                    }
                    out.push(digit(t + (q - t) % (BASE - t)));
                    q = (q - t) / (BASE - t);
                    k += BASE;
                }
                out.push(digit(q));
                bias = adapt(delta, h + 1, h == basic_length);
                delta = 0;
                h += 1;
            }
        }
        delta += 1;
        n += 1;
    }
    out
}

// RFC 3492 decoding, enough to validate A-labels.
fn punycode_decode(input: &str) -> Option<String> {
    let bytes = input.as_bytes();
    let (mut n, mut i, mut bias) = (128u32, 0u32, 72u32);
    let mut output: Vec<u32> = Vec::new();
    let basic = input.rfind('-').unwrap_or(0);
    for &c in &bytes[..basic] {
        if c >= 0x80 {
            return None;
        }
        output.push(c as u32);
    }
    let mut index = if basic > 0 { basic + 1 } else { 0 };
    while index < bytes.len() {
        let oldi = i;
        let mut w = 1u32;
        let mut k = BASE;
        loop {
            let c = *bytes.get(index)? as u32;
            index += 1;
            let digit = if c.wrapping_sub(48) < 10 {
                c - 22
            } else if c.wrapping_sub(65) < 26 {
                c - 65
            } else if c.wrapping_sub(97) < 26 {
                c - 97
            } else {
                BASE
            };
            if digit >= BASE {
                return None;
            }
            i = i.checked_add(digit.checked_mul(w)?)?;
            let t = if k <= bias {
                T_MIN
            } else if k >= bias + T_MAX {
                T_MAX
            } else {
                k - bias
            };
            if digit < t {
                break;
            }
            w = w.checked_mul(BASE - t)?;
            k += BASE;
        }
        let len = output.len() as u32 + 1;
        bias = adapt(i - oldi, len, oldi == 0);
        n = n.checked_add(i / len)?;
        i %= len;
        if n > 0x10ffff {
            return None;
        }
        output.insert(i as usize, n);
        i += 1;
    }
    output.into_iter().map(char::from_u32).collect()
}

static SCRIPT_GREEK: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"^\p{Greek}$").unwrap());
static SCRIPT_HEBREW: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"^\p{Hebrew}$").unwrap());
static KANA_HAN: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"^[\p{Hiragana}\p{Katakana}\p{Han}]$").unwrap());
static MN: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"^\p{Mn}$").unwrap());
static MN_ME: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"^[\p{Mn}\p{Me}]$").unwrap());
static STARTS_WITH_MARK: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"^\p{M}").unwrap());
static ARABIC_LIKE: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"^[\p{Arabic}\p{Syriac}\p{Thaana}\p{Nko}]$").unwrap());
static LETTER_OR_MC: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"^[\p{L}\p{Mc}]$").unwrap());
static RTL: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r"[\p{Hebrew}\p{Arabic}\p{Syriac}\p{Thaana}\p{Nko}\x{0660}-\x{0669}\x{066b}\x{066c}]").unwrap()
});
static CONTROL_FORMAT_SPACE: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"[\p{Cc}\p{Cf}\p{Zs}\p{Cn}]").unwrap());

fn is(re: &Regex, c: char) -> bool {
    let mut buf = [0u8; 4];
    re.is_match(c.encode_utf8(&mut buf))
}

#[derive(Clone, Copy, PartialEq, Eq)]
enum Bidi {
    L,
    R,
    AL,
    AN,
    EN,
    Nsm,
    ON,
}

// RFC 5893 Bidi rule, with Bidi classes approximated by script and general category.
fn bidi_class(c: char) -> Bidi {
    let cp = c as u32;
    if is(&MN_ME, c) {
        return Bidi::Nsm;
    }
    if (0x660..=0x669).contains(&cp) || cp == 0x66b || cp == 0x66c {
        return Bidi::AN;
    }
    if (0x30..=0x39).contains(&cp) || (0x6f0..=0x6f9).contains(&cp) {
        return Bidi::EN;
    }
    if is(&SCRIPT_HEBREW, c) {
        return Bidi::R;
    }
    if is(&ARABIC_LIKE, c) {
        return Bidi::AL;
    }
    if is(&LETTER_OR_MC, c) {
        return Bidi::L;
    }
    Bidi::ON
}

fn bidi_domain(label: &str) -> bool {
    RTL.is_match(label) && label.chars().any(|c| matches!(bidi_class(c), Bidi::R | Bidi::AL | Bidi::AN))
}

fn bidi_label_ok(label: &str, is_bidi_domain: bool) -> bool {
    if !is_bidi_domain {
        return true;
    }
    let classes: Vec<Bidi> = label.chars().map(bidi_class).collect();
    let Some(&first) = classes.first() else {
        return false;
    };
    let mut last = classes.len() - 1;
    while last > 0 && classes[last] == Bidi::Nsm {
        last -= 1;
    }
    match first {
        Bidi::R | Bidi::AL => {
            !classes.contains(&Bidi::L)
                && matches!(classes[last], Bidi::R | Bidi::AL | Bidi::EN | Bidi::AN)
                && !(classes.contains(&Bidi::EN) && classes.contains(&Bidi::AN))
        }
        Bidi::L => {
            !classes.iter().any(|c| matches!(c, Bidi::R | Bidi::AL | Bidi::AN))
                && matches!(classes[last], Bidi::L | Bidi::EN)
        }
        _ => false,
    }
}

const VIRAMAS: [u32; 33] = [
    0x094d, 0x09cd, 0x0a4d, 0x0acd, 0x0b4d, 0x0bcd, 0x0c4d, 0x0ccd, 0x0d3b, 0x0d3c, 0x0d4d, 0x0dca, 0x0e3a, 0x0eba,
    0x0f84, 0x1039, 0x103a, 0x1714, 0x1734, 0x17d2, 0x1a60, 0x1b44, 0x1baa, 0x1bab, 0x1bf2, 0x1bf3, 0x2d7f, 0xa806,
    0xa8c4, 0xa953, 0xa9c0, 0xaaf6, 0xabed,
];

fn zwnj_joining_context(cps: &[char], i: usize) -> bool {
    // (Joining_Type:{L,D})(Joining_Type:T)*ZWNJ(Joining_Type:T)*(Joining_Type:{R,D}) approximated with Arabic letters.
    let is_joiner = |c: char| {
        let c = c as u32;
        (0x0620..=0x064a).contains(&c) || (0x066e..=0x06d3).contains(&c)
    };
    let mut l = i as isize - 1;
    while l >= 0 && is(&MN, cps[l as usize]) {
        l -= 1;
    }
    let mut r = i + 1;
    while r < cps.len() && is(&MN, cps[r]) {
        r += 1;
    }
    l >= 0 && r < cps.len() && is_joiner(cps[l as usize]) && is_joiner(cps[r])
}

// Contextual and disallowed code points from RFC 5892 that the test suite exercises.
fn idn_label_ok(label: &str) -> bool {
    if label.is_empty() || label.starts_with('-') || label.ends_with('-') {
        return false;
    }
    let cps: Vec<char> = label.chars().collect();
    if cps.len() >= 4 && cps[2] == '-' && cps[3] == '-' {
        return false;
    }
    if STARTS_WITH_MARK.is_match(label) {
        return false;
    }
    let has = |lo: u32, hi: u32| cps.iter().any(|&c| (lo..=hi).contains(&(c as u32)));
    if has(0x660, 0x669) && has(0x6f0, 0x6f9) {
        return false;
    }
    for i in 0..cps.len() {
        match cps[i] as u32 {
            0x302e | 0x302f | 0x0640 | 0x07fa | 0x3031..=0x3035 | 0x303b => return false,
            0x00b7 => {
                if !(i > 0 && i < cps.len() - 1 && cps[i - 1] == 'l' && cps[i + 1] == 'l') {
                    return false;
                }
            }
            0x0375 => {
                if !(i < cps.len() - 1 && is(&SCRIPT_GREEK, cps[i + 1])) {
                    return false;
                }
            }
            0x05f3 | 0x05f4 => {
                if !(i > 0 && is(&SCRIPT_HEBREW, cps[i - 1])) {
                    return false;
                }
            }
            0x30fb => {
                if !cps.iter().any(|&d| d != '\u{30fb}' && is(&KANA_HAN, d)) {
                    return false;
                }
            }
            0x200d => {
                if i == 0 || !VIRAMAS.contains(&(cps[i - 1] as u32)) {
                    return false;
                }
            }
            0x200c if (i == 0 || !VIRAMAS.contains(&(cps[i - 1] as u32))) && !zwnj_joining_context(&cps, i) => {
                return false;
            }
            _ => {}
        }
    }
    true
}

pub(crate) fn idn_hostname(s: &str) -> bool {
    if s.is_empty() {
        return false;
    }
    // Label separators: full stop, ideographic full stop, fullwidth full stop, halfwidth ideographic full stop.
    let labels: Vec<&str> = s.split(['.', '\u{3002}', '\u{ff0e}', '\u{ff61}']).collect();
    let unicode_labels: Vec<String> =
        labels
            .iter()
            .map(|l| {
                if starts_with_xn(l) {
                    punycode_decode(&l[4..]).unwrap_or_else(|| l.to_string())
                } else {
                    l.to_string()
                }
            })
            .collect();
    let is_bidi = unicode_labels.iter().any(|l| bidi_domain(l));
    let mut ascii_length = 0usize;
    for (i, label) in labels.iter().enumerate() {
        if label.is_empty() {
            return false;
        }
        if label.is_ascii() {
            if !hostname_label_ok(label) {
                return false;
            }
            if starts_with_xn(label) && !punycode_label_ok(&label[4..]) {
                return false;
            }
            if !bidi_label_ok(&unicode_labels[i], is_bidi) {
                return false;
            }
            ascii_length += label.len() + 1;
        } else {
            if !idn_label_ok(label) {
                return false;
            }
            let without_joiners: String = label.chars().filter(|&c| c != '\u{200c}' && c != '\u{200d}').collect();
            if CONTROL_FORMAT_SPACE.is_match(&without_joiners) || disallowed(&without_joiners) {
                return false;
            }
            if !bidi_label_ok(label, is_bidi) {
                return false;
            }
            let a_label_len = 4 + punycode_encode(label).len();
            if a_label_len > 63 {
                return false;
            }
            ascii_length += a_label_len + 1;
        }
    }
    ascii_length - 1 <= 253
}

static EMAIL_LOCAL_RE: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(r#"^(?:[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*|"(?:[^"\\\r\n]|\\.)*")$"#)
        .unwrap()
});
static IDN_EMAIL_LOCAL_RE: LazyLock<Regex> = LazyLock::new(|| {
    Regex::new(
        r#"^(?:[\p{L}\p{M}\p{N}!#$%&'*+/=?^_`{|}~-]+(?:\.[\p{L}\p{M}\p{N}!#$%&'*+/=?^_`{|}~-]+)*|"(?:[^"\\\r\n]|\\.)*")$"#,
    )
    .unwrap()
});

fn email(s: &str, idn: bool) -> bool {
    let Some(at) = s.rfind('@') else {
        return false;
    };
    if at == 0 {
        return false;
    }
    let local = &s[..at];
    let domain = &s[at + 1..];
    let local_ok = if idn { IDN_EMAIL_LOCAL_RE.is_match(local) } else { EMAIL_LOCAL_RE.is_match(local) };
    if !local_ok {
        return false;
    }
    if domain.starts_with('[') && domain.ends_with(']') && domain.len() >= 2 {
        let inner = &domain[1..domain.len() - 1];
        if inner.len() >= 5 && inner.as_bytes()[..5].eq_ignore_ascii_case(b"IPv6:") {
            return ipv6(&inner[5..]);
        }
        return ipv4(inner);
    }
    if idn { idn_hostname(domain) } else { hostname(domain) }
}

// RFC 3986 (URI) and RFC 3987 (IRI) grammars.
fn uri_regex(iri: bool, reference: bool) -> Regex {
    const HEX: &str = "[0-9A-Fa-f]";
    let pct = format!("%{HEX}{{2}}");
    const SUB: &str = r"[!$&'()*+,;=]";
    const UNRESERVED: &str = r"[A-Za-z0-9\-._~]";
    const UCSCHAR: &str = r"[\x{A0}-\x{D7FF}\x{F900}-\x{FDCF}\x{FDF0}-\x{FFEF}\x{10000}-\x{EFFFD}]";
    let unreserved = if iri { format!("(?:{UNRESERVED}|{UCSCHAR})") } else { UNRESERVED.to_string() };
    let pchar = format!("(?:{unreserved}|{pct}|{SUB}|[:@])");
    let query = if iri {
        format!(r"(?:{pchar}|[/?]|[\x{{E000}}-\x{{F8FF}}\x{{F0000}}-\x{{FFFFD}}\x{{100000}}-\x{{10FFFD}}])*")
    } else {
        format!("(?:{pchar}|[/?])*")
    };
    let fragment = format!("(?:{pchar}|[/?])*");
    let dec_octet = "(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])";
    let ipv4 = format!(r"{dec_octet}(?:\.{dec_octet}){{3}}");
    let h16 = format!("{HEX}{{1,4}}");
    let ls32 = format!("(?:{h16}:{h16}|{ipv4})");
    let ipv6 = format!(
        "(?:(?:{h16}:){{6}}{ls32}|::(?:{h16}:){{5}}{ls32}|(?:{h16})?::(?:{h16}:){{4}}{ls32}|(?:(?:{h16}:){{0,1}}{h16})?::(?:{h16}:){{3}}{ls32}\
         |(?:(?:{h16}:){{0,2}}{h16})?::(?:{h16}:){{2}}{ls32}|(?:(?:{h16}:){{0,3}}{h16})?::{h16}:{ls32}|(?:(?:{h16}:){{0,4}}{h16})?::{ls32}\
         |(?:(?:{h16}:){{0,5}}{h16})?::{h16}|(?:(?:{h16}:){{0,6}}{h16})?::)"
    );
    let ip_literal = format!(r"\[(?:{ipv6}|v{HEX}+\.(?:{UNRESERVED}|{SUB}|:)+)\]");
    let reg_name = format!("(?:{unreserved}|{pct}|{SUB})*");
    let authority = format!("(?:(?:{unreserved}|{pct}|{SUB}|:)*@)?(?:{ip_literal}|{ipv4}|{reg_name})(?::[0-9]*)?");
    let segment = format!("{pchar}*");
    let segment_nz = format!("{pchar}+");
    let segment_nz_nc = format!("(?:{unreserved}|{pct}|{SUB}|@)+");
    let hier_part =
        format!("(?://{authority}(?:/{segment})*|/(?:{segment_nz}(?:/{segment})*)?|{segment_nz}(?:/{segment})*|)");
    let relative_part =
        format!("(?://{authority}(?:/{segment})*|/(?:{segment_nz}(?:/{segment})*)?|{segment_nz_nc}(?:/{segment})*|)");
    let scheme = r"[A-Za-z][A-Za-z0-9+\-.]*";
    let absolute = format!(r"{scheme}:{hier_part}(?:\?{query})?(?:#{fragment})?");
    let relative = format!(r"{relative_part}(?:\?{query})?(?:#{fragment})?");
    let body = if reference { format!("{absolute}|{relative}") } else { absolute };
    Regex::new(&format!("^(?:{body})$")).unwrap()
}

static URI_RE: LazyLock<Regex> = LazyLock::new(|| uri_regex(false, false));
static URI_REF_RE: LazyLock<Regex> = LazyLock::new(|| uri_regex(false, true));
static IRI_RE: LazyLock<Regex> = LazyLock::new(|| uri_regex(true, false));
static IRI_REF_RE: LazyLock<Regex> = LazyLock::new(|| uri_regex(true, true));

static URI_TEMPLATE_RE: LazyLock<Regex> = LazyLock::new(|| {
    let var = "(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2})(?:\\.?(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2}))*(?::[1-9][0-9]{0,3}|\\*)?";
    Regex::new(&format!(r#"^(?:[^\x00-\x20"'<>\\^`{{|}}]|\{{[+#./;?&=,!@|]?{var}(?:,{var})*\}})*$"#)).unwrap()
});
static JSON_POINTER_RE: LazyLock<Regex> = LazyLock::new(|| Regex::new(r"^(?:/(?:[^~/]|~[01])*)*$").unwrap());
static RELATIVE_JSON_POINTER_RE: LazyLock<Regex> =
    LazyLock::new(|| Regex::new(r"^(?:0|[1-9][0-9]*)(?:#|(?:/(?:[^~/]|~[01])*)*)$").unwrap());

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn formats() {
        assert!(date("2020-02-29") && !date("2021-02-29"));
        assert!(time("23:59:60Z") && !time("22:59:60Z") && time("08:30:06.283185+01:00"));
        assert!(date_time("1963-06-19T08:30:06.283185Z") && !date_time("1963-06-19 08:30:06Z"));
        assert!(ipv6("::ffff:192.168.0.1") && !ipv6("1:2:3:4:5:6:7:8:9"));
        assert!(hostname("xn--4gbwdl.xn--wgbh1c") && !hostname("-a.com"));
        assert!(idn_hostname("실례.테스트") && !idn_hostname("〮실례.테스트"));
        assert!(URI_RE.is_match("http://example.com/a?b#c") && !URI_RE.is_match("//example.com"));
        assert!(DURATION_RE.is_match("P4DT12H30M5S") && !DURATION_RE.is_match("PT1D"));
    }

    /// The straightforward (allocating) readings of the IP address formats.
    fn reference_ipv4(s: &str) -> bool {
        let parts: Vec<&str> = s.split('.').collect();
        parts.len() == 4
            && parts.iter().all(|p| {
                !p.is_empty()
                    && p.len() <= 3
                    && p.bytes().all(|c| c.is_ascii_digit())
                    && (p.len() == 1 || !p.starts_with('0'))
                    && p.parse::<u32>().is_ok_and(|v| v <= 255)
            })
    }

    fn reference_ipv6(s: &str) -> bool {
        if s.len() < 2 || !s.bytes().all(|c| c.is_ascii_hexdigit() || c == b':' || c == b'.') {
            return false;
        }
        let mut tail = s.to_string();
        if s.contains('.') {
            let Some(last_colon) = s.rfind(':') else { return false };
            if !reference_ipv4(&s[last_colon + 1..]) {
                return false;
            }
            tail = format!("{}0:0", &s[..=last_colon]);
        }
        let hex_ok = |p: &str| !p.is_empty() && p.len() <= 4 && p.bytes().all(|c| c.is_ascii_hexdigit());
        let parts =
            |x: &str| -> Vec<String> { if x.is_empty() { vec![] } else { x.split(':').map(String::from).collect() } };
        if let Some(dbl) = tail.find("::") {
            if tail[dbl + 1..].contains("::") {
                return false;
            }
            let (left, right) = (parts(&tail[..dbl]), parts(&tail[dbl + 2..]));
            return left.iter().all(|p| hex_ok(p)) && right.iter().all(|p| hex_ok(p)) && left.len() + right.len() < 8;
        }
        let all: Vec<&str> = tail.split(':').collect();
        all.len() == 8 && all.iter().all(|p| hex_ok(p))
    }

    #[test]
    fn ip_addresses_match_the_reference_readings() {
        let seeds = [
            "1.2.3.4",
            "255.255.255.255",
            "0.0.0.0",
            "::",
            "::1",
            "1::",
            "1:2:3:4:5:6:7:8",
            "fe80::1:2:3:4",
            "::ffff:192.168.0.1",
            "1:2:3:4:5:6:1.2.3.4",
            "abcd:ef01:2345:6789:abcd:ef01:2345:6789",
            "1:2:3:4:5:6:7::",
            "::2:3:4:5:6:7:8",
        ];
        let alphabet = b"0123456789abcdefABCDEF:.g ";
        let mut state = 0x2545_f491_4f6c_dd1du64;
        let mut next = |n: usize| {
            state ^= state << 13;
            state ^= state >> 7;
            state ^= state << 17;
            (state % n as u64) as usize
        };
        for seed in seeds {
            for _ in 0..4000 {
                let mut b = seed.as_bytes().to_vec();
                for _ in 0..1 + next(3) {
                    let at = next(b.len() + 1);
                    match next(4) {
                        0 if at < b.len() => {
                            b.remove(at);
                        }
                        1 if at < b.len() => b[at] = alphabet[next(alphabet.len())],
                        2 => b.insert(at, alphabet[next(alphabet.len())]),
                        _ => b.extend_from_within(..next(b.len() + 1)),
                    }
                }
                let s = std::str::from_utf8(&b).unwrap();
                assert_eq!(ipv4(s), reference_ipv4(s), "ipv4 {s:?}");
                assert_eq!(ipv6(s), reference_ipv6(s), "ipv6 {s:?}");
            }
        }
        let long = "1:".repeat(40) + "1";
        assert_eq!(ipv6(&long), reference_ipv6(&long));
    }
}
