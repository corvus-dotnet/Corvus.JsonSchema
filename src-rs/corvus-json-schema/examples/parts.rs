//! Times one part of each instance against one subschema, to localise a difference between builds:
//!
//!   cargo run --release --example parts -- <corpus dir> <entry point> <json pointer into each instance>
//!
//! Prints the best of seven passes (microseconds) and the number of parts.
use std::time::Instant;

use serde_json::Value;

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let dir = std::path::Path::new(&args[1]);
    let schema: Value = serde_json::from_str(&std::fs::read_to_string(dir.join("schema.json")).unwrap()).unwrap();
    let parts: Vec<Value> = std::fs::read_to_string(dir.join("instances.jsonl"))
        .unwrap()
        .lines()
        .filter(|l| !l.is_empty())
        .filter_map(|l| serde_json::from_str::<Value>(l).unwrap().pointer(&args[3]).cloned())
        .collect();
    let options = corvus_json_schema::CompileOptions { entry_point: Some(args[2].clone()), ..Default::default() };
    let v = corvus_json_schema::compile_with(&schema, &options).unwrap();
    let start = Instant::now();
    while start.elapsed().as_millis() < 500 {
        for x in &parts {
            std::hint::black_box(v.is_valid(x));
        }
    }
    let mut best = f64::MAX;
    for _ in 0..7 {
        let t = Instant::now();
        for _ in 0..20 {
            for x in &parts {
                std::hint::black_box(v.is_valid(x));
            }
        }
        best = best.min(t.elapsed().as_secs_f64() * 1e6 / 20.0);
    }
    println!("{best:.2} {}", parts.len());
}
