//! The mapping step of UTS #46 (Unicode IDNA Compatibility Processing, section 4 step 1), which the `idn-hostname`
//! and `idn-email` formats apply to a name before they check it.
//!
//! An "ignored" code point is removed and a "mapped" one is replaced by its mapping. Every other code point is kept:
//! "valid" and "deviation" ones (nontransitional processing, so ZERO WIDTH JOINER and ZERO WIDTH NON-JOINER stay and
//! meet the contextual rules of RFC 5892), and "disallowed" ones, which the IDNA2008 checks that follow refuse. ASCII
//! is kept as it is: its mapping only lowercases the letters, which does not change whether a name is valid. The
//! normalization step (step 2) is not applied: a name that is not in Normalization Form C is valid as a host name, as
//! the JSON-Schema-Test-Suite expects. The table is the one the .NET engine uses (`gen-uts46-data.ps1`).

use std::borrow::Cow;

use crate::uts46_data::{ENTRIES, POOL};

const ENTRY_SIZE: usize = 8;

/// The mapped form of a name; the name itself when nothing in it is mapped or ignored.
pub(crate) fn map(name: &str) -> Cow<'_, str> {
    if name.is_ascii() || !name.chars().any(|c| !c.is_ascii() && find(c as u32).is_some()) {
        return Cow::Borrowed(name);
    }
    let mut mapped = String::with_capacity(name.len());
    for c in name.chars() {
        match (!c.is_ascii()).then(|| find(c as u32)).flatten() {
            Some(replacement) => mapped.push_str(replacement),
            None => mapped.push(c),
        }
    }
    Cow::Owned(mapped)
}

/// The replacement of an ignored or mapped code point outside ASCII ("" when it is ignored).
fn find(code_point: u32) -> Option<&'static str> {
    let count = ENTRIES.len() / ENTRY_SIZE;
    let (mut low, mut high) = (0usize, count);
    while low < high {
        let middle = (low + high) / 2;
        let e = &ENTRIES[middle * ENTRY_SIZE..(middle + 1) * ENTRY_SIZE];
        let start = (u32::from(e[0]) << 16) | (u32::from(e[1]) << 8) | u32::from(e[2]);
        let end = start + ((u32::from(e[3]) << 8) | u32::from(e[4]));
        if code_point < start {
            high = middle;
        } else if code_point > end {
            low = middle + 1;
        } else {
            let offset = (usize::from(e[5]) << 8) | usize::from(e[6]);
            let length = usize::from(e[7]);
            return Some(if length == 0 {
                ""
            } else {
                // The pool holds whole UTF-8 sequences, written by the generator.
                std::str::from_utf8(&POOL[offset..offset + length]).expect("the UTS #46 pool is UTF-8")
            });
        }
    }
    None
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn maps_ignores_and_keeps() {
        assert_eq!(map("example.com"), "example.com");
        assert_eq!(map("a\u{200b}b"), "ab");
        assert_eq!(map("\u{ff41}\u{ff42}"), "ab");
        assert_eq!(map("ex\u{ad}ample"), "example");
        assert_eq!(map("a\u{200d}b"), "a\u{200d}b");
        assert_eq!(crate::uts46_data::UNICODE_VERSION, "17.0.0");
    }
}
