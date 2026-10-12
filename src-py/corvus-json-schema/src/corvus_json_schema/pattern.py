"""ECMA-262 regular expressions (as JSON Schema's ``pattern`` and ``format: regex`` specify), run by Python's engines.

A pattern is translated to Python's syntax with ECMA's meaning and compiled with the ASCII flag, so that ``\\d``,
``\\w`` and ``\\b`` keep their ECMA (ASCII) definitions. ECMA's ``\\s``, ``.`` and ``$`` are spelled out, and every
literal is escaped. A ``\\p{...}`` is the characters of Unicode 17 that the package's own tables have for it.

Two engines run the translations. The standard library's ``re`` cannot be stopped once a match has started, and
the ``regex`` package takes a timeout. So with the limit of a match in force (``pattern_match_limit``, one second
unless the caller says otherwise) ``re`` runs the patterns that are proved to match in time proportional to the
text (see ``_Linear``), and the ``regex`` package runs the rest with the limit, which is of time on the clock
(see ``_limited``): a lookbehind, the backreferences described below, and anything else that is not proved. With no limit ``re`` runs what it can. A
pattern over a limit of size (``MAX_PATTERN``, ``MAX_NESTING``, ``MAX_COPIES`` and the length of the translation)
is refused as too large, which is not the same as not valid.

Patterns are parsed with the ``u`` flag's grammar first; one that only parses without it (Annex B: identity escapes
such as ``\\-``, literal braces) is translated by the lenient grammar instead, as JavaScript's ``RegExp`` would be
constructed without the flag. Without the flag ECMA-262 reads a pattern and matches a text by UTF-16 code unit, so
such a pattern is translated from its code units, and a text is matched as its code units too when the pattern can
tell the difference (see ``_units``).

Three things have a meaning in ECMA-262 that neither engine has, and are written out for them. All of it is done
once, when a schema is compiled. For a pattern with no backreference and no modifier group, nothing is added to a
match.

**Modifier groups.** ``(?i:...)``, ``(?m:...)``, ``(?s:...)`` and ``(?-i:...)`` are read here and never passed on.
Inside ``s`` the dot is any character. Inside ``m``, ``^`` and ``$`` are lookarounds for ECMA's four line
terminators. Inside ``i`` a character is the class of the characters equal to it without case, and a class takes
every character equal to a member. Equal means the same simple case folding with the ``u`` flag and the same
uppercase form of a UTF-16 code unit without it, from the tables of ``_casefold`` (Unicode 17), which are the same
on every version of Python and with every release of the ``regex`` package. A backreference inside ``i`` is compared character by character with the same tables,
by a loop written out for the ``regex`` package (see ``_Writer._folded``). Neither engine's own case-insensitive
backreference is used: both take ``i`` for U+0130, and each has the case pairs of its own version of Unicode.

**Backreferences.** In ECMA-262 a reference to a group that has not matched matches the empty string, and the
groups inside a repetition lose their values at the start of each iteration. Both engines fail a reference to a
group with no value, and keep a value from one iteration to the next. So each reference is placed, from where it
and its group are in the pattern.

- It can never be read after its group has matched (it is inside the group, before it, or in another alternative).
  It is written as nothing.
- Its group has always matched when it is read, in the same iteration. It is written as a reference.
- Anything else. The pattern goes to ``regex``, where two groups may share a name. An empty group of the same name
  at the start of the pattern, and at the start of each iteration of a repetition the group is inside, gives the
  group the empty value, which reads as no value does in ECMA-262.

**An iteration that matches nothing.** ECMA-262 refuses one once the minimum count is met. Both engines accept it,
which shows when a group that the iteration set, or reset, is read afterwards. A repetition whose body can only
match nothing is written as its body once, or as nothing for a minimum of 0, which is what ECMA-262 makes of it.
In a pattern for ``regex`` that has a reference, a repetition that holds a group some reference reads is written
as its minimum count and then a rest where every iteration has to take a character: ``(?=(?P<e1>(?s:.)*))``
before the body takes the rest of the text, and ``(?!(?P=e1))`` after it fails where the rest of the text is still
the same. ``_Writer`` says what else such a pattern needs for that engine.

A lookaround is left by the first way through it, and its groups keep what that way gave them. Which way is first
depends on the iterations that ECMA-262 refuses, so every repetition inside a lookaround with a group that is read
outside it is written in the same way, whether or not it holds the group. ``^(?=((?:|a)*))\\1$`` matches ``a``:
the group holds ``a``, where the engines' own first way through the lookahead leaves it empty.
"""

from __future__ import annotations

import re
import sys
import threading
from array import array
from bisect import bisect_left
from collections import OrderedDict
from collections.abc import Callable
from time import monotonic
from typing import Any

from .options import PatternMatchLimitError, PatternTooLargeError, match_limit

# ECMA-262 WhiteSpace and LineTerminator (what \s matches).
# (Characters beyond U+00A0 are written as they are, here and in every translation: an escape of one is six
# characters, and a translation has a limit of length.)
_SPACE = "\\t-\\r \\xa0\u1680\u2000-\u200a\u2028\u2029\u202f\u205f\u3000\ufeff"
_DOT = "[^\\n\\r\u2028\u2029]"
_ANY = "(?s:.)"
# What an empty class is: nothing matches it. (The regex package reads [^\s\S] as any character.)
_NEVER = "(?!)"
# ^ and $ inside a group with the m modifier: at the start or the end of the text or next to a line terminator.
_LINE_START = "(?<![^\\n\\r\u2028\u2029])"
_LINE_END = "(?![^\\n\\r\u2028\u2029])"
_SYNTAX = set("^$\\.*+?()[]{}|/")
# A digit of a pattern is one of these. (str.isdigit and \d in a pattern for text take the decimal digits of every
# script, of the version of Unicode that the interpreter has.)
_DIGITS = frozenset("0123456789")
_QUANTIFIER_RE = re.compile(r"\{([0-9]+)(,([0-9]*))?\}")
# One character, escape or class as it is written, which a quantifier can follow without a group around it.
_ATOM_RE = re.compile(
    r"\[(?:[^\]\\]|\\.)*\]|\\[pP]\{[^}]*\}|\\x[0-9a-f]{2}|\\u[0-9a-f]{4}|\\.|[^\\\[\]()|^$.*+?{}]", re.DOTALL
)
_PROPERTY_RE = re.compile(r"\\[pP]\{[^}]*\}")
# A call of a class that is written once (see _Writer.write).
_CALL_RE = re.compile(r"\(\?[0-9]+\)|\(\?:\(\?=\(\?[0-9]+\)\)\(\?s:\.\)\)")
_ASCII_NAME_RE = re.compile(r"[A-Za-z_$][A-Za-z0-9_$]*")

# A count from here up is more characters than a text has. As a maximum it is no maximum.
_MAX_COUNT = 2**31 - 1
# The limits of size. A schema may come from outside, so what one pattern can cost to compile is bounded, and a
# pattern over a limit is refused as too large (PatternTooLargeError), which is not the same as not valid.
#
# The most characters a pattern may have, and the most its translation may have. A repetition written without a
# count (see _Writer._repeat) multiplies out when it is inside another. Both engines read a pattern with a parser
# written in Python, which takes up to 10 microseconds for a character, and keep up to 230 bytes for one.
MAX_PATTERN = 32_768
_MAX_TRANSLATED = 32_768
# The most levels of groups a pattern may have. Both engines and this translator read a group inside a group by
# recursion, and the regex package takes five frames of Python's stack for a level.
MAX_NESTING = 100
# The most copies the regex package may have to write out. It compiles X{1000} as a thousand and one copies of X
# and X+ as two, and a repetition inside a repetition multiplies: (?:a{1000}){1000} took 125 MB, and sixteen groups
# with a + inside each other took 41 MB and most of a second. The standard library's engine keeps a count as a
# number.
MAX_COPIES = 4_096
# The most text the tests of the case-insensitive references of a pattern may have (see _Writer._test). The test for
# every character that has another case is as long as the table of case, about 54,000 characters, and a pattern
# that would need more than this in several tests has that one for all of them.
_MAX_TESTS = 65_536
# A class that is written with more characters than this counts once towards the length of a translation however
# many times the pattern has it, and a call of it counts this much (see _Writer.write).
_LONG_CLASS = 64
_CALL = 8
# With each class written once for the regex package (see _Writer.write), a class of more characters than this is
# one that is called.
_SHARED = 6
# The most characters of a translation for the standard library's engine, with every copy of a long class counted.
_MAX_WRITTEN_FOR_RE = 262_144
# The most that a match of the regex package may keep for the groups of a pattern, in groups closed (see
# _Writer._keeps and _most_characters). The engine keeps what a group held each time it closes, for as long as the
# match goes on: 70 to 200 bytes as measured, and about a kilobyte for a group in a lookaround, which counts as
# _IN_A_LOOKAROUND groups. So this is 50 to 60 MB for the patterns that keep the most.
_MAX_KEPT = 1 << 19
_IN_A_LOOKAROUND = 16
# The most characters of a text that a pattern is matched against by the regex package once the first try at the
# match has been abandoned (see _again). The engine keeps 35 to 56 bytes for each iteration of a repetition of more
# than one character or class until the match ends, and raises MemoryError itself at some 550 MB of that:
# ^(?:ab)*$ did for a text of 16 million characters. So this is 59 MB.
_MOST_TEXT = 1 << 20
# A repetition that runs at most this many times is not one that a match keeps groups for in proportion to the text.
_FEW_TIMES = 16
# The most characters of a literal that the regex package is given in one piece. Before the first match of a
# pattern the engine builds the tables of a search for each literal in it, in one step that its timeout does not
# interrupt, and for a literal that repeats itself that step takes a time that grows as the cube of the length: 0.05
# seconds for 1,000 characters of "ab", 3.3 for 4,000 and 27 for 8,000. A longer literal is written with something
# between its pieces that takes no character and always holds there: there is a character before this place.
_MAX_LITERAL = 256
_AFTER_A_CHARACTER = "(?<=(?s:.))"
# The most ranges of a class that the regex package may go through for each character of a text in one step of a
# match. The engine takes a repetition of one character or class as one step, however much of the text it runs
# over, and looks at its clock every 256 steps. It goes through the ranges of a class one by one, so with a class
# of 250 ranges and a text of 100,000 characters its timeout came 2.4 seconds late, and with one of 24 ranges 0.15
# seconds. A class of more ranges than this is written under a repetition so that each character is a step (see
# _Writer._repeat), which costs a tenth of a microsecond more for a character.
_MAX_SCANNED = 24
# A text of more characters than this is matched by the pattern as it is written with every repetition of one class
# a step for each character (see _search). How late the timeout comes grows with the text as well as with the class:
# for ten million letters and the 24 ranges that \\p{L} is written with, it came 15 seconds late, for each try.
_LONG_TEXT = 32_768
# The most nodes the tree of a pattern may have once its repetitions are written without the iterations that match
# nothing (see _Plain). A pattern that would pass it is written as it is.
_MAX_PLAIN = 20_000

# Before CPython 3.11 the standard library's engine did not put back what a group held when it backtracked into a
# repetition (bpo-35859), so a reference could read what a group took on a path the match had left.
# "^([^a]?a+){2,3}?\1a$" did not match "aaaa". There the regex package runs a pattern with a reference to a group
# inside a repetition.
_GROUPS_IN_REPETITIONS_GO_STALE = sys.version_info < (3, 11)

_Ranges = list[tuple[int, int]]
_DIGIT: _Ranges = [(0x30, 0x39)]
_WORD: _Ranges = [(0x30, 0x39), (0x41, 0x5A), (0x5F, 0x5F), (0x61, 0x7A)]
# With the u flag, case-insensitive \w and \b also count the two characters that fold to an ASCII letter (the long
# s and the Kelvin sign).
_FOLD_WORD: _Ranges = [*_WORD, (0x17F, 0x17F), (0x212A, 0x212A)]
_FOLD_WORD_CLASS = "[0-9A-Za-z_\u017f\u212a]"
_WHITE: _Ranges = [
    (0x09, 0x0D),
    (0x20, 0x20),
    (0xA0, 0xA0),
    (0x1680, 0x1680),
    (0x2000, 0x200A),
    (0x2028, 0x2029),
    (0x202F, 0x202F),
    (0x205F, 0x205F),
    (0x3000, 0x3000),
    (0xFEFF, 0xFEFF),
]


class _Invalid(Exception):
    pass


# A property expression of ECMA-262: how the regex package is asked for its characters and for the others (None:
# the package has no such property), and what gives the characters as ranges.
_properties: dict[str, tuple[tuple[str, str] | None, Callable[[], _Ranges]]] = {}
_tables: dict[str, _Ranges] = {}
_table_texts: dict[str, str] = {}
_GROUPS = {"LC": ("Ll", "Lt", "Lu")}


def _table(key: str) -> _Ranges:
    """The characters of one table of ``_properties`` (the module), by ``gc:``, ``sc:``, ``scx:`` or ``b:`` and
    the name the table has there. They are read from the module's text when they are first asked for."""
    got = _tables.get(key)
    if got is None:
        if not _table_texts:
            from . import _properties as tables

            for kind, entries in (
                ("gc", tables.GENERAL_CATEGORY_RANGES),
                ("sc", tables.SCRIPT_RANGES),
                ("scx", tables.SCRIPT_EXTENSIONS_RANGES),
                ("b", tables.BINARY_RANGES),
            ):
                for entry in entries.split(";"):
                    name, _, text = entry.partition(":")
                    _table_texts[f"{kind}:{name}"] = text
        got = []
        for item in _table_texts[key].split(" "):
            lo, _, hi = item.partition("-")
            got.append((int(lo, 16), int(hi or lo, 16)))
        _tables[key] = got
    return got


def _union(keys: list[str]) -> Callable[[], _Ranges]:
    def ranges() -> _Ranges:
        key = "+".join(keys)
        got = _tables.get(key)
        if got is None:
            got = _tables[key] = _merged([r for k in keys for r in _table(k)])
        return got

    return ranges


def _property(body: str) -> tuple[tuple[str, str] | None, _Ranges] | None:
    """What a property expression of ECMA-262 (the text between the braces of a ``\\p{...}``) is: how the regex
    package is asked for its characters and for the others, and its characters as ranges. None: ECMA-262 has no
    such expression.

    The names are ECMA-262's and no others, and their case matters: a value of General_Category by itself or after
    ``General_Category=`` or ``gc=``, a value of Script after ``Script=``, ``sc=``, ``Script_Extensions=`` or
    ``scx=``, and a binary property that ECMA-262 lists. The regex package has many more (``\\p{Greek}``,
    ``\\p{IsLatin}``, ``\\p{alpha}``, block names, any case), so it is never asked what a name means.

    The characters are those of Unicode 17, from the tables of ``_properties`` (which every port of the evaluator
    has), whatever version of Unicode the regex package that is installed has. The package's own property is
    written with what is different between the two (see ``_host_form``)."""
    if not _properties:
        from . import _properties as tables

        values = [names.split(" ") for names in tables.GENERAL_CATEGORY.split(";")]
        shorts = [names[0] for names in values]
        for names in values:
            short = names[0]
            # A group (L, LC, M and so on) has the values whose names start with its letter.
            members = _GROUPS.get(short) or (
                [s for s in shorts if len(s) == 2 and s[0] == short and s[1].islower()] or [short]
            )
            entry = ((f"\\p{{gc={short}}}", f"\\P{{gc={short}}}"), _union([f"gc:{m}" for m in members]))
            for name in names:
                _properties[name] = _properties[f"gc={name}"] = _properties[f"General_Category={name}"] = entry
        for script in tables.SCRIPT.split(";"):
            names = script.split(" ")
            short, long = names[0], (names * 2)[1]
            for kind, property_name in (("sc", "Script"), ("scx", "Script_Extensions")):
                if short == "Zzzz":
                    # What no script has: the characters that are not assigned, of private use and surrogates.
                    ranges = _union(["gc:Cn", "gc:Co", "gc:Cs"])
                elif short == "Hrkt":
                    # No character has this value.
                    ranges = _union([])
                else:
                    table = f"scx:{long}" if kind == "scx" and f"scx:{long}" in _table_names() else f"sc:{long}"
                    ranges = _union([table])
                entry = ((f"\\p{{{kind}={short}}}", f"\\P{{{kind}={short}}}"), ranges)
                for name in names:
                    _properties[f"{kind}={name}"] = _properties[f"{property_name}={name}"] = entry

        def assigned() -> _Ranges:
            return _complement(_table("gc:Cn"))

        # The regex package has no property of these names, or (ASCII) has a block of the name.
        special: dict[str, tuple[tuple[str, str] | None, Callable[[], _Ranges]]] = {
            "ASCII": (None, lambda: [(0, 0x7F)]),
            "Any": (None, lambda: [(0, 0x10FFFF)]),
            "Assigned": (("\\P{gc=Cn}", "\\p{gc=Cn}"), assigned),
            "Changes_When_NFKC_Casefolded": (None, _union(["b:Changes_When_NFKC_Casefolded"])),
        }
        for property_names in tables.BINARY.split(";"):
            long = property_names.split(" ")[0]
            binary = special.get(long) or ((f"\\p{{{long}=Yes}}", f"\\P{{{long}=Yes}}"), _union([f"b:{long}"]))
            for name in property_names.split(" "):
                _properties[name] = binary
    known = _properties.get(body)
    return None if known is None else (known[0], known[1]())


def _table_names() -> dict[str, str]:
    if not _table_texts:
        _table("gc:Lu")
    return _table_texts


# What the regex package that is installed has for a property expression: its characters (None: it has no such
# property), and how a property is written for it (see _host_form).
_host_sets: dict[str, _Ranges | None] = {}
_host: dict[str, tuple[str, str, int]] = {}
_every_character: list[str] = []


def _host_set(expression: str) -> _Ranges | None:
    """The characters that the regex package that is installed has for a property expression, or None when it has
    no such property. The package has the version of Unicode of its release (16 in 2025.9.1, 18 in 2026.9.29).

    Every character is asked about, once for an expression, which takes some milliseconds. The text of every
    character (4 MB) is kept from the first time."""
    return _found(expression, _host_sets)


def _found(expression: str, known: dict[str, _Ranges | None]) -> _Ranges | None:
    if expression not in known:
        import regex

        if not _every_character:
            # (Joining a million strings of one character takes 85 MB while it is done.)
            units = array("I", range(0x110000)).tobytes()
            _every_character.append(
                units.decode("utf-32-le" if sys.byteorder == "little" else "utf-32-be", "surrogatepass")
            )
        try:
            compiled = regex.compile(f"(?:{expression})+", cache_pattern=False)
            known[expression] = [(m.start(), m.end() - 1) for m in compiled.finditer(_every_character[0])]
        except regex.error:
            known[expression] = None
    return known[expression]


def _host_form(here: str, other: str, target: _Ranges) -> tuple[str, str, int]:
    """How the characters ``target`` are written for the regex package, given the package's own expression for
    them (``here``) and for all the others (``other``): members of a class, one more class that stands beside it
    (either may be empty: the characters are those of the two together), and how many ranges the two have, which
    is what a character costs the engine.

    The engine looks a character up in a table for a property of its own, where it goes through the ranges of a
    class one by one: 3 nanoseconds a character against 45 to 1,000 for the 684 ranges of ``\\p{L}``. But its
    property is that of its version of Unicode, and the characters here are those of Unicode 17. So its property
    is written with what is different:

    - the same characters: the property;
    - the engine has too few: the property and the ranges it lacks, as members of a class;
    - the engine has too many: ``[^\\P{..}ranges]``, the characters that are not outside the property and not in
      the ranges it has too many of, which is a class of its own.

    Two releases of Unicode differ by tens of ranges for the properties they differ on at all. Where the engine
    has no such property, or what is different is longer than the ranges themselves, the ranges are written. What
    is written is then asked about for every character, and has to give the characters of the tables exactly."""
    form = _host.get(here)
    if form is None:
        plain = (_table_class(target), "", len(target))
        form = plain
        has = _host_set(here)
        if has is not None:
            add, rem = _minus(target, has), _minus(has, target)
            members = (here if not rem else "") + _table_class(add)
            beside = f"[^{other}{_table_class(rem)}]" if rem else ""
            if len(members) + len(beside) < len(plain[0]):
                written = "|".join(([f"[{members}]"] if members else []) + ([beside] if beside else []))
                if not written or _found(written, {}) == target:
                    form = (members, beside, len(add) + len(rem))
        _host[here] = form
    return form


def _minus(a: _Ranges, b: _Ranges) -> _Ranges:
    """The characters of ``a`` that are not in ``b`` (each merged, in order)."""
    out: _Ranges = []
    j = 0
    for lo, hi in a:
        while j < len(b) and b[j][1] < lo:
            j += 1
        k = j
        while k < len(b) and b[k][0] <= hi:
            if b[k][0] > lo:
                out.append((lo, b[k][0] - 1))
            lo = b[k][1] + 1
            k += 1
        if lo <= hi:
            out.append((lo, hi))
    return out


def _table_class(ranges: _Ranges) -> str:
    """Ranges as members of a class."""
    return "".join(_literal(chr(lo)) if lo == hi else f"{_literal(chr(lo))}-{_literal(chr(hi))}" for lo, hi in ranges)


def _holds(ranges: _Ranges, o: int) -> bool:
    k = bisect_left(ranges, (o + 1,)) - 1
    return k >= 0 and ranges[k][1] >= o


# A property as it is written for an engine: members of a class, a class that stands beside it (either may be
# empty), the characters of the two together, and how many ranges the engine goes through for a character.
_Property = tuple[str, str, _Ranges, int]


def _class_of(members: list[str], beside: list[str], negated: bool = False) -> str:
    """A class as it is written: the characters of the members and of the classes that stand beside them (what a
    class of Python cannot have inside it: ECMA's ``\\S``, and a property of the regex package that has too many
    characters, see ``_host_form``), or with ``negated`` every other character."""
    members = list(dict.fromkeys(members))
    beside = list(dict.fromkeys(beside))
    if not members and not beside:
        return _ANY if negated else _NEVER
    if not negated:
        if not beside and len(members) == 1 and _PROPERTY_RE.fullmatch(members[0]):
            return members[0]
        ways = ([f"[{''.join(members)}]"] if members else []) + beside
        return ways[0] if len(ways) == 1 else "(?:" + "|".join(ways) + ")"
    if not beside and not _both_ways(members):
        return f"[^{''.join(members)}]"
    if not members and len(beside) == 1 and beside[0].startswith("[^"):
        # Every character but those that are not in a class.
        return "[" + beside[0][2:]
    return f"(?:(?!{_class_of(members, beside)}){_ANY})"


def _both_ways(properties: list[str]) -> bool:
    """Whether a class has a ``\\p{...}`` and a ``\\P{...}``. The regex package (2025.9.1 to 2026.9.29 at least) reads
    a class with a property and the same property negated as every character and then drops a ``^`` before it, so
    ``[^\\p{L}\\P{L}]`` takes any character there. A negated class with both is written as a character that the
    class without the ``^`` does not take."""
    return any(p.startswith("\\p") for p in properties) and any(p.startswith("\\P") for p in properties)


class _TooLarge(Exception):
    """The pattern is over a limit of size. ``reason`` names the limit and its value. ``unread``: the pattern was
    not read to its end, so nothing is known of whether it is valid."""

    def __init__(self, reason: str = "", unread: bool = False) -> None:
        super().__init__(reason)
        self.unread = unread
        # Whether it is the length of the translation that is over the limit.
        self.length = not reason
        self.reason = reason or (
            f"its translation for Python's engines is longer than {_MAX_TRANSLATED:,} characters once its "
            "repetitions are written out"
        )


# The characters of ASCII that are written with a backslash: those that mean something to either engine outside a
# class or inside one (the regex package reads ``[``, ``&&``, ``||``, ``~~`` and ``--`` in a class).
_ESCAPED = frozenset("\\.^$*+?{}[]|()-&~")


def _literal(c: str) -> str:
    """A character as a pattern literal (outside or inside a class). A control character, a surrogate and what is
    between U+007F and U+00A0 are escapes. Any other character is written as it is, with a backslash before the
    ones of ``_ESCAPED``: a pattern of plain characters is as long translated as it was."""
    o = ord(c)
    if o < 32 or 0x7F <= o <= 0xA0:
        return f"\\x{o:02x}"
    if o < 128:
        return "\\" + c if c in _ESCAPED else c
    if 0xD800 <= o <= 0xDFFF:
        return f"\\u{o:04x}"
    return c


def _merged(ranges: _Ranges) -> _Ranges:
    out: _Ranges = []
    for lo, hi in sorted(ranges):
        if out and lo <= out[-1][1] + 1:
            if hi > out[-1][1]:
                out[-1] = (out[-1][0], hi)
        else:
            out.append((lo, hi))
    return out


def _complement(ranges: _Ranges) -> _Ranges:
    out: _Ranges = []
    at = 0
    for lo, hi in _merged(ranges):
        if lo > at:
            out.append((at, lo - 1))
        at = hi + 1
    if at <= 0x10FFFF:
        out.append((at, 0x10FFFF))
    return out


_fold_tables: dict[bool, tuple[list[int], dict[int, str]]] = {}


def _fold_table(unicode: bool) -> tuple[list[int], dict[int, str]]:
    """The characters that are equal to another without case, in order, and for each the set it is in. Built when
    the first pattern with a case-insensitive group is compiled."""
    table = _fold_tables.get(unicode)
    if table is None:
        from . import _casefold

        sets: dict[int, str] = {}
        for members in (_casefold.UNICODE if unicode else _casefold.LEGACY).split(" "):
            for c in members:
                sets[ord(c)] = members
        table = _fold_tables[unicode] = (sorted(sets), sets)
    return table


# The kinds of node of a pattern's tree.
_TEXT, _SEQUENCE, _ALTERNATION, _GROUP, _LOOK, _REPEAT, _REFERENCE = range(7)
# Where a reference is, from its group (see the module's description).
_EMPTY, _PLAIN, _DYNAMIC = range(3)


class _Node:
    __slots__ = (
        "back",
        "behind",
        "chars",
        "cost",
        "deep",
        "first",
        "group",
        "icase",
        "items",
        "kept",
        "kind",
        "last",
        "lazy",
        "max",
        "min",
        "negative",
        "nothing",
        "only_nothing",
        "others",
        "parent",
        "same",
        "shut",
        "state",
        "text",
        "weight",
    )

    def __init__(self, kind: int, text: str = "", items: list[_Node] | None = None, group: int = 0) -> None:
        self.kind = kind
        # _TEXT: a translated piece that is matched as it is written (a character, a class, an assertion).
        self.text = text
        self.items: list[_Node] = [] if items is None else items
        # _GROUP: the number of a capturing group, or 0. _REFERENCE: the group it names.
        self.group = group
        self.icase = False
        self.behind = False
        self.negative = False
        self.min = 0
        self.max = 0  # -1: no maximum
        self.lazy = False
        # _REPEAT, set by _Plain: the repetition is left as it is written.
        self.kept = False
        # Whether the node can match without taking a character (None: not worked out yet).
        self.nothing: bool | None = None
        self.only_nothing: bool | None = None
        # _TEXT that takes a character: the characters it can take (None: any), or with ``others`` every character
        # but those. On a _TEXT that takes none, ``others`` marks a \B.
        self.chars: _Ranges | None = None
        self.others = False
        # _TEXT that takes a character: how many ranges the regex package goes through, one by one, to find that a
        # character is not in the class (see _Writer._scanned).
        self.cost = 0
        self.state = _PLAIN
        # _REFERENCE, set by _Writer: the name of the test that two characters are equal without case, when the
        # reference is inside a case-insensitive group and its group can take a character that has another case.
        self.same = ""
        # Set by _Writer: the parent, the node's place in the tree (it holds the nodes first < n.first < last), and
        # whether it is matched backwards (it is in a lookbehind).
        self.parent: _Node | None = None
        self.first = 0
        self.last = 0
        self.back = False
        # Set by _Writer as it writes: how many copies of a character or a reference the regex package makes of
        # the node (see MAX_COPIES).
        self.weight = 1
        # Set by _Writer as it writes, for the regex package (see _Writer._keeps): how many groups one pass through
        # the node closes without taking a character for each, and how many it closes for a character it takes.
        self.shut = 0
        self.deep = 0


def _matches_nothing(n: _Node) -> bool:
    """Whether the node can match without taking a character. It may say yes for a node that cannot."""
    if n.nothing is None:
        k = n.kind
        if k == _SEQUENCE:
            n.nothing = all(_matches_nothing(x) for x in n.items)
        elif k == _ALTERNATION:
            n.nothing = any(_matches_nothing(x) for x in n.items)
        elif k == _GROUP:
            n.nothing = _matches_nothing(n.items[0])
        elif k == _REPEAT:
            n.nothing = n.min == 0 or _matches_nothing(n.items[0])
        else:
            n.nothing = True
    return n.nothing


def _only_nothing(n: _Node) -> bool:
    """Whether the node matches nothing wherever it matches: it has assertions, lookarounds and references that are
    never read after their group has matched, and nothing that takes a character. Asked once every reference has
    its place."""
    if n.only_nothing is None:
        k = n.kind
        if k == _TEXT:
            n.only_nothing = n.nothing is not False
        elif k == _REFERENCE:
            n.only_nothing = n.state == _EMPTY
        elif k == _LOOK:
            n.only_nothing = True
        elif k == _REPEAT:
            n.only_nothing = n.max == 0 or _only_nothing(n.items[0])
        else:
            n.only_nothing = all(_only_nothing(x) for x in n.items)
    return n.only_nothing


def _sees_code_units(n: _Node) -> bool:
    """Whether a pattern without the u flag can tell a character beyond the Basic Multilingual Plane from its two
    surrogates: something in it can take a character from U+D800 up, or it has a lookaround or a ``\\B``, which can
    hold between the two (nothing else holds there that does not hold at the start of the text too)."""
    if n.kind == _TEXT:
        if n.nothing is not False:
            return n.others
        return n.chars is None or n.others or any(hi >= 0xD800 for _, hi in n.chars)
    return n.kind == _LOOK or any(_sees_code_units(x) for x in n.items)


def _taken(n: _Node, out: _Ranges) -> bool:
    """Adds to ``out`` the characters that the text the node matches can have. False: it can have any."""
    k = n.kind
    if k == _TEXT:
        if n.nothing is not False:
            return True
        if n.chars is None:
            return False
        out.extend(_complement(n.chars) if n.others else n.chars)
        return True
    if k == _LOOK:
        return True
    if k == _REFERENCE:
        return n.state == _EMPTY
    return all(_taken(x, out) for x in n.items)


class _Keep(Exception):
    """A repetition is left as it is written (see ``_Plain``)."""


class _Plain:
    """Writes a pattern that no reference reads a group of without the iterations that match nothing.

    ECMA-262 refuses an iteration that matches nothing once the minimum count is met, and leaves the repetition
    there. Both engines accept the iteration and go on to what follows the repetition, and when that fails they
    leave the repetition as ECMA-262 does and try what follows again. One repetition inside another does that at
    each level, so a text that does not match takes a time that doubles with each repetition, where V8 answers at
    once. ``(?:(?:(-|){3,})+|){3}-$`` took over a minute for a text of twelve characters.

    Where no reference reads a group, a pattern only says which texts match, and which way a text matches does not
    show. So a repetition ``X{m,n}`` whose body can match nothing is written with a body that cannot, ``Y{0,n}``,
    where Y matches what X matches except nothing: the iterations that matched nothing are left out, and those up
    to the minimum were free to. That holds when X matches nothing wherever it is tried, or when m is 0. A body
    that matches nothing only where an assertion holds, under a minimum of 1 or more, is left as it is written, and
    so is each repetition around it whose body can match nothing.

    ``without`` gives Y for a node and ``nothing`` gives what the node is where it matches nothing (its
    assertions), each None when there is no such match. Y for a sequence is an alternative for each item that can
    be the first to take a character: the items before it as they are where they match nothing, the item with
    what it takes, and the rest as written. A pattern whose tree would grow past ``_MAX_PLAIN`` nodes is left as it
    is."""

    def __init__(self) -> None:
        self.nodes = 0

    def copy(self, n: _Node, items: list[_Node] | None = None) -> _Node:
        self.nodes += 1
        if self.nodes > _MAX_PLAIN:
            raise _TooLarge()
        out = _Node(n.kind, n.text, [self.copy(x) for x in n.items] if items is None else items)
        out.behind, out.negative, out.min, out.max, out.lazy = n.behind, n.negative, n.min, n.max, n.lazy
        out.chars, out.others, out.cost = n.chars, n.others, n.cost
        if n.kind == _TEXT:
            out.nothing = n.nothing
        elif n.kind == _REPEAT:
            out.kept = n.kept
        return out

    def plain(self, n: _Node) -> _Node:
        """The node with every repetition inside it written without the iterations that match nothing."""
        k = n.kind
        if k == _REFERENCE:
            # No reference is read after its group has matched: each matches nothing.
            return _Node(_SEQUENCE)
        if k != _REPEAT:
            return self.copy(n, [self.plain(x) for x in n.items])
        if n.max == 0:
            return _Node(_SEQUENCE)
        body = self.plain(n.items[0])
        out = self.copy(n, [body])
        if not _matches_nothing(body):
            return self.one_repetition(out)
        try:
            rest, empty = self.without(body), self.nothing(body)
        except _Keep:
            out.kept = True
            return out
        if empty is None:
            return out
        if rest is None:
            return empty if n.min else _Node(_SEQUENCE)
        if n.min and not _is_nothing(empty):
            out.kept = True
            return out
        out.items = [rest]
        out.min = 0
        if out.max >= _MAX_COUNT:
            out.max = -1
        return self.one_repetition(out)

    @staticmethod
    def one_repetition(out: _Node) -> _Node:
        """``(?:Y+)*`` as ``Y*`` and ``(?:Y+)+`` as ``Y+``: a repetition with no maximum whose body is nothing but
        another one matches what the inner one does, any number of times. Written one inside the other, the
        engines try every way to share a text between the two, and the regex package compiles each ``+`` as two
        copies of its body, so twenty of them inside each other took 331 MB to compile."""
        if out.max != -1 or out.min > 1:
            return out
        body = out.items[0]
        while body.kind == _GROUP or (body.kind == _SEQUENCE and len(body.items) == 1):
            body = body.items[0]
        if body.kind != _REPEAT or body.max != -1 or body.min > 1 or body.kept:
            return out
        body.min *= out.min
        body.lazy = out.lazy
        return body

    def without(self, n: _Node) -> _Node | None:
        """What matches the texts the node matches other than the empty text, or None when it matches no other."""
        k = n.kind
        if k == _TEXT:
            return self.copy(n) if n.nothing is False else None
        if k == _LOOK:
            return None
        if k == _GROUP:
            inner = self.without(n.items[0])
            return None if inner is None else self.copy(n, [inner])
        if k == _ALTERNATION:
            return self.either([self.without(x) for x in n.items])
        if k == _SEQUENCE:
            ways: list[_Node | None] = []
            before: list[_Node] = []
            for i, x in enumerate(n.items):
                first = self.without(x)
                if first is not None:
                    after = [self.copy(y) for y in n.items[i + 1 :]]
                    ways.append(_Node(_SEQUENCE, items=[*[self.copy(b) for b in before], first, *after]))
                empty = self.nothing(x)
                if empty is None:
                    break
                before.append(empty)
            return self.either(ways)
        if n.kept:
            raise _Keep()
        # A repetition whose body takes a character.
        if n.min:
            return self.copy(n)
        out = self.copy(n)
        out.min = 1
        return out

    def nothing(self, n: _Node) -> _Node | None:
        """What holds where the node matches the empty text (an empty sequence: it does anywhere), or None when it
        never does."""
        k = n.kind
        if k == _TEXT:
            return None if n.nothing is False else self.copy(n)
        if k == _LOOK:
            return self.copy(n)
        if k == _GROUP:
            return self.nothing(n.items[0])
        if k == _ALTERNATION:
            ways = [self.nothing(x) for x in n.items]
            if any(w is not None and _is_nothing(w) for w in ways):
                return _Node(_SEQUENCE)
            return self.either(ways)
        if k == _SEQUENCE:
            items = []
            for x in n.items:
                empty = self.nothing(x)
                if empty is None:
                    return None
                if not _is_nothing(empty):
                    items.append(empty)
            return _Node(_SEQUENCE, items=items)
        if n.kept:
            raise _Keep()
        return _Node(_SEQUENCE) if n.min == 0 else None

    @staticmethod
    def either(ways: list[_Node | None]) -> _Node | None:
        found = [w for w in ways if w is not None]
        if len(found) < 2:
            return found[0] if found else None
        return _Node(_GROUP, items=[_Node(_ALTERNATION, items=found)])


def _plain(root: _Node, writer: _Writer) -> _Node | None:
    """The tree of a pattern without the iterations that match nothing (see ``_Plain``), or None when nothing is to
    be written another way or the tree would be too large.

    In a pattern where a reference reads a group, the same holds for a repetition that has no such reference and no
    such group inside it: all that the rest of the pattern sees of it is where it ends, and it ends in the same
    places. A repetition inside a lookaround with a group that is read outside it is left as it is, as the first
    way through such a lookaround decides what the group holds."""
    plain = _Plain()
    if writer.live:
        read = sorted(writer.groups[g].first for g in writer.read)
        if not _plain_parts(root, writer, plain, read):
            return None
        # The tree is read again. A reference that is never read after its group has matched may have had its
        # group inside a repetition that is now written another way, without groups.
        _drop_empty_references(root)
        return root
    try:
        return plain.plain(root)
    except _TooLarge:
        return None


def _drop_empty_references(n: _Node) -> None:
    if n.kind == _REFERENCE and n.state == _EMPTY:
        n.kind = _SEQUENCE
    for x in n.items:
        _drop_empty_references(x)


def _plain_parts(n: _Node, writer: _Writer, plain: _Plain, read: list[int]) -> bool:
    changed = False
    for i, x in enumerate(n.items):
        if x.kind == _LOOK and x.first in writer.leaky:
            continue
        if (
            x.kind == _REPEAT
            and _repeats_inside_repeats(x)
            and not writer._holds_one(writer.live, x)
            and not writer._holds_one(read, x)
        ):
            try:
                n.items[i] = plain.plain(x)
                changed = True
            except _TooLarge:
                pass  # this one as it is written, and any other that would pass the limit
        elif _plain_parts(x, writer, plain, read):
            changed = True
    return changed


def _is_nothing(n: _Node) -> bool:
    """Whether the node is an empty sequence: it matches the empty text anywhere, and nothing else."""
    return n.kind == _SEQUENCE and not n.items


def _repeats_inside_repeats(n: _Node, inside: bool = False) -> bool:
    """Whether a repetition whose body can match nothing is inside a repetition or has one inside it."""
    if n.kind == _REPEAT and n.max != 0:
        if inside and _matches_nothing(n.items[0]):
            return True
        if _matches_nothing(n.items[0]) and _has_a_repeat(n.items[0]):
            return True
        inside = True
    return any(_repeats_inside_repeats(x, inside) for x in n.items)


def _has_a_repeat(n: _Node) -> bool:
    return (n.kind == _REPEAT and n.max != 0) or any(_has_a_repeat(x) for x in n.items)


_EVERY: _Ranges = [(0, 0x10FFFF)]
_INFINITE = float("inf")


def _meet(a: _Ranges, b: _Ranges) -> bool:
    """Whether two sets of characters (each merged, in order) have a character in common."""
    i = j = 0
    while i < len(a) and j < len(b):
        if a[i][1] < b[j][0]:
            i += 1
        elif b[j][1] < a[i][0]:
            j += 1
        else:
            return True
    return False


class _NotLinear(Exception):
    pass


class _Linear:
    """Proves that the standard library's engine matches a pattern in time proportional to the text, whatever the
    text is. That engine cannot be stopped once it has started (it looks at signals in the main thread only, and has
    no limit of its own), so with the limit of a match in force it is given the patterns this class proves and no
    others. Every other pattern goes to the regex package, which takes a timeout.

    The engine tries one way through a pattern and goes back to the last choice when it fails. The proof is that no
    text makes it go over more than a bounded number of characters a second time. It holds when, at every choice
    (the alternatives of an alternation, one more iteration of a repetition or none), the next character of the
    text says which way to go: the sets of characters that the ways can start with, or that can follow the choice
    where a way can match nothing, have no character in common, and at most one way can match nothing. A way that
    is not taken then fails at its first character. The body of a repetition has to take a character.

    Some things are allowed beyond that, each for a bounded cost. ``factor`` is the product of the costs, and the
    work for a character of the text is at most ``factor`` times the nodes of the tree that a character can pass
    more than once in a try: those inside a repetition, and in a pattern that does not start with ``^``, which is
    tried at every place in the text, all of them. In a pattern that starts with ``^`` the nodes outside every
    repetition are passed once for a text, so a pattern of 16,000 digits one after another is proved.

    - A pattern that can take a bounded number of characters and has no lookahead, when all the ways through it
      come to few characters (``^(?:es5|es6|es2015)$``): the engine cannot do more than try each way.
    - A repetition where the next character does not say whether to leave it, such as the first of
      ``^[a-z0-9]*[a-z]$``, when what follows it can take ``w`` characters at most and still fail. Each place to
      leave it is tried once, and each try costs ``w``.
    - An alternation whose alternatives start with the same character, when they take a bounded number of
      characters and no text matches one and starts with a match of another (``txt|trie``): one of them matches at
      most, and the others cost what they are. Where one can start with another (``http|https``) each is a way to
      go on, which is allowed outside any repetition, for a cost of the number of ways.
    - A lookahead of at most ``w`` characters, anywhere. A lookahead with no such bound (``(?=.*[a-z])``) is
      allowed where it is tried once only: at the start of a pattern that starts with ``^``.
    - A pattern that does not start with ``^`` is tried at every place in the text, so a try that fails may take
      ``w`` characters at most. ``[a-z][a-z0-9]*`` takes one: after its first character nothing can fail.

    A pattern with a reference that reads a group, or a lookbehind, is not proved. The proof reads the tree that is
    written for the engine, so it is of the translation and not of the pattern as ECMA-262 has it."""

    # The most a proved pattern may cost: the product of the costs above, and that times the nodes of its tree.
    MAX_FACTOR = 64
    MAX_WORK = 4096
    # The most ways through a bounded part that are written out to compare them, and the most characters they may
    # come to for a pattern that is tried at every place in the text.
    MAX_WAYS = 256
    MAX_WAYS_WORK = 1024

    def __init__(self) -> None:
        self.factor = 1.0
        # The nodes of the tree, and those of them that are inside a repetition.
        self.nodes = 0
        self.repeated = 0
        self.firsts: dict[int, _Ranges] = {}
        self.lengths: dict[int, float] = {}
        # The lookaheads that are tried once (by the place of the node in memory).
        self.once: set[int] = set()

    def proves(self, root: _Node) -> bool:
        try:
            anchored = self.anchored(root)
            if self.bounded(root, anchored):
                return True
            if anchored:
                self.mark_once(root)
            else:
                top = root
                while top.kind == _GROUP:
                    top = top.items[0]
                # An alternative that starts with ^ fails at once everywhere but at the start of the text.
                ways = top.items if top.kind == _ALTERNATION else [top]
                self.cost(max([self.fails_within(x, True) for x in ways if not self.anchored(x)], default=0.0))
            self.visit(root, [], True, 0.0, True, True)
        except _NotLinear:
            return False
        return self.factor * (self.repeated if anchored else self.nodes) <= self.MAX_WORK

    def cost(self, width: float) -> None:
        self.factor *= 1 + width
        if self.factor > self.MAX_FACTOR:
            raise _NotLinear()

    def bounded(self, root: _Node, anchored: bool) -> bool:
        """Whether the pattern takes a bounded number of characters, has no lookahead, and has few ways through
        it."""
        # (A pattern longer than the bound has too many characters in one way alone, and is not written out.)
        if self.length(root) > self.MAX_WORK or self.has_look(root):
            return False
        ways = self.ways(root)
        if ways is None:
            return False
        work = sum(len(w) + 1 for w in ways)
        return work <= (self.MAX_WORK if anchored else self.MAX_WAYS_WORK)

    def has_look(self, n: _Node) -> bool:
        return n.kind == _LOOK or any(self.has_look(x) for x in n.items)

    def ways(self, n: _Node) -> list[tuple[_Ranges, ...]] | None:
        """The ways through a node that takes a bounded number of characters, each as the sets of characters it
        takes in order (an assertion or a lookahead takes none). None: there are too many, or no bound."""
        k = n.kind
        if k == _TEXT:
            return [()] if n.nothing is not False else [(self.first(n),)]
        if k == _LOOK:
            return [()]
        if k == _REFERENCE:
            return [()] if n.state == _EMPTY else None
        if k == _GROUP:
            return self.ways(n.items[0])
        if k == _ALTERNATION:
            out: list[tuple[_Ranges, ...]] = []
            for x in n.items:
                more = self.ways(x)
                if more is None or len(out) + len(more) > self.MAX_WAYS:
                    return None
                out.extend(more)
            return out
        if k == _SEQUENCE:
            out = [()]
            for x in n.items:
                more = self.ways(x)
                if more is None or len(out) * len(more) > self.MAX_WAYS:
                    return None
                if more != [()]:
                    out = [a + b for a in out for b in more]
            return out
        # A repetition.
        if n.max == 0 or _only_nothing(n.items[0]):
            return [()]
        if n.max == -1 or n.max > self.MAX_WAYS:
            return None
        body = self.ways(n.items[0])
        if body is None:
            return None
        out = []
        some: list[tuple[_Ranges, ...]] = [()]
        for count in range(n.max + 1):
            if count >= n.min:
                if len(out) + len(some) > self.MAX_WAYS:
                    return None
                out.extend(some)
            if count < n.max:
                if len(some) * len(body) > self.MAX_WAYS or len(some[0]) > self.MAX_WAYS:
                    return None
                some = [a + b for a in some for b in body]
        return out

    @staticmethod
    def starts_with(a: tuple[_Ranges, ...], b: tuple[_Ranges, ...]) -> bool:
        """Whether some text that one way takes can start with a text that the other takes."""
        for x, y in zip(a, b):  # noqa: B905 - the shorter one is compared with the start of the longer
            if not _meet(x, y):
                return False
        return True

    def first(self, n: _Node) -> _Ranges:
        """The characters the text that the node matches can start with."""
        key = id(n)
        got = self.firsts.get(key)
        if got is None:
            k = n.kind
            if k == _TEXT:
                if n.nothing is not False:
                    got = []
                elif n.chars is None:
                    got = _EVERY
                else:
                    got = _complement(n.chars) if n.others else _merged(n.chars)
            elif k == _SEQUENCE:
                parts: _Ranges = []
                for x in n.items:
                    parts.extend(self.first(x))
                    if not _matches_nothing(x):
                        break
                got = _merged(parts)
            elif k in (_LOOK, _REFERENCE) or (k == _REPEAT and n.max == 0):
                got = []
            else:
                got = _merged([r for x in n.items for r in self.first(x)])
            self.firsts[key] = got
        return got

    def length(self, n: _Node) -> float:
        """The most characters the node can take."""
        key = id(n)
        got = self.lengths.get(key)
        if got is None:
            k = n.kind
            if k == _TEXT:
                got = 0.0 if n.nothing is not False else 1.0
            elif k in (_LOOK, _REFERENCE):
                got = 0.0
            elif k == _ALTERNATION:
                got = max(self.length(x) for x in n.items)
            elif k == _REPEAT:
                body = self.length(n.items[0])
                got = 0.0 if n.max == 0 or body == 0 else _INFINITE if n.max == -1 else n.max * body
            else:
                got = sum(self.length(x) for x in n.items)
            self.lengths[key] = got
        return got

    def anchored(self, n: _Node) -> bool:
        """Whether every way through the node starts with ``^``, so that a search is tried at the start of the text
        and nowhere else."""
        k = n.kind
        if k == _TEXT:
            return n.text == "^"
        if k == _SEQUENCE:
            for x in n.items:
                if self.anchored(x):
                    return True
                # Before the ^ there may be what takes no character, other than a lookahead, which would be tried
                # at every place.
                if self.length(x) != 0 or self.has_look(x):
                    return False
            return False
        if k == _ALTERNATION:
            return all(self.anchored(x) for x in n.items)
        if k == _GROUP:
            return self.anchored(n.items[0])
        if k == _REPEAT:
            return n.min >= 1 and self.anchored(n.items[0])
        return False

    def mark_once(self, n: _Node) -> None:
        """Marks the lookaheads at the start of an anchored pattern, before anything that takes a character or is a
        choice. Each is tried once."""
        while n.kind == _GROUP:
            n = n.items[0]
        if n.kind != _SEQUENCE:
            return
        for x in n.items:
            if x.kind == _LOOK:
                self.once.add(id(x))
            elif x.kind != _TEXT or x.nothing is False:
                return

    def never_fails(self, n: _Node) -> bool:
        k = n.kind
        if k == _TEXT:
            return n.text in ("", "(?:)")
        if k == _REFERENCE:
            return n.state == _EMPTY
        if k == _LOOK:
            return False
        if k == _REPEAT:
            return n.min == 0 or n.max == 0
        if k == _ALTERNATION:
            return any(self.never_fails(x) for x in n.items)
        return all(self.never_fails(x) for x in n.items)

    def fails_within(self, n: _Node, last: bool) -> float:
        """The most characters a try at the node can take and still fail. ``last``: once the node has matched,
        nothing after it can fail."""
        k = n.kind
        if not last:
            return self.length(n) if k != _LOOK else self.length(n.items[0])
        if k == _TEXT:
            return 0.0 if n.nothing is not False else 1.0
        if k == _LOOK:
            return self.length(n.items[0])
        if k == _REFERENCE:
            return 0.0
        if k == _GROUP:
            return self.fails_within(n.items[0], True)
        if k == _ALTERNATION:
            return max(self.fails_within(x, True) for x in n.items)
        if k == _REPEAT:
            if n.max == 0 or n.min == 0:
                return 0.0
            body = n.items[0]
            before = (n.min - 1) * self.length(body) if n.min > 1 else 0.0
            return before + self.fails_within(body, True)
        # A sequence: the items after the last one that can fail cost nothing.
        items = n.items
        j = len(items) - 1
        while j >= 0 and self.never_fails(items[j]):
            j -= 1
        if j < 0:
            return 0.0
        width = self.fails_within(items[j], True)
        for x in items[:j]:
            width += self.fails_within(x, False)
        return width

    def visit(self, n: _Node, follow: _Ranges, end: bool, after: float, never: bool, single: bool) -> None:
        """Checks the node. ``follow``: the characters that can come after it. ``end``: the pattern (or the
        lookahead the node is in) can end after it. ``after``: the most characters that what comes after it can
        take and still fail, and ``never``: what comes after it cannot fail. ``single``: the node is in no
        repetition, so it is tried once for each try at the pattern."""
        self.nodes += 1
        if not single:
            self.repeated += 1
        k = n.kind
        if k == _TEXT:
            return
        if k == _REFERENCE:
            if n.state != _EMPTY:
                raise _NotLinear()
            return
        if k == _GROUP:
            self.visit(n.items[0], follow, end, after, never, single)
            return
        if k == _LOOK:
            if n.behind:
                raise _NotLinear()
            width = self.length(n.items[0])
            if width == _INFINITE:
                if id(n) not in self.once:
                    raise _NotLinear()
            else:
                self.cost(width)
            self.visit(n.items[0], [], True, 0.0, True, single)
            return
        if k == _SEQUENCE:
            items = n.items
            for i in range(len(items) - 1, -1, -1):
                x = items[i]
                self.visit(x, follow, end, after, never, single)
                if _matches_nothing(x):
                    follow = _merged(follow + self.first(x))
                else:
                    follow, end = self.first(x), False
                if never:
                    if not self.never_fails(x):
                        after, never = self.fails_within(x, True), False
                else:
                    after += self.length(x)
            return
        if k == _ALTERNATION:
            seen: _Ranges = []
            nothing = clash = False
            for x in n.items:
                starts = self.first(x)
                if _matches_nothing(x):
                    clash = clash or nothing
                    nothing = True
                    starts = _merged(starts + follow)
                clash = clash or _meet(seen, starts)
                seen = _merged(seen + starts)
            if clash:
                # The next character does not say which alternative to take.
                ways = self.ways(n) if self.length(n) <= self.MAX_WORK else None
                if ways is None:
                    raise _NotLinear()
                if any(self.starts_with(a, b) for i, a in enumerate(ways) for b in ways[:i]):
                    # A text can match two of them. Each is a way to go on.
                    if not single:
                        raise _NotLinear()
                    self.cost(len(ways) - 1)
                self.nodes += sum(len(w) for w in ways)
                if not single:
                    self.repeated += sum(len(w) for w in ways)
            for x in n.items:
                self.visit(x, follow, end, after, never, single)
            return
        # A repetition.
        if n.max == 0:
            return
        body = n.items[0]
        if _only_nothing(body):
            # Written as the body once, or as nothing.
            if n.min:
                self.visit(body, follow, end, after, never, single)
            return
        if n.max == 1 and n.min == 1:
            self.visit(body, follow, end, after, never, single)
            return
        if _matches_nothing(body):
            raise _NotLinear()
        starts = self.first(body)
        if n.min != n.max and _meet(starts, follow):
            # The next character does not say whether to leave the repetition. Each place to leave it is tried.
            if after == _INFINITE:
                raise _NotLinear()
            self.cost(after)
        if n.max == 1:
            self.visit(body, follow, end, after, never, single)
        else:
            rest = _INFINITE if n.max == -1 else after + (n.max - 1) * self.length(body)
            self.visit(body, _merged(follow + starts), end, rest, False, False)


class _Translator:
    def __init__(
        self,
        pattern: str,
        unicode: bool,
        ascii_flag: bool,
        plain: bool = True,
        share: bool = False,
        counts: bool = False,
        steps: bool = False,
    ) -> None:
        self.p = pattern
        # Whether every repetition of one class is written for the regex package with a step for each character,
        # which is how a pattern is written for a long text (see _search).
        self.steps = steps
        # Set by translate for the regex package: whether the pattern has a repetition of one class that is not
        # written so.
        self.scans = False
        # Whether repetitions inside repetitions are written without the iterations that match nothing (_Plain).
        self.plain = plain
        # Whether a long class is written once for the regex package and called (see _Writer.write).
        self.share = share
        # Whether a minimum count of one character is written for the regex package without the count (see
        # _Writer._counted).
        self.counts = counts
        # With the ASCII flag (the standard library's engine), \d, \w and \b are ECMA's; without it (the regex
        # package, whose ASCII flag would also narrow \p{...}), they are spelled out.
        self.ascii_flag = ascii_flag
        self.n = len(pattern)
        self.u = unicode
        self.i = 0
        # The capturing groups of the whole pattern and whether any has a name, from a scan before parsing: what \1
        # and \k mean depends on groups that may follow them.
        self.group_count = 0
        self.has_names = False
        self.groups = 0
        # The groups of each name, each with the alternatives it is inside.
        self.names: dict[str, list[tuple[int, list[tuple[int, int]]]]] = {}
        self.named_refs: list[tuple[_Node, str]] = []
        self.needs_regex = False
        # The modifiers in force, which only a group such as (?i:...) changes.
        self.icase = False
        self.multiline = False
        self.dot_all = False
        # The alternatives the parser is inside: (which alternation, which alternative of it).
        self.path: list[tuple[int, int]] = []
        self.alternations = 0
        # Set by translate for a pattern without the u flag: whether a text is matched as its code units (see
        # _sees_code_units).
        self.wide = False
        # Set by translate: whether the standard library's engine is proved to match the translation in time
        # proportional to the text (see _Linear).
        self.linear = False
        # Set by translate for the regex package: how many copies it writes out for the counts (see _copies), and
        # the most characters a text may have for what a match keeps for the groups (None: any number).
        self.copies = 0
        self.most = _MOST_TEXT
        # How many groups the parser is inside.
        self.depth = 0
        # The length of the characters and classes read so far, as they are written (see _one).
        self.size = 0
        # The case-insensitive classes worked out so far, by what was written in them (see _folded_class).
        self.folded: dict[tuple[Any, ...], tuple[str, _Ranges, int]] = {}
        # The long classes read so far, as they are written.
        self.long: set[str] = set()
        # Whether the pattern is only read, to say whether it is valid (see reads).
        self.read_only = False
        # The properties read so far, as they are written (a pattern may have one thousands of times).
        self.properties: dict[tuple[str, bool, bool], _Property] = {}
        # For each property read in a case-insensitive group, the characters that are equal to one of its
        # characters without case and are not its own.
        self.equal: dict[tuple[str, str], _Ranges] = {}

    def error(self) -> _Invalid:
        return _Invalid(self.p)

    def peek(self, k: int = 0) -> str:
        j = self.i + k
        return self.p[j] if j < self.n else ""

    def reads(self) -> bool:
        """Whether the pattern is valid in the grammar. It is read to its end, whatever it would come to written
        for an engine. A pattern that nests too deep to read raises _TooLarge."""
        self.read_only = True
        try:
            self._count_groups()
            self._alternation(top=True)
            return self.i == self.n and all(name in self.names for _, name in self.named_refs)
        except (_Invalid, KeyError, ValueError, IndexError):
            return False

    def translate(self) -> str:
        self._count_groups()
        root = self._alternation(top=True)
        if self.i != self.n:
            raise self.error()
        for node, name in self.named_refs:
            if name not in self.names:
                raise self.error()
            # A name can belong to several groups in separate alternatives. At most one of them has a value, and a
            # group without one matches nothing.
            node.kind = _SEQUENCE
            for group, _ in self.names[name]:
                ref = _Node(_REFERENCE, group=group)
                ref.icase = node.icase
                node.items.append(ref)
        writer = _Writer(root, not self.ascii_flag, self.u, self.share, self.counts, self.steps)
        self.wide = not self.u and _sees_code_units(root)
        if self.plain and _repeats_inside_repeats(root):
            plain = _plain(root, writer)
            if plain is not None:
                writer = _Writer(plain, not self.ascii_flag, self.u, self.share, self.counts, self.steps)
                try:
                    text = writer.write()
                except _TooLarge:
                    # Written that way it is too large. The tree has been changed, so the pattern is read again
                    # and written as it is.
                    again = _Translator(
                        self.p,
                        self.u,
                        self.ascii_flag,
                        plain=False,
                        share=self.share,
                        counts=self.counts,
                        steps=self.steps,
                    )
                    text = again.translate()
                    self.needs_regex = again.needs_regex
                    self.linear = again.linear
                    self.copies = again.copies
                    self.most = again.most
                    self.scans = again.scans
                    return text
                return self._written(writer, text)
        return self._written(writer, writer.write())

    def _written(self, writer: _Writer, text: str) -> str:
        self.needs_regex = self.needs_regex or writer.needs_regex
        if self.ascii_flag:
            self.linear = not self.needs_regex and _Linear().proves(writer.root)
        else:
            # What the counts add to the characters and references the pattern has as it is written.
            self.copies = writer.root.weight - writer.leaves
            self.most = _most_characters(writer.kept)
            self.scans = writer.scans
        return text

    def _count_groups(self) -> None:
        i = 0
        in_class = False
        p = self.p
        while i < self.n:
            c = p[i]
            if c == "\\":
                i += 2
                continue
            if in_class:
                if c == "]":
                    in_class = False
            elif c == "[":
                in_class = True
            elif c == "(":
                if p.startswith("(?<", i) and not p.startswith("(?<=", i) and not p.startswith("(?<!", i):
                    self.group_count += 1
                    self.has_names = True
                elif not p.startswith("(?", i):
                    self.group_count += 1
            i += 1

    def _alternation(self, top: bool) -> _Node:
        self.alternations += 1
        which = self.alternations
        self.path.append((which, 0))
        parts = [self._sequence()]
        while self.peek() == "|":
            self.i += 1
            self.path[-1] = (which, len(parts))
            parts.append(self._sequence())
        self.path.pop()
        if not top and self.peek() != ")":
            raise self.error()
        return parts[0] if len(parts) == 1 else _Node(_ALTERNATION, items=parts)

    def _sequence(self) -> _Node:
        out: list[_Node] = []
        while self.i < self.n and self.peek() not in "|)":
            atom, quantifiable = self._term()
            if self.i < self.n and self.peek() in "*+?{":
                q = self._quantifier()
                if q is not None:
                    if not quantifiable:
                        raise self.error()
                    atom = _Node(_REPEAT, items=[atom])
                    atom.min, atom.max, atom.lazy = q
            out.append(atom)
        return _Node(_SEQUENCE, items=out)

    def _quantifier(self) -> tuple[int, int, bool] | None:
        c = self.peek()
        if c == "{":
            m = _QUANTIFIER_RE.match(self.p, self.i)
            if m is None:
                if self.u:
                    raise self.error()
                return None  # a literal brace (Annex B); the next term reads it
            lo = int(m.group(1))
            hi = lo if m.group(2) is None else -1 if m.group(3) == "" else int(m.group(3))
            if hi != -1 and hi < lo:
                raise self.error()
            self.i = m.end()
            lo = min(lo, _MAX_COUNT)
            if hi >= _MAX_COUNT:
                hi = -1 if m.group(2) is not None else _MAX_COUNT
        else:
            self.i += 1
            lo, hi = (0, -1) if c == "*" else (1, -1) if c == "+" else (0, 1)
        lazy = self.peek() == "?"
        if lazy:
            self.i += 1
        if self.peek() in ("*", "+", "?") or (self.peek() == "{" and _QUANTIFIER_RE.match(self.p, self.i)):
            raise self.error()
        return lo, hi, lazy

    def _term(self) -> tuple[_Node, bool]:
        c = self.peek()
        if c == "^":
            self.i += 1
            return _Node(_TEXT, _LINE_START if self.multiline else "^"), False
        if c == "$":
            self.i += 1
            return _Node(_TEXT, _LINE_END if self.multiline else "\\Z"), False
        if c == ".":
            self.i += 1
            return self._one(_ANY if self.dot_all else _DOT), True
        if c == "(":
            return self._group()
        if c == "[":
            return self._class(), True
        if c == "\\":
            return self._escape()
        if c in "*+?":
            raise self.error()
        if c == "{":
            if self.u or _QUANTIFIER_RE.match(self.p, self.i):
                raise self.error()
            self.i += 1
            return self._one("\\{", [(0x7B, 0x7B)]), True
        if c in "}]":
            if self.u:
                raise self.error()
            self.i += 1
            return self._one("\\" + c, [(ord(c), ord(c))]), True
        self.i += 1
        return self._char(c), True

    def _one(self, text: str, chars: _Ranges | None = None, others: bool = False, cost: int = 0) -> _Node:
        """The node of something that takes one character: one of ``chars``, or any (see ``_Node.chars``)."""
        # What is read is written at least once, but for what a count of 0 is around. A pattern whose characters
        # and classes are already twice the limit of a translation is not read to its end: a class of a property
        # in a case-insensitive group takes milliseconds to work out, and a pattern of 1,000 characters has room
        # for eighty. A long class that is written again counts as a call of it (see _Writer.write).
        if len(text) > (_SHARED if self.share else _LONG_CLASS):
            if text in self.long:
                self.size += _CALL
            else:
                self.long.add(text)
                self.size += len(text)
        else:
            self.size += len(text)
        if self.size > 2 * _MAX_TRANSLATED and not self.read_only:
            raise _TooLarge()
        n = _Node(_TEXT, text)
        n.nothing = False
        n.chars = chars
        n.others = others
        n.cost = cost
        return n

    def _char(self, c: str) -> _Node:
        # A literal character. Inside a case-insensitive group it is the class of the characters equal to it.
        if self.icase:
            members = _fold_table(self.u)[1].get(ord(c))
            if members is not None:
                text = "[" + "".join(_literal(m) for m in members) + "]"
                return self._one(text, [(ord(m), ord(m)) for m in members])
        return self._one(_literal(c), [(ord(c), ord(c))])

    def _group(self) -> tuple[_Node, bool]:
        self.depth += 1
        if self.depth > MAX_NESTING:
            raise _TooLarge(f"its groups nest more than {MAX_NESTING} deep", unread=True)
        try:
            return self._group_inside()
        finally:
            self.depth -= 1

    def _group_inside(self) -> tuple[_Node, bool]:
        p = self.p
        i = self.i
        if p.startswith("(?:", i):
            self.i += 3
            node, quantifiable = _Node(_GROUP), True
        elif p.startswith("(?=", i) or p.startswith("(?!", i):
            self.i += 3
            node, quantifiable = _Node(_LOOK), not self.u
            node.negative = p[i + 2] == "!"
        elif p.startswith("(?<=", i) or p.startswith("(?<!", i):
            self.i += 4
            node, quantifiable = _Node(_LOOK), False
            node.behind = True
            node.negative = p[i + 3] == "!"
            self.needs_regex = True
        elif p.startswith("(?<", i):
            self.i += 3
            name = self._group_name()
            # Two groups may share a name only when they are in separate alternatives of one alternation, so that
            # no match can go through both.
            for _, path in self.names.get(name, ()):
                if not any(a[0] == b[0] and a[1] != b[1] for a, b in zip(path, self.path, strict=False)):
                    raise self.error()
            self.groups += 1
            self.names.setdefault(name, []).append((self.groups, list(self.path)))
            node, quantifiable = _Node(_GROUP, group=self.groups), True
        elif p.startswith("(?", i):
            self.i += 2
            return self._modifier_group(), True
        else:
            self.i += 1
            self.groups += 1
            node, quantifiable = _Node(_GROUP, group=self.groups), True
        node.items.append(self._alternation(top=False))
        self.i += 1  # ')'
        return node, quantifiable

    def _modifier_group(self) -> _Node:
        """A group that turns modifiers on or off for its body, such as ``(?i:...)`` or ``(?s-i:...)``, from after
        its ``(?``. A modifier is named once at most, and at least one is named."""
        saved = (self.icase, self.multiline, self.dot_all)
        seen = ""
        on = True
        while True:
            c = self.peek()
            self.i += 1
            if c == ":":
                break
            if c == "-" and on:
                on = False
            elif c != "" and c in "ims" and c not in seen:
                seen += c
                if c == "i":
                    self.icase = on
                elif c == "m":
                    self.multiline = on
                else:
                    self.dot_all = on
            else:
                raise self.error()
        if not seen:
            raise self.error()
        node = _Node(_GROUP, items=[self._alternation(top=False)])
        self.i += 1  # ')'
        self.icase, self.multiline, self.dot_all = saved
        return node

    def _group_name(self) -> str:
        """A group name up to and including its ``>``. A character of the name may be written as a ``\\u`` escape."""
        name = ""
        while True:
            c = self.peek()
            if c == "":
                raise self.error()
            self.i += 1
            if c == ">":
                break
            if c == "\\":
                if self.peek() != "u":
                    raise self.error()
                self.i += 1
                code = self._unicode_escape(always=True)
                if code is None:
                    raise self.error()
                c = chr(code)
            name += c
        if _ASCII_NAME_RE.fullmatch(name) is None:
            if not self.u:
                # The pattern is read by code unit (see _units), and a name is of characters.
                name = name.encode("utf-16-le", "surrogatepass").decode("utf-16-le", "surrogatepass")
            # An identifier of ECMA-262, by the tables of Unicode 17 that the package has.
            starts, continues = _table("b:ID_Start"), _table("b:ID_Continue")
            if name == "" or not (name[0] in "$_" or _holds(starts, ord(name[0]))):
                raise self.error()
            if not all(c in "$\u200c\u200d" or _holds(continues, ord(c)) for c in name[1:]):
                raise self.error()
        return name

    def _escape(self) -> tuple[_Node, bool]:
        self.i += 1
        if self.i >= self.n:
            raise self.error()
        e = self.p[self.i]
        if e in "bB":
            self.i += 1
            if self.u and self.icase:
                w = _FOLD_WORD_CLASS
                if e == "b":
                    return _Node(_TEXT, f"(?:(?<={w})(?!{w})|(?<!{w})(?={w}))"), False
                return _Node(_TEXT, f"(?:(?<={w})(?={w})|(?<!{w})(?!{w}))"), False
            boundary = "\\b" if self.ascii_flag else "(?a:\\b)"
            # \B is "not at a word boundary", written as that: the standard library's \B never matched in an empty
            # text before CPython 3.14, where ECMA's does.
            node = _Node(_TEXT, boundary if e == "b" else f"(?!{boundary})")
            node.others = e == "B"  # see _sees_code_units
            return node, False
        if e in "123456789":
            j = self.i
            while j < self.n and self.p[j] in _DIGITS:
                j += 1
            number = int(self.p[self.i : j])
            if number <= self.group_count:
                self.i = j
                ref = _Node(_REFERENCE, group=number)
                ref.icase = self.icase
                return ref, True
            if self.u:
                raise self.error()
            return self._char(self._legacy_octal_or_identity()), True
        if e == "k" and (self.u or self.has_names):
            # Annex B reads \k as the letter k in a pattern with no named group.
            if self.peek(1) != "<":
                raise self.error()
            self.i += 2
            ref = _Node(_REFERENCE)
            ref.icase = self.icase
            self.named_refs.append((ref, self._group_name()))
            return ref, True
        if e in "pP" and self.u:
            written = self._property_escape()
            if self.icase:
                return self._folded_class([], [written], False), True
            members, beside, ranges, cost = written
            return self._one(
                _class_of([members] if members else [], [beside] if beside else []), ranges, cost=cost
            ), True
        text, char = self._char_escape(in_class=False)
        if char is not None:
            return self._char(char), True
        if self.icase and self.u and e in "wW":
            return self._folded_class(_FOLD_WORD if e == "w" else _complement(_FOLD_WORD), [], False), True
        return self._one(text, _DIGIT if e in "dD" else _WORD if e in "wW" else _WHITE, e in "DWS"), True

    def _legacy_octal_or_identity(self) -> str:
        # Annex B: \8 and \9 are identity escapes; other digits start a legacy octal escape.
        e = self.p[self.i]
        if e in "89":
            self.i += 1
            return e
        j = self.i
        while j < self.n and j < self.i + 3 and self.p[j] in "01234567":
            j += 1
        value = int(self.p[self.i : j], 8)
        if value > 0o377:
            j -= 1
            value = int(self.p[self.i : j], 8)
        self.i = j
        return chr(value)

    def _char_escape(self, in_class: bool) -> tuple[str, str | None]:
        """An escape after the backslash at ``self.i`` that denotes a character or a class: the translation of a
        class (``\\d``, ``\\p{...}``), or the character."""
        e = self.p[self.i]
        self.i += 1
        if e == "d":
            return ("\\d" if self.ascii_flag else "0-9" if in_class else "[0-9]"), None
        if e == "D":
            return ("\\D" if self.ascii_flag else "[^0-9]"), None
        if e == "w":
            return ("\\w" if self.ascii_flag else "0-9A-Za-z_" if in_class else "[0-9A-Za-z_]"), None
        if e == "W":
            return ("\\W" if self.ascii_flag else "[^0-9A-Za-z_]"), None
        if e == "s":
            return (_SPACE if in_class else f"[{_SPACE}]"), None
        if e == "S":
            return f"[^{_SPACE}]", None
        if e == "n":
            return "", "\n"
        if e == "r":
            return "", "\r"
        if e == "t":
            return "", "\t"
        if e == "f":
            return "", "\x0c"
        if e == "v":
            return "", "\x0b"
        if e == "0" and self.peek() not in _DIGITS:
            return "", "\x00"
        if e == "b" and in_class:
            return "", "\x08"
        if e == "-" and in_class:
            return "", "-"
        if e == "c":
            c = self.peek()
            if ("a" <= c <= "z") or ("A" <= c <= "Z"):
                self.i += 1
                return "", chr(ord(c) % 32)
            if in_class and not self.u and (c in _DIGITS or c == "_"):
                self.i += 1
                return "", chr(ord(c) % 32)
            if self.u:
                raise self.error()
            self.i -= 1  # '\c' is a literal backslash then 'c'
            return "", "\\"
        if e == "k" and self.has_names:
            # Annex B: in a pattern with a named group, \k is a reference to one and nothing else.
            raise self.error()
        if e == "x":
            hexa = self.p[self.i : self.i + 2]
            if len(hexa) == 2 and all(h in "0123456789abcdefABCDEF" for h in hexa):
                self.i += 2
                return "", chr(int(hexa, 16))
            if self.u:
                raise self.error()
            return "", "x"
        if e == "u":
            code = self._unicode_escape()
            if code is not None:
                return "", chr(code)
            if self.u:
                raise self.error()
            return "", "u"
        if e in _SYNTAX:
            return "", e
        if e == "0" and in_class and not self.u:
            return "", self._legacy_octal_after_zero()
        if self.u:
            raise self.error()
        if e == "0":
            return "", self._legacy_octal_after_zero()
        if in_class and e in _DIGITS:
            self.i -= 1
            return "", self._legacy_octal_or_identity()
        # Annex B identity escape.
        return "", e

    def _property_escape(self) -> _Property:
        """A ``\\p{...}`` or ``\\P{...}`` of a pattern with the ``u`` flag, from its ``p``: how its characters are
        written for the engine (members of a class, and a class that stands beside it, see ``_host_form``), the
        characters as ranges, and what a character costs. Without the ``u`` flag ``\\p`` is the letter ``p``
        (Annex B), which ``_char_escape`` reads."""
        negated = self.p[self.i] == "P"
        end = self.p.find("}", self.i)
        body = self.p[self.i + 2 : end] if end > 0 and self.peek(1) == "{" else ""
        key = (body, negated, self.ascii_flag)
        written = self.properties.get(key)
        if written is None:
            known = _property(body)
            if known is None:
                raise self.error()
            host, ranges = known
            if negated:
                ranges = _complement(ranges)
            # The standard library's engine has no properties, and finds a character in a class of any size in
            # one step. The regex package is given its own property with what is different.
            if host is None or self.ascii_flag:
                written = (_table_class(ranges), "", ranges, 0 if self.ascii_flag else len(ranges))
            else:
                members, beside, cost = _host_form(host[negated], host[not negated], ranges)
                written = (members, beside, ranges, cost)
            self.properties[key] = written
        self.i = end + 1
        return written

    def _legacy_octal_after_zero(self) -> str:
        self.i -= 1
        return self._legacy_octal_or_identity()

    def _unicode_escape(self, always: bool = False) -> int | None:
        """What follows ``\\u``. ``always``: as with the ``u`` flag whatever the grammar (in a group name)."""
        p = self.p
        u = self.u or always
        if u and self.peek() == "{":
            end = p.find("}", self.i)
            hexa = p[self.i + 1 : end] if end > 0 else ""
            if not hexa or not all(h in "0123456789abcdefABCDEF" for h in hexa) or int(hexa, 16) > 0x10FFFF:
                raise self.error()
            self.i = end + 1
            return int(hexa, 16)

        def four(at: int) -> int | None:
            h = p[at : at + 4]
            if len(h) == 4 and all(x in "0123456789abcdefABCDEF" for x in h):
                return int(h, 16)
            return None

        hi = four(self.i)
        if hi is None:
            return None
        self.i += 4
        if u and 0xD800 <= hi < 0xDC00 and p.startswith("\\u", self.i):
            lo = four(self.i + 2)
            if lo is not None and 0xDC00 <= lo < 0xE000:
                self.i += 6
                return 0x10000 + ((hi - 0xD800) << 10) + (lo - 0xDC00)
        return hi

    def _class(self) -> _Node:
        self.i += 1
        negated = self.peek() == "^"
        if negated:
            self.i += 1
        members: list[str] = []
        # Class escapes that Python cannot nest in a class (ECMA's \S), kept aside as alternatives.
        outside: list[str] = []
        # The same class as sets of characters: the ranges of what is written in it, and each property as it is
        # written for the engine with its characters.
        ranges: _Ranges = []
        properties: list[_Property] = []
        while True:
            if self.i >= self.n:
                raise self.error()
            c = self.peek()
            if c == "]":
                self.i += 1
                break
            lo, lo_char = self._class_atom(ranges, properties)
            if self.peek() == "-" and self.peek(1) not in ("]", ""):
                self.i += 1
                hi, hi_char = self._class_atom(ranges, properties)
                if lo_char is None or hi_char is None:
                    if self.u:
                        raise self.error()
                    # Annex B: a class escape at either end makes the '-' a literal.
                    for t in (lo, "\\-", hi):
                        (outside if t.startswith("[^") else members).append(t)
                    ranges.extend((ord(c), ord(c)) for c in (lo_char, "-", hi_char) if c is not None)
                    continue
                if ord(hi_char) < ord(lo_char):
                    raise self.error()
                members.append(f"{lo}-{hi}")
                ranges.append((ord(lo_char), ord(hi_char)))
                continue
            if lo:
                (outside if lo.startswith("[^") else members).append(lo)
            if lo_char is not None:
                ranges.append((ord(lo_char), ord(lo_char)))
        if self.icase:
            return self._folded_class(ranges, properties, negated)
        # What the regex package goes through for a character that is in none of them: the members that are
        # written in the class, and the ranges of the properties.
        properties = list({p[:2]: p for p in properties}.values())
        cost = 0 if self.ascii_flag else len(members) + len(outside) + sum(p[3] for p in properties)
        # (A property first: the engine goes through the members in order, and looks a property up in one step.)
        members = [p[0] for p in properties if p[0]] + members
        outside += [p[1] for p in properties if p[1]]
        text = _class_of(members, outside, negated)
        return self._one(text, ranges + [r for p in properties for r in p[2]], negated, cost)

    def _class_atom(self, ranges: _Ranges, properties: list[_Property]) -> tuple[str, str | None]:
        """One class atom: its translation and, when it is a single character, that character. An escape that is a
        set joins ``ranges`` or ``properties``, which a case-insensitive group reads."""
        c = self.peek()
        if c != "\\":
            self.i += 1
            return _literal(c), c
        self.i += 1
        if self.i >= self.n:
            raise self.error()
        e = self.p[self.i]
        if e in "pP" and self.u:
            properties.append(self._property_escape())
            return "", None
        text, char = self._char_escape(in_class=True)
        if char is not None:
            return _literal(char), char
        word = _FOLD_WORD if self.u and self.icase else _WORD
        positive = _DIGIT if e in "dD" else word if e in "wW" else _WHITE
        ranges.extend(positive if e in "dws" else _complement(positive))
        return text, None

    def _folded_class(self, ranges: _Ranges, properties: list[_Property], negated: bool) -> _Node:
        """A class inside a case-insensitive group: it has every character that is equal, without case, to one of
        its members, and it is negated after that. ``properties``: the properties in it, each as it is written for
        the engine and with its characters."""
        ranges = _merged(ranges)
        properties = list({p[:2]: p for p in properties}.values())
        # A class that is written again in the pattern is worked out once.
        key = (tuple(ranges), tuple(p[:2] for p in properties), negated)
        known = self.folded.get(key)
        if known is None:
            ranges = _merged(ranges + self._equal(ranges))
            for p in properties:
                # What a property adds is worked out once for the pattern: it takes milliseconds for the 684 ranges
                # of the letters.
                more = self.equal.get(p[:2])
                if more is None:
                    more = self.equal[p[:2]] = _minus(_merged(self._equal(p[2])), p[2])
                if more:
                    ranges = _merged(ranges + more)
            members = [p[0] for p in properties if p[0]] + ([_table_class(ranges)] if ranges else [])
            text = _class_of(members, [p[1] for p in properties if p[1]], negated)
            cost = 0 if self.ascii_flag else len(ranges) + sum(p[3] for p in properties)
            known = self.folded[key] = (text, ranges + [r for p in properties for r in p[2]], cost)
        return self._one(known[0], known[1], negated, known[2])

    def _equal(self, ranges: _Ranges) -> _Ranges:
        """The characters that are equal, without case, to a character of ``ranges``."""
        points, sets = _fold_table(self.u)
        more: set[str] = set()
        for lo, hi in ranges:
            k = bisect_left(points, lo)
            while k < len(points) and points[k] <= hi:
                more.update(sets[points[k]])
                k += 1
        return [(ord(c), ord(c)) for c in more]


class _Writer:
    """Places every reference of a pattern's tree and writes the pattern for the engine.

    ``for_regex``: the pattern is written for the regex package. That engine remembers, for a repetition, the
    places where its body or what follows it has already failed, and does not try them again. It drops that when it
    sees a reference in the body or after it, as the outcome then depends on what a group holds. It does not see a
    reference that is inside a repetition with a maximum count, so ``^(?:(?:(a|)|){2})?(?:x\\1)?$`` does not match
    ``axa``. So in a pattern with a reference, every repetition is followed by a reference to an empty group, which
    the engine sees, and a repetition with a reference or a reset group inside it is written without a count:
    ``X{2,4}`` as ``XX(?:X(?:X|)|)`` and ``X{2,}`` as ``XX(?:X)*``."""

    def __init__(
        self,
        root: _Node,
        for_regex: bool,
        unicode: bool = True,
        share: bool = False,
        counts: bool = False,
        steps: bool = False,
    ) -> None:
        self.root = root
        self.unicode = unicode
        self.for_regex = for_regex
        self.counts = counts and for_regex
        # Whether every repetition of one class is written with a step for each character, and whether one was
        # left as it is (see _repeat).
        self.steps = steps and for_regex
        self.scans = False
        # The long classes, each with how many times it is written. With ``share`` (for the regex package) each
        # is written once, as a group that is called, and this has the number of each one's group.
        self.share = share
        self.classes: dict[str, int] = {}
        # The most characters that are written. The standard library's engine reads a class of ranges at a
        # microsecond for a character and keeps a table for it, so it is given more, with each long class counted
        # once (see write).
        self.most = _MAX_TRANSLATED if for_regex else _MAX_WRITTEN_FOR_RE
        # How many characters of literals have been written for the regex package, and how many times one was
        # followed by what parts a long literal (see _MAX_LITERAL). The parts do not count towards the length.
        self.literals = 0
        self.parted = 0
        # What a match keeps for the groups of the pattern (see _keeps): for each power of the length of the text,
        # how many groups are closed. And where the writer is: how many repetitions with a lookaround between them
        # it is inside, whether it is inside one with no lookaround since, and whether it is inside a lookaround.
        self.kept: dict[int, int] = {}
        self.power = 0
        self.in_loop = False
        self.in_look = False
        if for_regex:
            self.most += (_MAX_TRANSLATED // _MAX_LITERAL + 1) * len(_AFTER_A_CHARACTER)
        self.groups: dict[int, _Node] = {}
        self.refs: list[_Node] = []
        self.count = 0
        # How many nodes take a character or are a reference.
        self.leaves = 0
        self._number(root, None, False)
        # The groups a reference reads, which get a name, and the ones a _DYNAMIC reference reads, which are reset.
        self.read: set[int] = set()
        self.reset: dict[int, list[_Node]] = {}
        self.needs_regex = False
        # The tests that two characters are equal without case (see _folded), by the sets of characters they are
        # for, each with its name.
        self.tests: dict[tuple[str, ...], str] = {}
        # The comparisons with a group that are written once and called, by the group and the test: the name and
        # the text of each.
        self.comparisons: dict[tuple[str, str], tuple[str, str]] = {}
        # The lookarounds with a group that is read outside them, and the repetitions inside one of those where an
        # iteration can match nothing (both by the node's place in the tree). See _repeat.
        self.leaky: set[int] = set()
        self.forced: set[int] = set()
        for ref in self.refs:
            ref.state = self._place(ref, self.groups[ref.group])
        for ref in self.refs:
            if ref.state != _EMPTY and self._never_runs(ref, self.groups[ref.group]):
                ref.state = _EMPTY
        for ref in self.refs:
            if ref.state != _EMPTY:
                self.read.add(ref.group)
                if ref.icase:
                    ref.same = self._same(self.groups[ref.group])
                    self.needs_regex = self.needs_regex or ref.same != ""
                self._leaks(ref, self.groups[ref.group])
                if _GROUPS_IN_REPETITIONS_GO_STALE:
                    n = self.groups[ref.group].parent
                    while n is not None and n.kind != _REPEAT:
                        n = n.parent
                    self.needs_regex = self.needs_regex or n is not None
            if ref.state == _DYNAMIC:
                self.reset.setdefault(ref.group, []).append(ref)
                self.needs_regex = True
        if self.leaky:
            self._force(root, False)
        self.live = [ref.first for ref in self.refs if ref.state != _EMPTY]
        # The references whose comparison is written out (see _folded). It sets groups of its own, and the regex
        # package runs another iteration of a repetition for as long as each one changes what a group holds, so a
        # repetition that holds one is written as one that holds a group some reference reads.
        self.compared = [ref.first for ref in self.refs if ref.same]
        # The reference to an empty group that the regex package sees after a repetition.
        self.seen = "(?P=z)" if for_regex and self.live else ""
        self.loops = 0

    def _number(self, n: _Node, parent: _Node | None, back: bool) -> None:
        n.parent = parent
        n.first = self.count
        self.count += 1
        n.back = back
        if n.kind == _GROUP and n.group:
            self.groups[n.group] = n
        elif n.kind == _REFERENCE:
            self.refs.append(n)
            self.leaves += 1
        elif n.kind == _TEXT:
            self.leaves += 1
        elif n.kind == _LOOK:
            back = n.behind
        for item in n.items:
            self._number(item, n, back)
        n.last = self.count

    @staticmethod
    def _place(ref: _Node, group: _Node) -> int:
        # The innermost node that holds both the reference and its group.
        both = ref.parent
        while both is not None and not both.first <= group.first < both.last:
            both = both.parent
        if both is None or both is group or both.kind != _SEQUENCE:
            # The reference is inside its group, which has no value until it closes, or the two are in separate
            # alternatives. Any repetition around both has reset the group since it last matched.
            return _EMPTY
        ahead = group.first < ref.first
        if ahead == both.back:
            # The group is matched after the reference (to its left, in a lookbehind).
            return _EMPTY
        plain = True
        n: _Node | None = group
        while n is not None and n is not both:
            k = n.kind
            if k == _LOOK and n.negative:
                # Nothing outside a negative lookaround sees its groups.
                return _EMPTY
            if k == _ALTERNATION:
                plain = False
            elif k == _REPEAT and (n.min == 0 or (n.max != n.min and _matches_nothing(n.items[0]))):
                # No iteration may run, or the last one may match nothing (which the engines and ECMA-262 treat
                # differently).
                plain = False
            n = n.parent
        return _PLAIN if plain else _DYNAMIC

    @staticmethod
    def _never_runs(ref: _Node, group: _Node) -> bool:
        """Whether the reference or its group is in a repetition that never runs an iteration (see ``_repeat``)."""
        for start in (ref, group):
            n: _Node | None = start
            while n is not None:
                if n.kind == _REPEAT and (n.max == 0 or (n.min == 0 and _only_nothing(n.items[0]))):
                    return True
                n = n.parent
        return False

    def _same(self, group: _Node) -> str:
        """The name of the test for a case-insensitive reference to the group, or nothing when no character the
        group can take is equal to another without case (the reference is then an exact one)."""
        points, sets = _fold_table(self.unicode)
        taken: _Ranges = []
        found: set[str] = set()
        if _taken(group.items[0], taken):
            for lo, hi in _merged(taken):
                k = bisect_left(points, lo)
                while k < len(points) and points[k] <= hi:
                    found.add(sets[points[k]])
                    k += 1
        else:
            found.update(sets.values())
        if not found:
            return ""
        key = tuple(sorted(found))
        name = self.tests.get(key)
        if name is None:
            name = self.tests[key] = f"s{len(self.tests) + 1}"
        return name

    def _leaks(self, ref: _Node, group: _Node) -> None:
        n = group.parent
        while n is not None:
            if n.kind == _LOOK and not n.negative and not n.first < ref.first < n.last:
                self.leaky.add(n.first)
            n = n.parent

    def _force(self, n: _Node, inside: bool) -> None:
        if n.kind == _LOOK:
            inside = inside or n.first in self.leaky
        elif inside and n.kind == _REPEAT and n.max != 0:
            body = n.items[0]
            if _matches_nothing(body) and not _only_nothing(body):
                self.forced.add(n.first)
                self.needs_regex = True
        for item in n.items:
            self._force(item, inside)

    def write(self) -> str:
        """The pattern as it is written for the engine.

        A translation has a limit of length because both engines read a pattern with a parser written in Python.
        A class of a property can be thousands of characters (the 684 ranges of ``\\p{L}``), and a pattern may
        have the same class many times. A class of more than ``_LONG_CLASS`` characters counts once towards the
        limit, and each time after the first as a call of it:

        - The standard library's engine has no way to write a class once. It is given every copy, up to
          ``_MAX_WRITTEN_FOR_RE`` characters in all, which it reads in a fifth of a second.
        - The regex package is given the classes as they are while the whole is within the limit. A pattern over
          it is written again with each long class once, as a group in the ``(?(DEFINE)...)`` at the start, and
          a call of the group by its number where the class stands (``share``). A call is slower than the class
          (0.15 microseconds), which is why it is not the first way."""
        out = self._write(self.root)
        if self.reset or self.seen:
            before = "(?P<z>)" if self.seen else ""
            out = before + "".join(f"(?P<g{g}>)" for g in sorted(self.reset)) + f"(?:{out})"
        length = len(out) - self.parted * len(_AFTER_A_CHARACTER)
        if self.share:
            # The groups of the classes come first, so that they have the numbers they are called by.
            classes = "".join(f"({text})" for text in self.classes)
            if length + len(classes) > _MAX_TRANSLATED:
                raise _TooLarge()
        else:
            classes = ""
            again = sum((times - 1) * (len(text) - _CALL) for text, times in self.classes.items())
            if length - (0 if self.for_regex else again) > _MAX_TRANSLATED:
                raise _TooLarge()
        if not self.tests:
            return f"(?(DEFINE){classes}){out}" if classes else out
        # The tests and the comparisons are written once each, and are not counted in the size: a test is as long as
        # the sets it is for, which no repetition multiplies.
        tests = "".join(self._test(name, key) for key, name in self.tests.items())
        if len(tests) > _MAX_TESTS:
            # One test for the sets of all of them, which is right for each, and the others call it.
            sets = tuple(sorted({members for key in self.tests for members in key}))
            names = list(self.tests.values())
            tests = self._test(names[0], sets) + "".join(f"(?P<{name}>(?&{names[0]}))" for name in names[1:])
        comparisons = "".join(f"(?P<{name}>{text})" for name, text in self.comparisons.values())
        if length + len(classes) + len(comparisons) > _MAX_TRANSLATED:
            raise _TooLarge()
        return f"(?(DEFINE){classes}{tests}{comparisons}){out}"

    @staticmethod
    def _test(name: str, sets: tuple[str, ...]) -> str:
        """The test that the character at the pointer of ``_folded`` and the character it has kept are equal
        without case: they are the same character, or both are in one of the sets. It is a group that is called,
        and it has no repetition of its own (the engine keeps the state of every repetition of a pattern at each
        lookaround it enters). Many sets are tried forty at a time, after a test that the character is in one of
        the forty."""

        raw = _literal

        at_pointer = "(?P=fr)(?<=(?=%s)(?P=fp))"
        tests = []
        for members in sets:
            one_of = "[" + "".join(raw(c) for c in members) + "]"
            tests.append(f"(?={one_of})" + at_pointer % one_of)
        if len(tests) > 80:
            some = []
            for k in range(0, len(tests), 40):
                points = _merged([(ord(c), ord(c)) for members in sets[k : k + 40] for c in members])
                any_of = "".join(raw(chr(lo)) if lo == hi else f"{raw(chr(lo))}-{raw(chr(hi))}" for lo, hi in points)
                some.append(f"(?=[{any_of}])(?:{'|'.join(tests[k : k + 40])})")
            tests = some
        return f"(?P<{name}>(?s:{'|'.join([at_pointer % '(?P=fx)', *tests])}))"

    def _folded(self, exact: str, same: str, back: bool) -> str:
        """A reference inside a case-insensitive group. ECMA-262 compares the text of the group G and the text at
        the reference character by character, each by its Canonicalize. Neither engine compares that way (both
        take ``i`` for U+0130, and each has the case pairs of its own version of Unicode), so the comparison is
        written out for the regex package with the sets of ``_casefold``.

        The text is G as it is (``exact``), or a loop takes one character of the text for each character of G. A
        group ``fp`` is the pointer into G: it holds the rest of the text from a place inside somewhere that G
        stands, and ``fe`` holds the rest of the text from the end of G there. Each iteration keeps the next
        character of the text as ``fx`` and the rest of the text from it as ``fr``. A place is found from what it
        holds: ``(?=(?P=fr)(?<=X(?P=fp)))`` goes to the end of the text and back over ``fp``, and X is matched where
        the pointer is. So the iteration asks that the pointer is not at the end of G, then that the character
        there is equal to ``fx`` without case (the test ``same``, written once for the pattern by ``_test`` and
        called), and then moves the pointer on by one. After the loop the pointer is at the end of G. The loop gives
        nothing back, and the whole is atomic, as two texts are compared in one way only.

        A comparison is a group that is written once and called from each reference. The engine puts back what
        the groups held when a call returns, and nothing outside the comparison reads the groups it sets.

        In a lookbehind (``back``) the text to compare ends where the reference stands: the place where it starts
        is looked for, where the comparison ends at the place that was left. That is written where the reference
        is, as the engine matches a lookbehind backwards and a group it calls forwards."""
        z = self.seen
        at_pointer = "(?=(?P=fr)(?<=%s(?P=fp)))"
        loop = (
            f"(?s:(?<=\\A(?=.*?(?P<fp>{exact}(?P<fe>.*))).*)"
            f"(?:{z}(?=(?P<fr>(?P<fx>.).*))"
            + at_pointer % "(?!(?P=fe)\\Z)"
            + f"(?=(?&{same}))"
            + at_pointer % "(?=.(?P<fp>.*))"
            + f".)*+{z}(?=(?P<fr>.*))"
            + at_pointer % "(?=(?P=fe)\\Z)"
            + ")"
        )
        if back:
            return f"(?>{exact}|(?={loop}(?=(?P=fh)\\Z)){z}{_ANY}*?{z}(?=(?P<fh>{_ANY}*)))"
        comparison = self.comparisons.get((exact, same))
        if comparison is None:
            comparison = self.comparisons[exact, same] = (f"f{len(self.comparisons) + 1}", f"(?>{exact}|{loop})")
        # The engine has to see a reference here (see the class's description), and does not look into a call.
        return f"(?&{comparison[0]}){z}"

    def _write(self, n: _Node) -> str:
        k = n.kind
        if k == _TEXT:
            text = n.text
            if self.for_regex and n.nothing is False and n.chars is not None and not n.others:
                chars = n.chars if len(n.chars) == 1 else _merged(n.chars) if len(n.chars) < 8 else n.chars
                if len(chars) == 1 and chars[0][0] == chars[0][1]:
                    # One character, which the engine makes a literal of with those around it, whatever groups
                    # that do not capture are between them.
                    self.literals += 1
                    if self.literals % _MAX_LITERAL == 0:
                        self.parted += 1
                        return text + _AFTER_A_CHARACTER
                    return text
            if n.nothing is not False or len(text) <= (_SHARED if self.share else _LONG_CLASS):
                return text
            if not self.share:
                self.classes[text] = self.classes.get(text, 0) + 1
                return text
            number = self.classes.setdefault(text, len(self.classes) + 1)
            # A call closes the group it calls, and keeps more than twice what a group does.
            if self.in_look:
                n.shut = _IN_A_LOOKAROUND
            else:
                n.deep = 2
            # The engine matches a group that it calls forwards, also where it is matching backwards (in a
            # lookbehind). There the character is taken backwards, and the call is in a lookahead.
            return f"(?:(?=(?{number})){_ANY})" if n.back else f"(?{number})"
        if k in (_SEQUENCE, _ALTERNATION):
            parts = []
            size = 0
            for x in n.items:
                part = self._write(x)
                size += len(part) + (k == _ALTERNATION)
                if size > self.most + 1:
                    # Nothing longer than the limit is built, whatever the pattern.
                    raise _TooLarge()
                parts.append(part)
            n.weight = sum(x.weight for x in n.items)
            n.deep = max((x.deep for x in n.items), default=0)
            shut = [x.shut for x in n.items]
            n.shut = sum(shut) if k == _SEQUENCE else max(shut, default=0)
            return ("" if k == _SEQUENCE else "|").join(parts)
        if k == _GROUP:
            child = n.items[0]
            body = self._write(child)
            n.weight, n.shut, n.deep = child.weight, child.shut, child.deep
            if n.group in self.read:
                if self.in_look or _matches_nothing(child):
                    n.shut += _IN_A_LOOKAROUND if self.in_look else 1
                else:
                    n.deep += 1
                return f"(?P<g{n.group}>{body})"
            # A group that no reference reads does not capture. Both engines keep what a group held each time it
            # closes, for as long as the match goes on, so 480 empty groups in a repetition took 740 MB for a text
            # of 100,000 characters, in either engine.
            return f"(?:{body})"
        if k == _LOOK:
            was = (self.in_look, self.in_loop)
            self.in_look, self.in_loop = True, False
            inside = self._write(n.items[0])
            self.in_look, self.in_loop = was
            n.weight, n.shut = n.items[0].weight, n.items[0].shut
            return f"(?{'<' if n.behind else ''}{'!' if n.negative else '='}{inside})"
        if k == _REFERENCE:
            if n.state == _EMPTY:
                return "(?:)"
            exact = f"(?P=g{n.group})"
            if not n.same:
                return exact
            # The comparison is a repetition of its own, over the text of the group, and closes groups for each
            # character it compares.
            self._keeps(8)
            return self._folded(exact, n.same, n.back)
        return self._repeat(n)

    def _keeps(self, groups: int) -> None:
        """Counts what a repetition here makes a match of the regex package keep: ``groups`` groups closed for
        each iteration, or for each character taken.

        The engine keeps what a group held each time it closes, until the match ends, so a repetition over the text
        keeps that many times the length of the text. A repetition inside a lookaround inside a repetition runs
        over the rest of the text at every place of it, and keeps that many times the square of the length:
        ``^(?:(?=(?:(a))*)\\1)*[bc]$`` took 400 MB in a second for a text of 100,000 characters. ``kept`` has
        the groups for each such power, and a text has a limit of length from it (see ``_most_characters``)."""
        if self.for_regex and groups:
            power = self.power if self.in_loop else self.power + 1
            self.kept[power] = self.kept.get(power, 0) + groups

    def _repeat(self, n: _Node) -> str:
        body = n.items[0]
        lo, hi = n.min, n.max
        n.weight = 1
        if hi == 0:
            return "(?:)"
        if _only_nothing(body):
            # Every iteration matches nothing, and ECMA-262 refuses one that does once the minimum count is met. So
            # none runs for a minimum of 0. For any other, the groups are left as one iteration leaves them.
            if not lo:
                return "(?:)"
            once = self._write(body)
            n.weight, n.shut, n.deep = body.weight, body.shut, body.deep
            return once
        if lo >= _MAX_COUNT:
            if not _matches_nothing(body):
                # More characters than a text has.
                return "(?!)"
            # That many iterations, of which all but a few match nothing, are beyond both engines.
            raise _TooLarge(f"it repeats what can match nothing at least {_MAX_COUNT:,} times")
        # A repetition that may run more than a few times is one that a match keeps groups for (see _keeps).
        loops = hi == -1 or hi > _FEW_TIMES
        was = (self.in_loop, self.power)
        if loops and not self.in_loop:
            self.in_loop, self.power = True, self.power + 1
        text = self._write(body)
        power = self.power
        self.in_loop, self.power = was
        if self.for_regex and not _CALL_RE.fullmatch(text):
            scanned = self._scanned(body)
            if scanned > _MAX_SCANNED or (self.steps and scanned >= 0 and self._one_of_many(body)):
                # A lookahead and then any character: the same characters, and a step of the match for each.
                text = f"(?:(?={text}){_ANY})"
            elif scanned >= 0 and self._one_of_many(body):
                self.scans = True
        k = bisect_left(self.live, n.first)
        read = self.seen != "" and (
            n.first in self.forced
            or self._holds_one(self.compared, n)
            or any(n.first < self.groups[g].first < n.last for g in self.read)
        )
        if not read and not (self.seen and k < len(self.live) and self.live[k] < n.last):
            # A reference to a name that two groups have is two references, which the count is for together. One
            # character, escape or class needs no group.
            if body.kind == _SEQUENCE or not (text.startswith("(") or _ATOM_RE.fullmatch(text)):
                text = f"(?:{text})"
            self._kept_by(n, body, loops, power, 0)
            if self.counts and lo > 1 and self._scanned(body) >= 0:
                n.weight = body.weight * 3
                return self._counted(text, lo, hi, n.lazy, n.back)
            counted = text + self._count(lo, hi, n.lazy)
            # The regex package writes a minimum count out, and then the body once more for the rest.
            n.weight = body.weight * (1 if hi == 1 or lo == 0 else lo + 1)
            # What the engine matches after the repetition is at its left in a lookbehind, which it matches
            # backwards, and the reference it has to see is there.
            return self.seen + counted if n.back else counted + self.seen
        # The groups inside that a reference may read without their having matched lose their values at the start
        # of each iteration (at its right, in a lookbehind).
        inside = [g for g in sorted(self.reset) if n.first < self.groups[g].first < n.last]
        resets = "".join(f"(?P<g{g}>)" for g in inside) if hi == -1 or hi > 1 else ""
        one = f"(?:{text}{resets})" if n.back else f"(?:{resets}{text})"
        more = one
        if read and _matches_nothing(body):
            # ECMA-262 refuses an iteration past the minimum that matches nothing. The engine does not, and it runs
            # another for as long as each one changes what a group holds. Where such an iteration ends, the rest of
            # the text (the text up to there, in a lookbehind) is not what it was where it started.
            self.loops += 1
            e = f"e{self.loops}"
            if n.back:
                more = f"(?:(?<!(?P={e})){text}(?<=(?P<{e}>{_ANY}*)){resets})"
            else:
                more = f"(?:{resets}(?=(?P<{e}>{_ANY}*)){text}(?!(?P={e})))"
        # The iterations up to the minimum, then the rest. In a lookbehind the first iteration is the rightmost.
        if lo * len(one) > _MAX_TRANSLATED or (hi - lo) * len(more) > _MAX_TRANSLATED:
            raise _TooLarge()
        n.weight = body.weight * (lo + (1 if hi == -1 else hi - lo))
        # Each iteration closes the groups that are reset, and the one that holds the rest of the text (which is in
        # a lookaround).
        self._kept_by(n, body, loops, power, (len(inside) if resets else 0) + (_IN_A_LOOKAROUND if more != one else 0))
        out = one * lo
        if hi == -1:
            rest = f"(?:{self.seen}{more}{self.seen})*{'?' if n.lazy else ''}"
            rest = self.seen + rest if n.back else rest + self.seen
        else:
            rest = ""
            for _ in range(hi - lo):
                inner = rest + more if n.back else more + rest
                rest = f"(?:|{inner})" if n.lazy else f"(?:{inner}|)"
                if len(rest) > _MAX_TRANSLATED:
                    raise _TooLarge()
        out = rest + out if n.back else out + rest
        if len(out) > _MAX_TRANSLATED:
            raise _TooLarge()
        return out

    def _one_of_many(self, n: _Node) -> bool:
        """Whether a node that is one character or class (see ``_scanned``) is a class that the engine has to look
        a character up in: anything but one character and the class of every character."""
        if n.kind == _TEXT:
            if n.text == _ANY:
                return False
            chars = n.chars
            return n.others or chars is None or len(chars) != 1 or chars[0][0] != chars[0][1]
        return len(n.items) != 1 or self._one_of_many(n.items[0])

    def _kept_by(self, n: _Node, body: _Node, loops: bool, power: int, more: int) -> None:
        """What a repetition makes a match keep (see ``_keeps``), where an iteration closes ``more`` groups beside
        those of its body. A repetition that runs a few times closes its groups that many times for one pass. One
        that may run many times is counted for the text, at ``power``."""
        each = body.shut + more
        n.deep = body.deep
        if not loops:
            n.shut = each * max(n.max, 1)
            return
        n.shut = each * n.min
        if self.for_regex and each + body.deep:
            self.kept[power] = self.kept.get(power, 0) + each + body.deep

    def _counted(self, one: str, lo: int, hi: int, lazy: bool, back: bool) -> str:
        """``X{lo,hi}`` for one character or class X, without a minimum count. The regex package writes a minimum
        count out as copies of what is counted, 270 bytes each: ``u{10400}`` is 3 MB, and a pattern may have no
        more than ``MAX_COPIES`` copies. A maximum count costs it nothing. So the count is written as three things
        that have none:

        - ``(?!X{0,lo-1}(?!X))``: it is not so that at most ``lo - 1`` characters of X are followed by one that is
          not X, which is to say that there are ``lo`` of them here;
        - ``(?>X{0,lo})``: ``lo`` of them are taken, and none is given back;
        - ``X{0,hi-lo}``: the rest, as the count has them (the most first, or the fewest for a lazy one).

        In a lookbehind the engine matches backwards, from the last of these to the first, and the first of the
        three looks behind: ``X{0,hi-lo}(?>X{0,lo})(?<!(?<!X)X{0,lo-1})``.

        This is slower than the count where the count is not refused (it goes over the first ``lo`` characters
        twice), so it is written only for a pattern whose copies are over the limit (see ``_translate``)."""
        z = self.seen
        if not (one.startswith("(") or _ATOM_RE.fullmatch(one)):
            one = f"(?:{one})"
        fewer = f"{one}{{0,{lo - 1}}}"
        here = f"(?<!(?<!{one}){fewer})" if back else f"(?!{fewer}(?!{one}))"
        taken = f"(?>{one}{{0,{lo}}})"
        rest = "" if hi == lo else one + self._count(0, -1 if hi == -1 else hi - lo, lazy)
        if back:
            return (z + rest if rest else "") + z + taken + here
        return here + taken + z + (rest + z if rest else "")

    def _scanned(self, n: _Node) -> int:
        """How many ranges the regex package goes through for a character when the node is the body of a
        repetition that it takes as one step (see ``_MAX_SCANNED``): the node is one character or class, or an
        alternation of those, which the engine makes one class of. -1: it is not such a body."""
        k = n.kind
        if k == _TEXT:
            return n.cost if n.nothing is False else -1
        if k == _SEQUENCE:
            return self._scanned(n.items[0]) if len(n.items) == 1 else -1
        if k == _GROUP:
            return -1 if n.group in self.read else self._scanned(n.items[0])
        if k == _ALTERNATION:
            costs = [self._scanned(x) for x in n.items]
            return -1 if min(costs) < 0 else sum(costs) + len(costs)
        return -1

    @staticmethod
    def _holds_one(places: list[int], n: _Node) -> bool:
        """Whether one of the places (in order) is inside the node."""
        k = bisect_left(places, n.first)
        return k < len(places) and places[k] < n.last

    @staticmethod
    def _count(lo: int, hi: int, lazy: bool) -> str:
        if hi == -1:
            q = "*" if lo == 0 else "+" if lo == 1 else f"{{{lo},}}"
        elif lo == hi:
            q = f"{{{lo}}}"
        else:
            q = "?" if (lo, hi) == (0, 1) else f"{{{lo},{hi}}}"
        return q + "?" if lazy else q


def _most_characters(kept: dict[int, int]) -> int:
    """The most characters a text may have for a match of the regex package to keep ``_MAX_KEPT`` groups at most,
    where ``kept`` has, for each power of the length of the text, how many groups a match closes (see
    ``_Writer._keeps``), and ``_MOST_TEXT`` characters at most."""
    if not kept:
        return _MOST_TEXT
    lo, hi = 0, _MOST_TEXT
    while lo < hi:
        mid = (lo + hi + 1) // 2
        if sum(groups * mid**power for power, groups in kept.items()) <= _MAX_KEPT:
            lo = mid
        else:
            hi = mid - 1
    return lo


class _Translation:
    """A pattern as it is written for one of the engines."""

    __slots__ = ("for_regex", "linear", "most", "scans", "text", "wide")

    def __init__(
        self, text: str, for_regex: bool, wide: bool, linear: bool, most: int = _MOST_TEXT, scans: bool = False
    ) -> None:
        self.text = text
        # For the regex package: whether the pattern has a repetition of one class that is one step of a match
        # however much of the text it runs over (see _search).
        self.scans = scans
        # For the regex package: the most characters a text may have (see _most_characters).
        self.most = most
        # Whether it is for the regex package (and so is compiled without the ASCII flag).
        self.for_regex = for_regex
        # Whether the pattern is one without the u flag for which a text is matched as its code units (see _units).
        self.wide = wide
        # Whether the standard library's engine is proved to match it in time proportional to the text.
        self.linear = linear


def _translate(pattern: str, unicode: bool, for_regex: bool, steps: bool = False) -> _Translation | None:
    """The pattern as it is written for the standard library's engine, or for the regex package when the pattern
    needs it or ``for_regex`` asks. None: the pattern is not valid in the grammar. A pattern over a limit of size
    raises _TooLarge."""
    if len(pattern) > MAX_PATTERN:
        raise _TooLarge(f"it is longer than {MAX_PATTERN:,} characters", unread=True)
    try:
        if not unicode:
            pattern = _units(pattern)
        if not for_regex:
            t = _Translator(pattern, unicode, True)
            try:
                text = t.translate()
            except _TooLarge:
                # The regex package may take less text for the same pattern.
                text = ""
                t.needs_regex = True
            if not t.needs_regex:
                return _Translation(text, False, t.wide, t.linear)
        t = _Translator(pattern, unicode, False, steps=steps)
        try:
            text = t.translate()
        except _TooLarge as e:
            if not e.length:
                raise
            # Again, with each long class written once and called.
            t = _Translator(pattern, unicode, False, share=True, steps=steps)
            text = t.translate()
        if t.copies > MAX_COPIES:
            # Again, with the minimum counts of one character written without copies.
            t = _Translator(pattern, unicode, False, share=t.share, counts=True, steps=steps)
            text = t.translate()
        if t.copies > MAX_COPIES:
            raise _TooLarge(
                f"its counts come to more than {MAX_COPIES:,} copies of what they repeat, which the regex package "
                "writes out"
            )
        return _Translation(text, True, t.wide, False, t.most, t.scans)
    except (_Invalid, KeyError, ValueError, IndexError):
        return None
    except RecursionError:
        # The limit of nesting keeps the translator within some hundreds of frames. This is for a caller that was
        # already near the end of Python's stack.
        raise _TooLarge("it nests deeper than the stack that was left for reading it", unread=True) from None


def translate(pattern: str, unicode: bool = True, for_regex: bool = False) -> tuple[str, bool] | None:
    """Translates an ECMA-262 pattern to Python syntax: the translation and whether it is for the ``regex`` package
    (and so is compiled without the ASCII flag), or None when the pattern is not valid in the grammar. It is for
    ``regex`` when the pattern needs it or ``for_regex`` asks."""
    t = _translate(pattern, unicode, for_regex)
    return None if t is None else (t.text, t.for_regex)


_ASTRAL_RE = re.compile("[\\U00010000-\\U0010ffff]")


def _pair(m: re.Match[str]) -> str:
    o = ord(m.group()) - 0x10000
    return chr(0xD800 + (o >> 10)) + chr(0xDC00 + (o & 0x3FF))


def _units(s: str) -> str:
    """The text as its UTF-16 code units, each a character: a character beyond the Basic Multilingual Plane becomes
    its two surrogates. Without the ``u`` flag ECMA-262 reads a pattern and matches a text by code unit, so ``.``
    takes half of such a character and a quantifier after one in the pattern repeats its second half. A pattern
    that is valid only without the flag is translated from its code units, and the texts it is given are matched
    as theirs when the pattern can tell the difference (see ``_sees_code_units``)."""
    return _ASTRAL_RE.sub(_pair, s)


Matcher = Callable[[str], Any]

DEFAULT_MATCH_LIMIT = 1.0
"""The limit of a match unless ``pattern_match_limit`` says otherwise, in seconds."""

# The patterns compiled last, each with the limit it was compiled for, so that the check of a schema and the
# validator made from it share one compilation. It holds _CACHE_PATTERNS matchers at most, of patterns of
# _CACHE_PATTERN characters at most (a longer pattern is compiled each time it is asked for).
_CACHE_PATTERNS = 256
_CACHE_PATTERN = 1024
_cache: OrderedDict[tuple[str, float | None], Matcher | None] = OrderedDict()
_cache_lock = threading.Lock()

# The standard library keeps the patterns it compiled last (512 of them), and its documented functions give no way
# to compile one that it does not keep. A translation of this many characters at most is compiled by re.compile, so
# that cache holds 512 of those at most for this package (16 bytes kept for a character, as measured: 8 MB). A
# longer one is compiled by the compiler behind re.compile, which keeps nothing, and goes to the regex package
# (which is asked not to keep it) on a Python that does not have that compiler where it is looked for.
_MAX_FOR_RE = 1024


def _compile_for_re(text: str) -> Any:
    """The translation compiled by the standard library's engine, or None when it is too long for the engine's
    cache and this Python has no way around the cache."""
    if len(text) <= _MAX_FOR_RE:
        return re.compile(text, re.ASCII)
    compiler = getattr(re, "_compiler", None)
    if compiler is None and sys.version_info < (3, 11):
        import sre_compile as compiler  # the name it had before CPython 3.11
    if compiler is None or not hasattr(compiler, "compile"):
        return None
    return compiler.compile(text, re.ASCII.value)


# The shares of the limit that the first two tries at a match are allowed, of the processor's time (see _limited).
_FIRST_TRY = 1 / 32
_SECOND_TRY = 1 / 16
# A match is abandoned once this share of the limit has passed on the clock since its first try ended.
_NEAR_THE_LIMIT = 0.9
# No try is allowed more of the processor's time than is left of this many times the limit, by the clock. A try
# takes its allowance on the clock when no other thread is at work, so this is the most a match can take.
_MOST_LIMITS = 2.0


def _limited(search: Matcher, limit: float, pattern: str, most: int = _MOST_TEXT) -> Matcher:
    """The search function of a pattern that the regex package runs, with the limit of a match, which is of time
    on the clock.

    The package's own timeout is not. It counts the time of the whole process on the processor (C's ``clock``), and
    it runs a match without the interpreter's lock, so with other threads at work that time passes several times
    as fast as the clock: with seven of them a timeout of one second came after an eighth of a second. So the
    package's timeout is used as an allowance of the processor's time, and the clock decides.

    The first try is allowed a thirty-second of the limit, and nothing is measured around it: a match that answers
    costs what the call of the engine costs. A match that does not answer in that is tried again from its start
    (see ``_again``)."""
    first = limit * _FIRST_TRY

    def limited(text: str) -> Any:
        try:
            return search(text, timeout=first)  # type: ignore[call-arg]
        except TimeoutError:
            pass
        return _again(search, text, limit, pattern, most)

    return limited


def _again(search: Matcher, text: str, limit: float, pattern: str, most: int = _MOST_TEXT) -> Any:
    """The answer of a match whose first try was abandoned, or PatternMatchLimitError once the limit has passed on
    the clock, or at once for a text of more than ``most`` characters.

    The limit of time does not bound what a match keeps: the engine fills hundreds of megabytes in a second, for
    the groups it closes (see ``_most_characters``) and for the iterations of a repetition (see ``_MOST_TEXT``).
    The length of the text does. The first try is not long enough to fill more than some tens of megabytes, so
    nothing was asked of a text before it, and a match that answers in it is answered whatever the length.

    Each try starts the match again, with an allowance of the processor's time. The second try is a short one (a
    sixteenth of the limit), and the two times it took, on the processor and on the clock, say how fast the
    processor's time is passing: once the clock's with no other thread at work, and eight times with seven. Each
    try after it is allowed what should come to the limit on the clock at the rate of the try before.

    A try cannot be stopped by the clock. One that was allowed the time of several threads goes on for all of it
    when the other threads stop, and a caller could be kept for as many limits as the machine has processors. So no
    try is allowed more than is left of twice the limit, which bounds a match at twice the limit and a little more
    whatever the other threads do. The price is that with more than one other thread at work throughout, a try
    gets less than the limit on the clock: half of it with three other threads, a quarter with seven. A match that
    needs more than that and less than the limit is abandoned, where it would have been answered alone. That is
    twice what the package's timeout by itself gave, and the error no longer comes early.

    The error is raised when the tries from the second on have taken nine tenths of the limit on the clock, and
    never before. On a machine where the process does not get a processor to itself, a try takes longer on the clock
    than it was allowed, and the bound of twice the limit does not hold."""
    if len(text) > most:
        raise PatternMatchLimitError(pattern, limit, len(text), most)
    start = monotonic()
    allowance = limit * _SECOND_TRY
    while True:
        began = monotonic()
        try:
            return search(text, timeout=allowance)  # type: ignore[call-arg]
        except TimeoutError:
            pass
        now = monotonic()
        passed = now - start
        if passed >= limit * _NEAR_THE_LIMIT:
            raise PatternMatchLimitError(pattern, limit) from None
        # How fast the processor's time passed during the try (at least as fast as the clock).
        rate = max(allowance / max(now - began, 1e-9), 1.0)
        allowance = min(rate * limit, _MOST_LIMITS * limit - passed)


def _compile(pattern: str, unicode: bool, limit: float | None) -> Matcher | None:
    search = _search(pattern, unicode, limit)
    if search is None or unicode or not search[1]:
        return None if search is None else search[0]
    find = search[0]

    def by_code_unit(text: str) -> Any:
        return find(text if text.isascii() else _units(text))

    return by_code_unit


def _search(pattern: str, unicode: bool, limit: float | None) -> tuple[Matcher, bool] | None:
    t = _translate(pattern, unicode, False)
    if t is None:
        return None
    if not t.for_regex and (limit is None or t.linear):
        try:
            compiled = _compile_for_re(t.text)
            if compiled is not None:
                return compiled.search, t.wide
        except re.error:
            # The regex package takes more (a lookbehind that the standard library does not, say).
            pass
        except RecursionError:
            raise _TooLarge("it nests deeper than the stack that was left for compiling it") from None
    if not t.for_regex:
        t = _translate(pattern, unicode, True)
        if t is None:
            return None
    search = _compile_for_regex(pattern, t)
    if limit is None:
        return search, t.wide
    if t.scans:
        search = _by_length(search, pattern, unicode)
    return _limited(search, limit, pattern, t.most), t.wide


def _compile_for_regex(pattern: str, t: _Translation) -> Matcher:
    import regex  # only patterns the standard library cannot run, or is not proved to run in linear time, need it

    try:
        search: Matcher = regex.compile(t.text, cache_pattern=False).search
    except regex.error as e:
        # The pattern is valid, and every property in it is one the engine has. A translation the engine does not
        # compile is a fault of the translator, and is not passed off as a pattern that is not valid.
        raise RuntimeError(
            f"corvus-json-schema translated the regular expression {pattern!r} to one that the regex package does "
            f"not compile ({e}). This is a fault of the package. The translation starts {t.text[:500]!r}."
        ) from None
    except RecursionError:
        raise _TooLarge("it nests deeper than the stack that was left for compiling it") from None
    return search


def _by_length(short: Matcher, pattern: str, unicode: bool) -> Matcher:
    """The search function of a pattern that the regex package runs and that has a repetition of one class, for a
    text of any length.

    The engine takes such a repetition as one step of a match, however much of the text it runs over, and looks at
    its clock every 256 steps. So its timeout comes late by a time that grows with the text: a fifth of a second
    for 100,000 letters and 15 seconds for ten million, for each try at the match. A text of more than
    ``_LONG_TEXT`` characters is given to the pattern as it is written with a step for each character of such a
    repetition, which is slower by a tenth of a microsecond for a character and has a timeout that comes when it
    should. That one is compiled when the first long text comes."""
    long: list[Matcher] = []

    def by_length(text: str, timeout: float | None = None) -> Any:
        if len(text) <= _LONG_TEXT:
            return short(text, timeout=timeout)  # type: ignore[call-arg]
        if not long:
            t = _translate(pattern, unicode, True, True)
            if t is None:  # (it was translated a moment ago)
                raise RuntimeError(f"corvus-json-schema could not write {pattern!r} for a long text.")
            long.append(_compile_for_regex(pattern, t))
        return long[0](text, timeout=timeout)  # type: ignore[call-arg]

    return by_length


def _shown(pattern: str) -> str:
    return pattern if len(pattern) <= 200 else f"{pattern[:200]}... ({len(pattern):,} characters)"


def compile_pattern(pattern: str, limit: float | None = DEFAULT_MATCH_LIMIT) -> Matcher | None:
    """A search function for an ECMA-262 pattern (truthy on a match), or None when the pattern is invalid.

    ``limit`` is the time one match may take on the clock, in seconds (None, zero or less: no limit). A pattern that the
    standard library's engine is proved to match in time proportional to the text is run by that engine, which has
    no limit and needs none. Any other pattern is run by the regex package, and a match that reaches the limit
    raises PatternMatchLimitError. With no limit the standard library runs every pattern it can.

    A pattern over a limit of size raises PatternTooLargeError (see MAX_PATTERN, MAX_NESTING and MAX_COPIES)."""
    limit = match_limit(limit)
    key = (pattern, limit)
    with _cache_lock:
        if key in _cache:
            _cache.move_to_end(key)
            return _cache[key]
    try:
        matcher = _compile(pattern, True, limit)
        if matcher is None:
            matcher = _compile(pattern, False, limit)
    except _TooLarge as e:
        raise PatternTooLargeError(
            f"The regular expression '{_shown(pattern)}' is too large to compile as an ECMA-262 regular expression: "
            f"{e.reason}."
        ) from None
    if len(pattern) <= _CACHE_PATTERN:
        with _cache_lock:
            _cache[key] = matcher
            if len(_cache) > _CACHE_PATTERNS:
                _cache.popitem(last=False)
    return matcher


def is_valid_ecma_regex(s: str) -> bool:
    """Whether the text is a valid ECMA-262 pattern with the ``u`` flag (``format: regex``). The text is read and
    not written for an engine: it is valid or not by the grammar, whatever its translation would come to. One that
    is longer than MAX_PATTERN or nests deeper than MAX_NESTING is not read to its end and is not valid."""
    if len(s) > MAX_PATTERN:
        return False
    try:
        return _Translator(s, True, True).reads()
    except (_TooLarge, RecursionError):
        return False
