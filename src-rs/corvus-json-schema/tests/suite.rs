//! Runs the JSON-Schema-Test-Suite (the repository's submodule) against the evaluator, mirroring the C# SuiteRunner
//! and the TypeScript `test/suite.mjs`: required and optional tests with format as an annotation, optional/format
//! with format asserted. Every case runs fail-fast and through a results collector at each level.
//!
//! Set `JSON_SCHEMA_TEST_SUITE` to use a different checkout, `SUITE_DRAFT` / `SUITE_FILTER` to narrow the run.

use std::collections::HashMap;
use std::path::{Path, PathBuf};
use std::sync::{Arc, Mutex};

use corvus_json_schema::{
    CompileOptions, Dialect, JsonDocument, JsonSchemaResultsCollector, ResultsLevel, Validator, compile_with,
};
use serde_json::Value;

const DRAFTS: [(&str, Dialect); 5] = [
    ("draft4", Dialect::Draft4),
    ("draft6", Dialect::Draft6),
    ("draft7", Dialect::Draft7),
    ("draft2019-09", Dialect::Draft201909),
    ("draft2020-12", Dialect::Draft202012),
];

// Exclusions, matching the C# runner: zero-terminated floats (serde_json cannot tell 1.0 from 1 either).
const EXCLUDED_FILES: [&str; 1] = ["draft4/optional/zeroTerminatedFloats.json"];

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

struct Runner {
    options_base: CompileOptions,
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
    fn run_file(&mut self, dialect: Dialect, file: &Path, label: &str, assert_format: bool) {
        let groups = read_json(file);
        let mut file_total = 0;
        let mut file_failed = 0;
        for group in groups.as_array().unwrap() {
            let description = group["description"].as_str().unwrap();
            if let Some(f) = &self.filter
                && !description.contains(f.as_str())
                && !label.contains(f.as_str())
            {
                continue;
            }
            let mut options = self.options_base.clone();
            options.default_dialect = dialect;
            options.assert_format = if assert_format { Some(true) } else { None };
            let compiled =
                std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| compile_with(&group["schema"], &options)));
            for test in group["tests"].as_array().unwrap() {
                file_total += 1;
                let expected = test["valid"].as_bool().unwrap();
                let test_description = test["description"].as_str().unwrap();
                let actual: Result<bool, String> = match &compiled {
                    Err(_) => Err("panic during compilation".into()),
                    Ok(Err(e)) => Err(format!("compile error: {e}")),
                    Ok(Ok(v)) => std::panic::catch_unwind(std::panic::AssertUnwindSafe(|| run_case(v, &test["data"])))
                        .unwrap_or_else(|_| Err("panic during evaluation".into())),
                };
                if actual.as_ref() != Ok(&expected) {
                    // Leap seconds are skipped in the format run, as in the C# runner.
                    if assert_format && test_description.to_lowercase().contains("leap second") {
                        continue;
                    }
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
    let tests = root.join("tests");
    if !tests.exists() {
        eprintln!("JSON-Schema-Test-Suite not found at {}; skipping", root.display());
        return;
    }
    let remotes = root.join("remotes");
    let cache: Arc<Mutex<HashMap<PathBuf, Value>>> = Arc::default();
    let resolver = move |uri: &str| -> Option<Value> {
        let rest = uri.strip_prefix("http://localhost:1234/")?;
        let file = remotes.join(rest);
        if !file.exists() {
            return None;
        }
        let mut cache = cache.lock().unwrap();
        Some(cache.entry(file.clone()).or_insert_with(|| read_json(&file)).clone())
    };
    let options_base = CompileOptions { resolve_document: Some(Arc::new(resolver)), ..CompileOptions::default() };

    let draft_filter = std::env::var("SUITE_DRAFT").ok();
    let mut runner = Runner {
        options_base,
        filter: std::env::var("SUITE_FILTER").ok(),
        total: 0,
        failures: Vec::new(),
        summary: Vec::new(),
    };
    // Keep panics in the evaluator from spamming the output: they are reported as failures.
    std::panic::set_hook(Box::new(|_| {}));
    for (draft, dialect) in DRAFTS {
        if draft_filter.as_deref().is_some_and(|d| d != draft) {
            continue;
        }
        let dir = tests.join(draft);
        for f in files(&dir) {
            let label = format!("{draft}/{}", f.file_name().unwrap().to_string_lossy());
            runner.run_file(dialect, &f, &label, false);
        }
        for f in files(&dir.join("optional")) {
            let label = format!("{draft}/optional/{}", f.file_name().unwrap().to_string_lossy());
            if !EXCLUDED_FILES.contains(&label.as_str()) {
                runner.run_file(dialect, &f, &label, false);
            }
        }
        for f in files(&dir.join("optional").join("format")) {
            let label = format!("{draft}/optional/format/{}", f.file_name().unwrap().to_string_lossy());
            runner.run_file(dialect, &f, &label, true);
        }
    }
    let _ = std::panic::take_hook();

    for line in &runner.failures {
        println!("{line}");
    }
    let mut by_area: Vec<(String, usize, usize)> = Vec::new();
    for (label, t, f) in &runner.summary {
        let depth = if label.contains("/optional/format/") {
            3
        } else if label.contains("/optional/") {
            2
        } else {
            1
        };
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
