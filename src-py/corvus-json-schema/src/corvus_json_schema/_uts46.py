"""The mapping step of UTS #46 (Unicode IDNA Compatibility Processing, section 4 step 1), which the ``idn-hostname``
and ``idn-email`` formats apply to a name before they check it.

An "ignored" code point is removed and a "mapped" one is replaced by its mapping. Every other code point is kept:
"valid" and "deviation" ones (nontransitional processing, so ZERO WIDTH JOINER and ZERO WIDTH NON-JOINER stay and meet
the contextual rules of RFC 5892), and "disallowed" ones, which the IDNA2008 checks that follow refuse. ASCII is kept
as it is: its mapping only lowercases the letters, which does not change whether a name is valid. The normalization
step (step 2) is not applied: a name that is not in Normalization Form C is valid as a host name, as the
JSON-Schema-Test-Suite expects. The table is the one the .NET engine and the Rust crate use.
"""

from __future__ import annotations

from ._uts46_data import ENTRIES, POOL

_ENTRY_SIZE = 8


def _find(c: int) -> str | None:
    """The replacement of an ignored or mapped code point outside ASCII ("" when it is ignored), or None."""
    low, high = 0, len(ENTRIES) // _ENTRY_SIZE
    while low < high:
        middle = (low + high) // 2
        at = middle * _ENTRY_SIZE
        start = (ENTRIES[at] << 16) | (ENTRIES[at + 1] << 8) | ENTRIES[at + 2]
        end = start + ((ENTRIES[at + 3] << 8) | ENTRIES[at + 4])
        if c < start:
            high = middle
        elif c > end:
            low = middle + 1
        else:
            offset = (ENTRIES[at + 5] << 8) | ENTRIES[at + 6]
            length = ENTRIES[at + 7]
            return POOL[offset : offset + length].decode("utf-8") if length else ""
    return None


def uts46_map(name: str) -> str:
    """The mapped form of a name; the name itself when nothing in it is mapped or ignored."""
    if name.isascii() or not any(ord(ch) >= 0x80 and _find(ord(ch)) is not None for ch in name):
        return name
    out = []
    for ch in name:
        replacement = _find(ord(ch)) if ord(ch) >= 0x80 else None
        out.append(ch if replacement is None else replacement)
    return "".join(out)
