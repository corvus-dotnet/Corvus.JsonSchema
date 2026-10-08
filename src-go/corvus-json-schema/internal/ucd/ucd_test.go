package ucd

import (
	"testing"
	"unicode"
)

// everyTable returns every table the package can name, by a label.
func everyTable() map[string]*Table {
	tables := map[string]*Table{}
	for _, name := range []string{
		"Lu", "Ll", "Lt", "Lm", "Lo", "Mn", "Mc", "Me", "Nd", "Nl", "No", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po", "Sm",
		"Sc", "Sk", "So", "Zs", "Zl", "Zp", "Cc", "Cf", "Cs", "Co", "Cn", "L", "LC", "M", "N", "P", "S", "Z", "C",
	} {
		tables["gc="+name] = Category(name)
	}
	for _, name := range BinaryNames() {
		tables[name] = Binary(name)
	}
	tables["Assigned"] = Binary("Assigned")
	for _, names := range ScriptNames() {
		for _, name := range names {
			tables["sc="+name] = Script(name)
			tables["scx="+name] = ScriptExtensions(name)
		}
	}
	return tables
}

// TestTablesAreWellFormed checks the invariants Contains and AppendRanges rely on.
func TestTablesAreWellFormed(t *testing.T) {
	tables := everyTable()
	for label, table := range tables {
		if table == nil {
			t.Errorf("%s has no table", label)
			continue
		}
		if len(table.r16)%2 != 0 || len(table.r32)%2 != 0 {
			t.Errorf("%s has an odd number of bounds", label)
			continue
		}
		for i := 0; i < len(table.r16); i += 2 {
			if table.r16[i] > table.r16[i+1] || i > 0 && uint32(table.r16[i]) <= uint32(table.r16[i-1])+1 {
				t.Errorf("%s: the 16-bit range at %d is out of order or touches the one before", label, i)
			}
		}
		for i := 0; i < len(table.r32); i += 2 {
			if table.r32[i] < 0x10000 || table.r32[i] > table.r32[i+1] || table.r32[i+1] > MaxRune ||
				i > 0 && table.r32[i] <= table.r32[i-1]+1 {
				t.Errorf("%s: the 32-bit range at %d is out of order, out of bounds or touches the one before", label, i)
			}
		}
		pairs := table.AppendRanges(nil)
		for i := 0; i < len(pairs); i += 2 {
			if pairs[i] > pairs[i+1] || i > 0 && pairs[i] <= pairs[i-1]+1 {
				t.Errorf("%s: AppendRanges gives a range at %d that is out of order or touches the one before", label, i)
			}
		}
	}
	t.Logf("%d names resolve", len(tables))
}

// TestContainsAgreesWithTheRanges walks every code point for tables of each shape.
func TestContainsAgreesWithTheRanges(t *testing.T) {
	for _, table := range []*Table{
		Category("Lu"), Category("Cn"), Category("Co"), Letter(), Assigned(), UnknownScript(), Binary("Alphabetic"),
		Binary("Noncharacter_Code_Point"), Script("Han"), ScriptExtensions("Latin"), Script("Adlam"), {},
	} {
		pairs := table.AppendRanges(nil)
		next := 0
		for r := rune(0); r <= MaxRune; r++ {
			for next < len(pairs) && pairs[next+1] < r {
				next += 2
			}
			want := next < len(pairs) && pairs[next] <= r
			if table.Contains(r) != want {
				t.Fatalf("Contains(U+%04X) = %v, and the ranges say %v", r, !want, want)
			}
		}
		if table.Contains(-1) || table.Contains(MaxRune+1) {
			t.Error("Contains accepts a value that is not a code point")
		}
	}
}

// TestGeneralCategoriesPartitionTheCodePoints checks that every code point has exactly one General_Category, that the
// groups hold what their members hold, and that Assigned and the Unknown script follow from the categories.
func TestGeneralCategoriesPartitionTheCodePoints(t *testing.T) {
	groups := map[string][]string{
		"L": {"Lu", "Ll", "Lt", "Lm", "Lo"}, "LC": {"Lu", "Ll", "Lt"}, "M": {"Mn", "Mc", "Me"}, "N": {"Nd", "Nl", "No"},
		"P": {"Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po"}, "S": {"Sm", "Sc", "Sk", "So"}, "Z": {"Zs", "Zl", "Zp"},
		"C": {"Cc", "Cf", "Cs", "Co", "Cn"},
	}
	var scripts []*Table
	for _, names := range ScriptNames() {
		if names[0] != "Unknown" {
			scripts = append(scripts, Script(names[0]))
		}
	}
	for r := rune(0); r <= MaxRune; r++ {
		count := 0
		for group, members := range groups {
			in := false
			for _, member := range members {
				in = in || Category(member).Contains(r)
			}
			if group != "LC" && in {
				count++
			}
			if Category(group).Contains(r) != in {
				t.Fatalf("U+%04X: the group %s and its members disagree", r, group)
			}
		}
		if count != 1 {
			t.Fatalf("U+%04X is in %d General_Category groups", r, count)
		}
		if Assigned().Contains(r) == Category("Cn").Contains(r) {
			t.Fatalf("U+%04X: Assigned is not the complement of Cn", r)
		}
		inScript := 0
		for _, script := range scripts {
			if script.Contains(r) {
				inScript++
			}
		}
		if unknown := UnknownScript().Contains(r); inScript > 1 || (inScript == 0) != unknown {
			t.Fatalf("U+%04X is in %d scripts, and Unknown says %v", r, inScript, unknown)
		}
	}
}

// TestTheDataIsUnicode17 checks code points whose properties older versions of Unicode do not have. The Go toolchains
// before 1.27 carry Unicode 15, so these fail if a table is ever taken from the unicode package again.
func TestTheDataIsUnicode17(t *testing.T) {
	if Version != "17.0.0" {
		t.Fatalf("Version = %q. Update this test for the new data.", Version)
	}
	for _, c := range []struct {
		table *Table
		r     rune
		want  bool
		what  string
	}{
		{Script("Garay"), 0x10D40, true, "Garay is a script of Unicode 16"},
		{Script("Gara"), 0x10D40, true, "Gara is the code of Garay"},
		{Script("Sidetic"), 0x10940, true, "Sidetic is a script of Unicode 17"},
		{Script("Beria_Erfe"), 0x16EA0, true, "Beria Erfe is a script of Unicode 17"},
		{Category("Lu"), 0x16EA0, true, "U+16EA0 is an uppercase letter of Unicode 17"},
		{Category("Cn"), 0x16EA0, false, "U+16EA0 is assigned in Unicode 17"},
		{Category("Cn"), 0x1FAE9, false, "U+1FAE9 is assigned in Unicode 16"},
		{Binary("Emoji"), 0x1FAE9, true, "U+1FAE9 is an emoji of Unicode 16"},
		{Script("Han"), 0x323B0, true, "U+323B0 starts CJK extension J of Unicode 17"},
		{UnknownScript(), 0x10D40, false, "U+10D40 has a script"},
		{Assigned(), 0x10FFFF, false, "U+10FFFF is a noncharacter"},
	} {
		if c.table == nil {
			t.Errorf("no table: %s", c.what)
		} else if got := c.table.Contains(c.r); got != c.want {
			t.Errorf("Contains(U+%04X) = %v: %s", c.r, got, c.what)
		}
	}
	// Case pairs of Unicode 16 and 17.
	for _, c := range [][2]rune{{0x10D50, 0x10D70}, {0x16EA0, 0x16EBB}, {0xA7CB, 0x264}, {0x1C89, 0x1C8A}} {
		if got := Fold(c[0]); got != c[1] {
			t.Errorf("Fold(U+%04X) = U+%04X, want U+%04X", c[0], got, c[1])
		}
		if got := ToUpper(c[1]); got != c[0] {
			t.Errorf("ToUpper(U+%04X) = U+%04X, want U+%04X", c[1], got, c[0])
		}
	}
	for _, name := range []string{"Nope", "", "l", "Latn=", "Any", "ASCII"} {
		if Category(name) != nil || Script(name) != nil || ScriptExtensions(name) != nil || Binary(name) != nil {
			t.Errorf("%q names a table", name)
		}
	}
}

// TestCaseMappings checks Fold and ToUpper over every code point.
func TestCaseMappings(t *testing.T) {
	folding := AppendFolding(nil)
	next := 0
	for r := rune(0); r <= MaxRune; r++ {
		folded := Fold(r)
		if Fold(folded) != folded {
			t.Fatalf("Fold is not idempotent at U+%04X", r)
		}
		changes := next < len(folding) && folding[next] == r
		if changes {
			next++
		}
		if changes != (folded != r) {
			t.Fatalf("AppendFolding and Fold disagree at U+%04X", r)
		}
		if r < 0x80 {
			if folded != mapCase(caseFolds, r) || ToUpper(r) != mapCase(upperCases, r) {
				t.Fatalf("the ASCII path and the table disagree at U+%04X", r)
			}
		}
	}
	for _, c := range [][2]rune{
		{'A', 'a'}, {'z', 'z'}, {0xB5, 0x3BC}, {0x17F, 's'}, {0x212A, 'k'}, {0x1E9E, 0xDF}, {0x3C2, 0x3C3}, {0x130, 0x130},
		{0x1F88, 0x1F80}, {0x1E921, 0x1E943},
	} {
		if got := Fold(c[0]); got != c[1] {
			t.Errorf("Fold(U+%04X) = U+%04X, want U+%04X", c[0], got, c[1])
		}
	}
	for _, c := range [][2]rune{{'a', 'A'}, {0xDF, 0xDF}, {0xFF, 0x178}, {0x131, 'I'}, {0x17F, 'S'}, {0x1C5, 0x1C4}} {
		if got := ToUpper(c[0]); got != c[1] {
			t.Errorf("ToUpper(U+%04X) = U+%04X, want U+%04X", c[0], got, c[1])
		}
	}
}

// TestAgreesWithTheToolchainOfTheSameVersion compares every table with the unicode package when the toolchain
// happens to carry the same version of Unicode. This test file may read the unicode package. No other file of the
// module may, and TestNoToolchainUnicodeData of the main package checks that.
func TestAgreesWithTheToolchainOfTheSameVersion(t *testing.T) {
	if unicode.Version != Version {
		t.Skipf("the toolchain carries Unicode %s and the tables hold Unicode %s", unicode.Version, Version)
	}
	compare := func(label string, std *unicode.RangeTable, table *Table) {
		if table == nil {
			t.Errorf("%s has no table", label)
			return
		}
		for r := rune(0); r <= MaxRune; r++ {
			if unicode.Is(std, r) != table.Contains(r) {
				t.Errorf("%s differs from the unicode package at U+%04X", label, r)
				return
			}
		}
	}
	for name, std := range unicode.Categories {
		compare("gc="+name, std, Category(name))
	}
	for name, std := range unicode.Scripts {
		compare("sc="+name, std, Script(name))
	}
	compared := 0
	for name, std := range unicode.Properties {
		// The unicode package also has contributory properties, which ECMA-262 does not list.
		if table := Binary(name); table != nil {
			compare(name, std, table)
			compared++
		}
	}
	if compared < 20 {
		t.Errorf("only %d binary properties were compared", compared)
	}
	for r := rune(0); r <= MaxRune; r++ {
		if unicode.ToUpper(r) != ToUpper(r) {
			t.Fatalf("ToUpper(U+%04X) differs from the unicode package", r)
		}
		for next := unicode.SimpleFold(r); next != r; next = unicode.SimpleFold(next) {
			if Fold(next) != Fold(r) {
				t.Fatalf("U+%04X and U+%04X fold together in the unicode package and apart here", r, next)
			}
		}
		if folded := Fold(r); folded != r {
			together := false
			for next := unicode.SimpleFold(r); next != r; next = unicode.SimpleFold(next) {
				together = together || next == folded
			}
			if !together {
				t.Fatalf("U+%04X folds to U+%04X here and not in the unicode package", r, folded)
			}
		}
	}
}

func BenchmarkContains(b *testing.B) {
	text := []rune("Ελληνικά кириллица 日本語のテキスト العربية naïve café ÉCOLE ৪২ 12345 🐲")
	letter := Letter()
	b.ReportAllocs()
	for i := 0; i < b.N; i++ {
		for _, r := range text {
			letter.Contains(r)
		}
	}
}
