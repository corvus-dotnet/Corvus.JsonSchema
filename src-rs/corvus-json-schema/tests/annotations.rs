//! Runs the JSON-Schema-Test-Suite annotation tests (`JSON-Schema-Test-Suite/annotations`) through a verbose results
//! collector, as the C# AnnotationSuiteTests and the TypeScript `test/annotations.mjs` do: every draft, cases filtered
//! by "compatibility", and each assertion compared with the annotations grouped by instance location, keyword and
//! schema location.

use std::path::{Path, PathBuf};
use std::sync::Arc;

use corvus_json_schema::{
    CompileOptions, Dialect, JsonSchemaResultsCollector, ResultsLevel, collect_annotations, compile_with,
};
use serde_json::Value;

const ORDER: [&str; 6] = ["3", "4", "6", "7", "2019", "2020"];

fn compatible(level: &str, compat: &str) -> bool {
    let level = ORDER.iter().position(|x| *x == level).unwrap();
    if let Some(max) = compat.strip_prefix("<=") {
        ORDER.iter().position(|x| *x == max).is_some_and(|max| level <= max)
    } else {
        ORDER.iter().position(|x| *x == compat).is_some_and(|min| level >= min)
    }
}

fn read_json(path: &Path) -> Value {
    serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap()
}

/// JSON equality with numbers compared by value (1 == 1.0) and objects unordered.
fn json_equal(a: &Value, b: &Value) -> bool {
    match (a, b) {
        (Value::Number(x), Value::Number(y)) => x.as_f64() == y.as_f64(),
        (Value::Array(x), Value::Array(y)) => x.len() == y.len() && x.iter().zip(y).all(|(x, y)| json_equal(x, y)),
        (Value::Object(x), Value::Object(y)) => {
            x.len() == y.len() && x.iter().all(|(k, v)| y.get(k).is_some_and(|w| json_equal(v, w)))
        }
        _ => a == b,
    }
}

#[test]
fn annotation_suite() {
    let root = std::env::var_os("JSON_SCHEMA_TEST_SUITE")
        .map(PathBuf::from)
        .unwrap_or_else(|| Path::new(env!("CARGO_MANIFEST_DIR")).join("../../JSON-Schema-Test-Suite"));
    let dir = root.join("annotations").join("tests");
    if !dir.exists() {
        eprintln!("annotation tests not found at {}; skipping", dir.display());
        return;
    }
    let remotes = root.join("remotes");
    let resolver = move |uri: &str| -> Option<Value> {
        let file = remotes.join(uri.strip_prefix("http://localhost:1234/")?);
        file.exists().then(|| read_json(&file))
    };
    let mut files: Vec<PathBuf> = std::fs::read_dir(&dir)
        .unwrap()
        .filter_map(|e| e.ok().map(|e| e.path()))
        .filter(|p| p.extension().is_some_and(|x| x == "json"))
        .collect();
    files.sort();

    let drafts = [
        ("draft4", Dialect::Draft4, "4"),
        ("draft6", Dialect::Draft6, "6"),
        ("draft7", Dialect::Draft7, "7"),
        ("draft2019-09", Dialect::Draft201909, "2019"),
        ("draft2020-12", Dialect::Draft202012, "2020"),
    ];
    let mut total = 0;
    let mut failures = Vec::new();
    for (draft, dialect, level) in drafts {
        for file in &files {
            let suite = read_json(file);
            let name = file.file_name().unwrap().to_string_lossy();
            for group in suite["suite"].as_array().unwrap() {
                if let Some(compat) = group.get("compatibility").and_then(Value::as_str)
                    && !compatible(level, compat)
                {
                    continue;
                }
                let options = CompileOptions {
                    default_dialect: dialect,
                    resolve_document: Some(Arc::new(resolver.clone())),
                    ..CompileOptions::default()
                };
                let description = group["description"].as_str().unwrap_or("");
                let validator = match compile_with(&group["schema"], &options) {
                    Ok(v) => v,
                    Err(e) => {
                        failures.push(format!("{draft}/{name} [{description}]: compile error: {e}"));
                        continue;
                    }
                };
                for test in group["tests"].as_array().unwrap() {
                    let mut collector = JsonSchemaResultsCollector::new(ResultsLevel::Verbose);
                    validator.evaluate(&test["instance"], &mut collector).unwrap();
                    let produced = collect_annotations(&collector);
                    for assertion in test["assertions"].as_array().unwrap() {
                        total += 1;
                        let location = assertion["location"].as_str().unwrap();
                        let keyword = assertion["keyword"].as_str().unwrap();
                        let expected = &assertion["expected"];
                        let actual = produced
                            .get(location)
                            .and_then(|k| k.get(keyword))
                            .map(|m| Value::Object(m.iter().map(|(k, v)| (k.clone(), v.clone())).collect()));
                        let expected_empty = expected.as_object().is_some_and(|o| o.is_empty());
                        let ok = match &actual {
                            None => expected_empty,
                            Some(a) => !expected_empty && json_equal(a, expected),
                        };
                        if !ok {
                            failures.push(format!(
                                "{draft}/{name} [{description}] instance {} '{location}' {keyword}: expected {expected}, actual {}",
                                test["instance"],
                                actual.map_or("undefined".to_string(), |a| a.to_string())
                            ));
                        }
                    }
                }
            }
        }
    }
    for f in &failures {
        println!("{f}");
    }
    println!("{}/{total} annotation assertions passed", total - failures.len());
    assert!(failures.is_empty(), "{} annotation assertions failed", failures.len());
}
