"""RFC-accurate ``format`` (and ``content``) checks — a faithful port of the TypeScript runtime ``__fmt``.

``format`` is annotation-only by default; when assertion is enabled (resolved at codegen time) the generated
validator calls :func:`fmt`. Every standard string format is validated here to the same rules as the
TypeScript engine (which the C# engine clears via Bowtie): the date/time family (RFC 3339, leap seconds),
``email``/``idn-email`` (RFC 5321/5322 + IDN), ``hostname``/``idn-hostname`` (RFC 1123 + IDNA/UTS-46 via the
``idna`` package), ``ipv4``/``ipv6``, ``uri``/``uri-reference``/``iri``/``iri-reference`` (RFC 3986/3987 as a
parser), ``uri-template`` (RFC 6570), ``json-pointer``/``relative-json-pointer`` (RFC 6901), ``uuid``
(RFC 4122), ``regex`` (ECMA-262). Unknown formats are annotation-only (``True``).
"""

from __future__ import annotations

import codecs
import re

import idna
import regex

# Fixed ASCII grammars use \A..\Z (not ^..$) so a trailing newline is rejected, matching JS `^..$` (no `m`);
# re.ASCII so `\d` is [0-9] only, matching JS `\d` (Python `\d` otherwise matches Unicode digits, e.g. Bengali).
_DATE = re.compile(r"\A(\d{4})-(\d{2})-(\d{2})\Z", re.ASCII)
_TIME = re.compile(r"\A(\d{2}):(\d{2}):(\d{2})(\.\d+)?([zZ]|[+-]\d{2}:\d{2})\Z", re.ASCII)
_DATE_TIME = re.compile(r"\A(\d{4}-\d{2}-\d{2})[tT](\d{2}:\d{2}:\d{2}(?:\.\d+)?(?:[zZ]|[+-]\d{2}:\d{2}))\Z", re.ASCII)
_OFFSET = re.compile(r"\A([+-])(\d{2}):(\d{2})\Z", re.ASCII)
# duration: RFC 3339 ABNF — a REGULAR grammar, stricter than ISO 8601 (contiguous descending units, no fractions).
_DUR = re.compile(r"\AP(?:\d+W|(?:\d+Y(?:\d+M(?:\d+D)?)?|\d+M(?:\d+D)?|\d+D)?(?:T(?:\d+H(?:\d+M(?:\d+S)?)?|\d+M(?:\d+S)?|\d+S))?)\Z", re.ASCII)
_IPV4 = re.compile(r"\A((25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\Z", re.ASCII)
_HEXTET = re.compile(r"\A[0-9a-fA-F]{1,4}\Z")
_UUID = re.compile(r"\A[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\Z")
_JSON_PTR = re.compile(r"\A(?:/(?:[^~/]|~0|~1)*)*\Z")
_REL_JSON_PTR = re.compile(r"\A(?:0|[1-9][0-9]*)(?:#|(?:/(?:[^~/]|~0|~1)*)*)\Z")
_URI_TEMPLATE = re.compile(r"\A(?:[^\s{}]|\{[^\s{}]*\})*\Z")


class FormatError(ValueError):
    """Raised by a validating factory when a value does not match its declared format."""

    def __init__(self, fmt_name: str) -> None:
        super().__init__(f"value does not match format '{fmt_name}'")
        self.format = fmt_name


def _dim(year: int, month: int) -> int:
    if month == 2:
        return 29 if (year % 4 == 0 and year % 100 != 0) or year % 400 == 0 else 28
    return (31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31)[month - 1]


def fmt_date(value: str) -> bool:
    """RFC 3339 full-date: a valid calendar date."""
    m = _DATE.match(value)
    if m is None:
        return False
    month, day = int(m.group(2)), int(m.group(3))
    return 1 <= month <= 12 and 1 <= day <= _dim(int(m.group(1)), month)


def fmt_time(value: str) -> bool:
    """RFC 3339 full-time: valid clock time with offset (leap second only at 23:59:60 UTC)."""
    m = _TIME.match(value)
    if m is None:
        return False
    hh, mm, ss = int(m.group(1)), int(m.group(2)), int(m.group(3))
    off_min = 0
    off = m.group(5)
    if off not in ("z", "Z"):
        om = _OFFSET.match(off)
        if om is None or int(om.group(2)) > 23 or int(om.group(3)) > 59:
            return False
        off_min = (-1 if om.group(1) == "-" else 1) * (int(om.group(2)) * 60 + int(om.group(3)))
    if hh > 23 or mm > 59:
        return False
    if ss > 59:
        if ss != 60:
            return False
        utc = (((hh * 60 + mm) - off_min) % 1440 + 1440) % 1440
        if utc != 23 * 60 + 59:
            return False
    return True


def fmt_date_time(value: str) -> bool:
    """RFC 3339 date-time."""
    m = _DATE_TIME.match(value)
    return m is not None and fmt_date(m.group(1)) and fmt_time(m.group(2))


def fmt_duration(value: str) -> bool:
    """RFC 3339 duration (not lone ``P``)."""
    return value != "P" and _DUR.match(value) is not None


def fmt_ipv6(value: str) -> bool:
    """An RFC 4291 IPv6 address (optionally with a trailing embedded IPv4), hand-rolled to match the engine."""
    if ":" not in value:
        return False
    parts = value.split("::")
    if len(parts) > 2:
        return False
    head = [] if parts[0] == "" else parts[0].split(":")
    tail = ([] if parts[1] == "" else parts[1].split(":")) if len(parts) == 2 else []
    everything = head + tail if len(parts) == 2 else head
    count = len(everything)
    v4 = False
    if len(everything) > 0 and "." in everything[-1]:
        if _IPV4.match(everything[-1]) is None:
            return False
        v4 = True
        count += 1
    for i, group in enumerate(everything):
        if v4 and i == len(everything) - 1:
            continue
        if _HEXTET.match(group) is None:
            return False
    return count <= 7 if len(parts) == 2 else count == 8


def _is_greek(cp: int) -> bool:
    return (0x370 <= cp <= 0x3FF) or (0x1F00 <= cp <= 0x1FFF)


def _is_hebrew(cp: int) -> bool:
    return (0x590 <= cp <= 0x5FF) or (0xFB1D <= cp <= 0xFB4F)


def _idna_label(label: str) -> bool:
    """The IDNA2008 (RFC 5892) DISALLOWED + CONTEXTO checks tr46's UTS-46 processing does not enforce."""
    cp = [ord(c) for c in label]
    for i, c in enumerate(cp):
        if c in (0x0640, 0x07FA, 0x302E, 0x302F, 0x303B) or 0x3031 <= c <= 0x3035:
            return False
        if c == 0x00B7 and not (i > 0 and cp[i - 1] == 0x6C and i + 1 < len(cp) and cp[i + 1] == 0x6C):
            return False
        if c == 0x0375 and not (i + 1 < len(cp) and _is_greek(cp[i + 1])):
            return False
        if c in (0x05F3, 0x05F4) and not (i > 0 and _is_hebrew(cp[i - 1])):
            return False
    # Katakana Middle Dot (U+30FB): the label must contain a Hiragana/Katakana/Han character.
    if 0x30FB in cp and not any(
        x != 0x30FB and ((0x3040 <= x <= 0x309F) or (0x30A0 <= x <= 0x30FF) or (0x3400 <= x <= 0x4DBF) or (0x4E00 <= x <= 0x9FFF))
        for x in cp
    ):
        return False
    return True


def fmt_hostname(value: str, idn: bool) -> bool:
    """RFC 1123 hostname (``idn`` = allow IDN via IDNA/UTS-46)."""
    if len(value) == 0 or len(value) > 253 or value.startswith(".") or value.endswith(".") or ".." in value:
        return False
    if not idn:
        for label in value.split("."):
            if len(label) == 0 or len(label) > 63 or label.startswith("-") or label.endswith("-"):
                return False
            for c in label:
                if not (("a" <= c <= "z") or ("A" <= c <= "Z") or ("0" <= c <= "9") or c == "-"):
                    return False
            if len(label) >= 4 and label[0] in "xX" and label[1] in "nN" and label[2] == "-" and label[3] == "-":
                # An xn-- A-label must decode to a valid IDNA2008 U-label (marks/joiners/mixing checked by idna),
                # plus the CONTEXTO rules idna does not enforce.
                try:
                    decoded = idna.decode(label, uts46=True)
                except idna.IDNAError:
                    return False
                if not _idna_label(decoded):
                    return False
        return True

    # UTS-46 maps the ideographic / fullwidth / halfwidth full stops to the label separator; a leading, trailing,
    # or doubled separator is an empty label and invalid.
    sep_norm = value.replace("。", ".").replace("．", ".").replace("｡", ".")
    if sep_norm.startswith(".") or sep_norm.endswith(".") or ".." in sep_norm:
        return False
    try:
        idna.encode(value, uts46=True, std3_rules=True)
        unicode_form = idna.decode(value, uts46=True)
    except idna.IDNAError:
        return False
    return all(_idna_label(label) for label in unicode_form.split("."))


def _is_hex(c: str) -> bool:
    return ("0" <= c <= "9") or ("a" <= c <= "f") or ("A" <= c <= "F")


def _all_digits(s: str) -> bool:
    return all("0" <= c <= "9" for c in s)


def _valid_uri_part(s: str, allow_unicode: bool) -> bool:
    i = 0
    length = len(s)
    while i < length:
        c = s[i]
        if c == "%":
            if i + 2 >= length or not _is_hex(s[i + 1]) or not _is_hex(s[i + 2]):
                return False
            i += 3
            continue
        if ("A" <= c <= "Z") or ("a" <= c <= "z") or ("0" <= c <= "9") or c in "-._~:/?#[]@!$&'()*+,;=":
            i += 1
            continue
        if allow_unicode and ord(c) > 127:
            i += 1
            continue
        return False
    return True


def _has_scheme(s: str) -> bool:
    if len(s) == 0 or not (("A" <= s[0] <= "Z") or ("a" <= s[0] <= "z")):
        return False
    i = 1
    while i < len(s) and (("A" <= s[i] <= "Z") or ("a" <= s[i] <= "z") or ("0" <= s[i] <= "9") or s[i] in "+.-"):
        i += 1
    return i < len(s) and s[i] == ":"


def _valid_authority(auth: str, allow_unicode: bool) -> bool:
    del allow_unicode
    at = auth.rfind("@")
    userinfo = auth[:at] if at >= 0 else ""
    host = auth[at + 1:] if at >= 0 else auth
    if "[" in userinfo or "]" in userinfo:
        return False
    if host.startswith("["):
        close = host.find("]")
        if close < 0:
            return False
        inner = host[1:close]
        if not fmt_ipv6(inner) and not (len(inner) > 0 and inner[0] == "v"):
            return False
        rest = host[close + 1:]
        return rest == "" or (rest[0] == ":" and _all_digits(rest[1:]))
    colon = host.find(":")
    if colon >= 0:
        if host.find(":", colon + 1) >= 0 or not _all_digits(host[colon + 1:]):
            return False
    return True


def fmt_uri(value: str, require_scheme: bool, allow_unicode: bool) -> bool:
    """RFC 3986 URI / RFC 3987 IRI as a parser (``require_scheme`` false = reference)."""
    if require_scheme and not _has_scheme(value):
        return False
    if not _valid_uri_part(value, allow_unicode):
        return False
    ds = value.find("//")
    if ds >= 0 and (ds == 0 or value[ds - 1] == ":"):
        end = len(value)
        for i in range(ds + 2, len(value)):
            if value[i] in "/?#":
                end = i
                break
        if not _valid_authority(value[ds + 2:end], allow_unicode):
            return False
    return True


def fmt_email(value: str, idn: bool) -> bool:
    """RFC 5321/5322 email (``idn`` = allow non-ASCII local part / IDN domain)."""
    at = value.rfind("@")
    if at < 1 or at >= len(value) - 1:
        return False
    local, domain = value[:at], value[at + 1:]
    if local.startswith('"') and local.endswith('"'):
        if len(local) < 2:
            return False
    else:
        if local.startswith(".") or local.endswith(".") or ".." in local:
            return False
        for c in local:
            if ("A" <= c <= "Z") or ("a" <= c <= "z") or ("0" <= c <= "9") or c in "!#$%&'*+/=?^_`{|}~.-" or (idn and ord(c) > 127):
                continue
            return False
    if domain.startswith("[") and domain.endswith("]"):
        ip = domain[1:-1]
        return fmt_ipv6(ip[5:]) if ip.startswith("IPv6:") else _IPV4.match(ip) is not None
    return fmt_hostname(domain, idn)


# The only letters ECMA-262 (/u mode) permits after a backslash. Any other letter escape (e.g. `\a`) is a
# SyntaxError in ECMA even though Python's `regex` accepts it — so `format: regex` must reject it.
_ECMA_ESCAPE_LETTERS = frozenset("dDwWsSbBfnrtvcxupPk")


def fmt_regex(value: str) -> bool:
    """``format: regex`` asserts a valid ECMA-262 regular expression.

    Python's ``regex`` engine is more permissive than ECMA-262 (it accepts invalid identity escapes such as
    ``\\a``), so the ECMA-invalid letter escapes are rejected explicitly before the engine gets a chance to
    accept them — the runtime peer of the build-time ``EcmaRegexPythonTranslator``'s escape validation."""
    i, n = 0, len(value)
    while i < n:
        if value[i] == "\\":
            if i + 1 >= n:
                return False
            e = value[i + 1]
            if e.isascii() and e.isalpha() and e not in _ECMA_ESCAPE_LETTERS:
                return False
            i += 2
            continue
        i += 1
    try:
        regex.compile(value)
    except regex.error:
        return False
    return True


def fmt(name: str, value: object) -> bool:
    """True when ``value`` matches the named string format. A non-string, or an unknown (annotation-only)
    format, passes."""
    if not isinstance(value, str):
        return True
    if name == "date":
        return fmt_date(value)
    if name == "date-time":
        return fmt_date_time(value)
    if name == "time":
        return fmt_time(value)
    if name == "duration":
        return fmt_duration(value)
    if name == "email":
        return fmt_email(value, False)
    if name == "idn-email":
        return fmt_email(value, True)
    if name == "hostname":
        return fmt_hostname(value, False)
    if name == "idn-hostname":
        return fmt_hostname(value, True)
    if name == "ipv4":
        return _IPV4.match(value) is not None
    if name == "ipv6":
        return fmt_ipv6(value)
    if name == "uuid":
        return _UUID.match(value) is not None
    if name == "uri":
        return fmt_uri(value, True, False)
    if name == "iri":
        return fmt_uri(value, True, True)
    if name == "uri-reference":
        return fmt_uri(value, False, False)
    if name == "iri-reference":
        return fmt_uri(value, False, True)
    if name == "uri-template":
        return _URI_TEMPLATE.match(value) is not None
    if name == "json-pointer":
        return _JSON_PTR.match(value) is not None
    if name == "relative-json-pointer":
        return _REL_JSON_PTR.match(value) is not None
    if name == "regex":
        return fmt_regex(value)
    return True


def fmt_content(content_encoding: str | None, content_media_type: str | None, value: object) -> bool:
    """Validate ``contentEncoding`` (base64) and ``contentMediaType`` (application/json)."""
    if not isinstance(value, str):
        return True
    content = value
    if content_encoding == "base64":
        if len(value) % 4 != 0:
            return False
        for c in value:
            if not (("A" <= c <= "Z") or ("a" <= c <= "z") or ("0" <= c <= "9") or c in "+/="):
                return False
        try:
            content = codecs.decode(value.encode("ascii"), "base64").decode("utf-8", "strict")
        except (ValueError, UnicodeError):
            return False
    if content_media_type == "application/json":
        import json

        try:
            json.loads(content)
        except ValueError:
            return False
    return True