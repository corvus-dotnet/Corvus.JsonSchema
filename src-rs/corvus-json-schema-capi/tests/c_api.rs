//! The C library, through its C functions only, against the crate it wraps: the JSON-Schema-Test-Suite (every
//! verdict, and every result row at each level, as the crate gives them), errors, callbacks, documents, collectors and
//! threads.
//!
//! Set `JSON_SCHEMA_TEST_SUITE` to use a different checkout of the suite.

use std::ffi::{c_char, c_void};
use std::path::{Path, PathBuf};
use std::sync::Arc;
use std::sync::atomic::{AtomicUsize, Ordering};

use corvus_json_schema::*;
use serde_json::Value;

// ---------------------------------------------------------------------------------------------------------------------
// Helpers

fn string(s: cjs_str) -> String {
    if s.len == 0 {
        return String::new();
    }
    let bytes = unsafe { std::slice::from_raw_parts(s.ptr.cast::<u8>(), s.len) };
    String::from_utf8(bytes.to_vec()).unwrap()
}

fn last_error() -> (String, usize) {
    (string(cjs_last_error_message()), cjs_last_error_offset())
}

fn ptr(s: &str) -> *const c_char {
    s.as_ptr().cast()
}

struct Options(*mut cjs_options);

impl Options {
    fn new() -> Options {
        let o = cjs_options_new();
        assert!(!o.is_null());
        Options(o)
    }
}

impl Drop for Options {
    fn drop(&mut self) {
        unsafe { cjs_options_free(self.0) }
    }
}

struct Compiled(*mut cjs_validator);

impl Drop for Compiled {
    fn drop(&mut self) {
        unsafe { cjs_validator_free(self.0) }
    }
}

// SAFETY: validators are immutable and thread-safe.
unsafe impl Send for Compiled {}
unsafe impl Sync for Compiled {}

struct Collector(*mut cjs_collector);

impl Collector {
    fn new(level: cjs_results_level) -> Collector {
        let c = cjs_collector_new(level);
        assert!(!c.is_null());
        Collector(c)
    }

    fn rows(&self) -> Vec<(bool, String, String, String, String)> {
        let n = unsafe { cjs_collector_count(self.0) };
        (0..n)
            .map(|i| unsafe {
                (
                    cjs_collector_is_match(self.0, i),
                    string(cjs_collector_message(self.0, i)),
                    string(cjs_collector_evaluation_location(self.0, i)),
                    string(cjs_collector_schema_location(self.0, i)),
                    string(cjs_collector_instance_location(self.0, i)),
                )
            })
            .collect()
    }
}

impl Drop for Collector {
    fn drop(&mut self) {
        unsafe { cjs_collector_free(self.0) }
    }
}

fn compile(schema: &str, options: Option<&Options>) -> Result<Compiled, (cjs_status, String, usize)> {
    let mut v = std::ptr::null_mut();
    let status = unsafe { cjs_compile(ptr(schema), schema.len(), options.map_or(std::ptr::null(), |o| o.0), &mut v) };
    if status == CJS_OK {
        assert!(!v.is_null());
        Ok(Compiled(v))
    } else {
        assert!(v.is_null());
        let (m, o) = last_error();
        Err((status, m, o))
    }
}

fn validate(v: &Compiled, json: &str) -> Result<bool, (cjs_status, String, usize)> {
    let mut valid = false;
    match unsafe { cjs_validator_validate_json(v.0, ptr(json), json.len(), &mut valid) } {
        CJS_OK => Ok(valid),
        status => {
            let (m, o) = last_error();
            Err((status, m, o))
        }
    }
}

fn evaluate(v: &Compiled, json: &str, c: &Collector) -> Result<bool, cjs_status> {
    let mut valid = false;
    match unsafe { cjs_validator_evaluate_json(v.0, ptr(json), json.len(), c.0, &mut valid) } {
        CJS_OK => Ok(valid),
        status => Err(status),
    }
}

/// The crate's own rows for the same evaluation.
fn crate_rows(
    v: &corvus::Validator,
    json: &str,
    level: corvus::ResultsLevel,
) -> Vec<(bool, String, String, String, String)> {
    let mut c = corvus::JsonSchemaResultsCollector::new(level);
    v.evaluate_json(json, &mut c).unwrap();
    c.results()
        .iter()
        .map(|r| {
            (
                r.is_match,
                r.message.clone(),
                r.evaluation_location.clone(),
                r.schema_evaluation_location.clone(),
                r.document_evaluation_location.clone(),
            )
        })
        .collect()
}

// ---------------------------------------------------------------------------------------------------------------------
// The JSON-Schema-Test-Suite

const DRAFTS: [(&str, cjs_dialect, corvus::Dialect); 5] = [
    ("draft4", CJS_DRAFT4, corvus::Dialect::Draft4),
    ("draft6", CJS_DRAFT6, corvus::Dialect::Draft6),
    ("draft7", CJS_DRAFT7, corvus::Dialect::Draft7),
    ("draft2019-09", CJS_DRAFT201909, corvus::Dialect::Draft201909),
    ("draft2020-12", CJS_DRAFT202012, corvus::Dialect::Draft202012),
];

// As the crate's runner: serde_json cannot tell 1.0 from 1.
const EXCLUDED_FILES: [&str; 1] = ["draft4/optional/zeroTerminatedFloats.json"];

fn suite_root() -> PathBuf {
    std::env::var_os("JSON_SCHEMA_TEST_SUITE")
        .map(PathBuf::from)
        .unwrap_or_else(|| Path::new(env!("CARGO_MANIFEST_DIR")).join("../../JSON-Schema-Test-Suite"))
}

fn files(dir: &Path) -> Vec<PathBuf> {
    let Ok(entries) = std::fs::read_dir(dir) else { return Vec::new() };
    let mut out: Vec<PathBuf> = entries
        .filter_map(|e| e.ok().map(|e| e.path()))
        .filter(|p| p.extension().is_some_and(|x| x == "json"))
        .collect();
    out.sort();
    out
}

/// The suite's remotes (`http://localhost:1234/...`), served by a resolver callback over the remotes directory.
unsafe extern "C" fn resolve_remote(
    user: *mut c_void,
    uri: *const c_char,
    len: usize,
    out: *mut cjs_resolved,
) -> cjs_status {
    let remotes = unsafe { &*user.cast::<PathBuf>() };
    let uri = std::str::from_utf8(unsafe { std::slice::from_raw_parts(uri.cast::<u8>(), len) }).unwrap();
    let Some(rest) = uri.strip_prefix("http://localhost:1234/") else { return CJS_OK };
    let Ok(text) = std::fs::read_to_string(remotes.join(rest)) else { return CJS_OK };
    unsafe { cjs_resolved_set_json(out, ptr(&text), text.len()) }
}

unsafe extern "C" fn free_path(user: *mut c_void) {
    drop(unsafe { Box::from_raw(user.cast::<PathBuf>()) });
}

#[test]
fn json_schema_test_suite_through_the_c_api() {
    let root = suite_root();
    let tests = root.join("tests");
    if !tests.exists() {
        eprintln!("JSON-Schema-Test-Suite not found at {}; skipping", root.display());
        return;
    }
    let remotes = root.join("remotes");
    let crate_resolver: corvus::DocumentResolver = {
        let remotes = remotes.clone();
        Arc::new(move |uri: &str| {
            let file = remotes.join(uri.strip_prefix("http://localhost:1234/")?);
            serde_json::from_str(&std::fs::read_to_string(file).ok()?).ok()
        })
    };
    let (mut total, mut failures) = (0, Vec::new());
    for (draft, cjs_dialect, dialect) in DRAFTS {
        let dir = tests.join(draft);
        let runs = files(&dir)
            .into_iter()
            .map(|f| (f, false))
            .chain(files(&dir.join("optional")).into_iter().map(|f| (f, false)))
            .chain(files(&dir.join("optional").join("format")).into_iter().map(|f| (f, true)));
        for (file, assert_format) in runs {
            let label = file.strip_prefix(&tests).unwrap().to_string_lossy().replace('\\', "/");
            if EXCLUDED_FILES.contains(&label.as_str()) {
                continue;
            }
            let groups: Value = serde_json::from_str(&std::fs::read_to_string(&file).unwrap()).unwrap();
            for group in groups.as_array().unwrap() {
                let options = Options::new();
                let user = Box::into_raw(Box::new(remotes.clone())).cast::<c_void>();
                unsafe {
                    assert_eq!(cjs_options_set_default_dialect(options.0, cjs_dialect), CJS_OK);
                    let mode = if assert_format { CJS_TRUE } else { CJS_DEFAULT };
                    assert_eq!(cjs_options_set_assert_format(options.0, mode), CJS_OK);
                    assert_eq!(
                        cjs_options_set_resolver(options.0, Some(resolve_remote), user, Some(free_path)),
                        CJS_OK
                    );
                }
                let crate_options = corvus::CompileOptions {
                    default_dialect: dialect,
                    assert_format: assert_format.then_some(true),
                    resolve_document: Some(crate_resolver.clone()),
                    ..corvus::CompileOptions::default()
                };
                let schema_text = serde_json::to_string(&group["schema"]).unwrap();
                let description = group["description"].as_str().unwrap();
                let (v, reference) = match (
                    compile(&schema_text, Some(&options)),
                    corvus::compile_with(&group["schema"], &crate_options),
                ) {
                    (Ok(v), Ok(r)) => (v, r),
                    (c, r) => {
                        failures.push(format!("{label} [{description}]: compile {:?} vs {:?}", c.err(), r.err()));
                        continue;
                    }
                };
                for test in group["tests"].as_array().unwrap() {
                    total += 1;
                    let data = serde_json::to_string(&test["data"]).unwrap();
                    let expected = test["valid"].as_bool().unwrap();
                    let what = format!("{label} [{description}] {}", test["description"].as_str().unwrap());
                    let leap_second = assert_format && what.to_lowercase().contains("leap second");
                    match validate(&v, &data) {
                        Ok(valid) if valid == expected || leap_second => {}
                        other => failures.push(format!("{what}: expected {expected}, got {other:?}")),
                    }
                    for (level, crate_level) in [
                        (CJS_BASIC, corvus::ResultsLevel::Basic),
                        (CJS_DETAILED, corvus::ResultsLevel::Detailed),
                        (CJS_VERBOSE, corvus::ResultsLevel::Verbose),
                    ] {
                        let c = Collector::new(level);
                        let valid = evaluate(&v, &data, &c);
                        if valid != Ok(reference.validate_json(&data).unwrap()) {
                            failures.push(format!("{what}: level {level} returned {valid:?}"));
                        } else if c.rows() != crate_rows(&reference, &data, crate_level) {
                            failures.push(format!("{what}: level {level} rows differ from the crate's"));
                        }
                    }
                }
            }
        }
    }
    assert!(total > 7000, "only {total} cases ran");
    assert!(failures.is_empty(), "{} of {total} failed:\n{}", failures.len(), failures.join("\n"));
    println!("{total} cases through the C API, every verdict and result row as the crate's");
}

// ---------------------------------------------------------------------------------------------------------------------
// Errors

#[test]
fn errors_carry_a_status_message_and_offset() {
    let v = compile(r#"{"type": "array", "items": {"type": "integer"}}"#, None).unwrap();
    assert_eq!(validate(&v, "[1, 2]"), Ok(true));
    assert_eq!(validate(&v, "[1, \"2\"]"), Ok(false));

    let (status, message, offset) = validate(&v, "[1, 2").unwrap_err();
    assert_eq!((status, offset), (CJS_INVALID_JSON, 5));
    assert!(message.contains("invalid JSON"), "{message}");

    let bad = b"[\"a\xff\"]";
    let mut valid = false;
    let status = unsafe { cjs_validator_validate_json(v.0, bad.as_ptr().cast(), bad.len(), &mut valid) };
    assert_eq!((status, cjs_last_error_offset()), (CJS_INVALID_UTF8, 3));

    let status = unsafe { cjs_validator_validate_json(std::ptr::null(), ptr("1"), 1, &mut valid) };
    assert_eq!(status, CJS_INVALID_ARGUMENT);
    assert!(last_error().0.contains("validator is NULL"));
    let status = unsafe { cjs_validator_validate_json(v.0, ptr("1"), 1, std::ptr::null_mut()) };
    assert_eq!(status, CJS_INVALID_ARGUMENT);
    // An empty string with a NULL pointer is the empty string (not JSON).
    let status = unsafe { cjs_validator_validate_json(v.0, std::ptr::null(), 0, &mut valid) };
    assert_eq!(status, CJS_INVALID_JSON);

    // A schema that is not JSON: the offset is where serde_json stopped.
    let (status, _, offset) = compile("{\n  \"type\": }", None).err().unwrap();
    assert_eq!((status, offset), (CJS_INVALID_JSON, 12));
    let (status, message, _) = compile(r#"{"$ref": "http://example.com/missing.json"}"#, None).err().unwrap();
    assert_eq!(status, CJS_COMPILATION_FAILED);
    assert!(message.contains("missing.json"), "{message}");

    // A small depth: a debug build's frames at the default depth overflow a test thread's stack.
    let shallow = Options::new();
    assert_eq!(unsafe { cjs_options_set_max_depth(shallow.0, 16) }, CJS_OK);
    let looping = compile(r##"{"$defs": {"a": {"$ref": "#/$defs/a"}}, "$ref": "#/$defs/a"}"##, Some(&shallow)).unwrap();
    assert_eq!(validate(&looping, "1").unwrap_err().0, CJS_DEPTH_EXCEEDED);

    let options = Options::new();
    assert_eq!(unsafe { cjs_options_set_default_dialect(options.0, 5) }, CJS_INVALID_ARGUMENT);
    assert_eq!(unsafe { cjs_options_set_assert_format(options.0, 3) }, CJS_INVALID_ARGUMENT);
    assert!(cjs_collector_new(9).is_null());
    assert!(last_error().0.contains("cjs_results_level"));

    // Freeing NULL is allowed.
    unsafe {
        cjs_validator_free(std::ptr::null_mut());
        cjs_options_free(std::ptr::null_mut());
        cjs_document_free(std::ptr::null_mut());
        cjs_collector_free(std::ptr::null_mut());
    }
}

#[test]
fn options_apply() {
    let options = Options::new();
    let entry = "#/$defs/item";
    unsafe {
        assert_eq!(cjs_options_set_default_dialect(options.0, CJS_DRAFT7), CJS_OK);
        assert_eq!(cjs_options_set_entry_point(options.0, ptr(entry), entry.len()), CJS_OK);
        assert_eq!(cjs_options_set_max_depth(options.0, 16), CJS_OK);
    }
    let v = compile(r#"{"$defs": {"item": {"type": "string"}}, "type": "object"}"#, Some(&options)).unwrap();
    assert_eq!(validate(&v, "\"x\""), Ok(true));
    assert_eq!(validate(&v, "{}"), Ok(false));
    // format is an annotation by default, asserted when asked.
    let date = r#"{"format": "date"}"#;
    assert_eq!(validate(&compile(date, None).unwrap(), "\"nope\""), Ok(true));
    let asserting = Options::new();
    unsafe { cjs_options_set_assert_format(asserting.0, CJS_TRUE) };
    assert_eq!(validate(&compile(date, Some(&asserting)).unwrap(), "\"nope\""), Ok(false));
}

// ---------------------------------------------------------------------------------------------------------------------
// Callbacks

struct Counter {
    calls: AtomicUsize,
    freed: Arc<AtomicUsize>,
}

unsafe extern "C" fn even_length(user: *mut c_void, _value: *const c_char, len: usize) -> bool {
    unsafe { &*user.cast::<Counter>() }.calls.fetch_add(1, Ordering::SeqCst);
    len % 2 == 0
}

unsafe extern "C" fn free_counter(user: *mut c_void) {
    let counter = unsafe { Box::from_raw(user.cast::<Counter>()) };
    counter.freed.fetch_add(1, Ordering::SeqCst);
}

#[test]
fn format_callbacks_run_and_their_user_data_is_freed_once_with_the_last_validator() {
    let freed = Arc::new(AtomicUsize::new(0));
    let counter = Box::into_raw(Box::new(Counter { calls: AtomicUsize::new(0), freed: freed.clone() }));
    let options = Options::new();
    let name = "even";
    unsafe {
        cjs_options_set_assert_format(options.0, CJS_TRUE);
        let s = cjs_options_add_format(
            options.0,
            ptr(name),
            name.len(),
            Some(even_length),
            counter.cast(),
            Some(free_counter),
        );
        assert_eq!(s, CJS_OK);
    }
    let v = compile(r#"{"items": {"format": "even"}}"#, Some(&options)).unwrap();
    let copy = Compiled(unsafe { cjs_validator_clone(v.0) });
    assert_eq!(validate(&v, r#"["ab", "cdef"]"#), Ok(true));
    assert_eq!(validate(&copy, r#"["ab", "c"]"#), Ok(false));
    assert_eq!(unsafe { &*counter }.calls.load(Ordering::SeqCst), 4);
    drop(options);
    drop(v);
    assert_eq!(freed.load(Ordering::SeqCst), 0, "freed while a validator still uses it");
    drop(copy);
    assert_eq!(freed.load(Ordering::SeqCst), 1);

    // A failed registration still frees the user data.
    let freed = Arc::new(AtomicUsize::new(0));
    let counter = Box::into_raw(Box::new(Counter { calls: AtomicUsize::new(0), freed: freed.clone() }));
    let s = unsafe {
        cjs_options_add_format(
            std::ptr::null_mut(),
            ptr(name),
            name.len(),
            Some(even_length),
            counter.cast(),
            Some(free_counter),
        )
    };
    assert_eq!((s, freed.load(Ordering::SeqCst)), (CJS_INVALID_ARGUMENT, 1));
}

unsafe extern "C" fn failing_resolver(
    _: *mut c_void,
    _: *const c_char,
    _: usize,
    out: *mut cjs_resolved,
) -> cjs_status {
    let why = "the network is down";
    unsafe { cjs_resolved_set_error(out, ptr(why), why.len()) };
    CJS_INVALID_ARGUMENT
}

unsafe extern "C" fn garbage_resolver(
    _: *mut c_void,
    _: *const c_char,
    _: usize,
    out: *mut cjs_resolved,
) -> cjs_status {
    let text = "{not json";
    unsafe { cjs_resolved_set_json(out, ptr(text), text.len()) }
}

#[test]
fn resolver_failures_are_reported() {
    let schema = r#"{"$ref": "http://example.com/other.json"}"#;
    for (resolver, expected) in [
        (failing_resolver as unsafe extern "C" fn(_, _, _, _) -> _, "the network is down"),
        (garbage_resolver, "invalid JSON"),
    ] {
        let options = Options::new();
        unsafe { cjs_options_set_resolver(options.0, Some(resolver), std::ptr::null_mut(), None) };
        let (status, message, _) = compile(schema, Some(&options)).err().unwrap();
        assert_eq!(status, CJS_COMPILATION_FAILED);
        assert!(message.contains(expected) && message.contains("other.json"), "{message}");
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Documents and collectors

#[test]
fn documents_validate_like_text() {
    let v = compile(r#"{"properties": {"name": {"type": "string", "title": "Name"}}}"#, None).unwrap();
    let text = r#"{"name": "aé"}"#.to_string();
    for borrowed in [false, true] {
        let mut d = std::ptr::null_mut();
        let parse = if borrowed { cjs_document_parse_borrowed } else { cjs_document_parse };
        assert_eq!(unsafe { parse(ptr(&text), text.len(), &mut d) }, CJS_OK);
        let mut valid = false;
        assert_eq!(unsafe { cjs_validator_validate_document(v.0, d, &mut valid) }, CJS_OK);
        assert!(valid);
        let c = Collector::new(CJS_VERBOSE);
        assert_eq!(unsafe { cjs_validator_evaluate_document(v.0, d, c.0, &mut valid) }, CJS_OK);
        assert!(valid && c.rows().iter().any(|r| r.4 == "/name"));
        let mut annotations = cjs_str { ptr: std::ptr::null(), len: 0 };
        assert_eq!(unsafe { cjs_collector_annotations_json(c.0, &mut annotations) }, CJS_OK);
        let a: Value = serde_json::from_str(&string(annotations)).unwrap();
        assert_eq!(a["/name"]["title"]["#/properties/name"], "Name");
        unsafe { cjs_document_free(d) };
    }
    let mut d = std::ptr::null_mut();
    assert_eq!(unsafe { cjs_document_parse(ptr("[1,"), 3, &mut d) }, CJS_INVALID_JSON);
    assert!(d.is_null());
}

#[test]
fn collectors_hold_the_latest_evaluation() {
    let v = compile(r#"{"type": "object", "required": ["a"]}"#, None).unwrap();
    let c = Collector::new(CJS_DETAILED);
    assert_eq!(evaluate(&v, "{}", &c), Ok(false));
    let failing = c.rows();
    assert!(failing.iter().any(|r| !r.0 && !r.1.is_empty()));
    assert_eq!(evaluate(&v, r#"{"a": 1}"#, &c), Ok(true));
    assert!(c.rows().len() < failing.len() + 1 && c.rows() != failing);
    unsafe { cjs_collector_clear(c.0) };
    assert_eq!(unsafe { cjs_collector_count(c.0) }, 0);
    // Rows out of range are empty.
    assert!(!unsafe { cjs_collector_is_match(c.0, 99) });
    assert_eq!(unsafe { cjs_collector_message(c.0, 99) }.len, 0);
}

// ---------------------------------------------------------------------------------------------------------------------
// Patterns

// The crate before 0.1.4 kept this pattern's excluded set as ASCII bits and read é as the bits of C and ).
#[test]
fn excluded_class_with_a_member_outside_ascii() {
    let v = compile(r#"{"pattern": "^(?=[^é]+$)(?=(.*\\w)).+$"}"#, None).unwrap();
    assert_eq!(validate(&v, r#""C1""#), Ok(true));
    assert_eq!(validate(&v, r#"")a""#), Ok(true));
    assert_eq!(validate(&v, r#""é1""#), Ok(false));
}

// ---------------------------------------------------------------------------------------------------------------------
// Recursion

// The crate before 0.1.6 evaluated a not without the guard on in-place recursion, so the schemas whose not leads back
// to the schema it is in overflowed the stack, which ends the process. Each of these stops at the maximum depth.
#[test]
fn not_on_an_in_place_cycle_stops_at_max_depth() {
    let schemas = [
        r##"{"not": {"$ref": "#"}}"##,
        r##"{"not": {"not": {"$ref": "#"}}}"##,
        r##"{"type": "integer", "not": {"$ref": "#"}}"##,
        r##"{"allOf": [{"not": {"$ref": "#"}}]}"##,
        r##"{"$defs": {"a": {"not": {"$ref": "#/$defs/b"}}, "b": {"not": {"$ref": "#/$defs/a"}}}, "$ref": "#/$defs/a"}"##,
        r##"{"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "not": {"$ref": "#/$defs/loop"}}"##,
        r##"{"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "not": {"not": {"$ref": "#/$defs/loop"}}}"##,
        r##"{"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "properties": {"a": {"not": {"$ref": "#/$defs/loop"}}}}"##,
        r##"{"unevaluatedProperties": false, "not": {"$ref": "#"}}"##,
    ];
    let options = Options::new();
    assert_eq!(unsafe { cjs_options_set_max_depth(options.0, 16) }, CJS_OK);
    for (s, schema) in schemas.iter().enumerate() {
        let v = compile(schema, Some(&options)).unwrap();
        for instance in ["1", r#""a""#, r#"{"a": 1}"#, "[1]"] {
            // Only an object with the property reaches the loop of the eighth schema, and anything but an integer
            // fails the type of the third before its not is reached, when failing fast.
            if (s == 7 && !instance.starts_with('{')) || (s == 2 && instance != "1") {
                continue;
            }
            let what = format!("{schema} with {instance}");
            assert_eq!(validate(&v, instance).map_err(|e| e.0), Err(CJS_DEPTH_EXCEEDED), "{what}");
            assert!(!last_error().0.is_empty(), "{what}");
            let mut d = std::ptr::null_mut();
            assert_eq!(unsafe { cjs_document_parse(ptr(instance), instance.len(), &mut d) }, CJS_OK);
            let mut valid = false;
            assert_eq!(unsafe { cjs_validator_validate_document(v.0, d, &mut valid) }, CJS_DEPTH_EXCEEDED, "{what}");
            for level in [CJS_BASIC, CJS_DETAILED, CJS_VERBOSE] {
                let c = Collector::new(level);
                assert_eq!(evaluate(&v, instance, &c), Err(CJS_DEPTH_EXCEEDED), "{what}");
                assert_eq!(
                    unsafe { cjs_validator_evaluate_document(v.0, d, c.0, &mut valid) },
                    CJS_DEPTH_EXCEEDED,
                    "{what}"
                );
            }
            unsafe { cjs_document_free(d) };
        }
    }
}

// ---------------------------------------------------------------------------------------------------------------------
// Threads and version

#[test]
fn threads_share_a_validator() {
    let v = Arc::new(compile(r#"{"type": "array", "items": {"type": "integer", "minimum": 0}}"#, None).unwrap());
    let handles: Vec<_> = (0..8)
        .map(|t| {
            let v = v.clone();
            std::thread::spawn(move || {
                for i in 0..2000 {
                    let json = format!("[{t}, {i}, {}]", if i % 7 == 0 { -1 } else { 1 });
                    assert_eq!(validate(&v, &json), Ok(i % 7 != 0));
                }
            })
        })
        .collect();
    for h in handles {
        h.join().unwrap();
    }
}

#[test]
fn version() {
    assert_eq!(string(cjs_version_string()), env!("CARGO_PKG_VERSION"));
    let v = cjs_version();
    assert_eq!((v >> 16, (v >> 8) & 0xff, v & 0xff), (CJS_VERSION_MAJOR, CJS_VERSION_MINOR, CJS_VERSION_PATCH));
}
