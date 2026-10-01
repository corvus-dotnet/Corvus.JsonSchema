//! Differential testing of the fail-fast plans (fused objects, type dispatch, name tables...) against the collecting
//! evaluator, which walks the general keyword-by-keyword path: every instance of the jsonschema-benchmark corpora, and
//! mutations of it (a property removed, retyped, nulled or added, an array item changed, at every depth), must get
//! the same verdict from both.
//!
//! Set `JSONSCHEMA_BENCHMARK` to a checkout of https://github.com/sourcemeta-research/jsonschema-benchmark (it
//! defaults to one next to this repository); the test is skipped when there is none. `DIFF_ONLY` narrows the corpora,
//! `DIFF_INSTANCES` caps the instances per corpus (default 60). It takes minutes, so it is ignored by default:
//!
//!   cargo test --release --test differential -- --ignored

use std::path::{Path, PathBuf};

use corvus_json_schema::{JsonSchemaResultsCollector, ResultsLevel, Validator, compile};
use serde_json::{Value, json};

/// Replacement values of every JSON type, tried in place of a value.
fn replacements() -> Vec<Value> {
    vec![json!(null), json!(true), json!(0), json!(-1.5), json!(""), json!("x"), json!([]), json!({}), json!([1, "a"])]
}

/// Mutations of `v`: at the root and, recursively, inside it (bounded per level to keep the count sane).
fn mutations(v: &Value, depth: usize, out: &mut Vec<Value>) {
    if depth > 6 {
        return;
    }
    match v {
        Value::Object(o) => {
            for (i, (k, child)) in o.iter().enumerate().take(12) {
                let mut removed = o.clone();
                removed.shift_remove(k);
                out.push(Value::Object(removed));
                for r in replacements().into_iter().skip(i % 3).step_by(3) {
                    let mut m = o.clone();
                    m.insert(k.clone(), r);
                    out.push(Value::Object(m));
                }
                let mut inner = Vec::new();
                mutations(child, depth + 1, &mut inner);
                for m in inner.into_iter().take(40) {
                    let mut o2 = o.clone();
                    o2.insert(k.clone(), m);
                    out.push(Value::Object(o2));
                }
            }
            let mut added = o.clone();
            added.insert("zzUnknownProperty".into(), json!(1));
            out.push(Value::Object(added));
        }
        Value::Array(a) => {
            for (i, item) in a.iter().enumerate().take(4) {
                for r in replacements().into_iter().skip(i % 2).step_by(2) {
                    let mut m = a.clone();
                    m[i] = r;
                    out.push(Value::Array(m));
                }
                let mut inner = Vec::new();
                mutations(item, depth + 1, &mut inner);
                for m in inner.into_iter().take(40) {
                    let mut a2 = a.clone();
                    a2[i] = m;
                    out.push(Value::Array(a2));
                }
            }
            if let Some(first) = a.first() {
                let mut dup = a.clone();
                dup.push(first.clone());
                out.push(Value::Array(dup));
            }
        }
        _ => out.extend(replacements()),
    }
}

fn agree(v: &Validator, x: &Value) -> Result<(), String> {
    let fast = v.is_valid(x);
    let mut c = JsonSchemaResultsCollector::new(ResultsLevel::Basic);
    let collected = v.evaluate(x, &mut c).map_err(|e| e.to_string())?;
    if fast == collected { Ok(()) } else { Err(format!("fast {fast}, collecting {collected}")) }
}

fn strip_format(v: &mut Value) {
    match v {
        Value::Object(o) => {
            if o.get("format").is_some_and(Value::is_string) {
                o.shift_remove("format");
            }
            o.values_mut().for_each(strip_format);
        }
        Value::Array(a) => a.iter_mut().for_each(strip_format),
        _ => {}
    }
}

#[test]
#[ignore = "slow: run with --release -- --ignored"]
fn plans_agree_with_the_general_evaluator_on_the_benchmark_corpora() {
    let root = std::env::var_os("JSONSCHEMA_BENCHMARK")
        .map(PathBuf::from)
        .unwrap_or_else(|| Path::new(env!("CARGO_MANIFEST_DIR")).join("../../../jsonschema-benchmark"));
    let schemas = root.join("schemas");
    let Ok(dirs) = std::fs::read_dir(&schemas) else {
        eprintln!("jsonschema-benchmark not found at {}; skipping", root.display());
        return;
    };
    let only: Option<Vec<String>> = std::env::var("DIFF_ONLY").ok().map(|s| s.split(',').map(String::from).collect());
    let cap: usize = std::env::var("DIFF_INSTANCES").ok().map_or(60, |s| s.parse().unwrap());
    let mut dirs: Vec<PathBuf> = dirs.filter_map(|e| e.ok().map(|e| e.path())).collect();
    dirs.sort();
    let mut failures = Vec::new();
    let mut checked = 0usize;
    for dir in dirs {
        let name = dir.file_name().unwrap().to_string_lossy().to_string();
        if only.as_ref().is_some_and(|o| !o.contains(&name)) {
            continue;
        }
        let Ok(text) = std::fs::read_to_string(dir.join("schema.json")) else { continue };
        let mut schema: Value = serde_json::from_str(&text).unwrap();
        strip_format(&mut schema);
        let v = match compile(&schema) {
            Ok(v) => v,
            Err(e) => {
                failures.push(format!("{name}: compile error {e}"));
                continue;
            }
        };
        let instances = std::fs::read_to_string(dir.join("instances.jsonl")).unwrap();
        let mut corpus_failures = 0;
        for line in instances.lines().filter(|l| !l.is_empty()).take(cap) {
            let x: Value = serde_json::from_str(line).unwrap();
            let mut cases = vec![x.clone()];
            mutations(&x, 0, &mut cases);
            for case in cases {
                checked += 1;
                if let Err(e) = agree(&v, &case) {
                    corpus_failures += 1;
                    if corpus_failures <= 3 {
                        let text = case.to_string();
                        failures.push(format!("{name}: {e} on {}", &text[..text.len().min(400)]));
                    }
                }
            }
        }
        if corpus_failures > 3 {
            failures.push(format!("{name}: {corpus_failures} disagreements in all"));
        }
    }
    for f in &failures {
        println!("{f}");
    }
    println!("{checked} instances checked");
    assert!(failures.is_empty(), "{} disagreements", failures.len());
}
