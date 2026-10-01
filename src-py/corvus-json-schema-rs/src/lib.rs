//! Python bindings for the `corvus-json-schema` crate.
//!
//! A schema is compiled once by the Rust evaluator. Each instance is then converted from Python objects to a
//! `serde_json::Value` (or parsed straight from JSON text) and evaluated in Rust. The Python layer
//! (`python/corvus_json_schema_rs/__init__.py`) gives the same API as the pure-Python `corvus_json_schema` package.

use std::cell::Cell;
use std::collections::HashMap;
use std::marker::PhantomData;
use std::ptr::addr_of_mut;
use std::sync::Arc;

use corvus_json_schema::{
    ArrayView, CompileOptions, Dialect, DocumentResolver, FormatValidator, Instance, JsonSchemaResultsCollector,
    ObjectView, ResultsLevel, SchemaResult, Validator, View,
};
use pyo3::create_exception;
use pyo3::exceptions::{PyException, PyTypeError, PyValueError};
use pyo3::ffi;
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
        // Lone surrogates (which json.loads can produce) have no UTF-8 form: each becomes one U+FFFD, so lengths and
        // positions stay those of the Python string.
        Err(_) => {
            let utf16 = s.call_method1("encode", ("utf-16-le", "surrogatepass"))?;
            let bytes = utf16.cast::<PyBytes>()?.as_bytes();
            let units = bytes.chunks_exact(2).map(|c| u16::from_le_bytes([c[0], c[1]]));
            Ok(char::decode_utf16(units).map(|c| c.unwrap_or(char::REPLACEMENT_CHARACTER)).collect())
        }
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

// ---------------------------------------------------------------------------------------------------------------------
// Python objects read in place

/// A Python object read as a JSON value through the C API, without converting it. Strings are read as their UTF-8
/// (which CPython keeps with the string, so ASCII strings need no copy), dicts through `PyDict_Next` and lists by index.
///
/// Anything this cannot read exactly (a tuple, a key that is not a string, a string with a lone surrogate, a number
/// beyond a double, nesting deeper than `MAX_NESTING`) sets `fallback`; the caller then converts the instance and
/// evaluates the converted value instead, so results never depend on which path ran.
#[derive(Clone, Copy)]
struct PyInstance<'a> {
    obj: *mut ffi::PyObject,
    fallback: &'a Cell<bool>,
    depth: u32,
    _life: PhantomData<&'a ()>,
}

impl<'a> PyInstance<'a> {
    fn root(obj: &'a Bound<'_, PyAny>, fallback: &'a Cell<bool>) -> Self {
        PyInstance { obj: obj.as_ptr(), fallback, depth: 0, _life: PhantomData }
    }

    #[inline(always)]
    fn child(self, obj: *mut ffi::PyObject) -> Self {
        PyInstance { obj, fallback: self.fallback, depth: self.depth + 1, _life: PhantomData }
    }

    #[cold]
    fn unsupported(self) -> View<'a, Self> {
        self.fallback.set(true);
        View::Null
    }
}

/// A Python string's UTF-8, or `None` (with the error cleared) when it has none.
#[inline]
unsafe fn utf8<'a>(obj: *mut ffi::PyObject) -> Option<&'a str> {
    let mut size: ffi::Py_ssize_t = 0;
    let data = unsafe { ffi::PyUnicode_AsUTF8AndSize(obj, &mut size) };
    if data.is_null() {
        unsafe { ffi::PyErr_Clear() };
        return None;
    }
    Some(unsafe { std::str::from_utf8_unchecked(std::slice::from_raw_parts(data.cast::<u8>(), size as usize)) })
}

impl<'a> Instance<'a> for PyInstance<'a> {
    type Array = PyListView<'a>;
    type Object = PyDictView<'a>;

    #[inline]
    fn view(self) -> View<'a, Self> {
        let o = self.obj;
        // SAFETY: the GIL is held for the whole evaluation and the instance is not mutated during it (the evaluator
        // runs no Python code but custom format callbacks, which receive copies of strings), so every borrowed object
        // outlives the evaluation.
        unsafe {
            let t = ffi::Py_TYPE(o);
            if t == addr_of_mut!(ffi::PyUnicode_Type) {
                return match utf8(o) {
                    Some(s) => View::String(s),
                    None => self.unsupported(),
                };
            }
            if t == addr_of_mut!(ffi::PyDict_Type) {
                return if self.depth < MAX_NESTING { View::Object(PyDictView(self)) } else { self.unsupported() };
            }
            if t == addr_of_mut!(ffi::PyList_Type) {
                return if self.depth < MAX_NESTING { View::Array(PyListView(self)) } else { self.unsupported() };
            }
            if o == ffi::Py_None() {
                return View::Null;
            }
            if t == addr_of_mut!(ffi::PyBool_Type) {
                return View::Bool(o == ffi::Py_True());
            }
            if t == addr_of_mut!(ffi::PyLong_Type)
                || (t != addr_of_mut!(ffi::PyFloat_Type) && ffi::PyLong_Check(o) != 0)
            {
                let mut overflow = 0;
                let v = ffi::PyLong_AsLongLongAndOverflow(o, &mut overflow);
                if overflow == 0 {
                    if v == -1 && !ffi::PyErr_Occurred().is_null() {
                        ffi::PyErr_Clear();
                        return self.unsupported();
                    }
                    return View::Number(v.into());
                }
                if overflow > 0 {
                    let u = ffi::PyLong_AsUnsignedLongLong(o);
                    if !(u == u64::MAX && !ffi::PyErr_Occurred().is_null()) {
                        return View::Number(u.into());
                    }
                    ffi::PyErr_Clear();
                }
                // Beyond 64 bits, the nearest double (as for a JSON parser without arbitrary precision).
                let f = ffi::PyLong_AsDouble(o);
                if f == -1.0 && !ffi::PyErr_Occurred().is_null() {
                    ffi::PyErr_Clear();
                    return self.unsupported();
                }
                return Number::from_f64(f).map_or_else(|| self.unsupported(), View::Number);
            }
            if t == addr_of_mut!(ffi::PyFloat_Type) || ffi::PyFloat_Check(o) != 0 {
                return Number::from_f64(ffi::PyFloat_AsDouble(o)).map_or_else(|| self.unsupported(), View::Number);
            }
            // Subclasses of str, dict and list (an OrderedDict, a str enum).
            if ffi::PyUnicode_Check(o) != 0 {
                return match utf8(o) {
                    Some(s) => View::String(s),
                    None => self.unsupported(),
                };
            }
            if ffi::PyDict_Check(o) != 0 && self.depth < MAX_NESTING {
                return View::Object(PyDictView(self));
            }
            if ffi::PyList_Check(o) != 0 && self.depth < MAX_NESTING {
                return View::Array(PyListView(self));
            }
        }
        self.unsupported()
    }
}

#[derive(Clone, Copy)]
struct PyListView<'a>(PyInstance<'a>);

impl<'a> ArrayView<'a> for PyListView<'a> {
    type Item = PyInstance<'a>;

    #[inline]
    fn len(self) -> usize {
        unsafe { ffi::PyList_Size(self.0.obj) as usize }
    }

    #[inline]
    fn get(self, index: usize) -> PyInstance<'a> {
        self.0.child(unsafe { ffi::PyList_GetItem(self.0.obj, index as ffi::Py_ssize_t) })
    }

    fn iter(self) -> impl Iterator<Item = PyInstance<'a>> {
        (0..self.len()).map(move |i| self.get(i))
    }
}

#[derive(Clone, Copy)]
struct PyDictView<'a>(PyInstance<'a>);

/// Dicts up to this size are searched by scanning their keys' UTF-8 (no string to create and hash).
const LINEAR_KEYS: usize = 16;

impl<'a> PyDictView<'a> {
    /// The key's UTF-8, or the empty string (flagging the fallback) for a key that is not a string.
    #[inline]
    fn key(self, key: *mut ffi::PyObject) -> &'a str {
        let s = unsafe { if ffi::PyUnicode_Check(key) != 0 { utf8(key) } else { None } };
        s.unwrap_or_else(|| {
            self.0.fallback.set(true);
            ""
        })
    }
}

/// The entries of a dict, borrowed.
struct DictEntries<'a> {
    dict: PyDictView<'a>,
    pos: ffi::Py_ssize_t,
}

impl<'a> Iterator for DictEntries<'a> {
    type Item = (&'a str, PyInstance<'a>);

    #[inline]
    fn next(&mut self) -> Option<Self::Item> {
        let mut key = std::ptr::null_mut();
        let mut value = std::ptr::null_mut();
        if unsafe { ffi::PyDict_Next(self.dict.0.obj, &mut self.pos, &mut key, &mut value) } == 0 {
            return None;
        }
        Some((self.dict.key(key), self.dict.0.child(value)))
    }
}

impl<'a> ObjectView<'a> for PyDictView<'a> {
    type Item = PyInstance<'a>;

    #[inline]
    fn len(self) -> usize {
        unsafe { ffi::PyDict_Size(self.0.obj) as usize }
    }

    fn get(self, name: &str) -> Option<PyInstance<'a>> {
        if self.len() <= LINEAR_KEYS {
            return self.iter().find(|(k, _)| *k == name).map(|(_, v)| v);
        }
        unsafe {
            let key = ffi::PyUnicode_FromStringAndSize(name.as_ptr().cast(), name.len() as ffi::Py_ssize_t);
            if key.is_null() {
                ffi::PyErr_Clear();
                return None;
            }
            let value = ffi::PyDict_GetItemWithError(self.0.obj, key);
            ffi::Py_DECREF(key);
            if value.is_null() {
                ffi::PyErr_Clear();
                return None;
            }
            Some(self.0.child(value))
        }
    }

    fn iter(self) -> impl Iterator<Item = (&'a str, PyInstance<'a>)> {
        DictEntries { dict: self, pos: 0 }
    }
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

    /// Evaluates the Python objects in place, or the converted value when they hold something the in-place reader
    /// does not (see `PyInstance`).
    fn check_object(&self, instance: &Bound<'_, PyAny>) -> PyResult<bool> {
        let fallback = Cell::new(false);
        let result = self.inner.validate_instance(PyInstance::root(instance, &fallback));
        if fallback.get() {
            return self.check(&to_value(instance, 0)?);
        }
        result.map_err(|e| SchemaEvaluationDepthError::new_err(e.to_string()))
    }
}

#[pymethods]
impl PyValidator {
    /// Whether the instance (a value as `json.loads` produces it) is valid against the schema.
    fn __call__(&self, instance: &Bound<'_, PyAny>) -> PyResult<bool> {
        self.check_object(instance)
    }

    /// Whether the instance (a value as `json.loads` produces it) is valid against the schema.
    fn is_valid(&self, instance: &Bound<'_, PyAny>) -> PyResult<bool> {
        self.check_object(instance)
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
        let Some(mut c) = collector else { return self.check_object(instance) };
        // Results collection reads the converted value (an evaluation that falls back part way would leave rows).
        let value = to_value(instance, 0)?;
        self.inner.evaluate(&value, &mut c.inner).map_err(|e| SchemaEvaluationDepthError::new_err(e.to_string()))
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
