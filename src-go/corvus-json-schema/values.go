package jsonschema

import (
	"encoding/binary"
	"math"
	"math/bits"
	"slices"
)

// JSON equality, hashing and uniqueness over values in documents (an instance against a schema's constant).

// valuesEqual is JSON equality: numbers by value, objects by their property sets, arrays element by element.
func valuesEqual(a *Document, x int, b *Document, y int) bool {
	kind := a.kind(x)
	if kind != b.kind(y) {
		return false
	}
	switch kind {
	case kindNull:
		return true
	case kindBool:
		return a.boolean(x) == b.boolean(y)
	case kindNumber:
		return compareNumbers(a.flags(x), a.data(x), b.flags(y), b.data(y)) == 0
	case kindString:
		return a.count(x) == b.count(y) && string(a.str(x)) == string(b.str(y))
	case kindArray:
		n := a.count(x)
		if n != b.count(y) {
			return false
		}
		p, q := a.first(x), b.first(y)
		for i := 0; i < n; i++ {
			if !valuesEqual(a, p+i, b, q+i) {
				return false
			}
		}
		return true
	default:
		n := a.count(x)
		if n != b.count(y) {
			return false
		}
		// Objects usually list their members in the same order: compare position by position, and look names up
		// only from the first position where the names differ.
		p, q := a.first(x), b.first(y)
		for i := 0; i < n; i++ {
			k, l := p+2*i, q+2*i
			if a.count(k) != b.count(l) || string(a.str(k)) != string(b.str(l)) {
				for j := i; j < n; j++ {
					key := p + 2*j
					w := findProperty(b, y, a.str(key))
					if w < 0 || !valuesEqual(a, key+1, b, w) {
						return false
					}
				}
				return true
			}
			if !valuesEqual(a, k+1, b, l+1) {
				return false
			}
		}
		return true
	}
}

// findProperty is the value of the property of object named by key, or -1.
func findProperty(d *Document, object int, key []byte) int {
	k := d.first(object)
	for i := d.count(object); i > 0; i-- {
		if d.count(k) == len(key) && string(d.str(k)) == string(key) {
			return k + 1
		}
		k += 2
	}
	return -1
}

const hashK = 0x9e3779b97f4a7c15

// valueHash is a hash that agrees with JSON equality (object hashing is independent of the member order).
func valueHash(d *Document, v int) uint64 {
	switch d.kind(v) {
	case kindNull:
		return 0x53
	case kindBool:
		if d.boolean(v) {
			return 0x52
		}
		return 0x51
	case kindNumber:
		flag, data := d.flags(v), d.data(v)
		if flag == numFloat {
			f := math.Float64frombits(data)
			// Integral floats hash as the integer they equal.
			if f == math.Floor(f) && math.Abs(f) < two63 {
				return uint64(int64(f))*hashK ^ 0x1234
			}
			return math.Float64bits(f)*hashK ^ 0x4321
		}
		if flag == numUint {
			// Floats at or above 2^63 are integers, and never equal a uint64 exactly unless the uint64 is a float:
			// hash both by the float.
			return math.Float64bits(float64(data))*hashK ^ 0x4321
		}
		return data*hashK ^ 0x1234
	case kindString:
		return strHash(d.str(v))
	case kindArray:
		n := d.count(v)
		h := uint64(0x54 + n)
		c := d.first(v)
		for i := 0; i < n; i++ {
			h = h*31 + valueHash(d, c+i)
		}
		return h
	default:
		// Equal objects have the same member values, so a sum of the values' hashes (whatever the order) agrees
		// with equality.
		n := d.count(v)
		h := uint64(0x55 + n)
		c := d.first(v)
		for i := 0; i < n; i++ {
			h += valueHash(d, c+2*i+1) * 0x2c1b3c6d
		}
		return h
	}
}

// strHash hashes a string, eight bytes at a time.
func strHash(b []byte) uint64 {
	n := len(b)
	h := uint64(n) * hashK
	i := 0
	for ; i+8 <= n; i += 8 {
		h = (bits.RotateLeft64(h, 5) ^ binary.LittleEndian.Uint64(b[i:])) * hashK
	}
	// The tail as one word: the last eight bytes when there are that many (overlapping the words already hashed),
	// else the overlapping first and last four, else the bytes themselves.
	var tail uint64
	switch {
	case n >= 8:
		tail = binary.LittleEndian.Uint64(b[n-8:])
	case n >= 4:
		tail = uint64(binary.LittleEndian.Uint32(b)) | uint64(binary.LittleEndian.Uint32(b[n-4:]))<<32
	default:
		for _, c := range b {
			tail = tail<<8 | uint64(c)
		}
	}
	return (bits.RotateLeft64(h, 5) ^ tail) * hashK
}

// allUnique decides uniqueItems: pairwise for short arrays, otherwise sorted by hash in scratch, each entry the
// hash's high half and the item's index, so that only items with equal hashes are compared. It allocates nothing
// once scratch has grown.
func allUnique(d *Document, array int, scratch *[]uint64) bool {
	n := d.count(array)
	if n < 2 {
		return true
	}
	c := d.first(array)
	if n <= 32 && allStrings(d, c, n) {
		// Lists of names, the common case: pairwise, comparing lengths first.
		for i := 1; i < n; i++ {
			for j := 0; j < i; j++ {
				if d.count(c+i) == d.count(c+j) && string(d.str(c+i)) == string(d.str(c+j)) {
					return false
				}
			}
		}
		return true
	}
	if n <= 16 {
		for i := 1; i < n; i++ {
			for j := 0; j < i; j++ {
				if valuesEqual(d, c+i, d, c+j) {
					return false
				}
			}
		}
		return true
	}
	s := (*scratch)[:0]
	for i := 0; i < n; i++ {
		s = append(s, valueHash(d, c+i)&^0xffffffff|uint64(i))
	}
	*scratch = s
	slices.Sort(s)
	start := 0
	for end := 1; end <= n; end++ {
		if end == n || s[end]>>32 != s[start]>>32 {
			for i := start + 1; i < end; i++ {
				for j := start; j < i; j++ {
					if valuesEqual(d, c+int(uint32(s[i])), d, c+int(uint32(s[j]))) {
						return false
					}
				}
			}
			start = end
		}
	}
	return true
}

func allStrings(d *Document, first, n int) bool {
	for i := 0; i < n; i++ {
		if d.kind(first+i) != kindString {
			return false
		}
	}
	return true
}
