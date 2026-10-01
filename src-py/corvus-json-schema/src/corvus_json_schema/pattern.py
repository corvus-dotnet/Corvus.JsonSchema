"""ECMA-262 regular expressions (as JSON Schema's ``pattern`` and ``format: regex`` specify), run by Python's engines.

A pattern is translated to Python's syntax with ECMA's meaning and compiled with the ASCII flag, so that ``\\d``,
``\\w`` and ``\\b`` keep their ECMA (ASCII) definitions. ECMA's ``\\s``, ``.`` and ``$`` are spelled out, named groups
are renamed to identifiers, and every literal is escaped. The standard library's ``re`` runs what it can; the
``regex`` package runs the rest (Unicode properties, variable-width lookbehind).

Patterns are parsed with the ``u`` flag's grammar first; one that only parses without it (Annex B: identity escapes
such as ``\\-``, literal braces) is translated by the lenient grammar instead, as JavaScript's ``RegExp`` would be
constructed without the flag.
"""

from __future__ import annotations

import re
from collections.abc import Callable
from typing import Any

# ECMA-262 WhiteSpace and LineTerminator (what \s matches).
_SPACE = "\\t\\n\\x0b\\x0c\\r \\xa0\\u1680\\u2000-\\u200a\\u2028\\u2029\\u202f\\u205f\\u3000\\ufeff"
_DOT = "[^\\n\\r\\u2028\\u2029]"
_SYNTAX = set("^$\\.*+?()[]{}|/")
_CLASS_ESCAPES = set("dDwWsS")
_QUANTIFIER_RE = re.compile(r"\{(\d+)(,(\d*))?\}")
_PROPERTY_RE = re.compile(r"[A-Za-z0-9_]+(=[A-Za-z0-9_]+)?")


class _Invalid(Exception):
    pass


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


class _Translator:
    def __init__(self, pattern: str, unicode: bool, ascii_flag: bool) -> None:
        self.p = pattern
        # With the ASCII flag (the standard library's engine), \d, \w and \b are ECMA's; without it (the regex
        # package, whose ASCII flag would also narrow \p{...}), they are spelled out.
        self.ascii_flag = ascii_flag
        self.n = len(pattern)
        self.u = unicode
        self.i = 0
        self.groups = 0
        self.names: dict[str, str] = {}
        self.needs_regex = False
        # Backreferences are checked against the group count once the whole pattern is read.
        self.backrefs: list[int] = []
        self.named_refs: list[str] = []

    def error(self) -> _Invalid:
        return _Invalid(self.p)

    def peek(self, k: int = 0) -> str:
        j = self.i + k
        return self.p[j] if j < self.n else ""

    def translate(self) -> str:
        self._count_groups()
        out = self._alternation(top=True)
        if self.i != self.n:
            raise self.error()
        for b in self.backrefs:
            if b > self.groups:
                raise self.error()
        for name in self.named_refs:
            if name not in self.names:
                raise self.error()
        return out

    def _count_groups(self) -> None:
        # Capturing groups, for deciding whether \N is a backreference (and the names of named groups).
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
                    end = p.find(">", i)
                    if end < 0:
                        raise self.error()
                    name = p[i + 3 : end]
                    if not name or name in self.names:
                        raise self.error()
                    self.groups += 1
                    self.names[name] = f"g{self.groups}"
                elif not p.startswith("(?", i):
                    self.groups += 1
            i += 1

    def _alternation(self, top: bool) -> str:
        parts = [self._sequence()]
        while self.peek() == "|":
            self.i += 1
            parts.append(self._sequence())
        if not top and self.peek() != ")":
            raise self.error()
        return "|".join(parts)

    def _sequence(self) -> str:
        out: list[str] = []
        while self.i < self.n and self.peek() not in "|)":
            atom, quantifiable = self._term()
            if self.i < self.n and self.peek() in "*+?{":
                q = self._quantifier()
                if q is not None:
                    if not quantifiable:
                        raise self.error()
                    atom = f"(?:{atom}){q}" if not atom.startswith("(") else atom + q
            out.append(atom)
        return "".join(out)

    def _quantifier(self) -> str | None:
        c = self.peek()
        if c == "{":
            m = _QUANTIFIER_RE.match(self.p, self.i)
            if m is None:
                if self.u:
                    raise self.error()
                return None  # a literal brace (Annex B); the next term reads it
            lo = int(m.group(1))
            hi = m.group(3)
            if m.group(2) is not None and hi != "" and int(hi) < lo:
                raise self.error()
            self.i = m.end()
            q = m.group(0)
        else:
            self.i += 1
            q = c
        if self.peek() == "?":
            self.i += 1
            q += "?"
        if self.peek() in ("*", "+", "?") or (self.peek() == "{" and _QUANTIFIER_RE.match(self.p, self.i)):
            raise self.error()
        return q

    def _term(self) -> tuple[str, bool]:
        c = self.peek()
        if c == "^":
            self.i += 1
            return "^", False
        if c == "$":
            self.i += 1
            return "\\Z", False
        if c == ".":
            self.i += 1
            return _DOT, True
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
            return "\\{", True
        if c in "}]":
            if self.u:
                raise self.error()
            self.i += 1
            return "\\" + c, True
        self.i += 1
        return self._char(c), True

    def _char(self, c: str) -> str:
        # A literal character; an astral character is one code point (as with the u flag).
        return _literal(c)

    def _group(self) -> tuple[str, bool]:
        p = self.p
        i = self.i
        if p.startswith("(?:", i):
            self.i += 3
            prefix, quantifiable = "(?:", True
        elif p.startswith("(?=", i) or p.startswith("(?!", i):
            self.i += 3
            prefix, quantifiable = p[i : i + 3], not self.u
        elif p.startswith("(?<=", i) or p.startswith("(?<!", i):
            self.i += 4
            prefix, quantifiable = p[i : i + 4], False
            self.needs_regex = True
        elif p.startswith("(?<", i):
            end = p.find(">", i)
            name = p[i + 3 : end]
            self.i = end + 1
            prefix, quantifiable = f"(?P<{self.names[name]}>", True
        elif p.startswith("(?", i):
            raise self.error()
        else:
            self.i += 1
            prefix, quantifiable = "(", True
        body = self._alternation(top=False)
        self.i += 1  # ')'
        return prefix + body + ")", quantifiable

    def _escape(self) -> tuple[str, bool]:
        self.i += 1
        if self.i >= self.n:
            raise self.error()
        e = self.p[self.i]
        if e == "b":
            self.i += 1
            return "\\b" if self.ascii_flag else "(?a:\\b)", False
        if e == "B":
            self.i += 1
            return "\\B" if self.ascii_flag else "(?a:\\B)", False
        if e in "123456789":
            j = self.i
            while j < self.n and self.p[j].isdigit():
                j += 1
            number = int(self.p[self.i : j])
            if number <= self.groups:
                self.i = j
                self.backrefs.append(number)
                return f"(?:\\{number})", True
            if self.u:
                raise self.error()
            return self._legacy_octal_or_identity(), True
        if e == "k":
            if self.peek(1) == "<":
                end = self.p.find(">", self.i)
                if end > 0:
                    name = self.p[self.i + 2 : end]
                    self.i = end + 1
                    if name in self.names:
                        return f"(?P={self.names[name]})", True
                    if self.u or self.names:
                        raise self.error()
                    return "k<" + "".join(_literal(ch) for ch in name) + ">", True
            if self.u or self.names:
                raise self.error()
            self.i += 1
            return "k", True
        text = self._char_escape(in_class=False)
        return text, True

    def _legacy_octal_or_identity(self) -> str:
        # Annex B: \8 and \9 are identity escapes; other digits start a legacy octal escape.
        e = self.p[self.i]
        if e in "89":
            self.i += 1
            return _literal(e)
        j = self.i
        while j < self.n and j < self.i + 3 and self.p[j] in "01234567":
            j += 1
        value = int(self.p[self.i : j], 8)
        if value > 0o377:
            j -= 1
            value = int(self.p[self.i : j], 8)
        self.i = j
        return _literal(chr(value))

    def _char_escape(self, in_class: bool) -> str:
        """An escape after the backslash at ``self.i`` that denotes a character or a class: its translation."""
        e = self.p[self.i]
        self.i += 1
        if e == "d":
            return "0-9" if in_class else "[0-9]"
        if e == "D":
            return "\\D" if self.ascii_flag else "[^0-9]"
        if e == "w":
            return "\\w" if self.ascii_flag else "0-9A-Za-z_" if in_class else "[0-9A-Za-z_]"
        if e == "W":
            return "\\W" if self.ascii_flag else "[^0-9A-Za-z_]"
        if e == "s":
            return _SPACE if in_class else f"[{_SPACE}]"
        if e == "S":
            return f"[^{_SPACE}]"
        if e == "n":
            return "\\n"
        if e == "r":
            return "\\r"
        if e == "t":
            return "\\t"
        if e == "f":
            return "\\x0c"
        if e == "v":
            return "\\x0b"
        if e == "0" and not self.peek().isdigit():
            return "\\x00"
        if e == "b" and in_class:
            return "\\x08"
        if e == "-" and in_class:
            return "\\-"
        if e == "c":
            c = self.peek()
            if ("a" <= c <= "z") or ("A" <= c <= "Z"):
                self.i += 1
                return _literal(chr(ord(c) % 32))
            if in_class and not self.u and (c.isdigit() or c == "_"):
                self.i += 1
                return _literal(chr(ord(c) % 32))
            if self.u:
                raise self.error()
            self.i -= 1  # '\c' is a literal backslash then 'c'
            return "\\\\"
        if e in "pP":
            if self.u and self.peek() == "{":
                end = self.p.find("}", self.i)
                body = self.p[self.i + 1 : end] if end > 0 else ""
                if not body or _PROPERTY_RE.fullmatch(body) is None:
                    raise self.error()
                self.i = end + 1
                self.needs_regex = True
                return f"\\{e}{{{body}}}"
            if self.u:
                raise self.error()
            return e
        if e == "x":
            hexa = self.p[self.i : self.i + 2]
            if len(hexa) == 2 and all(h in "0123456789abcdefABCDEF" for h in hexa):
                self.i += 2
                return _literal(chr(int(hexa, 16)))
            if self.u:
                raise self.error()
            return "x"
        if e == "u":
            code = self._unicode_escape()
            if code is not None:
                return _literal(chr(code))
            if self.u:
                raise self.error()
            return "u"
        if e in _SYNTAX:
            return _literal(e)
        if e == "0" and in_class and not self.u:
            return self._legacy_octal_after_zero()
        if self.u:
            raise self.error()
        if e == "0":
            return self._legacy_octal_after_zero()
        if in_class and e.isdigit():
            self.i -= 1
            return self._legacy_octal_or_identity()
        # Annex B identity escape.
        return _literal(e)

    def _legacy_octal_after_zero(self) -> str:
        self.i -= 1
        return self._legacy_octal_or_identity()

    def _unicode_escape(self) -> int | None:
        p = self.p
        if self.u and self.peek() == "{":
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
        if self.u and 0xD800 <= hi < 0xDC00 and p.startswith("\\u", self.i):
            lo = four(self.i + 2)
            if lo is not None and 0xDC00 <= lo < 0xE000:
                self.i += 6
                return 0x10000 + ((hi - 0xD800) << 10) + (lo - 0xDC00)
        return hi

    def _class(self) -> str:
        self.i += 1
        negated = self.peek() == "^"
        if negated:
            self.i += 1
        members: list[str] = []
        # Class escapes that Python cannot nest in a class (ECMA's \S), kept aside as alternatives.
        outside: list[str] = []
        while True:
            if self.i >= self.n:
                raise self.error()
            c = self.peek()
            if c == "]":
                self.i += 1
                break
            lo, lo_char = self._class_atom()
            if self.peek() == "-" and self.peek(1) not in ("]", ""):
                self.i += 1
                hi, hi_char = self._class_atom()
                if lo_char is None or hi_char is None:
                    if self.u:
                        raise self.error()
                    # Annex B: a class escape at either end makes the '-' a literal.
                    for t in (lo, "\\-", hi):
                        (outside if t.startswith("[^") else members).append(t)
                    continue
                if ord(hi_char) < ord(lo_char):
                    raise self.error()
                members.append(f"{lo}-{hi}")
                continue
            (outside if lo.startswith("[^") else members).append(lo)
        if not members and not outside:
            return "[^\\s\\S]" if not negated else "(?s:.)"
        alternatives = ([f"[{''.join(members)}]"] if members else []) + outside
        body = alternatives[0] if len(alternatives) == 1 else "(?:" + "|".join(alternatives) + ")"
        if not negated:
            return body
        if not outside:
            return f"[^{''.join(members)}]"
        return f"(?:(?!{body})(?s:.))"

    def _class_atom(self) -> tuple[str, str | None]:
        """One class atom: its translation and, when it is a single character, that character."""
        c = self.peek()
        if c != "\\":
            self.i += 1
            return _literal(c), c
        self.i += 1
        if self.i >= self.n:
            raise self.error()
        e = self.p[self.i]
        if e in _CLASS_ESCAPES or (e in "pP" and self.u):
            return self._char_escape(in_class=True), None
        start = self.i
        text = self._char_escape(in_class=True)
        return text, _single_char(self.p[start - 1 : self.i])


def _single_char(escape: str) -> str | None:
    """The character an escape (with its backslash) stands for, for range bounds."""
    body = escape[1:]
    simple = {"n": "\n", "r": "\r", "t": "\t", "f": "\x0c", "v": "\x0b", "b": "\x08", "0": "\x00", "-": "-"}
    if body in simple:
        return simple[body]
    if body.startswith("x") and len(body) == 3:
        return chr(int(body[1:], 16))
    if body.startswith("u{"):
        return chr(int(body[2:-1], 16))
    if body.startswith("u") and len(body) >= 5:
        hi = int(body[1:5], 16)
        if len(body) == 11:
            lo = int(body[7:11], 16)
            return chr(0x10000 + ((hi - 0xD800) << 10) + (lo - 0xDC00))
        return chr(hi)
    if body.startswith("c") and len(body) == 2:
        return chr(ord(body[1]) % 32)
    if body and body[0] in "01234567" and all(ch in "01234567" for ch in body):
        return chr(int(body, 8))
    return body[0] if len(body) == 1 else None


def translate(pattern: str, unicode: bool = True) -> tuple[str, bool] | None:
    """Translates an ECMA-262 pattern to Python syntax: the translation and whether it needs the ``regex`` package
    (and so is compiled without the ASCII flag), or None when the pattern is not valid in the grammar."""
    try:
        t = _Translator(pattern, unicode, True)
        text = t.translate()
        if not t.needs_regex:
            return text, False
        return _Translator(pattern, unicode, False).translate(), True
    except (_Invalid, KeyError, ValueError, IndexError):
        return None


Matcher = Callable[[str], Any]

_cache: dict[str, Matcher | None] = {}


def _compile(translated: str, needs_regex: bool) -> Matcher | None:
    if not needs_regex:
        try:
            return re.compile(translated, re.ASCII).search
        except re.error:
            pass
    import regex  # only patterns the standard library cannot run need it

    try:
        return regex.compile(translated).search
    except regex.error:
        return None


def compile_pattern(pattern: str) -> Matcher | None:
    """A search function for an ECMA-262 pattern (truthy on a match), or None when the pattern is invalid."""
    if pattern in _cache:
        return _cache[pattern]
    matcher: Matcher | None = None
    for unicode in (True, False):
        t = translate(pattern, unicode)
        if t is not None:
            matcher = _compile(*t)
            if matcher is not None:
                break
    _cache[pattern] = matcher
    return matcher


def is_valid_ecma_regex(s: str) -> bool:
    """Whether the text is a valid ECMA-262 pattern with the ``u`` flag (``format: regex``)."""
    t = translate(s, True)
    return t is not None and _compile(*t) is not None
