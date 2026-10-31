//! `pattern`/`patternProperties` matching with ECMA-262 semantics (the `u` flag), as JSON Schema specifies. A
//! pattern that is valid only without the flag is accepted too, and matched as ECMA-262 says for it: by UTF-16
//! code unit.
//!
//! A pattern gets the cheapest matcher that decides it exactly:
//!
//! - patterns every string matches (`""`, `.*`, `[\s\S]*` and the like), `.+`, and line lengths (`^.{1,256}$`);
//! - anchored sequences of quantified ASCII character classes and literals (`^[a-z][a-z0-9_]{0,29}$`, `^x-`,
//!   `^[@$_#]`), matched in one pass over the string (the TypeScript evaluator's class-sequence fast path);
//! - alternatives of such sequences once groups are multiplied out (`^([a|A]uto)|([n|N]one)$`,
//!   `^[Ee][Ss]2015(\.([Cc]ore|[Pp]roxy))?$`), sets of literals, and separated lists (`^([a-z]+)(\.[a-z]+)*$`),
//!   as the C# evaluator's pattern matchers do;
//! - anything else by [Corvus.Regex.Ecma262](https://docs.rs/corvus-regex-ecma262), an ECMA-262 engine that answers
//!   every pattern as V8 does, as the pattern is written, and matches a pattern with no backreference in time
//!   proportional to the pattern's size times the string.
//!
//! Whether a pattern is valid is the engine's answer, whichever matcher runs it, in the grammar of the edition the
//! dialect reads patterns with (ECMAScript 2020 for draft 2019-09 and draft 2020-12, today's for the others; see
//! [`Grammar`]), and so are the limits of size: a pattern over one is refused before any matcher reads it.
//!
//! Patterns compile once per process for each grammar (a pattern is immutable, so identical patterns share one
//! matcher).

use std::collections::{HashMap, VecDeque};
use std::fmt;
use std::sync::{Arc, LazyLock, Mutex};
use std::time::Duration;

use corvus_regex_ecma262::{Edition, Flags, MatchError, Regex, RegexBuilder, UnicodeVersion};

use crate::dialect::Dialect;
use crate::options::{
    CompileOptions, PatternMatchLimitError, PatternTooLargeError, SchemaCompilationError, SchemaCompilationErrorKind,
};

/// The grammar a pattern is read in: the edition of ECMA-262 and the version of Unicode.
#[derive(Clone, Copy, Debug, PartialEq, Eq, Hash)]
pub(crate) struct Grammar {
    pub edition: Edition,
    pub unicode_version: UnicodeVersion,
}

impl Grammar {
    /// The grammar of a dialect's patterns: the edition the options give, or by default ECMAScript 2020 for draft
    /// 2019-09 and draft 2020-12, which cite it, and today's for the earlier drafts; and the options' version of
    /// Unicode.
    pub(crate) fn of(dialect: Dialect, options: &CompileOptions) -> Grammar {
        let edition = options.regex_edition.unwrap_or(if dialect >= Dialect::Draft201909 {
            Edition::Es2020
        } else {
            Edition::Current
        });
        Grammar { edition, unicode_version: options.regex_unicode_version }
    }

    /// The flags of the grammar of the `u` flag, or of no flag.
    fn flags(unicode: bool) -> Flags {
        Flags { unicode, ..Flags::default() }
    }

    /// The pattern compiled in this grammar, with the `u` flag or with none.
    fn build(self, pattern: &str, unicode: bool) -> Result<Regex, corvus_regex_ecma262::Error> {
        RegexBuilder::new(pattern)
            .with_flags(Grammar::flags(unicode))
            .edition(self.edition)
            .unicode_version(self.unicode_version)
            .build()
    }

    /// Whether the pattern is valid in this grammar with the `u` flag.
    fn is_valid_unicode(self, pattern: &str) -> bool {
        Regex::is_valid_with(pattern, Grammar::flags(true), self.edition, self.unicode_version)
    }
}

/// A compiled `pattern`.
pub(crate) struct Pattern {
    pub source: String,
    grammar: Grammar,
    matcher: Matcher,
    /// What the pattern is counted as in the cache of compiled patterns, in bytes (see [`Cache`]).
    retained: usize,
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
    /// The `corvus-regex-ecma262` engine: by character for a pattern with the `u` flag, and by UTF-16 code unit for one that is
    /// valid only without it.
    Engine(Box<Regex>),
}

impl fmt::Debug for Pattern {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(f, "Pattern({:?})", self.source)
    }
}

/// The limit of a match, as a validator holds it.
#[derive(Clone, Copy, Debug)]
pub(crate) struct PatternLimit {
    /// The time one match may take, or `None` for no limit.
    limit: Option<Duration>,
}

impl PatternLimit {
    /// A limit of `limit` for each match. `None` and zero are no limit.
    pub(crate) fn new(limit: Option<Duration>) -> PatternLimit {
        PatternLimit { limit: limit.filter(|l| !l.is_zero()) }
    }
}

/// What one evaluation, or the compilation of one schema, gives every match of a pattern: the time each may take.
/// It notes the first match that reached a limit, after which every match is abandoned at once.
pub(crate) struct PatternBudget {
    limit: PatternLimit,
    /// The match that reached a limit. It is boxed so that an evaluation, which holds a budget, stays small.
    failure: Option<Box<PatternMatchLimitError>>,
}

impl PatternBudget {
    #[inline(always)]
    pub(crate) fn new(limit: PatternLimit) -> PatternBudget {
        PatternBudget { limit, failure: None }
    }

    /// Whether a match has reached a limit, so that what the evaluation came to is not an answer.
    #[inline(always)]
    pub(crate) fn failed(&self) -> bool {
        self.failure.is_some()
    }

    /// The error of the match that reached a limit, if one did.
    pub(crate) fn take_failure(&mut self) -> Option<PatternMatchLimitError> {
        self.failure.take().map(|e| *e)
    }

    /// Matches the string on the engine within the budget. When a limit is reached the answer is false, and it is
    /// not an answer: the caller of the evaluation gets the error.
    fn engine_match(&mut self, engine: &Regex, source: &str, s: &str) -> bool {
        if self.failure.is_some() {
            return false;
        }
        let result = match self.limit.limit {
            Some(limit) => engine.is_match_within(s, limit),
            None => engine.is_match(s),
        };
        result.unwrap_or_else(|e| {
            self.failure = Some(Box::new(PatternMatchLimitError::new(
                source,
                self.limit.limit.unwrap_or_default(),
                e == MatchError::Memory,
            )));
            false
        })
    }
}

impl Pattern {
    /// Whether the pattern matches somewhere in the string. The engine matches within the budget. Every other
    /// matcher reads the string once, or a bounded number of times, and has no limit.
    #[inline]
    pub fn is_match(&self, s: &str, budget: &mut PatternBudget) -> bool {
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
                    word |= CharSet::WORD.contains(c);
                }
                word
            }
            Matcher::Engine(engine) => budget.engine_match(engine, &self.source, s),
        }
    }
}

/// The kind of matcher a pattern has, for the tables of which pattern goes where.
#[cfg(test)]
pub(crate) fn kind(p: &Pattern) -> &'static str {
    match &p.matcher {
        Matcher::Engine(engine) if engine.flags().unicode => "engine",
        Matcher::Engine(_) => "engine-units",
        Matcher::Everything => "everything",
        Matcher::Literal { .. } => "literal",
        Matcher::Sequence(_) => "sequence",
        Matcher::SeparatedList(_) => "list",
        Matcher::HasContent { .. } => "has-content",
        Matcher::Line { .. } => "line",
        Matcher::Literals(_) => "literals",
        Matcher::Alternatives(_) => "alternatives",
        Matcher::ExcludedClassWithWord { .. } => "excluded-class",
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

/// The most patterns the process-wide cache holds.
const MAX_CACHED_PATTERNS: usize = 1024;

/// The most the patterns of the cache come to, in the bytes counted for each: 64 MiB. A pattern is counted as its
/// source and an estimate of what its matcher holds (see [`compile`]).
const MAX_CACHED_BYTES: usize = 64 << 20;

/// A pattern counted as more than this is compiled for the schema that has it and not kept in the cache.
const MAX_CACHED_PATTERN_BYTES: usize = MAX_CACHED_BYTES / 32;

/// The compiled patterns of the process, so that identical patterns share one matcher. It is bounded in number and
/// in size, since the patterns of a schema may come from outside: when it is over either bound the patterns that
/// were compiled first go, oldest first. A validator holds the patterns of its schema itself, so a pattern that has
/// left the cache is still matched by the validators that have it, and is compiled again only for a schema compiled
/// later.
#[derive(Default)]
struct Cache {
    patterns: HashMap<(Arc<str>, Grammar), Arc<Pattern>>,
    /// The sources and their grammars in the order they were compiled.
    order: VecDeque<(Arc<str>, Grammar)>,
    /// The sum of what the patterns are counted as.
    bytes: usize,
}

impl Cache {
    fn get(&self, source: &str, grammar: Grammar) -> Option<Arc<Pattern>> {
        self.patterns.get(&(Arc::from(source), grammar)).cloned()
    }

    /// Keeps a pattern, and returns the one to use: the pattern itself, or the one another thread kept for the same
    /// source meanwhile.
    fn insert(&mut self, pattern: Arc<Pattern>) -> Arc<Pattern> {
        if pattern.retained > MAX_CACHED_PATTERN_BYTES {
            return pattern;
        }
        let key: (Arc<str>, Grammar) = (Arc::from(pattern.source.as_str()), pattern.grammar);
        if let Some(kept) = self.patterns.get(&key) {
            return kept.clone();
        }
        self.bytes += pattern.retained;
        self.patterns.insert(key.clone(), pattern.clone());
        self.order.push_back(key);
        while self.patterns.len() > MAX_CACHED_PATTERNS || self.bytes > MAX_CACHED_BYTES {
            let Some(oldest) = self.order.pop_front() else { break };
            if let Some(evicted) = self.patterns.remove(&oldest) {
                self.bytes -= evicted.retained;
            }
        }
        pattern
    }
}

static CACHE: LazyLock<Mutex<Cache>> = LazyLock::new(|| Mutex::new(Cache::default()));

fn cache() -> std::sync::MutexGuard<'static, Cache> {
    CACHE.lock().unwrap_or_else(|e| e.into_inner())
}

/// Why a pattern does not compile.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub(crate) enum PatternError {
    /// It is not a valid ECMA-262 regular expression.
    Invalid,
    /// It is over a limit of size. It was not read to its end, so nothing is known of whether it is valid.
    TooLarge(corvus_regex_ecma262::TooLargeError),
}

impl PatternError {
    /// The error of a schema whose keyword `keyword` holds the pattern `pattern`.
    pub(crate) fn of(self, pattern: &str, keyword: &'static str) -> SchemaCompilationError {
        match self {
            PatternError::Invalid => SchemaCompilationError::of_kind(
                format!("Invalid regular expression '{pattern}' in {keyword}."),
                SchemaCompilationErrorKind::InvalidPattern,
            ),
            PatternError::TooLarge(large) => {
                let error =
                    PatternTooLargeError::new(pattern, keyword, large.limit.name(), large.value, large.to_string());
                SchemaCompilationError::of_kind(error.to_string(), SchemaCompilationErrorKind::PatternTooLarge(error))
            }
        }
    }
}

impl From<corvus_regex_ecma262::Error> for PatternError {
    fn from(e: corvus_regex_ecma262::Error) -> PatternError {
        match e {
            corvus_regex_ecma262::Error::TooLarge(large) => PatternError::TooLarge(large),
            // Not valid in the grammar, or (not given here) flags that are not.
            _ => PatternError::Invalid,
        }
    }
}

/// A pattern in quotes for a message, or its first 120 bytes and its length when it is longer.
pub(crate) fn shown_pattern(pattern: &str) -> String {
    const SHOWN: usize = 120;
    if pattern.len() <= SHOWN {
        return format!("'{pattern}'");
    }
    let mut cut = SHOWN;
    while !pattern.is_char_boundary(cut) {
        cut -= 1;
    }
    format!("'{}...' ({} bytes)", &pattern[..cut], pattern.len())
}

/// Compiles (or fetches from the process-wide cache) a pattern, in the grammar of its dialect.
pub(crate) fn compile(pattern: &str, grammar: Grammar) -> Result<Arc<Pattern>, PatternError> {
    if let Some(p) = cache().get(pattern, grammar) {
        return Ok(p);
    }
    // The limits of size come first: nothing below reads a pattern that is over one. Validity is ECMA-262's: a
    // pattern the engine refuses is an error, whichever matcher would run it. A pattern is read with the u flag, and
    // failing that with no flag, whose grammar (Annex B) many schemas need.
    let (engine, unicode) = match grammar.build(pattern, true) {
        Ok(engine) => (engine, true),
        Err(corvus_regex_ecma262::Error::TooLarge(large)) => return Err(PatternError::TooLarge(large)),
        Err(_) => (grammar.build(pattern, false)?, false),
    };
    let matcher = choose(pattern, unicode, grammar).unwrap_or_else(|| Matcher::Engine(Box::new(engine)));
    // What the cache counts for the pattern (see `MAX_CACHED_BYTES`): its source, and for the engine an estimate of
    // its program and automata, which grow with the pattern.
    let mut retained = pattern.len() + 256;
    if matches!(matcher, Matcher::Engine(_)) {
        retained += 16 * pattern.len();
    }
    Ok(cache().insert(Arc::new(Pattern { source: pattern.to_string(), grammar, matcher, retained })))
}

fn choose(pattern: &str, unicode: bool, grammar: Grammar) -> Option<Matcher> {
    // Unanchored (or start-anchored) `.*` finds an empty match in any string; `^.*$` does not (`.` stops at a line
    // terminator), so it is not listed.
    if matches!(pattern, "" | ".*" | "^.*" | ".*$" | "(.*)" | "^(.*)" | "[\\s\\S]*" | "^[\\s\\S]*" | "^[\\s\\S]*$") {
        return Some(Matcher::Everything);
    }
    // Without the `u` flag, a pattern matches UTF-16 code units rather than characters: only the engine has that.
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
        && grammar.is_valid_unicode(rest)
        && let Some(m) = choose(rest, true, grammar)
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
    None
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

/// The widest class sequence, in characters, and the longest literal, in bytes, that is searched for at every
/// position of a text. The search for a sequence takes the text times this, and these matchers have no limit, so a
/// wider one is left to the engine, which has the limit of a match. (The rule is the Go module's, so that the ports
/// give a pattern the same kind of matcher.)
const MAX_ANYWHERE_WIDTH: usize = 64;

/// Two or more alternatives once groups of alternatives (and optional groups) are expanded into whole alternatives
/// (`^a|b|c$`, `^([a|A]uto)|([n|N]one)$`, `^[Ee][Ss]2015(\.([Cc]ore|[Pp]roxy))?$`), each a literal or a class
/// sequence anchored where its own `^` and `$` say.
fn alternatives(p: &str) -> Option<Box<[Alternative]>> {
    let c: Vec<char> = p.chars().collect();
    let (parts, end) = expand(&c, 0, 0)?;
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
                // A literal that is anchored at neither end is searched for at every position of the text.
                if !start && !end && text.len() > MAX_ANYWHERE_WIDTH {
                    return None;
                }
                return Some(Alternative::Literal { text: text.into(), start, end });
            }
            let seq = Sequence::parse(&format!("^{body}{}", if end { "$" } else { "" }))?;
            if start {
                return Some(Alternative::Start(seq));
            }
            let width =
                seq.items.iter().all(|i| i.min == i.max).then(|| seq.items.iter().map(|i| i.min as usize).sum())?;
            if end {
                return Some(Alternative::End { seq, width });
            }
            // A sequence that is not anchored is tried at every position of the text, and reads its width at each.
            (width <= MAX_ANYWHERE_WIDTH).then_some(Alternative::Anywhere { seq, width })
        })
        .collect::<Option<_>>()?;
    let sequences = alternatives.iter().any(|a| !matches!(a, Alternative::Literal { .. }));
    (!sequences || alternatives.len() <= MAX_SEQUENCE_ALTERNATIVES).then_some(alternatives)
}

/// Whether the text ends in an unpaired `\` (so a `$` after it would be escaped).
fn ends_with_escape(t: &str) -> bool {
    t.bytes().rev().take_while(|&b| b == b'\\').count() % 2 == 1
}

/// The deepest nesting of groups that is multiplied out. `expand` goes a level deeper for each, and a pattern may
/// nest a thousand deep, which the stack of a thread cannot be relied on to hold. A pattern that nests deeper than
/// this is left to the matchers after this one.
const MAX_EXPANDED_NESTING: usize = 32;

/// Expands the alternatives from `i` up to an unmatched `)` or the end: groups of alternatives multiply out, a group
/// quantified by `?` also contributes the empty alternative, and everything else is copied. `None` for lookarounds,
/// other quantified groups, too many alternatives, or groups nested deeper than `MAX_EXPANDED_NESTING`.
fn expand(c: &[char], mut i: usize, depth: usize) -> Option<(Vec<String>, usize)> {
    if depth > MAX_EXPANDED_NESTING {
        return None;
    }
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
                let (mut inner, next) = expand(c, i, depth + 1)?;
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

/// The `regex` format: a valid ECMA-262 regular expression in the grammar of the `u` flag, of the edition and version
/// of Unicode the dialect reads patterns with. A string over one of the engine's limits of size
/// ([`corvus_regex_ecma262::MAX_PATTERN_LENGTH`], [`corvus_regex_ecma262::MAX_NESTING`],
/// [`corvus_regex_ecma262::MAX_SIZE`]) is not read to its end, so it is not reported as valid.
pub(crate) fn is_valid_ecma_regex(s: &str, grammar: Grammar) -> bool {
    grammar.is_valid_unicode(s)
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

    /// ASCII `lo..=hi` (`hi` below 128).
    const fn range(lo: u8, hi: u8) -> CharSet {
        let mut ascii = 0u128;
        let mut c = lo;
        while c <= hi {
            ascii |= 1 << c;
            c += 1;
        }
        CharSet { ascii, ..CharSet::EMPTY }
    }

    /// ECMA-262's `.`: everything but the line terminators.
    fn dot() -> CharSet {
        CharSet { ascii: !((1 << b'\n') | (1 << b'\r')), non_ascii: true, separators: false }
    }

    const fn union(self, other: CharSet) -> CharSet {
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

    /// Whether the set holds an ASCII character (a `u128` bit test costs several instructions; a word select fewer).
    #[inline]
    fn has_ascii(&self, c: u8) -> bool {
        let word = if c < 64 { self.ascii as u64 } else { (self.ascii >> 64) as u64 };
        (word >> (c & 63)) & 1 != 0
    }

    #[inline]
    fn contains(self, c: char) -> bool {
        match c as u32 {
            c if c < 128 => self.has_ascii(c as u8),
            0x2028 | 0x2029 => self.separators,
            _ => self.non_ascii,
        }
    }

    const DIGIT: CharSet = CharSet::range(b'0', b'9');

    /// `\w`. A constant: built per call, its ranges cost tens of nanoseconds, which matchers testing it per character
    /// paid per character.
    const WORD: CharSet = CharSet::range(b'0', b'9')
        .union(CharSet::range(b'A', b'Z'))
        .union(CharSet::range(b'a', b'z'))
        .union(CharSet::range(b'_', b'_'));

    fn digit() -> CharSet {
        CharSet::DIGIT
    }

    fn word() -> CharSet {
        CharSet::WORD
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
        let mut at = 0usize;
        for item in self.items.iter() {
            let start = at;
            let end = b.len().min(start.saturating_add(item.max as usize));
            while at < end && item.set.has_ascii(b[at]) {
                at += 1;
            }
            if at - start < item.min as usize {
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
                    if !item.set.has_ascii(c) {
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
        // Whether the text is ASCII is worked out once, for the whole of it. The final sequence is tried after
        // every item, and reading what is left of the text each time to see whether it is ASCII made the match take
        // time quadratic in the number of items.
        let ascii = s.is_ascii();
        loop {
            if repeats >= self.min_repeats
                && (if ascii { final_.is_match_ascii(rest.as_bytes()) } else { final_.is_match_chars(rest) })
            {
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
        // The items of a text that is a class sequence. A `$` at the end of the text is not an item, and the list has
        // no place for it: `^x(,a$)*$` is not `^x(,a)*$`. Such a text is not a part of a list.
        let items = |t: &str| Sequence::parse(&format!("^{t}")).filter(|s| !s.to_end).map(|s| s.items.into_vec());
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
                    (fixed(separator)
                        && Sequence::greedy_before(repeated, separator[0].set)
                        && final_stops_at(&final_, separator[0].set))
                    .then(|| SeparatedList {
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

/// Whether the final sequence of a list cannot read past the separator that follows an item: no item of it holds a
/// character the first item of the separator holds. Tried at the start of an item, it then fails at that separator
/// at the latest, so trying it after every item reads the text a bounded number of times. A final sequence that can
/// read through separators would read to the end of the text from every item.
fn final_stops_at(final_: &Sequence, separator: CharSet) -> bool {
    final_.items.iter().all(|item| item.set.disjoint(separator))
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
        // The set holds ASCII members only. A member outside ASCII leaves the pattern to the engine: a byte of 128
        // or more is not a bit of the set, and shifting by it overflows.
        if !c.is_ascii() {
            return None;
        }
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
            if lo > hi || !hi.is_ascii() {
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

#[cfg(test)]
mod tests {
    use super::*;

    const G: Grammar = Grammar { edition: Edition::Current, unicode_version: UnicodeVersion::Default };

    /// A pattern compiled in today's grammar.
    fn compile(p: &str) -> Result<Arc<Pattern>, PatternError> {
        super::compile(p, G)
    }

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
        // Lists with an anchor or an assertion inside them, and with an escaped anchor.
        "^x(,a$)*$",
        "^a(,a$)+$",
        "^x$(,a)*$",
        "^(a$)(,a)*$",
        "^$(,a)*$",
        "^(a,$)*b$",
        "^([a-z]+,$)+a$",
        "^a(^,a)*$",
        "^(^a,)*b$",
        "^a(,a\\b)*$",
        "^a(,a\\$)*$",
        "^a\\$(,a)*$",
        "^(a\\$,)*b$",
        // Valid only without the `u` flag (identity escapes), so matching code units.
        "^\\/[^\\*\\?\\&\\%]*(\\/\\*)?$",
        "^[\\&\\@\\_]+$",
        "a\\&.b",
        "^.{2}$",
        "^[^\\%]{1,3}$",
        "\\uD83D\\uDE00|\\&",
    ];

    /// Every matcher agrees with the engine's backtracking matcher on strings over an alphabet that exercises
    /// classes, anchors and non-ASCII.
    #[test]
    fn matchers_agree_with_the_engine() {
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
            let reference = G.build(p, true).or_else(|_| G.build(p, false)).unwrap();
            let compiled = compile(p).unwrap();
            for _ in 0..4000 {
                let len = next() % 9;
                let s: String = (0..len).map(|_| alphabet[(next() % alphabet.len() as u64) as usize]).collect();
                // Without the `u` flag the engine matches the UTF-16 code units of the string.
                let found = reference.is_match(&s).unwrap();
                for limit in [None, Some(Duration::from_secs(60)), Some(Duration::from_nanos(1))] {
                    let mut budget = PatternBudget::new(PatternLimit::new(limit));
                    let got = compiled.is_match(&s, &mut budget);
                    // A match on the engine that reached its first check of the clock has no answer.
                    assert!(got == found || budget.failed(), "{p:?} on {s:?} with a limit of {limit:?}");
                    assert!(limit.is_some_and(|l| l.as_nanos() == 1) || !budget.failed(), "{p:?} on {s:?}");
                }
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
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Engine(_)), "{p}");
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
            // An escaped `$` or `^` is a literal character of the list.
            "^x(,a\\$)*$",
            "^x\\$(,a)*$",
            "^(a\\$,)*b$",
            "^x(,\\^a)*$",
            "^(a,)*b\\$$",
            "^x(,[$^]a)*$",
        ] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::SeparatedList(_)), "{p}");
        }
        // A list with an anchor or an assertion inside it has no shape.
        for p in [
            "^x(,a$)*$",
            "^x(?:,a$)+$",
            "^x$(,a)*$",
            "^(x$)(,a)*$",
            "^$(,a)*$",
            "^(a,$)*b$",
            "^x(^,a)*$",
            "^(^a,)*b$",
            "^x(,(a$))*$",
            "^x(,a\\b)*$",
            "^x\\B(,a)*$",
            "^x(,a(?=,|$))*$",
            "^x(?=,)(,a)*$",
        ] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Engine(_)), "{p}");
        }
        assert!(matches!(
            compile("^(?=[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$").unwrap().matcher,
            Matcher::ExcludedClassWithWord { .. }
        ));
        for p in ["\\bfoo", "^\\p{L}+$"] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Engine(_)), "{p}");
        }
        for p in ["^\\/[^\\*\\?\\&\\%]*(\\/\\*)?$", "a\\&.b", "^\\-a"] {
            assert!(matches!(&compile(p).unwrap().matcher, Matcher::Engine(engine) if !engine.flags().unicode), "{p}");
        }
        for p in ["^((\\.(?!\\.)\\/)?\\w+\\/?)+$", "^\\1(a)", "\\Bs", "(?i:a)", "^[]", "\\cA"] {
            assert!(matches!(compile(p).unwrap().matcher, Matcher::Engine(_)), "{p}");
        }
        // Valid only without the `u` flag, and not in the syntax `regex` takes: the engine, by code unit.
        for p in ["^\\p{L}\\&", "^\\-?\u{10400}*$", "^\\-(?=a)"] {
            let compiled = compile(p).unwrap();
            assert!(matches!(&compiled.matcher, Matcher::Engine(engine) if !engine.flags().unicode), "{p}");
        }
    }
}

#[cfg(test)]
mod limits {
    use super::*;

    const G: Grammar = Grammar { edition: Edition::Current, unicode_version: UnicodeVersion::Default };

    /// A pattern compiled in today's grammar.
    fn compile(p: &str) -> Result<Arc<Pattern>, PatternError> {
        super::compile(p, G)
    }

    /// Which matcher a pattern gets (the Go module's `TestAWideSearchIsLeftToTheEngine`). A sequence or a literal
    /// that would be searched for at every position of the text keeps its matcher up to 64 characters, and beyond
    /// that is left to a matcher that takes time proportional to the text or has the limit of a match.
    #[test]
    fn a_wide_search_is_left_to_the_engine() {
        let a = |n: usize| "a".repeat(n);
        for (pattern, alternatives) in [
            (r"[a-z]{64}[0-9]{0}|x".to_string(), true),
            (r"[a-z]{63}[0-9]|x".to_string(), true),
            (r"[a-z]{64}[0-9]|x".to_string(), false),
            (r"[a-z]{50000}[0-9]|x".to_string(), false),
            (r"^[a-z]{50000}[0-9]|x".to_string(), true),
            (r"[a-z]{50000}[0-9]$|^x".to_string(), true),
            (format!("{}|^x", a(64)), true),
            (format!("{}|^x", a(65)), false),
            (format!("^{}|^x", a(65)), true),
            (format!("{}$|^x", a(65)), true),
        ] {
            let found = kind(&compile(&pattern).unwrap());
            assert_eq!(found == "alternatives", alternatives, "{pattern:.30} has the matcher {found}");
            assert!(alternatives || found == "engine", "{pattern:.30} has the matcher {found}");
        }
        // The final sequence of a list must stop at the separator.
        for (pattern, list) in [
            (r"^(?:[a-z]+,)*[a-z]{2}[0-9]$", true),
            (r"^(?:[a-z]+,)*[a-z,]+[0-9]$", false),
            (r"^(?:[a-z]+,)*[0-9]{1,3}$", true),
        ] {
            let found = kind(&compile(pattern).unwrap());
            assert_eq!(found == "list", list, "{pattern} has the matcher {found}");
        }
    }

    /// The cache of compiled patterns is bounded in number and in size, and a pattern that has left it is still
    /// matched by whoever holds it.
    #[test]
    fn the_cache_of_patterns_is_bounded() {
        let mut budget = PatternBudget::new(PatternLimit::new(None));
        let first = compile("^cache-bound-0+$").unwrap();
        let held: Vec<Arc<Pattern>> =
            (0..3 * MAX_CACHED_PATTERNS).map(|n| compile(&format!("^cache-bound-{n}(?=x)x$")).unwrap()).collect();
        {
            let cache = cache();
            assert!(cache.patterns.len() <= MAX_CACHED_PATTERNS && cache.bytes <= MAX_CACHED_BYTES);
            assert_eq!(cache.patterns.len(), cache.order.len());
            assert_eq!(cache.bytes, cache.patterns.values().map(|p| p.retained).sum::<usize>());
            assert!(cache.get("^cache-bound-0+$", G).is_none(), "the oldest pattern is still in the cache");
        }
        assert!(first.is_match("cache-bound-000", &mut budget));
        assert!(held[0].is_match("cache-bound-0x", &mut budget) && !held[7].is_match("cache-bound-0x", &mut budget));
        // The same source compiles again, to a matcher of its own.
        let again = compile("^cache-bound-0+$").unwrap();
        assert!(!Arc::ptr_eq(&first, &again) && again.is_match("cache-bound-0", &mut budget));
        // Large patterns leave the cache by size, well before their number is the bound.
        let class = r"[\u0100\u0102\u0104\u0106]".repeat(1500);
        let large: Vec<Arc<Pattern>> = (0..200).map(|n| compile(&format!("^(?=large-{n}){class}$")).unwrap()).collect();
        assert!(large[0].retained > 256 << 10, "a large pattern is counted as {} bytes", large[0].retained);
        assert!(cache().bytes <= MAX_CACHED_BYTES);
        // A pattern too large for the cache is not kept at all.
        let huge = compile(&format!("^(?=huge){}$", class.repeat(4))).unwrap();
        assert!(huge.retained > MAX_CACHED_PATTERN_BYTES && cache().get(&huge.source, G).is_none());
    }
}

#[cfg(test)]
mod corpus_patterns {
    use std::sync::Arc;

    use super::{Edition, Grammar, Pattern, PatternError, UnicodeVersion};

    const G: Grammar = Grammar { edition: Edition::Current, unicode_version: UnicodeVersion::Default };

    /// A pattern compiled in today's grammar.
    fn compile(p: &str) -> Result<Arc<Pattern>, PatternError> {
        super::compile(p, G)
    }

    /// Lists which matcher each pattern in `CORVUS_PATTERNS` (corpus<TAB>pattern lines) gets, and what the cache
    /// counts for it.
    #[test]
    #[ignore]
    fn classify_corpus_patterns() {
        let Ok(path) = std::env::var("CORVUS_PATTERNS") else { return };
        for line in std::fs::read_to_string(path).unwrap().lines() {
            let (corpus, p) = line.split_once('\t').unwrap();
            let kind = compile(p).map(|c| super::kind(&c)).unwrap_or("invalid");
            let retained = compile(p).map_or(0, |c| c.retained);
            println!("{kind}\t{corpus}\t{p}\t{retained}");
        }
    }
}
