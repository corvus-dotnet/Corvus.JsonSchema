//! Times validation alone over jsonschema-benchmark corpora (the instances parsed beforehand), for before/after
//! comparisons of the evaluator:
//!
//!   cargo run --release --example timing -- <schemas dir> [corpus,corpus,...]
//!
//! Prints the best of seven passes per corpus, in microseconds.
use std::time::Instant;

use serde_json::Value;

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let root = std::path::Path::new(&args[1]);
    let only: Option<Vec<&str>> = args.get(2).map(|s| s.split(',').collect());
    let mut dirs: Vec<_> = std::fs::read_dir(root).unwrap().map(|e| e.unwrap().path()).collect();
    dirs.sort();
    for dir in dirs {
        let name = dir.file_name().unwrap().to_string_lossy().to_string();
        if only.as_ref().is_some_and(|o| !o.contains(&name.as_str())) || !dir.join("instances.jsonl").exists() {
            continue;
        }
        let schema: Value = serde_json::from_str(&std::fs::read_to_string(dir.join("schema.json")).unwrap()).unwrap();
        let instances: Vec<Value> = std::fs::read_to_string(dir.join("instances.jsonl"))
            .unwrap()
            .lines()
            .filter(|l| !l.is_empty())
            .map(|l| serde_json::from_str(l).unwrap())
            .collect();
        let v = corvus_json_schema::compile(&schema).unwrap();
        // Warm up for about a second.
        let start = Instant::now();
        while start.elapsed().as_millis() < 1000 {
            for x in &instances {
                std::hint::black_box(v.is_valid(x));
            }
        }
        let mut best = f64::MAX;
        for _ in 0..7 {
            let t = Instant::now();
            let mut valid = 0;
            for x in &instances {
                valid += v.is_valid(x) as usize;
            }
            assert_eq!(valid, instances.len(), "{name}: every instance is valid");
            best = best.min(t.elapsed().as_secs_f64() * 1e6);
        }
        println!("{name:<28}{best:>12.1}");
    }
}
