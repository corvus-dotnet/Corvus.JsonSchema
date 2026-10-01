//! Validates a jsonschema-benchmark corpus repeatedly, for profilers:
//!
//!   cargo run --release --example profile -- <corpus dir> [passes]
//!
//! With `DOC` set, the instances are parsed into `JsonDocument`s instead of `serde_json::Value`s.
use corvus_json_schema::JsonDocument;
use serde_json::Value;

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let dir = std::path::Path::new(&args[1]);
    let passes: usize = args.get(2).map_or(100, |s| s.parse().unwrap());
    let schema: Value = serde_json::from_str(&std::fs::read_to_string(dir.join("schema.json")).unwrap()).unwrap();
    let contents = std::fs::read_to_string(dir.join("instances.jsonl")).unwrap();
    let lines: Vec<&str> = contents.lines().filter(|l| !l.is_empty()).collect();
    let v = corvus_json_schema::compile(&schema).unwrap();
    let mut rejected = 0;
    if std::env::var_os("DOC").is_some() {
        let documents: Vec<JsonDocument<'_>> = lines.iter().map(|l| JsonDocument::parse(l).unwrap()).collect();
        for _ in 0..passes {
            rejected += documents.iter().filter(|d| !v.validate_instance(d.root()).unwrap_or(false)).count();
        }
    } else {
        let instances: Vec<Value> = lines.iter().map(|l| serde_json::from_str(l).unwrap()).collect();
        for _ in 0..passes {
            rejected += instances.iter().filter(|x| !v.is_valid(x)).count();
        }
    }
    println!("{passes} passes, {rejected} rejected");
}
