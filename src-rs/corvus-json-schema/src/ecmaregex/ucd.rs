//! The Unicode Character Database properties the engine reads, for one fixed version of Unicode (the `ucd` package
//! of the Go module).
//!
//! Nothing in the engine takes Unicode properties from the Rust standard library or from the `regex` crate, because
//! those follow the toolchain and the version of the dependency. With its own tables the crate gives the same
//! answers whatever builds it, and the answers change only when the tables are generated again. [`VERSION`] names
//! the version of Unicode they hold.
//!
//! The tables are in `tables_gen.rs`, which `tools/gen-ucd-tables.ps1` writes from the tables of the Go module, so
//! the ports hold the same data. Each is a sorted list of inclusive ranges, the code points up to U+FFFF as 16-bit
//! bounds and the rest as 32-bit bounds. A value that follows from other values (a General_Category group such as
//! L, the Assigned property, the Unknown script) has no table of its own and is built when it is first asked for.

use std::sync::LazyLock;

use super::Rune;
use super::tables_gen as tables;

#[cfg(test)]
pub(crate) use super::tables_gen::VERSION;

/// The last Unicode code point.
pub(crate) const MAX_RUNE: Rune = 0x10FFFF;

/// An immutable set of code points. `r16` holds the ranges that end at or below U+FFFF and `r32` the ranges that
/// start above it, each as inclusive pairs in ascending order. No two ranges of one list overlap or touch.
pub(crate) struct Table {
    pub(super) r16: &'static [u16],
    pub(super) r32: &'static [u32],
}

impl Table {
    /// Whether the table holds the code point.
    pub(crate) fn contains(&self, r: Rune) -> bool {
        if (r as u32) <= 0xFFFF {
            let (ranges, c) = (self.r16, r as u16);
            let (mut lo, mut hi) = (0, ranges.len() / 2);
            while lo < hi {
                let mid = (lo + hi) >> 1;
                if c > ranges[2 * mid + 1] {
                    lo = mid + 1;
                } else if c < ranges[2 * mid] {
                    hi = mid;
                } else {
                    return true;
                }
            }
            return false;
        }
        let (ranges, c) = (self.r32, r as u32);
        let (mut lo, mut hi) = (0, ranges.len() / 2);
        while lo < hi {
            let mid = (lo + hi) >> 1;
            if c > ranges[2 * mid + 1] {
                lo = mid + 1;
            } else if c < ranges[2 * mid] {
                hi = mid;
            } else {
                return true;
            }
        }
        false
    }

    /// Appends the ranges of the table to `dst` as inclusive pairs in ascending order. The pairs neither overlap
    /// nor touch.
    pub(crate) fn append_ranges(&self, dst: &mut Vec<Rune>) {
        dst.reserve(self.r16.len() + self.r32.len());
        dst.extend(self.r16.iter().map(|&c| c as Rune));
        let mut high = self.r32;
        // A range that crosses U+FFFF is stored in two parts.
        if !self.r16.is_empty() && !high.is_empty() && self.r16[self.r16.len() - 1] == 0xFFFF && high[0] == 0x10000 {
            let last = dst.len() - 1;
            dst[last] = high[1] as Rune;
            high = &high[2..];
        }
        dst.extend(high.iter().map(|&c| c as Rune));
    }

    /// The ranges of the table as inclusive pairs.
    pub(crate) fn ranges(&self) -> Vec<Rune> {
        let mut pairs = Vec::new();
        self.append_ranges(&mut pairs);
        pairs
    }
}

/// A table that is built, not generated: the ranges of a value that follows from other values.
pub(crate) struct Built(Vec<Rune>);

/// The ranges of a property: of a generated table, or of one that was built.
pub(crate) enum Ranges {
    Table(&'static Table),
    Built(&'static Built),
}

impl Ranges {
    /// The ranges as inclusive pairs in ascending order.
    pub(crate) fn pairs(&self) -> Vec<Rune> {
        match self {
            Ranges::Table(t) => t.ranges(),
            Ranges::Built(b) => b.0.clone(),
        }
    }
}

/// The table of the code points that any of the tables holds.
fn union(tables: &[&Table]) -> Built {
    let mut all = Vec::new();
    for t in tables {
        t.append_ranges(&mut all);
    }
    let mut order: Vec<usize> = (0..all.len() / 2).collect();
    order.sort_by_key(|&i| all[2 * i]);
    let mut merged: Vec<Rune> = Vec::with_capacity(all.len());
    for i in order {
        let (lo, hi) = (all[2 * i], all[2 * i + 1]);
        if let Some(last) = merged.last_mut()
            && lo <= *last + 1
        {
            if hi > *last {
                *last = hi;
            }
            continue;
        }
        merged.push(lo);
        merged.push(hi);
    }
    Built(merged)
}

/// The table of the code points that the table does not hold.
fn complement(t: &Table) -> Built {
    let pairs = t.ranges();
    let mut inverse = Vec::with_capacity(pairs.len() + 2);
    let mut next: Rune = 0;
    for &[lo, hi] in pairs.as_chunks::<2>().0 {
        if lo > next {
            inverse.push(next);
            inverse.push(lo - 1);
        }
        next = hi + 1;
    }
    if next <= MAX_RUNE {
        inverse.push(next);
        inverse.push(MAX_RUNE);
    }
    Built(inverse)
}

// The General_Category values that are groups of other values, the Assigned property and the Unknown script. Each
// is built on first use and kept. The generator of the Go tables checks each definition against their source.
static LETTER: LazyLock<Built> =
    LazyLock::new(|| union(&[&tables::GC_LU, &tables::GC_LL, &tables::GC_LT, &tables::GC_LM, &tables::GC_LO]));
static CASED_LETTER: LazyLock<Built> = LazyLock::new(|| union(&[&tables::GC_LU, &tables::GC_LL, &tables::GC_LT]));
static MARK: LazyLock<Built> = LazyLock::new(|| union(&[&tables::GC_MN, &tables::GC_MC, &tables::GC_ME]));
static NUMBER: LazyLock<Built> = LazyLock::new(|| union(&[&tables::GC_ND, &tables::GC_NL, &tables::GC_NO]));
static PUNCTUATION: LazyLock<Built> = LazyLock::new(|| {
    union(&[
        &tables::GC_PC,
        &tables::GC_PD,
        &tables::GC_PS,
        &tables::GC_PE,
        &tables::GC_PI,
        &tables::GC_PF,
        &tables::GC_PO,
    ])
});
static SYMBOL: LazyLock<Built> =
    LazyLock::new(|| union(&[&tables::GC_SM, &tables::GC_SC, &tables::GC_SK, &tables::GC_SO]));
static SEPARATOR: LazyLock<Built> = LazyLock::new(|| union(&[&tables::GC_ZS, &tables::GC_ZL, &tables::GC_ZP]));
static OTHER: LazyLock<Built> =
    LazyLock::new(|| union(&[&tables::GC_CC, &tables::GC_CF, &tables::GC_CS, &tables::GC_CO, &tables::GC_CN]));
/// The code points that have a General_Category other than Cn.
static ASSIGNED: LazyLock<Built> = LazyLock::new(|| complement(&tables::GC_CN));
/// The code points that belong to no script. They are the unassigned, private use and surrogate code points.
static UNKNOWN_SCRIPT: LazyLock<Built> = LazyLock::new(|| union(&[&tables::GC_CN, &tables::GC_CO, &tables::GC_CS]));

/// The table of the Script value Katakana_Or_Hiragana (Hrkt). PropertyValueAliases.txt lists the value, and no code
/// point has it, in Script or in Script_Extensions. Scripts.txt has no line for it, so the generated tables do not
/// have it.
static KATAKANA_OR_HIRAGANA: Table = Table { r16: &[], r32: &[] };

/// The table of a General_Category value by its short name, such as "Lu" or "L". `None` for any other name.
pub(crate) fn category(name: &str) -> Option<Ranges> {
    let built = match name {
        "L" => &LETTER,
        "LC" => &CASED_LETTER,
        "M" => &MARK,
        "N" => &NUMBER,
        "P" => &PUNCTUATION,
        "S" => &SYMBOL,
        "Z" => &SEPARATOR,
        "C" => &OTHER,
        _ => return tables::category_table(name).map(Ranges::Table),
    };
    Some(Ranges::Built(built))
}

/// The table of a binary property by its canonical name, such as "Alphabetic" or "White_Space". `None` for any
/// other name. ASCII and Any, which ECMA-262 adds to the properties of Unicode, are not here.
pub(crate) fn binary(name: &str) -> Option<Ranges> {
    if name == "Assigned" {
        return Some(Ranges::Built(&ASSIGNED));
    }
    tables::binary_table(name).map(Ranges::Table)
}

/// The generated table of a binary property, for the callers that test one code point.
pub(crate) fn binary_table(name: &str) -> Option<&'static Table> {
    tables::binary_table(name)
}

/// The table of a Script value by its long name or an alias, such as "Greek" or "Grek". `None` for any other name.
pub(crate) fn script(name: &str) -> Option<Ranges> {
    match name {
        "Unknown" | "Zzzz" => Some(Ranges::Built(&UNKNOWN_SCRIPT)),
        "Katakana_Or_Hiragana" | "Hrkt" => Some(Ranges::Table(&KATAKANA_OR_HIRAGANA)),
        _ => tables::script_table(name).map(Ranges::Table),
    }
}

/// The table of a Script_Extensions value by the long name of the script or an alias. `None` for any other name.
pub(crate) fn script_extensions(name: &str) -> Option<Ranges> {
    match name {
        "Unknown" | "Zzzz" => Some(Ranges::Built(&UNKNOWN_SCRIPT)),
        "Katakana_Or_Hiragana" | "Hrkt" => Some(Ranges::Table(&KATAKANA_OR_HIRAGANA)),
        _ => tables::script_extensions_table(name).map(Ranges::Table),
    }
}

/// The names of every script. Each element is the long name of one script followed by its aliases.
#[cfg(test)]
pub(crate) fn script_names() -> &'static [&'static [&'static str]] {
    tables::SCRIPT_NAMES
}

/// The canonical name of every binary property that has a table, which is every name [`binary`] knows but Assigned.
#[cfg(test)]
pub(crate) fn binary_names() -> &'static [&'static str] {
    tables::BINARY_NAMES
}

/// Maps some of the code points `first`, `first + 1`, ... `first + length - 1` to themselves plus `delta`. The ones
/// it maps are those whose offset from `first` has none of the bits of `mask`, so a mask of 1 takes every other code
/// point.
pub(crate) struct CaseRange {
    pub(super) first: Rune,
    pub(super) delta: i32,
    pub(super) length: u16,
    pub(super) mask: u16,
}

fn map_case(table: &[CaseRange], r: Rune) -> Rune {
    let (mut lo, mut hi) = (0, table.len());
    while lo < hi {
        let mid = (lo + hi) >> 1;
        let row = &table[mid];
        if r < row.first {
            hi = mid;
        } else {
            let offset = r - row.first;
            if offset >= row.length as Rune {
                lo = mid + 1;
            } else if offset & row.mask as Rune == 0 {
                return r + row.delta;
            } else {
                return r;
            }
        }
    }
    r
}

/// The simple case folding of a code point (the C and S rows of CaseFolding.txt), which is the code point itself
/// when it has none. Two code points are equal ignoring case when `fold` gives the same result for both.
pub(crate) fn fold(r: Rune) -> Rune {
    if r < 0x80 {
        if (b'A' as Rune..=b'Z' as Rune).contains(&r) {
            return r + (b'a' - b'A') as Rune;
        }
        return r;
    }
    map_case(tables::CASE_FOLDS, r)
}

/// The simple uppercase mapping of a code point, which is the code point itself when it has none.
pub(crate) fn to_upper(r: Rune) -> Rune {
    if r < 0x80 {
        if (b'a' as Rune..=b'z' as Rune).contains(&r) {
            return r - (b'a' - b'A') as Rune;
        }
        return r;
    }
    map_case(tables::UPPER_CASES, r)
}

/// Every code point that [`fold`] changes, in ascending order.
pub(crate) fn folding() -> Vec<Rune> {
    let mut dst = Vec::new();
    for row in tables::CASE_FOLDS {
        for offset in 0..row.length as Rune {
            if offset & row.mask as Rune == 0 && row.delta != 0 {
                dst.push(row.first + offset);
            }
        }
    }
    dst
}
