package ecmaregex

import "unicode/utf8"

// Matching by UTF-16 code unit over UTF-8 text.
//
// ECMA-262 matches a pattern that has no u flag a code unit at a time. This package reads a pattern without the u
// flag only when the u flag grammar refuses it, and such a pattern is matched that way. A character of one, two or
// three bytes of UTF-8 is one code unit. A character of four bytes is two, a high surrogate and a low one, so the
// matcher has to be able to stand between them. That position is written as the offset of the character's third
// byte. Each half then spans two bytes, every position is still an offset into the text, a group is still a pair of
// offsets, and no UTF-16 copy of the text is made.
//
// The third byte of a character is a continuation byte, which is no position at all when matching by code point, so
// the two readings cannot be confused. Only a text with a character of four bytes differs between the two, and the
// instructions that compare bytes (a literal run, a word boundary, a line terminator) need no change: none of them
// can hold at the position between the halves, and all of them are right at every other.

// hasSupplementary reports whether the text has a character beyond the Basic Multilingual Plane, which is the only
// kind of text that code units and code points read differently. Such a character starts with a byte of 0xF0 or
// more.
func hasSupplementary(in []byte) bool {
	for _, c := range in {
		if c >= 0xF0 {
			return true
		}
	}
	return false
}

// unitAt returns the code unit at the position and the number of bytes it spans.
func unitAt(in []byte, pos int) (rune, int) {
	c := in[pos]
	if c >= 0xF0 {
		if r, w := utf8.DecodeRune(in[pos:]); w == 4 {
			return highSurrogate(r), 2
		}
		return utf8.RuneError, 1
	}
	if c >= 0x80 && c < 0xC0 && pos >= 2 && in[pos-2] >= 0xF0 {
		// Between the halves of the character that starts two bytes back.
		if r, w := utf8.DecodeRune(in[pos-2:]); w == 4 {
			return lowSurrogate(r), 2
		}
	}
	return utf8.DecodeRune(in[pos:])
}

// unitBefore returns the code unit that ends at the position and the number of bytes it spans.
func unitBefore(in []byte, pos int) (rune, int) {
	r, w := utf8.DecodeLastRune(in[:pos])
	if w == 4 {
		return lowSurrogate(r), 2
	}
	if w == 1 && r == utf8.RuneError && pos >= 2 && len(in)-pos >= 2 && in[pos-2] >= 0xF0 {
		// Between the halves of the character that starts two bytes back.
		if r, w := utf8.DecodeRune(in[pos-2:]); w == 4 {
			return highSurrogate(r), 2
		}
	}
	return r, w
}

// decode returns the character at the position and its width in bytes: a code unit with units set, and a code point
// otherwise. It is for the places that are not hot. The matcher's own instructions make the choice themselves, so
// that reading a code point stays one call.
func decode(in []byte, pos int, units bool) (rune, int) {
	if units {
		return unitAt(in, pos)
	}
	return utf8.DecodeRune(in[pos:])
}

// decodeLast is decode for the character that ends at the position.
func decodeLast(in []byte, pos int, units bool) (rune, int) {
	if units {
		return unitBefore(in, pos)
	}
	return utf8.DecodeLastRune(in[:pos])
}
