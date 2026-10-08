package jsonschema

import (
	"encoding/binary"
	"math/bits"
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

// nameKey is what decides whether a name is a given one: its length, its word, and for a name longer than eight
// bytes its last eight bytes. That is the whole name up to sixteen bytes, so only a longer name has its text compared.
type nameKey struct {
	word   uint64
	tail   uint64
	length int
}

// nameMap finds a property name among a set of names, through a hash table from a name's key to its index, with
// open addressing. The table has at least four slots for each name and its hash is the one of a few that leaves the
// fewest names away from their first slot (for most sets, none), so a search is one multiplication and one read to
// the index, then the comparison of the key.
type nameMap struct {
	names []string
	// The word of each name (see nameWord).
	words []uint64
	keys  []nameKey
	// The index + 1 of the name in each slot (0: empty). The length is a power of two.
	table []uint16
	// The multiplier of the hash. A slot is the high bits of the product, cut to the table's length.
	mul uint64
	// A set too large for the table's indexes (which no schema in practice has) is a map instead.
	large map[string]int
}

const (
	// The first multiplier tried for a map's hash, and the number tried.
	nameHashMultiplier  = 0x9e3779b97f4a7c15
	nameHashMultipliers = 24
	// The high bits of the hash that give a slot.
	nameHashShift = 64 - 18
	// More names than this are held in a map (the table has four to eight slots for each name).
	maxTableNames = 1 << 15
)

// tailWord is the last eight bytes of a name longer than eight bytes.
func tailWord(b []byte) uint64 {
	return binary.LittleEndian.Uint64(b[len(b)-8:])
}

func newNameMap(names []string) nameMap {
	m := nameMap{names: names, words: make([]uint64, len(names)), keys: make([]nameKey, len(names))}
	for i, n := range names {
		key := nameKey{word: nameWord([]byte(n)), length: len(n)}
		if len(n) > 8 {
			key.tail = tailWord([]byte(n))
		}
		m.words[i], m.keys[i] = key.word, key
	}
	if len(names) > maxTableNames {
		m.large = make(map[string]int, len(names))
		for i := len(names) - 1; i >= 0; i-- {
			m.large[names[i]] = i
		}
		return m
	}
	size := 4
	for size < 4*len(names) {
		size <<= 1
	}
	// The multiplier that leaves the fewest names away from their first slot.
	best, bestMoved := uint64(nameHashMultiplier), -1
	table := make([]uint16, size)
	mul := uint64(nameHashMultiplier)
	for try := 0; try < nameHashMultipliers; try++ {
		clear(table)
		m.mul = mul
		if moved := m.fill(table); bestMoved < 0 || moved < bestMoved {
			best, bestMoved = mul, moved
			if moved == 0 {
				break
			}
		}
		// The next odd multiplier (a step of an xorshift generator).
		mul ^= mul << 13
		mul ^= mul >> 7
		mul ^= mul << 17
		mul |= 1
	}
	if m.mul != best {
		clear(table)
		m.mul = best
		m.fill(table)
	}
	m.table = table
	return m
}

// fill puts the names in a table under the map's hash, each in the first free slot from its own. It returns how
// many are not in their own slot.
func (m *nameMap) fill(table []uint16) int {
	mask := uint64(len(table) - 1)
	moved := 0
names:
	for i := range m.keys {
		k := &m.keys[i]
		home := m.hash(k.word, k.tail, k.length) >> nameHashShift
		slot := home
		for ; table[slot&mask] != 0; slot++ {
			// The first of equal names keeps the slot (a set built from a schema has no equal names).
			if m.names[table[slot&mask]-1] == m.names[i] {
				continue names
			}
		}
		table[slot&mask] = uint16(i) + 1
		if slot != home {
			moved++
		}
	}
	return moved
}

// hash is the hash of a key.
func (m *nameMap) hash(word, tail uint64, length int) uint64 {
	return ((word ^ bits.RotateLeft64(tail, 29)) + uint64(length)) * m.mul
}

// rest reports whether name i is the given name, which is longer than eight bytes, when their lengths and words are
// equal.
func (m *nameMap) rest(i int, name []byte) bool {
	return tailWord(name) == m.keys[i].tail && (len(name) <= 16 || m.names[i] == string(name))
}

// equal reports whether name i is the given name, whose word is w.
func (m *nameMap) equal(i int, name []byte, w uint64) bool {
	k := &m.keys[i]
	return k.length == len(name) && k.word == w && (len(name) <= 8 || m.rest(i, name))
}

// find is the index of a name (whose word is w), or -1.
func (m *nameMap) find(name []byte, w uint64) int {
	if len(name) > 16 || m.large != nil {
		return m.findLong(name, w)
	}
	tail := uint64(0)
	if len(name) > 8 {
		tail = tailWord(name)
	}
	return m.findKey(w, tail, len(name))
}

// findKey is the index of the name with a key, or -1, for a name of at most sixteen bytes (which its key decides).
// It calls nothing, so it keeps no frame.
func (m *nameMap) findKey(w, tail uint64, length int) int {
	table := m.table
	mask := uint64(len(table) - 1)
	for slot := m.hash(w, tail, length) >> nameHashShift; ; slot++ {
		at := table[slot&mask]
		if at == 0 {
			return -1
		}
		if k := &m.keys[at-1]; k.word == w && k.tail == tail && k.length == length {
			return int(at) - 1
		}
	}
}

// findLong is find for a name longer than sixteen bytes, whose text is compared as well, and for a set held in a
// map.
func (m *nameMap) findLong(name []byte, w uint64) int {
	if m.large != nil {
		if i, ok := m.large[string(name)]; ok {
			return i
		}
		return -1
	}
	tail := uint64(0)
	if len(name) > 8 {
		tail = tailWord(name)
	}
	table := m.table
	mask := uint64(len(table) - 1)
	for slot := m.hash(w, tail, len(name)) >> nameHashShift; ; slot++ {
		at := table[slot&mask]
		if at == 0 {
			return -1
		}
		if k := &m.keys[at-1]; k.word == w && k.tail == tail && k.length == len(name) && m.names[at-1] == string(name) {
			return int(at) - 1
		}
	}
}

const noName = ^uint32(0)

// names maps declared property names to their index.
type names struct {
	// Bit n set: some name has length n (lengths of 63 and more share bit 63). A name whose length is not in the
	// set is not declared, which settles most misses without a search.
	lengths uint64
	m       nameMap
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
	ns := &names{m: newNameMap(list)}
	order := make([]uint32, len(list))
	for i, n := range list {
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
	keys := ns.m.keys
	return uint(hint) < uint(len(keys)) && keys[hint].length == length && keys[hint].word == w
}

// findFrom finds a name, trying the one after the previous match first: instances tend to list their properties in
// the schema's order, so the next name is usually the next one declared. It returns the index (or -1) and the hint
// for the next call. The property loops have these lines in them instead of a call, so that a property in the
// expected place costs no call.
func (ns *names) findFrom(name []byte, hint int) (int, int) {
	w := nameWord(name)
	if ns.at(hint, len(name), w) && (len(name) <= 8 || ns.m.rest(hint, name)) {
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
	if i := m.find(name, w); i >= 0 {
		return i, i + 1
	}
	return -1, hint
}
