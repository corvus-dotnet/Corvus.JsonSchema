package ecmaregex

import (
	"strconv"
	"strings"
)

// writeRE2 writes the standard library regexp syntax for a tree that has no lookaround and no backreference. Every
// literal is written as a code point escape and every class as explicit ranges, so nothing depends on how RE2 reads
// \d, \w, \s, the dot or a Unicode class. A group is written without capture, since only the answer to "does it
// match" is needed. A lazy quantifier is written greedy for the same reason. Without backreferences and
// lookarounds the two accept the same texts.
func writeRE2(b *strings.Builder, n *node) {
	switch n.kind {
	case nEmpty:
		b.WriteString("(?:)")
	case nChar:
		writeRE2Rune(b, n.r)
	case nSet:
		if n.set.empty() {
			// A class that excludes everything.
			b.WriteString(`[^\x{0}-\x{10FFFF}]`)
			return
		}
		b.WriteByte('[')
		for i := 0; i < len(n.set.ranges); i += 2 {
			writeRE2Rune(b, n.set.ranges[i])
			if n.set.ranges[i+1] != n.set.ranges[i] {
				b.WriteByte('-')
				writeRE2Rune(b, n.set.ranges[i+1])
			}
		}
		b.WriteByte(']')
	case nCat:
		for _, sub := range n.subs {
			writeRE2Group(b, sub)
		}
	case nAlt:
		for i, sub := range n.subs {
			if i > 0 {
				b.WriteByte('|')
			}
			writeRE2Group(b, sub)
		}
	case nGroup:
		writeRE2Group(b, n.subs[0])
	case nRepeat:
		writeRE2Group(b, n.subs[0])
		switch {
		case n.min == 0 && n.max == unbounded:
			b.WriteByte('*')
		case n.min == 1 && n.max == unbounded:
			b.WriteByte('+')
		case n.min == 0 && n.max == 1:
			b.WriteByte('?')
		default:
			b.WriteByte('{')
			b.WriteString(strconv.Itoa(n.min))
			if n.max != n.min {
				b.WriteByte(',')
				if n.max != unbounded {
					b.WriteString(strconv.Itoa(n.max))
				}
			}
			b.WriteByte('}')
		}
	case nBOL:
		b.WriteString(`\A`)
	case nEOL:
		b.WriteString(`\z`)
	case nWordBoundary:
		// RE2's \b is the ASCII word boundary, which is the one ECMA-262 defines.
		b.WriteString(`\b`)
	case nNotWordBoundary:
		b.WriteString(`\B`)
	}
}

func writeRE2Group(b *strings.Builder, n *node) {
	switch n.kind {
	case nChar, nSet, nBOL, nEOL, nWordBoundary, nNotWordBoundary, nEmpty:
		writeRE2(b, n)
	default:
		b.WriteString("(?:")
		writeRE2(b, n)
		b.WriteByte(')')
	}
}

func writeRE2Rune(b *strings.Builder, r rune) {
	b.WriteString(`\x{`)
	b.WriteString(strconv.FormatInt(int64(r), 16))
	b.WriteByte('}')
}
