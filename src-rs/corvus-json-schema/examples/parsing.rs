//! Compares parsing into a `JsonDocument` with parsing into a `serde_json::Value` over jsonschema-benchmark corpora,
//! and validation of each: that the two agree on every instance (the same value, the same verdict), and how long
//! each takes.
//!
//!   cargo run --release --example parsing -- <schemas dir> [corpus,corpus,...]
//!
//! Prints, per corpus, the best of seven passes of each (parsing every instance once; validating every parsed
//! instance once, after a warm-up), in microseconds, and the document/Value ratios with their geometric means.
//! With `RETAIN` set, it also times parsing into a vector that keeps every instance (as jsonschema-benchmark does),
//! where the allocator's cost per allocation shows: musl's is several times glibc's.
use std::time::Instant;

use corvus_json_schema::JsonDocument;
use serde_json::Value;

/// Whether the values are equal, but for doubles one ulp apart: serde_json's float parser (without its
/// `float_roundtrip` feature) is sometimes one ulp off the correctly rounded value, which the document has.
fn same(a: &Value, b: &Value) -> bool {
    match (a, b) {
        (Value::Number(x), Value::Number(y)) if x.is_f64() && y.is_f64() => {
            let (x, y) = (x.as_f64().unwrap(), y.as_f64().unwrap());
            x == y || (x.signum() == y.signum() && x.to_bits().abs_diff(y.to_bits()) == 1)
        }
        (Value::Array(x), Value::Array(y)) => x.len() == y.len() && x.iter().zip(y).all(|(x, y)| same(x, y)),
        (Value::Object(x), Value::Object(y)) => {
            x.len() == y.len() && x.iter().zip(y).all(|((kx, vx), (ky, vy))| kx == ky && same(vx, vy))
        }
        _ => a == b,
    }
}

fn best_of<F: FnMut()>(mut f: F) -> f64 {
    let mut best = f64::MAX;
    for _ in 0..7 {
        let t = Instant::now();
        f();
        best = best.min(t.elapsed().as_secs_f64() * 1e6);
    }
    best
}

fn warm<F: FnMut()>(mut f: F) {
    let start = Instant::now();
    while start.elapsed().as_millis() < 300 {
        f();
    }
}

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let root = std::path::Path::new(&args[1]);
    let only: Option<Vec<&str>> = args.get(2).map(|s| s.split(',').collect());
    let mut dirs: Vec<_> = std::fs::read_dir(root).unwrap().map(|e| e.unwrap().path()).collect();
    dirs.sort();
    println!(
        "{:<24} {:>11} {:>11} {:>7} {:>11} {:>11} {:>7}",
        "corpus (µs)", "parse Value", "parse doc", "ratio", "eval Value", "eval doc", "ratio"
    );
    let (mut parse_logs, mut eval_logs) = (Vec::new(), Vec::new());
    for dir in dirs {
        let name = dir.file_name().unwrap().to_string_lossy().to_string();
        if only.as_ref().is_some_and(|o| !o.contains(&name.as_str())) || !dir.join("instances.jsonl").exists() {
            continue;
        }
        let schema: Value = serde_json::from_str(&std::fs::read_to_string(dir.join("schema.json")).unwrap()).unwrap();
        let contents = std::fs::read_to_string(dir.join("instances.jsonl")).unwrap();
        let lines: Vec<&str> = contents.lines().filter(|l| !l.is_empty()).collect();
        let values: Vec<Value> = lines.iter().map(|l| serde_json::from_str(l).unwrap()).collect();
        let documents: Vec<JsonDocument<'_>> = lines.iter().map(|l| JsonDocument::parse(l).unwrap()).collect();
        let v = corvus_json_schema::compile(&schema).unwrap();
        for (i, (value, document)) in values.iter().zip(&documents).enumerate() {
            assert!(same(&document.to_value(), value), "{name} instance {i}: the document differs from the Value");
            assert_eq!(
                v.validate(value).unwrap(),
                v.validate_instance(document.root()).unwrap(),
                "{name} instance {i}: the verdicts differ"
            );
        }

        warm(|| {
            for l in &lines {
                std::hint::black_box(serde_json::from_str::<Value>(l).unwrap());
                std::hint::black_box(JsonDocument::parse(l).unwrap());
            }
        });
        let parse_value = best_of(|| {
            for l in &lines {
                std::hint::black_box(serde_json::from_str::<Value>(l).unwrap());
            }
        });
        if std::env::var_os("RETAIN").is_some() {
            let kept_value = best_of(|| {
                std::hint::black_box(
                    lines.iter().map(|l| serde_json::from_str::<Value>(l).unwrap()).collect::<Vec<_>>(),
                );
            });
            let kept_document = best_of(|| {
                std::hint::black_box(lines.iter().map(|l| JsonDocument::parse(l).unwrap()).collect::<Vec<_>>());
            });
            println!("{name:<24} retained: parse Value {kept_value:.1}, parse doc {kept_document:.1}");
        }
        let parse_document = best_of(|| {
            for l in &lines {
                std::hint::black_box(JsonDocument::parse(l).unwrap());
            }
        });
        warm(|| {
            for (x, d) in values.iter().zip(&documents) {
                std::hint::black_box(v.is_valid(x));
                std::hint::black_box(v.validate_instance(d.root()).is_ok());
            }
        });
        let eval_value = best_of(|| {
            for x in &values {
                std::hint::black_box(v.is_valid(x));
            }
        });
        let eval_document = best_of(|| {
            for d in &documents {
                std::hint::black_box(v.validate_instance(d.root()).is_ok());
            }
        });
        let (pr, er) = (parse_document / parse_value, eval_document / eval_value);
        parse_logs.push(pr.ln());
        eval_logs.push(er.ln());
        println!(
            "{name:<24} {parse_value:>11.1} {parse_document:>11.1} {pr:>7.2} {eval_value:>11.1} {eval_document:>11.1} {er:>7.2}"
        );
    }
    let geomean = |logs: &[f64]| (logs.iter().sum::<f64>() / logs.len() as f64).exp();
    println!(
        "\ngeometric mean document/Value: parse {:.2}, evaluation {:.2} ({} corpora; every instance agrees)",
        geomean(&parse_logs),
        geomean(&eval_logs),
        parse_logs.len()
    );
}
