"""Tables of V8's answers (V8 13.6, in Node 24.11.0) for the classes of pattern that are written out for Python's
engines. Each row has a pattern, the name of a list of texts, and V8's answer for each text of the list as the bits
of a hexadecimal number, the first text in the highest bit. A pattern in a case-insensitive group has V8's answers
for the same pattern with the i flag. A pattern with ``\\-`` outside a class is valid only without the u flag."""

from __future__ import annotations

from itertools import product

import pytest

import corvus_json_schema as cjs
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
