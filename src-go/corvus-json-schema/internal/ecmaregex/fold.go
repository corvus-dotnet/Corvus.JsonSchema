package ecmaregex

import (
	"sort"
	"sync"

	"github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema/internal/ucd"
)

// Case-insensitive matching exists only inside a modifier group such as (?i:...). ECMA-262 defines it through a
// Canonicalize function. Two characters match when they canonicalize to the same character. The function differs
// between the two grammars. With the u flag it is Unicode simple case folding. With no flag it is the single
// character uppercase mapping of a UTF-16 code unit, which never maps a character outside ASCII into ASCII.
//
// The parser applies the equivalence to every literal and class as it reads them, so both back ends match a
// case-insensitive group with plain classes. Only a backreference needs the function when matching.
//
// The folding and the uppercase mapping come from the tables of the ucd package, never from the unicode package of
// the Go toolchain, so a pattern matches the same texts whichever toolchain built the program.

// multiUpper holds the characters of the Basic Multilingual Plane whose uppercase form is more than one character,
// as inclusive pairs. Canonicalize leaves them alone.
var multiUpper = newCharSet([]rune{
	0xDF, 0xDF, 0x149, 0x149, 0x1F0, 0x1F0, 0x390, 0x390, 0x3B0, 0x3B0, 0x587, 0x587, 0x1E96, 0x1E9A, 0x1F50, 0x1F50,
	0x1F52, 0x1F52, 0x1F54, 0x1F54, 0x1F56, 0x1F56, 0x1F80, 0x1FAF, 0x1FB2, 0x1FB4, 0x1FB6, 0x1FB7, 0x1FBC, 0x1FBC,
	0x1FC2, 0x1FC4, 0x1FC6, 0x1FC7, 0x1FCC, 0x1FCC, 0x1FD2, 0x1FD3, 0x1FD6, 0x1FD7, 0x1FE2, 0x1FE4, 0x1FE6, 0x1FE7,
	0x1FF2, 0x1FF4, 0x1FF6, 0x1FF7, 0x1FFC, 0x1FFC, 0xFB00, 0xFB06, 0xFB13, 0xFB17,
}, false)

// canonicalize is the Canonicalize function of ECMA-262 for case-insensitive matching.
func canonicalize(r rune, unicodeMode bool) rune {
	if unicodeMode {
		// Every character of a simple case folding class folds to the same character.
		return ucd.Fold(r)
	}
	// A character beyond the Basic Multilingual Plane is two code units, and neither has an uppercase form.
	if r > 0xFFFF || multiUpper.contains(r) {
		return r
	}
	upper := ucd.ToUpper(r)
	if r >= 128 && upper < 128 || upper > 0xFFFF {
		return r
	}
	return upper
}

// A foldTable lists the characters that are equivalent to some other character, each with all the members of its
// equivalence class.
type foldTable struct {
	points  []rune
	classes [][]rune
}

var (
	unicodeFoldOnce, legacyFoldOnce   sync.Once
	unicodeFoldTable, legacyFoldTable *foldTable
)

func newFoldTable(classes map[rune][]rune) *foldTable {
	t := &foldTable{}
	for _, class := range classes {
		if len(class) < 2 {
			continue
		}
		for _, member := range class {
			t.points = append(t.points, member)
		}
	}
	sort.Slice(t.points, func(a, b int) bool { return t.points[a] < t.points[b] })
	byMember := map[rune][]rune{}
	for _, class := range classes {
		for _, member := range class {
			byMember[member] = class
		}
	}
	t.classes = make([][]rune, len(t.points))
	for i, point := range t.points {
		t.classes[i] = byMember[point]
	}
	return t
}

// foldClasses returns the table of the grammar in use. It is built on first use, which only a pattern with a
// case-insensitive group reaches.
func foldClasses(unicodeMode bool) *foldTable {
	if unicodeMode {
		unicodeFoldOnce.Do(func() {
			// A class is a character that others fold to, with those others.
			classes := map[rune][]rune{}
			for _, r := range ucd.AppendFolding(nil) {
				key := ucd.Fold(r)
				if _, seen := classes[key]; !seen {
					classes[key] = []rune{key}
				}
				classes[key] = append(classes[key], r)
			}
			unicodeFoldTable = newFoldTable(classes)
		})
		return unicodeFoldTable
	}
	legacyFoldOnce.Do(func() {
		classes := map[rune][]rune{}
		for r := rune(0); r <= 0xFFFF; r++ {
			key := canonicalize(r, false)
			classes[key] = append(classes[key], r)
		}
		legacyFoldTable = newFoldTable(classes)
	})
	return legacyFoldTable
}

// foldClosure returns the set of every character equivalent to some member of the set.
func foldClosure(set *charSet, unicodeMode bool) *charSet {
	table := foldClasses(unicodeMode)
	var extra []rune
	for i := 0; i < len(set.ranges); i += 2 {
		lo, hi := set.ranges[i], set.ranges[i+1]
		k := sort.Search(len(table.points), func(k int) bool { return table.points[k] >= lo })
		for ; k < len(table.points) && table.points[k] <= hi; k++ {
			for _, member := range table.classes[k] {
				if !set.contains(member) {
					extra = append(extra, member, member)
				}
			}
		}
	}
	if len(extra) == 0 {
		return set
	}
	return newCharSet(append(extra, set.ranges...), false)
}

// With the u flag and case-insensitive matching, \w and \b also count the two characters that fold to an ASCII
// letter (U+017F, the long s, and U+212A, the Kelvin sign) as word characters.
var foldWordPairs = []rune{'0', '9', 'A', 'Z', '_', '_', 'a', 'z', 0x17F, 0x17F, 0x212A, 0x212A}

var (
	foldWordSet    = newCharSet(foldWordPairs, false)
	notFoldWordSet = newCharSet(foldWordPairs, true)
)

func isFoldWordRune(r rune) bool {
	return r < 128 && isWordByte(byte(r)) || r == 0x17F || r == 0x212A
}
