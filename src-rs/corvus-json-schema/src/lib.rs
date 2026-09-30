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

mod compiler;
mod dialect;
mod eval;
mod formats;
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
pub use options::{
    CompileOptions, DocumentResolver, FormatValidator, SchemaCompilationError, SchemaEvaluationDepthError,
};
pub use results::{
    Annotation, JsonSchemaResultsCollector, ResultsLevel, SchemaResult, collect_annotations, encode_pointer_segment,
    enumerate_annotations, schema_location_fragment,
};

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
    Ok(Validator::from_compiled(compiled, options))
}

/// Compiles the schema document at `uri`, fetched through `options.resolve_document` (or one of the standard
/// metaschemas).
pub fn compile_from_uri(uri: &str, options: &CompileOptions) -> Result<Validator, SchemaCompilationError> {
    let compiled = compiler::SchemaCompiler::compile_from_uri(uri, options)?;
    Ok(Validator::from_compiled(compiled, options))
}

impl Validator {
    fn from_compiled(c: compiler::CompiledSchema, options: &CompileOptions) -> Validator {
        let program = eval::Program::new(
            c.nodes,
            c.root,
            c.uses_dynamic_scope,
            options.max_depth,
            options.formats.clone(),
            c.documents,
            c.annotation_sources,
            options.assert_format.is_some(),
        );
        Validator { program: Arc::new(program) }
    }

    /// Whether the instance is valid. A schema that recursed in place beyond the maximum depth is reported as
    /// invalid; use [`Validator::validate`] to tell the two apart.
    #[inline]
    pub fn is_valid(&self, instance: &Value) -> bool {
        eval::Evaluator::new(&self.program, None).validate(instance)
    }

    /// Whether the instance is valid, or an error if evaluation recursed in place beyond the maximum depth.
    pub fn validate(&self, instance: &Value) -> Result<bool, SchemaEvaluationDepthError> {
        let mut e = eval::Evaluator::new(&self.program, None);
        let ok = e.validate(instance);
        if e.depth_exceeded { Err(SchemaEvaluationDepthError) } else { Ok(ok) }
    }

    /// Evaluates the instance exhaustively, reporting to the collector. Returns whether it is valid.
    pub fn evaluate(
        &self,
        instance: &Value,
        collector: &mut JsonSchemaResultsCollector,
    ) -> Result<bool, SchemaEvaluationDepthError> {
        let mut e = eval::Evaluator::new(&self.program, Some(collector));
        let ok = e.evaluate(instance);
        if e.depth_exceeded { Err(SchemaEvaluationDepthError) } else { Ok(ok) }
    }
}
