//! Patterns that regress, the engine for the patterns no other matcher takes, answers differently from ECMA-262
//! when it is given them as they are written. The crate gives it another pattern with the same meaning
//! (`src/pattern/rewrite.rs`). The answers here are V8's.
//!
//! - A repetition inside a repetition. regress loses the iteration count of the inner one when it comes back into
//!   it by backtracking. It then gave wrong answers, and for a body that can match nothing it did not end:
//!   `{"pattern": "^(?!-)(?:(?:a*)*)*x"}` on `"a"` took memory until the process was stopped (0.1.0 to 0.1.6).
//! - A reference to a group from inside that group, which read a value the group does not have yet.
//! - Two groups with one name, where a reference to the name matched nothing.
//! - A quantifier on a word boundary, which is not valid and was accepted.
//! - A case-insensitive group in a pattern that is valid only without the `u` flag, and `\W` in a class of one
//!   with it.
//! - A pattern that is valid only without the `u` flag, which matches UTF-16 code units. Before 0.1.7 it was
//!   matched by character, so `^\-?.$` matched a character beyond the Basic Multilingual Plane.
//! - A case-insensitive reference in such a pattern, which regress compares by a folding of its own.
//!
//! One class is not regress's: `\B` beside a character of several bytes, which the `regex` crate answered.

use corvus_json_schema::{CompileOptions, compile, compile_with};
use serde_json::json;

/// Every text of up to three characters over `a`, `x` and `-`, and seven longer ones: 47 texts.
fn texts() -> Vec<String> {
    let mut all = vec![String::new()];
    let mut last = all.clone();
    for _ in 0..3 {
        last = last.iter().flat_map(|t| ["a", "x", "-"].map(|c| format!("{t}{c}"))).collect();
        all.extend(last.iter().cloned());
    }
    all.extend(["aaaa", "xaxa", "aaax", "xxxa", "a-a-", "ax-ax-", "aa-xx-a"].map(String::from));
    all
}

/// Checks each pattern against its answers, one `T` or `F` for each of [`texts`]: V8's
/// `new RegExp(pattern, "u").test(text)`, or without the flag for a pattern that is valid only without it.
fn check(cases: &[(&str, &str)]) {
    let texts = texts();
    let mut wrong = Vec::new();
    for &(pattern, answers) in cases {
        assert_eq!(answers.len(), texts.len(), "{pattern}");
        let v = compile(&json!({ "pattern": pattern })).unwrap_or_else(|e| panic!("{pattern}: {e:?}"));
        for (text, want) in texts.iter().zip(answers.chars()) {
            if v.is_valid(&json!(text)) != (want == 'T') {
                wrong.push(format!("{pattern} on {text:?}: want {want}"));
            }
        }
    }
    assert!(wrong.is_empty(), "{} wrong:\n{}", wrong.len(), wrong.join("\n"));
}

/// A repetition inside a repetition, where the inner one can be come back into: its body has an alternation or a
/// repetition of its own. Before 0.1.7 the first three, and the patterns from `(((?=x)|){3}){2}x$` to
/// `((?:x|(?<!^)){4,5}|x){2}$`, did not end on some text here, and the others gave a wrong answer on one.
#[rustfmt::skip]
const REPETITIONS: &[(&str, &str)] = &[
    ("^(?!-)(?:(?:a*)*)*x", "FFTFFTFTTTFFFFTFTTTFFFTTTTTTTTTFFFFFFFFFFTTTFTF"),
    ("^(?!-)(?:(?:a|)?){2}x", "FFTFFTFTTTFFFFTFTTTFFFTTTTTTTTTFFFFFFFFFFTFTFTF"),
    ("^(?!-)(?:(?:a?)?){3}$", "TTFFTFFFFFFFFTFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"),
    ("^(?!-)(?:(?:(?:a){1,}){1}){3}$", "FFFFFFFFFFFFFTFFFFFFFFFFFFFFFFFFFFFFFFFFTFFFFFF"),
    ("^(?!-)(?:(?:a|x){1,2}-)+$", "FFFFFFTFFTFFFFFTFFTFFFFFTFFTFFFFFFFFFFFFFFFFTTF"),
    ("^(?!-)(?:(?:a|x){1,3}?-)+$", "FFFFFFTFFTFFFFFTFFTFFFFFTFFTFFFFFFFFFFFFFFFFTTF"),
    ("^(?!-)(?:(?:a|x-?){2}-)*$", "TFFFFFFFFFFFFFFTFFTFFFFFTFFTFFFFFFFFFFFFFFFFFTF"),
    ("^(?!-)(?:(?:a|x)+-)+$", "FFFFFFTFFTFFFFFTFFTFFFFFTFFTFFFFFFFFFFFFFFFFTTF"),
    ("^(?!-)(?:(?:a|^)+x)+$", "FFTFFTFFFFFFFFTFFFFFFFFTFFFFFFFFFFFFFFFFFFTFFFF"),
    (r#"^(?!-)(?:(?:a|\b){2,}x)+$"#, "FFTFFTFFFFFFFFTFFFFFFFFFFFFFFFFFFFFFFFFFFFTFFFF"),
    (r#"^(?!-)(?:(?:a|\b){2,}?x)+$"#, "FFTFFTFFFFFFFFTFFFFFFFFFFFFFFFFFFFFFFFFFFFTFFFF"),
    ("^(?!-)(?:a(?:x[ax]{1,2})?-?)+$", "FTFFTFTFFFFFFTFTTTFTFFFFFFFFFFFFFFFFFFFFTFFFTFF"),
    ("(((?=x)|){3}){2}x$", "FFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFFTFFFF"),
    (r#"((?:\1|(a|)|.)*|a())*x"#, "FFTFFTFTTTFTFFTFTTTFTFTTTTTTTTTFTFTTTFTFFTTTFTT"),
    (r#"^(?:(|\B\1){2}|){3,5}$"#, "TFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"),
    ("(?:(-|){1}?|(?!$)){1,}?x$", "FFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFFTFFFF"),
    (r#"(\b([ax]?){3}|x|\1)*?x$"#, "FFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFFTFFFF"),
    (r#"^(|(a|$|\1(?:))+())+a"#, "FTFFTTTFFFFFFTTTTTTTTTFFFFFFFFFFFFFFFFFFTFTFTTT"),
    (r#"^((x|()|\2){0,}|){4}?x"#, "FFTFFFFTTTFFFFFFFFFFFFTTTTTTTTTFFFFFFFFFFTFTFFF"),
    ("^(?:|((?<!x)|){4,5})+x$", "FFTFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"),
    ("((?:x|(?<!^)){4,5}|x){2}$", "FTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTTT"),
    (r#"(?:a|){3}(^(?:(?:|[^a])+(?<![^a]\1)|[ax]){1}?|x)+$"#, "TTTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFTFFFTFFFF"),
    (r#"([^a][^a]|.(?:\1\1|a?[ax]){2}){4}a$"#, "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFT"),
    (r#"^(\b([^a]|\2){1,}){4}?a$"#, "FTFFFFFTFFFFFFFFFFFFFFFFFTFFTFFFFFFFFFFFFFFTFFF"),
    (r#"^(a|(?:(?<=a)\1|){3}?){2}a$"#, "FTFFTFFFFFFFFTFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"),
];

/// A reference from outside a repetition to a group inside it reads the last iteration that ran, and from inside
/// it, the group of its own iteration. Before 0.1.7 the first three did not end on some text here. regress
/// answers the others as they are written, and they are here for what a reference reads once the repetition is
/// written as copies.
#[rustfmt::skip]
const REFERENCES_TO_A_REPETITION: &[(&str, &str)] = &[
    (r#"^(?:(?:(a)|x|){1,2}\1-)+$"#, "FFFTFFFFFTFFTFFTFFTFFFFFFFFTFFTFFFFFTFFTFFFFFTF"),
    (r#"^(?:(?:(a)|x|)+\1-)+$"#, "FFFTFFFFFTFFTFFTFFTFFFFFFFFTFFTFFFFFTFFTFFFFFTF"),
    (r#"^(?:(?:(a)|x?){2,}\1-)+$"#, "FFFTFFTFFTFFTFFTFFTFFTFFFFFTFFTFFTFFTFFTFFFFTTF"),
    (r#"^(?:(?:(a)|x){1,2}\1-)+$"#, "FFFFFFFFFTFFFFFTFFTFFFFFFFFTFFFFFFFFFFFFFFFFFTF"),
    (r#"^(?:(?:(a)|(x))+\1\2-)*$"#, "TFFFFFFFFFFFFFFTFFFFFFFFFFFTFFFFFFFFFFFFFFFFFFF"),
    (r#"^(?:(?:(a)|x|-){0,2}?\1x)+$"#, "FFTFFFFFTFFTFFTFFTFFTFFFFFTFFTFFFFFTFFTFFFTFFFF"),
    (r#"^(?:(?:(a)|x){2,}\1-)+$"#, "FFFFFFFFFFFFFFFFFFTFFFFFFFFTFFFFFFFFFFFFFFFFFTF"),
    (r#"^(?:(?:(?:(a)|x){1,2}-){1,2}\1)+$"#, "FFFFFFFFFTFFFFFFFFTTFFFFFFFTFFFFFFFFFFFFFFFFFTF"),
];

/// The same inside a lookbehind, which matches from its end: the iterations run from the last one written. Before
/// 0.1.7 the first five did not end on some text here, and the next three gave a wrong answer on one.
#[rustfmt::skip]
const REPETITIONS_IN_A_LOOKBEHIND: &[(&str, &str)] = &[
    ("(?<=^(?:(?:a|){1,2}){3})-", "FFFTFFTFFFTTTFFTFFFTTTFFFFFFFFFTTTTTTTTTFFFFTFT"),
    ("(?<=^(?:x(?:a*x?){1,2})+)-", "FFFFFFFFFTFFFFFFFFFFFFFFTFFTTTTFFFFFFFFFFFFFFFF"),
    ("(?<=^(?:(?:a|)+){3})-", "FFFTFFTFFFTTTFFTFFFTTTFFFFFFFFFTTTTTTTTTFFFFTFT"),
    ("(?<=^(?:x(?:(a)|x|){1,2})+)-", "FFFFFFFFFTFFFFFFFFFFFFFFTFFTTTTFFFFFFFFFFFFFFFF"),
    ("(?<=^(?:x(?:a?x?){2})+)-", "FFFFFFFFFTFFFFFFFFFFFFFFTFFTTTTFFFFFFFFFFFFFFFF"),
    ("(?<=^(?:(?:a+){1,2}-?){2})x", "FFFFFFFFFFFFFFTFFFFFFFFFFFFFFFFFFFFFFFFFFFTFFFT"),
    ("(?<=^(?:(?:a+){1}-?){2})x", "FFFFFFFFFFFFFFTFFFFFFFFFFFFFFFFFFFFFFFFFFFTFFFT"),
    ("(?<=^(?:(?:(?:a){1,}){1}){3})x", "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFTFFFF"),
    ("(?<=^(?:(?:a|)*-?)*x)-", "FFFFFFFFFTFFFFFFFFTFFFFFFFFFTTTFFFFFTFFFFFFFFTF"),
    (r#"(?<=(?:(?:(a)|x){1,2}\1)+)-"#, "FFFFFFTFFTFFFFFTFFTTTTFFTFFTTTTFFTFFTFFFFFFFTTT"),
    (r#"(?<=^(?:\1(?:(a)|x){1,2}-)+)x"#, "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFTFFFFFFFFFFFFFFFFT"),
    ("(?<=^(?:(?:a|x){1,2}?-)+)a", "FFFFFFFFFFFFFFFFFFFTFFFFFFFFTFFFFFFFFFFFFFFFTTT"),
];

/// Patterns that are valid only without the `u` flag (`\-` is not an escape with it). Before 0.1.7 the first two
/// did not end on some text here.
#[rustfmt::skip]
const REPETITIONS_WITHOUT_THE_U_FLAG: &[(&str, &str)] = &[
    (r#"^(?:\-|(?:a|){1,2}x)+$"#, "FFTTFTFFTTFTTFTFFTTFFFFTFFTTFTTFTFFTTFTTFFFFFTF"),
    (r#"^(?:\-|(?:(a)|x|){1,2}\1x)+$"#, "FFTTFFFFTTFTTFTFFTFFFFFFFFTTFTTFFFFTTFTTFFTFFFF"),
    (r#"^(?:\-)?(?:(?:a|)+x)+$"#, "FFTFFTFFTFFTFFTFFTFFFFFTFFTFFFFFTFFTFFFFFFTFFFF"),
    (r#"^(?:\-|(?:(a)|x){1,2}\1)+$"#, "FFTTTTFFTTFTTTTTFTTFFFTTFFTTFTTTTFFTTFTTTFTFFTF"),
];

/// A reference to a group from inside the group matches nothing: the group has no value until it closes.
#[rustfmt::skip]
const REFERENCES_FROM_INSIDE_THE_GROUP: &[(&str, &str)] = &[
    (r#"(x|\1)x"#, "FFTFFTFTTTFTFFTFTTTFTFTTTTTTTTTFTFTTTFTFFTTTFTT"),
    (r#"^(a|\1a)$"#, "FTFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"),
    (r#"(a?\1)a"#, "FTFFTTTTFFTFFTTTTTTTTTTTTTFFTFFTTTTFFTFFTTTTTTT"),
    (r#"()([x]|\2)x"#, "FFTFFTFTTTFTFFTFTTTFTFTTTTTTTTTFTFTTTFTFFTTTFTT"),
    (r#"^(?:(a)|(x\2))+$"#, "FTTFTTFTTFFFFTTFTTFFFFTTFTTFFFFFFFFFFFFFTTTTFFF"),
    (r#"^(?:(a|x\1)-)+$"#, "FFFFFFTFFTFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFTFF"),
    (r#"(?<=(\1x|a))-"#, "FFFFFFTFFTFFFFFTFFTTTTFFTFFTTTTFFTFFTFFFFFFFTTT"),
];

/// Two groups with one name, in separate alternatives. A reference to the name reads the one that matched.
#[rustfmt::skip]
const GROUPS_WITH_ONE_NAME: &[(&str, &str)] = &[
    (r#"^(?:(?<n>a)|(?<n>x))\k<n>$"#, "FFFFTFFFTFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"),
    (r#"^(?:(?<n>a)|(?<n>x))+\k<n>$"#, "FFFFTFFFTFFFFTFFFTFFFFTFFFTFFFFFFFFFFFFFTFFFFFF"),
    (r#"(?:(?<n>a)|(?<n>x)){2}\k<n>"#, "FFFFFFFFFFFFFTFFFTFFFFTFFFTFFFFFFFFFFFFFTFTTFFF"),
    (r#"^(?:(?<n>a)|(?<n>x)|-)*\k<n>$"#, "TFFTTFTFTTFFTTFTFTTFFTTFTFTTFFTTFTFTTFFTTFFFTTF"),
    (r#"^(?:(?<m>a)|(?<m>x))(?:(?<n>a)|(?<n>x))\k<m>\k<n>$"#, "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFTTFFFFF"),
];

#[test]
fn a_repetition_inside_a_repetition_keeps_its_count() {
    check(REPETITIONS);
}

#[test]
fn a_reference_to_a_group_of_a_repetition_reads_the_last_iteration() {
    check(REFERENCES_TO_A_REPETITION);
}

#[test]
fn a_repetition_inside_a_repetition_in_a_lookbehind() {
    check(REPETITIONS_IN_A_LOOKBEHIND);
}

#[test]
fn a_repetition_inside_a_repetition_without_the_u_flag() {
    check(REPETITIONS_WITHOUT_THE_U_FLAG);
}

#[test]
fn a_reference_from_inside_its_group_matches_nothing() {
    check(REFERENCES_FROM_INSIDE_THE_GROUP);
}

#[test]
fn a_reference_to_a_name_of_two_groups_reads_the_one_that_matched() {
    check(GROUPS_WITH_ONE_NAME);
}

/// The patterns of `patternProperties` and `propertyNames` go the same way.
#[test]
fn a_repetition_inside_a_repetition_in_a_pattern_of_property_names() {
    let v = compile(&json!({ "patternProperties": { "^(?!-)(?:(?:a*)*)*x": false } })).unwrap();
    assert!(v.is_valid(&json!({ "a": 1, "-x": 1 })));
    assert!(!v.is_valid(&json!({ "aax": 1 })));
    let v = compile(&json!({ "propertyNames": { "pattern": r"^(?:(?:(a)|x){1,2}\1-)+$" } })).unwrap();
    assert!(v.is_valid(&json!({ "xaa-": 1, "x-": 1 })));
    assert!(!v.is_valid(&json!({ "xa-": 1 })));
}

/// An assertion takes no quantifier. regress allows one on `\b` and `\B`.
#[test]
fn a_quantifier_on_a_word_boundary_is_not_valid() {
    let format = CompileOptions { assert_format: Some(true), ..CompileOptions::default() };
    let format = compile_with(&json!({ "format": "regex" }), &format).unwrap();
    for pattern in [r"\b+", r"\B*", r"a\b?", r"^\b{2}a", r"\B{1,}?x", r"(a\b*)", r"[a]\b+\&", r"(?=a\B+)"] {
        let e = compile(&json!({ "pattern": pattern })).expect_err(pattern);
        assert_eq!(e.message(), format!("Invalid regular expression '{pattern}' in pattern."));
        assert!(compile(&json!({ "patternProperties": { pattern: true } })).is_err(), "{pattern}");
        assert!(!format.is_valid(&json!(pattern)), "{pattern}");
    }
    // A quantifier on a group that holds one, an escaped backslash before a `b`, and a backspace in a class.
    for (pattern, text) in [(r"(?:\b)+a", "a"), (r"\\b+", r"\bb"), (r"[\b+]", "+"), (r"a\b", "a"), (r"\b{", "a{")] {
        let v = compile(&json!({ "pattern": pattern })).unwrap_or_else(|e| panic!("{pattern}: {e:?}"));
        assert!(v.is_valid(&json!(text)), "{pattern} on {text:?}");
    }
    for pattern in [r"(?:\b)+a", r"\\b+", r"[\b+]", r"a\b"] {
        assert!(format.is_valid(&json!(pattern)), "{pattern}");
    }
}

/// Each pattern has a case-insensitive group, the texts it matches and the texts it does not. The answers are V8's
/// for the same pattern with the group taken off and the `i` flag set, which ECMA-262 gives the same meaning. (V8
/// 13.6 itself answers some of these groups differently from the flag.) The patterns with `\&` are valid only
/// without the `u` flag, where two characters are equal when the uppercase forms of their UTF-16 code units are:
/// `s` is not U+017F there, and `k` is not U+212A. With the flag, `\W` does not hold the characters that fold to a
/// word character.
#[rustfmt::skip]
const CASE_INSENSITIVE: &[(&str, &[&str], &[&str])] = &[
    (r"^(?i:s)$\&?", &["s", "S"], &["\u{17f}", "k", "K", "\u{212a}", "i", "I", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", "a", "0", "_", ""]),
    (r"^(?i:\u017f)$\&?", &["\u{17f}"], &["s", "S", "k", "K", "\u{212a}", "i", "I", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", "a", "0", "_", ""]),
    (r"^(?i:[a-z])$\&?", &["s", "S", "k", "K", "i", "I", "a"], &["\u{17f}", "\u{212a}", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", "0", "_", ""]),
    (r"^(?i:[^a-z])$\&?", &["\u{17f}", "\u{212a}", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", "0", "_"], &["s", "S", "k", "K", "i", "I", "a", ""]),
    (r"^(?i:\w)$\&?", &["s", "S", "k", "K", "i", "I", "a", "0", "_"], &["\u{17f}", "\u{212a}", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", ""]),
    (r"^(?i:\W)$\&?", &["\u{17f}", "\u{212a}", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-"], &["s", "S", "k", "K", "i", "I", "a", "0", "_", ""]),
    (r"^(?i:[\W])$\&?", &["\u{17f}", "\u{212a}", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-"], &["s", "S", "k", "K", "i", "I", "a", "0", "_", ""]),
    (r"^(?i:[^\W])$\&?", &["s", "S", "k", "K", "i", "I", "a", "0", "_"], &["\u{17f}", "\u{212a}", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", ""]),
    (r"^(?i:\u0131)$\&?", &["\u{131}"], &["s", "S", "\u{17f}", "k", "K", "\u{212a}", "i", "I", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", "a", "0", "_", ""]),
    (r"^(?i:i)$\&?", &["i", "I"], &["s", "S", "\u{17f}", "k", "K", "\u{212a}", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", "a", "0", "_", ""]),
    (r"^(?i:\u1f80)$\&?", &["\u{1f80}"], &["s", "S", "\u{17f}", "k", "K", "\u{212a}", "i", "I", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f88}", "-", "a", "0", "_", ""]),
    (r"^(?i:\u00b5)$\&?", &["\u{b5}", "\u{3bc}", "\u{39c}"], &["s", "S", "\u{17f}", "k", "K", "\u{212a}", "i", "I", "\u{131}", "\u{1f80}", "\u{1f88}", "-", "a", "0", "_", ""]),
    (r"^(?i:[k-s])$\&?", &["s", "S", "k", "K"], &["\u{17f}", "\u{212a}", "i", "I", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", "a", "0", "_", ""]),
    (r"^(?i:[\u212a])$\&?", &["\u{212a}"], &["s", "S", "\u{17f}", "k", "K", "i", "I", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", "a", "0", "_", ""]),
    (r"(?i:\Bs)\&?", &[], &["s", "S", "\u{17f}", "k", "K", "\u{212a}", "i", "I", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", "a", "0", "_", ""]),
    (r"^(?i:[\W])$", &["\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-"], &["s", "S", "\u{17f}", "k", "K", "\u{212a}", "i", "I", "a", "0", "_", ""]),
    (r"^(?i:[^\W])$", &["s", "S", "\u{17f}", "k", "K", "\u{212a}", "i", "I", "a", "0", "_"], &["\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", ""]),
    (r"^(?i:[^\W\d]+)$", &["s", "S", "\u{17f}", "k", "K", "\u{212a}", "i", "I", "a", "_"], &["\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", "0", ""]),
    (r"^(?i:[\W_])$", &["\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", "_"], &["s", "S", "\u{17f}", "k", "K", "\u{212a}", "i", "I", "a", "0", ""]),
    (r"^(?i:s)$", &["s", "S", "\u{17f}"], &["k", "K", "\u{212a}", "i", "I", "\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", "a", "0", "_", ""]),
    (r"^(?i:\W)$", &["\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-"], &["s", "S", "\u{17f}", "k", "K", "\u{212a}", "i", "I", "a", "0", "_", ""]),
    (r"^(?i:[\w])$", &["s", "S", "\u{17f}", "k", "K", "\u{212a}", "i", "I", "a", "0", "_"], &["\u{131}", "\u{b5}", "\u{3bc}", "\u{39c}", "\u{1f80}", "\u{1f88}", "-", ""]),
];

/// Checks each pattern against the texts it matches and the texts it does not.
fn check_texts(cases: &[(&str, &[&str], &[&str])]) {
    let mut wrong = Vec::new();
    for &(pattern, matches, fails) in cases {
        let v = compile(&json!({ "pattern": pattern })).unwrap_or_else(|e| panic!("{pattern}: {e:?}"));
        for (texts, want) in [(matches, true), (fails, false)] {
            for &text in texts {
                if v.is_valid(&json!(text)) != want {
                    wrong.push(format!("{pattern} on {text:?}: want {want}"));
                }
            }
        }
    }
    assert!(wrong.is_empty(), "{} wrong:\n{}", wrong.len(), wrong.join("\n"));
}

#[test]
fn a_case_insensitive_group_compares_as_its_grammar_says() {
    check_texts(CASE_INSENSITIVE);
}

/// Patterns that are valid only without the `u` flag (each has an identity escape the flag does not allow), with
/// the texts V8 matches without the flag and the texts it does not. Such a pattern is its UTF-16 code units, and
/// so is the text: a character beyond the Basic Multilingual Plane is two, a dot takes one of them, a quantifier
/// after such a character repeats its second code unit, and a class holds each of the two. `\u{3}` is `u` three
/// times, and the escape of a surrogate is that code unit. Before 0.1.7 the pattern and the text were read by
/// character.
#[rustfmt::skip]
const BY_CODE_UNIT: &[(&str, &[&str], &[&str])] = &[
    ("^\\-?.$", &["a", "u", "0", "{", "-"], &["", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "uuu", "ab"]),
    ("^\\-?..$", &["\u{10400}", "\u{10428}", "\u{1f600}", "ab"], &["", "a", "\u{10400}\u{10400}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "-"]),
    ("^\\-?\u{10400}*$", &["\u{10400}"], &["", "a", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?\u{10400}+$", &["\u{10400}"], &["", "a", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?\u{10400}?$", &["\u{10400}"], &["", "a", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?\u{10400}{2}$", &[], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?[\u{1f600}]$", &[], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?[\u{1f600}]{2}$", &["\u{1f600}"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?[^\u{1f600}]$", &["a", "u", "0", "{", "-"], &["", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "uuu", "ab"]),
    ("^\\-?[^\u{1f600}]+$", &["a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "u", "uuu", "0", "{", "ab", "-"], &["", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a"]),
    ("^\\-?[a-\u{10400}]+$", &["a", "\u{10400}", "\u{10400}\u{10400}", "u", "uuu", "{", "ab"], &["", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "0", "-"]),
    ("^\\-?\\ud83d\\ude00$", &["\u{1f600}"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?\\ud83d+\\ude00$", &["\u{1f600}"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?\\ud83d\\ude00+$", &["\u{1f600}"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?[\\ud83d\\ude00]$", &[], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?[\\ud83d\\ude00]{2}$", &["\u{1f600}"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?[\\ud800-\\udbff][\\udc00-\\udfff]$", &["\u{10400}", "\u{10428}", "\u{1f600}"], &["", "a", "\u{10400}\u{10400}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?\\ud801\\ud83d$", &[], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?[\\ud801\\ud83d]\\ude00$", &["\u{1f600}"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?\\u{3}$", &["uuu"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "0", "{", "ab", "-"]),
    ("^\\-?[\\u{3}]$", &["u", "{"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "uuu", "0", "ab", "-"]),
    ("^\\-?[\\u{10400}]+$", &["u", "uuu", "0", "{"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "ab", "-"]),
    ("(?<=\\ud83d)\\ude00\\-?$", &["\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("(?<!\\ud83d)\\ude00\\-?", &[], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?\\S\\B", &["\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "\u{1f600}a", "uuu", "{", "ab", "-"], &["", "a", "a\u{1f600}", "u", "0"]),
    ("^\\-?\\W{2}$", &["\u{10400}", "\u{10428}", "\u{1f600}"], &["", "a", "\u{10400}\u{10400}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?(.)\\1$", &[], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?(?:.{2})+$", &["\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "ab"], &["", "a", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "-"]),
    ("^\\-?\\S{1,3}$", &["a", "\u{10400}", "\u{10428}", "\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"], &["", "\u{10400}\u{10400}", "\u{1f600}\u{1f600}"]),
    ("a\\&?.$", &["ab"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "-"]),
    ("\\ud83d\\-?", &["\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "u", "uuu", "0", "{", "ab", "-"]),
    ("\\ude00\\-?$", &["\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}"], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("^\\-?[^a]{2,3}$", &["\u{10400}", "\u{10428}", "\u{1f600}", "uuu"], &["", "a", "\u{10400}\u{10400}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "0", "{", "ab", "-"]),
    ("(?i:^\\-?\u{10400}$)", &["\u{10400}"], &["", "a", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("(?i:^\\-?[\u{10400}]{2}$)", &["\u{10400}"], &["", "a", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
    ("(?i:^\\-?.A$)", &[], &["", "a", "\u{10400}", "\u{10400}\u{10400}", "\u{10428}", "\u{1f600}", "\u{1f600}\u{1f600}", "a\u{1f600}", "\u{1f600}a", "u", "uuu", "0", "{", "ab", "-"]),
];

#[test]
fn a_pattern_without_the_u_flag_matches_utf16_code_units() {
    check_texts(BY_CODE_UNIT);
}

/// Case-insensitive references, with V8's answers for the pattern with the `i` flag in place of the group. The
/// patterns with `\&` are valid only without the `u` flag. There two code units are equal when their uppercase
/// forms are: `s` is not U+017F, `i` is not U+0131, and U+1F80 is not U+1F88, each of which regress's own
/// comparison takes for equal (before 0.1.7 that comparison was the answer). The last five have the flag, where
/// the comparison is by simple case folding and regress has it.
#[rustfmt::skip]
const CASE_INSENSITIVE_REFERENCES: &[(&str, &[&str], &[&str])] = &[
    ("(?i:^(.)\\1$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(\\w)\\1$\\&?)", &["ss", "sS", "Ss", "ii", "iI", "kk", "kK", "aa", "aA", "11"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "ab", "1a", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(\\W)\\1$\\&?)", &["\u{17f}\u{17f}", "\u{131}\u{131}", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "--"], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "Ss", "ii", "iI", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "aa", "aA", "ab", "11", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^([a-z])\\1$\\&?)", &["ss", "sS", "Ss", "ii", "iI", "kk", "kK", "aa", "aA"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "ab", "11", "1a", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^([^s])\\1$\\&?)", &["\u{17f}\u{17f}", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "Ss", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(\\d)\\1$\\&?)", &["11"], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "1a", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(..)\\1$\\&?)", &["isis", "\u{131}S\u{131}s", "SISI", "sIsi", "x\u{131}X\u{131}"], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "11", "1a", "--", "is\u{131}s", "\u{131}sIS", "\u{17f}is\u{131}", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(.+)\\1$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--", "isis", "\u{131}S\u{131}s", "SISI", "sIsi", "is\u{1f80}IS\u{1f80}", "x\u{131}X\u{131}"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "is\u{131}s", "\u{131}sIS", "\u{17f}is\u{131}", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f88}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(.+?)\\1+$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--", "isis", "\u{131}S\u{131}s", "SISI", "sIsi", "sss", "\u{17f}\u{17f}\u{17f}", "iii", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "x\u{131}X\u{131}"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "is\u{131}s", "\u{131}sIS", "\u{17f}is\u{131}", "s\u{17f}s", "i\u{131}i", "is\u{1f80}IS\u{1f88}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(.)(.)\\2\\1$\\&?)", &[], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "11", "1a", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(?:(.)\\1)+$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(.)\\1{2}$\\&?)", &["sss", "\u{17f}\u{17f}\u{17f}", "iii", "aAa", "kKk"], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "11", "1a", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "s\u{17f}s", "i\u{131}i", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(\\u0131)\\1$\\&?)", &["\u{131}\u{131}"], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "11", "1a", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(\\u017f)\\1+$\\&?)", &["\u{17f}\u{17f}", "\u{17f}\u{17f}\u{17f}"], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "Ss", "ii", "iI", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "11", "1a", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(i)\\1$\\&?)", &["ii", "iI"], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "11", "1a", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(s)\\1+$\\&?)", &["ss", "sS", "Ss", "sss"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "ii", "iI", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "11", "1a", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(?<n>.)\\k<n>$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(.{1,2}?)\\1$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--", "isis", "\u{131}S\u{131}s", "SISI", "sIsi", "x\u{131}X\u{131}"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "is\u{131}s", "\u{131}sIS", "\u{17f}is\u{131}", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^..(?<=\\1(.))$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^.+(?<=\\1(..))$\\&?)", &["isis", "\u{131}S\u{131}s", "SISI", "sIsi", "x\u{131}X\u{131}"], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "11", "1a", "--", "is\u{131}s", "\u{131}sIS", "\u{17f}is\u{131}", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^.(?<=(.))\\1$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(?=(.)).\\1$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(?:(.)|x)+\\1$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--", "sss", "\u{17f}\u{17f}\u{17f}", "iii", "aAa", "kKk"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "s\u{17f}s", "i\u{131}i", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(.)(?!\\1).$\\&?)", &["s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a"], &["", "ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:(.)\\1\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--", "sss", "\u{17f}\u{17f}\u{17f}", "iii", "aAa", "kKk"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "s\u{17f}s", "i\u{131}i", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(?:(.)(?:\\1|-))+$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^([is\\u0131\\u017f]+)\\1$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "isis", "\u{131}S\u{131}s", "SISI", "sIsi"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "11", "1a", "--", "is\u{131}s", "\u{131}sIS", "\u{17f}is\u{131}", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(\\S\\S\\S)\\1$\\&?)", &["is\u{1f80}IS\u{1f80}"], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "11", "1a", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(.*?)(.)\\2\\1$\\&?)", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("^(.)(?i:\\1)$\\&?", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("^(.+)(?i:\\1)$\\&?", &["ss", "sS", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--", "isis", "\u{131}S\u{131}s", "SISI", "sIsi", "is\u{1f80}IS\u{1f80}", "x\u{131}X\u{131}"], &["", "s\u{17f}", "\u{17f}s", "\u{17f}S", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "k\u{212a}", "\u{212a}k", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{df}\u{1e9e}", "ab", "1a", "is\u{131}s", "\u{131}sIS", "\u{17f}is\u{131}", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f88}", "x\u{131}Xi", "xiX\u{131}"]),
    ("^(..)(?i:\\1)$\\&?", &["isis", "\u{131}S\u{131}s", "SISI", "sIsi", "x\u{131}X\u{131}"], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "11", "1a", "--", "is\u{131}s", "\u{131}sIS", "\u{17f}is\u{131}", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}Xi", "xiX\u{131}"]),
    ("^.(?<=(?i:\\1)(.))$\\&?", &[], &["", "ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "ab", "11", "1a", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(.)\\1$)", &["ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(\\w)\\1$)", &["ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "aa", "aA", "11"], &["", "i\u{131}", "\u{131}i", "\u{131}I", "\u{131}\u{131}", "I\u{130}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "ab", "1a", "--", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^(?:(.)\\1)+$)", &["ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:^..(?<=\\1(.))$)", &["ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--"], &["", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "i\u{131}i", "aAa", "kKk", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
    ("(?i:(.)\\1)", &["ss", "sS", "s\u{17f}", "\u{17f}s", "\u{17f}S", "\u{17f}\u{17f}", "Ss", "ii", "iI", "\u{131}\u{131}", "kk", "kK", "k\u{212a}", "\u{212a}k", "\u{212a}\u{212a}", "\u{1f80}\u{1f80}", "\u{1f80}\u{1f88}", "\u{1f88}\u{1f80}", "\u{1f88}\u{1f88}", "\u{df}\u{df}", "\u{df}\u{1e9e}", "\u{b5}\u{3bc}", "\u{b5}\u{39c}", "\u{3bc}\u{39c}", "aa", "aA", "11", "--", "sss", "s\u{17f}s", "\u{17f}\u{17f}\u{17f}", "iii", "aAa", "kKk"], &["", "i\u{131}", "\u{131}i", "\u{131}I", "I\u{130}", "ab", "1a", "isis", "is\u{131}s", "\u{131}sIS", "\u{131}S\u{131}s", "\u{17f}is\u{131}", "SISI", "sIsi", "i\u{131}i", "is\u{1f80}IS\u{1f80}", "is\u{1f80}IS\u{1f88}", "x\u{131}X\u{131}", "x\u{131}Xi", "xiX\u{131}"]),
];

#[test]
fn a_case_insensitive_reference_compares_as_its_grammar_says() {
    check_texts(CASE_INSENSITIVE_REFERENCES);
}

/// `\B` in patterns the `regex` crate would otherwise match. Its `(?-u:\B)` also holds between the bytes of a
/// character that takes several, and `is_match` then said no where there is a match: `s*?\B|.{2}.$` on
/// `k\u{17f}k` (before 0.1.7). The answers are V8's with the `u` flag.
#[rustfmt::skip]
const NOT_A_WORD_BOUNDARY: &[(&str, &[&str], &[&str])] = &[
    ("s*?\\B|.{2}.$", &["", "k\u{17f}k", "\u{17f}", "\u{e9}", "a\u{e9}", "\u{e9}a", "ab", "a b", "\u{17f}\u{17f}", "\u{1f600}", "a\u{1f600}", "\u{1f600}\u{1f600}", "k k", "abc", "a\u{e9}b", "\u{e9}\u{e9}a", "-", "a-"], &["a"]),
    ("s*?\\B|.{2}.{1,2}$", &["", "k\u{17f}k", "\u{17f}", "\u{e9}", "a\u{e9}", "\u{e9}a", "ab", "a b", "\u{17f}\u{17f}", "\u{1f600}", "a\u{1f600}", "\u{1f600}\u{1f600}", "k k", "abc", "a\u{e9}b", "\u{e9}\u{e9}a", "-", "a-"], &["a"]),
    ("\\B", &["", "\u{17f}", "\u{e9}", "a\u{e9}", "\u{e9}a", "ab", "\u{17f}\u{17f}", "\u{1f600}", "a\u{1f600}", "\u{1f600}\u{1f600}", "abc", "\u{e9}\u{e9}a", "-", "a-"], &["k\u{17f}k", "a", "a b", "k k", "a\u{e9}b"]),
    ("a\\B", &["ab", "abc"], &["", "k\u{17f}k", "\u{17f}", "\u{e9}", "a\u{e9}", "\u{e9}a", "a", "a b", "\u{17f}\u{17f}", "\u{1f600}", "a\u{1f600}", "\u{1f600}\u{1f600}", "k k", "a\u{e9}b", "\u{e9}\u{e9}a", "-", "a-"]),
    ("\\B\u{e9}", &["\u{e9}", "\u{e9}a", "\u{e9}\u{e9}a"], &["", "k\u{17f}k", "\u{17f}", "a\u{e9}", "a", "ab", "a b", "\u{17f}\u{17f}", "\u{1f600}", "a\u{1f600}", "\u{1f600}\u{1f600}", "k k", "abc", "a\u{e9}b", "-", "a-"]),
    ("^\\B", &["", "\u{17f}", "\u{e9}", "\u{e9}a", "\u{17f}\u{17f}", "\u{1f600}", "\u{1f600}\u{1f600}", "\u{e9}\u{e9}a", "-"], &["k\u{17f}k", "a\u{e9}", "a", "ab", "a b", "a\u{1f600}", "k k", "abc", "a\u{e9}b", "a-"]),
    ("\u{e9}\\B$", &["\u{e9}", "a\u{e9}"], &["", "k\u{17f}k", "\u{17f}", "\u{e9}a", "a", "ab", "a b", "\u{17f}\u{17f}", "\u{1f600}", "a\u{1f600}", "\u{1f600}\u{1f600}", "k k", "abc", "a\u{e9}b", "\u{e9}\u{e9}a", "-", "a-"]),
    ("k\\B|\u{17f}$", &["\u{17f}", "\u{17f}\u{17f}"], &["", "k\u{17f}k", "\u{e9}", "a\u{e9}", "\u{e9}a", "a", "ab", "a b", "\u{1f600}", "a\u{1f600}", "\u{1f600}\u{1f600}", "k k", "abc", "a\u{e9}b", "\u{e9}\u{e9}a", "-", "a-"]),
    ("x*\\B.", &["\u{17f}", "\u{e9}", "\u{e9}a", "ab", "\u{17f}\u{17f}", "\u{1f600}", "\u{1f600}\u{1f600}", "abc", "\u{e9}\u{e9}a", "-"], &["", "k\u{17f}k", "a\u{e9}", "a", "a b", "a\u{1f600}", "k k", "a\u{e9}b", "a-"]),
    ("^.\\B.$", &["ab", "\u{17f}\u{17f}", "\u{1f600}\u{1f600}"], &["", "k\u{17f}k", "\u{17f}", "\u{e9}", "a\u{e9}", "\u{e9}a", "a", "a b", "\u{1f600}", "a\u{1f600}", "k k", "abc", "a\u{e9}b", "\u{e9}\u{e9}a", "-", "a-"]),
    ("\\B|a$", &["", "\u{17f}", "\u{e9}", "a\u{e9}", "\u{e9}a", "a", "ab", "\u{17f}\u{17f}", "\u{1f600}", "a\u{1f600}", "\u{1f600}\u{1f600}", "abc", "\u{e9}\u{e9}a", "-", "a-"], &["k\u{17f}k", "a b", "k k", "a\u{e9}b"]),
    ("[^a]\\B|b$", &["\u{17f}", "\u{e9}", "a\u{e9}", "ab", "a b", "\u{17f}\u{17f}", "\u{1f600}", "a\u{1f600}", "\u{1f600}\u{1f600}", "abc", "a\u{e9}b", "\u{e9}\u{e9}a", "-", "a-"], &["", "k\u{17f}k", "\u{e9}a", "a", "k k"]),
    ("\\Ba\\B|\u{e9}{2}", &["\u{e9}\u{e9}a"], &["", "k\u{17f}k", "\u{17f}", "\u{e9}", "a\u{e9}", "\u{e9}a", "a", "ab", "a b", "\u{17f}\u{17f}", "\u{1f600}", "a\u{1f600}", "\u{1f600}\u{1f600}", "k k", "abc", "a\u{e9}b", "-", "a-"]),
];

#[test]
fn not_a_word_boundary_beside_a_character_of_several_bytes() {
    check_texts(NOT_A_WORD_BOUNDARY);
}

/// Patterns that another engine of this family answered wrongly at some point: a reference to a group of a
/// repetition, of a lookahead and of a lookbehind, an iteration that matches nothing inside a lookahead, and a
/// negated class of a property and its complement. The crate answers them all as V8 does, before 0.1.7 too. They
/// are here so that it stays so.
#[rustfmt::skip]
const ANSWERED_WRONGLY_ELSEWHERE: &[(&str, &[&str], &[&str])] = &[
    ("^(?:x(.)){1,2}\\1", &["xxxb", "xxxx"], &["", "a", "b", "c", "x", "ab", "ax", "xa", "ax-", "axa", "xxb", "xb", "acab", "acaab", "acb", "aca", "bab", "aba", "bb", "abxb", "abxa", "bx", "bax", "cc", "aax", "\u{e9}", "1", "-"]),
    ("(a(){1})?\\2a", &["a", "ab", "ax", "xa", "ax-", "axa", "acab", "acaab", "acb", "aca", "bab", "aba", "abxb", "abxa", "bax", "aax"], &["", "b", "c", "x", "xxxb", "xxb", "xb", "bb", "bx", "cc", "xxxx", "\u{e9}", "1", "-"]),
    ("(?=(a)?b)\\1", &["b", "ab", "xxxb", "xxb", "xb", "acab", "acaab", "acb", "bab", "aba", "bb", "abxb", "abxa", "bx", "bax"], &["", "a", "c", "x", "ax", "xa", "ax-", "axa", "aca", "cc", "aax", "xxxx", "\u{e9}", "1", "-"]),
    ("^(?:(?=([ab]))[ab](?:c|ca)\\1)*$", &["", "aca"], &["a", "b", "c", "x", "ab", "ax", "xa", "ax-", "axa", "xxxb", "xxb", "xb", "acab", "acaab", "acb", "bab", "aba", "bb", "abxb", "abxa", "bx", "bax", "cc", "aax", "xxxx", "\u{e9}", "1", "-"]),
    ("^(?:(?=([ab]))[ab](?:c\\1|ca\\1))*$", &["", "aca"], &["a", "b", "c", "x", "ab", "ax", "xa", "ax-", "axa", "xxxb", "xxb", "xb", "acab", "acaab", "acb", "bab", "aba", "bb", "abxb", "abxa", "bx", "bax", "cc", "aax", "xxxx", "\u{e9}", "1", "-"]),
    ("^(?:(?=([ab]+?))(?:\\1.)*?b)+$", &["b", "acaab", "acb", "bab", "bb"], &["", "a", "c", "x", "ab", "ax", "xa", "ax-", "axa", "xxxb", "xxb", "xb", "acab", "aca", "aba", "abxb", "abxa", "bx", "bax", "cc", "aax", "xxxx", "\u{e9}", "1", "-"]),
    ("^(?=((?:|a)*))\\1$", &["", "a"], &["b", "c", "x", "ab", "ax", "xa", "ax-", "axa", "xxxb", "xxb", "xb", "acab", "acaab", "acb", "aca", "bab", "aba", "bb", "abxb", "abxa", "bx", "bax", "cc", "aax", "xxxx", "\u{e9}", "1", "-"]),
    ("^(?=((?:c??)?))\\1$", &["", "c"], &["a", "b", "x", "ab", "ax", "xa", "ax-", "axa", "xxxb", "xxb", "xb", "acab", "acaab", "acb", "aca", "bab", "aba", "bb", "abxb", "abxa", "bx", "bax", "cc", "aax", "xxxx", "\u{e9}", "1", "-"]),
    ("(?<=(x*?)a*)\\1", &["", "a", "b", "c", "x", "ab", "ax", "xa", "ax-", "axa", "xxxb", "xxb", "xb", "acab", "acaab", "acb", "aca", "bab", "aba", "bb", "abxb", "abxa", "bx", "bax", "cc", "aax", "xxxx", "\u{e9}", "1", "-"], &[]),
    ("(?<=\\1a*(b))x", &[], &["", "a", "b", "c", "x", "ab", "ax", "xa", "ax-", "axa", "xxxb", "xxb", "xb", "acab", "acaab", "acb", "aca", "bab", "aba", "bb", "abxb", "abxa", "bx", "bax", "cc", "aax", "xxxx", "\u{e9}", "1", "-"]),
    ("(?<=([abc])+)x\\1", &["axa", "abxa"], &["", "a", "b", "c", "x", "ab", "ax", "xa", "ax-", "xxxb", "xxb", "xb", "acab", "acaab", "acb", "aca", "bab", "aba", "bb", "abxb", "bx", "bax", "cc", "aax", "xxxx", "\u{e9}", "1", "-"]),
    ("(?<=(?:(a)|.)+)x\\1", &["axa", "xxxb", "xxb", "abxa", "bx", "bax", "xxxx"], &["", "a", "b", "c", "x", "ab", "ax", "xa", "ax-", "xb", "acab", "acaab", "acb", "aca", "bab", "aba", "bb", "abxb", "cc", "aax", "\u{e9}", "1", "-"]),
    ("[^\\p{L}\\P{L}]", &[], &["", "a", "b", "c", "x", "ab", "ax", "xa", "ax-", "axa", "xxxb", "xxb", "xb", "acab", "acaab", "acb", "aca", "bab", "aba", "bb", "abxb", "abxa", "bx", "bax", "cc", "aax", "xxxx", "\u{e9}", "1", "-"]),
    ("^[^\\p{L}\\P{L}]*$", &[""], &["a", "b", "c", "x", "ab", "ax", "xa", "ax-", "axa", "xxxb", "xxb", "xb", "acab", "acaab", "acb", "aca", "bab", "aba", "bb", "abxb", "abxa", "bx", "bax", "cc", "aax", "xxxx", "\u{e9}", "1", "-"]),
    ("[\\p{L}\\P{L}]", &["a", "b", "c", "x", "ab", "ax", "xa", "ax-", "axa", "xxxb", "xxb", "xb", "acab", "acaab", "acb", "aca", "bab", "aba", "bb", "abxb", "abxa", "bx", "bax", "cc", "aax", "xxxx", "\u{e9}", "1", "-"], &[""]),
];

#[test]
fn patterns_another_engine_answered_wrongly() {
    check_texts(ANSWERED_WRONGLY_ELSEWHERE);
}

/// A pattern ECMA-262 refuses in both grammars is refused, and one it accepts only without the `u` flag is not.
#[test]
fn a_pattern_that_is_not_valid_is_refused() {
    for pattern in
        [r"a{2,1}", r"(?i)a", r"\b+", r"a\B{2}", r"(?<\u>a)", r"(?<n>a)(?<n>b)", r"[b-a]", r"(?<=a)+", r"\k<n>(?<m>a)"]
    {
        assert!(compile(&json!({ "pattern": pattern })).is_err(), "{pattern}");
    }
    for pattern in [r"^\u{110000}\-?$", r"^\u{3}\-?$", r"\k<n>\-", r"(?=a)+\-?", r"a{,2}", r"\c", r"[\c]", r"\8", r"]"]
    {
        assert!(compile(&json!({ "pattern": pattern })).is_ok(), "{pattern}");
    }
}

/// Ten texts for the repetitions with a count of 300.
fn long_texts() -> Vec<String> {
    let r = |s: &str, n: usize| s.repeat(n);
    vec![
        r("a", 300) + "-",
        r("a", 301) + "-",
        r("ax", 150) + "-" + &r("x", 300) + "-",
        r("ax", 150) + "-" + &r("x", 301) + "-",
        r("a-", 400),
        r("a-", 400) + "a",
        r("ax-", 200) + &r("a", 299) + "x-",
        r("a", 600) + "-",
        r("x", 299) + "--",
        format!("-{}-", r("a", 10)),
    ]
}

/// A repetition that is written out as copies multiplies with the repetitions around it, and each optional copy
/// is a group inside the one before. Past 65,536 characters, or deeper than the 256 levels the engine reads, a
/// repetition whose body cannot match the empty string is given to the engine as it is written, with its count.
/// These compiled before 0.1.7, and the first cut of 0.1.7 refused them. The answers are V8's, on [`texts`] and
/// then on [`long_texts`].
#[rustfmt::skip]
const TOO_LARGE_TO_WRITE_OUT: &[(&str, &str, &str)] = &[
    ("^(?!-)(?:(?:a|x){1,300}-)+$", "FFFFFFTFFTFFFFFTFFTFFFFFTFFTFFFFFFFFFFFFFFFFTTF", "TFTFTFTFFF"),
    ("^(?!-)(?:(?:a|x){1,300}?-)+$", "FFFFFFTFFTFFFFFTFFTFFFFFTFFTFFFFFFFFFFFFFFFFTTF", "TFTFTFTFFF"),
    ("^(?!-)(?:(?:a|x-?){2,300}-)*$", "TFFFFFFFFFFFFFFTFFTFFFFFTFFTFFFFFFFFFFFFFFFFFTF", "TFTFFFTFTF"),
    ("(?<=^(?:(?:a|x){1,300}?-)+)a", "FFFFFFFFFFFFFFFFFFFTFFFFFFFFTFFFFFFFFFFFFFFFTTT", "FFFFTTTFFF"),
    (r#"^(?!-)(?:(?:(a)|x){1,300}\1-)+$"#, "FFFFFFFFFTFFFFFTFFTFFFFFFFFTFFFFFFFFFFFFFFFFFTF", "TTTFFFTFFF"),
    // The rest of the pattern is still written for the engine: the reference from inside its group, and the
    // small repetition whose body can be empty.
    (r#"^(?!-)(?:(?:a|x){1,300}-)+(x|\1)x$"#, "FFFFFFFFFFFFFFFFFFFFTFFFFFFFFTFFFFFFFFFFFFFFFFF", "FFFFFFFFFF"),
    ("^(?!-)(?:(?:a|x){2,300}-|(?:a|){1,2}x)+$", "FFTFFTFFTFFFFFTTFTTFFFFTTFTTFFFFFFFFFFFFFFFFFTF", "TFTTFFTFFF"),
    // A repetition inside the one that is too large is written out, whether or not its body can be empty.
    ("^(?!-)(?:(?:(?:a|)*x){1,300}-)+$", "FFFFFFFFFTFFFFFFFFTFFFFFFFFTFFFFFFFFFFFFFFFFFTF", "FFTFFFTFFF"),
];

/// The same on [`texts`] alone: V8 takes minutes over some of the long ones for these.
#[rustfmt::skip]
const TOO_LARGE_TO_WRITE_OUT_ON_SHORT_TEXTS: &[(&str, &str)] = &[
    ("^(?!-)(?:(?:(?:a|x){1,3}-?){1,300}-)+$", "FFFFFFTFFTFFFFFTFFTFFTFFTFFTFFTFFFFFFFFFFFFFTTF"),
    ("^(?!-)(?:(?:(?:a|x){1,2}-){1,300}x?)+$", "FFFFFFTFFTFFFFFTFFTFTFFFTFFTFTFFFFFFFFFFFFFFTTF"),
    ("^(?!-)(?:(?:(?:a|x){2}-?){1,300}a)+$", "FFFFFFFFFFFFFTFFTFFFFFTFFTFFFFFFFFFFFFFFFFFFFFT"),
    ("^(?!-)(?:(?:(?:a|x){200}-|a){200}x)+$", "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"),
    ("^(?!-)(?:(?:a|x){4294967295}-)+$", "FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFF"),
    // The outer repetition, whose body can be empty, is written out around the inner one, which keeps its count.
    ("^(?!-)(?:(?:(?:a|x){1,300}-|a?){3}x)+$", "FFTFFTFFTFFFFFTFFTFFTFFTFFTFFTFFFFFFFFFFFFTFFFF"),
];

#[test]
fn a_repetition_too_large_to_write_out_keeps_its_count_when_its_body_cannot_be_empty() {
    check(TOO_LARGE_TO_WRITE_OUT_ON_SHORT_TEXTS);
    let short: Vec<(&str, &str)> =
        TOO_LARGE_TO_WRITE_OUT.iter().map(|&(pattern, answers, _)| (pattern, answers)).collect();
    check(&short);
    let texts = long_texts();
    for &(pattern, _, answers) in TOO_LARGE_TO_WRITE_OUT {
        let v = compile(&json!({ "pattern": pattern })).unwrap();
        let got: String = texts.iter().map(|text| if v.is_valid(&json!(text)) { 'T' } else { 'F' }).collect();
        assert_eq!(got, answers, "{pattern}");
    }
}

/// A repetition that is too large to write out and whose body can match the empty string is the one the engine
/// does not end on when it has it as written. The pattern is refused with a reason of its own. It is not run with
/// another meaning. A repetition inside it that could keep its count does not change that.
#[test]
fn a_repetition_too_large_to_write_out_is_refused_when_its_body_can_be_empty() {
    for pattern in [
        r"^(?!-)(?:(?:a|){1,300}-)+$",
        r"^(?!-)(?:(?:a|x|){4294967295}-)+$",
        r"^(?!-)(?:(?:(?:a|x|){200}-|a?){200}x)+$",
        r"^(?!-)(?:(?:(?:a|x){1,300}|){1,300}-)+$",
        r"^(?!-)(?:(?:(?:a|){1,300}x){1,300}-)+$",
    ] {
        let e = compile(&json!({ "pattern": pattern })).expect_err(pattern);
        assert_eq!(
            e.message(),
            format!(
                "The regular expression '{pattern}' in pattern is valid, but its repetitions inside repetitions are \
                 too large to compile."
            )
        );
    }
    // A count as large on a body that cannot be come back into is not written out.
    for (pattern, text) in [
        (r"^(?!-)(?:[ax]{1,300}-)+$", "ax-xxa-"),
        (r"^(?!-)(?:(?:ax){300}-)+$", &format!("{}-", "ax".repeat(300))),
        (r"^(?!-)(?:(?:a|x){1,200}-)+$", "ax-xxa-"),
    ] {
        let v = compile(&json!({ "pattern": pattern })).unwrap_or_else(|e| panic!("{pattern}: {e:?}"));
        assert!(v.is_valid(&json!(text)), "{pattern}");
        assert!(!v.is_valid(&json!("ax-xxa")), "{pattern}");
    }
}
