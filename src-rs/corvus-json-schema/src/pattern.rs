//! `pattern`/`patternProperties` matching with ECMA-262 semantics (the `u` flag), as JSON Schema specifies.
//!
//! A pattern gets the cheapest matcher that decides it exactly:
//!
//! - patterns every string matches (`""`, `.*`, `[\s\S]*` and the like), `.+`, and line lengths (`^.{1,256}$`);
//! - anchored sequences of quantified ASCII character classes and literals (`^[a-z][a-z0-9_]{0,29}$`, `^x-`,
//!   `^[@$_#]`), matched in one pass over the string (the TypeScript evaluator's class-sequence fast path);
//! - alternatives of such sequences once groups are multiplied out (`^([a|A]uto)|([n|N]one)$`,
//!   `^[Ee][Ss]2015(\.([Cc]ore|[Pp]roxy))?$`), sets of literals, and separated lists (`^([a-z]+)(\.[a-z]+)*$`),
//!   as the C# evaluator's pattern matchers do;
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
    SeparatedList(SeparatedList),
    /// `.+` (`^.+` with `start`): some (the first) character is not a line terminator.
    HasContent {
        start: bool,
    },
    /// `^.{min,max}$`: between `min` and `max` characters, none a line terminator.
    Line {
        min: u32,
        max: u32,
    },
    /// `^(a|b|...)$`: one of a set of strings.
    Literals(Literals),
    /// Top-level alternatives of literals, each optionally anchored (`^a|b|c$`).
    Alternatives(Box<[Alternative]>),
    /// `^(?=[^SET]+$)(?=(.*\w)).+$`: a non-empty line without a character of the set, containing a word character.
    /// With `bangs` (`^(?=!+[^SET]+$)…`, the set holding `!`), that line follows one or more `!`.
    ExcludedClassWithWord {
        set: CharSet,
        bangs: bool,
    },
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
            Matcher::SeparatedList(list) => list.is_match(s),
            Matcher::HasContent { start } => {
                if *start {
                    s.chars().next().is_some_and(|c| !is_line_terminator(c))
                } else {
                    s.chars().any(|c| !is_line_terminator(c))
                }
            }
            Matcher::Line { min, max } => line_length(s).is_some_and(|n| n >= *min as usize && n <= *max as usize),
            Matcher::Literals(set) => set.contains(s),
            Matcher::Alternatives(alts) => {
                let ascii = s.is_ascii();
                alts.iter().any(|a| a.is_match(s, ascii))
            }
            Matcher::ExcludedClassWithWord { set, bangs } => {
                let mut word = false;
                let s = if *bangs {
                    let rest = s.trim_start_matches('!');
                    if rest.len() == s.len() || rest.is_empty() {
                        return false;
                    }
                    rest
                } else {
                    s
                };
                for c in s.chars() {
                    if set.contains(c) || is_line_terminator(c) {
                        return false;
                    }
                    word |= CharSet::word().contains(c);
                }
                word
            }
            Matcher::Regex(re) => re.is_match(s),
            Matcher::Regress(re) => re.find(s).is_some(),
        }
    }
}

/// ECMA-262 `LineTerminator`, which `.` does not match.
#[inline]
fn is_line_terminator(c: char) -> bool {
    matches!(c, '\n' | '\r' | '\u{2028}' | '\u{2029}')
}

/// The number of characters, when none is a line terminator.
#[inline]
fn line_length(s: &str) -> Option<usize> {
    if s.is_ascii() {
        return (!s.bytes().any(|b| b == b'\n' || b == b'\r')).then_some(s.len());
    }
    let mut n = 0;
    for c in s.chars() {
        if is_line_terminator(c) {
            return None;
        }
        n += 1;
    }
    Some(n)
}

/// A set of strings: compared in turn when there are few, hashed otherwise.
enum Literals {
    Few(Box<[Box<str>]>),
    Many(std::collections::HashSet<Box<str>>),
}

impl Literals {
    fn new(texts: Vec<String>) -> Literals {
        if texts.len() <= 8 {
            Literals::Few(texts.into_iter().map(String::into_boxed_str).collect())
        } else {
            Literals::Many(texts.into_iter().map(String::into_boxed_str).collect())
        }
    }

    #[inline]
    fn contains(&self, s: &str) -> bool {
        match self {
            Literals::Few(texts) => texts.iter().any(|t| **t == *s),
            Literals::Many(set) => set.contains(s),
        }
    }
}

/// One alternative of [`Matcher::Alternatives`]: a literal anchored at either end (or neither), a class sequence
/// anchored at the start, or a fixed-width class sequence anchored at the end.
enum Alternative {
    Literal {
        text: Box<str>,
        start: bool,
        end: bool,
    },
    /// `^sequence` (with `$` when the sequence's `to_end`).
    Start(Sequence),
    /// `sequence$` of `width` characters: matched over the string's last `width` characters.
    End {
        seq: Sequence,
        width: usize,
    },
    /// An unanchored `sequence` of `width` characters: matched at each position.
    Anywhere {
        seq: Sequence,
        width: usize,
    },
}

impl Alternative {
    /// Whether the alternative matches `s`, `ascii` saying whether `s` is ASCII.
    #[inline]
    fn is_match(&self, s: &str, ascii: bool) -> bool {
        match self {
            Alternative::Literal { text, start, end } => match (start, end) {
                (true, true) => s == &**text,
                (true, false) => s.starts_with(&**text),
                (false, true) => s.ends_with(&**text),
                (false, false) => s.contains(&**text),
            },
            Alternative::Start(seq) => {
                if ascii {
                    seq.is_match_ascii(s.as_bytes())
                } else {
                    seq.is_match_chars(s)
                }
            }
            Alternative::End { seq, width } => {
                if ascii {
                    return s.len().checked_sub(*width).is_some_and(|at| seq.is_match_ascii(&s.as_bytes()[at..]));
                }
                let at = {
                    let n = s.chars().count();
                    n.checked_sub(*width).map(|skip| s.char_indices().nth(skip).map_or(s.len(), |(i, _)| i))
                };
                at.is_some_and(|at| seq.is_match_chars(&s[at..]))
            }
            Alternative::Anywhere { seq, width } => {
                if ascii {
                    let b = s.as_bytes();
                    return b.len() >= *width && (0..=b.len() - width).any(|at| seq.is_match_ascii(&b[at..]));
                }
                let n = s.chars().count();
                n >= *width && s.char_indices().take(n - width + 1).any(|(at, _)| seq.is_match_chars(&s[at..]))
            }
        }
    }
}

/// Compiles a pattern with the `u` flag, or (as many validators accept them) without it; the flag is returned.
fn compile_regress(pattern: &str) -> Option<(regress::Regex, bool)> {
    match regress::Regex::with_flags(pattern, "u") {
        Ok(re) => Some((re, true)),
        Err(_) => regress::Regex::new(pattern).ok().map(|re| (re, false)),
    }
}

static CACHE: LazyLock<Mutex<HashMap<String, Arc<Pattern>>>> = LazyLock::new(|| Mutex::new(HashMap::new()));

/// Compiles (or fetches from the process-wide cache) a pattern; `None` when it is not a valid ECMA-262 regex.
pub(crate) fn compile(pattern: &str) -> Option<Arc<Pattern>> {
    if let Some(p) = CACHE.lock().unwrap().get(pattern) {
        return Some(p.clone());
    }
    // Validity is ECMA-262's: a pattern regress rejects is an error, whichever matcher would run it.
    let (regress, unicode) = compile_regress(pattern)?;
    let matcher = choose(pattern, unicode).unwrap_or(Matcher::Regress(regress));
    let p = Arc::new(Pattern { source: pattern.to_string(), matcher });
    CACHE.lock().unwrap().insert(pattern.to_string(), p.clone());
    Some(p)
}

fn choose(pattern: &str, unicode: bool) -> Option<Matcher> {
    // Unanchored (or start-anchored) `.*` finds an empty match in any string; `^.*$` does not (`.` stops at a line
    // terminator), so it is not listed.
    if matches!(pattern, "" | ".*" | "^.*" | ".*$" | "(.*)" | "^(.*)" | "[\\s\\S]*" | "^[\\s\\S]*" | "^[\\s\\S]*$") {
        return Some(Matcher::Everything);
    }
    // Without the `u` flag, a pattern matches UTF-16 code units rather than characters: only regress has that.
    if !unicode {
        return None;
    }
    match pattern {
        ".+" | "." | "(.+)" => return Some(Matcher::HasContent { start: false }),
        "^.+" | "^." => return Some(Matcher::HasContent { start: true }),
        _ => {}
    }
    // `^X.*` (no `$`) matches exactly where `^X` does: `.*` can match nothing.
    if let Some(rest) = pattern.strip_suffix(".*")
        && rest.starts_with('^')
        && rest.len() > 1
        && !ends_with_escape(rest)
        && !rest.ends_with(['*', '+', '?', '}', '|', '(', '^'])
        && let Some(m) = choose(rest, unicode)
    {
        return Some(m);
    }
    if let Some((min, max)) = line_range(pattern) {
        return Some(Matcher::Line { min, max });
    }
    if let Some(texts) = whole_alternatives(pattern) {
        return Some(Matcher::Literals(Literals::new(texts)));
    }
    if let Some(alts) = alternatives(pattern) {
        return Some(Matcher::Alternatives(alts));
    }
    if let Some(list) = SeparatedList::parse(pattern) {
        return Some(Matcher::SeparatedList(list));
    }
    if let Some((set, bangs)) = excluded_class_with_word(pattern) {
        return Some(Matcher::ExcludedClassWithWord { set, bangs });
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

/// `^.{m,n}$` (and `^.*$`, `^.+$`, `^.{m}$`, `^.{m,}$`, each also as `^(.…)$`): the bounds on the length of a line.
fn line_range(p: &str) -> Option<(u32, u32)> {
    let q = p.strip_prefix("^.").or_else(|| p.strip_prefix("^(."))?.strip_suffix('$')?;
    let q = if p.starts_with("^(") { q.strip_suffix(')')? } else { q };
    let (min, max, next) = parse_quantifier(q.as_bytes(), 0)?;
    (next == q.len() && next > 0 && !q.ends_with('?')).then_some((min, max))
}

/// Literal text: characters other than syntax characters, and identity or control escapes.
fn literal_text(p: &str) -> Option<String> {
    let mut out = String::new();
    let mut chars = p.chars();
    while let Some(c) = chars.next() {
        match c {
            '\\' => out.push(match chars.next()? {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'f' => '\x0C',
                'v' => '\x0B',
                e
                @ ('^' | '$' | '\\' | '.' | '*' | '+' | '?' | '(' | ')' | '[' | ']' | '{' | '}' | '|' | '/' | '-') => e,
                _ => return None,
            }),
            '^' | '$' | '.' | '*' | '+' | '?' | '(' | ')' | '[' | ']' | '{' | '}' | '|' => return None,
            c => out.push(c),
        }
    }
    Some(out)
}

/// Splits at top-level `|`s (none inside a group, a class or after `\`).
fn split_alternatives(p: &str) -> Option<Vec<&str>> {
    let b = p.as_bytes();
    let (mut depth, mut in_class, mut i, mut start) = (0usize, false, 0, 0);
    let mut parts = Vec::new();
    while i < b.len() {
        match b[i] {
            b'\\' => i += 1,
            b'[' => in_class = true,
            b']' => in_class = false,
            b'(' if !in_class => depth += 1,
            b')' if !in_class => depth = depth.checked_sub(1)?,
            b'|' if !in_class && depth == 0 => {
                parts.push(&p[start..i]);
                start = i + 1;
            }
            _ => {}
        }
        i += 1;
    }
    parts.push(&p[start..]);
    Some(parts)
}

/// `^(a|b|...)$` or `^(?:a|b|...)$` over literal alternatives.
fn whole_alternatives(p: &str) -> Option<Vec<String>> {
    let inner = p.strip_prefix("^(")?.strip_suffix(")$")?;
    let inner = inner.strip_prefix("?:").unwrap_or(inner);
    if inner.starts_with('?') {
        return None;
    }
    let parts = split_alternatives(inner)?;
    if parts.len() < 2 {
        return None;
    }
    parts.into_iter().map(literal_text).collect()
}

/// At most this many alternatives after expanding groups.
const MAX_ALTERNATIVES: usize = 64;

/// At most this many alternatives when any is a class sequence: beyond it, trying each in turn is slower than the
/// `regex` crate's single automaton pass (measured on jsconfig's case-folded `lib` names).
const MAX_SEQUENCE_ALTERNATIVES: usize = 4;

/// Two or more alternatives once groups of alternatives (and optional groups) are expanded into whole alternatives
/// (`^a|b|c$`, `^([a|A]uto)|([n|N]one)$`, `^[Ee][Ss]2015(\.([Cc]ore|[Pp]roxy))?$`), each a literal or a class
/// sequence anchored where its own `^` and `$` say.
fn alternatives(p: &str) -> Option<Box<[Alternative]>> {
    let c: Vec<char> = p.chars().collect();
    let (parts, end) = expand(&c, 0)?;
    // A single alternative is only new here when a group was expanded (Sequence::parse takes the rest).
    if end != c.len() || parts.is_empty() || (parts.len() == 1 && parts[0] == p) {
        return None;
    }
    let alternatives: Box<[Alternative]> = parts
        .iter()
        .map(|part| {
            let (start, part) = part.strip_prefix('^').map_or((false, part.as_str()), |r| (true, r));
            let (end, body) = match part.strip_suffix('$') {
                Some(r) if !ends_with_escape(r) => (true, r),
                _ => (false, part),
            };
            if let Some(text) = literal_text(body) {
                return Some(Alternative::Literal { text: text.into(), start, end });
            }
            let seq = Sequence::parse(&format!("^{body}{}", if end { "$" } else { "" }))?;
            if start {
                return Some(Alternative::Start(seq));
            }
            let width =
                seq.items.iter().all(|i| i.min == i.max).then(|| seq.items.iter().map(|i| i.min as usize).sum())?;
            Some(if end { Alternative::End { seq, width } } else { Alternative::Anywhere { seq, width } })
        })
        .collect::<Option<_>>()?;
    let sequences = alternatives.iter().any(|a| !matches!(a, Alternative::Literal { .. }));
    (!sequences || alternatives.len() <= MAX_SEQUENCE_ALTERNATIVES).then_some(alternatives)
}

/// Whether the text ends in an unpaired `\` (so a `$` after it would be escaped).
fn ends_with_escape(t: &str) -> bool {
    t.bytes().rev().take_while(|&b| b == b'\\').count() % 2 == 1
}

/// Expands the alternatives from `i` up to an unmatched `)` or the end: groups of alternatives multiply out, a group
/// quantified by `?` also contributes the empty alternative, and everything else is copied. `None` for lookarounds,
/// other quantified groups, or too many alternatives.
fn expand(c: &[char], mut i: usize) -> Option<(Vec<String>, usize)> {
    let mut all = Vec::new();
    let mut branch = vec![String::new()];
    while i < c.len() {
        match c[i] {
            '|' => {
                all.append(&mut branch);
                branch.push(String::new());
                i += 1;
            }
            ')' => break,
            '(' => {
                i += 1;
                if c.get(i) == Some(&'?') {
                    if c.get(i + 1) != Some(&':') {
                        return None;
                    }
                    i += 2;
                }
                let (mut inner, next) = expand(c, i)?;
                if c.get(next) != Some(&')') {
                    return None;
                }
                i = next + 1;
                match c.get(i) {
                    Some('?') => {
                        inner.push(String::new());
                        i += 1;
                        if c.get(i) == Some(&'?') {
                            i += 1;
                        }
                    }
                    Some('{') => {
                        // An exact count repeats the group; any other bound needs a real regex.
                        let close = i + c[i..].iter().position(|&x| x == '}')?;
                        let n: usize = c[i + 1..close].iter().collect::<String>().parse().ok()?;
                        if n > 16 {
                            return None;
                        }
                        let mut repeated = vec![String::new()];
                        for _ in 0..n {
                            if repeated.len() * inner.len() > MAX_ALTERNATIVES {
                                return None;
                            }
                            repeated =
                                repeated.iter().flat_map(|r| inner.iter().map(move |x| format!("{r}{x}"))).collect();
                        }
                        inner = repeated;
                        i = close + 1;
                        if c.get(i) == Some(&'?') {
                            i += 1;
                        }
                    }
                    Some('*' | '+') => return None,
                    _ => {}
                }
                if branch.len() * inner.len() > MAX_ALTERNATIVES {
                    return None;
                }
                branch = branch.iter().flat_map(|b| inner.iter().map(move |x| format!("{b}{x}"))).collect();
            }
            '[' => {
                let start = i;
                i += 1;
                if c.get(i) == Some(&'^') {
                    i += 1;
                }
                if c.get(i) == Some(&']') {
                    i += 1;
                }
                while *c.get(i)? != ']' {
                    i += if c[i] == '\\' { 2 } else { 1 };
                }
                i += 1;
                let text: String = c[start..i].iter().collect();
                branch.iter_mut().for_each(|b| b.push_str(&text));
            }
            '\\' => {
                let text: String = c.get(i..i + 2)?.iter().collect();
                branch.iter_mut().for_each(|b| b.push_str(&text));
                i += 2;
            }
            ch => {
                branch.iter_mut().for_each(|b| b.push(ch));
                i += 1;
            }
        }
        if all.len() + branch.len() > MAX_ALTERNATIVES {
            return None;
        }
    }
    all.append(&mut branch);
    Some((all, i))
}

/// `^(?=[^SET]+$)(?=(.*\w)).+$` (with `(?:` or `(` around `.*\w`): the excluded set.
fn excluded_class_with_word(p: &str) -> Option<(CharSet, bool)> {
    // parse_class reads a class body from after `[`, here `^SET]`.
    let (bangs, body) = match p.strip_prefix("^(?=!+[") {
        Some(body) => (true, body),
        None => (false, p.strip_prefix("^(?=[")?),
    };
    let (negated, next) = parse_class(body.as_bytes(), 0)?;
    if !body.starts_with('^') {
        return None;
    }
    let excluded = CharSet { ascii: !negated.ascii, ..CharSet::EMPTY };
    // The run of `!` ends exactly where the class starts only when the class excludes `!`.
    if bangs && !excluded.contains('!') {
        return None;
    }
    matches!(&body[next..], "+$)(?=(.*\\w)).+$" | "+$)(?=(?:.*\\w)).+$" | "+$)(?=.*\\w).+$")
        .then_some((excluded, bangs))
}

/// The `regex` format: a valid ECMA-262 regular expression (with the `u` flag).
pub(crate) fn is_valid_ecma_regex(s: &str) -> bool {
    regress::Regex::with_flags(s, "u").is_ok()
}

// ---------------------------------------------------------------------------------------------------------------------
// Class sequences

/// A set of characters: ASCII by bitmask, then either all or none of the line separators U+2028 and U+2029, and
/// either all or none of the other non-ASCII characters.
#[derive(Clone, Copy, PartialEq, Eq, Debug)]
struct CharSet {
    ascii: u128,
    non_ascii: bool,
    separators: bool,
}

impl CharSet {
    const EMPTY: CharSet = CharSet { ascii: 0, non_ascii: false, separators: false };

    fn range(lo: u8, hi: u8) -> CharSet {
        let mut ascii = 0u128;
        for c in lo..=hi {
            ascii |= 1 << c;
        }
        CharSet { ascii, ..CharSet::EMPTY }
    }

    /// ECMA-262's `.`: everything but the line terminators.
    fn dot() -> CharSet {
        CharSet { ascii: !((1 << b'\n') | (1 << b'\r')), non_ascii: true, separators: false }
    }

    fn union(self, other: CharSet) -> CharSet {
        CharSet {
            ascii: self.ascii | other.ascii,
            non_ascii: self.non_ascii || other.non_ascii,
            separators: self.separators || other.separators,
        }
    }

    fn negate(self) -> CharSet {
        CharSet { ascii: !self.ascii, non_ascii: !self.non_ascii, separators: !self.separators }
    }

    fn disjoint(self, other: CharSet) -> bool {
        self.ascii & other.ascii == 0 && !(self.non_ascii && other.non_ascii) && !(self.separators && other.separators)
    }

    #[inline]
    fn contains(self, c: char) -> bool {
        match c as u32 {
            c if c < 128 => self.ascii & (1 << c) != 0,
            0x2028 | 0x2029 => self.separators,
            _ => self.non_ascii,
        }
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

#[derive(Debug, Clone)]
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
                b'.' => {
                    i += 1;
                    CharSet::dot()
                }
                b'(' | b')' | b'|' | b'^' | b'$' | b'*' | b'+' | b'?' | b'{' | b'}' | b']' => return None,
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
                let single =
                    i.min == 1 && i.max == 1 && !i.set.non_ascii && !i.set.separators && i.set.ascii.count_ones() == 1;
                single.then(|| i.set.ascii.trailing_zeros() as u8 as char)
            })
            .collect()
    }

    fn is_match(&self, s: &str) -> bool {
        if s.is_ascii() { self.is_match_ascii(s.as_bytes()) } else { self.is_match_chars(s) }
    }

    /// The same over any text, a character at a time.
    fn is_match_chars(&self, s: &str) -> bool {
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

    /// Greedily matches the items at the start of `s` (ignoring `to_end`): the bytes taken.
    fn consume(&self, s: &str) -> Option<usize> {
        let b = s.as_bytes();
        let mut at = 0;
        for item in self.items.iter() {
            let mut n = 0u32;
            while n < item.max && at < b.len() {
                let c = b[at];
                if c < 0x80 {
                    if item.set.ascii & (1 << c) == 0 {
                        break;
                    }
                    at += 1;
                } else {
                    let ch = s[at..].chars().next()?;
                    if !item.set.contains(ch) {
                        break;
                    }
                    at += ch.len_utf8();
                }
                n += 1;
            }
            if n < item.min {
                return None;
            }
        }
        Some(at)
    }

    /// Whether greedy matching is exact when `next` can follow the items: every variable item is disjoint from the
    /// items that can directly follow it (up to the first that cannot match nothing), `next` included.
    fn greedy_before(items: &[Item], next: CharSet) -> bool {
        items.iter().enumerate().all(|(i, item)| {
            if item.min == item.max {
                return true;
            }
            for following in &items[i + 1..] {
                if !item.set.disjoint(following.set) {
                    return false;
                }
                if following.min > 0 {
                    return true;
                }
            }
            item.set.disjoint(next)
        })
    }
}

/// A list of items between separators: `^I(SR)*$` or `^I(SR)+$` (`final_` is `None`), or `^(RS)*F$` and
/// `^(RS)+F$`. The separator is a fixed sequence and no variable class can run into what follows it, so greedy
/// matching splits the string exactly where the pattern does.
#[derive(Debug)]
struct SeparatedList {
    /// `I` of the first form.
    first: Option<Sequence>,
    repeated: Sequence,
    separator: Sequence,
    /// `F` of the second form, matched against the whole remainder.
    final_: Option<Sequence>,
    min_repeats: u32,
}

impl SeparatedList {
    fn is_match(&self, s: &str) -> bool {
        let mut rest = s;
        let mut repeats = 0;
        if let Some(first) = &self.first {
            let Some(n) = first.consume(rest) else { return false };
            rest = &rest[n..];
            loop {
                if rest.is_empty() {
                    return repeats >= self.min_repeats;
                }
                let Some(n) = self.separator.consume(rest) else { return false };
                rest = &rest[n..];
                let Some(n) = self.repeated.consume(rest) else { return false };
                rest = &rest[n..];
                repeats += 1;
            }
        }
        let final_ = self.final_.as_ref().expect("a separated list has a first or a final item");
        loop {
            if repeats >= self.min_repeats && final_.is_match(rest) {
                return true;
            }
            let Some(n) = self.repeated.consume(rest) else { return false };
            let Some(m) = self.separator.consume(&rest[n..]) else { return false };
            rest = &rest[n + m..];
            repeats += 1;
        }
    }

    fn parse(p: &str) -> Option<SeparatedList> {
        let body = p.strip_prefix('^')?.strip_suffix('$')?;
        if ends_with_escape(body) {
            return None;
        }
        let b = body.as_bytes();
        // Top-level groups: (start, end) of each, where end is the index of its `)`.
        let mut groups = Vec::new();
        let (mut depth, mut in_class, mut i, mut open) = (0usize, false, 0, 0);
        while i < b.len() {
            match b[i] {
                b'\\' => i += 1,
                b'[' if !in_class => in_class = true,
                b']' if in_class => in_class = false,
                b'(' if !in_class => {
                    if depth == 0 {
                        open = i;
                    }
                    depth += 1;
                }
                b')' if !in_class => {
                    depth = depth.checked_sub(1)?;
                    if depth == 0 {
                        groups.push((open, i));
                    }
                }
                _ => {}
            }
            i += 1;
        }
        let quantified: Vec<_> = groups.iter().filter(|&&(_, e)| matches!(b.get(e + 1), Some(b'*' | b'+'))).collect();
        let &&(open, close) = quantified.first().filter(|_| quantified.len() == 1)?;
        let min_repeats = u32::from(b[close + 1] == b'+');
        let group = strip_group(&body[open..=close])?;
        let before = &body[..open];
        let after = &body[close + 2..];
        let items = |t: &str| Sequence::parse(&format!("^{t}")).map(|s| s.items.into_vec());
        let seq = |items: &[Item], to_end| Sequence { items: items.into(), to_end };
        let fixed = |items: &[Item]| !items.is_empty() && items.iter().all(|i| i.min == i.max && i.min > 0);
        let g = items(group)?;
        match (before.is_empty(), after.is_empty()) {
            // ^I(SR)*$
            (false, true) => {
                let first = items(unwrap_group(before)?)?;
                (1..g.len()).find_map(|k| {
                    let (separator, repeated) = g.split_at(k);
                    let next = separator[0].set;
                    (fixed(separator)
                        && Sequence::greedy_before(&first, next)
                        && Sequence::greedy_before(repeated, next))
                    .then(|| SeparatedList {
                        first: Some(seq(&first, false)),
                        repeated: seq(repeated, false),
                        separator: seq(separator, false),
                        final_: None,
                        min_repeats,
                    })
                })
            }
            // ^(RS)*F$
            (true, false) => {
                let final_ = Sequence::parse(&format!("^{}$", unwrap_group(after)?))?;
                (1..g.len()).find_map(|k| {
                    let (repeated, separator) = g.split_at(k);
                    (fixed(separator) && Sequence::greedy_before(repeated, separator[0].set)).then(|| SeparatedList {
                        first: None,
                        repeated: seq(repeated, false),
                        separator: seq(separator, false),
                        final_: Some(Sequence { items: final_.items.clone(), to_end: true }),
                        min_repeats,
                    })
                })
            }
            _ => None,
        }
    }
}

/// The inside of `(...)` or `(?:...)`; `None` for other groups.
fn strip_group(g: &str) -> Option<&str> {
    let inner = g.strip_prefix('(')?.strip_suffix(')')?;
    match inner.strip_prefix("?:") {
        Some(rest) => Some(rest),
        None if inner.starts_with('?') => None,
        None => Some(inner),
    }
}

/// A text that is one group wrapping everything, unwrapped; otherwise the text itself.
fn unwrap_group(t: &str) -> Option<&str> {
    if t.starts_with('(') && t.ends_with(')') {
        let inner = strip_group(t)?;
        // Only when the parentheses enclose the whole text (`(a)(b)` is two groups).
        let mut depth = 0i32;
        let mut escaped = false;
        for c in inner.chars() {
            match c {
                _ if escaped => escaped = false,
                '\\' => escaped = true,
                '(' => depth += 1,
                ')' => {
                    depth -= 1;
                    if depth < 0 {
                        return None;
                    }
                }
                _ => {}
            }
        }
        return Some(inner);
    }
    Some(t)
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
        // ECMA's word boundaries are between ASCII word characters and the rest.
        'b' => {
            out.push_str("(?-u:\\b)");
            i + 1
        }
        'B' => {
            out.push_str("(?-u:\\B)");
            i + 1
        }
        // Unicode properties (validated as ECMA-262 names by regress) have the same names in the `regex` crate.
        'p' | 'P' if c.get(i + 1) == Some(&'{') => {
            let end = i + 1 + c[i + 1..].iter().position(|&x| x == '}')?;
            let body: String = c[i + 2..end].iter().collect();
            if body.is_empty() || !body.chars().all(|x| x.is_ascii_alphanumeric() || x == '_' || x == '=') {
                return None;
            }
            out.push('\\');
            out.push(e);
            out.push('{');
            out.push_str(&body);
            out.push('}');
            end + 1
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
        // Backreferences, \c, \k and the rest.
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
                'd' | 'D' | 'w' | 'W' | 's' | 'S' | 'p' | 'P' => None,
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
                if matches!(e, 'd' | 'D' | 'w' | 'W' | 's' | 'S' | 'p' | 'P') {
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
        ".",
        "^.",
        "^.+",
        "(.*)",
        "^(.*)",
        "^.+$",
        "^.{1,3}$",
        "^.{2}$",
        "^.{2,}$",
        "^(ab|cd)$",
        "^(?:es|ES|x-|a\\.b)$",
        "^(a|b|c|d|e|f|g|h|i|j|z)$",
        "^ab|cd$",
        "^x-|es|ms$",
        "a|b",
        "^a\\$|b",
        "\\Bs",
        "a\\b",
        "^\\p{Lu}",
        "[\\p{L}\\d]+$",
        "^\\P{L}+$",
        "^(?=[^a-c\\n]+$)(?=(.*\\w)).+$",
        "^\\-a",
        "[{}[\\]]",
        "(.+)",
        "^(.+)$",
        "^(.*)$",
        "^a(bc)?$",
        "^(a|b)c|d(e|f)$",
        "^([a|A][u|U][t|T][o|O])|([n|N][o|O][n|N][e|E])$",
        "^[Ee][Ss]2015(\\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?$",
        "^[Ee][Ss]([356]|20(1[567]|2[02])|[Nn][Ee][Xx][Tt])$",
        "^([a-z]+|x)-$",
        "^a|[0-9]{2}$",
        "^(a|b)*$",
        "^(?=a)a|b$",
        "^/.*",
        "^a.*",
        "^a\\\\.*",
        "(^([0-9]+)\\.([0-9]+)$)|(^\\{[A-F0-9]{2}(-[A-F0-9]{1}){2}\\}$)",
        "^[0-9]{1,}.[0-9]{1,}$",
        "^3\\.1\\.\\d+(-.+)?$",
        "^([A-Za-z_][-A-Za-z0-9_.:]*)$",
        "^a.c$",
        "^[a-z].$",
        "x.y|^z",
        "^es|ms|x-$",
        "^(ab){2}$",
        "^(a|b){2}c$",
        "^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$",
        "^([a-z][a-z0-9]{0,3})(\\.[a-z][a-z0-9]{0,3})*$",
        "^([a-z_$][a-z0-9_$]{0,3}\\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,3})$",
        "^([a-z]+)(,[a-z]+)+$",
        "^(a,)*b$",
        "^(ab,)+a$",
        "^a(,a)*$",
        "^[a-z]*(-[a-z]*)*$",
        "^(a-)*a-b$",
        "^(é,)*a$",
        "^(?=!+[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$",
        "^(?=!+[^a]+$)(?=(.*\\w)).+$",
    ];

    /// Every matcher agrees with regress on strings over an alphabet that exercises classes, anchors and non-ASCII.
    #[test]
    fn matchers_agree_with_regress() {
        let alphabet = [
            "a", "b", "z", "A", "X", "Z", "0", "1", "5", "9", "_", "-", ".", ":", "/", "@", "#", "$", "*", "!", "{",
            "}", "|", " ", "\n", "\u{a0}", "é", "µ", "😀", "\u{2028}", "x-", "es", "ES", "ms", "txt", "Au", "to", "No",
            "ne", "2015", "Co", "re", "20", "15", "22", "2", ",", "a,", "ab,", "a-",
        ];
        let mut seed: u64 = 0x2545_f491_4f6c_dd1d;
        let mut next = || {
            seed ^= seed << 13;
            seed ^= seed >> 7;
            seed ^= seed << 17;
            seed
        };
        for &p in PATTERNS {
            let (reference, _) = compile_regress(p).unwrap();
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
        for p in ["^[a-z]*a$", "(base64key|awskms)://(.*)"] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Regex(_)), "{p}");
        }
        for p in [".+", "^.+", "(.+)"] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::HasContent { .. }), "{p}");
        }
        for p in ["^.{1,256}$", "^.+$", "^.*$", "^(.*)$", "^(.+)$"] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Line { .. }), "{p}");
        }
        for p in ["^(ab|cd)$", "^(?:es|ES|x-)$"] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Literals(_)), "{p}");
        }
        for p in [
            "^ab|cd$",
            "a|b",
            "^([a|A][u|U][t|T][o|O])|([n|N][o|O][n|N][e|E])$",
            "^[Ee][Ss]2015(\\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?$",
            "^[1-5](?:[0-9]{2}|XX)$",
            "(^([0-9]+)\\.([0-9]+)$)|(^\\{[A-F0-9]{8}(-[A-F0-9]{4}){3}-[A-F0-9]{12}\\}$)",
            "^([t|T][o|O][p|P])|([c|C][e|E][n|N][t|T][e|E][r|R])|([b|B][o|O][t|T][t|T][o|O][m|M])$",
            "^([A-Za-z_][-A-Za-z0-9_.:]*)$",
        ] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Alternatives(_)), "{p}");
        }
        for p in [
            "^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$",
            "^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$",
            "^([a-z_$][a-z0-9_$]{0,39}\\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$",
        ] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::SeparatedList(_)), "{p}");
        }
        assert!(matches!(
            compile("^(?=[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$").unwrap().matcher,
            Matcher::ExcludedClassWithWord { .. }
        ));
        for p in ["\\bfoo", "^\\p{L}+$"] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Regex(_)), "{p}");
        }
        for p in ["^((\\.(?!\\.)\\/)?\\w+\\/?)+$", "^\\-a"] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Regress(_)), "{p}");
        }
    }
}

#[cfg(test)]
mod corpus_patterns {
    /// Lists which matcher each pattern in `CORVUS_PATTERNS` (corpus<TAB>pattern lines) gets.
    #[test]
    #[ignore]
    fn classify_corpus_patterns() {
        let Ok(path) = std::env::var("CORVUS_PATTERNS") else { return };
        for line in std::fs::read_to_string(path).unwrap().lines() {
            let (corpus, p) = line.split_once('\t').unwrap();
            let kind = super::compile(p)
                .map(|c| match &c.matcher {
                    super::Matcher::Regex(_) => "regex",
                    super::Matcher::Regress(_) => "regress",
                    _ => "fast",
                })
                .unwrap_or("invalid");
            println!("{kind}\t{corpus}\t{p}");
        }
    }
}
