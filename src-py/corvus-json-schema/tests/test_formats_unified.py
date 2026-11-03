"""The cases of the unified JSON-Schema-Test-Suite that the format checks got wrong (the descriptions are the
suite's)."""

from __future__ import annotations

import pytest

from corvus_json_schema._uts46 import uts46_map
from corvus_json_schema._uts46_data import UNICODE_VERSION
from corvus_json_schema.formats import FORMAT_VALIDATORS

CASES = [
    ("email", '"test\\\u00a9"@iana.org', False, "a non-ASCII character in a quoted pair is not valid"),
    ("email", '"\\a"@iana.org', True, "a quoted pair in the local part is valid"),
    ("idn-email", "\u0085@example.com", True, "a C1 control in the local part is valid"),
    ("idn-email", "\uffff@example.com", True, "a noncharacter in the local part is valid"),
    ("idn-hostname", "a\u200bb", True, "a zero width space is ignored by the mapping"),
    ("idn-hostname", "\uff41" * 63, True, "a label of 63 fullwidth characters is valid after mapping"),
    (
        "idn-hostname",
        "\uff58\uff4e--nxasmq6b",
        True,
        "an ACE prefix produced by the mapping step is decoded as an A-label",
    ),
    ("uri-template", "a'b", True, "an apostrophe in a literal is valid"),
    ("uri-template", "a%", False, "a literal with a lone percent sign is invalid"),
    ("uri-template", "a%4", False, "a literal with an incomplete percent-encoded triplet is invalid"),
    ("uri-template", "a%GG", False, "a literal with non-hex percent encoding is invalid"),
    ("uri-template", "a\u007fb", False, "a delete character in a literal is invalid"),
]


@pytest.mark.parametrize(("format_name", "text", "expected", "description"), CASES)
def test_formats_match_the_unified_suite(format_name: str, text: str, expected: bool, description: str) -> None:
    assert FORMAT_VALIDATORS[format_name](text) is expected, description


def test_the_uts46_mapping() -> None:
    assert UNICODE_VERSION == "17.0.0"
    assert uts46_map("a\u200bb") == "ab"
    assert uts46_map("a\u200db") == "a\u200db"
    # A name padded past the bound with ignored code points is not mapped down to a short name.
    assert FORMAT_VALIDATORS["idn-hostname"]("\u200b" * 400 + "a") is False
