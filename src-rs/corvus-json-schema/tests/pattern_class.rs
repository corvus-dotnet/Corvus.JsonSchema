//! A class with a member outside ASCII, in the shape `^(?=[^SET]+$)(?=(.*\w)).+$`.
//!
//! The shape's set holds ASCII characters as bits. A member outside ASCII used to be read byte by byte as bit
//! numbers of 128 or more, which panicked in a debug build and, in a release build, set the bits of unrelated ASCII
//! characters, so the pattern gave wrong answers. Such a pattern has no shape now and is matched by the engine.

use corvus_json_schema::compile;
use serde_json::json;

/// What the pattern means: no excluded character anywhere, and a word character somewhere.
fn expected(excluded: &dyn Fn(char) -> bool, text: &str) -> bool {
    !text.is_empty() && !text.chars().any(excluded) && text.chars().any(|c| c.is_ascii_alphanumeric() || c == '_')
}

#[test]
fn excluded_class_with_a_member_outside_ascii() {
    let cases: [(&str, &dyn Fn(char) -> bool); 5] = [
        ("^(?=[^\u{e9}]+$)(?=(.*\\w)).+$", &|c| c == '\u{e9}'),
        ("^(?=[^x\u{e9}]+$)(?=(?:.*\\w)).+$", &|c| c == 'x' || c == '\u{e9}'),
        ("^(?=[^\u{4e2d}/]+$)(?=.*\\w).+$", &|c| c == '\u{4e2d}' || c == '/'),
        ("^(?=[^\u{1f600}]+$)(?=(.*\\w)).+$", &|c| c == '\u{1f600}'),
        // A range that starts in ASCII and ends outside it.
        ("^(?=[^m-\u{e9}]+$)(?=(.*\\w)).+$", &|c| ('m'..='\u{e9}').contains(&c)),
    ];
    // The bytes of these characters, taken as bit numbers modulo 128, are C ) d - 8 p and other ASCII characters.
    let texts = [
        "C1",
        ")a",
        "abc",
        "d-8p",
        "p.q",
        "x1",
        "a/b",
        "---",
        "\u{e9}1",
        "a\u{e9}",
        "\u{4e2d}a",
        "a\u{1f600}",
        "z\u{e8}",
        "0",
        "ABC",
        "a{",
        "al",
    ];
    for (pattern, excluded) in cases {
        let validator = compile(&json!({ "pattern": pattern })).unwrap();
        for text in texts {
            assert_eq!(validator.is_valid(&json!(text)), expected(excluded, text), "{pattern} on {text:?}");
        }
    }
}
