//! Reading UTF-8, and matching by UTF-16 code unit over UTF-8 text (`units.go`, and what the Go source takes from
//! the `unicode/utf8` package).
//!
//! ECMA-262 matches a pattern that has no u flag a code unit at a time. The engine reads a pattern without the u
//! flag only when the u flag grammar refuses it, and such a pattern is matched that way. A character of one, two or
//! three bytes of UTF-8 is one code unit. A character of four bytes is two, a high surrogate and a low one, so the
//! matcher has to be able to stand between them. That position is written as the offset of the character's third
//! byte. Each half then spans two bytes, every position is still an offset into the text, a group is still a pair
//! of offsets, and no UTF-16 copy of the text is made.
//!
//! The third byte of a character is a continuation byte, which is no position at all when matching by code point,
//! so the two readings cannot be confused. Only a text with a character of four bytes differs between the two, and
//! the instructions that compare bytes (a literal run, a word boundary, a line terminator) need no change: none of
//! them can hold at the position between the halves, and all of them are right at every other.

use super::Rune;

/// What a byte that is not UTF-8 decodes to, one byte at a time.
pub(crate) const RUNE_ERROR: Rune = 0xFFFD;
/// The first byte value that is not a character by itself.
pub(crate) const RUNE_SELF: u8 = 0x80;

#[inline(always)]
fn continuation(b: u8) -> bool {
    b & 0xC0 == 0x80
}

/// The code point at the start of `p` and its width in bytes: `(RUNE_ERROR, 1)` for bytes that are not UTF-8, and
/// `(RUNE_ERROR, 0)` for no bytes. (`utf8.DecodeRune`.)
#[inline]
pub(crate) fn decode_rune(p: &[u8]) -> (Rune, usize) {
    let Some(&p0) = p.first() else { return (RUNE_ERROR, 0) };
    if p0 < RUNE_SELF {
        return (p0 as Rune, 1);
    }
    match p0 {
        0xC2..=0xDF if p.len() >= 2 && continuation(p[1]) => {
            return (((p0 & 0x1F) as Rune) << 6 | (p[1] & 0x3F) as Rune, 2);
        }
        0xE0..=0xEF if p.len() >= 3 && continuation(p[2]) => {
            let (lo, hi) = match p0 {
                0xE0 => (0xA0, 0xBF),
                0xED => (0x80, 0x9F),
                _ => (0x80, 0xBF),
            };
            if p[1] >= lo && p[1] <= hi {
                return (((p0 & 0x0F) as Rune) << 12 | ((p[1] & 0x3F) as Rune) << 6 | (p[2] & 0x3F) as Rune, 3);
            }
        }
        0xF0..=0xF4 if p.len() >= 4 && continuation(p[2]) && continuation(p[3]) => {
            let (lo, hi) = match p0 {
                0xF0 => (0x90, 0xBF),
                0xF4 => (0x80, 0x8F),
                _ => (0x80, 0xBF),
            };
            if p[1] >= lo && p[1] <= hi {
                return (
                    ((p0 & 0x07) as Rune) << 18
                        | ((p[1] & 0x3F) as Rune) << 12
                        | ((p[2] & 0x3F) as Rune) << 6
                        | (p[3] & 0x3F) as Rune,
                    4,
                );
            }
        }
        _ => {}
    }
    (RUNE_ERROR, 1)
}

/// The code point at the end of `p` and its width in bytes. (`utf8.DecodeLastRune`.)
#[inline]
pub(crate) fn decode_last_rune(p: &[u8]) -> (Rune, usize) {
    let end = p.len();
    if end == 0 {
        return (RUNE_ERROR, 0);
    }
    let last = p[end - 1];
    if last < RUNE_SELF {
        return (last as Rune, 1);
    }
    // A character is four bytes at most, so its first byte is one of the last four.
    let lim = end.saturating_sub(4);
    let mut start = end - 1;
    while start > lim {
        start -= 1;
        if !continuation(p[start]) {
            break;
        }
    }
    let (r, size) = decode_rune(&p[start..end]);
    if start + size != end {
        return (RUNE_ERROR, 1);
    }
    (r, size)
}

/// The two UTF-16 code units of a code point beyond the Basic Multilingual Plane.
#[inline]
pub(crate) fn high_surrogate(c: Rune) -> Rune {
    0xD800 + ((c - 0x10000) >> 10)
}

#[inline]
pub(crate) fn low_surrogate(c: Rune) -> Rune {
    0xDC00 + ((c - 0x10000) & 0x3FF)
}

/// Whether the text has a character beyond the Basic Multilingual Plane, which is the only kind of text that code
/// units and code points read differently. Such a character starts with a byte of 0xF0 or more.
#[inline]
pub(crate) fn has_supplementary(input: &[u8]) -> bool {
    input.iter().any(|&c| c >= 0xF0)
}

/// The code unit at the position and the number of bytes it spans.
#[inline]
pub(crate) fn unit_at(input: &[u8], pos: usize) -> (Rune, usize) {
    let c = input[pos];
    if c >= 0xF0 {
        let (r, w) = decode_rune(&input[pos..]);
        if w == 4 {
            return (high_surrogate(r), 2);
        }
        return (RUNE_ERROR, 1);
    }
    if (0x80..0xC0).contains(&c) && pos >= 2 && input[pos - 2] >= 0xF0 {
        // Between the halves of the character that starts two bytes back.
        let (r, w) = decode_rune(&input[pos - 2..]);
        if w == 4 {
            return (low_surrogate(r), 2);
        }
    }
    decode_rune(&input[pos..])
}

/// The code unit that ends at the position and the number of bytes it spans.
#[inline]
pub(crate) fn unit_before(input: &[u8], pos: usize) -> (Rune, usize) {
    let (r, w) = decode_last_rune(&input[..pos]);
    if w == 4 {
        return (low_surrogate(r), 2);
    }
    if w == 1 && r == RUNE_ERROR && pos >= 2 && input.len() - pos >= 2 && input[pos - 2] >= 0xF0 {
        // Between the halves of the character that starts two bytes back.
        let (r, w) = decode_rune(&input[pos - 2..]);
        if w == 4 {
            return (high_surrogate(r), 2);
        }
    }
    (r, w)
}

/// The character at the position and its width in bytes: a code unit with `units` set, and a code point otherwise.
/// It is for the places that are not hot. The matcher's own instructions make the choice themselves.
#[inline]
pub(crate) fn decode(input: &[u8], pos: usize, units: bool) -> (Rune, usize) {
    if units { unit_at(input, pos) } else { decode_rune(&input[pos..]) }
}

/// [`decode`] for the character that ends at the position.
#[inline]
pub(crate) fn decode_last(input: &[u8], pos: usize, units: bool) -> (Rune, usize) {
    if units { unit_before(input, pos) } else { decode_last_rune(&input[..pos]) }
}
