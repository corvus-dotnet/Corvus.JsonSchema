package jsonschema

import (
	"strings"
	"unicode/utf8"
)

// uts46Map applies the mapping step of UTS #46 (Unicode IDNA Compatibility Processing, section 4 step 1), which the
// idn-hostname and idn-email formats apply to a name before they check it.
//
// An "ignored" code point is removed and a "mapped" one is replaced by its mapping. Every other code point is kept:
// "valid" and "deviation" ones (nontransitional processing, so ZERO WIDTH JOINER and ZERO WIDTH NON-JOINER stay and
// meet the contextual rules of RFC 5892), and "disallowed" ones, which the IDNA2008 checks that follow refuse. ASCII is
// kept as it is: its mapping only lowercases the letters, which does not change whether a name is valid. The
// normalization step (step 2) is not applied: a name that is not in Normalization Form C is valid as a host name, as
// the JSON-Schema-Test-Suite expects. The table is the one the .NET engine and the Rust crate use.
func uts46Map(name string) string {
	changes := false
	for _, r := range name {
		if r >= utf8.RuneSelf {
			if _, ok := uts46Find(r); ok {
				changes = true
				break
			}
		}
	}
	if !changes {
		return name
	}
	var b strings.Builder
	b.Grow(len(name))
	for _, r := range name {
		if r >= utf8.RuneSelf {
			if replacement, ok := uts46Find(r); ok {
				b.WriteString(replacement)
				continue
			}
		}
		b.WriteRune(r)
	}
	return b.String()
}

// uts46Find returns the replacement of an ignored or mapped code point outside ASCII ("" when it is ignored).
func uts46Find(r rune) (string, bool) {
	const entrySize = 8
	low, high := 0, len(uts46Entries)/entrySize
	for low < high {
		middle := (low + high) / 2
		e := uts46Entries[middle*entrySize : (middle+1)*entrySize]
		start := rune(e[0])<<16 | rune(e[1])<<8 | rune(e[2])
		end := start + (rune(e[3])<<8 | rune(e[4]))
		switch {
		case r < start:
			high = middle
		case r > end:
			low = middle + 1
		default:
			offset := int(e[5])<<8 | int(e[6])
			length := int(e[7])
			if length == 0 {
				return "", true
			}
			return string(uts46Pool[offset : offset+length]), true
		}
	}
	return "", false
}
