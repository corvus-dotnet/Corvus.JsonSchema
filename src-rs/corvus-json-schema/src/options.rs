//! Compile options and errors (the counterparts of `JsonSchemaEvaluatorOptions` and the evaluator exceptions).

use std::collections::HashMap;
use std::fmt;
use std::sync::Arc;
use std::time::Duration;

use serde_json::Value;

use crate::dialect::Dialect;

/// Resolves a schema document by absolute URI; returns `None` if it is unknown.
pub type DocumentResolver = Arc<dyn Fn(&str) -> Option<Value> + Send + Sync>;

/// A custom format assertion over the string value (or the number's JSON text, for numbers).
pub type FormatValidator = Arc<dyn Fn(&str) -> bool + Send + Sync>;

/// Options for compiling a schema.
#[derive(Clone)]
pub struct CompileOptions {
    /// The dialect for documents without `$schema`. Defaults to 2020-12.
    pub default_dialect: Dialect,
    /// Whether `format` is asserted. `None` (the default) follows the vocabularies: the 2020-12 `format-assertion`
    /// vocabulary asserts, everything else annotates.
    pub assert_format: Option<bool>,
    /// When `assert_format` is `None`, assert `format` in draft 4 to 7 too.
    pub assert_format_in_legacy_drafts: bool,
    /// Assert `contentEncoding`/`contentMediaType` in draft 7 (the only draft that asserts them). Defaults to true.
    pub assert_content: bool,
    /// Custom format assertions, by format name; they take precedence over the built-in set.
    pub formats: HashMap<String, FormatValidator>,
    /// Resolves remote documents. The standard metaschemas are always available.
    pub resolve_document: Option<DocumentResolver>,
    /// The base URI of the root document.
    pub base_uri: Option<String>,
    /// A reference (relative to the root) to evaluate from, e.g. `#/$defs/item`. Defaults to the root.
    pub entry_point: Option<String>,
    /// Maximum depth of in-place recursion on a cycle before evaluation is abandoned. Defaults to 128.
    pub max_depth: u32,
    /// The time one match of a `pattern` or `patternProperties` regular expression against one string may take.
    /// Defaults to [`DEFAULT_PATTERN_MATCH_LIMIT`], one second. A match that takes longer is abandoned, and the
    /// validation it belongs to reports a [`PatternMatchLimitError`]. `None`, or a limit of zero, turns the limit
    /// off, for a caller who trusts both the schema and the instances. Without the limit a match also has no limit
    /// on its memory.
    ///
    /// The limit is on each match, not on the validation. Only the backtracking matcher can take long, and it
    /// reads the clock every 4,096 steps, so a match is abandoned within tens of microseconds of the limit. The
    /// matchers that take time proportional to the string are not stopped: a string is given to one only when it
    /// can answer within the limit.
    pub pattern_match_limit: Option<Duration>,
}

/// The time one match of a `pattern` or `patternProperties` regular expression against one string may take, unless
/// [`CompileOptions::pattern_match_limit`] says otherwise.
pub const DEFAULT_PATTERN_MATCH_LIMIT: Duration = Duration::from_secs(1);

impl Default for CompileOptions {
    fn default() -> Self {
        CompileOptions {
            default_dialect: Dialect::Draft202012,
            assert_format: None,
            assert_format_in_legacy_drafts: false,
            assert_content: true,
            formats: HashMap::new(),
            resolve_document: None,
            base_uri: None,
            entry_point: None,
            max_depth: 128,
            pattern_match_limit: Some(DEFAULT_PATTERN_MATCH_LIMIT),
        }
    }
}

impl fmt::Debug for CompileOptions {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.debug_struct("CompileOptions")
            .field("default_dialect", &self.default_dialect)
            .field("assert_format", &self.assert_format)
            .field("assert_format_in_legacy_drafts", &self.assert_format_in_legacy_drafts)
            .field("assert_content", &self.assert_content)
            .field("formats", &self.formats.keys().collect::<Vec<_>>())
            .field("resolve_document", &self.resolve_document.is_some())
            .field("base_uri", &self.base_uri)
            .field("entry_point", &self.entry_point)
            .field("max_depth", &self.max_depth)
            .field("pattern_match_limit", &self.pattern_match_limit)
            .finish()
    }
}

/// A schema could not be compiled (an unresolvable reference, an invalid pattern, a pattern over a limit of size).
/// [`SchemaCompilationError::kind`] tells the errors of a regular expression apart.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SchemaCompilationError {
    message: String,
    kind: SchemaCompilationErrorKind,
}

/// What kind of error a [`SchemaCompilationError`] is.
#[derive(Clone, Debug, PartialEq, Eq)]
#[non_exhaustive]
pub enum SchemaCompilationErrorKind {
    /// Any error that is not about a regular expression: an unresolvable reference, for one.
    Other,
    /// A `pattern`, or a key of `patternProperties`, is not a valid ECMA-262 regular expression.
    InvalidPattern,
    /// A regular expression is over a limit of size. It is not the error of one that is not valid: the pattern was
    /// not read to its end, so nothing is known of whether it is valid.
    PatternTooLarge(PatternTooLargeError),
    /// Matching a regular expression against a property name the schema declares, which is done once when the
    /// schema is compiled, reached the limit of a match (see [`CompileOptions::pattern_match_limit`]).
    PatternMatchLimit(PatternMatchLimitError),
}

impl SchemaCompilationError {
    pub(crate) fn new(message: impl Into<String>) -> Self {
        SchemaCompilationError { message: message.into(), kind: SchemaCompilationErrorKind::Other }
    }

    pub(crate) fn of_kind(message: impl Into<String>, kind: SchemaCompilationErrorKind) -> Self {
        SchemaCompilationError { message: message.into(), kind }
    }

    /// The reason compilation failed.
    pub fn message(&self) -> &str {
        &self.message
    }

    /// The kind of error, which tells a regular expression that is too large from one that is not valid.
    pub fn kind(&self) -> &SchemaCompilationErrorKind {
        &self.kind
    }
}

impl fmt::Display for SchemaCompilationError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.message)
    }
}

impl std::error::Error for SchemaCompilationError {}

/// A regular expression of a schema is over a limit of size, so the schema was not compiled.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct PatternTooLargeError {
    pattern: String,
    keyword: &'static str,
    limit: &'static str,
    value: usize,
    reason: String,
}

impl PatternTooLargeError {
    pub(crate) fn new(pattern: &str, keyword: &'static str, limit: &'static str, value: usize, reason: String) -> Self {
        PatternTooLargeError { pattern: pattern.to_string(), keyword, limit, value, reason }
    }

    /// The regular expression.
    pub fn pattern(&self) -> &str {
        &self.pattern
    }

    /// The keyword that holds it, `pattern` or `patternProperties`.
    pub fn keyword(&self) -> &str {
        self.keyword
    }

    /// The name of the limit: `length` (the bytes of the pattern, 1,048,576 at most), `nesting` (the depth of its
    /// groups, 1,000 at most) or `size` (the bytes counted for it as it is read, 1,048,576 at most).
    pub fn limit(&self) -> &str {
        self.limit
    }

    /// The value of that limit.
    pub fn value(&self) -> usize {
        self.value
    }
}

impl fmt::Display for PatternTooLargeError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        write!(
            f,
            "The regular expression {} in {} is too large to read as an ECMA-262 regular expression: {}.",
            crate::pattern::shown_pattern(&self.pattern),
            self.keyword,
            self.reason
        )
    }
}

impl std::error::Error for PatternTooLargeError {}

/// A validation was abandoned because one match of a `pattern` or `patternProperties` regular expression reached
/// its limit. The instance is then neither valid nor not valid.
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct PatternMatchLimitError {
    pattern: String,
    limit: Duration,
    memory: bool,
}

impl PatternMatchLimitError {
    pub(crate) fn new(pattern: &str, limit: Duration, memory: bool) -> Self {
        PatternMatchLimitError { pattern: pattern.to_string(), limit, memory }
    }

    /// The regular expression.
    pub fn pattern(&self) -> &str {
        &self.pattern
    }

    /// The time the match was allowed (see [`CompileOptions::pattern_match_limit`]).
    pub fn limit(&self) -> Duration {
        self.limit
    }

    /// Whether the match was abandoned for the memory of its backtracking, not for its time: it held 1,048,576
    /// choices, which is 24 MB.
    pub fn is_memory(&self) -> bool {
        self.memory
    }
}

impl fmt::Display for PatternMatchLimitError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        let pattern = crate::pattern::shown_pattern(&self.pattern);
        if self.memory {
            write!(
                f,
                "A match of the regular expression {pattern} was abandoned: it needed more than the limit of \
                 1,048,576 backtracking entries."
            )
        } else {
            write!(
                f,
                "A match of the regular expression {pattern} was abandoned: it took longer than the limit of {:?}.",
                self.limit
            )
        }
    }
}

impl std::error::Error for PatternMatchLimitError {}

/// The validation was abandoned at a limit, and gave no answer. It is the error of the functions whose error type
/// was there before the limit of a match was (`validate`, `validate_instance`, `evaluate`, `evaluate_instance`, and
/// inside [`crate::JsonValidationError::DepthExceeded`] `validate_json` and `evaluate_json`).
///
/// The name is from when there was one limit: an evaluation that recursed in place beyond `max_depth` (a schema
/// that loops without consuming the instance). A match of a regular expression that reached its limit (see
/// [`CompileOptions::pattern_match_limit`]) is reported as this error too. The type has one value, so it does not
/// say which limit it was, and its message names both. The `try_` functions (`Validator::try_validate` and the
/// rest) return a [`SchemaEvaluationError`], which says which.
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SchemaEvaluationDepthError;

impl fmt::Display for SchemaEvaluationDepthError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(
            "The validation was abandoned at a limit: the schema recursed in place beyond the maximum depth, or a \
             match of a regular expression reached its limit. The try_ functions say which.",
        )
    }
}

impl std::error::Error for SchemaEvaluationDepthError {}

/// Why an evaluation gave no answer. The instance is then neither valid nor not valid.
#[derive(Clone, Debug, PartialEq, Eq)]
#[non_exhaustive]
pub enum SchemaEvaluationError {
    /// The text is not valid JSON (the functions that take JSON text).
    InvalidJson(crate::JsonParseError),
    /// Evaluation recursed in place beyond the maximum depth.
    DepthExceeded(SchemaEvaluationDepthError),
    /// A match of a regular expression reached its limit.
    PatternMatchLimit(PatternMatchLimitError),
}

impl fmt::Display for SchemaEvaluationError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SchemaEvaluationError::InvalidJson(e) => write!(f, "invalid JSON: {e}"),
            // Not the message of the value inside, which is the one error of the functions that cannot say which
            // limit it was, and so names both.
            SchemaEvaluationError::DepthExceeded(_) => {
                f.write_str("The schema recursed in place beyond the maximum depth.")
            }
            SchemaEvaluationError::PatternMatchLimit(e) => e.fmt(f),
        }
    }
}

impl std::error::Error for SchemaEvaluationError {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        match self {
            SchemaEvaluationError::InvalidJson(e) => Some(e),
            SchemaEvaluationError::DepthExceeded(e) => Some(e),
            SchemaEvaluationError::PatternMatchLimit(e) => Some(e),
        }
    }
}
