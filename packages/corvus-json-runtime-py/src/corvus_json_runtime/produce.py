"""The immer-style ``produce`` and the byte-native read-modify-write helpers (Model C).

The differentiator path: a partial update is applied by SPLICING the changed members' value bytes into the
source document, copying every other byte through verbatim, rather than parsing + re-serialising the whole
value. A faithful port of the TypeScript runtime's Model C (scan / edit / rmw functions). ``produce`` (the
draft-proxy recipe) and array-element edits are later slices and still raise ``NotImplementedError``.
"""

from __future__ import annotations

from collections.abc import Callable, Mapping, Sequence
from dataclasses import dataclass, field
from typing import NamedTuple, TypedDict

from .core import build, canonicalize, decode_and_parse

_QUOTE = 0x22
_BACKSLASH = 0x5C
_COMMA = 0x2C
_COLON = 0x3A
_LBRACE = 0x7B
_RBRACE = 0x7D
_LBRACK = 0x5B
_RBRACK = 0x5D


def _is_ws(b: int) -> bool:
    return b == 0x20 or b == 0x09 or b == 0x0A or b == 0x0D


@dataclass
class RmwTarget:
    """A member to upsert: ``name`` (raw UTF-8, unquoted) and its new value ``content`` (JSON bytes). ``vbs`` /
    ``vbe`` are the source value-byte span found by :func:`scan_targets` (``-1`` = not present)."""

    name: bytes
    content: bytes
    vbs: int = field(default=-1)
    vbe: int = field(default=-1)


class RmwMember(NamedTuple):
    """A scanned object member: name span [ns, ne) (INCLUDING the quotes) and value span [vs, ve)."""

    ns: int
    ne: int
    vs: int
    ve: int


class ListOps(TypedDict, total=False):
    """Element edits on one array: ``set`` (replace at index), ``insert`` (splice values before an index),
    ``remove_at`` (delete indices), ``append`` (add to the end). Indices are 0-based into the source array."""

    set: Mapping[int, object]
    insert: Mapping[int, Sequence[object]]
    remove_at: Sequence[int]
    append: Sequence[object]


class RmwArrayOps(ListOps, total=False):
    """A :class:`ListOps` plus an optional ``rest`` block whose indices are offset by ``prefix_len`` (the
    prefixItems/tuple tail), a faithful port of the TS RmwArrayOps."""

    rest: ListOps


@dataclass
class RmwArrayEdit:
    """An array-valued member to edit in place: its ``name`` (raw UTF-8) and the ``ops`` to apply."""

    name: bytes
    ops: RmwArrayOps
    prefix_len: int = field(default=0)


def _skip_string_from(buf: bytes, i: int) -> int:
    i += 1
    while True:
        q = buf.find(b'"', i)
        if q < 0:
            raise ValueError("unterminated string")
        b, bs = q - 1, 0
        while b >= 0 and buf[b] == _BACKSLASH:
            bs += 1
            b -= 1
        i = q + 1
        if (bs & 1) == 0:
            return i


def _skip_container_from(buf: bytes, i: int, length: int) -> int:
    depth = 0
    while True:
        if i >= length:
            raise ValueError("unterminated container")
        b = buf[i]
        if b == _QUOTE:
            i = _skip_string_from(buf, i)
            continue
        if b == _LBRACE or b == _LBRACK:
            depth += 1
            i += 1
            continue
        if b == _RBRACE or b == _RBRACK:
            depth -= 1
            i += 1
            if depth == 0:
                return i
            continue
        i += 1


def _skip_value_from(buf: bytes, i: int, length: int) -> int:
    b = buf[i]
    if b == _QUOTE:
        return _skip_string_from(buf, i)
    if b == _LBRACE or b == _LBRACK:
        return _skip_container_from(buf, i, length)
    while i < length:
        c = buf[i]
        if c == _COMMA or c == _RBRACE or c == _RBRACK or _is_ws(c):
            break
        i += 1
    return i


def _eq_name(buf: bytes, start: int, end: int, name: bytes) -> bool:
    return buf[start:end] == name


def scan_targets(buf: bytes, targets: Sequence[RmwTarget]) -> bool:
    """Locate each target's value-byte span in ``buf``, setting ``vbs``/``vbe`` (``-1`` if absent). Returns
    True when every target was found (the fast in-place upsert path)."""
    length = len(buf)
    i = 0
    remaining = len(targets)
    for t in targets:
        t.vbs = -1
    if length >= 3 and buf[0] == 0xEF and buf[1] == 0xBB and buf[2] == 0xBF:
        i = 3
    while i < length and _is_ws(buf[i]):
        i += 1
    i += 1
    while remaining > 0 and i < length:
        while i < length and _is_ws(buf[i]):
            i += 1
        if i >= length or buf[i] == _RBRACE:
            break
        ns = i + 1
        i = _skip_string_from(buf, i)
        ne = i - 1
        hit = -1
        for k, t in enumerate(targets):
            if t.vbs == -1 and _eq_name(buf, ns, ne, t.name):
                hit = k
                break
        while i < length and _is_ws(buf[i]):
            i += 1
        i += 1
        while i < length and _is_ws(buf[i]):
            i += 1
        vs = i
        i = _skip_value_from(buf, i, length)
        if hit >= 0:
            targets[hit].vbs = vs
            targets[hit].vbe = i
            remaining -= 1
        while i < length and _is_ws(buf[i]):
            i += 1
        if i < length and buf[i] == _COMMA:
            i += 1
            continue
        if i < length and buf[i] == _RBRACE:
            break
    return remaining == 0


def apply_edits_bytes(buf: bytes, edits: list[tuple[int, int, bytes]]) -> bytes:
    """Apply non-overlapping ``(offset, length, content)`` edits to ``buf``, copying the rest through."""
    edits.sort(key=lambda e: e[0])
    out = bytearray()
    src = 0
    for offset, length, content in edits:
        out += buf[src:offset]
        out += content
        src = offset + length
    out += buf[src:]
    return bytes(out)


def scan_all(buf: bytes) -> tuple[list[RmwMember], int]:
    """Scan every top-level object member; returns the members and the offset of the closing ``}``."""
    length = len(buf)
    i = 0
    if length >= 3 and buf[0] == 0xEF and buf[1] == 0xBB and buf[2] == 0xBF:
        i = 3
    while i < length and _is_ws(buf[i]):
        i += 1
    i += 1
    out: list[RmwMember] = []
    while True:
        while i < length and _is_ws(buf[i]):
            i += 1
        if i >= length or buf[i] == _RBRACE:
            break
        ns = i
        i = _skip_string_from(buf, i)
        ne = i
        while i < length and _is_ws(buf[i]):
            i += 1
        i += 1
        while i < length and _is_ws(buf[i]):
            i += 1
        vs = i
        i = _skip_value_from(buf, i, length)
        out.append(RmwMember(ns, ne, vs, i))
        while i < length and _is_ws(buf[i]):
            i += 1
        if i < length and buf[i] == _COMMA:
            i += 1
            continue
        if i < length and buf[i] == _RBRACE:
            break
    return out, i


def find_member(buf: bytes, members: Sequence[RmwMember], name: bytes) -> int:
    for k, m in enumerate(members):
        if _eq_name(buf, m.ns + 1, m.ne - 1, name):
            return k
    return -1


def member_bytes(name: bytes, content: bytes) -> bytes:
    return b'"' + name + b'":' + content


def scan_array_elements(buf: bytes) -> tuple[list[tuple[int, int]], int]:
    """Scan every element of a top-level array; returns each element's [vs, ve) span and the closing ``]``."""
    length = len(buf)
    i = 0
    if length >= 3 and buf[0] == 0xEF and buf[1] == 0xBB and buf[2] == 0xBF:
        i = 3
    while i < length and _is_ws(buf[i]):
        i += 1
    i += 1
    out: list[tuple[int, int]] = []
    while True:
        while i < length and _is_ws(buf[i]):
            i += 1
        if i >= length or buf[i] == _RBRACK:
            break
        vs = i
        i = _skip_value_from(buf, i, length)
        out.append((vs, i))
        while i < length and _is_ws(buf[i]):
            i += 1
        if i < length and buf[i] == _COMMA:
            i += 1
            continue
        if i < length and buf[i] == _RBRACK:
            break
    return out, i


def rmw_array_bytes(source: bytes, ops: RmwArrayOps, prefix_len: int) -> bytes:
    """Apply element edits to an array's bytes, splicing changed/inserted/appended elements in and copying the
    rest verbatim (the array peer of :func:`rmw_produce_full`)."""
    elems, close = scan_array_elements(source)
    m = len(elems)
    edits: list[tuple[int, int, bytes]] = []
    removed = [False] * m
    appends: list[object] = []

    def apply_ops(o: ListOps, off: int) -> None:
        s = o.get("set")
        if s:
            for k, v in s.items():
                vs, ve = elems[off + k]
                edits.append((vs, ve - vs, build(v)))
        ins = o.get("insert")
        if ins:
            for k, vals in ins.items():
                vs, _ = elems[off + k]
                parts: list[bytes] = []
                for v in vals:
                    parts.append(build(v))
                    parts.append(b",")
                edits.append((vs, 0, b"".join(parts)))
        rem = o.get("remove_at")
        if rem:
            for r in rem:
                removed[off + r] = True
        ap = o.get("append")
        if ap:
            appends.extend(ap)

    apply_ops(ops, 0)
    rest = ops.get("rest")
    if rest:
        apply_ops(rest, prefix_len)
    comma = b","
    j = 0
    while j < m:
        if not removed[j]:
            j += 1
            continue
        hi = j
        while hi + 1 < m and removed[hi + 1]:
            hi += 1
        if j == 0 and hi == m - 1:
            edits.append((elems[0][0], elems[m - 1][1] - elems[0][0], b""))
        elif j == 0:
            edits.append((elems[0][0], elems[hi + 1][0] - elems[0][0], b""))
        elif hi == m - 1:
            edits.append((elems[j - 1][1], elems[m - 1][1] - elems[j - 1][1], b""))
        else:
            edits.append((elems[j - 1][1], elems[hi + 1][0] - elems[j - 1][1], comma))
        j = hi + 1
    if appends:
        surviving = sum(1 for k in range(m) if not removed[k])
        tail: list[bytes] = []
        for a, v in enumerate(appends):
            if a > 0 or surviving > 0:
                tail.append(comma)
            tail.append(build(v))
        edits.append((close, 0, b"".join(tail)))
    if not edits:
        return source
    return apply_edits_bytes(source, edits)


def rmw_upsert(source: bytes, targets: Sequence[RmwTarget]) -> bytes:
    """Splice changed members' value bytes into ``source``, copying the rest through verbatim."""
    if len(targets) == 0:
        return source
    scan_targets(source, targets)
    if all(t.vbs >= 0 for t in targets):
        edits = [(t.vbs, t.vbe - t.vbs, t.content) for t in targets]
        return apply_edits_bytes(source, edits)
    return rmw_produce_full(source, targets, [], [])


def rmw_produce_full(
    source: bytes,
    set_targets: Sequence[RmwTarget],
    remove_names: Sequence[bytes],
    array_edits: Sequence[RmwArrayEdit],
) -> bytes:
    """Produce a new document applying member upserts, array-element edits, and removals over ``source`` bytes."""
    members, close = scan_all(source)
    n = len(members)
    edits: list[tuple[int, int, bytes]] = []
    adds: list[RmwTarget] = []
    for t in set_targets:
        i = find_member(source, members, t.name)
        if i < 0:
            adds.append(t)
        else:
            edits.append((members[i].vs, members[i].ve - members[i].vs, t.content))
    for edit in array_edits:
        i = find_member(source, members, edit.name)
        if i < 0:
            raise ValueError("patch: array member not present in source")
        vs, ve = members[i].vs, members[i].ve
        edits.append((vs, ve - vs, rmw_array_bytes(source[vs:ve], edit.ops, edit.prefix_len)))
    removed = [False] * n
    for name in remove_names:
        i = find_member(source, members, name)
        if i < 0:
            raise ValueError("patch: removed member not present in source")
        removed[i] = True
    comma = b","
    j = 0
    while j < n:
        if not removed[j]:
            j += 1
            continue
        hi = j
        while hi + 1 < n and removed[hi + 1]:
            hi += 1
        if j == 0 and hi == n - 1:
            edits.append((members[0].ns, members[n - 1].ve - members[0].ns, b""))
        elif j == 0:
            edits.append((members[0].ns, members[hi + 1].ns - members[0].ns, b""))
        elif hi == n - 1:
            edits.append((members[j - 1].ve, members[n - 1].ve - members[j - 1].ve, b""))
        else:
            edits.append((members[j - 1].ve, members[hi + 1].ns - members[j - 1].ve, comma))
        j = hi + 1
    if adds:
        surviving = sum(1 for k in range(n) if not removed[k])
        parts: list[bytes] = []
        for a, add in enumerate(adds):
            if a > 0 or surviving > 0:
                parts.append(comma)
            parts.append(member_bytes(add.name, add.content))
        edits.append((close, 0, b"".join(parts)))
    if not edits:
        return source
    return apply_edits_bytes(source, edits)


def produce(source: bytes, recipe: Callable[[object], None]) -> bytes:
    """Apply ``recipe`` to a mutable parsed draft of ``source``, returning the result as canonical bytes.

    ``recipe`` mutates the decoded value in place (e.g. ``draft["k"] = v``). This is the recipe-driven
    whole-value transform, the idiomatic-Python counterpart to the TypeScript immer draft (which the byte-native
    ``patch`` path already covers for targeted, splice-only edits).
    """
    draft = decode_and_parse(source)
    recipe(draft)
    return canonicalize(draft)
