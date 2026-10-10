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
