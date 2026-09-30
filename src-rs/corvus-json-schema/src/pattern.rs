//! `pattern`/`patternProperties` matching with ECMA-262 semantics (the `u` flag), as JSON Schema specifies.
//!
//! A pattern gets the cheapest matcher that decides it exactly:
//!
//! - patterns every string matches (`""`, `.*`, `[\s\S]*` and the like);
//! - anchored sequences of quantified ASCII character classes and literals (`^[a-z][a-z0-9_]{0,29}$`, `^x-`,
//!   `^[@$_#]`), matched in one pass over the string (the TypeScript evaluator's class-sequence fast path);
//! - patterns within the common subset of ECMA-262 and the `regex` crate's syntax, translated with ECMA semantics
//!   (`\d`/`\w` ASCII-only, ECMA's `\s` and `.`) and run by `regex`, which searches in linear time without
//!   allocating per match;
//! - anything else (lookarounds, backreferences, word boundaries, Unicode properties) by `regress`, a backtracking
//!   ECMA-262 engine.
//!
//! Patterns compile once per process (a pattern is immutable, so identical patterns share one matcher).

use std::collections::HashMap;
use std::fmt;
use std::sync::{Arc, LazyLock, Mutex};

/// A compiled `pattern`.
pub(crate) struct Pattern {
    pub source: String,
    matcher: Matcher,
}

enum Matcher {
    /// Matches every string.
    Everything,
    /// `^literal` (with `$`: the whole string).
    Literal {
        text: Box<str>,
        whole: bool,
    },
    Sequence(Sequence),
    Regex(regex::Regex),
    Regress(regress::Regex),
}

impl fmt::Debug for Pattern {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "Pattern({:?})", self.source)
    }
}

impl Pattern {
    #[inline]
    pub fn is_match(&self, s: &str) -> bool {
        match &self.matcher {
            Matcher::Everything => true,
            Matcher::Literal { text, whole } => {
                if *whole {
                    s == &**text
                } else {
                    s.starts_with(&**text)
                }
            }
            Matcher::Sequence(seq) => seq.is_match(s),
            Matcher::Regex(re) => re.is_match(s),
            Matcher::Regress(re) => re.find(s).is_some(),
        }
    }
}

fn compile_regress(pattern: &str) -> Option<regress::Regex> {
    regress::Regex::with_flags(pattern, "u").or_else(|_| regress::Regex::new(pattern)).ok()
}

static CACHE: LazyLock<Mutex<HashMap<String, Arc<Pattern>>>> = LazyLock::new(|| Mutex::new(HashMap::new()));

/// Compiles (or fetches from the process-wide cache) a pattern; `None` when it is not a valid ECMA-262 regex.
pub(crate) fn compile(pattern: &str) -> Option<Arc<Pattern>> {
    if let Some(p) = CACHE.lock().unwrap().get(pattern) {
        return Some(p.clone());
    }
    // Validity is ECMA-262's: a pattern regress rejects is an error, whichever matcher would run it.
    let regress = compile_regress(pattern)?;
    let matcher = choose(pattern).unwrap_or(Matcher::Regress(regress));
    let p = Arc::new(Pattern { source: pattern.to_string(), matcher });
    CACHE.lock().unwrap().insert(pattern.to_string(), p.clone());
    Some(p)
}

fn choose(pattern: &str) -> Option<Matcher> {
    // Unanchored (or start-anchored) `.*` finds an empty match in any string; `^.*$` does not (`.` stops at a line
    // terminator), so it is not listed.
    if matches!(pattern, "" | ".*" | "^.*" | ".*$" | "[\\s\\S]*" | "^[\\s\\S]*" | "^[\\s\\S]*$") {
        return Some(Matcher::Everything);
    }
    if let Some(seq) = Sequence::parse(pattern) {
        return Some(match seq.literal() {
            Some(text) => Matcher::Literal { text: text.into(), whole: seq.to_end },
            None => Matcher::Sequence(seq),
        });
    }
    let translated = translate(pattern)?;
    regex::Regex::new(&translated).ok().map(Matcher::Regex)
}

/// The `regex` format: a valid ECMA-262 regular expression (with the `u` flag).
pub(crate) fn is_valid_ecma_regex(s: &str) -> bool {
    regress::Regex::with_flags(s, "u").is_ok()
}

// ---------------------------------------------------------------------------------------------------------------------
// Class sequences

/// A set of characters: ASCII by bitmask, and either all non-ASCII characters or none.
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
struct CharSet {
    ascii: u128,
    non_ascii: bool,
}

impl CharSet {
    const EMPTY: CharSet = CharSet { ascii: 0, non_ascii: false };

    fn range(lo: u8, hi: u8) -> CharSet {
        let mut ascii = 0u128;
        for c in lo..=hi {
            ascii |= 1 << c;
        }
        CharSet { ascii, non_ascii: false }
    }

    fn union(self, other: CharSet) -> CharSet {
        CharSet { ascii: self.ascii | other.ascii, non_ascii: self.non_ascii || other.non_ascii }
    }

    fn negate(self) -> CharSet {
        CharSet { ascii: !self.ascii, non_ascii: !self.non_ascii }
    }

    fn disjoint(self, other: CharSet) -> bool {
        self.ascii & other.ascii == 0 && !(self.non_ascii && other.non_ascii)
    }

    #[inline]
    fn contains(self, c: char) -> bool {
        let c = c as u32;
        if c < 128 { self.ascii & (1 << c) != 0 } else { self.non_ascii }
    }

    fn digit() -> CharSet {
        CharSet::range(b'0', b'9')
    }

    fn word() -> CharSet {
        CharSet::range(b'0', b'9')
            .union(CharSet::range(b'A', b'Z'))
            .union(CharSet::range(b'a', b'z'))
            .union(CharSet::range(b'_', b'_'))
    }
}

#[derive(Debug)]
struct Item {
    set: CharSet,
    min: u32,
    /// `u32::MAX`: unbounded.
    max: u32,
}

/// `^` then quantified character sets, optionally `$`. Matched greedily, which is exact because every variable item's
/// set is disjoint from the next item's (a character the item leaves cannot be taken by the next one either).
#[derive(Debug)]
struct Sequence {
    items: Box<[Item]>,
    to_end: bool,
}

impl Sequence {
    fn parse(p: &str) -> Option<Sequence> {
        let b = p.as_bytes();
        if !p.is_ascii() || b.first() != Some(&b'^') {
            return None;
        }
        let mut i = 1;
        let mut items = Vec::new();
        let mut to_end = false;
        while i < b.len() {
            let set = match b[i] {
                b'$' if i == b.len() - 1 => {
                    to_end = true;
                    i += 1;
                    break;
                }
                b'[' => {
                    let (set, next) = parse_class(b, i + 1)?;
                    i = next;
                    set
                }
                b'\\' => {
                    let set = class_escape(*b.get(i + 1)?)?;
                    i += 2;
                    set
                }
                b'.' | b'(' | b')' | b'|' | b'^' | b'$' | b'*' | b'+' | b'?' | b'{' | b'}' | b']' => return None,
                c => {
                    i += 1;
                    CharSet::range(c, c)
                }
            };
            let (min, max, next) = parse_quantifier(b, i)?;
            i = next;
            items.push(Item { set, min, max });
        }
        if i != b.len() {
            return None;
        }
        // Greedy matching is exact only when a variable item cannot give up characters an item after it needs: its set
        // must be disjoint from every item that can directly follow it (up to the first that cannot match nothing).
        for (i, item) in items.iter().enumerate() {
            if item.min == item.max {
                continue;
            }
            for next in &items[i + 1..] {
                if !item.set.disjoint(next.set) {
                    return None;
                }
                if next.min > 0 {
                    break;
                }
            }
        }
        Some(Sequence { items: items.into_boxed_slice(), to_end })
    }

    /// The text, when every item is one fixed character.
    fn literal(&self) -> Option<String> {
        self.items
            .iter()
            .map(|i| {
                let single = i.min == 1 && i.max == 1 && !i.set.non_ascii && i.set.ascii.count_ones() == 1;
                single.then(|| i.set.ascii.trailing_zeros() as u8 as char)
            })
            .collect()
    }

    fn is_match(&self, s: &str) -> bool {
        if s.is_ascii() {
            return self.is_match_ascii(s.as_bytes());
        }
        let mut chars = s.chars().peekable();
        for item in self.items.iter() {
            let mut n = 0u32;
            while n < item.max {
                match chars.peek() {
                    Some(&c) if item.set.contains(c) => {
                        chars.next();
                        n += 1;
                    }
                    _ => break,
                }
            }
            if n < item.min {
                return false;
            }
        }
        !self.to_end || chars.next().is_none()
    }

    /// The same over ASCII text, a byte per character.
    fn is_match_ascii(&self, b: &[u8]) -> bool {
        let mut at = 0;
        for item in self.items.iter() {
            let mut n = 0u32;
            while n < item.max && at < b.len() && item.set.ascii & (1 << b[at]) != 0 {
                at += 1;
                n += 1;
            }
            if n < item.min {
                return false;
            }
        }
        !self.to_end || at == b.len()
    }
}

/// The set of a class escape (`\d`, `\w`, their negations, or an escaped punctuation character); `None` for `\s`
/// (not ASCII-only) and anything else.
fn class_escape(c: u8) -> Option<CharSet> {
    Some(match c {
        b'd' => CharSet::digit(),
        b'D' => CharSet::digit().negate(),
        b'w' => CharSet::word(),
        b'W' => CharSet::word().negate(),
        b'n' => CharSet::range(b'\n', b'\n'),
        b'r' => CharSet::range(b'\r', b'\r'),
        b't' => CharSet::range(b'\t', b'\t'),
        c if c.is_ascii_punctuation() => CharSet::range(c, c),
        _ => return None,
    })
}

/// A class body from after `[` to after `]`, with ASCII members (a negated class also takes every non-ASCII
/// character).
fn parse_class(b: &[u8], mut i: usize) -> Option<(CharSet, usize)> {
    let negated = b.get(i) == Some(&b'^');
    if negated {
        i += 1;
    }
    let mut set = CharSet::EMPTY;
    let mut first = true;
    loop {
        let c = *b.get(i)?;
        if c == b']' && !first {
            i += 1;
            break;
        }
        if c == b']' {
            return None; // `[]` / `[^]`
        }
        first = false;
        // One atom: a single character (for ranges) or a set escape.
        let (atom, single, next) = match c {
            b'\\' => {
                let e = *b.get(i + 1)?;
                let s = class_escape(e)?;
                let single = matches!(e, b'n' | b'r' | b't') || e.is_ascii_punctuation();
                (s, single.then(|| s.ascii.trailing_zeros() as u8), i + 2)
            }
            b'[' => return None,
            _ => (CharSet::range(c, c), Some(c), i + 1),
        };
        i = next;
        if b.get(i) == Some(&b'-') && b.get(i + 1).is_some_and(|&n| n != b']') {
            let lo = single?;
            let hi = match b[i + 1] {
                b'\\' => {
                    let e = *b.get(i + 2)?;
                    if !e.is_ascii_punctuation() {
                        return None;
                    }
                    i += 3;
                    e
                }
                h => {
                    i += 2;
                    h
                }
            };
            if lo > hi {
                return None;
            }
            set = set.union(CharSet::range(lo, hi));
        } else {
            set = set.union(atom);
        }
    }
    Some((if negated { set.negate() } else { set }, i))
}

/// An optional quantifier (`*`, `+`, `?`, `{n}`, `{n,}`, `{n,m}`, each optionally lazy) at `i`.
fn parse_quantifier(b: &[u8], mut i: usize) -> Option<(u32, u32, usize)> {
    let (min, max) = match b.get(i) {
        Some(b'*') => {
            i += 1;
            (0, u32::MAX)
        }
        Some(b'+') => {
            i += 1;
            (1, u32::MAX)
        }
        Some(b'?') => {
            i += 1;
            (0, 1)
        }
        Some(b'{') => {
            let end = i + b[i..].iter().position(|&c| c == b'}')?;
            let body = std::str::from_utf8(&b[i + 1..end]).ok()?;
            i = end + 1;
            match body.split_once(',') {
                None => {
                    let n = body.parse().ok()?;
                    (n, n)
                }
                Some((lo, "")) => (lo.parse().ok()?, u32::MAX),
                Some((lo, hi)) => (lo.parse().ok()?, hi.parse().ok()?),
            }
        }
        _ => return Some((1, 1, i)),
    };
    if min > max {
        return None;
    }
    if b.get(i) == Some(&b'?') {
        i += 1; // Laziness does not change whether the whole pattern matches.
    }
    Some((min, max, i))
}

// ---------------------------------------------------------------------------------------------------------------------
// Translation to the regex crate

/// ECMA-262 `\s`: WhiteSpace and LineTerminator.
const ECMA_SPACE: &str =
    r"\t\n\x{B}\x{C}\r \x{A0}\x{1680}\x{2000}-\x{200A}\x{2028}\x{2029}\x{202F}\x{205F}\x{3000}\x{FEFF}";

/// Translates an ECMA-262 pattern (`u` flag) to the `regex` crate's syntax with the same meaning, or `None` when it
/// uses something outside the common subset.
fn translate(p: &str) -> Option<String> {
    let c: Vec<char> = p.chars().collect();
    let mut out = String::with_capacity(p.len() * 2);
    let mut i = 0;
    while i < c.len() {
        match c[i] {
            '\\' => {
                let (text, next) = escape(&c, i + 1, false)?;
                out.push_str(&text);
                i = next;
            }
            '[' => {
                let (text, next) = class(&c, i + 1)?;
                out.push_str(&text);
                i = next;
            }
            '(' => {
                if c.get(i + 1) == Some(&'?') {
                    match (c.get(i + 2), c.get(i + 3)) {
                        (Some(':'), _) => i += 3,
                        // A named group; lookbehinds (`(?<=`, `(?<!`) are not supported.
                        (Some('<'), Some(n)) if *n != '=' && *n != '!' => {
                            i += 3 + c[i + 3..].iter().position(|&x| x == '>')? + 1;
                        }
                        _ => return None,
                    }
                } else {
                    i += 1;
                }
                out.push_str("(?:");
            }
            '{' => {
                let end = i + c[i..].iter().position(|&x| x == '}')?;
                let body: String = c[i + 1..end].iter().collect();
                if body.is_empty() || !body.chars().all(|x| x.is_ascii_digit() || x == ',') {
                    return None;
                }
                out.push('{');
                out.push_str(&body);
                out.push('}');
                i = end + 1;
            }
            '.' => {
                out.push_str(r"[^\n\r\x{2028}\x{2029}]");
                i += 1;
            }
            ch @ (')' | '|' | '^' | '$' | '*' | '+' | '?') => {
                out.push(ch);
                i += 1;
            }
            ch => {
                push_literal(&mut out, ch);
                i += 1;
            }
        }
    }
    Some(out)
}

fn push_literal(out: &mut String, ch: char) {
    use std::fmt::Write;
    let _ = write!(out, "\\x{{{:X}}}", ch as u32);
}

/// An escape after `\` at `i`: its translation and the index after it.
fn escape(c: &[char], i: usize, in_class: bool) -> Option<(String, usize)> {
    let e = *c.get(i)?;
    let mut out = String::new();
    let next = match e {
        'd' => {
            out.push_str(if in_class { "0-9" } else { "[0-9]" });
            i + 1
        }
        'D' => {
            out.push_str("[^0-9]");
            i + 1
        }
        'w' => {
            out.push_str(if in_class { "0-9A-Za-z_" } else { "[0-9A-Za-z_]" });
            i + 1
        }
        'W' => {
            out.push_str("[^0-9A-Za-z_]");
            i + 1
        }
        's' => {
            if in_class {
                out.push_str(ECMA_SPACE);
            } else {
                out.push('[');
                out.push_str(ECMA_SPACE);
                out.push(']');
            }
            i + 1
        }
        'S' => {
            out.push_str("[^");
            out.push_str(ECMA_SPACE);
            out.push(']');
            i + 1
        }
        'n' | 'r' | 't' | 'f' | 'v' => {
            let ch = match e {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'f' => '\x0C',
                _ => '\x0B',
            };
            push_literal(&mut out, ch);
            i + 1
        }
        '0' if !c.get(i + 1).is_some_and(char::is_ascii_digit) => {
            push_literal(&mut out, '\0');
            i + 1
        }
        'b' if in_class => {
            push_literal(&mut out, '\x08');
            i + 1
        }
        'x' => {
            let hex: String = c.get(i + 1..i + 3)?.iter().collect();
            push_literal(&mut out, char::from_u32(u32::from_str_radix(&hex, 16).ok()?)?);
            i + 3
        }
        'u' => {
            let (code, next) = unicode_escape(c, i + 1)?;
            push_literal(&mut out, char::from_u32(code)?);
            next
        }
        // Identity escapes of syntax characters and `/` (and `-` in a class).
        '^' | '$' | '\\' | '.' | '*' | '+' | '?' | '(' | ')' | '[' | ']' | '{' | '}' | '|' | '/' | '-' => {
            push_literal(&mut out, e);
            i + 1
        }
        // \b \B (word boundaries), backreferences, \c, \k, \p, \P and the rest.
        _ => return None,
    };
    Some((out, next))
}

/// `\u` escapes after the `u`: `XXXX` (joining a surrogate pair) or `{X...}`.
fn unicode_escape(c: &[char], i: usize) -> Option<(u32, usize)> {
    if c.get(i) == Some(&'{') {
        let end = i + c[i..].iter().position(|&x| x == '}')?;
        let hex: String = c[i + 1..end].iter().collect();
        return Some((u32::from_str_radix(&hex, 16).ok()?, end + 1));
    }
    let four = |at: usize| -> Option<u32> {
        let hex: String = c.get(at..at + 4)?.iter().collect();
        if hex.len() != 4 {
            return None;
        }
        u32::from_str_radix(&hex, 16).ok()
    };
    let hi = four(i)?;
    if (0xD800..0xDC00).contains(&hi) {
        if c.get(i + 4) == Some(&'\\') && c.get(i + 5) == Some(&'u') {
            let lo = four(i + 6)?;
            if (0xDC00..0xE000).contains(&lo) {
                return Some((0x10000 + ((hi - 0xD800) << 10) + (lo - 0xDC00), i + 10));
            }
        }
        return None;
    }
    Some((hi, i + 4))
}

/// A class after `[` at `i`: its translation (every member spelled as an escape, so nothing in it is special to the
/// `regex` crate) and the index after `]`.
fn class(c: &[char], mut i: usize) -> Option<(String, usize)> {
    let mut out = String::from("[");
    if c.get(i) == Some(&'^') {
        out.push('^');
        i += 1;
    }
    let mut members = 0;
    loop {
        let ch = *c.get(i)?;
        if ch == ']' {
            i += 1;
            break;
        }
        // One atom: a character (which can start a range) or a class escape.
        let (atom, single, next) = if ch == '\\' {
            let e = *c.get(i + 1)?;
            let (text, next) = escape(c, i + 1, true)?;
            let single = match e {
                'd' | 'D' | 'w' | 'W' | 's' | 'S' => None,
                _ => Some(text.clone()),
            };
            (text, single, next)
        } else {
            let mut t = String::new();
            push_literal(&mut t, ch);
            (t.clone(), Some(t), i + 1)
        };
        i = next;
        members += 1;
        if c.get(i) == Some(&'-') && c.get(i + 1).is_some_and(|&n| n != ']') {
            // A range: both ends single characters.
            let lo = single?;
            let (hi, next) = if c[i + 1] == '\\' {
                let e = *c.get(i + 2)?;
                if matches!(e, 'd' | 'D' | 'w' | 'W' | 's' | 'S') {
                    return None;
                }
                escape(c, i + 2, true)?
            } else {
                let mut t = String::new();
                push_literal(&mut t, c[i + 1]);
                (t, i + 2)
            };
            out.push_str(&lo);
            out.push('-');
            out.push_str(&hi);
            i = next;
        } else {
            out.push_str(&atom);
        }
    }
    if members == 0 {
        return None; // `[]` and `[^]` have no counterpart.
    }
    out.push(']');
    Some((out, i))
}

#[cfg(test)]
mod tests {
    use super::*;

    const PATTERNS: &[&str] = &[
        "",
        ".*",
        "^.*",
        ".*$",
        "[\\s\\S]*",
        "^[\\s\\S]*",
        "^[\\s\\S]*$",
        "^.*$",
        "^[@$_#]",
        "^[a-zA-Z0-9_\\.\\-\\|@#]*$",
        "^[a-zA-Z0-9_\\-]*$",
        ".+",
        "^\\{\\{[^\\W\\.\\-][\\w\\.\\-]*\\}\\}$",
        "^.{1,256}$",
        "^[A-Z0-9_\\-\\/]+$",
        "^[a-zA-Z0-9_\\.\\-]+[\\|]?[a-zA-Z0-9_\\.\\-]+$",
        "^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$",
        "^x-",
        "^[1-5](?:[0-9]{2}|XX)$",
        "(base64key|awskms)://(.*)",
        "^[A-F0-9]{1,32}$",
        "^[a-z][a-z0-9]{0,29}$",
        "^([t|T][o|O][p|P])|([c|C][e|E][n|N][t|T][e|E][r|R])$",
        "^[\\w\\*]{0,60}$",
        "^(?:@[0-9a-z-_.]+\\/)?[a-z][0-9a-z-_.]*$",
        "^[a-z][a-z0-9_]+$",
        "^\\d+[:-]\\d+$",
        "^#[0-9a-fA-F]{6}$",
        "^[^:]+:[^:]+$",
        "^(?=[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$",
        "^.*\\.(?:txt|trie)(?:\\.gz)?$",
        "^([-\\w_\\s]+)(,[-\\w_\\s]+)*$",
        "^(!?[-\\w_\\s]+)|(\\*)$",
        "^[0-9]+(ns|ms|us|µs|s|m|h)$",
        "^\\/[^\\*\\?\\&\\%]*(\\/\\*)?$",
        "^[^- @#$%^&()!]+$",
        "^((\\.(?!\\.)\\/)?\\w+\\/?)+$",
        "^[0-9]{1,}.[0-9]{1,}.[0-9]{1,}$",
        "\\{.*\\}",
        "^[a-z]{1,2}$",
        "^abc$",
        "^\\/",
        "^es$",
        "^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-((?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\\.(?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\\+([0-9a-zA-Z-]+(?:\\.[0-9a-zA-Z-]+)*))?$",
        "^[Ee][Ss]5|[Ee][Ss]6|[Ee][Ss]7$",
        "^[a-z]*a$",
        "^a*a",
        "^a+b?a",
        "^a+b?c*a$",
        "^[a-z]+-?[a-z]+$",
        "\\bfoo",
        "^\\p{L}+$",
        "\\u00e9",
        "\\ud83d\\ude00",
        "[^\\d]x",
        "^\\S+$",
        "a{2}b{1,}c{0,3}",
        "(?<name>ab)+",
        "^[\\b]",
        "\\x41",
    ];

    /// Every matcher agrees with regress on strings over an alphabet that exercises classes, anchors and non-ASCII.
    #[test]
    fn matchers_agree_with_regress() {
        let alphabet = [
            "a", "b", "z", "A", "X", "Z", "0", "1", "5", "9", "_", "-", ".", ":", "/", "@", "#", "$", "*", "!", "{",
            "}", "|", " ", "\n", "\u{a0}", "é", "µ", "😀", "\u{2028}", "x-", "es", "ES", "ms", "txt",
        ];
        let mut seed: u64 = 0x2545_f491_4f6c_dd1d;
        let mut next = || {
            seed ^= seed << 13;
            seed ^= seed >> 7;
            seed ^= seed << 17;
            seed
        };
        for &p in PATTERNS {
            let reference = compile_regress(p).unwrap();
            let compiled = compile(p).unwrap();
            for _ in 0..4000 {
                let len = next() % 9;
                let s: String = (0..len).map(|_| alphabet[(next() % alphabet.len() as u64) as usize]).collect();
                assert_eq!(compiled.is_match(&s), reference.find(&s).is_some(), "{p:?} on {s:?}");
            }
        }
    }

    #[test]
    fn simple_patterns_take_the_fast_matchers() {
        for p in ["^[@$_#]", "^[a-zA-Z0-9_\\-]*$", "^#[0-9a-fA-F]{6}$", "^[a-z][a-z0-9_]+$", "^\\d{4}-\\d{2}-\\d{2}$"] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Sequence(_)), "{p}");
        }
        for p in ["^x-", "^\\/", "^abc$"] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Literal { .. }), "{p}");
        }
        for p in ["^[a-z]*a$", ".+", "^[1-5](?:[0-9]{2}|XX)$", "(base64key|awskms)://(.*)"] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Regex(_)), "{p}");
        }
        for p in ["\\bfoo", "^(?=[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$"] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Regress(_)), "{p}");
        }
    }
}
