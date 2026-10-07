package jsonschema

import (
	"encoding/binary"
	"sort"
)

// Property name lookup for the fail-fast plans.

// nameWord is a name of at most eight bytes as one word, unique among names of the same length: the first and last
// four bytes (overlapping, so every byte is in one of them), or for shorter names the first, middle and last byte.
func nameWord(b []byte) uint64 {
	n := len(b)
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
	names    []string
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
	m := nameMap{names: names, next: make([]uint32, len(names)), byLength: make([]byLength, max)}
	for i, n := range names {
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

// find is the index of a name, or -1.
func (m *nameMap) find(name []byte) int {
	if len(name) >= len(m.byLength) {
		for _, i := range m.long {
			if m.names[i] == string(name) {
				return int(i)
			}
		}
		return -1
	}
	entry := &m.byLength[len(name)]
	switch entry.kind {
	case byLengthWords:
		w := nameWord(name)
		for i := range entry.words {
			if entry.words[i].word == w {
				return int(entry.words[i].index)
			}
		}
	case byLengthFew:
		for _, i := range entry.few {
			if m.names[i] == string(name) {
				return int(i)
			}
		}
	case byLengthTable:
		for c := entry.first[name[entry.at]]; c != 0; c = m.next[c-1] {
			if m.names[c-1] == string(name) {
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
	m       nameMap
	// The word of each name of at most eight bytes: such a name equals another of its length when the words do.
	words []uint64
	// For a hint h (the index after the previous match), the name after that match in sorted order (entry 0: the
	// first name in sorted order). noName after the last.
	sortedNext []uint32
}

func lengthBit(length int) uint64 {
	if length > 63 {
		length = 63
	}
	return 1 << length
}

func newNames(list []string) *names {
	ns := &names{m: newNameMap(list), words: make([]uint64, len(list))}
	order := make([]uint32, len(list))
	for i, n := range list {
		if len(n) <= 8 {
			ns.words[i] = nameWord([]byte(n))
		}
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
	return ns.m.find(name)
}

// findString is find for a name held as a string.
func (ns *names) findString(name string) int {
	return ns.find([]byte(name))
}

// findFrom finds a name, trying the one after the previous match first: instances tend to list their properties in
// the schema's order, so the next name is usually the next one declared. Failing that, the next name in sorted order
// (instances written by tools that sort their keys). It returns the index (or -1) and the hint for the next call.
func (ns *names) findFrom(name []byte, hint int) (int, int) {
	if ns.lengths&lengthBit(len(name)) == 0 {
		return -1, hint
	}
	list := ns.m.names
	if len(name) <= 8 {
		// Short names are compared as words, without a call.
		w := nameWord(name)
		if hint < len(list) && len(list[hint]) == len(name) && ns.words[hint] == w {
			return hint, hint + 1
		}
		if hint < len(ns.sortedNext) {
			if next := ns.sortedNext[hint]; next != noName && len(list[next]) == len(name) && ns.words[next] == w {
				return int(next), int(next) + 1
			}
		}
		if len(list) <= lookupNames {
			for i := range list {
				if len(list[i]) == len(name) && ns.words[i] == w {
					return i, i + 1
				}
			}
			return -1, hint
		}
	} else {
		if hint < len(list) && list[hint] == string(name) {
			return hint, hint + 1
		}
		if hint < len(ns.sortedNext) {
			if next := ns.sortedNext[hint]; next != noName && list[next] == string(name) {
				return int(next), int(next) + 1
			}
		}
		// A few names are compared in turn (lengths settle most), more are searched.
		if len(list) <= lookupNames {
			for i := range list {
				if list[i] == string(name) {
					return i, i + 1
				}
			}
			return -1, hint
		}
	}
	if i := ns.m.find(name); i >= 0 {
		return i, i + 1
	}
	return -1, hint
}
