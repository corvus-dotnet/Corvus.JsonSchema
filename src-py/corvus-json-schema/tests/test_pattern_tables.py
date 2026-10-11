"""Tables of V8's answers (V8 13.6, in Node 24.11.0) for the classes of pattern that are written out for Python's
engines. Each row has a pattern, the name of a list of texts, and V8's answer for each text of the list as the bits
of a hexadecimal number, the first text in the highest bit. A pattern in a case-insensitive group has V8's answers
for the same pattern with the i flag. A pattern with ``\\-`` outside a class is valid only without the u flag."""

from __future__ import annotations

from itertools import product

import pytest

import corvus_json_schema as cjs
from corvus_json_schema import pattern as pattern_module
from corvus_json_schema.pattern import translate


def _words(alphabet: str, longest: int) -> list[str]:
    return ["".join(w) for n in range(longest + 1) for w in product(alphabet, repeat=n)]


def _check(pattern: str, texts: list[str], answers: str) -> None:
    expected = format(int(answers, 16), f"0{len(texts)}b")
    # The pattern, and the pattern after an empty lookbehind, which sends it to the regex package.
    for p in (pattern, "(?<=)" + pattern):
        validate = cjs.compile({"pattern": p})
        got = "".join("1" if validate(text) else "0" for text in texts)
        wrong = [text for text, e, g in zip(texts, expected, got, strict=True) if e != g]
        assert not wrong, (p, wrong)


_LETTERS = "iI\u0130\u0131sS\u017fkK\u212a\u00df\u1e9e\u01c5\u01c4\u1f80\u1f88a-\U00010400\U00010428"
_CASE_TEXTS = {
    "PAIRS": [a + b for a in _LETTERS for b in _LETTERS],
    "BMP": [a + b for a in _LETTERS[:-2] for b in _LETTERS[:-2]],
    "QUADS": [
        ab + c + d
        for ab in ("ik", "\u0130s", "\u017f\u212a", "\u01c5a")
        for c in "iI\u0130\u0131sS\u017f\u01c4"
        for d in "kK\u212asa"
    ],
}

# A reference inside a case-insensitive group compares by Canonicalize: simple case folding with the u flag (i is
# not U+0130 or U+0131), and the uppercase form of a UTF-16 code unit without it (s is not U+017F, k is not U+212A).
_CASE_INSENSITIVE_REFERENCES: list[tuple[str, str, str]] = [
    (
        "(?i:^(.)\\1$)",
        "PAIRS",
        "c0000c000020000100000e0000e0000e00001c0001c0001c000030000300000c0000c0000300003000008000040000300003",
    ),
    (
        "(?i:^(?<n>[a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28])\\k<n>$)",
        "PAIRS",
        "c0000c000020000100000e0000e0000e00001c0001c0001c000030000300000c0000c0000300003000008000000000300003",
    ),
    (
        "(?i:^([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]{2})\\1$)",
        "QUADS",
        "e700000000000400000000000e73800000000001",
    ),
    (
        "(?i:^([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]+)\\1$)",
        "QUADS",
        "e700000000000400000000000e73800000000001",
    ),
    (
        "(?i:^(?:([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28])\\1)+$)",
        "QUADS",
        "0",
    ),
    (
        "(?i:^..(?<=(.)\\1)$)",
        "PAIRS",
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
    ),
    (
        "(?i:^..(?<=\\1([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]))$)",
        "PAIRS",
        "c0000c000020000100000e0000e0000e00001c0001c0001c000030000300000c0000c0000300003000008000000000300003",
    ),
    (
        "(?i:^....(?<=([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]{2})\\1)$)",
        "QUADS",
        "ffffffffffffffffffffffffffffffffffffffff",
    ),
    (
        "(?i:^....(?<=\\1([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]{2}))$)",
        "QUADS",
        "e700000000000400000000000e73800000000001",
    ),
    (
        "(?i:^(?=.([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]))\\1)",
        "PAIRS",
        "c0000c000020000100000e0000e0000e00001c0001c0001c000030000300000c0000c0000300003000008000000000300003",
    ),
    (
        "(?i:^(?=..([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]{2}))\\1)",
        "QUADS",
        "e700000000000400000000000e73800000000001",
    ),
    ("(?i:^([a-z]+)\\1$)", "QUADS", "e700000000000000000000000e73800000000000"),
    (
        "(?i:^(\\w)\\1$)",
        "PAIRS",
        "c0000c000000000000000e0000e0000e00001c0001c0001c0000000000000000000000000000000000008000000000000000",
    ),
    ("(?i:^([\\d-])\\1$)", "PAIRS", "40000000000"),
    (
        "(?i:^([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28])?\\1.$)",
        "PAIRS",
        "0",
    ),
    (
        "(?i:^(?:([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28])|.)\\1)",
        "PAIRS",
        "ffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
    ),
    (
        "(?i:^(?:([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28])\\1|..)*$)",
        "QUADS",
        "ffffffffffffffffffffffffffffffffffffffff",
    ),
    (
        "(?i:(s)(.)((?<=\\1)|-)+$)",
        "PAIRS",
        "e0000e0000e00000000000000000000000000000000000000000000000000000000000000000000",
    ),
    ("(?i:^(a-)?(\\1?\\1?\\S)*$)", "QUADS", "ffffffffffffffffffffffffffffffffffffffff"),
    ("(?i:^((.[^-])*\\2)*[a-z](?:(?<=\\2{2})|([^-].))*$)", "QUADS", "0"),
    ("(?i:\\-{0}^(.)\\1$)", "BMP", "c000300002000040000c0003000020000600018000100002000040000c00030000200004000080001"),
    (
        "(?i:\\-{0}^(?<n>[a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28])\\k<n>$)",
        "BMP",
        "c000300002000040000c0003000020000600018000100002000040000c00030000200004000080000",
    ),
    (
        "(?i:\\-{0}^([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]{2})\\1$)",
        "QUADS",
        "c600000000000400000000000000800000000001",
    ),
    (
        "(?i:\\-{0}^([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]+)\\1$)",
        "QUADS",
        "c600000000000400000000000000800000000001",
    ),
    (
        "(?i:\\-{0}^(?:([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28])\\1)+$)",
        "QUADS",
        "0",
    ),
    (
        "(?i:\\-{0}^..(?<=(.)\\1)$)",
        "BMP",
        "fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
    ),
    (
        "(?i:\\-{0}^..(?<=\\1([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]))$)",
        "BMP",
        "c000300002000040000c0003000020000600018000100002000040000c00030000200004000080000",
    ),
    (
        "(?i:\\-{0}^....(?<=([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]{2})\\1)$)",
        "QUADS",
        "ffffffffffffffffffffffffffffffffffffffff",
    ),
    (
        "(?i:\\-{0}^....(?<=\\1([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]{2}))$)",
        "QUADS",
        "c600000000000400000000000000800000000001",
    ),
    (
        "(?i:\\-{0}^(?=.([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]))\\1)",
        "BMP",
        "c000300002000040000c0003000020000600018000100002000040000c00030000200004000080000",
    ),
    (
        "(?i:\\-{0}^(?=..([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]{2}))\\1)",
        "QUADS",
        "c600000000000400000000000000800000000001",
    ),
    ("(?i:\\-{0}^([a-z]+)\\1$)", "QUADS", "c600000000000000000000000000000000000000"),
    (
        "(?i:\\-{0}^(\\w)\\1$)",
        "BMP",
        "c000300000000000000c0003000000000600018000000000000000000000000000000000000080000",
    ),
    ("(?i:\\-{0}^([\\d-])\\1$)", "BMP", "1"),
    (
        "(?i:\\-{0}^([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28])?\\1.$)",
        "BMP",
        "0",
    ),
    (
        "(?i:\\-{0}^(?:([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28])|.)\\1)",
        "BMP",
        "fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff",
    ),
    (
        "(?i:\\-{0}^(?:([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28])\\1|..)*$)",
        "QUADS",
        "ffffffffffffffffffffffffffffffffffffffff",
    ),
    ("(?i:\\-{0}(s)(.)((?<=\\1)|-)+$)", "BMP", "c0003000000000000000000000000000000000000000000000000000000000"),
    ("(?i:\\-{0}^(a-)?(\\1?\\1?\\S)*$)", "QUADS", "ffffffffffffffffffffffffffffffffffffffff"),
    ("(?i:\\-{0}^((.[^-])*\\2)*[a-z](?:(?<=\\2{2})|([^-].))*$)", "QUADS", "0"),
    (
        "^(.)(?i:\\1)$",
        "PAIRS",
        "c0000c000020000100000e0000e0000e00001c0001c0001c000030000300000c0000c0000300003000008000040000300003",
    ),
    (
        "^(?i:([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]))\\1$",
        "PAIRS",
        "8000040000200001000008000040000200001000008000040000200001000008000040000200001000008000000000200001",
    ),
    (
        "^(?i:([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28])(?-i:\\1))$",
        "PAIRS",
        "8000040000200001000008000040000200001000008000040000200001000008000040000200001000008000000000200001",
    ),
    (
        "^([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]{2})(?i:\\1)$",
        "QUADS",
        "e700000000000400000000000e73800000000001",
    ),
    ("\\-{0}^(.)(?i:\\1)$", "BMP", "c000300002000040000c0003000020000600018000100002000040000c00030000200004000080001"),
    (
        "\\-{0}^(?i:([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]))\\1$",
        "BMP",
        "800010000200004000080001000020000400008000100002000040000800010000200004000080000",
    ),
    (
        "\\-{0}^(?i:([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28])(?-i:\\1))$",
        "BMP",
        "800010000200004000080001000020000400008000100002000040000800010000200004000080000",
    ),
    (
        "\\-{0}^([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]{2})(?i:\\1)$",
        "QUADS",
        "c600000000000400000000000000800000000001",
    ),
]


@pytest.mark.parametrize(("pattern", "texts", "answers"), _CASE_INSENSITIVE_REFERENCES)
def test_a_case_insensitive_reference_compares_by_canonicalize(pattern: str, texts: str, answers: str) -> None:
    _check(pattern, _CASE_TEXTS[texts], answers)


def test_a_case_insensitive_reference_is_written_out_where_its_group_can_hold_a_letter() -> None:
    # A group of digits can hold no character that has another case. Its reference stays a reference, and the
    # standard library's engine runs the pattern.
    assert translate("^(?i:(\\d+)-\\1)$") == ("^(?:(?P<g1>(?:[0-9])+)\\-(?P=g1))\\Z", False)
    # Any other is compared by a group that is written once, with the sets of the letters the group can hold, and
    # called from each reference. Neither engine's own case-insensitive comparison is used.
    text, for_regex = translate("^(?i:(?:([a-c])\\1){3})$") or ("", False)
    assert for_regex and "(?i" not in text
    assert text.count("(?P<f1>") == 1 and text.count("(?&f1)") == 3
    assert text.count("(?P<s1>") == 1 and "[Aa]" in text and "[Dd]" not in text
    # In a lookbehind the comparison is written where the reference is.
    text, for_regex = translate("^..(?i:(?<=\\1([a-c])))$") or ("", False)
    assert for_regex and "(?&f1)" not in text and "(?&s1)" in text


# A lookaround is left by the first way through it, and its groups keep what that way gave them. ECMA-262 refuses
# an iteration that matches nothing once the minimum count is met, which changes which way is first.

_EMPTY_ITERATIONS_IN_LOOKAROUNDS: list[tuple[str, str, str]] = [
    ("(?:(?=()((|(.))*))(\\2)a)", "ABC", "0"),
    ("(?:(?=b(?:((?:c){0,}?))*(([c]))?)[b]\\2)$", "ABC", "492492492492492492492492492492"),
    ("((?=((?:[^]{0,}?)?)((b)|)(b))(.b))\\3", "ABC", "925d2e974baffae975ff5d2ebfeba"),
    ("^(?=(()()())((c{0,}?)?))\\5$", "ABC", "1200000000000000000000000000000"),
    ("(?=(()((())(()))(()(())))(?=([^]{0,}?)+((|[^])a))b)\\13", "ABC", "0"),
    ("^(?=((?:|a)*))\\1b", "ABC", "4b85c7fc00b8ff803ffffff8000000"),
    ("^(?=((?:a??)*))\\1b", "ABC", "4b85c7fc00b8ff803ffffff8000000"),
    ("^(?=(?:a*?)*(a*))\\1$", "ABC", "1000000000000000000000000000000"),
    ("^(?=((?:a|b??)+))\\1c", "ABC", "24f2793ffe4f27ffc9e4ffffffffff"),
    ("^(?=((?:|a|ab)*)).\\1c", "ABC", "90001c0e00000000000ff80001ff"),
    ("^(?=((?:a??){2,3}))\\1b", "ABC", "4b81c7fc0000ff803ffffff8000000"),
    ("^(?=((?:a??){0,2}))\\1b", "ABC", "4b85c7fc0038ff803ffffff8000000"),
    ("^(?=((?:|a){1,}?)b?)\\1b", "ABC", "438007fc000000003ffffff8000000"),
    ("(?<=((?:|a)*))b\\1", "ABC", "43a197fc7412fd0ebffffff8cbfe3a"),
    ("(?<=((?:a??)*)c)b\\1", "ABC", "20000874000108800021d003fe3a"),
    ("^.*(?<=((?:a??b??)*))c\\1$", "ABC", "2010280c0201008040201008140601"),
    ("(?<=(?:c??)*(a|b))c\\1", "ABC", "200800040170008021c0100400"),
    ("^(?:(?=((?:|a)*))\\1b)+$", "ABC", "490482400090480012090000000000"),
    ("^(?!(?=((?:|a)*))\\1b)", "ABC", "1b47a3803ff47007fc0000007ffffff"),
    ("(?=(?=((?:|a)*)).)\\1b", "ABC", "4ba5d7fd74baffaebffffffaebfeba"),
    ("^(?=((?:|a)*)\\1b)", "ABC", "438407fc003800003ffffff8000000"),
]
# A group that a lookahead sets on only some ways to match, a lookaround group with its reference in one repetition
# and a choice between them, a group of a lookbehind that is matched backwards, and a reference inside a
# lookbehind to a group of it.

_GROUPS_OF_LOOKAROUNDS: list[tuple[str, str, str]] = [
    ("(?=(a)?b)\\1", "ABC", "4ba5d7fd74baffaebffffffaebfeba"),
    ("^(?=(a)?b)\\1b$", "ABC", "480000000000000000000000000000"),
    ("^(?=(a)|b)\\1.$", "ABC", "5c0000000000000000000000000000"),
    ("^(?=(?:(a)|b)(?:(c)|a))\\1\\2", "ABC", "160e3f0001ff007fffe00000000000"),
    ("^(?:(?=(a)?b)\\1.)+$", "ABC", "490082400000480002090000000000"),
    ("^(?:(?=([ab]))[ab](?:c|ca)\\1)*$", "ABC", "1000020080000004000000400000000"),
    ("^(?:(?=([ab]))[ab](?:c\\1|ca\\1))*$", "ABC", "1000020080000004000000400000000"),
    ("^(?:(?=(.))(?:a|ab)\\1)+$", "ABC", "100100000100000000000000000000"),
    ("^(?:(?<=(.))(?:a|ab)\\1)+$", "ABC", "0"),
    ("^(?:(?=([ab]))[ab]c*?\\1)*$", "ABC", "1110020080110000100110010000000"),
    ("(?<=(a+))b\\1", "ABC", "100000000e00004000000800000"),
    ("(?<=([ab]+)([bc]+))c\\1", "ABC", "20100002010000000"),
    ("(?<=(a+)(b*))c\\2\\1", "ABC", "20000000007000800000100000"),
    ("(?<=\\1(a))b", "ABC", "4000000b8000010000002000000"),
    ("(?<=\\1(a+))b", "ABC", "4000000b8000010000002000000"),
    ("(?<=(a+)\\1)b", "ABC", "805c20100b8ffa017080402e10080"),
    ("(?<=\\1b*(a+))c", "ABC", "200000047200008000001000000"),
    ("(?<=(a|ab)(b*))c\\1", "ABC", "20000004027000800000100000"),
    ("^.*(?<=(?:(a)|b)+)c\\1$", "ABC", "8021201004020209048040109008"),
    ("(?<=(\\w+?)(\\w*))c\\1", "ABC", "200802040271004021d0040207"),
    ("(?<=(a*)\\1b)c", "ABC", "80403c100807820100fff8201e08"),
]


@pytest.mark.parametrize(("pattern", "texts", "answers"), _EMPTY_ITERATIONS_IN_LOOKAROUNDS + _GROUPS_OF_LOOKAROUNDS)
def test_groups_of_lookarounds_hold_what_ecma_262_gives_them(pattern: str, texts: str, answers: str) -> None:
    _check(pattern, _words("abc", 4), answers)


def test_a_repetition_in_a_lookaround_goes_to_the_regex_package_when_a_group_of_it_is_read_outside() -> None:
    assert (translate("^(?=((?:|a)*))\\1$") or ("", False))[1]
    # No iteration here can match nothing, and here no group is read outside the lookahead.
    assert not (translate("^(?=(a*))\\1$") or ("", True))[1]
    assert not (translate("^(?=((?:|a)*)\\1)") or ("", True))[1]


_A, _B = "\U00010400", "\U00010428"
_H, _L, _L2 = chr(0xD801), chr(0xDC00), chr(0xDC28)
# fmt: off
_UNIT_TEXTS = [
    "", "a", "x", _A, _B, _A + _A, _A + _B, _H, _L, _L2, _H + "a", _L + "a", _A + _L, _A + _L + _L, _H + _A, _A + "a",
    "a" + _A, "a" + _A + "a", _A + _L2, _H + _H + _L, _B + _B, "a" + _H, "a" + _L, _H + _L2 + _L2, "aa", _A + "x",
    "x" + _B,
]
# fmt: on

# A pattern that is valid only without the u flag is read, and matches a text, by UTF-16 code unit: a dot takes half
# of a character beyond the Basic Multilingual Plane, and a quantifier after one repeats its second half.
_CODE_UNITS: list[tuple[str, str, str]] = [
    ("^\\-?\U00010400*$", "UNITS", "886000"),
    ("^\\-?\U00010400+$", "UNITS", "806000"),
    ("^\\-?\U00010400?x$", "UNITS", "2"),
    ("^\\-?[\U00010400]$", "UNITS", "c0000"),
    ("^\\-?[\U00010400]{2}$", "UNITS", "800000"),
    ("^\\-?.$", "UNITS", "30e0000"),
    ("^\\-?..$", "UNITS", "c18034"),
    ("^\\-?[^a]$", "UNITS", "10e0000"),
    ("^\\-?[^a]{2}$", "UNITS", "c00000"),
    ("^\\-?\\W$", "UNITS", "e0000"),
    ("^\\-?\\S\\S$", "UNITS", "c18034"),
    ("^\\-?\\D{3}$", "UNITS", "5d8b"),
    ("^\\-?\U00010400{2}$", "UNITS", "4000"),
    ("^\\-?(?:\U00010400){2}$", "UNITS", "200000"),
    ("^\\-?(\U00010400)\\1$", "UNITS", "200000"),
    ("^\\-?(.)\\1$", "UNITS", "4"),
    ("^\\-?(?i:\U00010400)$", "UNITS", "800000"),
    ("^\\-?(?i:[\U00010400])$", "UNITS", "c0000"),
    ("^\\-?[a-\U00010400]+$", "UNITS", "3adfeb6"),
    ("^\\-?[\U00010400-\\udfff]$", "UNITS", "e0000"),
    ("\\-?(?<=\U00010400)a", "UNITS", "a00"),
    ("\\-?(?<!\\ud801)\\udc00", "UNITS", "4e010"),
    ("^\\-?\\ud801\\udc00$", "UNITS", "800000"),
    ("^\\-?\\ud801$", "UNITS", "80000"),
    ("^\\-?[\\ud801]\\udc00$", "UNITS", "800000"),
    ("^\\-?\\ud801+\\udc00$", "UNITS", "801080"),
    ("^\\-?\\u{2}$", "UNITS", "0"),
    ("\\-?\U00010400\\b", "UNITS", "a02"),
    ("\\-?\\B\U00010400", "UNITS", "b07982"),
    ("^\\-?.\\B.$", "UNITS", "c00004"),
    ("\\-?a|\U00010400", "UNITS", "2b1ffb6"),
    ("^\\-?(?<n>\U00010400)\\k<n>$", "UNITS", "200000"),
    ("^\\-?(?<\U00010400>.)\\k<\U00010400>$", "UNITS", "4"),
    ("^\\-?[^\U00010400]$", "UNITS", "3020000"),
    ("^\\-?(?:.|\U00010400)$", "UNITS", "38e0000"),
    ("\\-?^.{2}(?!.)", "UNITS", "c18034"),
    ("^\\-?\\x{1}\U00010400$", "UNITS", "0"),
    ("^\\-?[\\s\\S]$", "UNITS", "30e0000"),
    ("^\\-?[\\w-\U00010400]{2}$", "UNITS", "818034"),
    ("\\-?(?=\U00010400)..$", "UNITS", "a01480"),
    ("^\\-?a?$", "UNITS", "6000000"),
    ("^[\\w\\-]+\\-?$", "UNITS", "3000004"),
    ("^\\-?[a-z]*$", "UNITS", "7000004"),
    ("\\B\\&?", "UNITS", "4ffffff"),
    ("\\-?(?!a)(?<!a)", "UNITS", "5fffffb"),
    ("\\-?(?<=a)(?=a)", "UNITS", "4"),
    ("(?m:\\-?^$)", "UNITS", "4000000"),
    ("\\-?a\\Ba", "UNITS", "4"),
    ("\\-?\\b\\b$", "UNITS", "3018a06"),
    ("^\\-?a(?=.a)", "UNITS", "0"),
    ("^\\-?a(?=..a)", "UNITS", "200"),
    (
        "(?i:\\-{0}^(.)\\1$)",
        "PAIRS",
        "c0000c000020000100000c0000c000020000180001800004000020000100000c0000c0000200001000008000040000000000",
    ),
    (
        "(?i:\\-{0}^..(?<=(.)\\1)$)",
        "PAIRS",
        "ffffcffffcffffcffffcffffcffffcffffcffffcffffcffffcffffcffffcffffcffffcffffcffffcffffcffffc0000000000",
    ),
    (
        "\\-{0}^(.)(?i:\\1)$",
        "PAIRS",
        "c0000c000020000100000c0000c000020000180001800004000020000100000c0000c0000200001000008000040000000000",
    ),
    (
        "(?i:\\-{0}^(?=.([a-z\\u0130\\u0131\\u017f\\u212a\\u00df\\u1e9e\\u01c4-\\u01c6\\u1f80\\u1f88\\ud801\\udc00\\udc28]))\\1)",
        "PAIRS",
        "c0000c000020000100000c0000c000020000180001800004000020000100000c0000c0000200001000008000000000000000",
    ),
]


@pytest.mark.parametrize(("pattern", "texts", "answers"), _CODE_UNITS)
def test_a_pattern_without_the_u_flag_matches_by_code_unit(pattern: str, texts: str, answers: str) -> None:
    _check(pattern, _CASE_TEXTS.get(texts, _UNIT_TEXTS), answers)


def test_a_text_is_split_into_code_units_only_for_a_pattern_that_can_tell() -> None:
    def split(pattern: str) -> bool:
        assert translate(pattern) is None, pattern
        t = pattern_module._translate(pattern, False, False)
        assert t is not None, pattern
        return t[2]

    # Nothing here takes a character from U+D800 up, and there is no lookaround and no \\B.
    for pattern in ("^[\\w\\-\\.]+\\&?$", "^\\-?[a-z]*$", "\\-?\\ba\\b", "(?m:\\-?^a$)"):
        assert not split(pattern), pattern
    for pattern in ("^\\-?.$", "^\\-?[^a]$", "^\\-?\\W$", "\\B\\&?", "\\-?(?!a)", "^\\-?\\ud801$"):
        assert split(pattern), pattern
    # The pattern is read by code unit too: a quantifier after a character beyond the Basic Multilingual Plane
    # repeats its second surrogate.
    assert translate("^\\-?\U00010400*$", False) == ("^(?:\\-)?\\ud801(?:\\udc00)*\\Z", False)


_PROPERTY_TEXTS = ["", "a", "B", "1", "-", "\u00e9", "\u03a3", "ab", "aB", "a1", "Ba", "1-", "\U00010400"]

# A negated class with a property and a negated property. The regex package reads a property and the same property
# negated as every character and drops the ^ before it.
_CLASSES_WITH_A_PROPERTY_BOTH_WAYS: list[tuple[str, str, str]] = [
    ("[^\\p{L}\\P{L}]", "PROPERTY", "0"),
    ("(?i:[^\\p{L}\\P{L}])", "PROPERTY", "0"),
    ("(?<=a)[^\\p{L}\\P{L}]", "PROPERTY", "0"),
    ("(?i:(?<=a)[^\\p{L}\\P{L}])", "PROPERTY", "0"),
    ("[^\\p{L}\\P{L}a]", "PROPERTY", "0"),
    ("(?i:[^\\p{L}\\P{L}a])", "PROPERTY", "0"),
    ("[^\\P{L}\\p{L}]", "PROPERTY", "0"),
    ("(?i:[^\\P{L}\\p{L}])", "PROPERTY", "0"),
    ("[^\\p{Lu}\\P{Lu}]", "PROPERTY", "0"),
    ("(?i:[^\\p{Lu}\\P{Lu}])", "PROPERTY", "0"),
    ("^[^\\p{Lu}\\P{Ll}]$", "PROPERTY", "880"),
    ("(?i:^[^\\p{Lu}\\P{Ll}]$)", "PROPERTY", "0"),
    ("^[^\\P{Lu}\\p{Ll}]+$", "PROPERTY", "441"),
    ("(?i:^[^\\P{Lu}\\p{Ll}]+$)", "PROPERTY", "0"),
    ("^[\\p{L}\\P{L}]$", "PROPERTY", "fc1"),
    ("(?i:^[\\p{L}\\P{L}]$)", "PROPERTY", "fc1"),
    ("^[^\\p{L}\\P{Letter}]?$", "PROPERTY", "1000"),
    ("(?i:^[^\\p{L}\\P{Letter}]?$)", "PROPERTY", "1000"),
    ("^[^\\p{L}\\p{N}]$", "PROPERTY", "100"),
    ("(?i:^[^\\p{L}\\p{N}]$)", "PROPERTY", "100"),
    ("^[^\\P{L}\\P{Lu}]$", "PROPERTY", "441"),
    ("(?i:^[^\\P{L}\\P{Lu}]$)", "PROPERTY", "0"),
    ("^[^\\p{Lu}\\P{L}1]$", "PROPERTY", "880"),
    ("(?i:^[^\\p{Lu}\\P{L}1]$)", "PROPERTY", "0"),
    ("^[^\\p{sc=Greek}\\P{Script=Greek}]*$", "PROPERTY", "1000"),
    ("(?i:^[^\\p{sc=Greek}\\P{Script=Greek}]*$)", "PROPERTY", "1000"),
    ("a[^\\p{L}\\P{L}]|B", "PROPERTY", "414"),
    ("(?i:a[^\\p{L}\\P{L}]|B)", "PROPERTY", "434"),
]


@pytest.mark.parametrize(("pattern", "texts", "answers"), _CLASSES_WITH_A_PROPERTY_BOTH_WAYS)
def test_a_negated_class_with_a_property_both_ways_takes_what_ecma_262_says(
    pattern: str, texts: str, answers: str
) -> None:
    _check(pattern, _PROPERTY_TEXTS, answers)


# fmt: off
_NAME_TEXTS = [
    "", "a", "Z", "p", "P", "\u00e9", "\u03a3", "\u03b1", "\u0416", "\u4e2d", "1", "\u0663", " ", "\u2028", "-", "_", "!", "$",
    "\u0301", "\u200d", "\U0001f600", "\U00010400", "\ue000", "\u0378", "\u01c5", "\u00df", "\u212a", "\u017f", "{", "}", "F",
    "g", "\ufdd0", "\u00aa", "\u00b5", "\u2167", "\u00ad", "pp", "ppp",
]
# fmt: on


def _name_texts(pattern: str) -> list[str]:
    # The texts, and the pattern as a text without its backslashes, which is what a pattern that is not valid with
    # the u flag matches: there \\p is the letter p, and a brace is a brace.
    literal = pattern.replace("\\", "")
    return [*_NAME_TEXTS, literal, "x" + literal + "y", literal[1:], literal[:-1]]


# The property expressions of ECMA-262: a value of General_Category by itself or after its property, a value of Script
# after Script or Script_Extensions, and a binary property that ECMA-262 lists. V8 takes each with the u flag.
_PROPERTIES_OF_ECMA_262: list[tuple[str, str, str]] = [
    ("\\p{L}", "NAMES", "3fe00279b3f"),
    ("^[\\P{L}]+$", "NAMES", "1ffd864c0"),
    ("^[^\\p{L}a]$", "NAMES", "1ffd864c0"),
    ("\\p{Lu}", "NAMES", "1540021100f"),
    ("^[\\P{Lu}]+$", "NAMES", "2abffdeeff0"),
    ("^[^\\p{Lu}a]$", "NAMES", "abffdeefc0"),
    ("\\p{Letter}", "NAMES", "3fe00279b3f"),
    ("^[\\P{Letter}]+$", "NAMES", "1ffd864c0"),
    ("^[^\\p{Letter}a]$", "NAMES", "1ffd864c0"),
    ("\\p{Uppercase_Letter}", "NAMES", "1540021100f"),
    ("^[\\P{Uppercase_Letter}]+$", "NAMES", "2abffdeeff0"),
    ("^[^\\p{Uppercase_Letter}a]$", "NAMES", "abffdeefc0"),
    ("\\p{gc=L}", "NAMES", "3fe00279b3f"),
    ("^[\\P{gc=L}]+$", "NAMES", "1ffd864c0"),
    ("^[^\\p{gc=L}a]$", "NAMES", "1ffd864c0"),
    ("\\p{General_Category=Uppercase_Letter}", "NAMES", "1540021100f"),
    ("^[\\P{General_Category=Uppercase_Letter}]+$", "NAMES", "2abffdeeff0"),
    ("^[^\\p{General_Category=Uppercase_Letter}a]$", "NAMES", "abffdeefc0"),
    ("\\p{gc=Letter}", "NAMES", "3fe00279b3f"),
    ("^[\\P{gc=Letter}]+$", "NAMES", "1ffd864c0"),
    ("^[^\\p{gc=Letter}a]$", "NAMES", "1ffd864c0"),
    ("\\p{LC}", "NAMES", "3fc0027993f"),
    ("^[\\P{LC}]+$", "NAMES", "3ffd866c0"),
    ("^[^\\p{LC}a]$", "NAMES", "3ffd866c0"),
    ("\\p{Cn}", "NAMES", "80400"),
    ("^[\\P{Cn}]+$", "NAMES", "3fffff7fbff"),
    ("^[^\\p{Cn}a]$", "NAMES", "1fffff7fbc0"),
    ("\\p{Combining_Mark}", "NAMES", "1000000"),
    ("^[\\P{Combining_Mark}]+$", "NAMES", "3fffeffffff"),
    ("^[^\\p{Combining_Mark}a]$", "NAMES", "1fffeffffc0"),
    ("\\p{punct}", "NAMES", "1c00600f"),
    ("^[\\P{punct}]+$", "NAMES", "3ffe3ff9ff0"),
    ("^[^\\p{punct}a]$", "NAMES", "1ffe3ff9fc0"),
    ("\\p{digit}", "NAMES", "180000000"),
    ("^[\\P{digit}]+$", "NAMES", "3fe7fffffff"),
    ("^[^\\p{digit}a]$", "NAMES", "1fe7fffffc0"),
    ("\\p{cntrl}", "NAMES", "0"),
    ("^[\\P{cntrl}]+$", "NAMES", "3ffffffffff"),
    ("^[^\\p{cntrl}a]$", "NAMES", "1ffffffffc0"),
    ("\\p{space}", "NAMES", "60000000"),
    ("^[\\P{space}]+$", "NAMES", "3ff9fffffff"),
    ("^[^\\p{space}a]$", "NAMES", "1ff9fffffc0"),
    ("\\p{White_Space}", "NAMES", "60000000"),
    ("^[\\P{White_Space}]+$", "NAMES", "3ff9fffffff"),
    ("^[^\\p{White_Space}a]$", "NAMES", "1ff9fffffc0"),
    ("\\p{Alpha}", "NAMES", "3fe00279bbf"),
    ("^[\\P{Alpha}]+$", "NAMES", "1ffd86440"),
    ("^[^\\p{Alpha}a]$", "NAMES", "1ffd86440"),
    ("\\p{Alphabetic}", "NAMES", "3fe00279bbf"),
    ("^[\\P{Alphabetic}]+$", "NAMES", "1ffd86440"),
    ("^[^\\p{Alphabetic}a]$", "NAMES", "1ffd86440"),
    ("\\p{Lower}", "NAMES", "2a800028b3f"),
    ("^[\\P{Lower}]+$", "NAMES", "157fffd74c0"),
    ("^[^\\p{Lower}a]$", "NAMES", "157fffd74c0"),
    ("\\p{Upper}", "NAMES", "1540021108f"),
    ("^[\\P{Upper}]+$", "NAMES", "2abffdeef70"),
    ("^[^\\p{Upper}a]$", "NAMES", "abffdeef40"),
    ("\\p{AHex}", "NAMES", "2010000100f"),
    ("^[\\P{AHex}]+$", "NAMES", "1feffffeff0"),
    ("^[^\\p{AHex}a]$", "NAMES", "1feffffefc0"),
    ("\\p{Hex}", "NAMES", "2010000100f"),
    ("^[\\P{Hex}]+$", "NAMES", "1feffffeff0"),
    ("^[^\\p{Hex}a]$", "NAMES", "1feffffefc0"),
    ("\\p{Emoji}", "NAMES", "100400000"),
    ("^[\\P{Emoji}]+$", "NAMES", "3feffbfffff"),
    ("^[^\\p{Emoji}a]$", "NAMES", "1feffbfffc0"),
    ("\\p{ID_Start}", "NAMES", "3fe00279bbf"),
    ("^[\\P{ID_Start}]+$", "NAMES", "1ffd86440"),
    ("^[^\\p{ID_Start}a]$", "NAMES", "1ffd86440"),
    ("\\p{IDS}", "NAMES", "3fe00279bbf"),
    ("^[\\P{IDS}]+$", "NAMES", "1ffd86440"),
    ("^[^\\p{IDS}a]$", "NAMES", "1ffd86440"),
    ("\\p{Math}", "NAMES", "0"),
    ("^[\\P{Math}]+$", "NAMES", "3fffffffff0"),
    ("^[^\\p{Math}a]$", "NAMES", "1ffffffffc0"),
    ("\\p{Dash}", "NAMES", "10000000"),
    ("^[\\P{Dash}]+$", "NAMES", "3ffefffffff"),
    ("^[^\\p{Dash}a]$", "NAMES", "1ffefffffc0"),
    ("\\p{CI}", "NAMES", "1800040"),
    ("^[\\P{CI}]+$", "NAMES", "3fffe7fffb2"),
    ("^[^\\p{CI}a]$", "NAMES", "1fffe7fff80"),
    ("\\p{ASCII}", "NAMES", "3c15e00783f"),
    ("^[\\P{ASCII}]+$", "NAMES", "3ea1ff87c0"),
    ("^[^\\p{ASCII}a]$", "NAMES", "3ea1ff87c0"),
    ("\\p{Any}", "NAMES", "3ffffffffff"),
    ("^[\\P{Any}]+$", "NAMES", "0"),
    ("^[^\\p{Any}a]$", "NAMES", "0"),
    ("\\p{Assigned}", "NAMES", "3fffff7fbff"),
    ("^[\\P{Assigned}]+$", "NAMES", "80400"),
    ("^[^\\p{Assigned}a]$", "NAMES", "80400"),
    ("\\p{Changes_When_NFKC_Casefolded}", "NAMES", "15400a793cf"),
    ("^[\\P{Changes_When_NFKC_Casefolded}]+$", "NAMES", "2abff586c30"),
    ("^[^\\p{Changes_When_NFKC_Casefolded}a]$", "NAMES", "abff586c00"),
    ("\\p{CWKCF}", "NAMES", "15400a793cf"),
    ("^[\\P{CWKCF}]+$", "NAMES", "2abff586c30"),
    ("^[^\\p{CWKCF}a]$", "NAMES", "abff586c00"),
    ("\\p{Changes_When_Casefolded}", "NAMES", "1540027918f"),
    ("^[\\P{Changes_When_Casefolded}]+$", "NAMES", "2abffd86e70"),
    ("^[^\\p{Changes_When_Casefolded}a]$", "NAMES", "abffd86e40"),
    ("\\p{CWCF}", "NAMES", "1540027918f"),
    ("^[\\P{CWCF}]+$", "NAMES", "2abffd86e70"),
    ("^[^\\p{CWCF}a]$", "NAMES", "abffd86e40"),
    ("\\p{sc=Greek}", "NAMES", "1800000000"),
    ("^[\\P{sc=Greek}]+$", "NAMES", "3e7ffffffff"),
    ("^[^\\p{sc=Greek}a]$", "NAMES", "1e7ffffffc0"),
    ("\\p{Script=Grek}", "NAMES", "1800000000"),
    ("^[\\P{Script=Grek}]+$", "NAMES", "3e7ffffffff"),
    ("^[^\\p{Script=Grek}a]$", "NAMES", "1e7ffffffc0"),
    ("\\p{scx=Grek}", "NAMES", "1801000000"),
    ("^[\\P{scx=Grek}]+$", "NAMES", "3e7feffffff"),
    ("^[^\\p{scx=Grek}a]$", "NAMES", "1e7feffffc0"),
    ("\\p{Script_Extensions=Greek}", "NAMES", "1801000000"),
    ("^[\\P{Script_Extensions=Greek}]+$", "NAMES", "3e7feffffff"),
    ("^[^\\p{Script_Extensions=Greek}a]$", "NAMES", "1e7feffffc0"),
    ("\\p{sc=Latn}", "NAMES", "3e000079abf"),
    ("^[\\P{sc=Latn}]+$", "NAMES", "1ffff86540"),
    ("^[^\\p{sc=Latn}a]$", "NAMES", "1ffff86540"),
    ("\\p{Script=Latin}", "NAMES", "3e000079abf"),
    ("^[\\P{Script=Latin}]+$", "NAMES", "1ffff86540"),
    ("^[^\\p{Script=Latin}a]$", "NAMES", "1ffff86540"),
    ("\\p{scx=Zyyy}", "NAMES", "17e40614f"),
    ("^[\\P{scx=Zyyy}]+$", "NAMES", "3fe81bf9eb0"),
    ("^[^\\p{scx=Zyyy}a]$", "NAMES", "1fe81bf9e80"),
    ("\\p{sc=Zzzz}", "NAMES", "180400"),
    ("^[\\P{sc=Zzzz}]+$", "NAMES", "3ffffe7fbff"),
    ("^[^\\p{sc=Zzzz}a]$", "NAMES", "1ffffe7fbc0"),
    ("\\p{Script=Unknown}", "NAMES", "180400"),
    ("^[\\P{Script=Unknown}]+$", "NAMES", "3ffffe7fbff"),
    ("^[^\\p{Script=Unknown}a]$", "NAMES", "1ffffe7fbc0"),
    ("\\p{sc=Zinh}", "NAMES", "1800000"),
    ("^[\\P{sc=Zinh}]+$", "NAMES", "3fffe7fffff"),
    ("^[^\\p{sc=Zinh}a]$", "NAMES", "1fffe7fffc0"),
    ("\\p{sc=Qaai}", "NAMES", "1800000"),
    ("^[\\P{sc=Qaai}]+$", "NAMES", "3fffe7fffff"),
    ("^[^\\p{sc=Qaai}a]$", "NAMES", "1fffe7fffc0"),
    ("\\p{scx=Han}", "NAMES", "200000000"),
    ("^[\\P{scx=Han}]+$", "NAMES", "3fdffffffff"),
    ("^[^\\p{scx=Han}a]$", "NAMES", "1fdffffffc0"),
    ("\\p{sc=Dsrt}", "NAMES", "200000"),
    ("^[\\P{sc=Dsrt}]+$", "NAMES", "3ffffdfffff"),
    ("^[^\\p{sc=Dsrt}a]$", "NAMES", "1ffffdfffc0"),
    ("\\p{Extended_Pictographic}", "NAMES", "400000"),
    ("^[\\P{Extended_Pictographic}]+$", "NAMES", "3ffffbfffff"),
    ("^[^\\p{Extended_Pictographic}a]$", "NAMES", "1ffffbfffc0"),
    ("\\p{NChar}", "NAMES", "400"),
    ("^[\\P{NChar}]+$", "NAMES", "3fffffffbff"),
    ("^[^\\p{NChar}a]$", "NAMES", "1fffffffbc0"),
    ("(?i:^\\P{Lu}$)", "NAMES", "3ffffffffc0"),
    ("(?i:^[^\\p{Lu}]$)", "NAMES", "3ffd866c0"),
    ("(?i:^[\\p{Lu}]$)", "NAMES", "3fc00279900"),
    ("(?i:^\\P{ASCII}$)", "NAMES", "3ea1ff87c0"),
    ("(?i:^[^\\p{ASCII}]$)", "NAMES", "3ea1fe07c0"),
    ("(?i:^[\\p{ASCII}]$)", "NAMES", "3c15e01f800"),
    ("(?i:^\\P{Any}$)", "NAMES", "0"),
    ("(?i:^[^\\p{Any}]$)", "NAMES", "0"),
    ("(?i:^[\\p{Any}]$)", "NAMES", "3ffffffffc0"),
    ("(?i:^\\P{Assigned}$)", "NAMES", "80400"),
    ("(?i:^[^\\p{Assigned}]$)", "NAMES", "80400"),
    ("(?i:^[\\p{Assigned}]$)", "NAMES", "3fffff7fbc0"),
    ("(?i:^\\P{CWKCF}$)", "NAMES", "3ffff79fd00"),
    ("(?i:^[^\\p{CWKCF}]$)", "NAMES", "3ff586400"),
    ("(?i:^[\\p{CWKCF}]$)", "NAMES", "3fc00a79bc0"),
    ("(?i:^\\P{sc=Greek}$)", "NAMES", "3e7ffffffc0"),
    ("(?i:^[^\\p{sc=Greek}]$)", "NAMES", "3e7fffffec0"),
    ("(?i:^[\\p{sc=Greek}]$)", "NAMES", "1800000100"),
    ("(?i:^\\P{Lower}$)", "NAMES", "3fffffffdc0"),
    ("(?i:^[^\\p{Lower}]$)", "NAMES", "3ffd86440"),
    ("(?i:^[\\p{Lower}]$)", "NAMES", "3fc00279b80"),
]
# Names that the regex package, Java, PCRE2 or .NET have and ECMA-262 has not, or has in another case. None is
# valid with the u flag. Without it \\p is the letter p, and what follows is a literal or a count.

_NAMES_ONLY_AN_ENGINE_HAS: list[tuple[str, str, str]] = [
    ("\\p{Greek}", "NAMES", "c"),
    ("^[\\P{Greek}]+$", "NAMES", "4000006000"),
    ("\\p{IsGreek}", "NAMES", "c"),
    ("^[\\P{IsGreek}]+$", "NAMES", "4000006000"),
    ("\\p{InGreek}", "NAMES", "c"),
    ("^[\\P{InGreek}]+$", "NAMES", "4000006000"),
    ("\\p{Latin}", "NAMES", "c"),
    ("^[\\P{Latin}]+$", "NAMES", "24000006000"),
    ("\\p{IsLatin}", "NAMES", "c"),
    ("^[\\P{IsLatin}]+$", "NAMES", "24000006000"),
    ("\\p{Latn}", "NAMES", "c"),
    ("^[\\P{Latn}]+$", "NAMES", "24000006000"),
    ("\\p{Punct}", "NAMES", "c"),
    ("^[\\P{Punct}]+$", "NAMES", "4000006000"),
    ("\\p{XDigit}", "NAMES", "c"),
    ("^[\\P{XDigit}]+$", "NAMES", "4000006800"),
    ("\\p{alpha}", "NAMES", "c"),
    ("^[\\P{alpha}]+$", "NAMES", "2c000006030"),
    ("\\p{Digit}", "NAMES", "c"),
    ("^[\\P{Digit}]+$", "NAMES", "4000006800"),
    ("\\p{Space}", "NAMES", "c"),
    ("^[\\P{Space}]+$", "NAMES", "2c000006030"),
    ("\\p{lower}", "NAMES", "c"),
    ("^[\\P{lower}]+$", "NAMES", "4000006000"),
    ("\\p{upper}", "NAMES", "c"),
    ("^[\\P{upper}]+$", "NAMES", "c000006030"),
    ("\\p{alnum}", "NAMES", "c"),
    ("^[\\P{alnum}]+$", "NAMES", "24000006000"),
    ("\\p{Alnum}", "NAMES", "c"),
    ("^[\\P{Alnum}]+$", "NAMES", "4000006000"),
    ("\\p{word}", "NAMES", "c"),
    ("^[\\P{word}]+$", "NAMES", "4000006000"),
    ("\\p{blank}", "NAMES", "c"),
    ("^[\\P{blank}]+$", "NAMES", "24000006000"),
    ("\\p{graph}", "NAMES", "c"),
    ("^[\\P{graph}]+$", "NAMES", "2c000006830"),
    ("\\p{print}", "NAMES", "c"),
    ("^[\\P{print}]+$", "NAMES", "c000006030"),
    ("\\p{xdigit}", "NAMES", "c"),
    ("^[\\P{xdigit}]+$", "NAMES", "4000006800"),
    ("\\p{posix_alpha}", "NAMES", "c"),
    ("^[\\P{posix_alpha}]+$", "NAMES", "2c008006030"),
    ("\\p{l}", "NAMES", "c"),
    ("^[\\P{l}]+$", "NAMES", "4000006000"),
    ("\\p{lu}", "NAMES", "c"),
    ("^[\\P{lu}]+$", "NAMES", "4000006000"),
    ("\\p{LU}", "NAMES", "c"),
    ("^[\\P{LU}]+$", "NAMES", "4000006000"),
    ("\\p{LETTER}", "NAMES", "c"),
    ("^[\\P{LETTER}]+$", "NAMES", "4000006000"),
    ("\\p{letter}", "NAMES", "c"),
    ("^[\\P{letter}]+$", "NAMES", "4000006000"),
    ("\\p{Uppercase Letter}", "NAMES", "c"),
    ("^[\\P{Uppercase Letter}]+$", "NAMES", "2c040006030"),
    ("\\p{UppercaseLetter}", "NAMES", "c"),
    ("^[\\P{UppercaseLetter}]+$", "NAMES", "2c000006030"),
    ("\\p{uppercase_letter}", "NAMES", "c"),
    ("^[\\P{uppercase_letter}]+$", "NAMES", "2c008006030"),
    ("\\p{L&}", "NAMES", "c"),
    ("^[\\P{L&}]+$", "NAMES", "4000006000"),
    ("\\p{L_}", "NAMES", "c"),
    ("^[\\P{L_}]+$", "NAMES", "4008006000"),
    ("\\p{Lc}", "NAMES", "c"),
    ("^[\\P{Lc}]+$", "NAMES", "4000006000"),
    ("\\p{^L}", "NAMES", "0"),
    ("^[\\P{^L}]+$", "NAMES", "4000006000"),
    ("\\p{Is L}", "NAMES", "c"),
    ("^[\\P{Is L}]+$", "NAMES", "4040006000"),
    ("\\p{ L}", "NAMES", "c"),
    ("^[\\P{ L}]+$", "NAMES", "4040006000"),
    ("\\p{IsL}", "NAMES", "c"),
    ("^[\\P{IsL}]+$", "NAMES", "4000006000"),
    ("\\p{IsLu}", "NAMES", "c"),
    ("^[\\P{IsLu}]+$", "NAMES", "4000006000"),
    ("\\p{IsLetter}", "NAMES", "c"),
    ("^[\\P{IsLetter}]+$", "NAMES", "4000006000"),
    ("\\p{IsAlphabetic}", "NAMES", "c"),
    ("^[\\P{IsAlphabetic}]+$", "NAMES", "2c000006030"),
    ("\\p{javaLowerCase}", "NAMES", "c"),
    ("^[\\P{javaLowerCase}]+$", "NAMES", "24000006000"),
    ("\\p{Xan}", "NAMES", "c"),
    ("^[\\P{Xan}]+$", "NAMES", "24000006000"),
    ("\\p{Xwd}", "NAMES", "c"),
    ("^[\\P{Xwd}]+$", "NAMES", "4000006000"),
    ("\\p{gc=lu}", "NAMES", "c"),
    ("^[\\P{gc=lu}]+$", "NAMES", "4000006800"),
    ("\\p{Gc=Lu}", "NAMES", "c"),
    ("^[\\P{Gc=Lu}]+$", "NAMES", "4000006000"),
    ("\\p{gc:Lu}", "NAMES", "c"),
    ("^[\\P{gc:Lu}]+$", "NAMES", "4000006800"),
    ("\\p{gc=Greek}", "NAMES", "c"),
    ("^[\\P{gc=Greek}]+$", "NAMES", "4000006800"),
    ("\\p{gc=Alphabetic}", "NAMES", "c"),
    ("^[\\P{gc=Alphabetic}]+$", "NAMES", "2c000006830"),
    ("\\p{General_Category=IsLu}", "NAMES", "c"),
    ("^[\\P{General_Category=IsLu}]+$", "NAMES", "24008006800"),
    ("\\p{sc=L}", "NAMES", "c"),
    ("^[\\P{sc=L}]+$", "NAMES", "4000006000"),
    ("\\p{sc=greek}", "NAMES", "c"),
    ("^[\\P{sc=greek}]+$", "NAMES", "4000006800"),
    ("\\p{script=Greek}", "NAMES", "c"),
    ("^[\\P{script=Greek}]+$", "NAMES", "c000006030"),
    ("\\p{Script=IsGreek}", "NAMES", "c"),
    ("^[\\P{Script=IsGreek}]+$", "NAMES", "c000006030"),
    ("\\p{Script:Greek}", "NAMES", "c"),
    ("^[\\P{Script:Greek}]+$", "NAMES", "c000006030"),
    ("\\p{Script=Is_Greek}", "NAMES", "c"),
    ("^[\\P{Script=Is_Greek}]+$", "NAMES", "c008006030"),
    ("\\p{ScriptExtensions=Greek}", "NAMES", "c"),
    ("^[\\P{ScriptExtensions=Greek}]+$", "NAMES", "c000006030"),
    ("\\p{scx=Any}", "NAMES", "c"),
    ("^[\\P{scx=Any}]+$", "NAMES", "4000006000"),
    ("\\p{sc=Sidetic}", "NAMES", "c"),
    ("^[\\P{sc=Sidetic}]+$", "NAMES", "4000006000"),
    ("\\p{sc=Sidt}", "NAMES", "c"),
    ("^[\\P{sc=Sidt}]+$", "NAMES", "4000006000"),
    ("\\p{sc=Tolong_Siki}", "NAMES", "c"),
    ("^[\\P{sc=Tolong_Siki}]+$", "NAMES", "4008006800"),
    ("\\p{blk=ASCII}", "NAMES", "c"),
    ("^[\\P{blk=ASCII}]+$", "NAMES", "4000006000"),
    ("\\p{Block=Greek}", "NAMES", "c"),
    ("^[\\P{Block=Greek}]+$", "NAMES", "4000006000"),
    ("\\p{InBasicLatin}", "NAMES", "c"),
    ("^[\\P{InBasicLatin}]+$", "NAMES", "24000006000"),
    ("\\p{IsBasicLatin}", "NAMES", "c"),
    ("^[\\P{IsBasicLatin}]+$", "NAMES", "24000006000"),
    ("\\p{Basic_Latin}", "NAMES", "c"),
    ("^[\\P{Basic_Latin}]+$", "NAMES", "24008006000"),
    ("\\p{BasicLatin}", "NAMES", "c"),
    ("^[\\P{BasicLatin}]+$", "NAMES", "24000006000"),
    ("\\p{InBasic_Latin}", "NAMES", "c"),
    ("^[\\P{InBasic_Latin}]+$", "NAMES", "24008006000"),
    ("\\p{Latin_1_Supplement}", "NAMES", "c"),
    ("^[\\P{Latin_1_Supplement}]+$", "NAMES", "2c108006030"),
    ("\\p{Greek_And_Coptic}", "NAMES", "c"),
    ("^[\\P{Greek_And_Coptic}]+$", "NAMES", "c008006030"),
    ("\\p{ascii}", "NAMES", "c"),
    ("^[\\P{ascii}]+$", "NAMES", "24000006000"),
    ("\\p{any}", "NAMES", "c"),
    ("^[\\P{any}]+$", "NAMES", "24000006000"),
    ("\\p{ANY}", "NAMES", "c"),
    ("^[\\P{ANY}]+$", "NAMES", "4000006000"),
    ("\\p{assigned}", "NAMES", "c"),
    ("^[\\P{assigned}]+$", "NAMES", "24000006800"),
    ("\\p{Alphabetic=Yes}", "NAMES", "c"),
    ("^[\\P{Alphabetic=Yes}]+$", "NAMES", "2c000006030"),
    ("\\p{Alphabetic=Y}", "NAMES", "c"),
    ("^[\\P{Alphabetic=Y}]+$", "NAMES", "2c000006030"),
    ("\\p{Alpha=True}", "NAMES", "c"),
    ("^[\\P{Alpha=True}]+$", "NAMES", "2c000006030"),
    ("\\p{L=Y}", "NAMES", "c"),
    ("^[\\P{L=Y}]+$", "NAMES", "4000006000"),
    ("\\p{Any=Yes}", "NAMES", "c"),
    ("^[\\P{Any=Yes}]+$", "NAMES", "4000006000"),
    ("\\p{WhiteSpace}", "NAMES", "c"),
    ("^[\\P{WhiteSpace}]+$", "NAMES", "2c000006030"),
    ("\\p{White Space}", "NAMES", "c"),
    ("^[\\P{White Space}]+$", "NAMES", "2c040006030"),
    ("\\p{whitespace}", "NAMES", "c"),
    ("^[\\P{whitespace}]+$", "NAMES", "2c000006030"),
    ("\\p{wspace}", "NAMES", "c"),
    ("^[\\P{wspace}]+$", "NAMES", "2c000006030"),
    ("\\p{Lu|Ll}", "NAMES", "f"),
    ("^[\\P{Lu|Ll}]+$", "NAMES", "4000006000"),
    ("\\p{Lu,Ll}", "NAMES", "c"),
    ("^[\\P{Lu,Ll}]+$", "NAMES", "4000006000"),
    ("\\p{RGI_Emoji}", "NAMES", "c"),
    ("^[\\P{RGI_Emoji}]+$", "NAMES", "4008006000"),
    ("\\p{Basic_Emoji}", "NAMES", "c"),
    ("^[\\P{Basic_Emoji}]+$", "NAMES", "24008006000"),
    ("\\p{Other_Alphabetic}", "NAMES", "c"),
    ("^[\\P{Other_Alphabetic}]+$", "NAMES", "2c008006030"),
    ("\\p{Hyphen}", "NAMES", "c"),
    ("^[\\P{Hyphen}]+$", "NAMES", "c000006030"),
    ("\\p{IDSU}", "NAMES", "c"),
    ("^[\\P{IDSU}]+$", "NAMES", "4000006000"),
    ("\\p{Changes_When_Case_Folded}", "NAMES", "c"),
    ("^[\\P{Changes_When_Case_Folded}]+$", "NAMES", "24008007800"),
    ("\\p{Composition_Exclusion}", "NAMES", "c"),
    ("^[\\P{Composition_Exclusion}]+$", "NAMES", "c008006030"),
    ("\\p{Age=1.1}", "NAMES", "c"),
    ("^[\\P{Age=1.1}]+$", "NAMES", "4100006800"),
    ("\\p{age=16.0}", "NAMES", "c"),
    ("^[\\P{age=16.0}]+$", "NAMES", "24100006800"),
    ("\\p{ccc=230}", "NAMES", "c"),
    ("^[\\P{ccc=230}]+$", "NAMES", "4000006000"),
    ("\\p{bc=L}", "NAMES", "c"),
    ("^[\\P{bc=L}]+$", "NAMES", "4000006000"),
    ("\\p{nt=De}", "NAMES", "c"),
    ("^[\\P{nt=De}]+$", "NAMES", "4000006000"),
    ("\\p{ea=W}", "NAMES", "c"),
    ("^[\\P{ea=W}]+$", "NAMES", "24000006000"),
    ("\\p{InF}", "NAMES", "c"),
    ("^[\\P{InF}]+$", "NAMES", "4000007000"),
    ("\\p{Nope}", "NAMES", "c"),
    ("^[\\P{Nope}]+$", "NAMES", "c000006030"),
    ("\\p{}", "NAMES", "c"),
    ("^[\\P{}]+$", "NAMES", "4000006000"),
    ("\\p{3}", "NAMES", "10"),
    ("^[\\P{3}]+$", "NAMES", "4000006000"),
    ("\\p{2,}", "NAMES", "30"),
    ("^[\\P{2,}]+$", "NAMES", "4000006000"),
    ("\\p{1,2}", "NAMES", "800000003d"),
    ("^[\\P{1,2}]+$", "NAMES", "4100006000"),
]

# fmt: off
_ANNEX_B_TEXTS = [
    "", "a", "aa", "ab", "b", "p", "pp", "ppp", "P", "pL", "p{", "p{}", "p{L}", "L",
    "k", "kk", "k<a>", "xk", "u", "uuu", "u{", "u{61}", "u12", "c", "\\", "\\c", "\\c1", "\u0011",
    "\u001f", "\u000a", "*", "x", "x1", "x1g", "\u0001", "\u0000", "\u00000", "\u00001", "\u00008", "\u000a3", "S", "\u00ff",
    " 0", "18", "81", "8", "9", "a\u0008", "a\u0002", "{", "}", "]", "a{", "a{1", "a{1,", "a{,1}",
    "a{1,2", "x{a}", "a}", "a]", "{}", "{a}", "-", "e", "z", "aZ", "Aa", "G", "h", "R",
    "X", "N{U+41}", "o{101}", "g{1}", "Q.E", "QaE", "<a>", "_", " ", "B", "d", "1", "w", "s",
    "y", "U0001F600", ":", "[", "&", ":]", "ba", "1a", "\u0007", "\u001b", "A", "ax", "aa\u0008",
]
# fmt: on

# Escapes and literals of the grammar of Annex B, which reads a pattern that is not valid with the u flag: \\p, \\k and
# \\u without what the u flag wants after them are the letters, \\c without a letter is a backslash and a c, an escape
# of digits that is no reference is an octal escape or the digit, and a brace or a bracket by itself is a literal.
# A few of the rows are valid with the u flag, and have its meaning.
_ANNEX_B: list[tuple[str, str, str]] = [
    ("\\p", "ANNEX", "ef000000000000000000000"),
    ("\\P", "ANNEX", "10000000000000000000000"),
    ("\\p{", "ANNEX", "7000000000000000000000"),
    ("\\p{}", "ANNEX", "2000000000000000000000"),
    ("\\pL", "ANNEX", "8000000000000000000000"),
    ("\\p{3}", "ANNEX", "20000000000000000000000"),
    ("^\\p{2,}$", "ANNEX", "60000000000000000000000"),
    ("[\\p]", "ANNEX", "ef000000000000000000000"),
    ("^[\\pL]+$", "ANNEX", "e8800000000000000000000"),
    ("(?<n>a)\\p{Greek}", "ANNEX", "0"),
    ("\\k", "ANNEX", "780000000000000000000"),
    ("\\k<a>", "ANNEX", "100000000000000000000"),
    ("^\\k{2}$", "ANNEX", "200000000000000000000"),
    ("[\\k]", "ANNEX", "780000000000000000000"),
    ("^[\\k<a>]+$", "ANNEX", "c00700000000000000100000"),
    ("(x)\\k", "ANNEX", "80000000000000000000"),
    ("\\u{61}", "ANNEX", "e001000000031fe8c0300063"),
    ("^\\u{3}$", "ANNEX", "0"),
    ("\\u{", "ANNEX", "18000000000000000000"),
    ("\\u12", "ANNEX", "4000000000000000000"),
    ("[\\u{61}]", "ANNEX", "e001000000031fe8c0300063"),
    ("\\c", "ANNEX", "c00000000000000000"),
    ("^\\c$", "ANNEX", "800000000000000000"),
    ("\\c1", "ANNEX", "400000000000000000"),
    ("^[\\c1]$", "ANNEX", "200000000000000000"),
    ("^[\\c_]$", "ANNEX", "100000000000000000"),
    ("[\\c]", "ANNEX", "3c00000000000000000"),
    ("^[\\c*]$", "ANNEX", "3040000000000000000"),
    ("\\x", "ANNEX", "80038000008000000002"),
    ("\\x1", "ANNEX", "18000000000000000"),
    ("\\x1g", "ANNEX", "8000000000000000"),
    ("[\\x1]", "ANNEX", "8c4388300f8003808822"),
    ("\\0", "ANNEX", "3c00000000000000"),
    ("^\\00$", "ANNEX", "2000000000000000"),
    ("^\\01$", "ANNEX", "4000000000000000"),
    ("^\\08$", "ANNEX", "400000000000000"),
    ("^\\012$", "ANNEX", "80000000000000000"),
    ("^\\0123$", "ANNEX", "200000000000000"),
    ("^\\377$", "ANNEX", "80000000000000"),
    ("^\\400$", "ANNEX", "40000000000000"),
    ("^\\1$", "ANNEX", "4000000000000000"),
    ("^(a)\\1$", "ANNEX", "400000000000000000000000"),
    ("^(a)\\2$", "ANNEX", "1000000000000"),
    ("^(a)\\10$", "ANNEX", "2000000000000"),
    ("^\\8$", "ANNEX", "8000000000000"),
    ("^\\9$", "ANNEX", "4000000000000"),
    ("^\\18$", "ANNEX", "0"),
    ("^\\81$", "ANNEX", "10000000000000"),
    ("^[\\1]$", "ANNEX", "4000000000000000"),
    ("^[\\8]$", "ANNEX", "8000000000000"),
    ("^[\\12]$", "ANNEX", "80000000000000000"),
    ("^[\\400]$", "ANNEX", "40000"),
    ("{", "ANNEX", "70180000009f9803800000"),
    ("}", "ANNEX", "300800000042d803800000"),
    ("]", "ANNEX", "202000000080"),
    ("^a{$", "ANNEX", "100000000000"),
    ("^a{1$", "ANNEX", "80000000000"),
    ("^a{1,$", "ANNEX", "40000000000"),
    ("^a{,1}$", "ANNEX", "20000000000"),
    ("^a{1,2$", "ANNEX", "10000000000"),
    ("^x{a}$", "ANNEX", "8000000000"),
    ("^a}$", "ANNEX", "4000000000"),
    ("^a]$", "ANNEX", "2000000000"),
    ("^[]a]$", "ANNEX", "0"),
    ("^{}$", "ANNEX", "1000000000"),
    ("^{a}$", "ANNEX", "800000000"),
    ("\\-", "ANNEX", "400000000"),
    ("^\\a$", "ANNEX", "800000000000000000000000"),
    ("^\\e$", "ANNEX", "200000000"),
    ("^\\z$", "ANNEX", "100000000"),
    ("^a\\Z$", "ANNEX", "80000000"),
    ("^\\Aa$", "ANNEX", "40000000"),
    ("\\G", "ANNEX", "20000000"),
    ("^\\h$", "ANNEX", "10000000"),
    ("\\R", "ANNEX", "8000000"),
    ("\\X", "ANNEX", "4000000"),
    ("^\\N{U+41}$", "ANNEX", "0"),
    ("^\\o{101}$", "ANNEX", "0"),
    ("^\\g{1}$", "ANNEX", "0"),
    ("^\\Q.\\E$", "ANNEX", "600000"),
    ("\\<a\\>", "ANNEX", "100000000000000100000"),
    ("^\\_$", "ANNEX", "80000"),
    ("^\\ $", "ANNEX", "40000"),
    ("^[\\a]$", "ANNEX", "800000000000000000000000"),
    ("^[\\-]$", "ANNEX", "400000000"),
    ("^[\\B]$", "ANNEX", "20000"),
    ("^[\\d-a]$", "ANNEX", "80000000000c000400008000"),
    ("^[a-\\d]$", "ANNEX", "80000000000c000400008000"),
    ("^[\\w-\\d]$", "ANNEX", "990c4202010c00073c0bf004"),
    ("^[\\s-z]$", "ANNEX", "80000000500040000"),
    ("^(?=a)*a$", "ANNEX", "800000000000000000000000"),
    ("^(?!a)?b$", "ANNEX", "100000000000000000000000"),
    ("^(?=(a))?\\1a$", "ANNEX", "800000000000000000000000"),
    ("^(?=(a)){0}\\1a$", "ANNEX", "800000000000000000000000"),
    ("\\U0001F600", "ANNEX", "800"),
    ("[[:alpha:]]", "ANNEX", "2000000080"),
    ("^[[:alpha:]]$", "ANNEX", "2000000080"),
    ("^[a&&b]$", "ANNEX", "900000000000000000000100"),
    ("^[[a]]$", "ANNEX", "2000000000"),
]

# V8 refuses each of these with the u flag and without it.
_NOT_VALID = [
    "(?<n>a)[\\k]",
    "[\\k](?<n>a)",
    "(?<n>a)[\\k<n>]",
    "(?<a>x)\\k",
    "(?<a>x)\\k<b>",
    "(?<a>x)\\k<a",
    "{1}",
    "a{2,1}",
    "(?<=a)*",
    "\\b*",
    "(?P<a>x)",
    "(?#c)",
    "(?>a)",
    "a++",
    "(?i)a",
    "[b-a]",
    "^[a--b]$",
    "[a",
    "a)",
    "(a",
    "\\",
    "\\p{L}++",
    "[\\p{L}",
    "(?<n>\\p{L}",
]


@pytest.mark.parametrize(("pattern", "texts", "answers"), _PROPERTIES_OF_ECMA_262 + _NAMES_ONLY_AN_ENGINE_HAS)
def test_a_property_name_is_one_of_ecma_262_or_the_pattern_is_read_without_the_u_flag(
    pattern: str, texts: str, answers: str
) -> None:
    _check(pattern, _name_texts(pattern), answers)


@pytest.mark.parametrize(("pattern", "texts", "answers"), _ANNEX_B)
def test_an_escape_of_annex_b_means_what_annex_b_says(pattern: str, texts: str, answers: str) -> None:
    _check(pattern, _ANNEX_B_TEXTS, answers)


@pytest.mark.parametrize("pattern", _NOT_VALID)
def test_a_pattern_neither_grammar_takes_is_refused(pattern: str) -> None:
    with pytest.raises(cjs.SchemaCompilationError):
        cjs.compile({"pattern": pattern})


def test_the_engine_is_never_asked_what_a_property_name_means() -> None:
    # Each expression is written with its property named and a value by its short name, whatever alias was used.
    assert translate("\\p{Lu}") == translate("\\p{Uppercase_Letter}") == ("\\p{gc=Lu}", True)
    assert translate("\\P{Script=Greek}") == translate("\\P{sc=Grek}") == ("\\P{sc=Grek}", True)
    assert translate("[\\p{scx=Greek}\\p{Alpha}]") == ("[\\p{scx=Grek}\\p{Alphabetic=Yes}]", True)
    assert translate("\\p{Assigned}") == ("\\P{gc=Cn}", True)
    # ASCII and Any are ranges, which the standard library's engine runs.
    assert translate("^\\p{ASCII}$") == ("^[\\x00-\\x7f]\\Z", False)
    assert translate("^[^\\P{ASCII}a]$") == ("^[^\\u0080-\\U0010ffffa]\\Z", False)
    assert translate("\\P{Any}") == translate("[\\P{Any}]") == ("(?!)", False)
    for pattern in (
        "\\p{Greek}",
        "\\p{IsGreek}",
        "\\p{alpha}",
        "\\p{lu}",
        "\\p{InF}",
        "[\\p{L&}]",
        "\\p{gc=lu}",
        "\\pL",
    ):
        assert translate(pattern) is None, pattern
        assert translate(pattern, False) is not None, pattern


def test_the_regex_package_has_every_property_of_ecma_262() -> None:
    assert pattern_module._property("L") is not None
    names = sorted(pattern_module._properties)
    assert len(names) == 1690
    for name in names:
        for pattern in (f"\\p{{{name}}}", f"[^\\P{{{name}}}a]"):
            assert cjs.compile({"pattern": pattern})("a") in (True, False), pattern


def test_where_v8_departs_from_ecma_262_in_the_names_it_takes() -> None:
    # V8 13.6 takes \\p{WSpace}, an alias of White_Space that the Unicode Character Database has and ECMA-262 does not
    # list. Here it is not valid with the u flag, so it is the letter p and what follows it.
    validate = cjs.compile({"pattern": "^\\p{WSpace}$"})
    assert validate("p{WSpace}") and not validate(" ")
    # V8 refuses the Script value Katakana_Or_Hiragana (Hrkt), which PropertyValueAliases.txt has, so ECMA-262 has it.
    # No character has that value.
    for name in ("sc=Hrkt", "Script=Katakana_Or_Hiragana", "scx=Hrkt", "Script_Extensions=Katakana_Or_Hiragana"):
        assert translate(f"\\p{{{name}}}") is not None, name
        assert not any(cjs.compile({"pattern": f"\\p{{{name}}}"})(text) for text in _NAME_TEXTS), name
        assert cjs.compile({"pattern": f"^\\P{{{name}}}$"})("\u30ab"), name
