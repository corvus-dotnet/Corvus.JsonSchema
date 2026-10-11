//! Case-insensitive matching (`fold.go`).
//!
//! It exists only inside a modifier group such as `(?i:...)`. ECMA-262 defines it through a Canonicalize function.
//! Two characters match when they canonicalize to the same character. The function differs between the two
//! grammars. With the u flag it is Unicode simple case folding. With no flag it is the single character uppercase
//! mapping of a UTF-16 code unit, which never maps a character outside ASCII into ASCII.
//!
//! The parser applies the equivalence to every literal and class as it reads them, so a case-insensitive group is
//! matched with plain classes. Only a backreference needs the function when matching.
//!
//! The folding and the uppercase mapping come from the tables of `ucd`, never from the standard library, so a
//! pattern matches the same texts whichever toolchain built the program.

use std::collections::HashMap;
use std::sync::{Arc, LazyLock};

use super::Rune;
use super::charset::CharSet;
use super::ucd;

/// The characters of the Basic Multilingual Plane whose uppercase form is more than one character, as inclusive
/// pairs. Canonicalize leaves them alone.
static MULTI_UPPER: LazyLock<CharSet> = LazyLock::new(|| {
    CharSet::new(
        &[
            0xDF, 0xDF, 0x149, 0x149, 0x1F0, 0x1F0, 0x390, 0x390, 0x3B0, 0x3B0, 0x587, 0x587, 0x1E96, 0x1E9A, 0x1F50,
            0x1F50, 0x1F52, 0x1F52, 0x1F54, 0x1F54, 0x1F56, 0x1F56, 0x1F80, 0x1FAF, 0x1FB2, 0x1FB4, 0x1FB6, 0x1FB7,
            0x1FBC, 0x1FBC, 0x1FC2, 0x1FC4, 0x1FC6, 0x1FC7, 0x1FCC, 0x1FCC, 0x1FD2, 0x1FD3, 0x1FD6, 0x1FD7, 0x1FE2,
            0x1FE4, 0x1FE6, 0x1FE7, 0x1FF2, 0x1FF4, 0x1FF6, 0x1FF7, 0x1FFC, 0x1FFC, 0xFB00, 0xFB06, 0xFB13, 0xFB17,
        ],
        false,
    )
});

/// The Canonicalize function of ECMA-262 for case-insensitive matching.
pub(crate) fn canonicalize(r: Rune, unicode_mode: bool) -> Rune {
    if unicode_mode {
        // Every character of a simple case folding class folds to the same character.
        return ucd::fold(r);
    }
    // A character beyond the Basic Multilingual Plane is two code units, and neither has an uppercase form.
    if r > 0xFFFF || MULTI_UPPER.contains(r) {
        return r;
    }
    let upper = ucd::to_upper(r);
    if r >= 128 && upper < 128 || upper > 0xFFFF {
        return r;
    }
    upper
}

/// The characters that are equivalent to some other character, each with all the members of its equivalence class.
pub(crate) struct FoldTable {
    points: Vec<Rune>,
    /// For each point, the number of its class in `classes`.
    class_of: Vec<u32>,
    classes: Vec<Vec<Rune>>,
}

fn new_fold_table(classes: HashMap<Rune, Vec<Rune>>) -> FoldTable {
    let mut t = FoldTable { points: Vec::new(), class_of: Vec::new(), classes: Vec::new() };
    let mut by_member: HashMap<Rune, u32> = HashMap::new();
    for (_, class) in classes {
        if class.len() < 2 {
            continue;
        }
        for &member in &class {
            t.points.push(member);
            by_member.insert(member, t.classes.len() as u32);
        }
        t.classes.push(class);
    }
    t.points.sort_unstable();
    t.class_of = t.points.iter().map(|point| by_member[point]).collect();
    t
}

static UNICODE_FOLD_TABLE: LazyLock<FoldTable> = LazyLock::new(|| {
    // A class is a character that others fold to, with those others.
    let mut classes: HashMap<Rune, Vec<Rune>> = HashMap::new();
    for r in ucd::folding() {
        let key = ucd::fold(r);
        classes.entry(key).or_insert_with(|| vec![key]).push(r);
    }
    new_fold_table(classes)
});

static LEGACY_FOLD_TABLE: LazyLock<FoldTable> = LazyLock::new(|| {
    let mut classes: HashMap<Rune, Vec<Rune>> = HashMap::new();
    for r in 0..=0xFFFF {
        classes.entry(canonicalize(r, false)).or_default().push(r);
    }
    new_fold_table(classes)
});

/// The table of the grammar in use. It is built on first use, which only a pattern with a case-insensitive group
/// reaches.
fn fold_classes(unicode_mode: bool) -> &'static FoldTable {
    if unicode_mode { &UNICODE_FOLD_TABLE } else { &LEGACY_FOLD_TABLE }
}

/// The set of every character equivalent to some member of the set.
///
/// It also returns the number of characters with a case variant that the set holds, which is the work it did.
pub(crate) fn fold_closure(set: &Arc<CharSet>, unicode_mode: bool) -> (Arc<CharSet>, usize) {
    let table = fold_classes(unicode_mode);
    let mut extra: Vec<Rune> = Vec::new();
    let mut variants = 0;
    for &[lo, hi] in set.ranges.as_chunks::<2>().0 {
        let mut k = table.points.partition_point(|&p| p < lo);
        while k < table.points.len() && table.points[k] <= hi {
            variants += 1;
            for &member in &table.classes[table.class_of[k] as usize] {
                if !set.contains(member) {
                    extra.push(member);
                    extra.push(member);
                }
            }
            k += 1;
        }
    }
    if extra.is_empty() {
        return (set.clone(), variants);
    }
    extra.extend_from_slice(&set.ranges);
    (CharSet::shared(&extra, false), variants)
}

/// With the u flag and case-insensitive matching, `\w` and `\b` also count the two characters that fold to an ASCII
/// letter (U+017F, the long s, and U+212A, the Kelvin sign) as word characters.
const FOLD_WORD_PAIRS: &[Rune] = &[0x30, 0x39, 0x41, 0x5A, 0x5F, 0x5F, 0x61, 0x7A, 0x17F, 0x17F, 0x212A, 0x212A];

pub(crate) static FOLD_WORD_SET: LazyLock<Arc<CharSet>> = LazyLock::new(|| CharSet::shared(FOLD_WORD_PAIRS, false));
pub(crate) static NOT_FOLD_WORD_SET: LazyLock<Arc<CharSet>> = LazyLock::new(|| CharSet::shared(FOLD_WORD_PAIRS, true));

pub(crate) fn is_fold_word_rune(r: Rune) -> bool {
    (0..128).contains(&r) && super::charset::is_word_byte(r as u8) || r == 0x17F || r == 0x212A
}
