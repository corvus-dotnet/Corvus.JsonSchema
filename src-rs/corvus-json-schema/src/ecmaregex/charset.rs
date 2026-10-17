//! Sets of code points, the classes ECMA-262 defines, and the sets of the Unicode properties (`charset.go`).

use std::collections::HashMap;
use std::sync::{Arc, LazyLock, Mutex};

use super::Rune;
use super::ucd;

/// The last Unicode code point.
pub(crate) const MAX_RUNE: Rune = ucd::MAX_RUNE;

/// An immutable set of code points. The ranges are sorted, inclusive, and neither overlap nor touch. The two words
/// of `ascii` repeat the membership of the code points below 128 so that the common case is one bit test.
#[derive(Debug, PartialEq, Eq)]
pub(crate) struct CharSet {
    pub(crate) ranges: Vec<Rune>,
    pub(crate) ascii: [u64; 2],
}

impl CharSet {
    /// Builds a set from inclusive pairs in any order, which may overlap. With `negate` it builds the complement.
    pub(crate) fn new(pairs: &[Rune], negate: bool) -> CharSet {
        let mut idx: Vec<usize> = (0..pairs.len() / 2).collect();
        idx.sort_by_key(|&i| pairs[2 * i]);
        let mut merged: Vec<Rune> = Vec::with_capacity(pairs.len());
        for i in idx {
            let (lo, hi) = (pairs[2 * i], pairs[2 * i + 1]);
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
        if negate {
            let mut inverse = Vec::with_capacity(merged.len() + 2);
            let mut next: Rune = 0;
            for &[lo, hi] in merged.as_chunks::<2>().0 {
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
            merged = inverse;
        }
        let mut ascii = [0u64; 2];
        for &[lo, hi] in merged.as_chunks::<2>().0 {
            if lo >= 128 {
                break;
            }
            let mut c = lo;
            while c <= hi && c < 128 {
                ascii[(c >> 6) as usize] |= 1 << (c & 63);
                c += 1;
            }
        }
        CharSet { ranges: merged, ascii }
    }

    /// [`CharSet::new`], shared.
    pub(crate) fn shared(pairs: &[Rune], negate: bool) -> Arc<CharSet> {
        Arc::new(CharSet::new(pairs, negate))
    }

    /// Whether the set holds the ASCII character.
    #[inline(always)]
    pub(crate) fn has_ascii(&self, c: u8) -> bool {
        (self.ascii[(c >> 6) as usize & 1] >> (c & 63)) & 1 != 0
    }

    /// Whether the set holds the code point.
    #[inline]
    pub(crate) fn contains(&self, r: Rune) -> bool {
        if (0..128).contains(&r) {
            return (self.ascii[(r >> 6) as usize] >> (r & 63)) & 1 != 0;
        }
        let ranges = &self.ranges[..];
        let (mut lo, mut hi) = (0, ranges.len() / 2);
        while lo < hi {
            let mid = (lo + hi) >> 1;
            if r > ranges[2 * mid + 1] {
                lo = mid + 1;
            } else if r < ranges[2 * mid] {
                hi = mid;
            } else {
                return true;
            }
        }
        false
    }

    /// Whether the set holds no code point.
    pub(crate) fn is_empty(&self) -> bool {
        self.ranges.is_empty()
    }
}

// The classes ECMA-262 defines. \d and \w are ASCII only, \s is WhiteSpace and LineTerminator, and . is everything
// but the four line terminators.
pub(crate) const DIGIT_PAIRS: &[Rune] = &[0x30, 0x39];
pub(crate) const WORD_PAIRS: &[Rune] = &[0x30, 0x39, 0x41, 0x5A, 0x5F, 0x5F, 0x61, 0x7A];
pub(crate) const SPACE_PAIRS: &[Rune] = &[
    0x9, 0xD, 0x20, 0x20, 0xA0, 0xA0, 0x1680, 0x1680, 0x2000, 0x200A, 0x2028, 0x2029, 0x202F, 0x202F, 0x205F, 0x205F,
    0x3000, 0x3000, 0xFEFF, 0xFEFF,
];
pub(crate) const LINE_TERMINATOR_PAIRS: &[Rune] = &[0xA, 0xA, 0xD, 0xD, 0x2028, 0x2029];

pub(crate) static DOT_SET: LazyLock<Arc<CharSet>> = LazyLock::new(|| CharSet::shared(LINE_TERMINATOR_PAIRS, true));

/// Whether a byte is an ECMA-262 word character. Every word character is ASCII, so a word boundary can be decided
/// from the bytes on either side of a position.
#[inline(always)]
pub(crate) fn is_word_byte(b: u8) -> bool {
    b.is_ascii_alphanumeric() || b == b'_'
}

/// The short name of a General_Category value, from its long name, an alias or the short name itself.
fn category_name(value: &str) -> Option<&'static str> {
    Some(match value {
        "Letter" | "L" => "L",
        "Lowercase_Letter" | "Ll" => "Ll",
        "Uppercase_Letter" | "Lu" => "Lu",
        "Titlecase_Letter" | "Lt" => "Lt",
        "Cased_Letter" | "LC" => "LC",
        "Modifier_Letter" | "Lm" => "Lm",
        "Other_Letter" | "Lo" => "Lo",
        "Mark" | "Combining_Mark" | "M" => "M",
        "Nonspacing_Mark" | "Mn" => "Mn",
        "Spacing_Mark" | "Mc" => "Mc",
        "Enclosing_Mark" | "Me" => "Me",
        "Number" | "N" => "N",
        "Decimal_Number" | "digit" | "Nd" => "Nd",
        "Letter_Number" | "Nl" => "Nl",
        "Other_Number" | "No" => "No",
        "Punctuation" | "punct" | "P" => "P",
        "Connector_Punctuation" | "Pc" => "Pc",
        "Dash_Punctuation" | "Pd" => "Pd",
        "Open_Punctuation" | "Ps" => "Ps",
        "Close_Punctuation" | "Pe" => "Pe",
        "Initial_Punctuation" | "Pi" => "Pi",
        "Final_Punctuation" | "Pf" => "Pf",
        "Other_Punctuation" | "Po" => "Po",
        "Symbol" | "S" => "S",
        "Math_Symbol" | "Sm" => "Sm",
        "Currency_Symbol" | "Sc" => "Sc",
        "Modifier_Symbol" | "Sk" => "Sk",
        "Other_Symbol" | "So" => "So",
        "Separator" | "Z" => "Z",
        "Space_Separator" | "Zs" => "Zs",
        "Line_Separator" | "Zl" => "Zl",
        "Paragraph_Separator" | "Zp" => "Zp",
        "Other" | "C" => "C",
        "Control" | "cntrl" | "Cc" => "Cc",
        "Format" | "Cf" => "Cf",
        "Surrogate" | "Cs" => "Cs",
        "Private_Use" | "Co" => "Co",
        "Unassigned" | "Cn" => "Cn",
        _ => return None,
    })
}

/// The canonical name of a binary property ECMA-262 lists, from the name or an alias.
fn binary_name(name: &str) -> Option<&'static str> {
    Some(match name {
        "ASCII" => "ASCII",
        "ASCII_Hex_Digit" | "AHex" => "ASCII_Hex_Digit",
        "Alphabetic" | "Alpha" => "Alphabetic",
        "Any" => "Any",
        "Assigned" => "Assigned",
        "Bidi_Control" | "Bidi_C" => "Bidi_Control",
        "Bidi_Mirrored" | "Bidi_M" => "Bidi_Mirrored",
        "Case_Ignorable" | "CI" => "Case_Ignorable",
        "Cased" => "Cased",
        "Changes_When_Casefolded" | "CWCF" => "Changes_When_Casefolded",
        "Changes_When_Casemapped" | "CWCM" => "Changes_When_Casemapped",
        "Changes_When_Lowercased" | "CWL" => "Changes_When_Lowercased",
        "Changes_When_NFKC_Casefolded" | "CWKCF" => "Changes_When_NFKC_Casefolded",
        "Changes_When_Titlecased" | "CWT" => "Changes_When_Titlecased",
        "Changes_When_Uppercased" | "CWU" => "Changes_When_Uppercased",
        "Dash" => "Dash",
        "Default_Ignorable_Code_Point" | "DI" => "Default_Ignorable_Code_Point",
        "Deprecated" | "Dep" => "Deprecated",
        "Diacritic" | "Dia" => "Diacritic",
        "Emoji" => "Emoji",
        "Emoji_Component" | "EComp" => "Emoji_Component",
        "Emoji_Modifier" | "EMod" => "Emoji_Modifier",
        "Emoji_Modifier_Base" | "EBase" => "Emoji_Modifier_Base",
        "Emoji_Presentation" | "EPres" => "Emoji_Presentation",
        "Extended_Pictographic" | "ExtPict" => "Extended_Pictographic",
        "Extender" | "Ext" => "Extender",
        "Grapheme_Base" | "Gr_Base" => "Grapheme_Base",
        "Grapheme_Extend" | "Gr_Ext" => "Grapheme_Extend",
        "Hex_Digit" | "Hex" => "Hex_Digit",
        "IDS_Binary_Operator" | "IDSB" => "IDS_Binary_Operator",
        "IDS_Trinary_Operator" | "IDST" => "IDS_Trinary_Operator",
        "ID_Continue" | "IDC" => "ID_Continue",
        "ID_Start" | "IDS" => "ID_Start",
        "Ideographic" | "Ideo" => "Ideographic",
        "Join_Control" | "Join_C" => "Join_Control",
        "Logical_Order_Exception" | "LOE" => "Logical_Order_Exception",
        "Lowercase" | "Lower" => "Lowercase",
        "Math" => "Math",
        "Noncharacter_Code_Point" | "NChar" => "Noncharacter_Code_Point",
        "Pattern_Syntax" | "Pat_Syn" => "Pattern_Syntax",
        "Pattern_White_Space" | "Pat_WS" => "Pattern_White_Space",
        "Quotation_Mark" | "QMark" => "Quotation_Mark",
        "Radical" => "Radical",
        "Regional_Indicator" | "RI" => "Regional_Indicator",
        "Sentence_Terminal" | "STerm" => "Sentence_Terminal",
        "Soft_Dotted" | "SD" => "Soft_Dotted",
        "Terminal_Punctuation" | "Term" => "Terminal_Punctuation",
        "Unified_Ideograph" | "UIdeo" => "Unified_Ideograph",
        "Uppercase" | "Upper" => "Uppercase",
        "Variation_Selector" | "VS" => "Variation_Selector",
        "White_Space" | "space" => "White_Space",
        "XID_Continue" | "XIDC" => "XID_Continue",
        "XID_Start" | "XIDS" => "XID_Start",
        _ => return None,
    })
}

/// The sets already built for property expressions. A property set can have hundreds of ranges, and schemas tend to
/// repeat the same few. Only an expression ECMA-262 defines is kept, and those are a fixed few hundred, so the
/// cache is bounded.
static PROPERTY_CACHE: LazyLock<Mutex<HashMap<String, Arc<CharSet>>>> = LazyLock::new(|| Mutex::new(HashMap::new()));

/// Resolves the expression between the braces of `\p{...}` to its set. `None` for a name or value that ECMA-262
/// does not define.
pub(crate) fn property_set(expr: &str) -> Option<Arc<CharSet>> {
    if let Some(cached) = PROPERTY_CACHE.lock().unwrap_or_else(|e| e.into_inner()).get(expr) {
        return Some(cached.clone());
    }
    let pairs = property_pairs(expr)?;
    let set = CharSet::shared(&pairs, false);
    PROPERTY_CACHE.lock().unwrap_or_else(|e| e.into_inner()).insert(expr.to_string(), set.clone());
    Some(set)
}

/// The ranges of a property expression. Every range comes from the tables of `ucd`, which hold one fixed version of
/// Unicode, so the answer does not depend on the toolchain.
fn property_pairs(expr: &str) -> Option<Vec<Rune>> {
    if let Some((name, value)) = expr.split_once('=') {
        return match name {
            "General_Category" | "gc" => category_pairs(value),
            "Script" | "sc" => ucd::script(value).map(|t| t.pairs()),
            "Script_Extensions" | "scx" => ucd::script_extensions(value).map(|t| t.pairs()),
            _ => None,
        };
    }
    if let Some(pairs) = category_pairs(expr) {
        return Some(pairs);
    }
    match binary_name(expr)? {
        "ASCII" => Some(vec![0, 0x7F]),
        "Any" => Some(vec![0, MAX_RUNE]),
        canonical => ucd::binary(canonical).map(|t| t.pairs()),
    }
}

fn category_pairs(value: &str) -> Option<Vec<Rune>> {
    ucd::category(category_name(value)?).map(|t| t.pairs())
}
