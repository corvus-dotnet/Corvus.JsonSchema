"""Writes lib/JSON/Schema/Corvus/Regex/Unicode.pm from the Go port's Unicode tables
(src-go/corvus-json-schema/internal/ucd/tables_gen.go), so that every port matches with the same version of Unicode,
whatever version the Perl that runs it carries.

    python3 tools/gen_unicode.py ../../src-go/corvus-json-schema/internal/ucd/tables_gen.go lib/JSON/Schema/Corvus/Regex/Unicode.pm
"""

import re
import sys

source = open(sys.argv[1], encoding="utf-8").read()
version = re.search(r'const Version = "([^"]+)"', source).group(1)

# Tables: var NAME = Table{ r16: []uint16{...}, r32: []uint32{...}, }
tables = {}
for m in re.finditer(r"^var (\w+) = Table\{\n(.*?)^\}", source, re.S | re.M):
    body = m.group(2)
    pairs = []
    for part in re.finditer(r"r(16|32): \[\]uint(?:16|32)\{(.*?)\}", body, re.S):
        pairs += [int(x, 16) for x in re.findall(r"0x[0-9A-Fa-f]+", part.group(2))]
    # A range that crosses U+FFFF is stored in two parts: join them.
    joined = []
    for i in range(0, len(pairs), 2):
        lo, hi = pairs[i], pairs[i + 1]
        if joined and joined[-1] == 0xFFFF and lo == 0x10000:
            joined[-1] = hi
        else:
            joined += [lo, hi]
    tables[m.group(1)] = joined

def switch(function):
    body = re.search(r"^func " + function + r"\(name string\) \*Table \{\n(.*?)^\}", source, re.S | re.M).group(1)
    out = {}
    for m in re.finditer(r"case ((?:\"[^\"]+\",? ?)+):\n\t\treturn &(\w+)", body):
        for name in re.findall(r"\"([^\"]+)\"", m.group(1)):
            out[name] = m.group(2)
    return out

categories = switch("categoryTable")
binaries = switch("binaryTable")
scripts = switch("scriptTable")
script_extensions = switch("scriptExtensionsTable")

def case_rows(name):
    body = re.search(r"^var " + name + r" = \[\]caseRange\{\n(.*?)^\}", source, re.S | re.M).group(1)
    return [tuple(int(x, 0) for x in m.groups()) for m in re.finditer(r"\{(0x[0-9A-Fa-f]+|\d+), (-?\d+), (\d+), (\d+)\}", body)]

folds = case_rows("caseFolds")
uppers = case_rows("upperCases")

used = set(categories.values()) | set(binaries.values()) | set(scripts.values()) | set(script_extensions.values())

def hexes(values):
    return " ".join("%x" % v for v in values)

def wrap(text, width=110):
    words = text.split(" ")
    lines, line = [], ""
    for w in words:
        if line and len(line) + 1 + len(w) > width:
            lines.append(line)
            line = w
        else:
            line = w if not line else line + " " + w
    if line:
        lines.append(line)
    return lines

out = [
    "package JSON::Schema::Corvus::Regex::Unicode;",
    "",
    "# Written by tools/gen_unicode.py from the Go port's tables (src-go/corvus-json-schema/internal/ucd/tables_gen.go),",
    f"# Unicode {version}. Do not edit.",
    "#",
    "# Each table is a string of hexadecimal bounds, inclusive pairs in ascending order, that ranges() reads on first use.",
    "",
    "use 5.026;",
    "use strict;",
    "use warnings;",
    "",
    f"our $VERSION_OF_UNICODE = '{version}';",
    "",
    "our %TABLES = (",
]
for name in sorted(used):
    lines = wrap(hexes(tables[name]))
    out.append(f"  {name} =>")
    for i, line in enumerate(lines):
        out.append("    " + ("q{" if i == 0 else "  ") + line + ("}," if i == len(lines) - 1 else ""))
    if not lines:
        out.append("    q{},")
out.append(");")
out.append("")
for var, mapping in (("CATEGORY", categories), ("BINARY", binaries), ("SCRIPT", scripts), ("SCRIPT_EXTENSIONS", script_extensions)):
    out.append(f"our %{var} = (")
    items = [f"'{k}' => '{v}'," for k, v in mapping.items()]
    for line in wrap(" ".join(items)):
        out.append("  " + line)
    out.append(");")
    out.append("")
for var, rows in (("CASE_FOLDS", folds), ("UPPER_CASES", uppers)):
    out.append(f"# [first, delta, length, mask]: the delta applies to the code points whose offset has none of the mask's bits.")
    out.append(f"our @{var} = (")
    items = [f"[{a}, {b}, {c}, {d}]," for a, b, c, d in rows]
    for line in wrap(" ".join(items)):
        out.append("  " + line)
    out.append(");")
    out.append("")
out += [
    "my %RANGES;",
    "",
    "# The ranges of a table, as a list of inclusive pairs.",
    "sub ranges {",
    "  my ($name) = @_;",
    "  return $RANGES{$name} //= [ map { hex } split ' ', $TABLES{$name} // die \"no Unicode table $name\" ];",
    "}",
    "",
    "1;",
    "",
]
open(sys.argv[2], "w", encoding="utf-8").write("\n".join(out))
print(f"Unicode {version}: {len(used)} tables, {len(folds)} folding rows, {len(uppers)} uppercase rows")
