//! The C library over the `corvus-json-schema` crate (see DESIGN.md): opaque handles created and freed by the library,
//! UTF-8 strings in as (pointer, length) and out as borrowed `cjs_str` views, a `cjs_status` from every fallible
//! function with the details in thread-local storage, and no Rust panic crossing into the caller.
//!
//! The comments on the public items are the C header's documentation (cbindgen copies them).

// The names are the C API's.
#![allow(non_camel_case_types)]
// The safety contracts are the C API's documented ones (a handle from this library, a valid pointer and length).
#![allow(clippy::missing_safety_doc)]

use std::cell::RefCell;
use std::ffi::{c_char, c_void};
use std::panic::{AssertUnwindSafe, catch_unwind};
use std::sync::{Arc, Mutex};

use corvus::{
    CompileOptions, Dialect, JsonDocument, JsonSchemaResultsCollector, JsonValidationError, ResultsLevel,
    SchemaEvaluationDepthError, Validator,
};
use serde_json::Value;

// ---------------------------------------------------------------------------------------------------------------------
// Types and constants

/// The outcome of a call: `CJS_OK`, or why it failed (the details are in `cjs_last_error_message`).
pub type cjs_status = u32;
/// The call succeeded.
pub const CJS_OK: cjs_status = 0;
/// A NULL handle or pointer, or a value out of range.
pub const CJS_INVALID_ARGUMENT: cjs_status = 1;
/// Text that is not UTF-8 (`cjs_last_error_offset` is the offset of the first invalid byte).
pub const CJS_INVALID_UTF8: cjs_status = 2;
/// Text that is not JSON (`cjs_last_error_offset` is the offset at which it was found to be invalid).
pub const CJS_INVALID_JSON: cjs_status = 3;
/// The schema could not be compiled (an unresolvable reference, an invalid pattern).
pub const CJS_COMPILATION_FAILED: cjs_status = 4;
/// Evaluation recursed in place beyond the maximum depth (a schema that loops without consuming the instance).
pub const CJS_DEPTH_EXCEEDED: cjs_status = 5;
/// A bug in the library: it was caught, and the library remains usable, but the object passed may be left in an
/// unspecified state (free it).
pub const CJS_PANIC: cjs_status = 6;

/// A JSON Schema dialect.
pub type cjs_dialect = u32;
pub const CJS_DRAFT4: cjs_dialect = 4;
pub const CJS_DRAFT6: cjs_dialect = 6;
pub const CJS_DRAFT7: cjs_dialect = 7;
pub const CJS_DRAFT201909: cjs_dialect = 2019;
pub const CJS_DRAFT202012: cjs_dialect = 2020;

/// An option left to its default, or set on or off.
pub type cjs_tristate = u32;
pub const CJS_DEFAULT: cjs_tristate = 0;
pub const CJS_TRUE: cjs_tristate = 1;
pub const CJS_FALSE: cjs_tristate = 2;

/// How much a collector records.
pub type cjs_results_level = u32;
/// Failures only, without message text (the lowest overhead).
pub const CJS_BASIC: cjs_results_level = 0;
/// Failures only, with message text.
pub const CJS_DETAILED: cjs_results_level = 1;
/// Every evaluation, passing and failing, with message text, including annotations.
pub const CJS_VERBOSE: cjs_results_level = 2;

/// The header's version: compare with `cjs_version()`, the library's.
pub const CJS_VERSION_MAJOR: u32 = 0;
pub const CJS_VERSION_MINOR: u32 = 1;
pub const CJS_VERSION_PATCH: u32 = 0;

/// A UTF-8 string the library owns: `len` bytes from `ptr`, not NUL-terminated. Each function returning one says how
/// long it stays valid.
#[repr(C)]
#[derive(Clone, Copy)]
pub struct cjs_str {
    pub ptr: *const c_char,
    pub len: usize,
}

impl cjs_str {
    fn of(s: &str) -> cjs_str {
        cjs_str { ptr: s.as_ptr().cast(), len: s.len() }
    }

    const EMPTY: cjs_str = cjs_str { ptr: c"".as_ptr(), len: 0 };
}

/// Frees a callback's `user_data`.
pub type cjs_free_fn = Option<unsafe extern "C" fn(user_data: *mut c_void)>;

/// A format assertion: whether the string (or, for a number, its JSON text) is valid. It runs during validation, on
/// whichever thread validates, so it must be thread-safe. It must not unwind (throw) into the library.
pub type cjs_format_fn = Option<unsafe extern "C" fn(user_data: *mut c_void, value: *const c_char, len: usize) -> bool>;

/// A document resolver: for an absolute URI, the document's JSON text through `cjs_resolved_set_json`, or nothing
/// for an unknown document. It runs during compilation, on the compiling thread. A status other than `CJS_OK` fails
/// the compilation (with the message from `cjs_resolved_set_error`, if any). It must not unwind into the library.
pub type cjs_resolve_fn = Option<
    unsafe extern "C" fn(
        user_data: *mut c_void,
        uri: *const c_char,
        uri_len: usize,
        out: *mut cjs_resolved,
    ) -> cjs_status,
>;

/// Compile options (a builder: see the `cjs_options_` functions).
pub struct cjs_options {
    inner: CompileOptions,
    resolver: Option<Resolver>,
}

/// A compiled schema. Immutable: any number of threads may use one at once.
pub struct cjs_validator {
    inner: Validator,
}

/// Parsed JSON text, for an instance validated more than once. Immutable: may be shared between threads.
pub struct cjs_document {
    // Dropped before the text it may borrow.
    doc: JsonDocument<'static>,
    _text: Option<Box<str>>,
}

/// The results of an evaluation. Used by one thread at a time.
pub struct cjs_collector {
    level: ResultsLevel,
    inner: JsonSchemaResultsCollector,
    annotations: Option<String>,
}

/// A document resolver's answer for one URI (see `cjs_resolve_fn`).
pub struct cjs_resolved {
    json: Option<String>,
    error: Option<String>,
}

// ---------------------------------------------------------------------------------------------------------------------
// Errors

struct LastError {
    message: String,
    offset: usize,
}

thread_local! {
    static LAST_ERROR: RefCell<LastError> = const { RefCell::new(LastError { message: String::new(), offset: 0 }) };
}

struct Failure {
    status: cjs_status,
    message: String,
    offset: usize,
}

impl Failure {
    fn new(status: cjs_status, message: impl Into<String>) -> Failure {
        Failure { status, message: message.into(), offset: 0 }
    }

    fn argument(message: impl Into<String>) -> Failure {
        Failure::new(CJS_INVALID_ARGUMENT, message)
    }
}

fn set_last_error(message: String, offset: usize) {
    LAST_ERROR.with(|e| {
        let mut e = e.borrow_mut();
        e.message = message;
        e.offset = offset;
    });
}

fn panic_message(payload: &(dyn std::any::Any + Send)) -> String {
    let what = payload
        .downcast_ref::<&str>()
        .map(|s| s.to_string())
        .or_else(|| payload.downcast_ref::<String>().cloned())
        .unwrap_or_else(|| "unknown".into());
    format!("internal error (please report it): {what}")
}

/// Runs a call: a failure or a panic becomes its status, with the details stored for `cjs_last_error_message`.
fn guard(f: impl FnOnce() -> Result<(), Failure>) -> cjs_status {
    match catch_unwind(AssertUnwindSafe(f)) {
        Ok(Ok(())) => CJS_OK,
        Ok(Err(failure)) => {
            set_last_error(failure.message, failure.offset);
            failure.status
        }
        Err(payload) => {
            set_last_error(panic_message(&*payload), 0);
            CJS_PANIC
        }
    }
}

/// Runs a call returning a new handle: NULL on a panic.
fn guard_ptr<T>(f: impl FnOnce() -> *mut T) -> *mut T {
    catch_unwind(AssertUnwindSafe(f)).unwrap_or_else(|payload| {
        set_last_error(panic_message(&*payload), 0);
        std::ptr::null_mut()
    })
}

/// A string argument: `len` UTF-8 bytes at `ptr` (which may be NULL when `len` is 0).
unsafe fn text<'a>(ptr: *const c_char, len: usize, what: &str) -> Result<&'a str, Failure> {
    if len == 0 {
        return Ok("");
    }
    if ptr.is_null() {
        return Err(Failure::argument(format!("{what} is NULL with a length of {len}")));
    }
    // SAFETY: the caller passes `len` readable bytes at `ptr`.
    let bytes = unsafe { std::slice::from_raw_parts(ptr.cast::<u8>(), len) };
    std::str::from_utf8(bytes).map_err(|e| Failure {
        status: CJS_INVALID_UTF8,
        message: format!("{what} is not UTF-8 at byte {}", e.valid_up_to()),
        offset: e.valid_up_to(),
    })
}

unsafe fn handle<'a, T>(ptr: *const T, what: &str) -> Result<&'a T, Failure> {
    // SAFETY: a non-NULL handle is one this library created and the caller has not freed.
    unsafe { ptr.as_ref() }.ok_or_else(|| Failure::argument(format!("{what} is NULL")))
}

unsafe fn handle_mut<'a, T>(ptr: *mut T, what: &str) -> Result<&'a mut T, Failure> {
    // SAFETY: as `handle`, and the caller uses the object from one thread at a time.
    unsafe { ptr.as_mut() }.ok_or_else(|| Failure::argument(format!("{what} is NULL")))
}

unsafe fn out<'a, T>(ptr: *mut T, what: &str) -> Result<&'a mut T, Failure> {
    // SAFETY: a non-NULL out-parameter points to writable storage for a T.
    unsafe { ptr.as_mut() }.ok_or_else(|| Failure::argument(format!("{what} is NULL")))
}

fn invalid_json(e: corvus::JsonParseError) -> Failure {
    Failure { status: CJS_INVALID_JSON, message: format!("invalid JSON: {e}"), offset: e.offset() }
}

fn depth_exceeded(e: SchemaEvaluationDepthError) -> Failure {
    Failure::new(CJS_DEPTH_EXCEEDED, e.to_string())
}

fn validation_error(e: JsonValidationError) -> Failure {
    match e {
        JsonValidationError::InvalidJson(e) => invalid_json(e),
        JsonValidationError::DepthExceeded(e) => depth_exceeded(e),
    }
}

/// serde_json reports a line and column: the byte offset of that position.
fn serde_offset(text: &str, e: &serde_json::Error) -> usize {
    let line_start: usize = text.split_inclusive('\n').take(e.line().saturating_sub(1)).map(str::len).sum();
    (line_start + e.column().saturating_sub(1)).min(text.len())
}

fn parse_value(text: &str, what: &str) -> Result<Value, Failure> {
    serde_json::from_str(text).map_err(|e| Failure {
        status: CJS_INVALID_JSON,
        message: format!("{what} is not valid JSON: {e}"),
        offset: serde_offset(text, &e),
    })
}

/// The message of the most recent failure on this thread (empty if none). Valid until the thread's next failure.
#[unsafe(no_mangle)]
pub extern "C" fn cjs_last_error_message() -> cjs_str {
    // The view outlives the borrow: the string is only replaced by the thread's next failure.
    LAST_ERROR.with(|e| cjs_str::of(&e.borrow().message))
}

/// The byte offset that goes with the most recent failure on this thread: where invalid UTF-8 or invalid JSON was
/// found (0 otherwise).
#[unsafe(no_mangle)]
pub extern "C" fn cjs_last_error_offset() -> usize {
    LAST_ERROR.with(|e| e.borrow().offset)
}

// ---------------------------------------------------------------------------------------------------------------------
// Version

/// The library's version: `(major << 16) | (minor << 8) | patch`.
#[unsafe(no_mangle)]
pub extern "C" fn cjs_version() -> u32 {
    (CJS_VERSION_MAJOR << 16) | (CJS_VERSION_MINOR << 8) | CJS_VERSION_PATCH
}

/// The library's version as text, such as "0.1.0" (static).
#[unsafe(no_mangle)]
pub extern "C" fn cjs_version_string() -> cjs_str {
    cjs_str::of(env!("CARGO_PKG_VERSION"))
}

// ---------------------------------------------------------------------------------------------------------------------
// Callbacks

/// A callback's user data, freed (by its free function, if any) when the last object using it is dropped.
struct UserData {
    ptr: *mut c_void,
    free: cjs_free_fn,
}

// SAFETY: the C API requires format callbacks (and their user data) to be thread-safe; the free function runs once.
unsafe impl Send for UserData {}
unsafe impl Sync for UserData {}

impl Drop for UserData {
    fn drop(&mut self) {
        if let Some(free) = self.free {
            // SAFETY: the caller's free function for the user data it registered.
            unsafe { free(self.ptr) }
        }
    }
}

#[derive(Clone)]
struct Resolver {
    f: unsafe extern "C" fn(*mut c_void, *const c_char, usize, *mut cjs_resolved) -> cjs_status,
    user: Arc<UserData>,
}

/// Gives a resolver's answer: the document's JSON text (copied). Call it at most once per resolution.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_resolved_set_json(out: *mut cjs_resolved, json: *const c_char, len: usize) -> cjs_status {
    guard(|| {
        // SAFETY: the resolution object the library passed to the resolver.
        let out = unsafe { handle_mut(out, "out") }?;
        out.json = Some(unsafe { text(json, len, "json") }?.to_owned());
        Ok(())
    })
}

/// Gives the reason a resolver fails (copied), reported with `CJS_COMPILATION_FAILED`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_resolved_set_error(
    out: *mut cjs_resolved,
    message: *const c_char,
    len: usize,
) -> cjs_status {
    guard(|| {
        let out = unsafe { handle_mut(out, "out") }?;
        out.error = Some(unsafe { text(message, len, "message") }?.to_owned());
        Ok(())
    })
}

// ---------------------------------------------------------------------------------------------------------------------
// Options

/// New options with the defaults: 2020-12 for schemas without `$schema`, `format` as the vocabularies say, the
/// standard metaschemas only. NULL only on an internal error.
#[unsafe(no_mangle)]
pub extern "C" fn cjs_options_new() -> *mut cjs_options {
    guard_ptr(|| Box::into_raw(Box::new(cjs_options { inner: CompileOptions::default(), resolver: None })))
}

/// Frees options (NULL is ignored). Validators compiled with them are unaffected.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_options_free(options: *mut cjs_options) {
    if !options.is_null() {
        // SAFETY: options from cjs_options_new, not yet freed.
        let _ = catch_unwind(AssertUnwindSafe(|| drop(unsafe { Box::from_raw(options) })));
    }
}

unsafe fn with_options(
    options: *mut cjs_options,
    f: impl FnOnce(&mut cjs_options) -> Result<(), Failure>,
) -> cjs_status {
    guard(|| f(unsafe { handle_mut(options, "options") }?))
}

/// The dialect of schemas without `$schema` (`CJS_DRAFT4` to `CJS_DRAFT202012`).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_options_set_default_dialect(
    options: *mut cjs_options,
    dialect: cjs_dialect,
) -> cjs_status {
    unsafe {
        with_options(options, |o| {
            o.inner.default_dialect = match dialect {
                CJS_DRAFT4 => Dialect::Draft4,
                CJS_DRAFT6 => Dialect::Draft6,
                CJS_DRAFT7 => Dialect::Draft7,
                CJS_DRAFT201909 => Dialect::Draft201909,
                CJS_DRAFT202012 => Dialect::Draft202012,
                d => return Err(Failure::argument(format!("{d} is not a cjs_dialect"))),
            };
            Ok(())
        })
    }
}

/// Whether `format` is asserted: `CJS_DEFAULT` follows the vocabularies (the 2020-12 format-assertion vocabulary
/// asserts, everything else annotates).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_options_set_assert_format(options: *mut cjs_options, mode: cjs_tristate) -> cjs_status {
    unsafe {
        with_options(options, |o| {
            o.inner.assert_format = match mode {
                CJS_DEFAULT => None,
                CJS_TRUE => Some(true),
                CJS_FALSE => Some(false),
                t => return Err(Failure::argument(format!("{t} is not a cjs_tristate"))),
            };
            Ok(())
        })
    }
}

/// With `format` assertion left to its default, assert it in draft 4 to 7 too.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_options_set_assert_format_in_legacy_drafts(
    options: *mut cjs_options,
    enabled: bool,
) -> cjs_status {
    unsafe {
        with_options(options, |o| {
            o.inner.assert_format_in_legacy_drafts = enabled;
            Ok(())
        })
    }
}

/// Assert `contentEncoding` and `contentMediaType` in draft 7, the only draft that asserts them (default true).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_options_set_assert_content(options: *mut cjs_options, enabled: bool) -> cjs_status {
    unsafe {
        with_options(options, |o| {
            o.inner.assert_content = enabled;
            Ok(())
        })
    }
}

/// The base URI of the root schema document (an empty string removes it).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_options_set_base_uri(
    options: *mut cjs_options,
    uri: *const c_char,
    len: usize,
) -> cjs_status {
    unsafe {
        with_options(options, |o| {
            let uri = text(uri, len, "uri")?;
            o.inner.base_uri = (!uri.is_empty()).then(|| uri.to_owned());
            Ok(())
        })
    }
}

/// A reference, relative to the root, to evaluate from, such as "#/$defs/item" (an empty string: the root).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_options_set_entry_point(
    options: *mut cjs_options,
    reference: *const c_char,
    len: usize,
) -> cjs_status {
    unsafe {
        with_options(options, |o| {
            let reference = text(reference, len, "reference")?;
            o.inner.entry_point = (!reference.is_empty()).then(|| reference.to_owned());
            Ok(())
        })
    }
}

/// The maximum depth of in-place recursion on a cycle before evaluation is abandoned (default 128).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_options_set_max_depth(options: *mut cjs_options, depth: u32) -> cjs_status {
    unsafe {
        with_options(options, |o| {
            o.inner.max_depth = depth;
            Ok(())
        })
    }
}

/// A custom format assertion, taking precedence over the built-in one of that name. `free_user_data` (may be NULL)
/// runs when the options and every validator compiled with them have been freed.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_options_add_format(
    options: *mut cjs_options,
    name: *const c_char,
    name_len: usize,
    format: cjs_format_fn,
    user_data: *mut c_void,
    free_user_data: cjs_free_fn,
) -> cjs_status {
    // The user data is owned (and freed) from here on, even if the call fails.
    let user = Arc::new(UserData { ptr: user_data, free: free_user_data });
    unsafe {
        with_options(options, |o| {
            let name = text(name, name_len, "name")?;
            let Some(f) = format else { return Err(Failure::argument("format is NULL")) };
            o.inner.formats.insert(
                name.to_owned(),
                Arc::new(move |value: &str| {
                    // SAFETY: the caller's callback, with its user data and the value's bytes (the closure is written
                    // inside the call's unsafe block).
                    f(user.ptr, value.as_ptr().cast(), value.len())
                }),
            );
            Ok(())
        })
    }
}

/// The resolver for documents other than the standard metaschemas (replacing any earlier one). `free_user_data` (may
/// be NULL) runs when the options and every validator compiled with them have been freed.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_options_set_resolver(
    options: *mut cjs_options,
    resolve: cjs_resolve_fn,
    user_data: *mut c_void,
    free_user_data: cjs_free_fn,
) -> cjs_status {
    let user = Arc::new(UserData { ptr: user_data, free: free_user_data });
    unsafe {
        with_options(options, |o| {
            let Some(f) = resolve else { return Err(Failure::argument("resolve is NULL")) };
            o.resolver = Some(Resolver { f, user });
            Ok(())
        })
    }
}

/// The options for one compilation: the caller's resolver wrapped to keep the first failure it reports.
fn compile_options(options: Option<&cjs_options>) -> (CompileOptions, Arc<Mutex<Option<String>>>) {
    let failure: Arc<Mutex<Option<String>>> = Arc::default();
    let Some(o) = options else { return (CompileOptions::default(), failure) };
    let mut inner = o.inner.clone();
    if let Some(r) = o.resolver.clone() {
        let failure = failure.clone();
        inner.resolve_document = Some(Arc::new(move |uri: &str| {
            let fail = |message: String| {
                failure.lock().unwrap().get_or_insert(message);
                None
            };
            let mut answer = cjs_resolved { json: None, error: None };
            // SAFETY: the caller's resolver, with its user data, the URI's bytes and a resolution object.
            let status = unsafe { (r.f)(r.user.ptr, uri.as_ptr().cast(), uri.len(), &mut answer) };
            if status != CJS_OK {
                let why = answer.error.unwrap_or_else(|| format!("status {status}"));
                return fail(format!("the document resolver failed for {uri}: {why}"));
            }
            let json = answer.json?;
            match serde_json::from_str(&json) {
                Ok(v) => Some(v),
                Err(e) => fail(format!("the document resolver returned invalid JSON for {uri}: {e}")),
            }
        }));
    }
    (inner, failure)
}

fn compile_failure(e: corvus::SchemaCompilationError, resolver: &Mutex<Option<String>>) -> Failure {
    let message = match resolver.lock().unwrap().take() {
        Some(r) => format!("{} ({r})", e.message()),
        None => e.message().to_owned(),
    };
    Failure::new(CJS_COMPILATION_FAILED, message)
}

// ---------------------------------------------------------------------------------------------------------------------
// Validators

/// Compiles a schema from its JSON text, with the options (NULL: the defaults). On success `*out` is a new validator,
/// freed with `cjs_validator_free`; otherwise it is NULL.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_compile(
    schema: *const c_char,
    len: usize,
    options: *const cjs_options,
    out: *mut *mut cjs_validator,
) -> cjs_status {
    guard(|| {
        let out = unsafe { self::out(out, "out") }?;
        *out = std::ptr::null_mut();
        let schema = parse_value(unsafe { text(schema, len, "schema") }?, "the schema")?;
        let (options, resolver) = compile_options(unsafe { options.as_ref() });
        let v = corvus::compile_with(&schema, &options).map_err(|e| compile_failure(e, &resolver))?;
        *out = Box::into_raw(Box::new(cjs_validator { inner: v }));
        Ok(())
    })
}

/// Compiles the schema document at an absolute URI, fetched through the options' resolver (or one of the standard
/// metaschemas). On success `*out` is a new validator; otherwise it is NULL.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_compile_uri(
    uri: *const c_char,
    len: usize,
    options: *const cjs_options,
    out: *mut *mut cjs_validator,
) -> cjs_status {
    guard(|| {
        let out = unsafe { self::out(out, "out") }?;
        *out = std::ptr::null_mut();
        let uri = unsafe { text(uri, len, "uri") }?;
        let (options, resolver) = compile_options(unsafe { options.as_ref() });
        let v = corvus::compile_from_uri(uri, &options).map_err(|e| compile_failure(e, &resolver))?;
        *out = Box::into_raw(Box::new(cjs_validator { inner: v }));
        Ok(())
    })
}

/// A new handle to the same compiled schema (cheap). NULL for a NULL validator.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_validator_clone(validator: *const cjs_validator) -> *mut cjs_validator {
    guard_ptr(|| match unsafe { validator.as_ref() } {
        Some(v) => Box::into_raw(Box::new(cjs_validator { inner: v.inner.clone() })),
        None => std::ptr::null_mut(),
    })
}

/// Frees a validator (NULL is ignored).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_validator_free(validator: *mut cjs_validator) {
    if !validator.is_null() {
        // SAFETY: a validator from this library, not yet freed.
        let _ = catch_unwind(AssertUnwindSafe(|| drop(unsafe { Box::from_raw(validator) })));
    }
}

/// Whether the JSON text is valid against the schema: `*valid` on `CJS_OK`. The text is parsed into per-thread
/// buffers and evaluated in place, allocating nothing in the steady state.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_validator_validate_json(
    validator: *const cjs_validator,
    json: *const c_char,
    len: usize,
    valid: *mut bool,
) -> cjs_status {
    guard(|| {
        let v = unsafe { handle(validator, "validator") }?;
        let valid = unsafe { out(valid, "valid") }?;
        *valid = v.inner.validate_json(unsafe { text(json, len, "json") }?).map_err(validation_error)?;
        Ok(())
    })
}

/// Whether the parsed document is valid against the schema: `*valid` on `CJS_OK`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_validator_validate_document(
    validator: *const cjs_validator,
    document: *const cjs_document,
    valid: *mut bool,
) -> cjs_status {
    guard(|| {
        let v = unsafe { handle(validator, "validator") }?;
        let d = unsafe { handle(document, "document") }?;
        let valid = unsafe { out(valid, "valid") }?;
        *valid = v.inner.validate_instance(d.doc.root()).map_err(depth_exceeded)?;
        Ok(())
    })
}

/// Evaluates the JSON text, replacing the collector's results with this evaluation's (every keyword is evaluated and
/// reported at the collector's level); `*valid` on `CJS_OK`.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_validator_evaluate_json(
    validator: *const cjs_validator,
    json: *const c_char,
    len: usize,
    collector: *mut cjs_collector,
    valid: *mut bool,
) -> cjs_status {
    guard(|| {
        let v = unsafe { handle(validator, "validator") }?;
        let c = unsafe { handle_mut(collector, "collector") }?;
        let valid = unsafe { out(valid, "valid") }?;
        let json = unsafe { text(json, len, "json") }?;
        c.reset();
        *valid = v.inner.evaluate_json(json, &mut c.inner).map_err(validation_error)?;
        Ok(())
    })
}

/// `cjs_validator_evaluate_json` for a parsed document.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_validator_evaluate_document(
    validator: *const cjs_validator,
    document: *const cjs_document,
    collector: *mut cjs_collector,
    valid: *mut bool,
) -> cjs_status {
    guard(|| {
        let v = unsafe { handle(validator, "validator") }?;
        let d = unsafe { handle(document, "document") }?;
        let c = unsafe { handle_mut(collector, "collector") }?;
        let valid = unsafe { out(valid, "valid") }?;
        c.reset();
        *valid = v.inner.evaluate_instance(d.doc.root(), &mut c.inner).map_err(depth_exceeded)?;
        Ok(())
    })
}

// ---------------------------------------------------------------------------------------------------------------------
// Documents

/// Parses JSON text into a new document (`*out`, freed with `cjs_document_free`; NULL on failure). The text is copied.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_document_parse(
    json: *const c_char,
    len: usize,
    out: *mut *mut cjs_document,
) -> cjs_status {
    guard(|| {
        let out = unsafe { self::out(out, "out") }?;
        *out = std::ptr::null_mut();
        let owned: Box<str> = unsafe { text(json, len, "json") }?.into();
        // SAFETY: the document borrows the boxed text, which moves with it (a box's contents stay put) and is dropped
        // after it (field order).
        let borrowed: &'static str = unsafe { &*std::ptr::from_ref::<str>(&owned) };
        let doc = JsonDocument::parse(borrowed).map_err(invalid_json)?;
        *out = Box::into_raw(Box::new(cjs_document { doc, _text: Some(owned) }));
        Ok(())
    })
}

/// `cjs_document_parse` without copying the text: the caller keeps the text unchanged until the document is freed.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_document_parse_borrowed(
    json: *const c_char,
    len: usize,
    out: *mut *mut cjs_document,
) -> cjs_status {
    guard(|| {
        let out = unsafe { self::out(out, "out") }?;
        *out = std::ptr::null_mut();
        // SAFETY: the caller guarantees the text outlives the document.
        let borrowed: &'static str = unsafe { text(json, len, "json") }?;
        let doc = JsonDocument::parse(borrowed).map_err(invalid_json)?;
        *out = Box::into_raw(Box::new(cjs_document { doc, _text: None }));
        Ok(())
    })
}

/// Frees a document (NULL is ignored).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_document_free(document: *mut cjs_document) {
    if !document.is_null() {
        // SAFETY: a document from this library, not yet freed.
        let _ = catch_unwind(AssertUnwindSafe(|| drop(unsafe { Box::from_raw(document) })));
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Collectors

impl cjs_collector {
    fn reset(&mut self) {
        self.inner = JsonSchemaResultsCollector::new(self.level);
        self.annotations = None;
    }

    fn row(&self, i: usize) -> Option<&corvus::SchemaResult> {
        self.inner.results().get(i)
    }
}

/// A new collector recording at the level (`CJS_BASIC`, `CJS_DETAILED` or `CJS_VERBOSE`); NULL for any other value.
#[unsafe(no_mangle)]
pub extern "C" fn cjs_collector_new(level: cjs_results_level) -> *mut cjs_collector {
    let level = match level {
        CJS_BASIC => ResultsLevel::Basic,
        CJS_DETAILED => ResultsLevel::Detailed,
        CJS_VERBOSE => ResultsLevel::Verbose,
        l => {
            set_last_error(format!("{l} is not a cjs_results_level"), 0);
            return std::ptr::null_mut();
        }
    };
    guard_ptr(|| {
        Box::into_raw(Box::new(cjs_collector {
            level,
            inner: JsonSchemaResultsCollector::new(level),
            annotations: None,
        }))
    })
}

/// Frees a collector (NULL is ignored).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_collector_free(collector: *mut cjs_collector) {
    if !collector.is_null() {
        // SAFETY: a collector from this library, not yet freed.
        let _ = catch_unwind(AssertUnwindSafe(|| drop(unsafe { Box::from_raw(collector) })));
    }
}

/// Removes the collector's results.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_collector_clear(collector: *mut cjs_collector) {
    if let Some(c) = unsafe { collector.as_mut() } {
        let _ = catch_unwind(AssertUnwindSafe(|| c.reset()));
    }
}

/// The number of result rows (0 for NULL).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_collector_count(collector: *const cjs_collector) -> usize {
    unsafe { collector.as_ref() }.map_or(0, |c| c.inner.results().len())
}

/// Whether row `i` is a match (false for a row out of range). The row functions' views are valid until the collector
/// is next evaluated into, cleared or freed.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_collector_is_match(collector: *const cjs_collector, i: usize) -> bool {
    unsafe { collector.as_ref() }.and_then(|c| c.row(i)).is_some_and(|r| r.is_match)
}

unsafe fn row_str(collector: *const cjs_collector, i: usize, field: fn(&corvus::SchemaResult) -> &str) -> cjs_str {
    unsafe { collector.as_ref() }.and_then(|c| c.row(i)).map_or(cjs_str::EMPTY, |r| cjs_str::of(field(r)))
}

/// Row `i`'s message (empty when the level records none; raw JSON for an annotation row).
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_collector_message(collector: *const cjs_collector, i: usize) -> cjs_str {
    unsafe { row_str(collector, i, |r| &r.message) }
}

/// Row `i`'s evaluation path, the keywords from the root schema (such as "/properties/name/type").
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_collector_evaluation_location(collector: *const cjs_collector, i: usize) -> cjs_str {
    unsafe { row_str(collector, i, |r| &r.evaluation_location) }
}

/// Row `i`'s schema location: the JSON pointer of the evaluated schema or keyword within its document.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_collector_schema_location(collector: *const cjs_collector, i: usize) -> cjs_str {
    unsafe { row_str(collector, i, |r| &r.schema_evaluation_location) }
}

/// Row `i`'s instance location: the JSON pointer of the evaluated value (such as "/name").
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_collector_instance_location(collector: *const cjs_collector, i: usize) -> cjs_str {
    unsafe { row_str(collector, i, |r| &r.document_evaluation_location) }
}

/// The annotations of a verbose evaluation as JSON text, grouped by instance location, keyword and schema location:
/// `{"/name": {"title": {"#/properties/name": "Name"}}}`. Valid until the collector is next evaluated into, cleared
/// or freed.
#[unsafe(no_mangle)]
pub unsafe extern "C" fn cjs_collector_annotations_json(
    collector: *mut cjs_collector,
    out: *mut cjs_str,
) -> cjs_status {
    guard(|| {
        let c = unsafe { handle_mut(collector, "collector") }?;
        let out = unsafe { self::out(out, "out") }?;
        if c.annotations.is_none() {
            let grouped = corvus::collect_annotations(&c.inner);
            c.annotations = Some(serde_json::to_string(&grouped).map_err(|e| Failure::new(CJS_PANIC, e.to_string()))?);
        }
        *out = cjs_str::of(c.annotations.as_deref().unwrap_or_default());
        Ok(())
    })
}

#[cfg(test)]
mod tests {
    #[test]
    fn version_constants_match_the_package() {
        assert_eq!(super::CJS_VERSION_MAJOR.to_string(), env!("CARGO_PKG_VERSION_MAJOR"));
        assert_eq!(super::CJS_VERSION_MINOR.to_string(), env!("CARGO_PKG_VERSION_MINOR"));
        assert_eq!(super::CJS_VERSION_PATCH.to_string(), env!("CARGO_PKG_VERSION_PATCH"));
    }
}
