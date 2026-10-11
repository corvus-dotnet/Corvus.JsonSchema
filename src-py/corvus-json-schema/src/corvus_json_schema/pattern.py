"""ECMA-262 regular expressions (as JSON Schema's ``pattern`` and ``format: regex`` specify), run by Python's engines.

A pattern is translated to Python's syntax with ECMA's meaning and compiled with the ASCII flag, so that ``\\d``,
``\\w`` and ``\\b`` keep their ECMA (ASCII) definitions. ECMA's ``\\s``, ``.`` and ``$`` are spelled out, and every
literal is escaped. The standard library's ``re`` runs what it can. The ``regex`` package runs the rest (Unicode
properties, variable-width lookbehind, and the backreferences described below).

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
uppercase form of a UTF-16 code unit without it, from the tables of ``_casefold`` (Unicode 16), which are the same
on every version of Python. A backreference inside ``i`` is compared character by character with the same tables,
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
from bisect import bisect_left
from collections.abc import Callable
from typing import Any

from .options import SchemaCompilationError

# ECMA-262 WhiteSpace and LineTerminator (what \s matches).
_SPACE = "\\t\\n\\x0b\\x0c\\r \\xa0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000\\ufeff"
_DOT = "[^\\n\\r\\u2028\\u2029]"
_ANY = "(?s:.)"
# What an empty class is: nothing matches it. (The regex package reads [^\s\S] as any character.)
_NEVER = "(?!)"
# ^ and $ inside a group with the m modifier: at the start or the end of the text or next to a line terminator.
_LINE_START = "(?<![^\\n\\r\\u2028\\u2029])"
_LINE_END = "(?![^\\n\\r\\u2028\\u2029])"
_SYNTAX = set("^$\\.*+?()[]{}|/")
_QUANTIFIER_RE = re.compile(r"\{(\d+)(,(\d*))?\}")
_ASCII_NAME_RE = re.compile(r"[A-Za-z_$][A-Za-z0-9_$]*")

# A count from here up is more characters than a text has. As a maximum it is no maximum.
_MAX_COUNT = 2**31 - 1
# The most text a translation may have. A repetition written without a count (see _Writer._repeat) multiplies out
# when it is inside another, and a pattern that passes this is refused.
_MAX_TRANSLATED = 65_536

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
_FOLD_WORD_CLASS = "[0-9A-Za-z_\\u017f\\u212a]"
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


_properties: dict[str, tuple[str, str, _Ranges | None]] = {}


def _property(body: str) -> tuple[str, str, _Ranges | None] | None:
    """What a property expression of ECMA-262 (the text between the braces of a ``\\p{...}``) is for the regex
    package: the class member that takes its characters and the one that takes the others, or the ranges of its
    characters for a property that the package does not have. None: ECMA-262 has no such expression.

    The names are ECMA-262's and no others, and their case matters: a value of General_Category by itself or after
    ``General_Category=`` or ``gc=``, a value of Script after ``Script=``, ``sc=``, ``Script_Extensions=`` or
    ``scx=``, and a binary property that ECMA-262 lists. The regex package has many more (``\\p{Greek}``,
    ``\\p{IsLatin}``, ``\\p{alpha}``, block names, any case), so it is never asked what a name means: each
    expression is written for it with the property named, and a value by its short name."""
    if not _properties:
        from . import _properties as tables

        for names in tables.GENERAL_CATEGORY.split(";"):
            short = names.split(" ")[0]
            entry: tuple[str, str, _Ranges | None] = (f"\\p{{gc={short}}}", f"\\P{{gc={short}}}", None)
            for name in names.split(" "):
                _properties[name] = _properties[f"gc={name}"] = _properties[f"General_Category={name}"] = entry
        for names in tables.SCRIPT.split(";"):
            short = names.split(" ")[0]
            for kind, long in (("sc", "Script"), ("scx", "Script_Extensions")):
                entry = (f"\\p{{{kind}={short}}}", f"\\P{{{kind}={short}}}", None)
                for name in names.split(" "):
                    _properties[f"{kind}={name}"] = _properties[f"{long}={name}"] = entry
        changes: _Ranges = []
        for item in tables.CHANGES_WHEN_NFKC_CASEFOLDED.split(" "):
            lo, _, hi = item.partition("-")
            changes.append((int(lo, 16), int(hi or lo, 16)))
        # The regex package has no property of these names, or (ASCII) has a block of the name.
        special: dict[str, tuple[str, str, _Ranges | None]] = {
            "ASCII": ("", "", [(0, 0x7F)]),
            "Any": ("", "", [(0, 0x10FFFF)]),
            "Assigned": ("\\P{gc=Cn}", "\\p{gc=Cn}", None),
            "Changes_When_NFKC_Casefolded": ("", "", changes),
        }
        for names in tables.BINARY.split(";"):
            long = names.split(" ")[0]
            binary = special.get(long, (f"\\p{{{long}=Yes}}", f"\\P{{{long}=Yes}}", None))
            for name in names.split(" "):
                _properties[name] = binary
    return _properties.get(body)


def _both_ways(properties: list[str]) -> bool:
    """Whether a class has a ``\\p{...}`` and a ``\\P{...}``. The regex package (2025.9.1 to 2026.9.29 at least) reads
    a class with a property and the same property negated as every character and then drops a ``^`` before it, so
    ``[^\\p{L}\\P{L}]`` takes any character there. A negated class with both is written as a character that the
    class without the ``^`` does not take."""
    return any(p.startswith("\\p") for p in properties) and any(p.startswith("\\P") for p in properties)


class _TooLarge(Exception):
    """The pattern is valid, and its translation would pass _MAX_TRANSLATED."""


def _literal(c: str) -> str:
    """A character as a pattern literal (outside or inside a class)."""
    o = ord(c)
    if o < 32 or o == 127:
        return f"\\x{o:02x}"
    if o < 128:
        return c if c.isalnum() else "\\" + c
    if o < 0x10000:
        return f"\\u{o:04x}"
    return f"\\U{o:08x}"


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


def _class_text(ranges: _Ranges) -> str:
    return "".join(_literal(chr(lo)) if lo == hi else f"{_literal(chr(lo))}-{_literal(chr(hi))}" for lo, hi in ranges)


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
        "first",
        "group",
        "icase",
        "items",
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
        "state",
        "text",
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
        # Whether the node can match without taking a character (None: not worked out yet).
        self.nothing: bool | None = None
        self.only_nothing: bool | None = None
        # _TEXT that takes a character: the characters it can take (None: any), or with ``others`` every character
        # but those. On a _TEXT that takes none, ``others`` marks a \B.
        self.chars: _Ranges | None = None
        self.others = False
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


class _Translator:
    def __init__(self, pattern: str, unicode: bool, ascii_flag: bool) -> None:
        self.p = pattern
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

    def error(self) -> _Invalid:
        return _Invalid(self.p)

    def peek(self, k: int = 0) -> str:
        j = self.i + k
        return self.p[j] if j < self.n else ""

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
        writer = _Writer(root, not self.ascii_flag, self.u)
        self.needs_regex = self.needs_regex or writer.needs_regex
        self.wide = not self.u and _sees_code_units(root)
        return writer.write()

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

    @staticmethod
    def _one(text: str, chars: _Ranges | None = None, others: bool = False) -> _Node:
        """The node of something that takes one character: one of ``chars``, or any (see ``_Node.chars``)."""
        n = _Node(_TEXT, text)
        n.nothing = False
        n.chars = chars
        n.others = others
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
            import regex

            if not self.u:
                # The pattern is read by code unit (see _units), and a name is of characters.
                name = name.encode("utf-16-le", "surrogatepass").decode("utf-16-le", "surrogatepass")

            if regex.fullmatch(r"[\p{ID_Start}$_][\p{ID_Continue}$\u200c\u200d]*", name) is None:
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
            while j < self.n and self.p[j].isdigit():
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
            member, ranges = self._property_escape()
            if self.icase:
                return self._folded_class(ranges or [], [member] if ranges is None else [], False), True
            if ranges is None:
                return self._one(member), True
            return self._one(f"[{member}]" if ranges else _NEVER, ranges), True
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
            return ("0-9" if in_class else "[0-9]"), None
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
        if e == "0" and not self.peek().isdigit():
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
            if in_class and not self.u and (c.isdigit() or c == "_"):
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
        if in_class and e.isdigit():
            self.i -= 1
            return "", self._legacy_octal_or_identity()
        # Annex B identity escape.
        return "", e

    def _property_escape(self) -> tuple[str, _Ranges | None]:
        """A ``\\p{...}`` or ``\\P{...}`` of a pattern with the ``u`` flag, from its ``p``: the class member that
        takes its characters, and the characters as ranges when the member is written from them (see ``_property``).
        Without the ``u`` flag ``\\p`` is the letter ``p`` (Annex B), which ``_char_escape`` reads."""
        negated = self.p[self.i] == "P"
        end = self.p.find("}", self.i)
        known = _property(self.p[self.i + 2 : end]) if end > 0 and self.peek(1) == "{" else None
        if known is None:
            raise self.error()
        self.i = end + 1
        positive, negative, ranges = known
        if ranges is None:
            self.needs_regex = True
            return (negative if negated else positive), None
        if negated:
            ranges = _complement(ranges)
        return _class_text(ranges), ranges

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
        # The same class as sets of characters, for a case-insensitive group: ranges, and properties as written.
        ranges: _Ranges = []
        properties: list[str] = []
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
        if not members and not outside:
            text = _NEVER if not negated else _ANY
        else:
            alternatives = ([f"[{''.join(members)}]"] if members else []) + outside
            text = alternatives[0] if len(alternatives) == 1 else "(?:" + "|".join(alternatives) + ")"
            if negated:
                apart = outside or _both_ways(properties)
                text = f"(?:(?!{text}){_ANY})" if apart else f"[^{''.join(members)}]"
        return self._one(text, None if properties else ranges, negated)

    def _class_atom(self, ranges: _Ranges, properties: list[str]) -> tuple[str, str | None]:
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
            member, sets = self._property_escape()
            if sets is None:
                properties.append(member)
            else:
                ranges.extend(sets)
            return member, None
        text, char = self._char_escape(in_class=True)
        if char is not None:
            return _literal(char), char
        word = _FOLD_WORD if self.u and self.icase else _WORD
        positive = _DIGIT if e in "dD" else word if e in "wW" else _WHITE
        ranges.extend(positive if e in "dws" else _complement(positive))
        return text, None

    def _folded_class(self, ranges: _Ranges, properties: list[str], negated: bool) -> _Node:
        """A class inside a case-insensitive group: it has every character that is equal, without case, to one of
        its members, and it is negated after that."""
        points, sets = _fold_table(self.u)
        ranges = _merged(ranges)
        more: list[str] = []
        for lo, hi in ranges:
            k = bisect_left(points, lo)
            while k < len(points) and points[k] <= hi:
                more.append(sets[points[k]])
                k += 1
        if properties:
            import regex

            try:
                members = [regex.compile(p).match for p in properties]
            except regex.error:
                raise self.error() from None
            for point in points:
                c = chr(point)
                if any(m(c) for m in members):
                    more.append(sets[point])
        ranges = _merged(ranges + [(ord(c), ord(c)) for c in set("".join(more))])
        if not ranges and not properties:
            text = _ANY if negated else _NEVER
        else:
            text = f"[{_class_text(ranges)}{''.join(properties)}]"
            if negated:
                text = f"(?:(?!{text}){_ANY})" if _both_ways(properties) else "[^" + text[1:]
        return self._one(text, None if properties else ranges, negated)


class _Writer:
    """Places every reference of a pattern's tree and writes the pattern for the engine.

    ``for_regex``: the pattern is written for the regex package. That engine remembers, for a repetition, the
    places where its body or what follows it has already failed, and does not try them again. It drops that when it
    sees a reference in the body or after it, as the outcome then depends on what a group holds. It does not see a
    reference that is inside a repetition with a maximum count, so ``^(?:(?:(a|)|){2})?(?:x\\1)?$`` does not match
    ``axa``. So in a pattern with a reference, every repetition is followed by a reference to an empty group, which
    the engine sees, and a repetition with a reference or a reset group inside it is written without a count:
    ``X{2,4}`` as ``XX(?:X(?:X|)|)`` and ``X{2,}`` as ``XX(?:X)*``."""

    def __init__(self, root: _Node, for_regex: bool, unicode: bool = True) -> None:
        self.root = root
        self.unicode = unicode
        self.groups: dict[int, _Node] = {}
        self.refs: list[_Node] = []
        self.count = 0
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
        out = self._write(self.root)
        if self.reset or self.seen:
            before = "(?P<z>)" if self.seen else ""
            out = before + "".join(f"(?P<g{g}>)" for g in sorted(self.reset)) + f"(?:{out})"
        if len(out) > _MAX_TRANSLATED:
            raise _TooLarge()
        if not self.tests:
            return out
        # The tests and the comparisons are written once each, and are not counted in the size: a test is as long as
        # the sets it is for, which no repetition multiplies.
        tests = "".join(self._test(name, key) for key, name in self.tests.items())
        comparisons = "".join(f"(?P<{name}>{text})" for name, text in self.comparisons.values())
        return f"(?(DEFINE){tests}{comparisons}){out}"

    @staticmethod
    def _test(name: str, sets: tuple[str, ...]) -> str:
        """The test that the character at the pointer of ``_folded`` and the character it has kept are equal
        without case: they are the same character, or both are in one of the sets. It is a group that is called,
        and it has no repetition of its own (the engine keeps the state of every repetition of a pattern at each
        lookaround it enters). Many sets are tried forty at a time, after a test that the character is in one of
        the forty."""

        def raw(c: str) -> str:
            return c if ord(c) > 127 else _literal(c)

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
            return n.text
        if k == _SEQUENCE:
            return "".join([self._write(x) for x in n.items])
        if k == _ALTERNATION:
            return "|".join([self._write(x) for x in n.items])
        if k == _GROUP:
            body = self._write(n.items[0])
            if n.group in self.read:
                return f"(?P<g{n.group}>{body})"
            # In a pattern with a reference that reads a group, a group that none reads does not capture.
            return f"({body})" if n.group and not self.read else f"(?:{body})"
        if k == _LOOK:
            return f"(?{'<' if n.behind else ''}{'!' if n.negative else '='}{self._write(n.items[0])})"
        if k == _REFERENCE:
            if n.state == _EMPTY:
                return "(?:)"
            exact = f"(?P=g{n.group})"
            return self._folded(exact, n.same, n.back) if n.same else exact
        return self._repeat(n)

    def _repeat(self, n: _Node) -> str:
        body = n.items[0]
        lo, hi = n.min, n.max
        if hi == 0:
            return "(?:)"
        if _only_nothing(body):
            # Every iteration matches nothing, and ECMA-262 refuses one that does once the minimum count is met. So
            # none runs for a minimum of 0. For any other, the groups are left as one iteration leaves them.
            return self._write(body) if lo else "(?:)"
        if lo >= _MAX_COUNT:
            if not _matches_nothing(body):
                # More characters than a text has.
                return "(?!)"
            # That many iterations, of which all but a few match nothing, are beyond both engines.
            raise _TooLarge()
        text = self._write(body)
        k = bisect_left(self.live, n.first)
        read = self.seen != "" and (
            n.first in self.forced
            or self._holds_one(self.compared, n)
            or any(n.first < self.groups[g].first < n.last for g in self.read)
        )
        if not read and not (self.seen and k < len(self.live) and self.live[k] < n.last):
            # A reference to a name that two groups have is two references, which the count is for together.
            if body.kind == _SEQUENCE or not text.startswith("("):
                text = f"(?:{text})"
            counted = text + self._count(lo, hi, n.lazy)
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


def _translate(pattern: str, unicode: bool, for_regex: bool) -> tuple[str, bool, bool] | None:
    """As ``translate``, and whether the pattern is one without the ``u`` flag for which a text is matched as its
    code units (see ``_units``)."""
    try:
        if not unicode:
            pattern = _units(pattern)
        if not for_regex:
            t = _Translator(pattern, unicode, True)
            text = t.translate()
            if not t.needs_regex:
                return text, False, t.wide
        t = _Translator(pattern, unicode, False)
        return t.translate(), True, t.wide
    except (_Invalid, KeyError, ValueError, IndexError):
        return None


def translate(pattern: str, unicode: bool = True, for_regex: bool = False) -> tuple[str, bool] | None:
    """Translates an ECMA-262 pattern to Python syntax: the translation and whether it is for the ``regex`` package
    (and so is compiled without the ASCII flag), or None when the pattern is not valid in the grammar. It is for
    ``regex`` when the pattern needs it or ``for_regex`` asks."""
    t = _translate(pattern, unicode, for_regex)
    return None if t is None else t[:2]


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

_cache: dict[str, Matcher | None] = {}


def _compile(pattern: str, unicode: bool) -> Matcher | None:
    search = _search(pattern, unicode)
    if search is None or unicode or not search[1]:
        return None if search is None else search[0]
    find = search[0]

    def by_code_unit(text: str) -> Any:
        return find(text if text.isascii() else _units(text))

    return by_code_unit


def _search(pattern: str, unicode: bool) -> tuple[Matcher, bool] | None:
    t = _translate(pattern, unicode, False)
    if t is None:
        return None
    if not t[1]:
        try:
            return re.compile(t[0], re.ASCII).search, t[2]
        except re.error:
            # The regex package takes more (a lookbehind that the standard library does not, say).
            t = _translate(pattern, unicode, True)
            if t is None:
                return None
    import regex  # only patterns the standard library cannot run need it

    try:
        return regex.compile(t[0]).search, t[2]
    except regex.error as e:
        # The pattern is valid, and every property in it is one the engine has. A translation the engine does not
        # compile is a fault of the translator, and is not passed off as a pattern that is not valid.
        raise RuntimeError(
            f"corvus-json-schema translated the regular expression {pattern!r} to one that the regex package does "
            f"not compile ({e}). This is a fault of the package. The translation starts {t[0][:500]!r}."
        ) from None


def compile_pattern(pattern: str) -> Matcher | None:
    """A search function for an ECMA-262 pattern (truthy on a match), or None when the pattern is invalid. A valid
    pattern whose translation would be too large raises SchemaCompilationError."""
    if pattern in _cache:
        return _cache[pattern]
    try:
        matcher = _compile(pattern, True)
        if matcher is None:
            matcher = _compile(pattern, False)
    except _TooLarge:
        raise SchemaCompilationError(
            f"The regular expression '{pattern}' is valid, and too large for the engine once its repetitions are "
            "written out."
        ) from None
    _cache[pattern] = matcher
    return matcher


def is_valid_ecma_regex(s: str) -> bool:
    """Whether the text is a valid ECMA-262 pattern with the ``u`` flag (``format: regex``)."""
    try:
        return _compile(s, True) is not None
    except (_TooLarge, RuntimeError):
        # The pattern is valid, whatever the engine made of its translation.
        return True
