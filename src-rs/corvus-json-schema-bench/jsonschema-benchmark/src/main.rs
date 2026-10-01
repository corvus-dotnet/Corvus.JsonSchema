//! The jsonschema-benchmark (https://github.com/sourcemeta-research/jsonschema-benchmark) implementation entry point:
//!
//!   corvus_rs_benchmark <schema.json> <instances.jsonl>
//!
//! Mirrors the other implementations: read the instance file, parse every instance (timed), compile the schema
//! (timed), validate every instance once cold, warm up, validate once more warm. Prints one line
//! "cold,warm,compile,parse" in nanoseconds and exits non-zero if any instance is invalid.

use std::process::ExitCode;
use std::time::Instant;

use corvus_json_schema::Validator;
use serde_json::Value;

const WARMUP_ITERATIONS: u128 = 100;
const MAX_WARMUP_TIME: u128 = 10_000_000_000; // 10 seconds

fn validate_all(validator: &Validator, instances: &[Value]) -> bool {
    let mut valid = true;
    for instance in instances {
        valid &= validator.is_valid(instance);
    }
    valid
}

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().collect();
    if args.len() != 3 {
        eprintln!("Usage: corvus_rs_benchmark <schema> <instances>");
        return ExitCode::FAILURE;
    }
    let schema: Value =
        serde_json::from_str(&std::fs::read_to_string(&args[1]).expect("read the schema")).expect("parse the schema");
    let contents = std::fs::read_to_string(&args[2]).expect("read the instances");

    let parse_start = Instant::now();
    let instances: Vec<Value> = contents
        .lines()
        .filter(|l| !l.is_empty())
        .map(|l| serde_json::from_str(l).expect("parse an instance"))
        .collect();
    let parse = parse_start.elapsed().as_nanos();

    // The benchmark's schema-noformat.json has no `format` keywords; the defaults leave `format` as an annotation.
    let compile_start = Instant::now();
    let validator = corvus_json_schema::compile(&schema).expect("compile the schema");
    let compile = compile_start.elapsed().as_nanos();

    let cold_start = Instant::now();
    let valid = validate_all(&validator, &instances);
    let cold = cold_start.elapsed().as_nanos();

    let iterations = MAX_WARMUP_TIME.div_ceil(cold.max(1));
    for _ in 0..iterations.min(WARMUP_ITERATIONS) {
        std::hint::black_box(validate_all(&validator, &instances));
    }

    let warm_start = Instant::now();
    std::hint::black_box(validate_all(&validator, &instances));
    let warm = warm_start.elapsed().as_nanos();

    println!("{cold},{warm},{compile},{parse}");
    if valid { ExitCode::SUCCESS } else { ExitCode::FAILURE }
}
