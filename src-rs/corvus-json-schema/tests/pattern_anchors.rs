//! Patterns with an anchor, a word boundary or a lookaround in a place where a matcher for a shape could lose it.
//!
//! The matcher for a separated list (`^item(separator item)*$` and its other forms) read the first item and the
//! repeated group as class sequences, and dropped a `$` at the end of either. `^x(,a$)*$` was matched as `^x(,a)*$`,
//! so it took `x,a,a`, which has a `$` before its second `,a` (0.1.0 to 0.1.6). Such a list has no shape now and is
//! matched by the engine. An escaped `\$` or `\^` is a literal character, and a list with one keeps its shape.

use corvus_json_schema::compile;
use serde_json::json;

/// Each pattern with the texts it matches and the texts it does not. The answers are V8's:
/// `new RegExp(pattern, "u").test(text)`.
#[rustfmt::skip]
const CASES: &[(&str, &[&str], &[&str])] = &[
    (r"^x(,a$)*$", &["x", "x,a"], &["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^x(,a$)+$", &["x,a"], &["x", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^x(?:,a$)*$", &["x", "x,a"], &["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^x$(,a)*$", &["x"], &["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^(x$)(,a)*$", &["x"], &["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^(?:x$)(,a)+$", &[], &["x", "x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^$(,a)*$", &[""], &[",a", ",a,a", "a"]),
    (r"^(a,$)*b$", &["b"], &["a,b", "a,a,b", "a,", "", "a,a"]),
    (r"^(a,$)+b$", &[], &["b", "a,b", "a,a,b", "a,", "", "a,a"]),
    (r"^(?:a,$)*b$", &["b"], &["a,b", "a,a,b", "a,", "", "a,a"]),
    (r"^([a-z]+)(\.[a-z]+$)*$", &["a", "a.b"], &["a.b.c", "a.", ""]),
    (r"^([a-z]+\.$)*[a-z]+$", &["a"], &["a.b", "a.b.c", ".a", ""]),
    (r"^x(,a$$)*$", &["x", "x,a"], &["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^x(^,a)*$", &["x"], &["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^x(,^a)*$", &["x"], &["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^^x(,a)*$", &["x", "x,a", "x,a,a", "x,a,a,a"], &["", "x,", "x,b"]),
    (r"^(^x)(,a)*$", &["x", "x,a", "x,a,a", "x,a,a,a"], &["", "x,", "x,b"]),
    (r"^(^a,)*b$", &["b", "a,b"], &["a,a,b", "a,", "", "a,a"]),
    (r"^(a,)*^b$", &["b"], &["a,b", "a,a,b", "a,", "", "a,a"]),
    (r"^x(,(a$))*$", &["x", "x,a"], &["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^x(,(?:a$))*$", &["x", "x,a"], &["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^x(,a|$)*$", &["x", "x,a", "x,a,a", "x,a,a,a"], &["", "x,", "x,b"]),
    (r"^x(,(a|$))*$", &["x", "x,a", "x,a,a", "x,a,a,a", "x,"], &["", "x,b"]),
    (r"^x((^|,)a)*$", &["x", "x,a", "x,a,a", "x,a,a,a"], &["", "x,", "x,b"]),
    (r"^(a,)*(b$)$", &["b", "a,b", "a,a,b"], &["a,", "", "a,a"]),
    (r"^(a,)*b$$", &["b", "a,b", "a,a,b"], &["a,", "", "a,a"]),
    (r"^x(,a\b)*$", &["x", "x,a", "x,a,a", "x,a,a,a"], &["", "x,", "x,b", "x,ab"]),
    (r"^x(\b,a)*$", &["x", "x,a", "x,a,a", "x,a,a,a"], &["", "x,", "x,b"]),
    (r"^x(,\Ba)*$", &["x"], &["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^x(\B,a)*$", &["x"], &["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^x\b(,a)*$", &["x", "x,a", "x,a,a", "x,a,a,a"], &["", "x,", "x,b"]),
    (r"^x\B(,a)*$", &[], &["x", "x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^(a\b,)*b$", &["b", "a,b", "a,a,b"], &["a,", "", "a,a"]),
    (r"^(a,\B)*b$", &["b"], &["a,b", "a,a,b", "a,", "", "a,a"]),
    (r"^x(,a(?=,|$))*$", &["x", "x,a", "x,a,a", "x,a,a,a"], &["", "x,", "x,b"]),
    (r"^x(,(?!a)a)*$", &["x"], &["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^x(?=,)(,a)*$", &["x,a", "x,a,a", "x,a,a,a"], &["x", "", "x,", "x,b"]),
    (r"^x(,a(?<=a))*$", &["x", "x,a", "x,a,a", "x,a,a,a"], &["", "x,", "x,b"]),
    (r"^x(,(?<!,)a)*$", &["x"], &["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^x(?=,a$)(,a)*$", &["x,a"], &["x", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (r"^(a,(?=b))*b$", &["b", "a,b"], &["a,a,b", "a,", "", "a,a"]),
    (r"^(?!a,a)(a,)*b$", &["b", "a,b"], &["a,a,b", "a,", "", "a,a"]),
    (r"^x(,a\$)*$", &["x", "x,a$", "x,a$,a$"], &["x,a", "x,a,a", "x,a$,a"]),
    (r"^x\$(,a)*$", &["x$", "x$,a", "x$,a,a"], &["x", "x,a"]),
    (r"^(a\$,)*b$", &["b", "a$,b", "a$,a$,b"], &["a,b", "a$,a,b"]),
    (r"^x(,\^a)*$", &["x", "x,^a", "x,^a,^a"], &["x,a", "x,a,a"]),
    (r"^(a,)*b\$$", &["b$", "a,b$", "a,a,b$"], &["b", "a,b"]),
    (r"^x(,[$^]a)*$", &["x", "x,$a", "x,^a,$a"], &["x,a", "x,a,a"]),
    (r"^a$b", &[], &["a", "ab", "a$b", ""]),
    (r"^a^b", &[], &["a", "ab", "b"]),
    (r"^a$$", &["a"], &["ab", ""]),
    (r"^^a", &["a", "ab"], &["ba"]),
    (r"^a\$b$", &["a$b"], &["ab", "a"]),
    (r"^a\b", &["a", "a b", "a,"], &["ab"]),
    (r"^a\Bb", &["ab"], &["a b", "a"]),
    (r"^\ba", &["a"], &["ba", ""]),
    (r"^a(?=b)", &["ab"], &["a", "ac"]),
    (r"^a(?!b)", &["a", "ac"], &["ab"]),
    (r"^[a-c]+\b$", &["a", "abc"], &[""]),
    (r"^[a-c]+$\b", &["a", "abc"], &[""]),
    (r"^$", &[""], &["a"]),
    (r"^a$.*", &["a"], &["ab", ""]),
    (r"^a\b.*", &["a", "a b"], &["ab"]),
    (r"^a^.*", &[], &["a", "ab"]),
    (r"^\$.*", &["$", "$a"], &["a"]),
    (r"^a$|b", &["a", "ab", "b", "cb"], &["c"]),
    (r"a$|^b", &["a", "ba", "bc"], &["ab", "cb"]),
    (r"^(a$|b)$", &["a", "b"], &["ab", ""]),
    (r"^(a|^b)$", &["a", "b"], &["ab"]),
    (r"^(a|b\b)$", &["a", "b"], &["ab"]),
    (r"^(?:a\$|b)$", &["a$", "b"], &["a"]),
    (r"^(a|$)$", &["a", ""], &["b"]),
    (r"^(^|b)$", &["", "b"], &["a"]),
    (r"^(a$|b)c", &["bc", "bcd"], &["a", "ac"]),
    (r"(^a|b)c$", &["ac", "bc", "xbc"], &["xac", "acx"]),
    (r"a(b|^c)", &["ab", "xab"], &["ac", "c"]),
    (r"(a$){2}", &[], &["a", "aa", ""]),
    (r"(^a){2}", &[], &["a", "aa"]),
    (r"x(a$)?b", &["xb"], &["xab", "xa"]),
    (r"^x(a$)?", &["x", "xa", "xab", "xb"], &[]),
    (r"(a|b$)c", &["ac"], &["bc", "b"]),
    (r"a|\bb", &["a", "b", "c b"], &["cb"]),
    (r"a\b|c", &["a", "c", "b a"], &["ab"]),
    (r"(?=a)a|b", &["a", "b"], &["c"]),
    (r"(?<=a)b|c", &["ab", "c"], &["b"]),
    (r"a^|b", &["b"], &["a", ""]),
    (r"$a|b", &["b"], &["a", ""]),
    (r"^|a", &["", "b"], &[]),
    (r"a|$", &["", "b"], &[]),
    (r"(a|b)$c", &[], &["ac", "a", "bc"]),
    (r"^(a|b)^c", &[], &["ac", "c"]),
    (r"[a-c]{2}$|^x", &["ab", "x", "yab"], &["abx", "yx"]),
    (r"[a-c]\b|x", &["a", "x", "d a"], &["ad"]),
    (r"^[a-c]+$|^\d$", &["abc", "1"], &["a1", ""]),
    (r"^(a|b){2}$", &["ab"], &["a", "abc"]),
    (r"^.{1,3}$$", &["a"], &["abcd", ""]),
    (r"^.$^", &[], &["a", ""]),
    (r"^(.*)$", &["", "a"], &[]),
    (r"^es$|ms", &["es", "xms"], &["esx", "x"]),
    (r"^x-$", &["x-"], &["x-a"]),
    (r"^x-\b", &["x-a"], &["x-", "x--"]),
];

#[test]
fn an_anchor_or_an_assertion_inside_a_shape_is_not_dropped() {
    let mut wrong = Vec::new();
    for &(pattern, matches, fails) in CASES {
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
fn an_anchor_inside_a_pattern_of_property_names_is_not_dropped() {
    let v = compile(&json!({ "patternProperties": { "^x(,a$)*$": false } })).unwrap();
    assert!(!v.is_valid(&json!({ "x,a": 1 })));
    assert!(v.is_valid(&json!({ "x,a,a": 1 })));
    let v = compile(&json!({ "propertyNames": { "pattern": "^(a,$)*b$" } })).unwrap();
    assert!(v.is_valid(&json!({ "b": 1 })));
    assert!(!v.is_valid(&json!({ "a,b": 1 })));
}
