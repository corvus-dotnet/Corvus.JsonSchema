"""Generates src/corvus_json_schema/_properties.py: the names a ``\\p{...}`` of a pattern may have in ECMA-262 and
the characters each one has, so that a pattern is read in the same way whatever names and whatever version of
Unicode the engine that runs it has.

    python scripts/embed_properties.py

The data is that of the Go module of this repository, src-go/corvus-json-schema/internal/ucd/tables_gen.go, which
its gen_tables.ps1 writes from the Unicode property tables of the regress crate. Every port of the evaluator reads
the same tables, so every port has the same version of Unicode (17.0.0). ECMA-262 takes the values of
General_Category, Script and Script_Extensions from PropertyValueAliases.txt, and lists the binary properties
itself (the table "Binary Unicode property aliases and their canonical property names", copied below from the 2025
edition).
"""

import re
from pathlib import Path

here = Path(__file__).resolve().parent
tables = here.parents[2] / "src-go" / "corvus-json-schema" / "internal" / "ucd" / "tables_gen.go"

BINARY = """ASCII
ASCII_Hex_Digit AHex
Alphabetic Alpha
Any
Assigned
Bidi_Control Bidi_C
Bidi_Mirrored Bidi_M
Case_Ignorable CI
Cased
Changes_When_Casefolded CWCF
Changes_When_Casemapped CWCM
Changes_When_Lowercased CWL
Changes_When_NFKC_Casefolded CWKCF
Changes_When_Titlecased CWT
Changes_When_Uppercased CWU
Dash
Default_Ignorable_Code_Point DI
Deprecated Dep
Diacritic Dia
Emoji
Emoji_Component EComp
Emoji_Modifier EMod
Emoji_Modifier_Base EBase
Emoji_Presentation EPres
Extended_Pictographic ExtPict
Extender Ext
Grapheme_Base Gr_Base
Grapheme_Extend Gr_Ext
Hex_Digit Hex
IDS_Binary_Operator IDSB
IDS_Trinary_Operator IDST
ID_Continue IDC
ID_Start IDS
Ideographic Ideo
Join_Control Join_C
Logical_Order_Exception LOE
Lowercase Lower
Math
Noncharacter_Code_Point NChar
Pattern_Syntax Pat_Syn
Pattern_White_Space Pat_WS
Quotation_Mark QMark
Radical
Regional_Indicator RI
Sentence_Terminal STerm
Soft_Dotted SD
Terminal_Punctuation Term
Unified_Ideograph UIdeo
Uppercase Upper
Variation_Selector VS
White_Space space
XID_Continue XIDC
XID_Start XIDS"""

# The values of General_Category as PropertyValueAliases.txt has them: the short name and then the other names. The
# tables have the thirty values that are not groups, and a group is the union of its values.
GENERAL_CATEGORY = """C Other
Cc Control cntrl
Cf Format
Cn Unassigned
Co Private_Use
Cs Surrogate
L Letter
LC Cased_Letter
Ll Lowercase_Letter
Lm Modifier_Letter
Lo Other_Letter
Lt Titlecase_Letter
Lu Uppercase_Letter
M Mark Combining_Mark
Mc Spacing_Mark
Me Enclosing_Mark
Mn Nonspacing_Mark
N Number
Nd Decimal_Number digit
Nl Letter_Number
No Other_Number
P Punctuation punct
Pc Connector_Punctuation
Pd Dash_Punctuation
Pe Close_Punctuation
Pf Final_Punctuation
Pi Initial_Punctuation
Po Other_Punctuation
Ps Open_Punctuation
S Symbol
Sc Currency_Symbol
Sk Modifier_Symbol
Sm Math_Symbol
So Other_Symbol
Z Separator
Zl Line_Separator
Zp Paragraph_Separator
Zs Space_Separator"""

source = tables.read_text(encoding="utf-8")
version = re.search(r'const Version = "([0-9.]+)"', source)
assert version is not None
# Each table: "var name = Table{ r16: []uint16{...}, r32: []uint32{...} }", inclusive pairs in ascending order.
ranges: dict[str, list[tuple[int, int]]] = {}
for name, body in re.findall(r"^var (\w+) = Table\{\n(.*?)^\}", source, re.M | re.S):
    numbers = [int(x, 16) for x in re.findall(r"0x([0-9A-Fa-f]+)", body)]
    assert len(numbers) % 2 == 0 and numbers == sorted(numbers), name
    ranges[name] = list(zip(numbers[::2], numbers[1::2], strict=True))


def switch(function: str) -> list[tuple[list[str], str]]:
    """The cases of one of the lookup functions: the names of each case and the table it returns."""
    body = re.search(rf"^func {function}\(name string\) \*Table \{{\n(.*?)^\}}", source, re.M | re.S)
    assert body is not None, function
    return [
        (re.findall(r'"([^"]+)"', names), table)
        for names, table in re.findall(r"case (.*?):\n\t\treturn &(\w+)", body.group(1))
    ]


def text(pairs: list[tuple[int, int]]) -> str:
    return " ".join(f"{lo:x}" if lo == hi else f"{lo:x}-{hi:x}" for lo, hi in pairs)


def constant(name: str, items: list[str], separator: str) -> str:
    out = f"{name} = (\n"
    line = ""
    for k, item in enumerate(items):
        words = item.split(" ")
        for j, word in enumerate(words):
            piece = word + (" " if j + 1 < len(words) else separator if k + 1 < len(items) else "")
            if len(line) + len(piece) > 110:
                out += f'    "{line}"\n'
                line = ""
            line += piece
    return out + f'    "{line}"\n)\n'


categories = {names[0]: table for names, table in switch("categoryTable")}
assert len(categories) == 30
binaries = {names[0]: table for names, table in switch("binaryTable")}
scripts = switch("scriptTable")
extensions = {names[0]: table for names, table in switch("scriptExtensionsTable")}
# A script as PropertyValueAliases.txt has it: the short name (the code of ISO 15924) and then the other names.
# Three scripts have no code of their own there, and for those the name is the short name.
script_names = []
for names, _ in scripts:
    code = next((n for n in names[1:] if len(n) == 4 and n not in ("Qaai", "Qaac")), names[0])
    script_names.append(" ".join([code, *[n for n in names if n != code]]))
# Two values have no table: no character has Katakana_Or_Hiragana, and Unknown is what no script has.
script_names += ["Hrkt Katakana_Or_Hiragana", "Zzzz Unknown"]
script_names.sort()

gc = [f"{short}:{text(ranges[table])}" for short, table in categories.items()]
sc = [f"{names[0]}:{text(ranges[table])}" for names, table in scripts]
# Script_Extensions is written where it is not what Script is.
scx = [f"{names[0]}:{text(ranges[extensions[names[0]]])}" for names, table in scripts if extensions[names[0]] != table]
binary_names = BINARY.split("\n")
missing = [b.split(" ")[0] for b in binary_names if b.split(" ")[0] not in binaries]
assert missing == ["ASCII", "Any", "Assigned"], missing
binary = [f"{long}:{text(ranges[table])}" for long, table in binaries.items()]

out = "# <auto-generated> by scripts/embed_properties.py from the Unicode "
out += f"{version.group(1)} tables of src-go/corvus-json-schema/internal/ucd. Do not edit.\n\n"
out += '"""The property expressions of ECMA-262: what may be written in a ``\\\\p{...}`` of a pattern with the ``u`` flag, and\n'
out += "the characters each one has.\n\n"
out += "``GENERAL_CATEGORY`` and ``SCRIPT`` have the values of those properties, with a semicolon after each value. A\n"
out += (
    "value is its short name and then its other names. ``BINARY`` has the binary properties that ECMA-262 lists, each\n"
)
out += "as its name and then its alias.\n\n"
out += "The four ``..._RANGES`` have the characters: a semicolon after each entry, and an entry is a name, a colon and the\n"
out += "ranges in hexadecimal. ``GENERAL_CATEGORY_RANGES`` has the thirty values that are not groups by their short names,\n"
out += "``SCRIPT_RANGES`` every script by its long name, ``SCRIPT_EXTENSIONS_RANGES`` the scripts whose Script_Extensions\n"
out += "is not their Script, and ``BINARY_RANGES`` the binary properties by their names. ASCII, Any and Assigned, the\n"
out += 'groups of General_Category and the scripts Unknown and Katakana_Or_Hiragana follow from the others."""\n\n'
out += f'UNICODE_VERSION = "{version.group(1)}"\n\n'
out += constant("GENERAL_CATEGORY", GENERAL_CATEGORY.split("\n"), ";") + "\n"
out += constant("SCRIPT", script_names, ";") + "\n"
out += constant("BINARY", binary_names, ";") + "\n"
out += constant("GENERAL_CATEGORY_RANGES", gc, ";") + "\n"
out += constant("SCRIPT_RANGES", sc, ";") + "\n"
out += constant("SCRIPT_EXTENSIONS_RANGES", scx, ";") + "\n"
out += constant("BINARY_RANGES", binary, ";")
(here.parent / "src" / "corvus_json_schema" / "_properties.py").write_text(out, encoding="utf-8")
print(
    f"Embedded {len(gc)} categories, {len(sc)} scripts ({len(scx)} with extensions) and {len(binary)} binary "
    f"properties of Unicode {version.group(1)}."
)
