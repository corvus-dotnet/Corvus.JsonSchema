"""The limits on a regular expression. A schema and an instance may both come from outside, so a match of a pattern
has a limit of time (one second unless ``pattern_match_limit`` says otherwise), and a pattern has limits of size.
Reaching either raises an exception of its own, and is never an answer.

No test here needs a quiet machine. A match that is to reach its limit would run for longer than the age of the
universe, the limit is never reached before nine tenths of it have passed on the clock, and the upper bounds are many
times what a quiet machine takes.
"""

from __future__ import annotations

import hashlib
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
    # The limit has passed on the clock, to within a tenth of it. With no other thread at work the processor's time
    # is the clock's, and it is within twice the limit but for what the test itself costs (the collectors, pytest).
    # The upper bound on the clock only has to show that nothing hung.
    assert limit * 0.9 <= wall < 60, (wall, limit)
    assert cpu < 2 * limit + 2, (cpu, limit)
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


# The limit is of time on the clock. The regex package counts the time of the whole process on the processor, which
# passes several times as fast as the clock while other threads are at work.


class _Busy:
    """Other threads at work for as long as it is entered. Each hashes a megabyte over and over, which the standard
    library does without the interpreter's lock, so they run beside a match whatever the build of Python."""

    def __init__(self, threads: int) -> None:
        self.stop = threading.Event()
        self.threads = [threading.Thread(target=self.work, daemon=True) for _ in range(threads)]

    def work(self) -> None:
        block = bytes(1 << 20)
        while not self.stop.is_set():
            hashlib.sha256(block).digest()

    def __enter__(self) -> None:
        for thread in self.threads:
            thread.start()

    def __exit__(self, *_: object) -> None:
        self.stop.set()
        for thread in self.threads:
            thread.join(60)


def test_the_limit_is_of_time_on_the_clock_whatever_other_threads_do() -> None:
    limit = 0.5
    v = cjs.compile({"pattern": NEVER_ANSWERS}, pattern_match_limit=limit)
    with _Busy(7):
        for _ in range(3):
            start = time.monotonic()
            with pytest.raises(cjs.PatternMatchLimitError):
                v(NEVER_ANSWERED)
            wall = time.monotonic() - start
            # Never before the limit, to within a tenth of it. A machine that is doing other work only makes it
            # later, and the upper bound only has to show that nothing hung.
            assert 0.9 * limit <= wall < 60, wall


def test_a_match_that_is_within_the_limit_is_answered_while_another_thread_is_at_work() -> None:
    # A match that takes a good part of a second by itself: some thousands of tries, each over the rest of the text.
    pattern, text = "(?:a|b)*(?<=x)c$", "ab" * 2000 + "c"
    slow = compile_pattern(pattern, None)
    assert slow is not None
    start = time.monotonic()
    assert slow(text) is None
    alone = time.monotonic() - start
    limit = max(1.5 * alone, 0.2)
    matcher = compile_pattern(pattern, limit)
    assert matcher is not None and _engine(matcher) == "regex"
    with _Busy(1):
        for _ in range(3):
            start = time.monotonic()
            try:
                # With one other thread the processor's time passes twice as fast as the clock, and the engine's own
                # limit is reached after half of the limit, which is three quarters of what the match needs.
                assert matcher(text) is None
            except cjs.PatternMatchLimitError:
                # A machine that is doing other work may make the match half as slow again. Then the error is
                # right, when the limit has passed on the clock.
                assert time.monotonic() - start >= 0.9 * limit


class _Engine:
    """A search function as the regex package's, of a match that needs ``needs`` seconds on the clock, in a process
    where the processor's time passes ``rate`` times as fast as the clock (and as fast as the clock from ``quiet``
    seconds after the first call)."""

    def __init__(self, needs: float, rate: float, quiet: float = 1e9) -> None:
        self.needs = needs
        self.rate = rate
        self.quiet = quiet
        self.timeouts: list[float] = []
        self.starts: list[float] = []

    def __call__(self, text: str, timeout: float) -> str:
        now = time.monotonic()
        self.timeouts.append(timeout)
        self.starts.append(now)
        rate = self.rate if now - self.starts[0] < self.quiet else 1.0
        if timeout / rate < self.needs:
            time.sleep(timeout / rate)
            raise TimeoutError
        time.sleep(self.needs)
        return text

    def bounded(self, limit: float) -> bool:
        """Whether no try was allowed more than was left of twice the limit, counted from the second try (a try
        takes its allowance on the clock at most, where the process has a processor to itself). Half a limit more
        is let pass, for a machine that keeps the test waiting between the reading of the clock and the try."""
        return all(
            (start - self.starts[1]) + allowed <= 2.5 * limit
            for start, allowed in zip(self.starts[1:], self.timeouts[1:], strict=True)
        )


@pytest.mark.parametrize("rate", [1.0, 1.05, 1.5, 2.0, 4.0, 16.0])
def test_a_match_is_tried_again_until_the_limit_has_passed_on_the_clock(rate: float) -> None:
    limit = 0.4
    engine = _Engine(1e9, rate)
    limited = pattern_module._limited(engine, limit, "p")
    start = time.monotonic()
    with pytest.raises(cjs.PatternMatchLimitError) as raised:
        limited("text")
    wall = time.monotonic() - start
    assert raised.value.pattern == "p" and raised.value.limit == limit
    assert 0.9 * limit <= wall < 60, wall
    # The first try is a small share of the limit, and nothing is measured around it. The second is a short one
    # that shows how fast the processor's time is passing, and the third is allowed the limit on the clock at
    # that rate, or what is left of twice the limit.
    assert engine.timeouts[:2] == [limit / 32, limit / 16]
    assert engine.timeouts[2] <= min(rate * limit * 1.05, 2 * limit)
    assert engine.bounded(limit), (engine.starts, engine.timeouts)
    # With no other thread at work the third try is the last. (With some, a machine that keeps this test's sleeps
    # waiting makes the rate look lower than it is, and the third try shorter.)
    assert rate > 1.05 or len(engine.timeouts) == 3, engine.timeouts
    assert len(engine.timeouts) <= 3 + rate, engine.timeouts


@pytest.mark.parametrize(("rate", "share"), [(1.0, 0.7), (2.0, 0.7), (4.0, 0.3), (16.0, 0.05), (16.0, 0.001)])
def test_a_match_that_needs_less_than_the_limit_is_answered_with_other_threads_at_work(
    rate: float, share: float
) -> None:
    limit = 0.3
    engine = _Engine(share * limit, rate)
    limited = pattern_module._limited(engine, limit, "p")
    try:
        assert limited("text") == "text"
    except cjs.PatternMatchLimitError:
        # Only where the machine made the sleeps of this test so long that the limit passed on the clock.
        assert time.monotonic() - engine.starts[1] >= 0.9 * limit
        return
    assert len(engine.timeouts) <= 3
    assert engine.bounded(limit)


@pytest.mark.parametrize("quiet", [0.0, 0.01, 0.05, 0.2])
def test_a_match_is_bounded_at_twice_the_limit_when_the_other_threads_stop(quiet: float) -> None:
    # Sixteen threads at work, which stop while the match is being tried. A try that was allowed sixteen times the
    # limit of the processor's time would go on for sixteen times the limit.
    limit = 0.3
    engine = _Engine(1e9, 16.0, quiet)
    limited = pattern_module._limited(engine, limit, "p")
    with pytest.raises(cjs.PatternMatchLimitError):
        limited("text")
    assert engine.bounded(limit), (engine.starts, engine.timeouts)
    assert max(engine.timeouts) <= 2 * limit


def test_a_match_that_answers_at_once_is_one_call_of_the_engine() -> None:
    engine = _Engine(0.0, 8.0)
    limited = pattern_module._limited(engine, 1.0, "p")
    assert limited("text") == "text"
    assert engine.timeouts == [1.0 / 32]


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


def test_the_classes_of_a_pattern_are_worked_out_within_a_bound(monkeypatch: pytest.MonkeyPatch) -> None:
    # A class of a property in a case-insensitive group takes some milliseconds to work out, and a pattern of 1,000
    # characters has room for eighty, one of 32,768 for thousands.
    classes: list[int] = []
    closures: list[int] = []
    class_of, equal = pattern_module._class_of, pattern_module._Translator._equal

    def counted_class_of(members: Any, beside: Any, negated: bool = False) -> str:
        classes.append(len(members))
        return class_of(members, beside, negated)

    def counted_equal(self: Any, ranges: Any) -> Any:
        closures.append(len(ranges))
        return equal(self, ranges)

    monkeypatch.setattr(pattern_module, "_class_of", counted_class_of)
    monkeypatch.setattr(pattern_module._Translator, "_equal", counted_equal)
    # The same class again and again is worked out once (for each engine and grammar it is read for).
    matcher = compile_pattern("(?i:^" + "[\\p{Lu}\\p{Ll}]" * 60 + "$)")
    assert matcher is not None and matcher("aB" * 30) and not matcher("aB" * 29 + "a1")
    assert 1 <= len(classes) <= 3, len(classes)
    # In classes that differ, what a property adds without case is worked out once, and the rest of the work for a
    # class is for what is written in it beside the property.
    classes.clear()
    closures.clear()
    different = "".join(f"[\\p{{L}}{chr(0x2460 + k)}]" for k in range(80))
    matcher = compile_pattern(f"(?i:(?<=^){different}$)")
    assert (
        matcher is not None and matcher("a" * 80) and matcher("\u2460" + "A" * 79) and not matcher("\u2461" + "a" * 79)
    )
    assert 80 <= len(classes) <= 3 * 80, len(classes)
    assert sum(1 for n in closures if n > 100) <= 3, closures
    # A few such classes mean what they did.
    monkeypatch.undo()
    validate = cjs.compile({"pattern": "(?i:^[\\p{Lu}_][\\p{Lu}_]$)"})
    assert validate("aB") and validate("_\u00e9") and not validate("a1")


def test_translate_reads_nothing_longer_than_the_limit() -> None:
    long = "a|" * 300000
    for call in (lambda: pattern_module.translate(long), lambda: pattern_module.translate(long, False, True)):
        start = time.process_time()
        with pytest.raises(pattern_module._TooLarge) as raised:
            call()
        assert raised.value.unread and time.process_time() - start < 1


def test_asking_the_regex_package_about_a_property_takes_megabytes_and_not_tens_of_them() -> None:
    # What the package has for a property is found by asking it about every character, and so is whether what is
    # written for it has the characters of the tables. The text of every character is 4 MB. Building it from a
    # million strings took 85 MB.
    pattern_module._every_character.clear()
    pattern_module._host.clear()
    pattern_module._host_sets.clear()
    for name in ("Lt", "L", "Cn"):
        known = pattern_module._property(name)
        assert known is not None and known[0] is not None
        tracemalloc.start()
        try:
            pattern_module._host_form(known[0][0], known[0][1], known[1])
            peak = tracemalloc.get_traced_memory()[1]
        finally:
            tracemalloc.stop()
        assert peak < 24 * 1024 * 1024, (name, peak)
    assert len(pattern_module._every_character[0]) == 0x110000
    assert [ord(c) for c in pattern_module._every_character[0][0xD7FF:0xD802]] == [0xD7FF, 0xD800, 0xD801]


# ---------------------------------------------------------------------------------------------------------------------
# Properties: the characters of Unicode 17, by the engine's own property where it has one


def _every_character() -> str:
    pattern_module._host_set("\\p{gc=Lt}")
    return pattern_module._every_character[0]


def _taken_by(pattern: str, for_regex: bool) -> list[tuple[int, int]]:
    """The characters that a pattern of one character takes, as it is written for an engine."""
    import re

    import regex

    written = pattern_module.translate(pattern, True, for_regex)
    assert written is not None, pattern
    text, by_regex = written
    assert by_regex == for_regex, pattern
    compiled = regex.compile(f"(?:{text})+") if for_regex else re.compile(f"(?:{text})+", re.ASCII)
    return [(m.start(), m.end() - 1) for m in compiled.finditer(_every_character())]


# Properties that the releases of the regex package have with other characters than Unicode 17 (more in one, fewer
# in another), some that all have alike, and some that the package does not have at all.
_SOME_PROPERTIES = [
    "L",
    "Lu",
    "Ll",
    "Lo",
    "N",
    "S",
    "Mn",
    "C",
    "Cn",
    "Zs",
    "Alphabetic",
    "ID_Start",
    "ID_Continue",
    "Extended_Pictographic",
    "Emoji",
    "Lowercase",
    "Script=Latin",
    "sc=Zyyy",
    "sc=Zzzz",
    "scx=Han",
    "Script=Sidetic",
    "Assigned",
    "Any",
    "ASCII",
    "Changes_When_NFKC_Casefolded",
]


@pytest.mark.parametrize("name", _SOME_PROPERTIES)
def test_a_property_is_the_characters_of_the_tables_however_it_is_written_for_an_engine(name: str) -> None:
    known = pattern_module._property(name)
    digits = pattern_module._property("Nd")
    assert known is not None and digits is not None
    merged, complement = pattern_module._merged, pattern_module._complement
    has, others = known[1], complement(known[1])
    extra = [(0x30, 0x39), (0x5F, 0x5F), (0x4E00, 0x4E00)]
    for for_regex in (False, True):
        for pattern, expected in (
            (f"\\p{{{name}}}", has),
            (f"\\P{{{name}}}", others),
            (f"[\\p{{{name}}}]", has),
            (f"[^\\p{{{name}}}]", others),
            (f"[^\\P{{{name}}}]", has),
            (f"[\\p{{{name}}}0-9_\u4e00]", merged(has + extra)),
            (f"[^\\p{{{name}}}0-9_\u4e00]", complement(merged(has + extra))),
            (f"[\\P{{{name}}}0-9_\u4e00]", merged(others + extra)),
            (f"[^\\P{{{name}}}0-9_\u4e00]", complement(merged(others + extra))),
            (f"[\\p{{{name}}}\\p{{Nd}}\\p{{{name}}}]", merged(has + digits[1])),
            (f"[^\\p{{Nd}}\\p{{{name}}}]", complement(merged(has + digits[1]))),
            (f"[\\p{{{name}}}\\P{{{name}}}]", [(0, 0x10FFFF)]),
            (f"[^\\p{{{name}}}\\P{{{name}}}]", []),
        ):
            assert _taken_by(pattern, for_regex) == expected, (pattern, for_regex)


def test_a_property_is_written_for_the_regex_package_as_its_own_with_what_is_different() -> None:
    letters = pattern_module._property("L")
    assert letters is not None and letters[0] is not None
    # The 684 ranges of the letters are 1,900 characters as a class. The package has the letters of its own version
    # of Unicode, and they are written with the tens of ranges that are different.
    for pattern in ("\\p{L}", "\\P{L}", "[\\p{L}\\p{N}_-]", "[^\\p{L}\\p{N}_-]", "\\p{Lu}", "\\p{Alphabetic}"):
        written = pattern_module.translate(pattern, True, True)
        assert written is not None and len(written[0]) < 400, (pattern, len(written[0]))
        assert "\\p{" in written[0] or "\\P{" in written[0], pattern
    # A property that every release has alike is the property and nothing else.
    assert pattern_module.translate("\\p{Lt}", True, True) == ("\\p{gc=Lt}", True)
    assert pattern_module.translate("\\P{sc=Thai}+", True, True) == ("(?:\\P{sc=Thai})+", True)


# Patterns that were refused as too large: the classes of properties were written as ranges, each time.
_MANY_PROPERTIES = [
    (
        "^(?:"
        + "|".join(f"\\p{{{n}}}" for n in ["L", "N", "M", "P", "S", "Z", "Cf", "Lu", "Ll", "Lt", "Lm", "Lo"] * 6)
        + ")*$",
        "aB1 \u00e9",
        "aB1\x00",
    ),
    ("^" + "[\\p{L}\\p{N}\\p{M}\\p{P}\\p{S}]" * 36 + "$", "a1\u0301.+" * 7 + "a", "a" * 35 + " "),
    ("^" + "\\P{L}" * 190 + "$", "1" * 190, "1" * 189 + "a"),
    ("(?i:^" + "[\\p{Lu}\\p{Ll}]" * 60 + "$)", "aB" * 30, "aB" * 29 + "a1"),
    ("(?i:^" + "[^\\p{Lu}]" * 100 + "$)", "1" * 100, "1" * 99 + "a"),
    ("[" + "\\p{L}" * 199 + "]", "\u00e9", "1"),
    ("^" + "\\p{L}{1000}" * 90 + "$", "\u00e9" * 90000, "\u00e9" * 89999),
    ("(?i:^" + "\\p{L}{1000}" * 90 + "$)", "\u00e9" * 90000, "\u00e9" * 89999 + "1"),
    ("^" + "[\\p{L}\\p{N}\\p{P}\\p{S}\\p{M}]" * 40, "a1.+\u0301" * 8, "a1.+\u0301" * 7 + "a1.+ "),
    ("(?i:^" + "[\\p{L}\\p{N}\\p{P}\\p{S}\\p{M}]" * 40 + ")", "a1.+\u0301" * 8, "a1.+\u0301" * 7 + "a1.+ "),
    # Ordinary patterns of names in more languages than one, at the length a schema may have them.
    ("^" + "[\\p{L}\\p{N}_-]+\\." * 40 + "$", "\u00e91_-." * 40, "\u00e91_-." * 39 + "\u00e9 ."),
    ("(?<=^)" + "\\p{L}" * 3000 + "$", "\u00e9" * 3000, "\u00e9" * 2999 + "1"),
    ("(?<=" + "\\p{Lu}\\p{Ll}" * 400 + ")x", "A\u00e9" * 400 + "x", "A\u00e9" * 399 + "AAx"),
    ("^" + "\\p{L}" * 6000 + "$", "\u4e00" * 6000, "\u4e00" * 5999 + "1"),
]


@pytest.mark.parametrize(("pattern", "matches", "fails"), _MANY_PROPERTIES)
def test_a_pattern_with_many_classes_of_properties_is_not_too_large(pattern: str, matches: str, fails: str) -> None:
    assert len(pattern) <= pattern_module.MAX_PATTERN
    start = time.process_time()
    matcher = compile_pattern(pattern)
    assert time.process_time() - start < 30, "many times what a quiet machine takes"
    assert matcher is not None
    assert matcher(matches) is not None
    assert matcher(fails) is None


def test_a_pattern_of_names_in_many_languages_is_run_by_the_standard_library() -> None:
    name = "[\\p{L}\\p{N}_-]+"
    for pattern in (f"^{name}(?:\\.{name})*$", "^" + f"{name}\\." * 40 + "$", "^\\p{L}{1,64}$"):
        assert _engine(compile_pattern(pattern)) == "re", pattern[:40]
    # Each copy of a long class is written for the standard library, which has no way to write it once. They count
    # once towards the limit of a translation, and all of them have a limit of their own.
    assert pattern_module._MAX_WRITTEN_FOR_RE == 262_144
    written = pattern_module.translate("^" + f"{name}\\." * 40 + "$")
    assert written is not None and not written[1]
    assert pattern_module._MAX_TRANSLATED < len(written[0]) <= pattern_module._MAX_WRITTEN_FOR_RE
    # More copies than that go to the regex package, which is given each long class once.
    many = "^" + "\\p{L}" * 6000 + "$"
    assert pattern_module.translate(many) == pattern_module.translate(many, True, True)
    written = pattern_module.translate(many)
    assert written is not None and written[1] and written[0].startswith("(?(DEFINE)(")
    assert len(written[0]) <= pattern_module._MAX_TRANSLATED


def _costly(limit: float, pattern: str, text: str) -> float:
    """The processor's time until the limit of a match is reached, for a match that never answers."""
    matcher = compile_pattern(pattern, limit)
    assert matcher is not None and _engine(matcher) == "regex"
    start = time.process_time()
    with pytest.raises(cjs.PatternMatchLimitError):
        matcher(text)
    return time.process_time() - start


def test_the_limit_is_not_seconds_late_for_a_repetition_of_a_class_of_many_ranges() -> None:
    # The regex package takes a repetition of one class as one step of a match, however much of the text it runs
    # over, and looks at its clock every 256 steps. A class of hundreds of ranges, which it goes through one by one
    # for each character, made the error of a limit of one second come after 4.4 seconds for 100,000 characters.
    many = "".join(f"\\u{0x1000 + 40 * k:04x}-\\u{0x1010 + 40 * k:04x}" for k in range(120))
    for one, unit in (
        ("[\\p{L}\\p{N}]", "\u4e00"),
        ("\\p{L}", "\U00020000"),
        ("(?i:\\p{Lu})", "\U0001e943"),
        ("\\p{Changes_When_NFKC_Casefolded}", "\U0001d7ff"),
        (f"[{many}\u4e00]", "\u4e00"),
        (f"(?:[{many[: len(many) // 2]}]|\u4e00|[{many[len(many) // 2 :]}])", "\u4e00"),
    ):
        pattern = f"(?:{one}*1|{one}*2|{one}*3)"
        # It is tried three times (see pattern._again), and each try may be late. A quiet machine takes 0.2 to 0.6
        # seconds of the processor's time for a limit of 0.1.
        assert _costly(0.1, pattern, unit * 100_000) < 2.5, one[:30]


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
    # (Its cache is in regex._main from release 2026, and in regex.regex before.)
    internals = getattr(regex, "_main", None) or getattr(regex, "regex", None)
    cached = getattr(internals, "_cache", None)
    assert cached is not None and not any("(?<=q)" in str(key) for key in cached)
    assert regex.compile("(?<=q)w{3,}e") is not None and any("(?<=q)" in str(key) for key in cached)


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
