// Package ucd holds the Unicode Character Database properties the module reads, for one fixed version of Unicode.
//
// Nothing in the module takes Unicode properties from the unicode package of the standard library, because that
// package follows the Go toolchain. Go 1.25 and 1.26 carry Unicode 15 and Go 1.27 carries Unicode 17. With its own
// tables the module gives the same answers whichever toolchain builds it, and the answers change only when the tables
// are generated again. Version names the version of Unicode they hold.
//
// The tables are in tables_gen.go, which gen_tables.ps1 writes. Each is a sorted list of inclusive ranges, the code
// points up to U+FFFF as 16-bit bounds and the rest as 32-bit bounds. A value that follows from other values (a
// General_Category group such as L, the Assigned property, the Unknown script) has no table of its own and is built
// when it is first asked for.
package ucd

import (
	"sort"
	"sync"
)

// MaxRune is the last Unicode code point.
const MaxRune = 0x10FFFF

// A Table is an immutable set of code points.
type Table struct {
	// r16 holds the ranges that end at or below U+FFFF and r32 the ranges that start above it, each as inclusive
	// pairs in ascending order. No two ranges of one list overlap or touch.
	r16 []uint16
	r32 []uint32
}

// Contains reports whether the table holds the code point.
func (t *Table) Contains(r rune) bool {
	if uint32(r) <= 0xFFFF {
		ranges, c := t.r16, uint16(r)
		lo, hi := 0, len(ranges)/2
		for lo < hi {
			mid := int(uint(lo+hi) >> 1)
			if c > ranges[2*mid+1] {
				lo = mid + 1
			} else if c < ranges[2*mid] {
				hi = mid
			} else {
				return true
			}
		}
		return false
	}
	ranges, c := t.r32, uint32(r)
	lo, hi := 0, len(ranges)/2
	for lo < hi {
		mid := int(uint(lo+hi) >> 1)
		if c > ranges[2*mid+1] {
			lo = mid + 1
		} else if c < ranges[2*mid] {
			hi = mid
		} else {
			return true
		}
	}
	return false
}

// AppendRanges appends the ranges of the table to dst as inclusive pairs in ascending order, and returns the
// extended slice. The pairs neither overlap nor touch.
func (t *Table) AppendRanges(dst []rune) []rune {
	if need := len(t.r16) + len(t.r32); cap(dst)-len(dst) < need {
		grown := make([]rune, len(dst), len(dst)+need)
		copy(grown, dst)
		dst = grown
	}
	for _, c := range t.r16 {
		dst = append(dst, rune(c))
	}
	high := t.r32
	// A range that crosses U+FFFF is stored in two parts.
	if len(t.r16) > 0 && len(high) > 0 && t.r16[len(t.r16)-1] == 0xFFFF && high[0] == 0x10000 {
		dst[len(dst)-1] = rune(high[1])
		high = high[2:]
	}
	for _, c := range high {
		dst = append(dst, rune(c))
	}
	return dst
}

// newTable builds a table from inclusive pairs in ascending order that neither overlap nor touch.
func newTable(pairs []rune) *Table {
	t := &Table{}
	for i := 0; i < len(pairs); i += 2 {
		lo, hi := pairs[i], pairs[i+1]
		switch {
		case hi <= 0xFFFF:
			t.r16 = append(t.r16, uint16(lo), uint16(hi))
		case lo > 0xFFFF:
			t.r32 = append(t.r32, uint32(lo), uint32(hi))
		default:
			t.r16 = append(t.r16, uint16(lo), 0xFFFF)
			t.r32 = append(t.r32, 0x10000, uint32(hi))
		}
	}
	return t
}

// Union builds the table of the code points that any of the tables holds. A caller that tests one code point
// against several properties builds their union once and searches one table.
func Union(tables ...*Table) *Table {
	var all []rune
	for _, t := range tables {
		all = t.AppendRanges(all)
	}
	n := len(all) / 2
	order := make([]int, n)
	for i := range order {
		order[i] = i
	}
	sort.Slice(order, func(a, b int) bool { return all[2*order[a]] < all[2*order[b]] })
	merged := make([]rune, 0, len(all))
	for _, i := range order {
		lo, hi := all[2*i], all[2*i+1]
		if m := len(merged); m > 0 && lo <= merged[m-1]+1 {
			if hi > merged[m-1] {
				merged[m-1] = hi
			}
			continue
		}
		merged = append(merged, lo, hi)
	}
	return newTable(merged)
}

// complement builds the table of the code points that the table does not hold.
func complement(t *Table) *Table {
	pairs := t.AppendRanges(nil)
	inverse := make([]rune, 0, len(pairs)+2)
	next := rune(0)
	for i := 0; i < len(pairs); i += 2 {
		if pairs[i] > next {
			inverse = append(inverse, next, pairs[i]-1)
		}
		next = pairs[i+1] + 1
	}
	if next <= MaxRune {
		inverse = append(inverse, next, MaxRune)
	}
	return newTable(inverse)
}

// The General_Category values that are groups of other values, the Assigned property and the Unknown script. Each is
// built on first use and kept. gen_tables.ps1 checks each definition against the source of the tables.
var (
	// Letter is the General_Category group L.
	Letter = sync.OnceValue(func() *Table { return Union(&gcLu, &gcLl, &gcLt, &gcLm, &gcLo) })
	// CasedLetter is the General_Category group LC.
	CasedLetter = sync.OnceValue(func() *Table { return Union(&gcLu, &gcLl, &gcLt) })
	// Mark is the General_Category group M.
	Mark = sync.OnceValue(func() *Table { return Union(&gcMn, &gcMc, &gcMe) })
	// Number is the General_Category group N.
	Number = sync.OnceValue(func() *Table { return Union(&gcNd, &gcNl, &gcNo) })
	// Punctuation is the General_Category group P.
	Punctuation = sync.OnceValue(func() *Table { return Union(&gcPc, &gcPd, &gcPs, &gcPe, &gcPi, &gcPf, &gcPo) })
	// Symbol is the General_Category group S.
	Symbol = sync.OnceValue(func() *Table { return Union(&gcSm, &gcSc, &gcSk, &gcSo) })
	// Separator is the General_Category group Z.
	Separator = sync.OnceValue(func() *Table { return Union(&gcZs, &gcZl, &gcZp) })
	// Other is the General_Category group C.
	Other = sync.OnceValue(func() *Table { return Union(&gcCc, &gcCf, &gcCs, &gcCo, &gcCn) })
	// Assigned is the code points that have a General_Category other than Cn.
	Assigned = sync.OnceValue(func() *Table { return complement(&gcCn) })
	// UnknownScript is the code points that belong to no script. They are the unassigned, private use and surrogate
	// code points.
	UnknownScript = sync.OnceValue(func() *Table { return Union(&gcCn, &gcCo, &gcCs) })
)

// Category returns the table of a General_Category value by its short name, such as "Lu" or "L". It returns nil for
// any other name.
func Category(name string) *Table {
	switch name {
	case "L":
		return Letter()
	case "LC":
		return CasedLetter()
	case "M":
		return Mark()
	case "N":
		return Number()
	case "P":
		return Punctuation()
	case "S":
		return Symbol()
	case "Z":
		return Separator()
	case "C":
		return Other()
	}
	return categoryTable(name)
}

// Binary returns the table of a binary property by its canonical name, such as "Alphabetic" or "White_Space". It
// returns nil for any other name. ASCII and Any, which ECMA-262 adds to the properties of Unicode, are not here.
func Binary(name string) *Table {
	if name == "Assigned" {
		return Assigned()
	}
	return binaryTable(name)
}

// Script returns the table of a Script value by its long name or an alias, such as "Greek" or "Grek". It returns nil
// for any other name.
func Script(name string) *Table {
	if name == "Unknown" || name == "Zzzz" {
		return UnknownScript()
	}
	return scriptTable(name)
}

// ScriptExtensions returns the table of a Script_Extensions value by the long name of the script or an alias. It
// returns nil for any other name.
func ScriptExtensions(name string) *Table {
	if name == "Unknown" || name == "Zzzz" {
		return UnknownScript()
	}
	return scriptExtensionsTable(name)
}

// ScriptNames returns the names of every script. Each element is the long name of one script followed by its
// aliases. The caller must not change it.
func ScriptNames() [][]string { return scriptNames }

// BinaryNames returns the canonical name of every binary property that has a table, which is every name Binary knows
// but Assigned. The caller must not change it.
func BinaryNames() []string { return binaryNames }

// A caseRange maps some of the code points first, first+1, ... first+length-1 to themselves plus delta. The ones it
// maps are those whose offset from first has none of the bits of mask, so a mask of 1 takes every other code point.
type caseRange struct {
	first  rune
	delta  int32
	length uint16
	mask   uint16
}

func mapCase(table []caseRange, r rune) rune {
	lo, hi := 0, len(table)
	for lo < hi {
		mid := int(uint(lo+hi) >> 1)
		if row := &table[mid]; r < row.first {
			hi = mid
		} else if offset := r - row.first; offset >= rune(row.length) {
			lo = mid + 1
		} else if offset&rune(row.mask) == 0 {
			return r + row.delta
		} else {
			return r
		}
	}
	return r
}

// Fold returns the simple case folding of a code point (the C and S rows of CaseFolding.txt), which is the code
// point itself when it has none. Two code points are equal ignoring case when Fold gives the same result for both.
func Fold(r rune) rune {
	if r < 0x80 {
		if r >= 'A' && r <= 'Z' {
			return r + ('a' - 'A')
		}
		return r
	}
	return mapCase(caseFolds, r)
}

// ToUpper returns the simple uppercase mapping of a code point, which is the code point itself when it has none.
func ToUpper(r rune) rune {
	if r < 0x80 {
		if r >= 'a' && r <= 'z' {
			return r - ('a' - 'A')
		}
		return r
	}
	return mapCase(upperCases, r)
}

// AppendFolding appends every code point that Fold changes, in ascending order, and returns the extended slice.
func AppendFolding(dst []rune) []rune {
	for i := range caseFolds {
		row := &caseFolds[i]
		for offset := rune(0); offset < rune(row.length); offset++ {
			if offset&rune(row.mask) == 0 && row.delta != 0 {
				dst = append(dst, row.first+offset)
			}
		}
	}
	return dst
}
