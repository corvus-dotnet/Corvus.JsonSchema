//! A high-performance JSON Schema evaluator (draft 4, 6, 7, 2019-09 and 2020-12), ported from the Corvus.Text.Json V5
//! runtime evaluator (`Corvus.Text.Json.RuntimeEvaluator`).
//!
//! A schema is compiled once into a node graph with pre-digested keyword data, then any number of
//! `serde_json::Value` instances are evaluated against it: fast (fail-fast, no reporting) with
//! [`Validator::is_valid`], or exhaustively with a [`JsonSchemaResultsCollector`] for Basic, Detailed or Verbose
//! results and annotations, with the same rows (paths, messages, order) as the C# collector.
//!
//! ```
//! use serde_json::json;
//!
//! let validator = corvus_json_schema::compile(&json!({
//!     "type": "object",
//!     "properties": { "id": { "type": "integer", "minimum": 1 } },
//!     "required": ["id"]
//! }))
//! .unwrap();
//! assert!(validator.is_valid(&json!({ "id": 3 })));
//! assert!(!validator.is_valid(&json!({ "id": 0 })));
//! ```
//!
//! # Regular expressions and their limits
//!
//! `pattern`, `patternProperties` and the `regex` format follow ECMA-262. Simple patterns are matched by matchers of
//! the crate's own and by the `regex` crate, and everything else (lookarounds, backreferences, modifier groups) by
//! the crate's own ECMA-262 engine, as each pattern is written.
//!
//! A schema and an instance may both come from outside, so a pattern is bounded in what it costs. One match against
//! one string may take [`CompileOptions::pattern_match_limit`], one second unless the options say otherwise. A match
//! that reaches it is abandoned and the validation has no answer: [`Validator::try_validate`] and the other `try_`
//! functions return [`SchemaEvaluationError::PatternMatchLimit`]. A pattern longer than 1,048,576 bytes, nested more
//! than 1,000 groups deep or of more than about 16,000 terms does not compile, and
//! [`SchemaCompilationError::kind`] is then [`SchemaCompilationErrorKind::PatternTooLarge`], which is not the kind
//! of a pattern that is not valid.
//!
//! ```
//! use std::time::Duration;
//!
//! use corvus_json_schema::{CompileOptions, SchemaCompilationErrorKind, SchemaEvaluationError, compile, compile_with};
//! use serde_json::json;
//!
//! // A group read back inside a repetition: on this string the match would run for longer than anyone waits.
//! let options = CompileOptions { pattern_match_limit: Some(Duration::from_millis(50)), ..CompileOptions::default() };
//! let validator = compile_with(&json!({ "pattern": r"^(?:(a+)\1*)+$" }), &options).unwrap();
//! let instance = json!("a".repeat(60) + "b");
//! match validator.try_validate(&instance) {
//!     Err(SchemaEvaluationError::PatternMatchLimit(e)) => {
//!         assert_eq!(e.pattern(), r"^(?:(a+)\1*)+$");
//!         assert_eq!(e.limit(), Duration::from_millis(50));
//!     }
//!     other => panic!("{other:?}"),
//! }
//! // is_valid has no way to say so, and answers false.
//! assert!(!validator.is_valid(&instance));
//!
//! // A pattern over a limit of size is not a pattern that is not valid.
//! let error = compile(&json!({ "pattern": "a".repeat(20000) })).unwrap_err();
//! assert!(matches!(error.kind(), SchemaCompilationErrorKind::PatternTooLarge(large) if large.limit() == "size"));
//! let error = compile(&json!({ "pattern": "a**" })).unwrap_err();
//! assert_eq!(error.kind(), &SchemaCompilationErrorKind::InvalidPattern);
//! ```

mod compiler;
mod dialect;
mod document;
mod ecmaregex;
mod eval;
mod formats;
mod instance;
mod loader;
#[rustfmt::skip]
mod metaschemas;
mod node;
mod numbers;
mod options;
mod pattern;
mod results;
mod uri;

use std::sync::Arc;

use serde_json::Value;

pub use dialect::Dialect;
pub use document::{JsonDocument, JsonDocumentArray, JsonDocumentObject, JsonDocumentValue, JsonParseError};
pub use instance::{ArrayView, Instance, Kind, ObjectView, View};
pub use options::{
    CompileOptions, DEFAULT_PATTERN_MATCH_LIMIT, DocumentResolver, FormatValidator, PatternMatchLimitError,
    PatternTooLargeError, SchemaCompilationError, SchemaCompilationErrorKind, SchemaEvaluationDepthError,
    SchemaEvaluationError,
};
pub use results::{
    Annotation, JsonSchemaResultsCollector, ResultsLevel, SchemaResult, collect_annotations, encode_pointer_segment,
    enumerate_annotations, schema_location_fragment,
};

/// The version of this crate.
pub const VERSION: &str = env!("CARGO_PKG_VERSION");

/// Why JSON text could not be validated, as [`Validator::validate_json`] and [`Validator::evaluate_json`] report it.
/// [`Validator::try_validate_json`] and [`Validator::try_evaluate_json`] report a [`SchemaEvaluationError`], which
/// also tells a match of a regular expression that reached its limit from the maximum depth.
#[derive(Clone, Debug, PartialEq, Eq)]
pub enum JsonValidationError {
    /// The text is not valid JSON.
    InvalidJson(JsonParseError),
    /// The validation was abandoned at a limit: it recursed in place beyond the maximum depth, or a match of a
    /// regular expression reached its limit. The name is from when the depth was the one limit, and the message
    /// names both (see [`without_the_limit`] for why the two are one here).
    DepthExceeded(SchemaEvaluationDepthError),
}

impl std::fmt::Display for JsonValidationError {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        match self {
            JsonValidationError::InvalidJson(e) => write!(f, "invalid JSON: {e}"),
            JsonValidationError::DepthExceeded(e) => e.fmt(f),
        }
    }
}

impl std::error::Error for JsonValidationError {
    fn source(&self) -> Option<&(dyn std::error::Error + 'static)> {
        match self {
            JsonValidationError::InvalidJson(e) => Some(e),
            JsonValidationError::DepthExceeded(e) => Some(e),
        }
    }
}

/// What the functions whose error type was fixed before the limit of a match existed report for an evaluation that
/// was abandoned. Their type, [`SchemaEvaluationDepthError`], has one value, so a match that reached its limit is
/// reported as that value too: an error, and so not an answer, though not one that says which limit it was. The
/// `try_` functions say which. This is the one place that decides it.
fn without_the_limit(e: SchemaEvaluationError) -> SchemaEvaluationDepthError {
    match e {
        SchemaEvaluationError::DepthExceeded(e) => e,
        _ => SchemaEvaluationDepthError,
    }
}

fn json_error(e: SchemaEvaluationError) -> JsonValidationError {
    match e {
        SchemaEvaluationError::InvalidJson(e) => JsonValidationError::InvalidJson(e),
        other => JsonValidationError::DepthExceeded(without_the_limit(other)),
    }
}

/// A compiled schema. Cheap to clone and safe to share between threads.
#[derive(Clone)]
pub struct Validator {
    program: Arc<eval::Program>,
}

impl std::fmt::Debug for Validator {
    fn fmt(&self, f: &mut std::fmt::Formatter<'_>) -> std::fmt::Result {
        f.debug_struct("Validator").field("nodes", &self.program.nodes.len()).finish()
    }
}

/// Compiles a schema with the default options (2020-12 for documents without `$schema`, `format` as an annotation
/// unless the vocabularies assert it).
pub fn compile(schema: &Value) -> Result<Validator, SchemaCompilationError> {
    compile_with(schema, &CompileOptions::default())
}

/// Compiles a schema.
pub fn compile_with(schema: &Value, options: &CompileOptions) -> Result<Validator, SchemaCompilationError> {
    let compiled = compiler::SchemaCompiler::compile(schema.clone(), options)?;
    Validator::from_compiled(compiled, options)
}

/// Compiles the schema document at `uri`, fetched through `options.resolve_document` (or one of the standard
/// metaschemas).
pub fn compile_from_uri(uri: &str, options: &CompileOptions) -> Result<Validator, SchemaCompilationError> {
    let compiled = compiler::SchemaCompiler::compile_from_uri(uri, options)?;
    Validator::from_compiled(compiled, options)
}

impl Validator {
    fn from_compiled(
        c: compiler::CompiledSchema,
        options: &CompileOptions,
    ) -> Result<Validator, SchemaCompilationError> {
        let program = eval::Program::new(
            c.nodes,
            c.root,
            c.uses_dynamic_scope,
            options.max_depth,
            options.formats.clone(),
            c.documents,
            c.annotation_sources,
            options.assert_format.is_some(),
            pattern::PatternLimit::new(options.pattern_match_limit),
        );
        // Compiling matches each pattern of patternProperties against the property names the schema declares.
        let failure = program.compile_budget.lock().unwrap_or_else(|e| e.into_inner()).take_failure();
        if let Some(e) = failure {
            return Err(SchemaCompilationError::of_kind(
                e.to_string(),
                SchemaCompilationErrorKind::PatternMatchLimit(e),
            ));
        }
        Ok(Validator { program: Arc::new(program) })
    }

    /// Whether the instance is valid. It has no way to report an error, so it fails closed: it reports false both
    /// for a schema that recursed in place beyond the maximum depth and for a match of a regular expression that
    /// reached its limit (see [`CompileOptions::pattern_match_limit`]). A limit is never reported as valid. In
    /// neither case is the instance known to be not valid. Use [`Validator::try_validate`] to tell them from an
    /// instance that is not valid.
    #[inline]
    pub fn is_valid(&self, instance: &Value) -> bool {
        eval::Evaluator::new(&self.program, None).validate(instance)
    }

    /// Whether the instance is valid, or why the evaluation gave no answer: it recursed in place beyond the maximum
    /// depth, or a match of a regular expression reached its limit.
    pub fn try_validate(&self, instance: &Value) -> Result<bool, SchemaEvaluationError> {
        self.try_validate_instance(instance)
    }

    /// [`Validator::try_validate`] for any [`Instance`]: a value read in place, such as a host language's own
    /// objects, with no conversion to `serde_json::Value`.
    pub fn try_validate_instance<'a, I: Instance<'a>>(&self, instance: I) -> Result<bool, SchemaEvaluationError> {
        let mut e = eval::Evaluator::new(&self.program, None);
        let ok = e.validate(instance);
        e.outcome(ok)
    }

    /// Whether the JSON text is valid, or why there is no answer. The text is parsed into the thread's reused
    /// buffers and evaluated in place: in the steady state nothing is allocated. Use [`JsonDocument`] to validate
    /// the same text more than once.
    pub fn try_validate_json(&self, json: &str) -> Result<bool, SchemaEvaluationError> {
        JsonDocument::with_document(json, |d| self.try_validate_instance(d.root()))
            .map_err(SchemaEvaluationError::InvalidJson)?
    }

    /// Evaluates the instance exhaustively, reporting to the collector. Returns whether it is valid, or why the
    /// evaluation gave no answer. With an error the collector's results are not complete.
    pub fn try_evaluate(
        &self,
        instance: &Value,
        collector: &mut JsonSchemaResultsCollector,
    ) -> Result<bool, SchemaEvaluationError> {
        self.try_evaluate_instance(instance, collector)
    }

    /// [`Validator::try_evaluate`] for any [`Instance`].
    pub fn try_evaluate_instance<'a, I: Instance<'a>>(
        &self,
        instance: I,
        collector: &mut JsonSchemaResultsCollector,
    ) -> Result<bool, SchemaEvaluationError> {
        let mut e = eval::Evaluator::new(&self.program, Some(collector));
        let ok = e.evaluate(instance);
        e.outcome(ok)
    }

    /// [`Validator::try_evaluate`] for JSON text, parsed as [`Validator::try_validate_json`] parses it.
    pub fn try_evaluate_json(
        &self,
        json: &str,
        collector: &mut JsonSchemaResultsCollector,
    ) -> Result<bool, SchemaEvaluationError> {
        JsonDocument::with_document(json, |d| self.try_evaluate_instance(d.root(), collector))
            .map_err(SchemaEvaluationError::InvalidJson)?
    }

    /// Whether the instance is valid, or an error if the validation was abandoned at a limit. The error type has
    /// one value, whose message names both limits, so it does not say whether the evaluation recursed in place
    /// beyond the maximum depth or a match of a regular expression reached its limit: [`Validator::try_validate`]
    /// does.
    pub fn validate(&self, instance: &Value) -> Result<bool, SchemaEvaluationDepthError> {
        self.try_validate(instance).map_err(without_the_limit)
    }

    /// Evaluates the instance exhaustively, reporting to the collector. Returns whether it is valid. The error is
    /// that of [`Validator::validate`], for a validation abandoned at either limit: [`Validator::try_evaluate`]
    /// says which.
    pub fn evaluate(
        &self,
        instance: &Value,
        collector: &mut JsonSchemaResultsCollector,
    ) -> Result<bool, SchemaEvaluationDepthError> {
        self.try_evaluate(instance, collector).map_err(without_the_limit)
    }

    /// [`Validator::validate`] for any [`Instance`]: a value read in place, such as a host language's own objects,
    /// with no conversion to `serde_json::Value`. The error is that of [`Validator::validate`], for a validation
    /// abandoned at either limit: [`Validator::try_validate_instance`] says which.
    pub fn validate_instance<'a, I: Instance<'a>>(&self, instance: I) -> Result<bool, SchemaEvaluationDepthError> {
        self.try_validate_instance(instance).map_err(without_the_limit)
    }

    /// Whether the JSON text is valid. The text is parsed into the thread's reused buffers and evaluated in place: in
    /// the steady state nothing is allocated. Use [`JsonDocument`] to validate the same text more than once. An
    /// evaluation that was abandoned is reported as [`JsonValidationError::DepthExceeded`], whichever limit it
    /// reached: [`Validator::try_validate_json`] says which.
    pub fn validate_json(&self, json: &str) -> Result<bool, JsonValidationError> {
        self.try_validate_json(json).map_err(json_error)
    }

    /// [`Validator::evaluate`] for JSON text, parsed as [`Validator::validate_json`] parses it. An evaluation that
    /// was abandoned is [`JsonValidationError::DepthExceeded`], whichever limit it reached:
    /// [`Validator::try_evaluate_json`] says which.
    pub fn evaluate_json(
        &self,
        json: &str,
        collector: &mut JsonSchemaResultsCollector,
    ) -> Result<bool, JsonValidationError> {
        self.try_evaluate_json(json, collector).map_err(json_error)
    }

    /// [`Validator::evaluate`] for any [`Instance`]. The error is that of [`Validator::validate`], for a validation
    /// abandoned at either limit: [`Validator::try_evaluate_instance`] says which.
    pub fn evaluate_instance<'a, I: Instance<'a>>(
        &self,
        instance: I,
        collector: &mut JsonSchemaResultsCollector,
    ) -> Result<bool, SchemaEvaluationDepthError> {
        self.try_evaluate_instance(instance, collector).map_err(without_the_limit)
    }
}
