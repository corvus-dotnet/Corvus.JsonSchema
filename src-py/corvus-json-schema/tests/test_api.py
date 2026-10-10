from __future__ import annotations

import copy
import importlib.util
import json
import random
import re
import sys
from pathlib import Path
from types import ModuleType
from typing import Any

import pytest

import corvus_json_schema as cjs
from corvus_json_schema.pattern import compile_pattern, is_valid_ecma_regex, translate


def import_module(source: str, tmp_path: Path, name: str = "generated") -> ModuleType:
    path = tmp_path / f"{name}.py"
    path.write_text(source, encoding="utf-8")
    spec = importlib.util.spec_from_file_location(name, path)
    assert spec is not None and spec.loader is not None
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def test_compile_accepts_json_text_and_parsed_schemas() -> None:
    for v in (cjs.compile('{"type":"string","minLength":2}'), cjs.compile({"type": "string", "minLength": 2})):
        assert v("ab") is True
        assert v("a") is False
        assert v(12) is False


def test_generate_module_emits_a_standalone_module_equivalent_to_compile(tmp_path: Path) -> None:
    schema = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "type": "object",
        "properties": {
            "id": {"type": "integer", "minimum": 1},
            "tags": {"type": "array", "items": {"type": "string"}, "uniqueItems": True},
        },
        "required": ["id"],
        "unevaluatedProperties": False,
    }
    mod = import_module(cjs.generate_module(schema), tmp_path)
    direct = cjs.compile(schema)
    for c in [
        {"id": 1},
        {"id": 1, "tags": ["a", "b"]},
        {"id": 1, "tags": ["a", "a"]},
        {"id": 0},
        {"id": 1, "extra": True},
        {},
        "x",
    ]:
        assert mod.validate(c) == direct(c), c


def test_generate_module_keeps_the_dynamic_scope(tmp_path: Path) -> None:
    schema = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/tree",
        "$dynamicAnchor": "node",
        "type": "object",
        "properties": {"data": True, "children": {"type": "array", "items": {"$dynamicRef": "#node"}}},
    }
    strict = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/strict-tree",
        "$dynamicAnchor": "node",
        "$ref": "tree",
        "unevaluatedProperties": False,
    }

    def resolve_document(uri: str) -> Any:
        return schema if uri == "https://example.com/tree" else None

    mod = import_module(cjs.generate_module(strict, resolve_document=resolve_document), tmp_path)
    direct = cjs.compile(strict, resolve_document=resolve_document)
    ok = {"children": [{"data": 1, "children": []}]}
    bad = {"children": [{"daat": 1}]}
    assert direct(ok) is True
    assert direct(bad) is False
    assert mod.validate(ok) is True
    assert mod.validate(bad) is False


def test_custom_formats_are_asserted_when_format_assertion_is_on() -> None:
    v = cjs.compile(
        {"type": "string", "format": "even-length"},
        assert_format=True,
        formats={"even-length": lambda s: len(s) % 2 == 0},
    )
    assert v("ab") is True
    assert v("abc") is False
    with pytest.raises(cjs.SchemaCompilationError):
        cjs.generate_module({"format": "even-length"}, assert_format=True, formats={"even-length": lambda s: True})


def test_format_is_an_annotation_by_default_and_asserted_on_request() -> None:
    assert cjs.compile({"format": "ipv4"})("not an address") is True
    assert cjs.compile({"format": "ipv4"}, assert_format=True)("not an address") is False
    assert cjs.compile({"format": "ipv4"}, assert_format=True)("10.0.0.1") is True


def test_entry_point_evaluates_from_a_subschema() -> None:
    schema = {"$defs": {"positive": {"type": "number", "exclusiveMinimum": 0}}, "type": "string"}
    v = cjs.compile(schema, entry_point="#/$defs/positive")
    assert v(3) is True
    assert v(-3) is False
    assert v("s") is False


def test_default_dialect_applies_to_schemas_without_schema() -> None:
    schema = {"items": [{"type": "string"}], "additionalItems": False}
    assert cjs.compile(schema, default_dialect=cjs.Dialect.DRAFT7)(["a", "b"]) is False
    # In 2020-12 an array-valued "items" is not the tuple form, so neither keyword applies.
    assert cjs.compile(schema)(["a", "b"]) is True


def test_unresolvable_references_fail_compilation() -> None:
    with pytest.raises(cjs.SchemaCompilationError):
        cjs.compile({"$ref": "https://example.com/missing.json"})


def test_invalid_patterns_fail_compilation() -> None:
    with pytest.raises(cjs.SchemaCompilationError):
        cjs.compile({"pattern": "^(abc"})


def test_a_translation_the_engine_does_not_compile_is_a_fault(monkeypatch: pytest.MonkeyPatch) -> None:
    from corvus_json_schema import pattern as pattern_module

    # A property the regex package does not have makes a pattern one that is not valid with the u flag.
    for pattern in ("\\p{Nope}", "[\\p{Script=Nope}]", "(?i:\\P{sc=L})", "(?i:[\\p{gc=Greek}])"):
        assert not is_valid_ecma_regex(pattern), pattern
    with pytest.raises(cjs.SchemaCompilationError, match="Invalid regular expression"):
        cjs.compile({"pattern": "\\p{Nope}+*"})
    # Anything else the engine does not compile is a fault of the translation. It is raised as one, and is not
    # passed off as a pattern that is not valid.
    monkeypatch.setattr(pattern_module, "_translate", lambda pattern, unicode, for_regex: ("(?P<a>", True, False))
    with pytest.raises(RuntimeError, match="fault of the package"):
        pattern_module._compile("a", True)
    assert is_valid_ecma_regex("a")


def test_in_place_recursion_beyond_max_depth_raises() -> None:
    v = cjs.compile({"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "$ref": "#/$defs/loop"}, max_depth=16)
    with pytest.raises(cjs.SchemaEvaluationDepthError):
        v(1)
    # The depth count is reset for the next evaluation.
    with pytest.raises(cjs.SchemaEvaluationDepthError):
        v(1)


def test_numbers_are_compared_exactly_for_multiple_of() -> None:
    v = cjs.compile({"multipleOf": 0.01})
    assert v(0.07) is True
    assert v(19.99) is True
    assert v(0.075) is False
    assert cjs.compile({"multipleOf": 0.0001})(0.0075) is True
    assert cjs.compile({"multipleOf": 3})(10**30) is False
    assert cjs.compile({"multipleOf": 3})(3 * 10**30) is True


def test_booleans_are_not_numbers() -> None:
    assert cjs.compile({"const": 1})(True) is False
    assert cjs.compile({"const": 1})(1.0) is True
    assert cjs.compile({"const": False})(0) is False
    assert cjs.compile({"enum": [0, 1]})(False) is False
    assert cjs.compile({"enum": [0, 1, 2, 3, 4, 5, 6, 7]})(True) is False
    assert cjs.compile({"enum": [0, 1, 2, 3, 4, 5, 6, 7]})(7.0) is True
    assert cjs.compile({"enum": [[1], {"a": True}]})([True]) is False
    assert cjs.compile({"enum": [[1], {"a": True}]})({"a": True}) is True
    assert cjs.compile({"uniqueItems": True})([1, True]) is True
    assert cjs.compile({"uniqueItems": True})([1, 1.0]) is False
    assert cjs.compile({"uniqueItems": True})([[1], [True]]) is True
    assert cjs.compile({"uniqueItems": True})([{"a": 1}, {"a": 1.0}]) is False
    assert cjs.compile({"type": "integer"})(True) is False
    assert cjs.compile({"type": "number", "minimum": 0})(True) is False


def test_integers_are_exact() -> None:
    v = cjs.compile({"type": "integer", "maximum": 2**64})
    assert v(2**64) is True
    assert v(2**64 + 1) is False
    assert cjs.compile({"type": "integer"})(1.0) is True
    assert cjs.compile({"$schema": "http://json-schema.org/draft-04/schema#", "type": "integer"})(1.0) is False


def test_unusual_property_names_are_looked_up_as_keys() -> None:
    v = cjs.compile(
        {
            "required": ["constructor", "toString"],
            "properties": {"__proto__": {"type": "string"}, "a'b\"c": {"type": "string"}},
        }
    )
    assert v({}) is False
    assert v({"constructor": 1, "toString": 2}) is True
    assert v({"constructor": 1, "toString": 2, "__proto__": 3}) is False
    assert v({"constructor": 1, "toString": 2, "__proto__": "x"}) is True
    assert v({"constructor": 1, "toString": 2, "a'b\"c": 1}) is False


def test_structurally_identical_subschemas_share_one_generated_function(monkeypatch: pytest.MonkeyPatch) -> None:
    # Without inlining (which would put each copy in its caller), the three copies are one function.
    monkeypatch.setattr("corvus_json_schema.codegen.INLINE_CHILDREN", False)
    leaf = {"type": "object", "properties": {"a": {"type": "string"}, "b": {"type": "integer"}}, "required": ["a"]}
    v = cjs.compile({"type": "object", "properties": {"x": leaf, "y": copy.deepcopy(leaf), "z": copy.deepcopy(leaf)}})
    assert len(re.findall(r"^def ", v.source, re.M)) == 2


def test_pattern_fast_paths_agree_with_the_regular_expression() -> None:
    patterns = [
        "^[1-5](?:[0-9]{2}|XX)$",
        "^[a-zA-Z0-9._-]+$",
        "^\\d{4}-\\d{2}-\\d{2}$",
        "^[A-Z]",
        "^[a-z]{2,3}$",
        "^(ab|cd)",
        "^[a-c]{2}[0-9]*$",
        "^x-",
        "^\\w+$",
        "^(a|b)c{2}$",
        "^(abc|de)$",
        "^abc$",
        "b-c",
        "^ab",
    ]
    alphabet = ["a", "b", "c", "d", "e", "x", "X", "A", "Z", "-", ".", "_", "0", "1", "2", "5", "9", " ", "é", "😀"]
    rng = random.Random(12345)
    for p in patterns:
        matcher = compile_pattern(p)
        assert matcher is not None
        via_compile = cjs.compile({"pattern": p})
        for _ in range(3000):
            s = "".join(rng.choice(alphabet) for _ in range(rng.randrange(7)))
            assert via_compile(s) == (matcher(s) is not None), f"{p} on {s!r}"


@pytest.mark.parametrize(
    ("pattern", "text", "expected"),
    [
        ("^[a-z]+$", "abc\n", False),
        ("\\d", "\u0663", False),
        ("\\w", "é", False),
        ("^\\p{L}+$", "héllo", True),
        ("a.c", "a\nc", False),
        ("a.c", "a\u2028c", False),
        ("^\\s$", "\u00a0", True),
        ("^\\S$", "\u00a0", False),
        ("(?<year>\\d{4})-\\k<year>", "2020-2020", True),
        ("^\\d{3}\\-\\d{4}$", "555-1234", True),
        ("\\bfoo", "éfoo", True),
        ("[^]", "\n", True),
        ("^[]", "a", False),
        ("\\u{1F600}", "😀", True),
        ("\\uD83D\\uDE00", "😀", True),
        ("(?<=a+)b", "aab", True),
        # \B holds in an empty text and \b does not (CPython before 3.14 has \B never match there).
        ("\\B", "", True),
        ("^\\B$", "", True),
        ("^(\\d|\\B){2}", "", True),
        ("(?<!a)\\B", "", True),
        ("\\b", "", False),
        ("(?<!a)\\b", "", False),
        ("^\\B$", "-", False),
        ("\\B", "a", False),
        ("\\B", "é", True),
        ("^.\\B.$", "aé", False),
    ],
)
def test_patterns_have_ecma_semantics(pattern: str, text: str, expected: bool) -> None:
    assert cjs.compile({"pattern": pattern})(text) is expected


def test_word_boundaries_are_ecma_at_every_position_of_every_short_text() -> None:
    # \b is between a word character ([A-Za-z0-9_]) and anything else, the ends of the text included. \B is every
    # other position. Each is tried through both engines: a lookbehind sends a pattern to the regex package.
    alphabet = ["a", "_", "7", "-", " ", "é"]
    texts = [""]
    for _ in range(3):
        texts += [t + c for t in texts for c in alphabet]

    def word(t: str, i: int) -> bool:
        return 0 <= i < len(t) and (t[i].isascii() and (t[i].isalnum() or t[i] == "_"))

    for at in range(4):
        dots = "[^]" * at
        for prefix in ("", "(?<![^])"):
            boundary = cjs.compile({"pattern": f"^{prefix}{dots}\\b"})
            not_boundary = cjs.compile({"pattern": f"^{prefix}{dots}\\B"})
            for t in set(texts):
                expected = None if at > len(t) else word(t, at - 1) != word(t, at)
                assert boundary(t) is (expected is True), f"\\b at {at} of {t!r}"
                assert not_boundary(t) is (expected is False), f"\\B at {at} of {t!r}"


def _on_both_engines(pattern: str) -> list[str]:
    # The pattern, and the pattern after an empty lookbehind, which sends it to the regex package.
    return [pattern, "(?<=)" + pattern]


@pytest.mark.parametrize(
    ("pattern", "text", "expected"),
    [
        # V8's answers. A reference to a group that has not matched matches the empty string: the group is in
        # another alternative, comes later, is the one the reference is in, or was reset by its repetition, which
        # every iteration does at its start.
        ("^(?:(a)|b)\\1$", "b", True),
        ("^(?:(a)|b)\\1$", "aa", True),
        ("^(?:(a)|b)\\1$", "a", False),
        ("^(?:(a)|b)\\1$", "ba", False),
        ("^\\1(a)$", "a", True),
        ("^\\1(a)$", "aa", False),
        ("^(a\\1)$", "a", True),
        ("^(a\\1)$", "aa", False),
        ("^(?:(a)|\\1){1}$", "", True),
        ("^(?:(a)|\\1){1}$", "a", True),
        ("^(?:(a)|\\1){4}$", "", True),
        ("^(?:(a)|\\1){4}$", "a", True),
        ("^(?:(a)|\\1){4}$", "aa", True),
        ("^(?:(a)|\\1){4}$", "aaa", True),
        ("^(?:(a)|\\1){4}$", "aaaa", True),
        ("^(?:(a)|\\1){4}$", "aaaaa", False),
        ("^(?:\\1(a)){2}$", "aa", True),
        ("^(?:\\1(a)){2}$", "aaa", False),
        ("^(?:\\1(a)){2}$", "a", False),
        ("^(?:(a)?\\1){2}x$", "x", True),
        ("^(?:(a)?\\1){2}x$", "aax", True),
        ("^(?:(a)?\\1){2}x$", "aaaax", True),
        ("^(?:(a)?\\1){2}x$", "ax", False),
        ("^(?:(a)|b)*\\1$", "ab", True),
        ("^(?:(a)|b)*\\1$", "aba", False),
        ("^(?:(a)|b)*\\1$", "baa", True),
        ("^(?:(a)|b)*\\1$", "", True),
        ("^(?:(a)|(b))+\\1\\2$", "ab", False),
        ("^(?:(a)|(b))+\\1\\2$", "aa", True),
        ("^(?:(a)|(b))+\\1\\2$", "abb", True),
        ("^(?:(a)|(b))+\\1\\2$", "bb", True),
        ("^(?:(a)(b)?)+\\2\\1$", "aba", False),
        ("^(?:(a)(b)?)+\\2\\1$", "abab", False),
        ("^(?:(a)(b)?)+\\2\\1$", "abaa", True),
        ("(?!(a))\\1b", "b", True),
        ("(?!(a))\\1b", "ab", True),
        ("^(a)?\\1b$", "b", True),
        ("^(a)?\\1b$", "aab", True),
        ("^(a)?\\1b$", "ab", False),
        ("^(?:(?=(a))a|b)*\\1$", "ab", True),
        ("^(?:(?=(a))a|b)*\\1$", "aa", True),
        ("^(?:(?=(a))a|b)*\\1$", "abaa", True),
        ("(?<=(a)\\1)b", "ab", True),
        ("(?<=(a)\\1)b", "b", False),
        ("(?<=\\1(a))b", "aab", True),
        ("(?<=\\1(a))b", "ab", False),
        ("^(?<n>a)?\\k<n>b$", "b", True),
        ("^(?<n>a)?\\k<n>b$", "aab", True),
        ("^(?<n>a)?\\k<n>b$", "ab", False),
        ("^\\k<n>(?<n>a)$", "a", True),
        ("^\\k<n>(?<n>a)$", "aa", False),
        ("^(['\"]).*\\1$", "'a'", True),
        ("^(['\"]).*\\1$", '"a"', True),
        ("^(['\"]).*\\1$", "'a\"", False),
        ("^(a)\\1{2}$", "aaa", True),
        ("^(a)\\1{2}$", "aa", False),
        ("^([^a]?a+){2,3}?\\1a$", "aaaa", True),
        ("^([^a]?a+){2,3}?\\1a$", "aaa", False),
        ("^([^a]?a+){2,3}?\\1a$", "aa", False),
        ("^(x|a{1,2}){3,}?\\1x", "aaaax", True),
        ("^(x|a{1,2}){3,}?\\1x", "aaax", False),
        ("^(x|a{1,2}){3,}?\\1x", "xxxxx", True),
    ],
)
def test_backreferences_have_ecma_semantics(pattern: str, text: str, expected: bool) -> None:
    for p in _on_both_engines(pattern):
        assert cjs.compile({"pattern": p})(text) is expected, p


def test_a_backreference_is_written_from_where_its_group_is() -> None:
    def written(pattern: str) -> tuple[str, bool]:
        t = translate(pattern)
        assert t is not None
        return t

    # The group has always matched when the reference is read: the standard library's engine runs the pattern.
    for pattern in ("^(a)\\1$", "^(['\"]).*\\1$", "^(?<n>\\w)-\\k<n>$", "^(a)(?:b|\\1)*$"):
        text, for_regex = written(pattern)
        assert "(?P=g1)" in text and not for_regex, pattern
    # Before CPython 3.11 that engine lost what a group inside a repetition held when it backtracked, so there the
    # regex package runs a pattern with a reference to such a group.
    text, for_regex = written("^(?:(a)\\1)+$")
    assert "(?P=g1)" in text and for_regex is (sys.version_info < (3, 11))
    # The reference can never be read after its group has matched: it is written as nothing.
    for pattern in ("^(a\\1)$", "^\\1(a)$", "^(?:(a)|\\1)+$", "^\\k<n>(?<n>a)$", "(?!(a))\\1b"):
        text, for_regex = written(pattern)
        assert "(?P" not in text and not for_regex, pattern
    # The group may have no value when the reference is read: the regex package runs the pattern, with the group
    # reset at the start of the pattern and of each iteration.
    for pattern in ("^(a)?\\1b$", "^(?:(a)|b)\\1$", "^(?:(a)|b)*\\1$"):
        text, for_regex = written(pattern)
        assert "(?P<g1>)" in text and for_regex, pattern
    assert written("^(?:(a)|b)*\\1$")[0].count("(?P<g1>)") == 2


@pytest.mark.parametrize(
    ("pattern", "text", "expected"),
    [
        # V8's answers (for a case-insensitive group, its answers for the same pattern with the i flag).
        # A case-insensitive group, with the u flag's simple case folding.
        ("a(?i:b)c", "abc", True),
        ("a(?i:b)c", "aBc", True),
        ("a(?i:b)c", "Abc", False),
        ("a(?i:b)c", "abC", False),
        ("(?i:a(?-i:b)c)", "AbC", True),
        ("(?i:a(?-i:b)c)", "ABC", False),
        ("^(?i:k)$", "k", True),
        ("^(?i:k)$", "K", True),
        ("^(?i:k)$", "\u212a", True),
        ("^(?i:k)$", "s", False),
        ("^(?i:s)$", "s", True),
        ("^(?i:s)$", "S", True),
        ("^(?i:s)$", "\u017f", True),
        ("^(?i:\\u017f)$", "s", True),
        ("^(?i:\\u017f)$", "S", True),
        ("^(?i:\\u017f)$", "\u017f", True),
        ("^(?i:[a-z])$", "K", True),
        ("^(?i:[a-z])$", "\u212a", True),
        ("^(?i:[a-z])$", "\u017f", True),
        ("^(?i:[a-z])$", "\u00e9", False),
        ("^(?i:[^a-z])$", "\u212a", False),
        ("^(?i:[^a-z])$", "\u00e9", True),
        ("^(?i:[^a-z])$", "K", False),
        ("^(?i:[^a-z])$", "1", True),
        ("^(?i:\\w)$", "\u017f", True),
        ("^(?i:\\w)$", "\u212a", True),
        ("^(?i:\\w)$", "k", True),
        ("^(?i:\\w)$", "\u00e9", False),
        ("^(?i:\\W)$", "\u017f", False),
        ("^(?i:\\W)$", "\u212a", False),
        ("^(?i:\\W)$", "\u00e9", True),
        ("^(?i:\\W)$", "k", False),
        ("^(?i:[\\W])$", "\u017f", False),
        ("^(?i:[\\W])$", "\u212a", False),
        ("^(?i:[\\W])$", "\u00e9", True),
        ("^(?i:[\\W])$", "k", False),
        ("^(?i:[^\\W])$", "\u017f", True),
        ("^(?i:[^\\W])$", "\u212a", True),
        ("^(?i:[^\\W])$", "\u00e9", False),
        ("^(?i:[^\\W])$", "k", True),
        ("^(?i:\u00df)$", "\u00df", True),
        ("^(?i:\u00df)$", "\u1e9e", True),
        ("^(?i:\u00df)$", "ss", False),
        ("^(?i:\u00df)$", "SS", False),
        ("^(?i:\u03c3)$", "\u03c3", True),
        ("^(?i:\u03c3)$", "\u03c2", True),
        ("^(?i:\u03c3)$", "\u03a3", True),
        ("^(?i:\u03c3)$", "s", False),
        ("^(?i:i)$", "i", True),
        ("^(?i:i)$", "I", True),
        ("^(?i:i)$", "\u0130", False),
        ("^(?i:i)$", "\u0131", False),
        ("^(?i:\u0130)$", "i", False),
        ("^(?i:\u0130)$", "I", False),
        ("^(?i:\u0130)$", "\u0130", True),
        ("^(?i:\u0130)$", "\u0131", False),
        ("^(?i:\u01c5)$", "\u01c4", True),
        ("^(?i:\u01c5)$", "\u01c5", True),
        ("^(?i:\u01c5)$", "\u01c6", True),
        ("^(?i:\u01c5)$", "D", False),
        ("^(?i:\U00010400)$", "\U00010400", True),
        ("^(?i:\U00010400)$", "\U00010428", True),
        ("^(?i:\U00010400)$", "\U00010401", False),
        ("^(?i:\u00b5)$", "\u00b5", True),
        ("^(?i:\u00b5)$", "\u03bc", True),
        ("^(?i:\u00b5)$", "\u039c", True),
        ("^(?i:\u00b5)$", "m", False),
        ("^(?i:[\u00e0-\u00fe])+$", "\u00c9\u00e9", True),
        ("^(?i:[\u00e0-\u00fe])+$", "\u00d7", False),
        ("^(?i:[\u00e0-\u00fe])+$", "\u00f7", True),
        ("^(?i:[\u00e0-\u00fe])+$", "\u1e9e", False),
        ("^(?i:\\p{Lu})+$", "aA", True),
        ("^(?i:\\p{Lu})+$", "\u01c5", True),
        ("^(?i:\\p{Lu})+$", "1", False),
        ("^(?i:\\P{Lu})+$", "aA", True),
        ("^(?i:\\P{Lu})+$", "1", True),
        ("^(?i:\\P{Lu})+$", "\u01c5", True),
        ("^(?i:[^\\p{Lu}])+$", "a", False),
        ("^(?i:[^\\p{Lu}])+$", "A", False),
        ("^(?i:[^\\p{Lu}])+$", "1", True),
        ("(?i:a\\b)", "A\u017f", False),
        ("(?i:a\\b)", "A-", True),
        ("(?i:a\\b)", "a\u212a", False),
        ("(?i:a\\b)", "a", True),
        ("(?i:a\\B)", "A\u017f", True),
        ("(?i:a\\B)", "A-", False),
        ("(?i:a\\B)", "a\u212a", True),
        ("(?i:a\\B)", "a", False),
        ("^(a)(?i:\\1)$", "aA", True),
        ("^(a)(?i:\\1)$", "aa", True),
        ("^(a)(?i:\\1)$", "ab", False),
        ("^(?i:(a)\\1)$", "aA", True),
        ("^(?i:(a)\\1)$", "Aa", True),
        ("^(?i:(a)\\1)$", "AB", False),
        ("^(.)(?i:\\1)$", "s\u017f", True),
        ("^(.)(?i:\\1)$", "k\u212a", True),
        ("^(.)(?i:\\1)$", "\u00e9\u00c9", True),
        ("^(.)(?i:\\1)$", "ab", False),
        # Without the u flag (\& is an escape only that grammar has), by the uppercase form of a UTF-16 code unit.
        ("^(?i:k)\\&?$", "k", True),
        ("^(?i:k)\\&?$", "K", True),
        ("^(?i:k)\\&?$", "\u212a", False),
        ("^(?i:s)\\&?$", "S", True),
        ("^(?i:s)\\&?$", "\u017f", False),
        ("^(?i:\u00df)\\&?$", "\u00df", True),
        ("^(?i:\u00df)\\&?$", "\u1e9e", False),
        ("^(?i:[a-z])\\&?$", "K", True),
        ("^(?i:[a-z])\\&?$", "\u017f", False),
        ("^(?i:[a-z])\\&?$", "\u212a", False),
        ("^(?i:\\w)\\&?$", "k", True),
        ("^(?i:\\w)\\&?$", "\u017f", False),
        ("^(?i:\\w)\\&?$", "\u212a", False),
        ("^(?i:[^\\W])\\&?$", "k", True),
        ("^(?i:[^\\W])\\&?$", "\u017f", False),
        ("^(?i:[^\\W])\\&?$", "\u212a", False),
        ("^(?i:\u01c5)\\&?$", "\u01c4", True),
        ("^(?i:\u01c5)\\&?$", "\u01c6", True),
        ("^(?i:\u01c5)\\&?$", "\u01c5", True),
        ("^(?i:\u00b5)\\&?$", "\u03bc", True),
        ("^(?i:\u00b5)\\&?$", "\u039c", True),
        ("^(?i:\u00b5)\\&?$", "\u00b5", True),
        ("^(?i:\u03c3)\\&?$", "\u03c2", True),
        ("^(?i:\u03c3)\\&?$", "\u03a3", True),
        ("(?i:a\\b)\\&?", "A\u017f", True),
        ("(?i:a\\b)\\&?", "A-", True),
        # Inside m, ^ and $ hold at each of the four line terminators and nowhere else.
        ("^a(?m:$)", "a", True),
        ("^a(?m:$)", "a\n", True),
        ("^a(?m:$)", "a\r", True),
        ("^a(?m:$)", "a\u2028", True),
        ("^a(?m:$)", "a\u2029", True),
        ("^a(?m:$)", "a\u0085", False),
        ("^a(?m:$)", "ab", False),
        ("(?m:^)a$", "a", True),
        ("(?m:^)a$", "\na", True),
        ("(?m:^)a$", "\ra", True),
        ("(?m:^)a$", "\u2028a", True),
        ("(?m:^)a$", "\u2029a", True),
        ("(?m:^)a$", "\u0085a", False),
        ("(?m:^)a$", "ba", False),
        ("(?m:^a$)", "b\na\rb", True),
        ("(?m:^a$)", "ba\n", False),
        ("(?m:^a$)", "\u2028a\u2029", True),
        ("(?m:^)(?-m:^)a", "a", True),
        ("(?m:^)(?-m:^)a", "\na", False),
        ("^a$", "a\n", False),
        ("^a$", "\na", False),
        # Inside s, the dot is any character. Outside it, any but the four line terminators.
        ("^a(?s:.)b$", "a\nb", True),
        ("^a(?s:.)b$", "a\rb", True),
        ("^a(?s:.)b$", "a\u2028b", True),
        ("^a(?s:.)b$", "a\u2029b", True),
        ("^a(?s:.)b$", "axb", True),
        ("^a(?s:.)b$", "ab", False),
        ("^a(?s:(?-s:.))b$", "a\nb", False),
        ("^a(?s:(?-s:.))b$", "a\u0085b", True),
        ("^(?s-i:.)$", "\n", True),
        ("^(?s-i:.)$", "\u2029", True),
        ("^(?ims:a.^B$)", "A\nb", True),
        ("^(?ims:a.^B$)", "a\rB\n", True),
        ("^(?ims:a.^B$)", "a\nb\u2028c", True),
        ("^(?ims:a.^B$)", "a-b", False),
    ],
)
def test_modifier_groups_have_ecma_semantics(pattern: str, text: str, expected: bool) -> None:
    for p in _on_both_engines(pattern):
        assert cjs.compile({"pattern": p})(text) is expected, p


def test_a_modifier_group_names_each_modifier_once_and_at_least_one() -> None:
    for pattern in ("(?i:a)", "(?i-:a)", "(?-i:a)", "(?ims:a)", "(?s-im:a)", "(?m:(?-m:a))", "(?i:a)\\&"):
        assert compile_pattern(pattern) is not None, pattern
    for pattern in ("(?-:a)", "(?ii:a)", "(?i-i:a)", "(?i--m:a)", "(?x:a)", "(?i)a", "(?i:a", "(?u:a)"):
        assert compile_pattern(pattern) is None, pattern


def test_every_character_matches_its_case_variants_in_a_case_insensitive_group() -> None:
    # The sets of _casefold.py, which are checked against V8 when the file is made. A member, as a literal and in
    # a class, matches every member of its set and no member of the next one. \& makes a pattern one that is valid
    # only without the u flag.
    from corvus_json_schema import _casefold

    for table, legacy in ((_casefold.UNICODE, ""), (_casefold.LEGACY, "\\&?")):
        sets = table.split(" ")
        assert len(sets) > 1000
        for k in range(0, len(sets), 7):
            members, others = sets[k], sets[(k + 1) % len(sets)]
            for pattern in (f"^(?i:{members[0]}){legacy}$", f"^(?i:[{members[-1]}]){legacy}$"):
                matcher = compile_pattern(pattern)
                assert matcher is not None, pattern
                for c in members:
                    assert matcher(c), f"{pattern} on U+{ord(c):04X}"
                for c in others:
                    assert not matcher(c), f"{pattern} on U+{ord(c):04X}"


@pytest.mark.parametrize(
    ("pattern", "text", "expected"),
    [
        # V8's answers.
        # An iteration that matches nothing is refused once the minimum count is met, so it leaves no group set.
        ("^(?:(a?))+\\1b$", "ab", False),
        ("^(?:(a?))+\\1b$", "aab", True),
        ("^(?:(a?))+\\1b$", "b", True),
        ("^(?:(?=(a)))?\\1a", "a", True),
        ("^(?:(?=(a)))?\\1a", "aa", True),
        ("^(?:(?=(a)))?\\1b", "ab", False),
        ("^(?:(?=(a)))?\\1b", "b", True),
        ("^(?:(a)|())*\\1b$", "ab", False),
        ("^(?:(a)|())*\\1b$", "aab", True),
        ("^(?:(a)|())*\\1b$", "b", True),
        ("^(?:(a)|b|){2,4}\\1$", "ab", True),
        ("^(?:(a)|b|){2,4}\\1$", "aba", False),
        ("^(?:(a)|b|){2,4}\\1$", "aa", True),
        ("^(?:(a)|b|){2,4}\\1$", "abab", True),
        ("^(a|b|\u00e9)*?(?=\\1)", "a", True),
        ("^(a|b|\u00e9)*?(?=\\1)", "ab", True),
        ("^(a|b|\u00e9)*?(?=\\1)", "", True),
        ("(?<=\\1(?:(a)|b|)*)c", "abc", True),
        ("(?<=\\1(?:(a)|b|)*)c", "c", True),
        ("(?<=\\1(?:(a)|b|)*)c", "aac", True),
        ("^(x$|(?:(a|)||.$){4,6}(|){2})?((?:|(?:(?!$))*?){3,}x|(?:[^a]-?|\\3){3,5}\\2)?$", "axa", True),
        ("^(x$|(?:(a|)||.$){4,6}(|){2})?((?:|(?:(?!$))*?){3,}x|(?:[^a]-?|\\3){3,5}\\2)?$", "a-a", True),
        ("^(x$|(?:(a|)||.$){4,6}(|){2})?((?:|(?:(?!$))*?){3,}x|(?:[^a]-?|\\3){3,5}\\2)?$", "aa-a", True),
        ("^(x$|(?:(a|)||.$){4,6}(|){2})?((?:|(?:(?!$))*?){3,}x|(?:[^a]-?|\\3){3,5}\\2)?$", "ax", True),
        # A repetition that can only match nothing runs once, or not at all for a minimum of 0.
        ("^(?:(?=(a))){0,}\\1a$", "a", True),
        ("^(?:(?=(a))){0,}\\1a$", "aa", False),
        ("^(?:(?=(a))){2,}\\1$", "aa", False),
        ("^(?:(?=(a))){2,}\\1$", "a", True),
        ("^(?:()|(?=x)){3}\\1x$", "x", True),
        ("^(?:()|(?=x)){3}\\1x$", "xx", False),
        # The regex package does not see a reference inside a repetition with a maximum.
        ("^(?:(?:(a|)|){2})?(?:x\\1)?$", "axa", True),
        ("^(?:(?:(a|)|){2})?(?:x\\1)?$", "ax", True),
        ("^(?:(?:(a|)|){2})?(?:x\\1)?$", "x", True),
        ("^(?:(?:(a|)|){2})?(?:x\\1)?$", "xa", False),
        ("(?<=^(?:(?:(a|)|){2})?(?:x\\1)?)$", "axa", False),
        ("(?<=^(?:(?:(a|)|){2})?(?:x\\1)?)$", "ax", True),
        ("(?<=^(?:(?:(a|)|){2})?(?:x\\1)?)$", "xa", False),
        # Two groups with one name, in separate alternatives.
        ("^(?:(?<a>x)|(?<a>y))\\k<a>$", "xx", True),
        ("^(?:(?<a>x)|(?<a>y))\\k<a>$", "yy", True),
        ("^(?:(?<a>x)|(?<a>y))\\k<a>$", "xy", False),
        ("^(?:(?<a>x)|(?<a>y))\\k<a>$", "x", False),
        ("^(?:(?<a>x)|(?<a>y))+\\k<a>$", "xyy", True),
        ("^(?:(?<a>x)|(?<a>y))+\\k<a>$", "xyx", False),
        ("^(?:(?<a>x)|(?<a>y))+\\k<a>$", "yxx", True),
        ("^(?:(?<a>x)|(?<a>y)|z)*\\k<a>$", "xz", True),
        ("^(?:(?<a>x)|(?<a>y)|z)*\\k<a>$", "xzx", False),
        ("^(?:(?<a>x)|(?<a>y)|z)*\\k<a>$", "z", True),
        ("^(?:(?<a>x)|(?<a>y)|z)*\\k<a>$", "yzy", False),
        ("\\k<a>(?:(?<a>x)|(?<a>y))", "x", True),
        ("\\k<a>(?:(?<a>x)|(?<a>y))", "y", True),
        ("\\k<a>(?:(?<a>x)|(?<a>y))", "z", False),
        # A group name is an identifier, and may have \u escapes.
        ("^(?<\\u0061>a)\\k<a>$", "aa", True),
        ("^(?<\\u0061>a)\\k<a>$", "a", False),
        ("^(?<\u03c0>a)\\k<\\u03c0>$", "aa", True),
        ("^(?<\u03c0>a)\\k<\\u03c0>$", "a", False),
        ("^(?<\\u{61}b>a)\\k<ab>$", "aa", True),
        ("^(?<\\u{61}b>a)\\k<ab>$", "a", False),
        ("^(?<$_a1>a)\\k<$_a1>$", "aa", True),
        ("^(?<$_a1>a)\\k<$_a1>$", "a", False),
        # An empty class matches nothing, also where the regex package runs the pattern.
        ("(?<=a)[]", "ab", False),
        ("(?<=a)[]", "a", False),
        ("(?<=a)(?:[]|b)", "ab", True),
        ("(?<=a)(?:[]|b)", "ac", False),
        ("^[]*$", "", True),
        ("^[]*$", "a", False),
        ("(?<=a)[^]", "ab", True),
        ("(?<=a)[^]", "a", False),
    ],
)
def test_empty_iterations_and_group_names_have_ecma_semantics(pattern: str, text: str, expected: bool) -> None:
    for p in _on_both_engines(pattern):
        assert cjs.compile({"pattern": p})(text) is expected, p


def test_a_group_name_that_is_not_an_identifier_or_is_given_twice_is_refused() -> None:
    for name in ("1a", "a b", "a\\u0020", "\\u0030", "\\x61", "\\u", "\\u{}", "‍", "a-b", "a.b", "\U0001f600", ""):
        assert compile_pattern(f"(?<{name}>a)") is None, name
    for pattern in ("(?<a>x)(?<a>y)", "(?<a>x)|(?:(?<a>y)(?<a>z))", "(?<a>(?<a>x))", "(?<a>x)\\k<b>"):
        assert compile_pattern(pattern) is None, pattern
    for pattern in ("(?<a>x)|(?<a>y)", "(?:(?<a>x)|(?<a>y)){2}\\k<a>", "(?<a>x)|(?=(?<a>y))", "(?<a\\u0030>a)\\k<a0>"):
        assert compile_pattern(pattern) is not None, pattern


def test_a_valid_pattern_too_large_to_write_out_is_refused_and_is_a_valid_regex() -> None:
    # A repetition with a reference to what it holds is written without a count, so a count inside a count
    # multiplies out.
    pattern = "^(?:(?:(a)|b){300}){300}\\1$"
    with pytest.raises(cjs.SchemaCompilationError, match="too large"):
        cjs.compile({"pattern": pattern})
    assert is_valid_ecma_regex(pattern)
    assert cjs.compile({"pattern": "^(?:(a)|b){300}\\1$"})("ab" * 150) is True


@pytest.mark.parametrize(
    ("pattern", "text", "expected"),
    [
        # V8's answers. A reference to a name that two groups have is a reference to each, and a count after it is
        # for both.
        ("^(?:(?<a>x)\\k<a>{2}|(?<a>y))$", "xxx", True),
        ("^(?:(?<a>x)\\k<a>{2}|(?<a>y))$", "xx", False),
        ("^(?:(?<a>x)\\k<a>{2}|(?<a>y))$", "y", True),
        ("^(?:(?<a>x)\\k<a>{2}|(?<a>y))$", "x", False),
        ("^(?:(?<a>x)\\k<a>*z|(?<a>y)\\k<a>+)$", "xxxz", True),
        ("^(?:(?<a>x)\\k<a>*z|(?<a>y)\\k<a>+)$", "xz", True),
        ("^(?:(?<a>x)\\k<a>*z|(?<a>y)\\k<a>+)$", "yy", True),
        ("^(?:(?<a>x)\\k<a>*z|(?<a>y)\\k<a>+)$", "y", False),
        ("^(?:(?<a>x)\\k<a>*z|(?<a>y)\\k<a>+)$", "yx", False),
        ("^(?:(?<a>x)|(?<a>y))\\k<a>{2}$", "xxx", True),
        ("^(?:(?<a>x)|(?<a>y))\\k<a>{2}$", "yyy", True),
        ("^(?:(?<a>x)|(?<a>y))\\k<a>{2}$", "xx", False),
        ("^(?:(?<a>x)|(?<a>y))\\k<a>{2}$", "xyy", False),
    ],
)
def test_a_count_after_a_reference_to_a_name_of_two_groups_is_for_the_whole_reference(
    pattern: str, text: str, expected: bool
) -> None:
    for p in _on_both_engines(pattern):
        assert cjs.compile({"pattern": p})(text) is expected, p


@pytest.mark.parametrize(
    ("pattern", "text", "expected"),
    [
        # V8's answers.
        # These took the regex package's memory: a group that is set again by an iteration that takes no character.
        ("(?:(?=.(a.|){3}|[^a]x|\\1)(?<=a)|[ax][^a])*?x$", "aaax", True),
        ("(?:(?=.(a.|){3}|[^a]x|\\1)(?<=a)|[ax][^a])*?x$", "aaa", False),
        ("(?:(?=.(a.|){3}|[^a]x|\\1)(?<=a)|[ax][^a])*?x$", "x", True),
        ("(?:|(?<!a)){4,5}(?:(?=([^a]a){2}(?<=a)|.|\\1)|(?=x)|()){1,}?x$", "xaxax", True),
        ("(?:|(?<!a)){4,5}(?:(?=([^a]a){2}(?<=a)|.|\\1)|(?=x)|()){1,}?x$", "aaa", False),
        ("(?:|(?<!a)){4,5}(?:(?=([^a]a){2}(?<=a)|.|\\1)|(?=x)|()){1,}?x$", "x", True),
        ("(?:(?=(.){2}\\1)|b)*(?<=x)$", "axx", True),
        ("(?:(?=(.){2}\\1)|b)*(?<=x)$", "axy", False),
        ("(?<=^(?:(?=(.){2})|b)*\\1)x", "x", True),
        ("(?<=^(?:(?=(.){2})|b)*\\1)x", "bx", True),
        ("(?<=^(?:(?=(.){2})|b)*\\1)x", "ax", False),
        # A count no text can reach.
        ("^a{99999999999999999999}$", "a", False),
        ("^a{99999999999999999999}$", "", False),
        ("^a{0,99999999999999999999}$", "aaa", True),
        ("^a{0,99999999999999999999}$", "", True),
        ("^a{0,99999999999999999999}$", "b", False),
        ("^a{2,99999999999999999999}$", "aaa", True),
        ("^a{2,99999999999999999999}$", "a", False),
        ("(?<=b)a{99999999999999999999}", "ba", False),
    ],
)
def test_patterns_that_raised_from_inside_an_engine_have_answers(pattern: str, text: str, expected: bool) -> None:
    for p in _on_both_engines(pattern):
        assert cjs.compile({"pattern": p})(text) is expected, p


def test_a_minimum_no_text_reaches_over_a_body_that_can_match_nothing_is_refused() -> None:
    # That many iterations, nearly all of which match nothing, are beyond both engines. The pattern is valid.
    pattern = "(?<=b)(?:a|){99999999999999999999}"
    with pytest.raises(cjs.SchemaCompilationError, match="too large"):
        cjs.compile({"pattern": pattern})
    assert is_valid_ecma_regex(pattern)


def test_standalone_modules_collect_the_same_results_as_compile(tmp_path: Path) -> None:
    schema = {"type": "object", "properties": {"a": {"type": "string", "title": "A"}}, "required": ["b"]}
    mod = import_module(cjs.generate_module(schema), tmp_path)
    direct = cjs.compile(schema)
    for level in (cjs.ResultsLevel.BASIC, cjs.ResultsLevel.DETAILED, cjs.ResultsLevel.VERBOSE):
        a = cjs.JsonSchemaResultsCollector.create(level)
        b = cjs.JsonSchemaResultsCollector.create(level)
        assert mod.evaluate({"a": 1}, a) == direct.evaluate({"a": 1}, b)
        assert a.results == b.results
    assert "def evaluate" not in cjs.generate_module(schema, collecting=False)


def test_one_schema_object_used_at_two_locations_keeps_both_locations() -> None:
    leaf = {"type": "string"}
    v = cjs.compile({"properties": {"a": leaf, "b": leaf}})
    assert v({"a": "x", "b": 1}) is False
    c = cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.DETAILED)
    v.evaluate({"a": "x", "b": 1}, c)
    assert any(
        r.schema_evaluation_location == "/properties/b/type" and r.document_evaluation_location == "/b"
        for r in c.results
    )


def _root_function(source: str, name: str = "v0") -> str:
    m = re.search(rf"^def {name}\(x\):\n(?:    .*\n?)*", source, re.M)
    assert m is not None
    return m.group(0)


def test_all_of_and_ref_compositions_of_plain_objects_check_an_object_in_one_pass() -> None:
    base = {
        "type": "object",
        "properties": {"id": {"type": "integer"}, "tags": {"type": "array"}},
        "required": ["id"],
        "maxProperties": 4,
    }
    flat = {
        "$defs": {"base": base},
        "allOf": [
            {"$ref": "#/$defs/base"},
            {"properties": {"name": {"type": "string"}, "id": {"type": "integer"}}, "required": ["name"]},
        ],
        "properties": {"active": True},
        "minProperties": 2,
    }
    # The root function holds every branch's names: one section, no call per branch.
    root = _root_function(cjs.compile(flat).source)
    for name in ("id", "tags", "name"):
        assert repr(name) in root, name

    conflicting = {"allOf": [{"properties": {"id": {"type": "integer"}}}, {"properties": {"id": {"minimum": 1}}}]}
    non_object_branch = {
        "allOf": [
            {"properties": {"id": {"type": "integer"}}},
            {"minLength": 2, "properties": {"n": {"type": "string"}}},
        ]
    }
    for schema in (flat, conflicting, non_object_branch):
        v = cjs.compile(schema)
        for instance in [
            {"id": 1, "name": "a"},
            {"id": 1, "name": "a", "tags": [], "active": 1},
            {"id": 1, "name": "a", "tags": [], "active": 1, "other": 2},
            {"id": "x", "name": "a"},
            {"id": 1, "name": 2},
            {"id": 0},
            {"name": "a"},
            {"id": 1, "n": "a"},
            {"id": 1, "n": 1},
            {},
            "ab",
            "a",
            [],
            None,
        ]:
            collected = v.evaluate(instance, cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.BASIC))
            assert v(instance) == collected, f"{json.dumps(schema)} on {json.dumps(instance)}"
    assert cjs.compile(flat)({"id": 1, "name": "a", "tags": [], "active": 1, "other": 2}) is False
    assert cjs.compile(flat)({"id": 1}) is False
    assert cjs.compile(non_object_branch)("a") is False


def test_unevaluated_properties_under_conditionals_is_decided_without_run_time_tracking() -> None:
    parameter = {
        "type": "object",
        "properties": {
            "name": {"type": "string"},
            "in": {"enum": ["query", "path", "header"]},
            "schema": True,
            "content": True,
        },
        "required": ["name", "in"],
        "if": {"properties": {"in": {"const": "query"}}, "required": ["in"]},
        "then": {"properties": {"allowEmptyValue": {"type": "boolean"}}},
        "dependentSchemas": {
            "schema": {
                "properties": {"style": {"type": "string"}},
                "allOf": [
                    {
                        "if": {"properties": {"style": {"const": "form"}}, "required": ["style"]},
                        "then": {"properties": {"explode": {"type": "boolean"}}},
                        "else": {"properties": {"explode": {"const": False}}},
                    }
                ],
            }
        },
        "unevaluatedProperties": False,
    }
    patterns = {
        "properties": {"kind": True},
        "if": {"properties": {"kind": {"const": "ext"}}},
        "then": {"patternProperties": {"^x-": {"type": "string"}}},
        "else": {"if": {"required": ["legacy"]}, "then": {"properties": {"legacy": True, "old": {"type": "integer"}}}},
        "unevaluatedProperties": {"type": "number"},
    }
    alternatives = {
        "properties": {"a": True},
        "anyOf": [{"properties": {"b": True}}, {"required": ["c"]}],
        "unevaluatedProperties": False,
    }
    for schema in (parameter, patterns):
        assert "= set()" not in cjs.compile(schema).source, json.dumps(schema)
    for schema in (parameter, patterns, alternatives):
        v = cjs.compile(schema)
        for instance in [
            {"name": "q", "in": "query"},
            {"name": "q", "in": "query", "allowEmptyValue": True},
            {"name": "q", "in": "path", "allowEmptyValue": True},
            {"name": "q", "in": "query", "schema": {}, "style": "form", "explode": True},
            {"name": "q", "in": "query", "schema": {}, "style": "simple", "explode": True},
            {"name": "q", "in": "query", "schema": {}, "style": "simple", "explode": False},
            {"name": "q", "in": "query", "style": "form", "explode": True},
            {"name": "q", "in": "query", "schema": {}, "other": 1},
            {"kind": "ext", "x-a": "y"},
            {"kind": "ext", "x-a": 1},
            {"kind": "other", "x-a": "y"},
            {"kind": "other", "x-a": 2},
            {"legacy": 1, "old": 2},
            {"legacy": 1, "old": "two"},
            {"old": 2},
            {"a": 1, "b": 2},
            {"a": 1, "c": 2},
            {"a": 1, "b": 2, "c": 3},
            {},
            [],
            "x",
        ]:
            collected = v.evaluate(instance, cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.BASIC))
            assert v(instance) == collected, f"{json.dumps(schema)} on {json.dumps(instance)}"


def test_many_declared_properties_dispatch_through_a_table() -> None:
    names = [f"p{i}" for i in range(20)]
    schema = {
        "type": "object",
        "properties": {
            n: {"type": "integer"} if i % 2 else {"type": "string", "minLength": 1} for i, n in enumerate(names)
        },
        "patternProperties": {"^x-": {"type": "boolean"}},
        "additionalProperties": False,
    }
    v = cjs.compile(schema)
    assert ".get(k)" in v.source
    assert v({"p0": "a", "p1": 1, "x-y": True}) is True
    assert v({"p0": "", "p1": 1}) is False
    assert v({"p1": "a"}) is False
    assert v({"x-y": 1}) is False
    assert v({"q": 1}) is False


def test_validators_with_module_state_are_safe_across_threads() -> None:
    import threading

    tree = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/tree",
        "$dynamicAnchor": "node",
        "type": "object",
        "properties": {"data": True, "children": {"type": "array", "items": {"$dynamicRef": "#node"}}},
    }
    strict = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/strict-tree",
        "$dynamicAnchor": "node",
        "$ref": "tree",
        "unevaluatedProperties": False,
    }
    documents = {"https://example.com/tree": tree, "https://example.com/strict-tree": strict}
    # The entry resource does not define the anchor, so the reference stays dynamic (a scope is kept at run time).
    root = {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "https://example.com/root",
        "$ref": "strict-tree",
    }
    v = cjs.compile(root, resolve_document=documents.get)
    assert "LOCK" in v.source and "DS.append" in v.source
    ok = {"children": [{"data": 1, "children": [{"data": 2, "children": []}] * 20}] * 20}
    bad = {"children": [{"data": 1, "children": [{"daat": 2}]}]}
    errors: list[str] = []

    def work() -> None:
        try:
            for _ in range(200):
                if v(ok) is not True or v(bad) is not False:
                    errors.append("wrong result")
        except Exception as e:  # the failure is the finding
            errors.append(repr(e))

    threads = [threading.Thread(target=work) for _ in range(8)]
    for t in threads:
        t.start()
    for t in threads:
        t.join()
    assert errors == []


def test_a_custom_format_can_reenter_the_validator() -> None:
    holder: dict[str, cjs.Validator] = {}

    def nested(s: str) -> bool:
        # Validates a JSON document embedded in the string with the same validator.
        return holder["v"](json.loads(s)) if s.startswith("{") else True

    schema = {
        "$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}},
        "type": "object",
        "properties": {"inner": {"type": "string", "format": "nested"}, "x": {"$ref": "#/$defs/loop"}},
    }
    v = cjs.compile(schema, assert_format=True, formats={"nested": nested}, max_depth=8)
    holder["v"] = v
    assert "LOCK" in v.source
    assert v({"inner": json.dumps({"inner": "plain"})}) is True
    with pytest.raises(cjs.SchemaEvaluationDepthError):
        v({"x": 1})
    assert v({"inner": "plain"}) is True


def test_scratch_names_never_shadow_module_constants() -> None:
    # The contains count is a local of the function that also reads the enum's constant: their names must differ.
    schema = {
        "prefixItems": [{"type": "string", "pattern": "^a.*b"}],
        "contains": {"type": "string"},
        "enum": [["ab"], ["axb", 1]],
    }
    v = cjs.compile(schema)
    assert v(["ab"]) is True
    assert v(["axb", 1]) is True
    assert v(["zz"]) is False
    assert v(["ab", "c"]) is False
