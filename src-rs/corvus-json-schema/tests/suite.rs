//! Runs the JSON-Schema-Test-Suite (the repository's submodule) against the evaluator, mirroring the C# SuiteRunner
//! and the TypeScript `test/suite.mjs`. Every case runs fail-fast and through a results collector at each level.
//!
//! The suite keeps every dialect in one tree (`validation/tests`). Each test case says which releases it applies to
//! (`compatibility`), whether it expects `format` to be asserted (`tags`), and carries any other schemas it references
//! (`externalSchemas`), which are scoped to that test case.
//!
//! Set `JSON_SCHEMA_TEST_SUITE` to use a different checkout, `SUITE_DRAFT` / `SUITE_FILTER` to narrow the run.

use std::collections::HashMap;
use std::path::{Path, PathBuf};
use std::sync::Arc;

use corvus_json_schema::{
    CompileOptions, Dialect, JsonDocument, JsonSchemaResultsCollector, ResultsLevel, Validator, compile_with,
};
use serde_json::Value;

// The drafts, with the release number the suite's `compatibility` uses for each.
const DRAFTS: [(&str, Dialect, u32); 5] = [
    ("draft4", Dialect::Draft4, 4),
    ("draft6", Dialect::Draft6, 6),
    ("draft7", Dialect::Draft7, 7),
    ("draft2019-09", Dialect::Draft201909, 2019),
    ("draft2020-12", Dialect::Draft202012, 2020),
];

// Exclusions, matching the C# runner: zero-terminated floats (serde_json cannot tell 1.0 from 1 either).
const EXCLUDED_FILES: [&str; 1] = ["draft4/optional/zero-terminated-floats.json"];

// The suite contradicts itself on leap seconds: format-time.json (required) says 23:59:60Z is not a valid time, and
// optional/format-time.json says it is, for the same drafts. The evaluator accepts leap seconds, so these six required
// cases are left out by name.
//
// The crate reads JSON through serde_json without its arbitrary_precision feature, so a number has at most the
// precision of an f64 by the time the evaluator sees it. These high-precision cases of optional/bignum.json need the
// digits of the text, and are left out by name.
const EXCLUDED_CASES: [(&str, &str); 11] = [
    ("format-time.json", "a leap second is not a valid time"),
    ("format-time.json", "a leap second with a zero time-offset is not a valid time"),
    ("format-time.json", "a leap second with a positive time-offset is not a valid time"),
    ("format-time.json", "a leap second with a large positive time-offset is not a valid time"),
    ("format-time.json", "a leap second with a negative time-offset is not a valid time"),
    ("format-time.json", "a leap second with a large negative time-offset is not a valid time"),
    ("bignum.json", "below the exclusive maximum is valid"),
    ("bignum.json", "above the exclusive minimum is valid"),
    ("bignum.json", "just below the minimum is invalid"),
    ("bignum.json", "a slightly smaller decimal is invalid"),
    ("bignum.json", "a slightly larger decimal is invalid"),
];

/// Whether a test case's `compatibility` (a comma-separated list of minimums, `<=` maximums and `=` releases, from
/// the smallest release to the largest) admits the release, by the algorithm of the suite's validation/README.md.
fn compatible(compatibility: Option<&str>, release: u32) -> bool {
    let Some(compatibility) = compatibility else { return true };
    let mut valid = true;
    for constraint in compatibility.split(',') {
        let (op, version) = if let Some(v) = constraint.strip_prefix("<=") {
            ("<=", v)
        } else if let Some(v) = constraint.strip_prefix('=') {
            ("=", v)
        } else {
            ("", constraint)
        };
        let version: u32 = version.trim().parse().unwrap();
        valid = match op {
            "<=" => release <= version,
            "=" => release == version,
            _ => release >= version,
        };
        if release <= version {
            break;
        }
    }
    valid
}

fn has_tag(case: &Value, tag: &str) -> bool {
    case["tags"].as_array().is_some_and(|tags| tags.iter().any(|t| t == tag))
}

fn suite_root() -> PathBuf {
    std::env::var_os("JSON_SCHEMA_TEST_SUITE")
        .map(PathBuf::from)
        .unwrap_or_else(|| Path::new(env!("CARGO_MANIFEST_DIR")).join("../../JSON-Schema-Test-Suite"))
}

fn read_json(path: &Path) -> Value {
    serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap()
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

/// A resolver serving a test case's `externalSchemas` at their retrieval URIs, and nothing else.
fn resolver_for(case: &Value) -> Option<corvus_json_schema::DocumentResolver> {
    let schemas: HashMap<String, Value> =
        case["externalSchemas"].as_object()?.iter().map(|(k, v)| (k.clone(), v.clone())).collect();
    Some(Arc::new(move |uri: &str| schemas.get(uri).cloned()))
}

struct Runner {
    filter: Option<String>,
    total: usize,
    failures: Vec<String>,
    summary: Vec<(String, usize, usize)>,
}

const LEVELS: [ResultsLevel; 3] = [ResultsLevel::Basic, ResultsLevel::Detailed, ResultsLevel::Verbose];

fn run_case(v: &Validator, data: &Value) -> Result<bool, String> {
    let fast = v.validate(data).map_err(|e| format!("fast: {e}"))?;
    for level in LEVELS {
        let mut c = JsonSchemaResultsCollector::new(level);
        let ok = v.evaluate(data, &mut c).map_err(|e| format!("{level:?}: {e}"))?;
        if ok != fast {
            return Err(format!("{level:?} returned {ok}, fast returned {fast}"));
        }
        if !c
            .results()
            .iter()
            .any(|r| r.evaluation_location.is_empty() && r.document_evaluation_location.is_empty() && r.is_match == ok)
        {
            return Err(format!("{level:?}: no root summary row matching the result"));
        }
    }
    // The same instance parsed into a JsonDocument: the same verdict, and the same results rows.
    let text = serde_json::to_string(data).unwrap();
    let document = JsonDocument::parse(&text).map_err(|e| format!("document: {e}"))?;
    let from_document = v.validate_instance(document.root()).map_err(|e| format!("document: {e}"))?;
    if from_document != fast {
        return Err(format!("the document returned {from_document}, the Value {fast}"));
    }
    let (mut c1, mut c2) = (
        JsonSchemaResultsCollector::new(ResultsLevel::Verbose),
        JsonSchemaResultsCollector::new(ResultsLevel::Verbose),
    );
    v.evaluate(data, &mut c1).map_err(|e| format!("Verbose: {e}"))?;
    v.evaluate_instance(document.root(), &mut c2).map_err(|e| format!("document Verbose: {e}"))?;
    if c1.results() != c2.results() {
        return Err("the document's verbose results differ from the Value's".into());
    }
    // And straight from the text, in the parser's reused buffers.
    let from_text = v.validate_json(&text).map_err(|e| format!("validate_json: {e}"))?;
    if from_text != fast {
        return Err(format!("validate_json returned {from_text}, the Value {fast}"));
    }
    let mut c3 = JsonSchemaResultsCollector::new(ResultsLevel::Verbose);
    v.evaluate_json(&text, &mut c3).map_err(|e| format!("evaluate_json: {e}"))?;
    if c1.results() != c3.results() {
        return Err("evaluate_json's verbose results differ from the Value's".into());
    }
    Ok(fast)
}

impl Runner {
    fn run_file(&mut self, dialect: Dialect, release: u32, file: &Path, label: &str) {
        let file_name = file.file_name().unwrap().to_string_lossy().to_string();
        let groups = read_json(file);
        let mut file_total = 0;
        let mut file_failed = 0;
        for group in groups["tests"].as_array().unwrap() {
            let description = group["description"].as_str().unwrap();
            if !compatible(group["compatibility"].as_str(), release) {
                continue;
            }
            // Drafts 4 to 7 assert format by default here (the specification allows it), so a "format-annotation"
            // case describes behaviour the evaluator does not provide there.
            if has_tag(group, "format-annotation") && release <= 7 {
                continue;
            }
            if let Some(f) = &self.filter
                && !description.contains(f.as_str())
                && !label.contains(f.as_str())
            {
                continue;
            }
            let options = CompileOptions {
                default_dialect: dialect,
                assert_format: if has_tag(group, "format-assertion") { Some(true) } else { None },
                resolve_document: resolver_for(group),
                ..CompileOptions::default()
            };
            let compiled =
                std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| compile_with(&group["schema"], &options)));
            for test in group["tests"].as_array().unwrap() {
                let expected = test["valid"].as_bool().unwrap();
                let test_description = test["description"].as_str().unwrap();
                if EXCLUDED_CASES.contains(&(file_name.as_str(), test_description)) {
                    continue;
                }
                file_total += 1;
                let actual: Result<bool, String> = match &compiled {
                    Err(_) => Err("panic during compilation".into()),
                    Ok(Err(e)) => Err(format!("compile error: {e}")),
                    Ok(Ok(v)) => std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| run_case(v, &test["data"])))
                        .unwrap_or_else(|_| Err("panic during evaluation".into())),
                };
                if actual.as_ref() != Ok(&expected) {
                    file_failed += 1;
                    self.failures.push(format!(
                        "{label} [{description}] {test_description}: expected {expected}, got {}",
                        match actual {
                            Ok(b) => b.to_string(),
                            Err(e) => e,
                        }
                    ));
                }
            }
        }
        self.total += file_total;
        self.summary.push((label.to_string(), file_total, file_failed));
    }
}

#[test]
fn json_schema_test_suite() {
    let root = suite_root();
    let tests = root.join("validation").join("tests");
    if !tests.exists() {
        eprintln!("JSON-Schema-Test-Suite not found at {}; skipping", root.display());
        return;
    }

    let draft_filter = std::env::var("SUITE_DRAFT").ok();
    let mut runner =
        Runner { filter: std::env::var("SUITE_FILTER").ok(), total: 0, failures: Vec::new(), summary: Vec::new() };
    // Keep panics in the evaluator from spamming the output: they are reported as failures.
    std::panic::set_hook(Box::new(|_| {}));
    for (draft, dialect, release) in DRAFTS {
        if draft_filter.as_deref().is_some_and(|d| d != draft) {
            continue;
        }
        for f in files(&tests) {
            let label = format!("{draft}/{}", f.file_name().unwrap().to_string_lossy());
            runner.run_file(dialect, release, &f, &label);
        }
        for f in files(&tests.join("optional")) {
            let label = format!("{draft}/optional/{}", f.file_name().unwrap().to_string_lossy());
            if !EXCLUDED_FILES.contains(&label.as_str()) {
                runner.run_file(dialect, release, &f, &label);
            }
        }
    }
    let _ = std::panic::take_hook();

    for line in &runner.failures {
        println!("{line}");
    }
    let mut by_area: Vec<(String, usize, usize)> = Vec::new();
    for (label, t, f) in &runner.summary {
        let depth = if label.contains("/optional/") { 2 } else { 1 };
        let area = label.split('/').take(depth).collect::<Vec<_>>().join("/");
        match by_area.iter_mut().find(|(a, _, _)| *a == area) {
            Some(entry) => {
                entry.1 += t;
                entry.2 += f;
            }
            None => by_area.push((area, *t, *f)),
        }
    }
    for (area, t, f) in &by_area {
        println!("{area:<34} {:>5}/{t}", t - f);
    }
    let failed = runner.failures.len();
    println!("\n{}/{} passed, {failed} failed", runner.total - failed, runner.total);
    assert_eq!(failed, 0, "{failed} JSON-Schema-Test-Suite cases failed");
}
