"""URI handling for schema identification and reference resolution (RFC 3986 section 5).

Mirrors Corvus.Text.Json.RuntimeEvaluator.Compilation.UriUtilities, but implements reference resolution directly so
that opaque bases (urn:, tag:) resolve the same way on every host.
"""

from __future__ import annotations

import re
from dataclasses import dataclass, replace
from typing import Any

_URI_RE = re.compile(r"^(?:([A-Za-z][A-Za-z0-9+.-]*):)?(?://([^/?#]*))?([^?#]*)(?:\?([^#]*))?(?:#(.*))?$", re.S)
_SCHEME_RE = re.compile(r"^[A-Za-z][A-Za-z0-9+.-]*:")
_INDEX_RE = re.compile(r"^(?:0|[1-9][0-9]*)$")


@dataclass
class _UriParts:
    scheme: str | None = None
    authority: str | None = None
    path: str = ""
    query: str | None = None
    fragment: str | None = None


def _parse(uri: str) -> _UriParts:
    m = _URI_RE.match(uri)
    assert m is not None
    return _UriParts(m.group(1), m.group(2), m.group(3) or "", m.group(4), m.group(5))


def _format(p: _UriParts) -> str:
    s = ""
    if p.scheme is not None:
        s += p.scheme + ":"
    if p.authority is not None:
        s += "//" + p.authority
    s += p.path
    if p.query is not None:
        s += "?" + p.query
    if p.fragment is not None:
        s += "#" + p.fragment
    return s


def _remove_dot_segments(path: str) -> str:
    if "." not in path:
        return path
    segments = path.split("/")
    out: list[str] = []
    last = len(segments) - 1
    for i, seg in enumerate(segments):
        if seg == ".":
            if i == last:
                out.append("")
            continue
        if seg == "..":
            if len(out) > 1 or (len(out) == 1 and out[0] != ""):
                out.pop()
            if i == last:
                out.append("")
            continue
        out.append(seg)
    return "/".join(out)


def _merge(base: _UriParts, ref_path: str) -> str:
    if base.authority is not None and base.path == "":
        return "/" + ref_path
    i = base.path.rfind("/")
    return base.path[: i + 1] + ref_path if i >= 0 else ref_path


def has_scheme(reference: str) -> bool:
    """True when the reference starts with a URI scheme."""
    return _SCHEME_RE.match(reference) is not None


def split(reference: str) -> tuple[str, str]:
    """Splits a reference at its first '#'."""
    hash_index = reference.find("#")
    return (reference, "") if hash_index < 0 else (reference[:hash_index], reference[hash_index + 1 :])


def _normalize_parts(p: _UriParts) -> str:
    n = replace(p, fragment=None)
    if n.scheme is not None:
        n.scheme = n.scheme.lower()
    if n.authority is not None:
        n.authority = n.authority.lower()
        if (n.scheme == "http" and n.authority.endswith(":80")) or (
            n.scheme == "https" and n.authority.endswith(":443")
        ):
            n.authority = n.authority[: n.authority.rfind(":")]
        if n.path == "":
            n.path = "/"
    n.path = _remove_dot_segments(n.path)
    return _format(n)


def normalize(uri: str) -> str:
    """Normalises an absolute URI (without its fragment) so that equivalent spellings compare equal."""
    uri_part = split(uri)[0]
    if not has_scheme(uri_part):
        return uri_part
    return _normalize_parts(_parse(uri_part))


def resolve(base_uri: str, reference: str) -> str:
    """Resolves a reference (without fragment) against a base URI, returning the normalised absolute URI."""
    if len(reference) == 0:
        return base_uri
    r = _parse(reference)
    if r.scheme is not None:
        return _normalize_parts(r)
    if len(base_uri) == 0:
        return reference
    b = _parse(base_uri)
    t = _UriParts()
    if r.authority is not None:
        t.authority = r.authority
        t.path = _remove_dot_segments(r.path)
        t.query = r.query
    else:
        if r.path == "":
            t.path = b.path
            t.query = r.query if r.query is not None else b.query
        else:
            t.path = _remove_dot_segments(r.path) if r.path.startswith("/") else _remove_dot_segments(_merge(b, r.path))
            t.query = r.query
        t.authority = b.authority
    t.scheme = b.scheme
    return _normalize_parts(t) if t.scheme is not None else _format(t)


def decode_fragment(fragment: str) -> str:
    """Percent-decodes a fragment."""
    if "%" not in fragment:
        return fragment
    from urllib.parse import unquote

    try:
        return unquote(fragment, errors="strict")
    except UnicodeDecodeError:
        return fragment


def resolve_pointer(root: Any, pointer: str) -> tuple[bool, Any, list[str | int]]:
    """Resolves an RFC 6901 JSON pointer against a JSON value: (found, value, path)."""
    if pointer == "":
        return True, root, []
    if pointer[0] != "/":
        return False, None, []
    current = root
    path: list[str | int] = []
    for raw in pointer[1:].split("/"):
        token = raw.replace("~1", "/").replace("~0", "~")
        if type(current) is list:
            if _INDEX_RE.match(token) is None:
                return False, None, path
            i = int(token)
            if i >= len(current):
                return False, None, path
            current = current[i]
            path.append(i)
        elif type(current) is dict:
            if token not in current:
                return False, None, path
            current = current[token]
            path.append(token)
        else:
            return False, None, path
    return True, current, path
