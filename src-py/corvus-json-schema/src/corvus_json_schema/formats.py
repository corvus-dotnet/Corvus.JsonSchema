"""Format assertions, applied only when ``format`` is asserted (format-assertion vocabulary or assert_format)."""

from __future__ import annotations

import re
from collections.abc import Callable
from typing import Any

from .pattern import is_valid_ecma_regex


def _RX(source: str) -> _Lazy:
    return _Lazy(source, "regex")


class _Lazy:
    """A regular expression compiled on first use, so that importing the package compiles none of them (the URI and
    IRI grammars alone take hundreds of milliseconds). ``engine`` "regex" uses the regex package (Unicode
    properties)."""

    __slots__ = ("_compiled", "_engine", "_source")

    def __init__(self, source: str, engine: str = "re") -> None:
        self._source = source
        self._engine = engine
        self._compiled: Any = None

    def _get(self) -> Any:
        c = self._compiled
        if c is None:
            if self._engine == "regex":
                import regex

                c = regex.compile(self._source)
            else:
                c = re.compile(self._source)
            self._compiled = c
        return c

    def match(self, s: str) -> Any:
        return self._get().match(s)

    def search(self, s: str) -> Any:
        return self._get().search(s)

    def sub(self, repl: str, s: str) -> str:
        return str(self._get().sub(repl, s))

    def split(self, s: str) -> list[str]:
        return list(self._get().split(s))


_DATE_RE = _Lazy(r"^([0-9]{4})-([0-9]{2})-([0-9]{2})\Z")
_TIME_RE = _Lazy(r"^([0-9]{2}):([0-9]{2}):([0-9]{2})(\.[0-9]+)?([zZ]|([+-])([0-9]{2}):([0-9]{2}))\Z")
_DAYS = (0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31)


def _is_leap_year(y: int) -> bool:
    return y % 4 == 0 and (y % 100 != 0 or y % 400 == 0)


def date(s: str) -> bool:
    m = _DATE_RE.match(s)
    if m is None:
        return False
    y, mo, d = int(m.group(1)), int(m.group(2)), int(m.group(3))
    return 1 <= mo <= 12 and 1 <= d <= (29 if mo == 2 and _is_leap_year(y) else _DAYS[mo])


def time(s: str) -> bool:
    m = _TIME_RE.match(s)
    if m is None:
        return False
    h, mi, sec = int(m.group(1)), int(m.group(2)), int(m.group(3))
    oh = 0
    om = 0
    if m.group(6) is not None:
        oh = int(m.group(7))
        om = int(m.group(8))
        if oh > 23 or om > 59:
            return False
    if h > 23 or mi > 59 or sec > 60:
        return False
    if sec == 60:
        # A leap second is only valid at 23:59:60 UTC.
        sign = 1 if m.group(6) == "-" else -1
        utc_minutes = (h * 60 + mi + sign * (oh * 60 + om)) % 1440
        return utc_minutes == 23 * 60 + 59
    return True


def date_time(s: str) -> bool:
    t = -1
    for i, c in enumerate(s):
        if c in "tT":
            t = i
            break
    return t == 10 and date(s[:10]) and time(s[11:])


# RFC 3339 appendix A: dur-year = 1*DIGIT "Y" [dur-month], dur-month = 1*DIGIT "M" [dur-day], and so on.
_DUR_TIME = r"(?:[0-9]+H(?:[0-9]+M(?:[0-9]+S)?)?|[0-9]+M(?:[0-9]+S)?|[0-9]+S)"
_DURATION_RE = _Lazy(
    rf"^P(?:[0-9]+W|(?:[0-9]+Y(?:[0-9]+M(?:[0-9]+D)?)?|[0-9]+M(?:[0-9]+D)?|[0-9]+D)(?:T{_DUR_TIME})?|T{_DUR_TIME})\Z"
)


def duration(s: str) -> bool:
    return _DURATION_RE.match(s) is not None


_UUID_RE = _Lazy(r"^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\Z")


def uuid(s: str) -> bool:
    return _UUID_RE.match(s) is not None


_IPV4_RE = _Lazy(
    r"^(?:(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])\.){3}(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])\Z"
)
_IPV6_CHARS_RE = _Lazy(r"^[0-9a-fA-F:.]+\Z")
_HEX_GROUP_RE = _Lazy(r"^[0-9a-fA-F]{1,4}\Z")


def ipv4(s: str) -> bool:
    return _IPV4_RE.match(s) is not None


def ipv6(s: str) -> bool:
    if len(s) < 2 or _IPV6_CHARS_RE.match(s) is None:
        return False
    groups = 8
    tail = s
    last_colon = s.rfind(":")
    if "." in s:
        if not ipv4(s[last_colon + 1 :]):
            return False
        tail = s[: last_colon + 1] + "0:0"
    dbl = tail.find("::")
    if dbl >= 0 and tail.find("::", dbl + 1) >= 0:
        return False

    def parts(x: str) -> list[str]:
        return [] if x == "" else x.split(":")

    def hex_ok(p: str) -> bool:
        return _HEX_GROUP_RE.match(p) is not None

    if dbl >= 0:
        left = parts(tail[:dbl])
        right = parts(tail[dbl + 2 :])
        return all(map(hex_ok, left)) and all(map(hex_ok, right)) and len(left) + len(right) < groups
    every = tail.split(":")
    return len(every) == groups and all(map(hex_ok, every))


_LABEL_RE = _Lazy(r"^[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?\Z")


def _is_a_label(label: str) -> bool:
    return label[:4].lower() == "xn--"


def _hostname_label_ok(label: str) -> bool:
    if len(label) == 0 or len(label) > 63:
        return False
    if _LABEL_RE.match(label) is None:
        return False
    # "--" in the third and fourth positions is reserved for A-labels (xn--).
    return not (len(label) >= 4 and label[2] == "-" and label[3] == "-" and not _is_a_label(label))


def legacy_hostname(s: str) -> bool:
    """RFC 1123 host names (draft 4 and 6): no IDNA rules for "--" or A-labels."""
    if len(s) == 0 or len(s) > 253:
        return False
    return all(0 < len(label) <= 63 and _LABEL_RE.match(label) is not None for label in s.split("."))


def hostname(s: str) -> bool:
    if len(s) == 0 or len(s) > 253:
        return False
    for label in s.split("."):
        if not _hostname_label_ok(label):
            return False
        if _is_a_label(label) and not _punycode_label_ok(label[4:]):
            return False
    return True


def _punycode_label_ok(encoded: str) -> bool:
    decoded = _punycode_decode(encoded)
    if decoded is None or len(decoded) == 0 or decoded.isascii():
        return False
    # The encoding must be canonical: re-encoding the U-label gives the same A-label.
    if _punycode_encode(decoded) != encoded.lower():
        return False
    return (
        _idn_label_ok(decoded)
        and _DISALLOWED_RE.search(_DISALLOWED_EXCEPTIONS_RE.sub("", decoded)) is None
        and _bidi_label_ok(decoded, _bidi_domain(decoded))
    )


# Code points IDNA2008 disallows that the tests exercise: controls, format characters, spaces, unassigned, uppercase
# and titlecase letters (mapped away, never PVALID), and symbols.
_DISALLOWED_EXCEPTIONS_RE = _RX("[\u06fd\u06fe\u0f0b\u00b7\u05f3\u05f4\u30fb\\-]")
_DISALLOWED_RE = _RX(r"[\p{Cc}\p{Cs}\p{Co}\p{Cn}\p{Zs}\p{Zl}\p{Zp}\p{Lu}\p{Lt}\p{Sm}\p{So}\p{P}]")

_BASE = 36
_T_MIN = 1
_T_MAX = 26
_SKEW = 38
_DAMP = 700


def _adapt(delta: int, num_points: int, first_time: bool) -> int:
    delta = delta // _DAMP if first_time else delta >> 1
    delta += delta // num_points
    k = 0
    while delta > ((_BASE - _T_MIN) * _T_MAX) >> 1:
        delta //= _BASE - _T_MIN
        k += _BASE
    return k + ((_BASE - _T_MIN + 1) * delta) // (delta + _SKEW)


def _punycode_encode(text: str) -> str:
    """RFC 3492 encoding, for the canonical round-trip and A-label length checks."""
    cps = [ord(c) for c in text]
    out = "".join(chr(c) for c in cps if c < 0x80)
    basic_length = len(out)
    h = basic_length
    if basic_length > 0:
        out += "-"
    n = 128
    delta = 0
    bias = 72

    def digit(d: int) -> str:
        return chr(d + 97 if d < 26 else d + 22)

    while h < len(cps):
        m = min(c for c in cps if c >= n)
        delta += (m - n) * (h + 1)
        n = m
        for c in cps:
            if c < n:
                delta += 1
            if c == n:
                q = delta
                k = _BASE
                while True:
                    t = _T_MIN if k <= bias else _T_MAX if k >= bias + _T_MAX else k - bias
                    if q < t:
                        break
                    out += digit(t + (q - t) % (_BASE - t))
                    q = (q - t) // (_BASE - t)
                    k += _BASE
                out += digit(q)
                bias = _adapt(delta, h + 1, h == basic_length)
                delta = 0
                h += 1
        delta += 1
        n += 1
    return out


# RFC 5893 Bidi rule, with Bidi classes approximated by script and general category, as the TypeScript evaluator does.
_RTL_RE = _RX(
    r"[\p{Script=Hebrew}\p{Script=Arabic}\p{Script=Syriac}\p{Script=Thaana}\p{Script=Nko}\u0660-\u0669\u066b\u066c]"
)
_NSM_RE = _RX(r"\p{Mn}|\p{Me}")
_HEBREW_RE = _RX(r"\p{Script=Hebrew}")
_AL_RE = _RX(r"[\p{Script=Arabic}\p{Script=Syriac}\p{Script=Thaana}\p{Script=Nko}]")
_L_RE = _RX(r"\p{L}|\p{Mc}")


def _bidi_class(c: str) -> str:
    cp = ord(c)
    if _NSM_RE.match(c):
        return "NSM"
    if 0x660 <= cp <= 0x669 or cp == 0x66B or cp == 0x66C:
        return "AN"
    if 0x30 <= cp <= 0x39 or 0x6F0 <= cp <= 0x6F9:
        return "EN"
    if _HEBREW_RE.match(c):
        return "R"
    if _AL_RE.match(c):
        return "AL"
    if _L_RE.match(c):
        return "L"
    return "ON"


def _bidi_domain(label: str) -> bool:
    return _RTL_RE.search(label) is not None and any(_bidi_class(c) in ("R", "AL", "AN") for c in label)


def _bidi_label_ok(label: str, is_bidi_domain: bool) -> bool:
    if not is_bidi_domain:
        return True
    classes = [_bidi_class(c) for c in label]
    first = classes[0]
    last = len(classes) - 1
    while last > 0 and classes[last] == "NSM":
        last -= 1
    if first in ("R", "AL"):
        if "L" in classes:
            return False
        if classes[last] not in ("R", "AL", "EN", "AN"):
            return False
        return not ("EN" in classes and "AN" in classes)
    if first == "L":
        if any(c in ("R", "AL", "AN") for c in classes):
            return False
        return classes[last] in ("L", "EN")
    return False


def _punycode_decode(text: str) -> str | None:
    """RFC 3492 decoding, enough to validate A-labels."""
    n = 128
    i = 0
    bias = 72
    output: list[int] = []
    basic = text.rfind("-")
    basic = max(basic, 0)
    for j in range(basic):
        c = ord(text[j])
        if c >= 0x80:
            return None
        output.append(c)
    index = basic + 1 if basic > 0 else 0
    while index < len(text):
        oldi = i
        w = 1
        k = _BASE
        while True:
            if index >= len(text):
                return None
            c = ord(text[index])
            index += 1
            digit = c - 22 if c - 48 < 10 else c - 65 if c - 65 < 26 else c - 97 if c - 97 < 26 else _BASE
            if digit < 0 or digit >= _BASE:
                return None
            i += digit * w
            t = _T_MIN if k <= bias else _T_MAX if k >= bias + _T_MAX else k - bias
            if digit < t:
                break
            w *= _BASE - t
            k += _BASE
        bias = _adapt(i - oldi, len(output) + 1, oldi == 0)
        n += i // (len(output) + 1)
        i %= len(output) + 1
        if n > 0x10FFFF:
            return None
        output.insert(i, n)
        i += 1
    try:
        return "".join(map(chr, output))
    except ValueError:
        return None


_VIRAMAS = frozenset(
    (
        0x094D, 0x09CD, 0x0A4D, 0x0ACD, 0x0B4D, 0x0BCD, 0x0C4D, 0x0CCD, 0x0D3B, 0x0D3C, 0x0D4D, 0x0DCA, 0x0E3A, 0x0EBA,
        0x0F84, 0x1039, 0x103A, 0x1714, 0x1734, 0x17D2, 0x1A60, 0x1B44, 0x1BAA, 0x1BAB, 0x1BF2, 0x1BF3, 0x2D7F, 0xA806,
        0xA8C4, 0xA953, 0xA9C0, 0xAAF6, 0xABED,
    )
)  # fmt: skip
_MARK_START_RE = _RX(r"^\p{M}")
_GREEK_RE = _RX(r"\p{Script=Greek}")
_KANA_HAN_RE = _RX(r"[\p{Script=Hiragana}\p{Script=Katakana}\p{Script=Han}]")
_MN_RE = _RX(r"\p{Mn}")


def _idn_label_ok(label: str) -> bool:
    """Contextual and disallowed code points from RFC 5892 that the test suite exercises."""
    if len(label) == 0:
        return False
    if label.startswith("-") or label.endswith("-"):
        return False
    if len(label) >= 4 and label[2] == "-" and label[3] == "-":
        return False
    cps = [ord(c) for c in label]
    if _MARK_START_RE.match(label):
        return False
    if any(0x660 <= c <= 0x669 for c in cps) and any(0x6F0 <= c <= 0x6F9 for c in cps):
        return False
    for i, c in enumerate(cps):
        if c in (0x302E, 0x302F, 0x0640, 0x07FA, 0x3031, 0x3032, 0x3033, 0x3034, 0x3035, 0x303B):
            return False
        if c == 0x00B7:  # MIDDLE DOT: between two 'l'
            if not (0 < i < len(cps) - 1 and cps[i - 1] == 0x6C and cps[i + 1] == 0x6C):
                return False
        elif c == 0x0375:  # GREEK KERAIA: followed by Greek
            if not (i < len(cps) - 1 and _GREEK_RE.match(chr(cps[i + 1]))):
                return False
        elif c in (0x05F3, 0x05F4):  # HEBREW GERESH / GERSHAYIM: preceded by Hebrew
            if not (i > 0 and _HEBREW_RE.match(chr(cps[i - 1]))):
                return False
        elif c == 0x30FB:  # KATAKANA MIDDLE DOT: label contains Hiragana, Katakana or Han
            if not any(d != 0x30FB and _KANA_HAN_RE.match(chr(d)) for d in cps):
                return False
        elif c == 0x200D:  # ZERO WIDTH JOINER: preceded by virama
            if not (i > 0 and cps[i - 1] in _VIRAMAS):
                return False
        elif c == 0x200C:  # ZERO WIDTH NON-JOINER: preceded by virama (the joining-type rule is approximated)
            if not (i > 0 and cps[i - 1] in _VIRAMAS) and not _zwnj_joining_context(cps, i):
                return False
    return True


def _zwnj_joining_context(cps: list[int], i: int) -> bool:
    # (Joining_Type:{L,D})(Joining_Type:T)*\u200c(Joining_Type:T)*(Joining_Type:{R,D}) approximated with Arabic letters.
    def is_joiner(c: int) -> bool:
        return 0x0620 <= c <= 0x064A or 0x066E <= c <= 0x06D3

    left = i - 1
    while left >= 0 and _MN_RE.match(chr(cps[left])):
        left -= 1
    right = i + 1
    while right < len(cps) and _MN_RE.match(chr(cps[right])):
        right += 1
    return left >= 0 and right < len(cps) and is_joiner(cps[left]) and is_joiner(cps[right])


_LABEL_SEPARATORS_RE = _Lazy("[.\u3002\uff0e\uff61]")
_IDN_DISALLOWED_RE = _RX(r"[\p{Cc}\p{Cf}\p{Zs}\p{Cn}]")


def idn_hostname(s: str) -> bool:
    if len(s) == 0:
        return False
    # Label separators: full stop, ideographic full stop, fullwidth full stop, halfwidth ideographic full stop.
    labels = _LABEL_SEPARATORS_RE.split(s)
    unicode_labels = [(_punycode_decode(label[4:]) or label) if _is_a_label(label) else label for label in labels]
    is_bidi_domain = any(map(_bidi_domain, unicode_labels))
    ascii_length = 0
    for i, label in enumerate(labels):
        if len(label) == 0:
            return False
        if label.isascii():
            if not _hostname_label_ok(label):
                return False
            if _is_a_label(label) and not _punycode_label_ok(label[4:]):
                return False
            if not _bidi_label_ok(unicode_labels[i], is_bidi_domain):
                return False
            ascii_length += len(label) + 1
        else:
            if not _idn_label_ok(label):
                return False
            without_joiners = label.replace("\u200c", "").replace("\u200d", "")
            if _IDN_DISALLOWED_RE.search(without_joiners) or _DISALLOWED_RE.search(
                _DISALLOWED_EXCEPTIONS_RE.sub("", without_joiners)
            ):
                return False
            if not _bidi_label_ok(label, is_bidi_domain):
                return False
            a_label = "xn--" + _punycode_encode(label)
            if len(a_label) > 63:
                return False
            ascii_length += len(a_label) + 1
    return ascii_length - 1 <= 253


_EMAIL_LOCAL_RE = _Lazy(
    r"""^(?:[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*|"(?:[^"\\\r\n]|\\.)*")\Z"""
)
_IDN_EMAIL_LOCAL_RE = _RX(
    r"""^(?:[\p{L}\p{M}\p{N}!#$%&'*+/=?^_`{|}~-]+(?:\.[\p{L}\p{M}\p{N}!#$%&'*+/=?^_`{|}~-]+)*|"(?:[^"\\\r\n]|\\.)*")\Z"""
)


def _email_domain(domain: str, idn: bool) -> bool:
    if domain.startswith("[") and domain.endswith("]"):
        inner = domain[1:-1]
        if inner[:5].lower() == "ipv6:":
            return ipv6(inner[5:])
        return ipv4(inner)
    return idn_hostname(domain) if idn else hostname(domain)


def email(s: str) -> bool:
    at = s.rfind("@")
    if at <= 0:
        return False
    return _EMAIL_LOCAL_RE.match(s[:at]) is not None and _email_domain(s[at + 1 :], False)


def idn_email(s: str) -> bool:
    at = s.rfind("@")
    if at <= 0:
        return False
    return _IDN_EMAIL_LOCAL_RE.match(s[:at]) is not None and _email_domain(s[at + 1 :], True)


# RFC 3986 (URI) and RFC 3987 (IRI) grammars.
_HEX = "[0-9A-Fa-f]"
_PCT = f"%{_HEX}{{2}}"
_SUB = "[!$&'()*+,;=]"
_UNRESERVED = "[A-Za-z0-9\\-._~]"
_UCSCHAR = "[\\u00a0-\\ud7ff\\uf900-\\ufdcf\\ufdf0-\\uffef\\U00010000-\\U000efffd]"


def _uri_regex(iri: bool, reference: bool) -> _Lazy:
    unreserved = f"(?:{_UNRESERVED}|{_UCSCHAR})" if iri else _UNRESERVED
    pchar = f"(?:{unreserved}|{_PCT}|{_SUB}|[:@])"
    query = (
        f"(?:{pchar}|[/?]|[\\ue000-\\uf8ff\\U000f0000-\\U000ffffd\\U00100000-\\U0010fffd])*"
        if iri
        else f"(?:{pchar}|[/?])*"
    )
    fragment = f"(?:{pchar}|[/?])*"
    dec_octet = "(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])"
    ip4 = f"{dec_octet}(?:\\.{dec_octet}){{3}}"
    h16 = f"{_HEX}{{1,4}}"
    ls32 = f"(?:{h16}:{h16}|{ip4})"
    ip6 = (
        f"(?:(?:{h16}:){{6}}{ls32}|::(?:{h16}:){{5}}{ls32}|(?:{h16})?::(?:{h16}:){{4}}{ls32}"
        f"|(?:(?:{h16}:){{0,1}}{h16})?::(?:{h16}:){{3}}{ls32}"
        f"|(?:(?:{h16}:){{0,2}}{h16})?::(?:{h16}:){{2}}{ls32}|(?:(?:{h16}:){{0,3}}{h16})?::{h16}:{ls32}"
        f"|(?:(?:{h16}:){{0,4}}{h16})?::{ls32}"
        f"|(?:(?:{h16}:){{0,5}}{h16})?::{h16}|(?:(?:{h16}:){{0,6}}{h16})?::)"
    )
    ip_literal = f"\\[(?:{ip6}|v{_HEX}+\\.(?:{_UNRESERVED}|{_SUB}|:)+)\\]"
    reg_name = f"(?:{unreserved}|{_PCT}|{_SUB})*"
    authority = f"(?:(?:{unreserved}|{_PCT}|{_SUB}|:)*@)?(?:{ip_literal}|{ip4}|{reg_name})(?::[0-9]*)?"
    segment = f"{pchar}*"
    segment_nz = f"{pchar}+"
    segment_nz_nc = f"(?:{unreserved}|{_PCT}|{_SUB}|@)+"
    hier_part = f"(?://{authority}(?:/{segment})*|/(?:{segment_nz}(?:/{segment})*)?|{segment_nz}(?:/{segment})*|)"
    relative_part = (
        f"(?://{authority}(?:/{segment})*|/(?:{segment_nz}(?:/{segment})*)?|{segment_nz_nc}(?:/{segment})*|)"
    )
    scheme = "[A-Za-z][A-Za-z0-9+\\-.]*"
    absolute = f"{scheme}:{hier_part}(?:\\?{query})?(?:#{fragment})?"
    relative = f"{relative_part}(?:\\?{query})?(?:#{fragment})?"
    return _Lazy(f"^(?:{f'{absolute}|{relative}' if reference else absolute})\\Z")


_URI_RE = _uri_regex(False, False)
_URI_REF_RE = _uri_regex(False, True)
_IRI_RE = _uri_regex(True, False)
_IRI_REF_RE = _uri_regex(True, True)


def uri(s: str) -> bool:
    return _URI_RE.match(s) is not None


def uri_reference(s: str) -> bool:
    return _URI_REF_RE.match(s) is not None


def iri(s: str) -> bool:
    return _IRI_RE.match(s) is not None


def iri_reference(s: str) -> bool:
    return _IRI_REF_RE.match(s) is not None


_URI_TEMPLATE_RE = _Lazy(
    r"^(?:[^\x00-\x20\"'<>\\^`{|}]|\{[+#./;?&=,!@|]?(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2})(?:\.?(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2}))*"
    r"(?::[1-9][0-9]{0,3}|\*)?(?:,(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2})(?:\.?(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2}))*"
    r"(?::[1-9][0-9]{0,3}|\*)?)*\})*\Z"
)


def uri_template(s: str) -> bool:
    return _URI_TEMPLATE_RE.match(s) is not None


_JSON_POINTER_RE = _Lazy(r"^(?:/(?:[^~/]|~[01])*)*\Z")
_RELATIVE_JSON_POINTER_RE = _Lazy(r"^(?:0|[1-9][0-9]*)(?:#|(?:/(?:[^~/]|~[01])*)*)\Z")


def json_pointer(s: str) -> bool:
    return _JSON_POINTER_RE.match(s) is not None


def relative_json_pointer(s: str) -> bool:
    return _RELATIVE_JSON_POINTER_RE.match(s) is not None


def regex_format(s: str) -> bool:
    return is_valid_ecma_regex(s)


FORMAT_VALIDATORS: dict[str, Callable[[str], bool]] = {
    "date": date,
    "time": time,
    "date-time": date_time,
    "duration": duration,
    "uuid": uuid,
    "ipv4": ipv4,
    "ipv6": ipv6,
    "hostname": hostname,
    "idn-hostname": idn_hostname,
    "email": email,
    "idn-email": idn_email,
    "uri": uri,
    "uri-reference": uri_reference,
    "iri": iri,
    "iri-reference": iri_reference,
    "uri-template": uri_template,
    "json-pointer": json_pointer,
    "relative-json-pointer": relative_json_pointer,
    "regex": regex_format,
}
"""The built-in format assertions, by format name."""


# Numeric formats (a Corvus extension): integers of a given width, and floating-point/decimal magnitudes.
def _integer_range(lo: int, hi: int) -> Callable[[int | float], bool]:
    def check(x: int | float) -> bool:
        if type(x) is float:
            if not x.is_integer():
                return False
            x = int(x)
        return lo <= x <= hi

    return check


def _magnitude(limit: float) -> Callable[[int | float], bool]:
    def check(x: int | float) -> bool:
        try:
            return abs(float(x)) <= limit
        except OverflowError:
            return False

    return check


NUMERIC_FORMAT_VALIDATORS: dict[str, Callable[[int | float], bool]] = {
    "byte": _integer_range(0, 255),
    "uint16": _integer_range(0, 65535),
    "uint32": _integer_range(0, 4294967295),
    "uint64": _integer_range(0, 2**64 - 1),
    "uint128": _integer_range(0, 2**128 - 1),
    "sbyte": _integer_range(-128, 127),
    "int16": _integer_range(-32768, 32767),
    "int32": _integer_range(-2147483648, 2147483647),
    "int64": _integer_range(-(2**63), 2**63 - 1),
    "int128": _integer_range(-(2**127), 2**127 - 1),
    "half": _magnitude(65504),
    "single": _magnitude(3.40282346638528859e38),
    "double": _magnitude(1.7976931348623157e308),
    "decimal": _magnitude(79228162514264337593543950335),
}
"""Numeric format assertions, applied to numbers when ``format`` is asserted."""

_SINCE = {
    "uri-reference": 1,
    "uri-template": 1,
    "json-pointer": 1,
    "date": 2,
    "time": 2,
    "regex": 2,
    "relative-json-pointer": 2,
    "idn-email": 2,
    "idn-hostname": 2,
    "iri": 2,
    "iri-reference": 2,
    "duration": 3,
    "uuid": 3,
}
_ALWAYS = frozenset(
    (
        "byte", "uint16", "uint32", "uint64", "uint128", "sbyte", "int16", "int32", "int64", "int128", "half", "single",
        "double", "decimal", "date-time", "email", "hostname", "ipv4", "ipv6", "uri",
    )
)  # fmt: skip


def format_kind(fmt: str, dialect: int) -> str:
    """The format a dialect recognises for a ``format`` value (SchemaCompiler.GetFormatKind): its canonical name, or
    'unknown' for names the dialect does not define (which always match)."""
    if fmt == "float":
        return "single"
    if fmt in _ALWAYS:
        return fmt
    since = _SINCE.get(fmt)
    return fmt if since is not None and dialect >= since else "unknown"


def is_numeric_format(kind: str) -> bool:
    return kind in NUMERIC_FORMAT_VALIDATORS
