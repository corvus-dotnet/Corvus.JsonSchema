package jsonschema

import (
	"encoding/binary"
	"sort"
)

// Property name lookup for the fail-fast plans.

// nameWord is a word that tells names of one length apart cheaply. For a name of at most eight bytes it is unique
// among names of the same length: the first and last four bytes (overlapping, so every byte is in one of them), or
// for shorter names the first, middle and last byte. For a longer name it is the first eight bytes, so names with
// different words differ, and names with the same word are compared in full.
func nameWord(b []byte) uint64 {
	n := len(b)
	if n > 8 {
		return binary.LittleEndian.Uint64(b)
	}
	if n >= 4 {
		return uint64(binary.LittleEndian.Uint32(b)) | uint64(binary.LittleEndian.Uint32(b[n-4:]))<<32
	}
	if n > 0 {
		return uint64(b[0]) | uint64(b[n/2])<<8 | uint64(b[n-1])<<16
	}
	return 0
}

const (
	// At most this many names of one length are compared in turn. More get a table on their most distinguishing
	// byte.
	maxCandidates = 3
	// Names longer than this are compared in turn (no entry by length).
	maxIndexedLength = 128
)

type byLengthKind uint8

const (
	byLengthNone byLengthKind = iota
	// Names of at most eight bytes: their words and indexes.
	byLengthWords
	// A few longer names.
	byLengthFew
	// The byte position that best splits the names, and the first candidate (index + 1, 0: none) for each byte.
	byLengthTable
)

type wordEntry struct {
	word  uint64
	index uint32
}

type byLength struct {
	kind  byLengthKind
	words []wordEntry
	few   []uint32
	at    int
	first *[256]uint32
}

// nameMap holds property names by length: a few names of a length are compared directly (names of up to eight bytes
// as a single word each, so a miss never touches the text), and more are split by the byte position that best tells
// them apart, through a table from that byte to a chain of candidates.
type nameMap struct {
	names []string
	// The word of each name (see nameWord).
	words    []uint64
	byLength []byLength
	// Names longer than maxIndexedLength.
	long []uint32
	// The next candidate (index + 1, 0: none) after each name in its table chain.
	next []uint32
}

func newNameMap(names []string) nameMap {
	max := 0
	for _, n := range names {
		if len(n) <= maxIndexedLength && len(n)+1 > max {
			max = len(n) + 1
		}
	}
	groups := make([][]uint32, max)
	m := nameMap{
		names: names, words: make([]uint64, len(names)), next: make([]uint32, len(names)),
		byLength: make([]byLength, max),
	}
	for i, n := range names {
		m.words[i] = nameWord([]byte(n))
		if len(n) < max {
			groups[len(n)] = append(groups[len(n)], uint32(i))
		} else {
			m.long = append(m.long, uint32(i))
		}
	}
	for length, group := range groups {
		switch {
		case len(group) == 0:
		case length <= 8 && (len(group) <= maxCandidates || length == 0):
			entry := byLength{kind: byLengthWords}
			for _, i := range group {
				entry.words = append(entry.words, wordEntry{nameWord([]byte(names[i])), i})
			}
			m.byLength[length] = entry
		case len(group) <= maxCandidates:
			m.byLength[length] = byLength{kind: byLengthFew, few: group}
		default:
			// The position with the most distinct bytes (the shortest chains). Of equal positions, the last.
			at, best := 0, -1
			for position := 0; position < length; position++ {
				var seen [256]bool
				distinct := 0
				for _, i := range group {
					if b := names[i][position]; !seen[b] {
						seen[b] = true
						distinct++
					}
				}
				if distinct >= best {
					at, best = position, distinct
				}
			}
			first := new([256]uint32)
			// Built back to front so each chain keeps the names' order.
			for j := len(group) - 1; j >= 0; j-- {
				i := group[j]
				b := names[i][at]
				m.next[i] = first[b]
				first[b] = i + 1
			}
			m.byLength[length] = byLength{kind: byLengthTable, at: at, first: first}
		}
	}
	return m
}

// equal reports whether name i is the given name, whose word is w.
func (m *nameMap) equal(i int, name []byte, w uint64) bool {
	n := m.names[i]
	return len(n) == len(name) && m.words[i] == w && (len(name) <= 8 || n == string(name))
}

// find is the index of a name (whose word is w), or -1.
func (m *nameMap) find(name []byte, w uint64) int {
	if len(name) >= len(m.byLength) {
		for _, i := range m.long {
			if m.equal(int(i), name, w) {
				return int(i)
			}
		}
		return -1
	}
	entry := &m.byLength[len(name)]
	switch entry.kind {
	case byLengthWords:
		for i := range entry.words {
			if entry.words[i].word == w {
				return int(entry.words[i].index)
			}
		}
	case byLengthFew:
		for _, i := range entry.few {
			if m.words[i] == w && (len(name) <= 8 || m.names[i] == string(name)) {
				return int(i)
			}
		}
	case byLengthTable:
		for c := entry.first[name[entry.at]]; c != 0; c = m.next[c-1] {
			if m.words[c-1] == w && (len(name) <= 8 || m.names[c-1] == string(name)) {
				return int(c - 1)
			}
		}
	}
	return -1
}

const noName = ^uint32(0)

// names maps declared property names to their index.
type names struct {
	// Bit n set: some name has length n (lengths of 63 and more share bit 63). A name whose length is not in the
	// set is not declared, which settles most misses without a search.
	lengths uint64
	// The length and the word of each name, side by side: what the test of the expected name reads (see at).
	keys []nameKey
	m    nameMap
	// For a hint h (the index after the previous match), the name after that match in sorted order (entry 0: the
	// first name in sorted order). noName after the last.
	sortedNext []uint32
}

// nameKey is a name's length and word (see nameWord), which decide a name of at most eight bytes.
type nameKey struct {
	word   uint64
	length int
}

func lengthBit(length int) uint64 {
	if length > 63 {
		length = 63
	}
	return 1 << length
}

func newNames(list []string) *names {
	ns := &names{m: newNameMap(list), keys: make([]nameKey, len(list))}
	order := make([]uint32, len(list))
	for i, n := range list {
		ns.keys[i] = nameKey{ns.m.words[i], len(n)}
		ns.lengths |= lengthBit(len(n))
		order[i] = uint32(i)
	}
	sort.SliceStable(order, func(a, b int) bool { return list[order[a]] < list[order[b]] })
	ns.sortedNext = make([]uint32, len(list)+1)
	for i := range ns.sortedNext {
		ns.sortedNext[i] = noName
	}
	for i, at := range order {
		if i == 0 {
			ns.sortedNext[0] = at
		} else {
			ns.sortedNext[order[i-1]+1] = at
		}
	}
	return ns
}

func (ns *names) len() int {
	return len(ns.m.names)
}

// find is the index of a name, without the ordering hint, or -1.
func (ns *names) find(name []byte) int {
	if ns.lengths&lengthBit(len(name)) == 0 {
		return -1
	}
	return ns.m.find(name, nameWord(name))
}

// findString is find for a name held as a string.
func (ns *names) findString(name string) int {
	return ns.find([]byte(name))
}

// at reports whether the name at a hint (the index after the previous match) has the given length and word. For a
// name of at most eight bytes that is the name.
func (ns *names) at(hint, length int, w uint64) bool {
	return uint(hint) < uint(len(ns.keys)) && ns.keys[hint].length == length && ns.keys[hint].word == w
}

// findFrom finds a name, trying the one after the previous match first: instances tend to list their properties in
// the schema's order, so the next name is usually the next one declared. It returns the index (or -1) and the hint
// for the next call. The property loops have these lines in them instead of a call, so that a property in the
// expected place costs no call.
func (ns *names) findFrom(name []byte, hint int) (int, int) {
	w := nameWord(name)
	if ns.at(hint, len(name), w) && (len(name) <= 8 || string(name) == ns.m.names[hint]) {
		return hint, hint + 1
	}
	return ns.findAfter(name, w, hint)
}

// findAfter is findFrom for a name (whose word is w) that is not the one at the hint. It tries the name after the
// previous match in sorted order (instances written by tools that sort their keys), then searches.
func (ns *names) findAfter(name []byte, w uint64, hint int) (int, int) {
	if ns.lengths&lengthBit(len(name)) == 0 {
		return -1, hint
	}
	m := &ns.m
	if hint < len(ns.sortedNext) {
		if next := ns.sortedNext[hint]; next != noName && m.equal(int(next), name, w) {
			return int(next), int(next) + 1
		}
	}
	// A few names are compared in turn (lengths and words settle most), more are searched.
	if len(m.names) <= lookupNames {
		for i := range m.names {
			if m.equal(i, name, w) {
				return i, i + 1
			}
		}
		return -1, hint
	}
	if i := m.find(name, w); i >= 0 {
		return i, i + 1
	}
	return -1, hint
}
