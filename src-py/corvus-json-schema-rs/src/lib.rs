//! Python bindings for the `corvus-json-schema` crate.
//!
//! A schema is compiled once by the Rust evaluator. Each instance is then converted from Python objects to a
//! `serde_json::Value` (or parsed straight from JSON text) and evaluated in Rust. The Python layer
//! (`python/corvus_json_schema_rs/__init__.py`) gives the same API as the pure-Python `corvus_json_schema` package.

use std::collections::HashMap;
use std::sync::Arc;

use corvus_json_schema::{
    CompileOptions, Dialect, DocumentResolver, FormatValidator, JsonSchemaResultsCollector, ResultsLevel, SchemaResult,
    Validator,
};
use pyo3::create_exception;
use pyo3::exceptions::{PyException, PyTypeError, PyValueError};
use pyo3::prelude::*;
use pyo3::types::{PyBool, PyBytes, PyDict, PyFloat, PyInt, PyList, PyString, PyTuple};
use serde_json::{Map, Number, Value};

create_exception!(
    corvus_json_schema_rs,
    SchemaCompilationError,
    PyException,
    "Raised when a schema cannot be compiled (an unresolvable reference, an invalid pattern)."
);
create_exception!(
    corvus_json_schema_rs,
    SchemaEvaluationDepthError,
    PyException,
    "Raised when evaluation recurses in place beyond max_depth (a schema that loops without consuming the instance)."
);

/// The deepest instance nesting converted to a JSON value (deeper instances are rejected rather than overflowing the
/// stack).
const MAX_NESTING: u32 = 1024;

// ---------------------------------------------------------------------------------------------------------------------
// Python objects to JSON values

fn to_value(obj: &Bound<'_, PyAny>, depth: u32) -> PyResult<Value> {
    // Exact types first: these are what json.loads produces. Subclasses (bool is an int; str, dict and list
    // subclasses) follow.
    if let Ok(s) = obj.cast_exact::<PyString>() {
        return Ok(Value::String(string(s)?));
    }
    if let Ok(d) = obj.cast_exact::<PyDict>() {
        return object(d, depth);
    }
    if let Ok(l) = obj.cast_exact::<PyList>() {
        if depth >= MAX_NESTING {
            return Err(PyValueError::new_err("The instance is nested too deeply."));
        }
        let mut items = Vec::with_capacity(l.len());
        for item in l.iter() {
            items.push(to_value(&item, depth + 1)?);
        }
        return Ok(Value::Array(items));
    }
    if obj.is_none() {
        return Ok(Value::Null);
    }
    if let Ok(b) = obj.cast_exact::<PyBool>() {
        return Ok(Value::Bool(b.is_true()));
    }
    if let Ok(i) = obj.cast_exact::<PyInt>() {
        return integer(i);
    }
    if let Ok(f) = obj.cast_exact::<PyFloat>() {
        return float(f.value());
    }
    if let Ok(b) = obj.cast::<PyBool>() {
        return Ok(Value::Bool(b.is_true()));
    }
    if let Ok(i) = obj.cast::<PyInt>() {
        return integer(i);
    }
    if let Ok(f) = obj.cast::<PyFloat>() {
        return float(f.value());
    }
    if let Ok(s) = obj.cast::<PyString>() {
        return Ok(Value::String(string(s)?));
    }
    if let Ok(d) = obj.cast::<PyDict>() {
        return object(d, depth);
    }
    if obj.cast::<PyList>().is_ok() || obj.cast::<PyTuple>().is_ok() {
        if depth >= MAX_NESTING {
            return Err(PyValueError::new_err("The instance is nested too deeply."));
        }
        let mut items = Vec::new();
        for item in obj.try_iter()? {
            items.push(to_value(&item?, depth + 1)?);
        }
        return Ok(Value::Array(items));
    }
    Err(PyTypeError::new_err(format!(
        "Values of type '{}' are not JSON.",
        obj.get_type().name().map(|n| n.to_string()).unwrap_or_default()
    )))
}

fn string(s: &Bound<'_, PyString>) -> PyResult<String> {
    match s.to_str() {
        Ok(text) => Ok(text.to_owned()),
        // Lone surrogates (which json.loads can produce) have no UTF-8 form.
        Err(_) => Ok(s.to_string_lossy().into_owned()),
    }
}

fn object(d: &Bound<'_, PyDict>, depth: u32) -> PyResult<Value> {
    if depth >= MAX_NESTING {
        return Err(PyValueError::new_err("The instance is nested too deeply."));
    }
    let mut map = Map::with_capacity(d.len());
    for (k, v) in d.iter() {
        let key = match k.cast::<PyString>() {
            Ok(s) => string(s)?,
            Err(_) => return Err(PyTypeError::new_err("JSON object keys must be strings.")),
        };
        map.insert(key, to_value(&v, depth + 1)?);
    }
    Ok(Value::Object(map))
}

fn integer(i: &Bound<'_, PyInt>) -> PyResult<Value> {
    if let Ok(v) = i.extract::<i64>() {
        return Ok(Value::Number(v.into()));
    }
    if let Ok(v) = i.extract::<u64>() {
        return Ok(Value::Number(v.into()));
    }
    // Beyond 64 bits, the nearest double (as for a JSON parser without arbitrary precision).
    float(i.extract::<f64>()?)
}

fn float(f: f64) -> PyResult<Value> {
    Number::from_f64(f)
        .map(Value::Number)
        .ok_or_else(|| PyValueError::new_err("NaN and infinities are not JSON numbers."))
}

// JSON values to Python objects (resolved documents arrive as Python objects; annotations go back as them).

fn to_python<'py>(py: Python<'py>, value: &Value) -> PyResult<Bound<'py, PyAny>> {
    Ok(match value {
        Value::Null => py.None().into_bound(py),
        Value::Bool(b) => PyBool::new(py, *b).to_owned().into_any(),
        Value::Number(n) => {
            if let Some(i) = n.as_i64() {
                i.into_pyobject(py)?.into_any()
            } else if let Some(u) = n.as_u64() {
                u.into_pyobject(py)?.into_any()
            } else {
                n.as_f64().unwrap_or(f64::NAN).into_pyobject(py)?.into_any()
            }
        }
        Value::String(s) => PyString::new(py, s).into_any(),
        Value::Array(items) => {
            let list = PyList::empty(py);
            for item in items {
                list.append(to_python(py, item)?)?;
            }
            list.into_any()
        }
        Value::Object(map) => {
            let dict = PyDict::new(py);
            for (k, v) in map {
                dict.set_item(k, to_python(py, v)?)?;
            }
            dict.into_any()
        }
    })
}

// ---------------------------------------------------------------------------------------------------------------------
// Validators

fn dialect(value: u8) -> PyResult<Dialect> {
    Ok(match value {
        0 => Dialect::Draft4,
        1 => Dialect::Draft6,
        2 => Dialect::Draft7,
        3 => Dialect::Draft201909,
        4 => Dialect::Draft202012,
        _ => return Err(PyValueError::new_err(format!("Unknown dialect {value}."))),
    })
}

fn level(value: u8) -> PyResult<ResultsLevel> {
    Ok(match value {
        0 => ResultsLevel::Basic,
        1 => ResultsLevel::Detailed,
        2 => ResultsLevel::Verbose,
        _ => return Err(PyValueError::new_err(format!("Unknown results level {value}."))),
    })
}

/// A compiled schema. Call it (or `is_valid`) with an instance; `is_valid_json` takes JSON text instead.
#[pyclass(frozen, module = "corvus_json_schema_rs", name = "Validator")]
struct PyValidator {
    inner: Validator,
}

impl PyValidator {
    fn check(&self, value: &Value) -> PyResult<bool> {
        self.inner.validate(value).map_err(|e| SchemaEvaluationDepthError::new_err(e.to_string()))
    }
}

#[pymethods]
impl PyValidator {
    /// Whether the instance (a value as `json.loads` produces it) is valid against the schema.
    fn __call__(&self, instance: &Bound<'_, PyAny>) -> PyResult<bool> {
        self.check(&to_value(instance, 0)?)
    }

    /// Whether the instance (a value as `json.loads` produces it) is valid against the schema.
    fn is_valid(&self, instance: &Bound<'_, PyAny>) -> PyResult<bool> {
        self.check(&to_value(instance, 0)?)
    }

    /// Whether the JSON text (`str` or `bytes`) is valid against the schema. The text is parsed in Rust, so no Python
    /// objects are created for the instance.
    fn is_valid_json(&self, py: Python<'_>, text: &Bound<'_, PyAny>) -> PyResult<bool> {
        let parsed: Result<Value, serde_json::Error> = if let Ok(s) = text.cast::<PyString>() {
            let s = s.to_str()?;
            py.detach(|| serde_json::from_str(s))
        } else if let Ok(b) = text.cast::<PyBytes>() {
            let b = b.as_bytes();
            py.detach(|| serde_json::from_slice(b))
        } else {
            return Err(PyTypeError::new_err("Expected JSON text as str or bytes."));
        };
        let value = parsed.map_err(|e| PyValueError::new_err(format!("Invalid JSON: {e}")))?;
        self.check(&value)
    }

    /// Evaluates the instance, reporting results to the collector when one is given (every keyword is evaluated and
    /// reported, at the collector's level); without a collector this is the validator itself.
    #[pyo3(signature = (instance, collector = None))]
    fn evaluate(&self, instance: &Bound<'_, PyAny>, collector: Option<PyRefMut<'_, PyCollector>>) -> PyResult<bool> {
        let value = to_value(instance, 0)?;
        match collector {
            None => self.check(&value),
            Some(mut c) => self
                .inner
                .evaluate(&value, &mut c.inner)
                .map_err(|e| SchemaEvaluationDepthError::new_err(e.to_string())),
        }
    }

    fn __repr__(&self) -> String {
        format!("{:?}", self.inner)
    }
}

/// Compiles a schema (a parsed JSON value) with the given options.
#[pyfunction]
#[pyo3(signature = (
    schema,
    *,
    default_dialect = 4,
    assert_format = None,
    assert_format_in_legacy_drafts = false,
    assert_content = true,
    formats = None,
    resolve_document = None,
    base_uri = None,
    entry_point = None,
    max_depth = 128,
))]
#[allow(clippy::too_many_arguments)]
fn compile(
    schema: &Bound<'_, PyAny>,
    default_dialect: u8,
    assert_format: Option<bool>,
    assert_format_in_legacy_drafts: bool,
    assert_content: bool,
    formats: Option<HashMap<String, Py<PyAny>>>,
    resolve_document: Option<Py<PyAny>>,
    base_uri: Option<String>,
    entry_point: Option<String>,
    max_depth: u32,
) -> PyResult<PyValidator> {
    let value = to_value(schema, 0)?;
    let mut options = CompileOptions {
        default_dialect: dialect(default_dialect)?,
        assert_format,
        assert_format_in_legacy_drafts,
        assert_content,
        base_uri,
        entry_point,
        max_depth,
        ..CompileOptions::default()
    };
    for (name, f) in formats.unwrap_or_default() {
        let f = Arc::new(f);
        let validator: FormatValidator = Arc::new(move |s: &str| {
            Python::attach(|py| f.bind(py).call1((s,)).and_then(|r| r.is_truthy()).unwrap_or(false))
        });
        options.formats.insert(name, validator);
    }
    if let Some(resolve) = resolve_document {
        let resolve = Arc::new(resolve);
        let resolver: DocumentResolver = Arc::new(move |uri: &str| {
            Python::attach(|py| {
                let result = resolve.bind(py).call1((uri,)).ok()?;
                if result.is_none() {
                    return None;
                }
                if let Ok(text) = result.cast::<PyString>() {
                    return serde_json::from_str(text.to_str().ok()?).ok();
                }
                if let Ok(bytes) = result.cast::<PyBytes>() {
                    return serde_json::from_slice(bytes.as_bytes()).ok();
                }
                to_value(&result, 0).ok()
            })
        });
        options.resolve_document = Some(resolver);
    }
    corvus_json_schema::compile_with(&value, &options)
        .map(|inner| PyValidator { inner })
        .map_err(|e| SchemaCompilationError::new_err(e.message().to_string()))
}

// ---------------------------------------------------------------------------------------------------------------------
// Results

/// One result row.
#[pyclass(frozen, eq, module = "corvus_json_schema_rs", name = "SchemaResult")]
#[derive(PartialEq)]
struct PySchemaResult {
    #[pyo3(get)]
    is_match: bool,
    /// The message, or '' when the level records none or the keyword has none. Annotation rows carry raw JSON.
    #[pyo3(get)]
    message: String,
    /// The path of keywords from the root schema (e.g. `/properties/name/type`).
    #[pyo3(get)]
    evaluation_location: String,
    /// The JSON pointer of the evaluated schema (or keyword) within its document.
    #[pyo3(get)]
    schema_evaluation_location: String,
    /// The JSON pointer of the instance location (e.g. `/name`).
    #[pyo3(get)]
    document_evaluation_location: String,
}

#[pymethods]
impl PySchemaResult {
    fn __repr__(&self) -> String {
        format!(
            "SchemaResult(is_match={}, message={:?}, evaluation_location={:?}, schema_evaluation_location={:?}, \
             document_evaluation_location={:?})",
            if self.is_match { "True" } else { "False" },
            self.message,
            self.evaluation_location,
            self.schema_evaluation_location,
            self.document_evaluation_location
        )
    }
}

impl From<&SchemaResult> for PySchemaResult {
    fn from(r: &SchemaResult) -> Self {
        PySchemaResult {
            is_match: r.is_match,
            message: r.message.clone(),
            evaluation_location: r.evaluation_location.clone(),
            schema_evaluation_location: r.schema_evaluation_location.clone(),
            document_evaluation_location: r.document_evaluation_location.clone(),
        }
    }
}

/// Collects the results of an evaluation.
#[pyclass(subclass, module = "corvus_json_schema_rs", name = "JsonSchemaResultsCollector")]
struct PyCollector {
    inner: JsonSchemaResultsCollector,
    level: u8,
}

#[pymethods]
impl PyCollector {
    #[new]
    fn new(level: u8) -> PyResult<Self> {
        Ok(PyCollector { inner: JsonSchemaResultsCollector::new(self::level(level)?), level })
    }

    /// Creates a collector at the given level.
    #[staticmethod]
    fn create(level: u8) -> PyResult<Self> {
        Self::new(level)
    }

    /// The level, as an int (see ResultsLevel).
    #[getter]
    fn level_value(&self) -> u8 {
        self.level
    }

    /// The results, in commit order.
    #[getter]
    fn results(&self) -> Vec<PySchemaResult> {
        self.inner.results().iter().map(PySchemaResult::from).collect()
    }

    #[getter]
    fn result_count(&self) -> usize {
        self.inner.results().len()
    }

    /// Annotations grouped by instance location, then keyword, then schema location fragment, with parsed values.
    fn collect_annotations<'py>(&self, py: Python<'py>) -> PyResult<Bound<'py, PyDict>> {
        let out = PyDict::new(py);
        for (instance, by_keyword) in corvus_json_schema::collect_annotations(&self.inner) {
            let keywords = PyDict::new(py);
            for (keyword, by_schema) in by_keyword {
                let schemas = PyDict::new(py);
                for (schema, value) in by_schema {
                    schemas.set_item(schema, to_python(py, &value)?)?;
                }
                keywords.set_item(keyword, schemas)?;
            }
            out.set_item(instance, keywords)?;
        }
        Ok(out)
    }
}

/// `#` followed by the schema location, percent-encoded as a URI fragment (upper-case hex, UTF-8).
#[pyfunction]
fn schema_location_fragment(schema_location: &str) -> String {
    corvus_json_schema::schema_location_fragment(schema_location)
}

#[pymodule]
fn _native(m: &Bound<'_, PyModule>) -> PyResult<()> {
    m.add("SchemaCompilationError", m.py().get_type::<SchemaCompilationError>())?;
    m.add("SchemaEvaluationDepthError", m.py().get_type::<SchemaEvaluationDepthError>())?;
    m.add("RUST_CRATE_VERSION", corvus_json_schema::VERSION)?;
    m.add_class::<PyValidator>()?;
    m.add_class::<PySchemaResult>()?;
    m.add_class::<PyCollector>()?;
    m.add_function(wrap_pyfunction!(compile, m)?)?;
    m.add_function(wrap_pyfunction!(schema_location_fragment, m)?)?;
    Ok(())
}
