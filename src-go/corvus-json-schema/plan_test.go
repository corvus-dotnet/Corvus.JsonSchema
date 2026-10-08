package jsonschema

import (
	"fmt"
	"slices"
	"strings"
	"testing"
	"unicode/utf8"
)

func TestMeetTreatsIntegersAsNumbers(t *testing.T) {
	for _, c := range []struct{ a, b, want uint8 }{
		{typeInteger, typeNumber, typeInteger},
		{typeNumber, typeNumber | typeString, typeNumber | typeInteger},
		{anyType, typeString | typeArray, typeString | typeArray},
		{typeString, typeInteger, 0},
	} {
		if got := meetTypes(c.a, c.b); got != c.want {
			t.Errorf("meet(%#x, %#x) = %#x, want %#x", c.a, c.b, got, c.want)
		}
	}
	for _, json := range []string{`1`, `1.5`, `"a"`} {
		d := mustParse(t, json)
		for _, c := range [][2]uint8{
			{typeInteger, typeNumber}, {typeNumber, typeInteger}, {typeNumber | typeString, typeInteger | typeString},
			{anyType, typeNumber},
		} {
			if got, want := typeOK(meetTypes(c[0], c[1]), d, d.root), typeOK(c[0], d, d.root) && typeOK(c[1], d, d.root); got != want {
				t.Errorf("%#x and %#x on %s: %v, want %v", c[0], c[1], json, got, want)
			}
		}
	}
}

func TestNameMapFindsEveryName(t *testing.T) {
	var list []string
	for i := 0; i < 200; i++ {
		list = append(list, fmt.Sprintf("p%d%s", i, strings.Repeat("x", i%13)))
	}
	list = append(list, "", "a", "ab", "abc", "abcd", "abcdefgh", "abcdefghi", "é", "日本", strings.Repeat("y", 300),
		"abcdefghijkl", "abcdefghijkm", "abcdefghXjkl",
		// Longer than sixteen bytes with the same first and last eight: only the text tells them apart.
		"aaaaaaaa-1-bbbbbbbb", "aaaaaaaa-2-bbbbbbbb", "aaaaaaaa-3-bbbbbbbb")
	find := func(m *nameMap, name string) int { return m.find([]byte(name), nameWord([]byte(name))) }
	m := newNameMap(list)
	for i, n := range list {
		if got := find(&m, n); got != i {
			t.Errorf("find(%q) = %d, want %d", n, got, i)
		}
	}
	for _, miss := range []string{
		"p", "q1", "p1000", "p0x", "b", "abce", "abcdefgj", "abcdefghj", "è", "y", strings.Repeat("y", 299),
		"abcdefghijkn", "abcdefghXjkm", "aaaaaaaa-4-bbbbbbbb", strings.Repeat("y", 150) + "z" + strings.Repeat("y", 149),
	} {
		if got := find(&m, miss); got != -1 {
			t.Errorf("find(%q) = %d", miss, got)
		}
	}
	for _, few := range [][]string{
		{}, {"a"}, {"ab", "ba"}, {"alpha", "gamma", "delta", "omega"}, {"abcdefghij", "abcdefghik"}, {"twice", "twice", "once"},
	} {
		m := newNameMap(few)
		for i, n := range few {
			if got := find(&m, n); got != slices.Index(few, n) {
				t.Errorf("%v: find(%q) = %d, want %d", few, n, got, i)
			}
		}
		if got := find(&m, "zeta!"); got != -1 {
			t.Errorf("%v: find(zeta!) = %d", few, got)
		}
	}
}

func TestNamesFollowDeclaredOrSortedOrder(t *testing.T) {
	declared := []string{"name", "version", "repository", "alias"}
	ns := newNames(declared)
	// Every order of the names, from every starting hint, finds each one.
	for _, order := range [][]string{
		{"name", "version", "repository", "alias"}, {"alias", "name", "repository", "version"},
		{"version", "alias", "name", "repository"}, {"repository", "repository", "name"},
	} {
		for start := 0; start <= len(declared); start++ {
			hint := start
			for _, n := range order {
				var got int
				if got, hint = ns.findFrom([]byte(n), hint); got != slices.Index(declared, n) {
					t.Errorf("%s in %v from %d: %d", n, order, start, got)
				}
			}
		}
	}
	if got, _ := ns.findFrom([]byte("other"), 0); got != -1 {
		t.Errorf("other: %d", got)
	}
	if got, _ := ns.findFrom([]byte("names"), 2); got != -1 {
		t.Errorf("names: %d", got)
	}
	// Sorted successors: after "name" (index 0) comes "repository" (2), after it "version" (1), then none.
	if want := []uint32{3, 2, noName, 1, 0}; !slices.Equal(ns.sortedNext, want) {
		t.Errorf("sortedNext = %v, want %v", ns.sortedNext, want)
	}
	many := make([]string, 40)
	for i := range many {
		many[i] = fmt.Sprintf("property-number-%02d", (i*7)%40)
	}
	large := newNames(many)
	for start := 0; start <= len(many); start += 13 {
		for i, n := range many {
			if got, _ := large.findFrom([]byte(n), start); got != i {
				t.Errorf("%s from %d: %d, want %d", n, start, got, i)
			}
		}
		if got, _ := large.findFrom([]byte("property-number-40"), start); got != -1 {
			t.Errorf("property-number-40 from %d: %d", start, got)
		}
	}
}

func TestLinearNamesFindFromAnyHint(t *testing.T) {
	ns := newNames([]string{"a", "b", "c"})
	for start := 0; start < 3; start++ {
		for i, n := range []string{"a", "b", "c"} {
			if got, _ := ns.findFrom([]byte(n), start); got != i {
				t.Errorf("%s from %d: %d", n, start, got)
			}
		}
		if got, _ := ns.findFrom([]byte("d"), start); got != -1 {
			t.Errorf("d from %d: %d", start, got)
		}
	}
}

// A plan of a few names looks each one up in a small object (visitLookup): every name is found by its length and
// word, the text deciding beyond eight bytes, and a name that is not there is not.
func TestPropertyLookupByName(t *testing.T) {
	const instance = `{"a": 1, "ab": 2, "abcdefgh": 3, "abcdefghi": 4, "abcdefghj": 5, "é": 6, "": 7, "a\nb": 8}`
	d := mustParse(t, instance)
	quote := func(name string) string { return string(appendQuoted(nil, []byte(name))) }
	for name, want := range map[string]string{
		"a": "1", "ab": "2", "abcdefgh": "3", "abcdefghi": "4", "abcdefghj": "5", "é": "6", "": "7", "a\nb": "8",
	} {
		if v := d.property(d.root, name); v < 0 || string(d.numberText(v)) != want {
			t.Errorf("property %q: %d", name, v)
		}
		for value, valid := range map[string]bool{want: true, "0": false} {
			schema := `{"properties": {` + quote(name) + `: {"const": ` + value + `}}, "required": [` + quote(name) + `]}`
			v, err := CompileString(schema)
			if err != nil {
				t.Fatalf("%s: %v", schema, err)
			}
			if pl := v.program.plans[v.program.entry].body.object; pl == nil || !pl.lookup {
				t.Fatalf("%s: not a lookup plan", schema)
			}
			if got := v.IsValid(d); got != valid {
				t.Errorf("%s: %v, want %v", schema, got, valid)
			}
		}
	}
	for _, miss := range []string{"b", "ba", "abcdefgi", "abcdefghk", "abcdefgh ", "è"} {
		if d.property(d.root, miss) != -1 {
			t.Errorf("property %q found", miss)
		}
		// Found, the property would fail its schema. Required, it is missed.
		for schema, valid := range map[string]bool{
			`{"properties": {` + quote(miss) + `: false}}`:                                   true,
			`{"properties": {` + quote(miss) + `: true}, "required": [` + quote(miss) + `]}`: false,
		} {
			v, err := CompileString(schema)
			if err != nil {
				t.Fatalf("%s: %v", schema, err)
			}
			if got := v.IsValid(d); got != valid {
				t.Errorf("%s: %v, want %v", schema, got, valid)
			}
		}
	}
}

func TestLengthOKAgreesWithCounting(t *testing.T) {
	for _, s := range []string{"", "a", "abcd", "é", "éé", "😀😀", "a😀b"} {
		d := mustParse(t, `"`+s+`"`)
		chars := uint64(utf8.RuneCountInString(s))
		for min := uint64(0); min < 6; min++ {
			for max := uint64(0); max < 6; max++ {
				if got, want := lengthOK(d, d.root, min, max), chars >= min && chars <= max; got != want {
					t.Errorf("%q in [%d, %d]: %v", s, min, max, got)
				}
			}
		}
	}
}
