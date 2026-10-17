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

// re2Cost estimates the number of instructions of the program the regexp package compiles for a tree. The package
// writes a counted repetition out: x{n,m} is m copies of x, and x{n,} is n copies and a loop. The estimate is never
// much below the true figure, and it stops counting above maxRE2Cost.
func re2Cost(n *node) int {
	switch n.kind {
	case nEmpty:
		return 0
	case nCat, nAlt, nGroup:
		cost := len(n.subs) - 1
		for _, sub := range n.subs {
			cost += re2Cost(sub)
			if cost > maxRE2Cost {
				return maxRE2Cost + 1
			}
		}
		return cost
	case nRepeat:
		copies := n.max
		if copies == unbounded {
			copies = n.min
		}
		if copies < 1 {
			copies = 1
		}
		// Each copy has the body and one instruction for the choice to enter it.
		body := re2Cost(n.subs[0]) + 1
		if copies > maxRE2Cost || body*copies > maxRE2Cost {
			return maxRE2Cost + 1
		}
		return body * copies
	}
	return 1
}

// maxRE2Size is the largest program the regexp package is asked to compile, in the bytes estimated for what the
// package holds for it (see re2Size): 2 MiB. A pattern over it runs on the backtracking matcher, which holds a class
// once however often the pattern repeats it.
const maxRE2Size = 2 << 20

// re2Size estimates what the regexp package holds for the program it compiles for a tree, in bytes. The package
// does not say, so this is what a pattern on it is counted as (see Retained), and what maxRE2Size is compared with.
//
// An instruction is 40 bytes, and the one-pass matcher has a copy of each. A class is one instruction, with its
// ranges beside it: 8 bytes a range in the program, and in the one-pass matcher 8 more with a table of 4 a range.
// A counted repetition is written out, and the one-pass matcher has the ranges of every copy. The translation the
// package keeps as text has every range of a class once, at 20 bytes or so. Measured, no pattern held more than the estimate
// (see TestAPatternIsCountedAsNoLessThanItHolds and TestNoCountOfAWideClassHoldsALargeProgram). The estimate stops
// counting above maxRE2Size.
func re2Size(n *node) int {
	written, copied := re2Sizes(n)
	return min(written+copied, maxRE2Size+1)
}

// re2Sizes returns the two parts of re2Size: what a tree holds once however often it is repeated, and what every
// copy of it holds.
func re2Sizes(n *node) (written, copied int) {
	const (
		instBytes    = 96
		rangeBytes   = 32
		writtenBytes = 48
	)
	switch n.kind {
	case nEmpty:
		return 0, 0
	case nSet:
		ranges := len(n.set.ranges) / 2
		return min(writtenBytes*ranges, maxRE2Size+1), min(instBytes+rangeBytes*ranges, maxRE2Size+1)
	case nCat, nAlt, nGroup:
		copied = instBytes * len(n.subs)
		for _, sub := range n.subs {
			w, c := re2Sizes(sub)
			written, copied = written+w, copied+c
			if written > maxRE2Size || copied > maxRE2Size {
				return maxRE2Size + 1, maxRE2Size + 1
			}
		}
		return written, copied
	case nRepeat:
		copies := n.max
		if copies == unbounded {
			copies = n.min
		}
		copies = max(copies, 1)
		written, copied = re2Sizes(n.subs[0])
		copied += instBytes
		if copies > maxRE2Size || copied*copies > maxRE2Size {
			return written, maxRE2Size + 1
		}
		return written, copied * copies
	}
	return 0, instBytes
}
