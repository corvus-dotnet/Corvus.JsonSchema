//! Compile options and errors (the counterparts of `JsonSchemaEvaluatorOptions` and the evaluator exceptions).

use std::collections::HashMap;
use std::fmt;
use std::sync::Arc;

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
}

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
            .finish()
    }
}

/// A schema could not be compiled (an unresolvable reference, an invalid pattern).
#[derive(Clone, Debug, PartialEq, Eq)]
pub struct SchemaCompilationError {
    message: String,
}

impl SchemaCompilationError {
    pub(crate) fn new(message: impl Into<String>) -> Self {
        SchemaCompilationError { message: message.into() }
    }

    /// The reason compilation failed.
    pub fn message(&self) -> &str {
        &self.message
    }
}

impl fmt::Display for SchemaCompilationError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(&self.message)
    }
}

impl std::error::Error for SchemaCompilationError {}

/// Evaluation recursed in place beyond `max_depth` (a schema that loops without consuming the instance).
#[derive(Clone, Copy, Debug, PartialEq, Eq)]
pub struct SchemaEvaluationDepthError;

impl fmt::Display for SchemaEvaluationDepthError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str("The schema recursed in place beyond the maximum depth.")
    }
}

impl std::error::Error for SchemaEvaluationDepthError {}
