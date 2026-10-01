//! Validates a jsonschema-benchmark corpus repeatedly, for profilers:
//!
//!   cargo run --release --example profile -- <corpus dir> [passes]
use serde_json::Value;

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let dir = std::path::Path::new(&args[1]);
    let passes: usize = args.get(2).map_or(100, |s| s.parse().unwrap());
    let schema: Value = serde_json::from_str(&std::fs::read_to_string(dir.join("schema.json")).unwrap()).unwrap();
    let instances: Vec<Value> = std::fs::read_to_string(dir.join("instances.jsonl"))
        .unwrap()
        .lines()
        .filter(|l| !l.is_empty())
        .map(|l| serde_json::from_str(l).unwrap())
        .collect();
    let v = corvus_json_schema::compile(&schema).unwrap();
    let mut rejected = 0;
    for _ in 0..passes {
        rejected += instances.iter().filter(|x| !v.is_valid(x)).count();
    }
    println!("{passes} passes, {rejected} rejected");
}
