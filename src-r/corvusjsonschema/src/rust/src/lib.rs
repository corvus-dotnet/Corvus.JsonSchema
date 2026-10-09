//! The native code of the corvusjsonschema R package: the corvus-json-schema crate, with R values read in place (see
//! src-rs/BINDINGS.md). The R layer (R/corvusjsonschema.R) gives the public API.
//!
//! Every entry point returns an R value. A failure is returned as a list of class `corvus_native_error` (its kind and
//! message), which the R layer raises as a condition: nothing here calls `Rf_error`, whose long jump would skip the
//! destructors of the Rust frames above it.

mod r;

use std::cell::{Cell, RefCell};
use std::os::raw::{c_char, c_int, c_void};
use std::panic::{AssertUnwindSafe, catch_unwind};
use std::sync::{Arc, Mutex};

use corvus::{
    ArrayView, CompileOptions, Dialect, Instance, JsonSchemaResultsCollector, JsonValidationError, Kind, ObjectView,
    ResultsLevel, View,
};
use r::{R_xlen_t, SEXP};
use serde_json::{Map, Number, Value as Json};

/// Lists nested deeper than this are converted (with a depth check) rather than read in place.
const MAX_NESTING: u32 = 256;

/// Whole doubles up to this magnitude are read as integers (every one of them is exact).
const MAX_EXACT_INTEGER: f64 = 9_007_199_254_740_992.0;

// ---------------------------------------------------------------------------------------------------------------------
// Errors

/// A failure, as the R layer raises it: the kind names the condition's class.
struct Failure {
    kind: &'static str,
    message: String,
}

impl Failure {
    fn new(kind: &'static str, message: impl Into<String>) -> Failure {
        Failure { kind, message: message.into() }
    }

    fn value(message: impl Into<String>) -> Failure {
        Failure::new("value", message)
    }

    fn depth() -> Failure {
        Failure::new("depth", corvus::SchemaEvaluationDepthError.to_string())
    }
}

thread_local! {
    /// The first failure of an R callback during the current call (format validators and the resolver run inside the
    /// crate, which cannot carry an R error out).
    static CALLBACK_ERROR: RefCell<Option<String>> = const { RefCell::new(None) };
}

fn note_callback_error(message: String) {
    CALLBACK_ERROR.with(|e| {
        e.borrow_mut().get_or_insert(message);
    });
}

fn take_callback_error() -> Result<(), Failure> {
    match CALLBACK_ERROR.with(|e| e.borrow_mut().take()) {
        Some(message) => Err(Failure::new("callback", message)),
        None => Ok(()),
    }
}

/// Runs an entry point: a panic becomes a failure, and a failure becomes the list the R layer raises.
fn entry(f: impl FnOnce() -> Result<SEXP, Failure>) -> SEXP {
    let result = catch_unwind(AssertUnwindSafe(f)).unwrap_or_else(|panic| {
        let message = panic
            .downcast_ref::<&str>()
            .map(|s| (*s).to_owned())
            .or_else(|| panic.downcast_ref::<String>().cloned())
            .unwrap_or_else(|| "the native code panicked".to_owned());
        Err(Failure::new("internal", message))
    });
    match result {
        Ok(value) => value,
        Err(failure) => unsafe {
            let out = r::Rf_protect(r::Rf_allocVector(r::VECSXP as u32, 2));
            r::SET_VECTOR_ELT(out, 0, string_vector(&[failure.kind]));
            r::SET_VECTOR_ELT(out, 1, string_vector(&[&failure.message]));
            let class = string_vector(&["corvus_native_error"]);
            r::Rf_protect(class);
            r::Rf_setAttrib(out, r::class_symbol(), class);
            r::Rf_unprotect(2);
            out
        },
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// R values made here

/// A CHARSXP for UTF-8 text. R's strings cannot hold a NUL (R would raise an error on one), so each is written as
/// U+FFFD, the replacement character.
unsafe fn mk_char(s: &str) -> SEXP {
    let replaced;
    let s = if s.contains('\0') {
        replaced = s.replace('\0', "\u{FFFD}");
        &replaced
    } else {
        s
    };
    // An R string holds at most 2^31 - 1 bytes; nothing built here comes near it.
    unsafe { r::Rf_mkCharLenCE(s.as_ptr().cast::<c_char>(), s.len() as c_int, r::CE_UTF8) }
}

/// A character vector of the strings.
unsafe fn string_vector(items: &[&str]) -> SEXP {
    unsafe {
        let out = r::Rf_protect(r::Rf_allocVector(r::STRSXP as u32, items.len() as R_xlen_t));
        for (i, s) in items.iter().enumerate() {
            r::SET_STRING_ELT(out, i as R_xlen_t, mk_char(s));
        }
        r::Rf_unprotect(1);
        out
    }
}

/// JSON as an R value (for annotations): objects as named lists, arrays as lists, `null` as `NULL`.
unsafe fn to_r(j: &Json) -> SEXP {
    unsafe {
        match j {
            Json::Null => r::nil(),
            Json::Bool(b) => r::Rf_ScalarLogical(c_int::from(*b)),
            Json::Number(n) => match n.as_i64() {
                Some(i) if i > i64::from(c_int::MIN) && i <= i64::from(c_int::MAX) => r::Rf_ScalarInteger(i as c_int),
                _ => r::Rf_ScalarReal(n.as_f64().unwrap_or(f64::NAN)),
            },
            Json::String(s) => string_vector(&[s]),
            Json::Array(a) => {
                let out = r::Rf_protect(r::Rf_allocVector(r::VECSXP as u32, a.len() as R_xlen_t));
                for (i, item) in a.iter().enumerate() {
                    r::SET_VECTOR_ELT(out, i as R_xlen_t, to_r(item));
                }
                r::Rf_unprotect(1);
                out
            }
            Json::Object(o) => {
                let out = r::Rf_protect(r::Rf_allocVector(r::VECSXP as u32, o.len() as R_xlen_t));
                let names = r::Rf_protect(r::Rf_allocVector(r::STRSXP as u32, o.len() as R_xlen_t));
                for (i, (k, v)) in o.iter().enumerate() {
                    r::SET_STRING_ELT(names, i as R_xlen_t, mk_char(k));
                    r::SET_VECTOR_ELT(out, i as R_xlen_t, to_r(v));
                }
                r::Rf_setAttrib(out, r::names_symbol(), names);
                r::Rf_unprotect(2);
                out
            }
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// R values read in place

/// The state of one evaluation: whether a value could not be read in place (so the caller converts the instance and
/// evaluates that instead, which also reports what the value was).
struct Context {
    fallback: Cell<bool>,
}

/// How an R value reads as JSON.
///
/// - `NULL` is `null`; a list with names is an object and a list without is an array (so `list()` is `[]`, and an empty
///   named list is `{}`), as jsonlite reads and writes them;
/// - a logical, integer, double or character vector of length one is a scalar, and of any other length an array of
///   scalars (its names are ignored); wrapped in `I()`, a vector of length one is an array too;
/// - `NA` of any type is `null`; `NaN` and the infinities are not JSON values;
/// - a whole double is an integer (R writes `1` for the double 1), in every dialect.
///
/// Anything else (a function, an environment, a data frame, a factor or any other classed object) is not a JSON
/// value. Such a value, a string in another encoding (which the conversion translates), or nesting deeper than
/// `MAX_NESTING`, sets the context's fallback.
///
/// R's collector does not move values, and the instance is protected by the call that passed it for the whole
/// evaluation, so the borrows made here hold even when an R format validator runs (and collects) during it.
#[derive(Clone, Copy)]
struct RInstance<'a> {
    v: SEXP,
    /// The element of the atomic vector `v`, or -1 for `v` itself.
    element: R_xlen_t,
    ctx: &'a Context,
    depth: u32,
}

impl<'a> RInstance<'a> {
    fn root(v: SEXP, ctx: &'a Context) -> Self {
        RInstance { v, element: -1, ctx, depth: 0 }
    }

    #[inline(always)]
    fn child(self, v: SEXP) -> Self {
        RInstance { v, element: -1, ctx: self.ctx, depth: self.depth + 1 }
    }

    #[inline(always)]
    fn element(self, index: R_xlen_t) -> Self {
        RInstance { v: self.v, element: index, ctx: self.ctx, depth: self.depth + 1 }
    }

    #[cold]
    fn unsupported(self) -> View<'a, Self> {
        self.ctx.fallback.set(true);
        View::Null
    }

    /// The scalar at `index` of the atomic vector `v` of type `kind`.
    fn scalar(self, kind: c_int, index: R_xlen_t) -> View<'a, Self> {
        // SAFETY: `v` is a live vector of this type with more than `index` elements.
        unsafe {
            match kind {
                r::LGLSXP => match r::LOGICAL_ELT(self.v, index) {
                    r::NA_INTEGER => View::Null,
                    b => View::Bool(b != 0),
                },
                r::INTSXP => match r::INTEGER_ELT(self.v, index) {
                    r::NA_INTEGER => View::Null,
                    i => View::Number(Number::from(i)),
                },
                r::REALSXP => {
                    let x = r::REAL_ELT(self.v, index);
                    if r::R_IsNA(x) != 0 {
                        return View::Null;
                    }
                    number_of(x).map_or_else(|| self.unsupported(), View::Number)
                }
                _ => {
                    let s = r::STRING_ELT(self.v, index);
                    if s == r::na_string() {
                        return View::Null;
                    }
                    str_of(s).map_or_else(|| self.unsupported(), View::String)
                }
            }
        }
    }
}

/// A double as a JSON number: a whole one as an integer; `None` for `NaN` and the infinities.
fn number_of(x: f64) -> Option<Number> {
    if x.fract() == 0.0 && x.abs() <= MAX_EXACT_INTEGER {
        return Some(Number::from(x as i64));
    }
    Number::from_f64(x)
}

/// A CHARSXP's text when it can be read where it lies: ASCII, or valid UTF-8 that is marked as UTF-8 or as being in
/// the session's native encoding. A native string that is valid UTF-8 is taken to be UTF-8 whatever the locale: in a
/// UTF-8 locale it is, and in the C locale, where R leaves such strings unmarked, nothing else can read it. The
/// borrow lasts while the string lives (see `RInstance`).
fn str_of<'a>(s: SEXP) -> Option<&'a str> {
    // SAFETY: `s` is a live CHARSXP: its bytes are `Rf_xlength(s)` long and never change.
    unsafe {
        let bytes: &'a [u8] = std::slice::from_raw_parts(r::R_CHAR(s).cast::<u8>(), r::Rf_xlength(s) as usize);
        if bytes.is_ascii() {
            return Some(std::str::from_utf8_unchecked(bytes));
        }
        match r::Rf_getCharCE(s) {
            r::CE_UTF8 | r::CE_NATIVE => std::str::from_utf8(bytes).ok(),
            _ => None,
        }
    }
}

/// Whether the vector reads as an array even at length one (it was wrapped in `I()`), or `None` when it is an object
/// of another class, which is not a JSON value.
fn as_is(v: SEXP) -> Option<bool> {
    // SAFETY: `v` is a live R value.
    unsafe {
        if r::Rf_isObject(v) == 0 {
            return Some(false);
        }
        (r::Rf_inherits(v, c"AsIs".as_ptr()) != 0 && r::Rf_inherits(v, c"factor".as_ptr()) == 0).then_some(true)
    }
}

impl<'a> Instance<'a> for RInstance<'a> {
    type Array = RArray<'a>;
    type Object = RObject<'a>;

    fn view(self) -> View<'a, Self> {
        // SAFETY: `v` is a live R value.
        unsafe {
            let kind = r::TYPEOF(self.v);
            if self.element >= 0 {
                return self.scalar(kind, self.element);
            }
            match kind {
                r::NILSXP => View::Null,
                r::VECSXP => {
                    if self.depth >= MAX_NESTING || r::Rf_isObject(self.v) != 0 {
                        return self.unsupported();
                    }
                    let names = r::Rf_getAttrib(self.v, r::names_symbol());
                    let len = r::Rf_xlength(self.v) as usize;
                    if names == r::nil() {
                        View::Array(RArray { len, atomic: false, parent: self })
                    } else {
                        View::Object(RObject { names, len, parent: self })
                    }
                }
                r::LGLSXP | r::INTSXP | r::REALSXP | r::STRSXP => {
                    let Some(array) = as_is(self.v) else { return self.unsupported() };
                    let len = r::Rf_xlength(self.v) as usize;
                    if len == 1 && !array {
                        self.scalar(kind, 0)
                    } else {
                        View::Array(RArray { len, atomic: true, parent: self })
                    }
                }
                _ => self.unsupported(),
            }
        }
    }

    fn kind(self) -> Kind {
        match self.view() {
            View::Null => Kind::Null,
            View::Bool(_) => Kind::Bool,
            View::Number(_) => Kind::Number,
            View::String(_) => Kind::String,
            View::Array(_) => Kind::Array,
            View::Object(_) => Kind::Object,
        }
    }
}

/// A list without names, or an atomic vector, as a JSON array.
#[derive(Clone, Copy)]
struct RArray<'a> {
    len: usize,
    atomic: bool,
    parent: RInstance<'a>,
}

impl<'a> ArrayView<'a> for RArray<'a> {
    type Item = RInstance<'a>;

    fn len(self) -> usize {
        self.len
    }

    fn get(self, index: usize) -> RInstance<'a> {
        assert!(index < self.len, "an index beyond the end of the array");
        if self.atomic {
            self.parent.element(index as R_xlen_t)
        } else {
            // SAFETY: the parent's value is a live list with more than `index` elements.
            self.parent.child(unsafe { r::VECTOR_ELT(self.parent.v, index as R_xlen_t) })
        }
    }

    fn iter(self) -> impl Iterator<Item = RInstance<'a>> {
        (0..self.len).map(move |i| self.get(i))
    }
}

/// A list with names as a JSON object. A name that is `NA` or not UTF-8 sets the context's fallback.
#[derive(Clone, Copy)]
struct RObject<'a> {
    names: SEXP,
    len: usize,
    parent: RInstance<'a>,
}

impl<'a> RObject<'a> {
    fn name(self, index: usize) -> &'a str {
        // SAFETY: the names are a live character vector as long as the list.
        let s = unsafe { r::STRING_ELT(self.names, index as R_xlen_t) };
        let text = if s == r::na_string() { None } else { str_of(s) };
        text.unwrap_or_else(|| {
            self.parent.ctx.fallback.set(true);
            ""
        })
    }

    fn value(self, index: usize) -> RInstance<'a> {
        // SAFETY: the parent's value is a live list with more than `index` elements.
        self.parent.child(unsafe { r::VECTOR_ELT(self.parent.v, index as R_xlen_t) })
    }
}

impl<'a> ObjectView<'a> for RObject<'a> {
    type Item = RInstance<'a>;

    fn len(self) -> usize {
        self.len
    }

    fn get(self, name: &str) -> Option<RInstance<'a>> {
        (0..self.len).find(|&i| self.name(i) == name).map(|i| self.value(i))
    }

    fn iter(self) -> impl Iterator<Item = (&'a str, RInstance<'a>)> {
        (0..self.len).map(move |i| (self.name(i), self.value(i)))
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Conversions

/// A CHARSXP's text in UTF-8, translated from its encoding when it has another.
fn text_of(s: SEXP) -> Result<String, Failure> {
    if let Some(text) = str_of(s) {
        return Ok(text.to_owned());
    }
    // SAFETY: `s` is a live CHARSXP; the translation is NUL-terminated and lives until the call returns.
    let translated = unsafe { std::ffi::CStr::from_ptr(r::Rf_translateCharUTF8(s)) };
    translated.to_str().map(str::to_owned).map_err(|_| Failure::value("a string that cannot be read as UTF-8"))
}

/// The scalar at `index` of an atomic vector as JSON.
fn scalar_to_json(v: SEXP, kind: c_int, index: R_xlen_t) -> Result<Json, Failure> {
    // SAFETY: `v` is a live vector of this type with more than `index` elements.
    unsafe {
        Ok(match kind {
            r::LGLSXP => match r::LOGICAL_ELT(v, index) {
                r::NA_INTEGER => Json::Null,
                b => Json::Bool(b != 0),
            },
            r::INTSXP => match r::INTEGER_ELT(v, index) {
                r::NA_INTEGER => Json::Null,
                i => Json::Number(Number::from(i)),
            },
            r::REALSXP => {
                let x = r::REAL_ELT(v, index);
                if r::R_IsNA(x) != 0 {
                    Json::Null
                } else {
                    Json::Number(
                        number_of(x).ok_or_else(|| Failure::value("NaN and the infinities are not JSON numbers"))?,
                    )
                }
            }
            _ => {
                let s = r::STRING_ELT(v, index);
                if s == r::na_string() { Json::Null } else { Json::String(text_of(s)?) }
            }
        })
    }
}

/// What a value that is not a JSON value is, for the message.
fn describe(v: SEXP) -> String {
    // SAFETY: `v` is a live R value.
    unsafe {
        if r::Rf_isObject(v) != 0 {
            let class = r::Rf_getAttrib(v, r::class_symbol());
            if r::TYPEOF(class) == r::STRSXP
                && r::Rf_xlength(class) > 0
                && let Ok(name) = text_of(r::STRING_ELT(class, 0))
            {
                return format!("an object of class \"{name}\"");
            }
            return "a classed object".to_owned();
        }
        match r::TYPEOF(v) {
            3 | 7 | 8 => "a function".to_owned(),
            4 => "an environment".to_owned(),
            15 => "a complex vector".to_owned(),
            24 => "a raw vector".to_owned(),
            1 => "a symbol".to_owned(),
            6 => "a call".to_owned(),
            kind => format!("a value of type {kind}"),
        }
    }
}

/// An R value as JSON (for schemas, and for instances that cannot be read in place), as `RInstance` reads one.
fn to_json(v: SEXP, depth: u32) -> Result<Json, Failure> {
    if depth > 1024 {
        return Err(Failure::value("the value nests too deeply"));
    }
    // SAFETY: `v` is a live R value.
    unsafe {
        let kind = r::TYPEOF(v);
        match kind {
            r::NILSXP => Ok(Json::Null),
            r::VECSXP => {
                if r::Rf_isObject(v) != 0 {
                    return Err(Failure::value(format!("{} is not a JSON value", describe(v))));
                }
                let names = r::Rf_getAttrib(v, r::names_symbol());
                let len = r::Rf_xlength(v);
                if names == r::nil() {
                    let mut out = Vec::with_capacity(len as usize);
                    for i in 0..len {
                        out.push(to_json(r::VECTOR_ELT(v, i), depth + 1)?);
                    }
                    return Ok(Json::Array(out));
                }
                let mut out = Map::with_capacity(len as usize);
                for i in 0..len {
                    let name = r::STRING_ELT(names, i);
                    if name == r::na_string() {
                        return Err(Failure::value("an NA name is not a JSON property name"));
                    }
                    // The first of two properties with one name is the one read in place.
                    let key = text_of(name)?;
                    if !out.contains_key(&key) {
                        out.insert(key, to_json(r::VECTOR_ELT(v, i), depth + 1)?);
                    }
                }
                Ok(Json::Object(out))
            }
            r::LGLSXP | r::INTSXP | r::REALSXP | r::STRSXP => {
                let Some(array) = as_is(v) else {
                    return Err(Failure::value(format!("{} is not a JSON value", describe(v))));
                };
                let len = r::Rf_xlength(v);
                if len == 1 && !array {
                    return scalar_to_json(v, kind, 0);
                }
                let mut out = Vec::with_capacity(len as usize);
                for i in 0..len {
                    out.push(scalar_to_json(v, kind, i)?);
                }
                Ok(Json::Array(out))
            }
            _ => Err(Failure::value(format!("{} is not a JSON value", describe(v)))),
        }
    }
}

/// A character vector of length one, not `NA`, as text.
fn single_string(v: SEXP) -> Option<Result<String, Failure>> {
    // SAFETY: `v` is a live R value.
    unsafe {
        if r::TYPEOF(v) != r::STRSXP || r::Rf_xlength(v) != 1 || r::Rf_isObject(v) != 0 {
            return None;
        }
        let s = r::STRING_ELT(v, 0);
        (s != r::na_string()).then(|| text_of(s))
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Callbacks

/// An R function the validator keeps (its external pointer protects it). The R layer wraps each one so that a call
/// returns `list(TRUE, value)` or `list(FALSE, message)` and never signals a condition.
#[derive(Clone, Copy)]
struct Callback(SEXP);

// SAFETY: R runs on one thread, and callbacks run only inside a call from it; the crate requires Send + Sync of the
// closures that hold them.
unsafe impl Send for Callback {}
unsafe impl Sync for Callback {}

impl Callback {
    /// Calls the function with one string, giving the value it returned or the message of the condition it signalled.
    /// The value is unprotected: the caller reads it before anything else allocates.
    fn call(self, arg: &str) -> Result<SEXP, String> {
        // SAFETY: the function is live (see `Callback`), and the R layer's wrapper returns a list of two.
        unsafe {
            let text = r::Rf_protect(string_vector(&[arg]));
            let call = r::Rf_protect(r::Rf_lang2(self.0, text));
            let result = r::Rf_eval(call, r::global_env());
            r::Rf_unprotect(2);
            if r::TYPEOF(result) != r::VECSXP || r::Rf_xlength(result) != 2 {
                return Err("a callback returned an unexpected value".to_owned());
            }
            let ok = r::VECTOR_ELT(result, 0);
            let value = r::VECTOR_ELT(result, 1);
            if r::TYPEOF(ok) == r::LGLSXP && r::Rf_xlength(ok) == 1 && r::LOGICAL_ELT(ok, 0) == 1 {
                return Ok(value);
            }
            Err(single_string(value).and_then(Result::ok).unwrap_or_else(|| "a callback failed".to_owned()))
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Validator

unsafe extern "C" fn finalize_validator(pointer: SEXP) {
    // SAFETY: the pointer was made by `cjsr_compile` and is cleared here, so the box is dropped once.
    unsafe {
        let address = r::R_ExternalPtrAddr(pointer).cast::<corvus::Validator>();
        if !address.is_null() {
            r::R_ClearExternalPtr(pointer);
            drop(Box::from_raw(address));
        }
    }
}

fn validator_of<'a>(pointer: SEXP) -> Result<&'a corvus::Validator, Failure> {
    // SAFETY: an external pointer with this package's tag is one `cjsr_compile` made; a validator restored from a
    // saved workspace has a null address.
    unsafe {
        if r::TYPEOF(pointer) != 22 || r::R_ExternalPtrTag(pointer) != r::validator_tag() {
            return Err(Failure::value("validator must be a compiled schema (see compile_schema)"));
        }
        r::R_ExternalPtrAddr(pointer).cast::<corvus::Validator>().as_ref().ok_or_else(|| {
            Failure::value("the compiled schema is no longer valid (it does not survive a saved session)")
        })
    }
}

fn integer_argument(v: SEXP, what: &str) -> Result<i64, Failure> {
    // SAFETY: `v` is a live R value.
    unsafe {
        match (r::TYPEOF(v), r::Rf_xlength(v)) {
            (r::INTSXP, 1) if r::INTEGER_ELT(v, 0) != r::NA_INTEGER => Ok(i64::from(r::INTEGER_ELT(v, 0))),
            _ => Err(Failure::value(format!("{what} must be one integer"))),
        }
    }
}

/// A logical of length one: `Some(None)` for `NA`.
fn logical_argument(v: SEXP, what: &str) -> Result<Option<bool>, Failure> {
    // SAFETY: `v` is a live R value.
    unsafe {
        match (r::TYPEOF(v), r::Rf_xlength(v)) {
            (r::LGLSXP, 1) => Ok(match r::LOGICAL_ELT(v, 0) {
                r::NA_INTEGER => None,
                b => Some(b != 0),
            }),
            _ => Err(Failure::value(format!("{what} must be TRUE, FALSE or NA"))),
        }
    }
}

fn optional_string(v: SEXP, what: &str) -> Result<Option<String>, Failure> {
    if v == r::nil() {
        return Ok(None);
    }
    single_string(v).ok_or_else(|| Failure::value(format!("{what} must be one string or NULL")))?.map(Some)
}

fn level_argument(v: SEXP) -> Result<ResultsLevel, Failure> {
    match integer_argument(v, "the results level")? {
        0 => Ok(ResultsLevel::Basic),
        1 => Ok(ResultsLevel::Detailed),
        2 => Ok(ResultsLevel::Verbose),
        l => Err(Failure::value(format!("{l} is not a results level"))),
    }
}

/// Validates an R value: read in place, or converted when it holds something the reader does not.
fn check(validator: &corvus::Validator, value: SEXP) -> Result<bool, Failure> {
    let ctx = Context { fallback: Cell::new(false) };
    let result = validator.validate_instance(RInstance::root(value, &ctx));
    if !ctx.fallback.get() {
        take_callback_error()?;
        return result.map_err(|_| Failure::depth());
    }
    CALLBACK_ERROR.with(|e| e.borrow_mut().take());
    let json = to_json(value, 0)?;
    let result = validator.validate(&json);
    take_callback_error()?;
    result.map_err(|_| Failure::depth())
}

/// The results of an evaluation as an R list: whether the instance is valid, the columns of the rows, and the columns
/// of the annotations (of a verbose evaluation; `NULL` otherwise).
fn evaluation(valid: bool, collector: &JsonSchemaResultsCollector) -> SEXP {
    let rows = collector.results();
    // SAFETY: every value allocated here is protected until it is stored in a protected list.
    unsafe {
        let out = r::Rf_protect(r::Rf_allocVector(r::VECSXP as u32, 3));
        r::SET_VECTOR_ELT(out, 0, r::Rf_ScalarLogical(c_int::from(valid)));

        let columns = r::Rf_allocVector(r::VECSXP as u32, 5);
        r::SET_VECTOR_ELT(out, 1, columns);
        let matches = r::Rf_allocVector(r::LGLSXP as u32, rows.len() as R_xlen_t);
        r::SET_VECTOR_ELT(columns, 0, matches);
        for (i, row) in rows.iter().enumerate() {
            r::SET_LOGICAL_ELT(matches, i as R_xlen_t, c_int::from(row.is_match));
        }
        let texts: [fn(&corvus::SchemaResult) -> &str; 4] = [
            |row| &row.message,
            |row| &row.evaluation_location,
            |row| &row.schema_evaluation_location,
            |row| &row.document_evaluation_location,
        ];
        for (c, text) in texts.iter().enumerate() {
            let column = r::Rf_allocVector(r::STRSXP as u32, rows.len() as R_xlen_t);
            r::SET_VECTOR_ELT(columns, (c + 1) as R_xlen_t, column);
            for (i, row) in rows.iter().enumerate() {
                r::SET_STRING_ELT(column, i as R_xlen_t, mk_char(text(row)));
            }
        }

        if collector.level() == ResultsLevel::Verbose {
            // A row for each annotation: where in the instance, the keyword, where in the schema, and the value.
            let annotations: Vec<corvus::Annotation> = corvus::enumerate_annotations(collector).collect();
            let columns = r::Rf_allocVector(r::VECSXP as u32, 4);
            r::SET_VECTOR_ELT(out, 2, columns);
            for c in 0..3 {
                let column = r::Rf_allocVector(r::STRSXP as u32, annotations.len() as R_xlen_t);
                r::SET_VECTOR_ELT(columns, c, column);
                for (i, a) in annotations.iter().enumerate() {
                    let fragment;
                    let text: &str = match c {
                        0 => &a.instance_location,
                        1 => &a.keyword,
                        _ => {
                            fragment = corvus::schema_location_fragment(&a.schema_location);
                            &fragment
                        }
                    };
                    r::SET_STRING_ELT(column, i as R_xlen_t, mk_char(text));
                }
            }
            let values = r::Rf_allocVector(r::VECSXP as u32, annotations.len() as R_xlen_t);
            r::SET_VECTOR_ELT(columns, 3, values);
            for (i, a) in annotations.iter().enumerate() {
                let value = serde_json::from_str(&a.value).unwrap_or_else(|_| Json::String(a.value.clone()));
                r::SET_VECTOR_ELT(values, i as R_xlen_t, to_r(&value));
            }
        }
        r::Rf_unprotect(1);
        out
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Entry points

/// Keeps R's global values. Called once by `R_init_corvusjsonschema`.
///
/// # Safety
/// The arguments are R's `R_NilValue`, `R_NamesSymbol`, `R_ClassSymbol`, `R_NaString` and `R_GlobalEnv`, and the
/// symbol that tags a validator's external pointer.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjsr_init(
    nil: SEXP,
    names: SEXP,
    class: SEXP,
    na_string: SEXP,
    global_env: SEXP,
    validator_tag: SEXP,
) {
    r::set_globals(nil, names, class, na_string, global_env, validator_tag);
}

/// The version of the corvus-json-schema crate the package was built from.
#[unsafe(no_mangle)]
pub extern "C" fn cjsr_crate_version() -> SEXP {
    entry(|| Ok(unsafe { string_vector(&[corvus::VERSION]) }))
}

/// Compiles a schema (an R value, or JSON text) with the options the R layer passes, giving an external pointer to
/// the validator. `formats` is a named list of wrapped functions, and `resolver` a wrapped function, or `NULL`.
///
/// # Safety
/// The arguments are live R values.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjsr_compile(
    schema: SEXP,
    dialect: SEXP,
    assert_format: SEXP,
    assert_format_in_legacy_drafts: SEXP,
    assert_content: SEXP,
    formats: SEXP,
    resolver: SEXP,
    base_uri: SEXP,
    entry_point: SEXP,
    max_depth: SEXP,
) -> SEXP {
    entry(|| {
        let schema: Json = match single_string(schema) {
            Some(text) => serde_json::from_str(&text?)
                .map_err(|e| Failure::new("invalid_json", format!("the schema is not valid JSON: {e}")))?,
            None => to_json(schema, 0)?,
        };
        let mut options = CompileOptions {
            default_dialect: match integer_argument(dialect, "the dialect")? {
                4 => Dialect::Draft4,
                6 => Dialect::Draft6,
                7 => Dialect::Draft7,
                2019 => Dialect::Draft201909,
                2020 => Dialect::Draft202012,
                d => return Err(Failure::value(format!("{d} is not a dialect"))),
            },
            assert_format: logical_argument(assert_format, "assert_format")?,
            assert_format_in_legacy_drafts: logical_argument(
                assert_format_in_legacy_drafts,
                "assert_format_in_legacy_drafts",
            )?
            .unwrap_or(false),
            assert_content: logical_argument(assert_content, "assert_content")?.unwrap_or(true),
            base_uri: optional_string(base_uri, "base_uri")?,
            entry_point: optional_string(entry_point, "entry_point")?,
            max_depth: u32::try_from(integer_argument(max_depth, "max_depth")?)
                .map_err(|_| Failure::value("max_depth must not be negative"))?,
            ..CompileOptions::default()
        };
        // SAFETY: the R layer passes a named list of functions (or NULL) and a function (or NULL).
        unsafe {
            if formats != r::nil() {
                let names = r::Rf_getAttrib(formats, r::names_symbol());
                for i in 0..r::Rf_xlength(formats) {
                    let name = text_of(r::STRING_ELT(names, i))?;
                    let callback = Callback(r::VECTOR_ELT(formats, i));
                    options.formats.insert(
                        name,
                        Arc::new(move |s: &str| match callback.call(s) {
                            Ok(v) => r::TYPEOF(v) == r::LGLSXP && r::Rf_xlength(v) == 1 && r::LOGICAL_ELT(v, 0) == 1,
                            Err(message) => {
                                note_callback_error(message);
                                false
                            }
                        }),
                    );
                }
            }
        }
        // The first failure of the resolver (an R error, or a value that is not JSON), as text: the crate's resolver
        // can only say a document is unknown.
        let resolver_error: Arc<Mutex<Option<String>>> = Default::default();
        if resolver != r::nil() {
            let callback = Callback(resolver);
            let failure = resolver_error.clone();
            options.resolve_document = Some(Arc::new(move |uri: &str| {
                let fail = |message: String| {
                    failure.lock().unwrap().get_or_insert(message);
                    None
                };
                match callback.call(uri) {
                    Ok(v) if v == r::nil() => None,
                    Ok(v) => {
                        // Translating a string to UTF-8 allocates in R, so the value is protected while it is read.
                        // SAFETY: `v` is a live R value.
                        unsafe { r::Rf_protect(v) };
                        let document = match single_string(v) {
                            Some(Ok(text)) => serde_json::from_str::<Json>(&text)
                                .map_err(|e| format!("the resolver returned invalid JSON for {uri}: {e}")),
                            Some(Err(e)) => Err(format!("the resolver failed for {uri}: {}", e.message)),
                            None => to_json(v, 0).map_err(|e| format!("the resolver failed for {uri}: {}", e.message)),
                        };
                        // SAFETY: balances the protection above.
                        unsafe { r::Rf_unprotect(1) };
                        document.map_or_else(fail, Some)
                    }
                    Err(message) => fail(format!("the resolver failed for {uri}: {message}")),
                }
            }));
        }
        let validator = corvus::compile_with(&schema, &options).map_err(|e| {
            let message = match resolver_error.lock().unwrap().take() {
                Some(reason) => format!("{} ({reason})", e.message()),
                None => e.message().to_owned(),
            };
            Failure::new("compilation", message)
        })?;
        // SAFETY: the callbacks live as long as the pointer, which protects the list holding them.
        unsafe {
            let kept = r::Rf_protect(r::Rf_allocVector(r::VECSXP as u32, 2));
            r::SET_VECTOR_ELT(kept, 0, formats);
            r::SET_VECTOR_ELT(kept, 1, resolver);
            let pointer = r::Rf_protect(r::R_MakeExternalPtr(
                Box::into_raw(Box::new(validator)).cast::<c_void>(),
                r::validator_tag(),
                kept,
            ));
            r::R_RegisterCFinalizerEx(pointer, finalize_validator, 1);
            r::Rf_unprotect(2);
            Ok(pointer)
        }
    })
}

/// Whether an R value is valid.
///
/// # Safety
/// The arguments are live R values.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjsr_is_valid(validator: SEXP, value: SEXP) -> SEXP {
    entry(|| {
        let valid = check(validator_of(validator)?, value)?;
        Ok(unsafe { r::Rf_ScalarLogical(c_int::from(valid)) })
    })
}

fn json_failure(e: JsonValidationError, index: Option<R_xlen_t>) -> Failure {
    match e {
        JsonValidationError::InvalidJson(e) => {
            let at = index.map_or_else(String::new, |i| format!(" (element {})", i + 1));
            Failure::new("invalid_json", format!("{e}{at}"))
        }
        JsonValidationError::DepthExceeded(_) => Failure::depth(),
    }
}

/// Whether each JSON text of a character vector is valid (`NA` for an `NA` element).
///
/// # Safety
/// The arguments are live R values.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjsr_is_valid_json(validator: SEXP, texts: SEXP) -> SEXP {
    entry(|| {
        let validator = validator_of(validator)?;
        // SAFETY: `texts` is a live R value, and the result is protected while it is filled.
        unsafe {
            if r::TYPEOF(texts) != r::STRSXP {
                return Err(Failure::value("the JSON text must be a character vector"));
            }
            let len = r::Rf_xlength(texts);
            let out = r::Rf_protect(r::Rf_allocVector(r::LGLSXP as u32, len));
            let mut failure = None;
            for i in 0..len {
                let s = r::STRING_ELT(texts, i);
                if s == r::na_string() {
                    r::SET_LOGICAL_ELT(out, i, r::NA_INTEGER);
                    continue;
                }
                let owned;
                let json: &str = match str_of(s) {
                    Some(text) => text,
                    None => match text_of(s) {
                        Ok(text) => {
                            owned = text;
                            &owned
                        }
                        Err(e) => {
                            failure = Some(e);
                            break;
                        }
                    },
                };
                let result = validator.validate_json(json);
                if let Err(e) = take_callback_error() {
                    failure = Some(e);
                    break;
                }
                match result {
                    Ok(valid) => r::SET_LOGICAL_ELT(out, i, c_int::from(valid)),
                    Err(e) => {
                        failure = Some(json_failure(e, (len > 1).then_some(i)));
                        break;
                    }
                }
            }
            r::Rf_unprotect(1);
            failure.map_or(Ok(out), Err)
        }
    })
}

/// Evaluates an R value exhaustively at a results level, giving the list `evaluation` builds.
///
/// # Safety
/// The arguments are live R values.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjsr_evaluate(validator: SEXP, value: SEXP, level: SEXP) -> SEXP {
    entry(|| {
        let validator = validator_of(validator)?;
        let level = level_argument(level)?;
        let mut collector = JsonSchemaResultsCollector::new(level);
        let ctx = Context { fallback: Cell::new(false) };
        let result = validator.evaluate_instance(RInstance::root(value, &ctx), &mut collector);
        let result = if ctx.fallback.get() {
            CALLBACK_ERROR.with(|e| e.borrow_mut().take());
            collector = JsonSchemaResultsCollector::new(level);
            let json = to_json(value, 0)?;
            validator.evaluate(&json, &mut collector)
        } else {
            result
        };
        take_callback_error()?;
        Ok(evaluation(result.map_err(|_| Failure::depth())?, &collector))
    })
}

/// Evaluates JSON text exhaustively at a results level, giving the list `evaluation` builds.
///
/// # Safety
/// The arguments are live R values.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjsr_evaluate_json(validator: SEXP, text: SEXP, level: SEXP) -> SEXP {
    entry(|| {
        let validator = validator_of(validator)?;
        let level = level_argument(level)?;
        let text =
            single_string(text).ok_or_else(|| Failure::value("the JSON text must be one string, and not NA"))??;
        let mut collector = JsonSchemaResultsCollector::new(level);
        let result = validator.evaluate_json(&text, &mut collector);
        take_callback_error()?;
        Ok(evaluation(result.map_err(|e| json_failure(e, None))?, &collector))
    })
}
