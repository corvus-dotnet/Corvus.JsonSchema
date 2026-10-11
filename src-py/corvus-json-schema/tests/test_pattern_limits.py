"""The limits on a regular expression. A schema and an instance may both come from outside, so a match of a pattern
has a limit of time (one second unless ``pattern_match_limit`` says otherwise), and a pattern has limits of size.
Reaching either raises an exception of its own, and is never an answer.

No test here needs a quiet machine. A match that is to reach its limit would run for longer than the age of the
universe, the time until the limit is measured on the processor (which is what the regex package counts), and the
upper bounds on the clock are many times what a quiet machine takes.
"""

from __future__ import annotations

import importlib.util
import sys
import threading
import time
import tracemalloc
import types
from pathlib import Path
from typing import Any

import pytest

import corvus_json_schema as cjs
from corvus_json_schema import pattern as pattern_module
from corvus_json_schema.collecting import load_program, serialize_program
from corvus_json_schema.pattern import compile_pattern, is_valid_ecma_regex

# The names and the pattern of the tests of the package over the Rust crate (corvus-json-schema-rs), which has the
# same option and the same two exceptions.
NEVER_ANSWERS = r"^(?:(a+)\1*)+$"
NEVER_ANSWERED = "a" * 60 + "b"


def _engine(matcher: Any) -> str:
    """Which engine a search function belongs to: "re", or "regex" (with the limit of a match around it or not)."""
    owner = getattr(matcher, "__self__", None)
    if owner is None:
        cells = [c.cell_contents for c in matcher.__closure__ or ()]
        owner = next(getattr(c, "__self__", None) for c in cells if callable(c))
    return type(owner).__module__.split(".")[0].lstrip("_")


def _raises_at_the_limit(check: Any, limit: float) -> cjs.PatternMatchLimitError:
    wall, cpu = time.monotonic(), time.process_time()
    with pytest.raises(cjs.PatternMatchLimitError) as raised:
        check()
    wall, cpu = time.monotonic() - wall, time.process_time() - cpu
    # The engine counts the time of the process on the processor. It is at the limit, and within twice the limit of
    # it but for what the test itself costs (the collectors, pytest). The clock on the wall only has to show that
    # nothing hung.
    assert limit * 0.9 <= cpu < 2 * limit + 2, (cpu, limit)
    assert wall < 60, wall
    return raised.value


# ---------------------------------------------------------------------------------------------------------------------
# The limit of a match


def test_a_match_that_reaches_its_limit_raises_from_every_way_in() -> None:
    v = cjs.compile({"pattern": NEVER_ANSWERS}, pattern_match_limit=0.05)
    checks = [
        lambda: v(NEVER_ANSWERED),
        lambda: v.evaluate(NEVER_ANSWERED),
        *[
            (lambda level=level: v.evaluate(NEVER_ANSWERED, cjs.JsonSchemaResultsCollector.create(level)))
            for level in cjs.ResultsLevel
        ],
    ]
    for check in checks:
        error = _raises_at_the_limit(check, 0.05)
        assert NEVER_ANSWERS in str(error) and "limit of 50ms." in str(error)
        assert error.pattern == NEVER_ANSWERS and error.limit == 0.05
    # It is not an answer and not one of the other errors, and the validator is as it was.
    assert not issubclass(cjs.PatternMatchLimitError, (cjs.SchemaCompilationError, cjs.SchemaEvaluationDepthError))
    assert v("aaaa") is True and v("b") is False


def test_the_limit_holds_for_property_names_and_in_any_mode_of_collection() -> None:
    schema = {"patternProperties": {NEVER_ANSWERS: {"type": "integer"}}, "additionalProperties": False}
    v = cjs.compile(schema, cjs.CompileOptions(pattern_match_limit=0.05))
    _raises_at_the_limit(lambda: v({NEVER_ANSWERED: 1}), 0.05)
    for level in cjs.ResultsLevel:
        collector = cjs.JsonSchemaResultsCollector.create(level)
        _raises_at_the_limit(lambda collector=collector: v.evaluate({NEVER_ANSWERED: 1}, collector), 0.05)
    assert v({"aa": 1}) is True and v({"aa": "x"}) is False and v({"b": 1}) is False


def test_the_default_limit_is_one_second() -> None:
    assert cjs.CompileOptions().pattern_match_limit == 1.0
    assert pattern_module.DEFAULT_MATCH_LIMIT == 1.0
    v = cjs.compile({"pattern": NEVER_ANSWERS})
    error = _raises_at_the_limit(lambda: v(NEVER_ANSWERED), 1.0)
    assert "limit of 1s." in str(error)
    assert "limit of 1.5s." in str(cjs.PatternMatchLimitError("a", 1.5))
    assert "limit of 250\u00b5s." in str(cjs.PatternMatchLimitError("a", 0.00025))


def test_the_limit_can_be_turned_off() -> None:
    # Off: the match runs to its end. Fourteen characters take a moment, and the answer is no.
    for off in (None, 0, 0.0, -1):
        v = cjs.compile({"pattern": NEVER_ANSWERS}, pattern_match_limit=off)
        assert v("a" * 14 + "b") is False
    v = cjs.compile({"pattern": NEVER_ANSWERS}, cjs.CompileOptions(pattern_match_limit=None))
    assert v("a" * 14 + "b") is False
    for bad in (float("nan"), "1", True):
        with pytest.raises(ValueError, match="not a number of seconds"):
            cjs.compile({"pattern": "a+"}, pattern_match_limit=bad)
    # With no limit the standard library runs every pattern it can, as it did before there was a limit.
    for pattern in ("^(a+)+$", "^(?:a|a)+$", "a*b", "^[a-z]+$"):
        assert _engine(compile_pattern(pattern, None)) == "re", pattern


def test_the_limit_holds_in_a_thread_that_is_not_the_main_one() -> None:
    v = cjs.compile({"pattern": NEVER_ANSWERS}, pattern_match_limit=0.05)
    seen: list[BaseException | bool] = []

    def work() -> None:
        try:
            seen.append(v(NEVER_ANSWERED))
        except cjs.PatternMatchLimitError as e:
            seen.append(e)

    thread = threading.Thread(target=work)
    thread.start()
    thread.join(60)
    assert not thread.is_alive()
    assert len(seen) == 1 and isinstance(seen[0], cjs.PatternMatchLimitError)


# What ECMA-262 patterns have brought validators down with. Each runs for longer than anyone can wait on its text
# with an engine that backtracks, or for a time that grows as a power of the length of the text.
RUNAWAY = [
    ("^(a+)+$", "a" * 64 + "!"),
    ("^(?:a|a)+$", "a" * 64 + "!"),
    ("^(?:a|aa)+$", "a" * 200 + "!"),
    ("^([a-zA-Z0-9]+[._-]?)*[a-zA-Z0-9]+@x$", "a" * 64 + "!"),
    ("^(?:(a+)\\1*)+$", "a" * 60 + "b"),
    ("^(?:.*a){24}$", "a" * 3000 + "!"),
    ("(?:.*.*.*.*.*)x$", "a" * 100000),
    ("(?<=^(?:a|a)*)b$", "a" * 64 + "!"),
    ("^(?=(?:a|a)+$)", "a" * 64 + "!"),
    ("(?i:^(?:(.)\\1?)+$)", "aA" * 40 + "!"),
    ("^(?:\\p{L}|\\p{Ll})+$", "a" * 64 + "!"),
    ("^(?:a+a+)+\\-?$", "a" * 64 + "!"),
]


@pytest.mark.parametrize(("pattern", "text"), RUNAWAY)
def test_a_pattern_that_does_not_answer_reaches_the_limit(pattern: str, text: str) -> None:
    matcher = compile_pattern(pattern, 0.1)
    assert matcher is not None
    assert _engine(matcher) == "regex", "a pattern that is not proved linear goes to the engine that takes a limit"
    try:
        _raises_at_the_limit(lambda: matcher(text), 0.1)
    except pytest.fail.Exception:
        # The regex package answers some of these at once, where the standard library's engine would not. An
        # answer in time is as good as the error.
        start = time.process_time()
        matcher(text)
        assert time.process_time() - start < 0.1


# ---------------------------------------------------------------------------------------------------------------------
# Which engine runs a pattern

# Patterns the standard library's engine is proved to match in time proportional to the text. It has no limit and
# needs none, and a match costs what it did before there was a limit.
PROVED = [
    "^[a-z0-9]+(?:-[a-z0-9]+)*$",
    "^[a-z]+@[a-z]+\\.[a-z]{2,}$",
    "^\\d{3}-\\d{4}$",
    "^[A-Za-z_][A-Za-z0-9_]*$",
    "^(?=.*[a-z])(?=.*[A-Z])(?=.*\\d).{8,}$",
    "^.*\\.json$",
    "^.*\\.(?:txt|trie)(?:\\.gz)?$",
    "[a-z][a-z0-9]*",
    "\\d+",
    "^x-",
    "^(http|https)://",
    "^(?:es5|es6|es2015)$",
    "^[Ee][Ss]5|[Ee][Ss]6|[Ee][Ss]7$",
    "^([a-z_$][a-z0-9_$]{0,39}\\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$",
    "^[0-9]+(ns|ms|us|µs|s|m|h)$",
    "^(?:(?!--)[a-z-])+$",
    "^a{1000}$",
    "^(?:[a-z]{1,63}\\.){1,8}[a-z]{2,63}$",
    "^[A-Za-z]{2,}.[A-Za-z]{2,}",
    "default|^[0-9]+$",
]

# Patterns it is not proved for. Most of them are not linear: the comment says how the time grows.
NOT_PROVED = [
    "^(a+)+$",  # doubles with each character
    "^(?:a|a)+$",  # doubles
    "^(?:a|aa)+$",  # as the Fibonacci numbers
    "^[a-zA-Z0-9]+([._-]?[a-zA-Z0-9]+)*$",  # doubles
    "^((\\.(?!\\.)\\/)?\\w+\\/?)+$",  # doubles
    "a*b",  # the square: a try from every character that runs to the end
    "\\{.*\\}",  # the square
    "f.*o",  # the square
    "^.*foo.*$",  # the square, on a text with line breaks
    "^[0-9]{1,}.[0-9]{1,}.[0-9]{1,}$",  # the cube
    "^[a-zA-Z0-9_\\.\\-]+[\\|]?[a-zA-Z0-9_\\.\\-]+$",  # the square
    "^(?:[a-z]+,)*[a-z]+$",  # linear, and not proved: the next character does not say whether the list ends
    "^([a-z]+)\\1$",  # a reference reads a group
    "^(?:(?=.*x)a)*$",  # a lookahead with no bound in a repetition
    "(?=.*x)a",  # the same at every place in the text
    "^(?:[^/]+/)*[^/]+\\.[a-z]{1,8}$",  # linear, and not proved, as the list above
    "[a-z]{500}[0-9]",  # tried at every place, and a try takes 500 characters
    "(?<=a)b",  # a lookbehind (the regex package runs those)
]


def _proved(pattern: str) -> bool:
    t = pattern_module._translate(pattern, True, False) or pattern_module._translate(pattern, False, False)
    assert t is not None, pattern
    return t.linear


@pytest.mark.parametrize("pattern", PROVED)
def test_an_ordinary_pattern_is_run_by_the_standard_library_as_before(pattern: str) -> None:
    assert _proved(pattern), pattern
    limited, free = compile_pattern(pattern), compile_pattern(pattern, None)
    assert limited is not None and free is not None
    # The search function is the engine's own: nothing is called around it, with the limit or without.
    assert _engine(limited) == "re" and _engine(free) == "re"
    assert isinstance(limited, types.BuiltinMethodType)
    assert limited.__self__.pattern == free.__self__.pattern  # type: ignore[attr-defined]


@pytest.mark.parametrize("pattern", NOT_PROVED)
def test_a_pattern_that_is_not_proved_linear_is_run_with_the_limit(pattern: str) -> None:
    assert not _proved(pattern), pattern
    limited = compile_pattern(pattern)
    assert limited is not None and _engine(limited) == "regex"


def _texts(n: int) -> list[str]:
    out = []
    for unit in ("a", "ab", "a,", "a.", "a/", "a1", "aB", "x-", "es5", "http://", "a.json", "1ms"):
        t = (unit * n)[:n]
        out += [t, t + "!", "!" + t, t + "\n", t[: n // 2] + "\n" + t[n // 2 :]]
    return out


@pytest.mark.parametrize("pattern", PROVED)
def test_a_proved_pattern_takes_time_in_proportion_to_the_text(pattern: str) -> None:
    # 200,000 characters. A pattern whose time grew as the square of the length would take minutes for one of these
    # texts, and one that doubled would never end. Linear time is some milliseconds. The bound is 30 s for all 60.
    matcher = compile_pattern(pattern)
    assert matcher is not None
    start = time.process_time()
    for text in _texts(200_000):
        matcher(text)
    assert time.process_time() - start < 30


def test_the_answers_are_the_same_with_the_limit_and_without() -> None:
    texts = ["", "a", "ab", "a-b", "a@b.cc", "x-y", "123-4567", "aB3defgh", "a.json", "http://x", "es5", "{a}", "foo\n"]
    for pattern in PROVED + NOT_PROVED:
        limited, free = compile_pattern(pattern), compile_pattern(pattern, None)
        assert limited is not None and free is not None
        for text in texts:
            assert bool(limited(text)) == bool(free(text)), (pattern, text)


# ---------------------------------------------------------------------------------------------------------------------
# The limit in generated modules and program images


def test_a_generated_module_carries_the_limit(tmp_path: Path) -> None:
    schema = {"pattern": NEVER_ANSWERS}
    # The default is not written, so a module for it is the text it was before the limit existed.
    assert f"R.pattern({NEVER_ANSWERS!r})" in cjs.generate_module(schema)
    assert f"R.pattern({NEVER_ANSWERS!r}, 0.05)" in cjs.generate_module(schema, pattern_match_limit=0.05)
    assert f"R.pattern({NEVER_ANSWERS!r}, None)" in cjs.generate_module(schema, pattern_match_limit=0)
    file = tmp_path / "limited_validator.py"
    file.write_text(cjs.generate_module(schema, pattern_match_limit=0.05), encoding="utf-8")
    spec = importlib.util.spec_from_file_location("limited_validator", file)
    assert spec is not None and spec.loader is not None
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    _raises_at_the_limit(lambda: module.validate(NEVER_ANSWERED), 0.05)
    collector = cjs.JsonSchemaResultsCollector.create(cjs.ResultsLevel.VERBOSE)
    _raises_at_the_limit(lambda: module.evaluate(NEVER_ANSWERED, collector), 0.05)
    assert module.validate("aa") is True


def test_a_program_image_carries_the_limit() -> None:
    import json

    import corvus_json_schema as package

    _, program = package._generate({"pattern": "a+"}, cjs.CompileOptions(pattern_match_limit=0.25))
    image = serialize_program(package._collecting_program(program))
    assert load_program(image).pattern_match_limit == 0.25
    # An image written before there was a limit has the default.
    data = json.loads(image)
    del data["patternMatchLimit"]
    assert load_program(json.dumps(data)).pattern_match_limit == 1.0


def test_the_command_line_takes_the_limit_and_reports_a_match_that_reaches_it(
    tmp_path: Path, capsys: pytest.CaptureFixture[str]
) -> None:
    import json

    from corvus_json_schema.cli import main

    schema, slow, fine = tmp_path / "schema.json", tmp_path / "slow.json", tmp_path / "fine.json"
    schema.write_text(json.dumps({"pattern": NEVER_ANSWERS}), encoding="utf-8")
    slow.write_text(json.dumps(NEVER_ANSWERED), encoding="utf-8")
    fine.write_text(json.dumps("aaaa"), encoding="utf-8")
    assert main(["validate", str(schema), str(fine), str(slow), "--pattern-match-limit", "0.05"]) == 2
    out = capsys.readouterr().out
    assert f"{fine}: valid" in out and f"{slow}: no answer. A match of the regular expression" in out
    assert main(["validate", str(schema), str(fine), "--pattern-match-limit", "0"]) == 0
    assert main(["generate", str(schema), "--pattern-match-limit", "0.05"]) == 0
    assert ", 0.05)" in capsys.readouterr().out


# ---------------------------------------------------------------------------------------------------------------------
# The limits of size


def _too_large(schema: Any, reason: str) -> None:
    start = time.monotonic()
    with pytest.raises(cjs.PatternTooLargeError) as raised:
        cjs.compile(schema)
    message = str(raised.value)
    assert reason in message, message
    assert "too large" in message and "invalid" not in message.lower() and len(message) < 500
    assert time.monotonic() - start < 30


def test_a_pattern_over_a_limit_of_size_is_not_an_invalid_pattern() -> None:
    def nest(depth: int) -> str:
        return "(?:" * depth + "a" + ")" * depth

    assert pattern_module.MAX_PATTERN == 32768 and pattern_module.MAX_NESTING == 100
    for at, over, reason in (
        ("[ab]" + "a" * 32764, "[ab]" + "a" * 32765, "it is longer than 32,768 characters"),
        (nest(100) + "+b*", nest(101) + "+b*", "its groups nest more than 100 deep"),
    ):
        assert cjs.compile({"pattern": at}) is not None
        for schema in ({"pattern": over}, {"patternProperties": {over: True}, "additionalProperties": False}):
            _too_large(schema, reason)
    # It is a compilation error, so code that catches those still catches it.
    assert issubclass(cjs.PatternTooLargeError, cjs.SchemaCompilationError)
    with pytest.raises(cjs.SchemaCompilationError, match=r"^Invalid regular expression") as raised:
        cjs.compile({"pattern": "a**"})
    assert not isinstance(raised.value, cjs.PatternTooLargeError)


@pytest.mark.parametrize(
    "pattern",
    [
        "(" * 10000 + ")" * 10000,
        "(" * 10000,
        "(?:" * 10000 + "a" + ")*" * 10000,
        "[" * 30000 + "a" * 3000,
        "a" * 1_000_000,
        "(?:a|b)" * 20000,
    ],
)
def test_a_pattern_too_long_or_too_deep_to_read_is_refused_at_once(pattern: str) -> None:
    start = time.monotonic()
    with pytest.raises(cjs.PatternTooLargeError):
        compile_pattern(pattern)
    assert not is_valid_ecma_regex(pattern)
    # It is refused before it is read. A second is thousands of times what that takes.
    assert time.monotonic() - start < 5


# A count the regex package would write out as copies, or a repetition this package writes without a count.
COPIES = [
    "(?<=b)a{10001}",
    "(?<=b)(?:a{1000}){1000}",
    "(?:a{1000}){1000}",
    "(?<=x)(?:(?:[ab]{100}){100}){100}",
    "^(?:(?:(a)|b){300}){300}\\1$",
    "(?=a)" + "a{999}" * 160,
    "^" + "(" * 14 + "a|b" + ")+" * 14 + "\\1$",
]


@pytest.mark.parametrize("pattern", COPIES)
def test_a_count_that_would_be_written_out_too_many_times_is_refused(pattern: str) -> None:
    tracemalloc.start()
    try:
        start = time.monotonic()
        with pytest.raises(cjs.PatternTooLargeError, match="too large to compile"):
            compile_pattern(pattern)
        took = time.monotonic() - start
        peak = tracemalloc.get_traced_memory()[1]
    finally:
        tracemalloc.stop()
    # Before the limit the first of these took 13 MB to compile and the third 125 MB. Refusing one takes some
    # kilobytes and no time. The bounds are far above that and far below what compiling one costs.
    assert peak < 16 * 1024 * 1024, peak
    assert took < 20
    # It is read to its end, so it is known to be valid.
    assert is_valid_ecma_regex(pattern)


def test_the_longest_pattern_is_read_proved_and_compiled_in_time_in_proportion_to_it(
    monkeypatch: pytest.MonkeyPatch,
) -> None:
    # Whether a pattern is proved linear is worked out in time proportional to the pattern. The ways through a
    # bounded pattern are written out to compare them, and writing out one way of 32,000 characters, a character at
    # a time, took 3 s. Nothing longer than the bound on the work of a proved pattern is written out.
    lengths: list[float] = []
    original = pattern_module._Linear.ways

    def ways(self: Any, n: Any) -> Any:
        lengths.append(self.length(n))
        return original(self, n)

    monkeypatch.setattr(pattern_module._Linear, "ways", ways)
    for pattern in (
        "^[ab]" + "a" * 32000 + "$",
        "^(?:" + "a|" * 16000 + "b)$",
        "^" + "[ab]" * 8000,
        "x" * 30000 + "y*",
    ):
        start = time.process_time()
        matcher = compile_pattern(pattern)
        assert matcher is not None
        # Twenty times what the whole compilation takes on a quiet machine.
        assert time.process_time() - start < 10, pattern[:20]
    assert max(lengths, default=0) <= pattern_module._Linear.MAX_WORK
    # A proved pattern has 4,096 nodes at most, which bounds the work of the engine for a character of the text. A
    # longer one goes to the engine with the limit.
    assert _engine(compile_pattern("^[ab]" + "a" * 3000 + "$")) == "re"
    assert _engine(compile_pattern("^[ab]" + "a" * 32000 + "$")) == "regex"
    # A bounded pattern within the bound is still proved by its ways.
    lengths.clear()
    assert _proved("^(?:es5|es6|es2015)$") and lengths


def test_a_count_is_no_matter_to_the_standard_library() -> None:
    # The standard library's engine keeps a count as a number, so a pattern it is proved to run in linear time may
    # have any count.
    for pattern, text, expected in (
        ("^a{100000}$", "a" * 100000, True),
        ("^a{100000}$", "a" * 99999, False),
        ("^(?:a{1000}){1000}$", "a" * 1_000_000, True),
        ("^[ab]{20000,30000}$", "ab" * 12500, True),
    ):
        matcher = compile_pattern(pattern)
        assert matcher is not None and _engine(matcher) == "re"
        assert bool(matcher(text)) is expected, pattern
    # And (?<=b)a{4000} is within what the regex package may write out.
    matcher = compile_pattern("(?<=b)a{4000}")
    assert matcher is not None and matcher("b" + "a" * 4000)


def test_a_repetition_of_nothing_but_a_repetition_is_written_as_one() -> None:
    # Where no reference reads a group, (?:Y+)+ is Y+ and (?:Y+)* is Y*. One inside the other, both engines try
    # every way to share a text between the two, and the regex package compiles each + as two copies of its body:
    # twenty groups with a * of what can match nothing inside each other took it 331 MB and 6 s to compile.
    for depth in (2, 3, 20, 60):
        nested = "(?:" * depth + "a*" + ")*" * depth
        assert pattern_module.translate(nested + "b") == ("(?:a)*b", False), depth
        assert pattern_module.translate(nested + "b", for_regex=True) == ("(?:a)*b", True), depth
        assert pattern_module.translate("^" + "(" * depth + "a|" + ")+" * depth + "$") == ("^(?:a)*\\Z", False), depth
    tracemalloc.start()
    try:
        start = time.monotonic()
        for limit in (1.0, None):
            matcher = compile_pattern("^" + "(?:" * 20 + "a*" + ")*" * 20 + "$", limit)
            assert matcher is not None and _engine(matcher) == "re"
            # Nested as it was written, this text had no answer from either engine.
            assert matcher("a" * 5000) and not matcher("a" * 5000 + "!")
        assert time.monotonic() - start < 20 and tracemalloc.get_traced_memory()[1] < 16 * 1024 * 1024
    finally:
        tracemalloc.stop()
    # A repetition with a count, or with something beside the inner one, is not such a repetition.
    assert pattern_module.translate("^(?:(?:a|)*x?){2,}$") == ("^(?:(?:(?:a)+(?:x)?|(?:x){1}))*\\Z", False)
    assert pattern_module.translate("^(?:(?:a|){2,3})*$") == ("^(?:(?:a){1,3})*\\Z", False)
    # Nor is one that a reference reads a group of.
    text, for_regex = pattern_module.translate("^(?:(?:(a)|)*)*\\1$") or ("", False)
    assert for_regex and text.count("(?P<g1>a)") >= 1 and "(?:a)*" not in text


def test_nothing_longer_than_the_limit_is_written_for_an_engine() -> None:
    limit = pattern_module._MAX_TRANSLATED
    shapes = [
        "^" + "(?:" * 20 + "(a)" + "|){1,2}" * 20 + "\\1$",
        "^(?=(" + "(?:" * 30 + "a" + "){0,8}?" * 30 + "))\\1$",
        "^(a|)" + "(?:" * 60 + "\\1" + "|b){1,2}" * 60 + "$",
        "(?i:" + "k" * 990 + ")",
        "(?i:^(.)" + "\\1" * 300 + "$)",
        "^(?:(a)|b|){0,1000}\\1$",
        "(?i:" + "[^a]" * 198 + ")",
    ]
    for pattern in shapes:
        for for_regex in (False, True):
            try:
                t = pattern_module._translate(pattern, True, for_regex)
            except pattern_module._TooLarge:
                continue
            assert t is not None, pattern
            # The tests of a case-insensitive reference are written once for a pattern and are counted apart: they
            # are as long as the table of case at most, whatever the pattern is.
            assert len(t.text) <= limit + pattern_module._MAX_TESTS + 64, (pattern, len(t.text))
    # Four references to groups that take different characters have a test each. Many of the widest have one.
    few = pattern_module._translate("(?i:^(.)\\1(\\w)\\2$)", True, False)
    many = pattern_module._translate("(?i:^(.)\\1(\\w)\\2(\\D)\\3([^a])\\4(\\S)\\5$)", True, False)
    assert few is not None and many is not None
    assert len(many.text) <= pattern_module._MAX_TESTS + 4096 and len(few.text) > 40000


def test_the_caches_are_bounded() -> None:
    import re

    assert pattern_module._CACHE_PATTERNS == 256
    for k in range(600):
        assert compile_pattern(f"^a{{{k}}}b+$") is not None
        assert compile_pattern(f"^(?:a|a){{{k}}}b+$", 0.5) is not None
    assert len(pattern_module._cache) <= 256
    # A long pattern is not kept at all, by this package or by the standard library, which compiles it all the same.
    long = "^[ab]" + "a" * 2000 + "$"
    re.purge()
    assert compile_pattern(long) is not None
    assert (long, 1.0) not in pattern_module._cache
    assert not any(long[5:50] in str(key) for key in getattr(re, "_cache", {}))
    assert _engine(compile_pattern(long)) == "re"
    assert compile_pattern(long)("b" + "a" * 2000) and not compile_pattern(long)("b" + "a" * 1999)  # type: ignore[misc]
    # On a Python without the compiler behind re.compile where it is looked for, the regex package takes it.
    with pytest.MonkeyPatch.context() as patch:
        patch.setattr(pattern_module, "_compile_for_re", lambda text: None)
        assert _engine(compile_pattern(long)) == "regex"
    # The regex package is asked not to keep what it compiles for this package.
    import regex

    regex.purge()
    assert compile_pattern("(?<=q)w{3,}e") is not None
    assert not any("(?<=q)" in str(key) for key in getattr(regex._main, "_cache", {}))


def test_a_caller_near_the_end_of_the_stack_gets_the_error_of_size() -> None:
    pattern = "(?:" * 100 + "a" + "|b)" * 100
    outcomes = set()

    def deep(n: int) -> None:
        if n:
            deep(n - 1)
            return
        try:
            compile_pattern(pattern + "b")
            outcomes.add("compiled")
        except cjs.PatternTooLargeError as e:
            assert "stack" in str(e)
            outcomes.add("too large")

    # From 200 frames to 80 frames short of the limit of recursion. Whatever is left, the outcome is one of the two.
    here = len(__import__("inspect").stack())
    for left in (200, 160, 120, 80):
        deep(sys.getrecursionlimit() - here - left)
    assert outcomes <= {"compiled", "too large"} and "too large" in outcomes
    assert compile_pattern(pattern + "c") is not None


def test_format_regex_reads_within_the_limits() -> None:
    v = cjs.compile({"format": "regex"}, assert_format=True)
    assert v("^(a+)+$") is True and v("a**") is False
    # Too long or too deep to read: not valid, and at once. Valid and too large to write out: valid.
    start = time.monotonic()
    assert v("a" * 40000) is False
    assert v("(" * 5000 + ")" * 5000) is False
    assert v("(?<=b)(?:a{1000}){1000}") is True
    assert time.monotonic() - start < 10
