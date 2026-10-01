//! Compares corvus-json-schema with other Rust JSON Schema validators on the jsonschema-benchmark corpora
//! (https://github.com/sourcemeta-research/jsonschema-benchmark), in one process on one machine.
//!
//!   cargo run --release -- --schemas ../../../jsonschema-benchmark/schemas [--only a,b] [--engines corvus,boon]
//!                          [--budget-ms 1000] [--json results/run.json]
//!
//! For each corpus and engine: the schema (with `format` keywords removed, as in the benchmark's schema-noformat.json)
//! is compiled, every instance is validated once cold, and then whole passes over the instances are repeated for the
//! time budget after a warm-up. Every instance in the corpora is valid, so an engine that rejects one is reported.
//! The table shows the median warm pass time per engine; the JSON output also carries the minimum, cold pass and
//! compile times.

use std::path::{Path, PathBuf};
use std::time::{Duration, Instant};

use serde_json::{Map, Value, json};

type Check = Box<dyn Fn(&Value) -> bool>;

struct Engine {
    name: &'static str,
    compile: fn(&Value, &Path) -> Result<Check, String>,
}

fn corvus(schema: &Value, _: &Path) -> Result<Check, String> {
    let v = corvus_json_schema::compile(schema).map_err(|e| e.to_string())?;
    Ok(Box::new(move |x| v.is_valid(x)))
}

fn boon(schema: &Value, file: &Path) -> Result<Check, String> {
    let url = format!("file://{}", file.display());
    let mut schemas = boon::Schemas::new();
    let mut compiler = boon::Compiler::new();
    compiler.add_resource(&url, schema.clone()).map_err(|e| e.to_string())?;
    let index = compiler.compile(&url, &mut schemas).map_err(|e| e.to_string())?;
    Ok(Box::new(move |x| schemas.validate(x, index).is_ok()))
}

fn jsonschema_rs(schema: &Value, _: &Path) -> Result<Check, String> {
    let v = jsonschema::validator_for(schema).map_err(|e| e.to_string())?;
    Ok(Box::new(move |x| v.is_valid(x)))
}

const ENGINES: [Engine; 3] = [
    Engine { name: "corvus", compile: corvus },
    Engine { name: "boon", compile: boon },
    Engine { name: "jsonschema", compile: jsonschema_rs },
];

/// Removes string-valued `format` members, as the benchmark's `schema-noformat.json` does.
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

struct Measurement {
    compile: Duration,
    cold: Duration,
    warm_median: Duration,
    warm_min: Duration,
    passes: usize,
    rejected: usize,
}

fn pass(check: &Check, instances: &[Value]) -> usize {
    instances.iter().filter(|x| !check(x)).count()
}

/// Measures every engine on one corpus. Warm passes are interleaved (a round runs one pass of each engine, starting
/// from a different engine each round), so that drift in the machine's speed affects every engine alike.
fn measure_all(
    engines: &[&Engine],
    schema: &Value,
    file: &Path,
    instances: &[Value],
    budget: Duration,
) -> Vec<Result<Measurement, String>> {
    let mut compiled = Vec::new();
    for e in engines {
        let start = Instant::now();
        let check = (e.compile)(schema, file);
        let compile = start.elapsed();
        compiled.push(check.map(|c| {
            let start = Instant::now();
            let rejected = pass(&c, instances);
            (c, compile, start.elapsed(), rejected)
        }));
    }
    let live: Vec<usize> = (0..engines.len()).filter(|&i| compiled[i].is_ok()).collect();
    let round = |times: Option<&mut Vec<Vec<Duration>>>, r: usize| {
        let mut times = times;
        for k in 0..live.len() {
            let i = live[(r + k) % live.len()];
            let (check, ..) = compiled[i].as_ref().unwrap();
            let start = Instant::now();
            std::hint::black_box(pass(check, instances));
            if let Some(t) = times.as_deref_mut() {
                t[i].push(start.elapsed());
            }
        }
    };
    let total = budget * live.len().max(1) as u32;
    let warmup = Instant::now();
    let mut r = 0;
    while warmup.elapsed() < total / 5 {
        round(None, r);
        r += 1;
    }
    let mut times: Vec<Vec<Duration>> = vec![Vec::new(); engines.len()];
    let timed = Instant::now();
    while r < 5 || timed.elapsed() < total || times.iter().any(|t| !t.is_empty() && t.len() < 5) {
        round(Some(&mut times), r);
        r += 1;
    }
    compiled
        .into_iter()
        .zip(times)
        .map(|(c, mut t)| {
            let (_, compile, cold, rejected) = c?;
            t.sort();
            Ok(Measurement { compile, cold, warm_median: t[t.len() / 2], warm_min: t[0], passes: t.len(), rejected })
        })
        .collect()
}

fn micros(d: Duration) -> f64 {
    d.as_secs_f64() * 1e6
}

fn main() {
    let args: Vec<String> = std::env::args().collect();
    let arg = |name: &str| args.iter().position(|a| a == name).map(|i| args[i + 1].clone());
    let schemas = PathBuf::from(arg("--schemas").unwrap_or_else(|| "../../../jsonschema-benchmark/schemas".into()));
    let only: Option<Vec<String>> = arg("--only").map(|s| s.split(',').map(String::from).collect());
    let engines: Vec<&Engine> = match arg("--engines") {
        Some(list) => list.split(',').map(|n| ENGINES.iter().find(|e| e.name == n).expect("unknown engine")).collect(),
        None => ENGINES.iter().collect(),
    };
    let budget = Duration::from_millis(arg("--budget-ms").map_or(1000, |s| s.parse().unwrap()));
    // --profile N: run N passes of the first engine over each corpus and nothing else (for profilers).
    let profile: Option<usize> = arg("--profile").map(|s| s.parse().unwrap());

    let mut corpora: Vec<PathBuf> = std::fs::read_dir(&schemas)
        .unwrap_or_else(|e| panic!("{}: {e}", schemas.display()))
        .filter_map(|e| e.ok().map(|e| e.path()))
        .filter(|p| p.join("instances.jsonl").exists())
        .filter(|p| only.as_ref().is_none_or(|o| o.iter().any(|n| p.ends_with(n))))
        .collect();
    corpora.sort();

    print!("{:<24}", "corpus (warm µs/pass)");
    for e in &engines {
        print!(" {:>12}", e.name);
    }
    if engines.len() > 1 {
        print!(" {:>10}", "vs best");
    }
    println!();

    let mut rows = Vec::new();
    let mut ratios = Vec::new();
    for dir in &corpora {
        let name = dir.file_name().unwrap().to_string_lossy().to_string();
        let file = std::fs::canonicalize(dir.join("schema.json")).unwrap();
        let mut schema: Value = serde_json::from_str(&std::fs::read_to_string(&file).unwrap()).unwrap();
        strip_format(&mut schema);
        let instances: Vec<Value> = std::fs::read_to_string(dir.join("instances.jsonl"))
            .unwrap()
            .lines()
            .filter(|l| !l.is_empty())
            .map(|l| serde_json::from_str(l).unwrap())
            .collect();

        if let Some(passes) = profile {
            let check = (engines[0].compile)(&schema, &file).unwrap();
            let rejected: usize = (0..passes).map(|_| pass(&check, &instances)).sum();
            println!("{name}: {passes} passes, {rejected} rejected");
            continue;
        }
        print!("{name:<24}");
        let mut results = Map::new();
        let mut warm = Vec::new();
        let measured = measure_all(&engines, &schema, &file, &instances, budget);
        for (e, m) in engines.iter().zip(measured) {
            match m {
                Ok(m) => {
                    let flag = if m.rejected > 0 { "!" } else { "" };
                    print!(" {:>12}", format!("{flag}{:.1}", micros(m.warm_median)));
                    warm.push((m.rejected == 0).then_some(micros(m.warm_median)));
                    results.insert(
                        e.name.into(),
                        json!({
                            "compile_us": micros(m.compile),
                            "cold_us": micros(m.cold),
                            "warm_median_us": micros(m.warm_median),
                            "warm_min_us": micros(m.warm_min),
                            "passes": m.passes,
                            "rejected": m.rejected,
                        }),
                    );
                }
                Err(err) => {
                    print!(" {:>12}", "error");
                    warm.push(None);
                    results.insert(e.name.into(), json!({ "error": err }));
                }
            }
        }
        if engines.len() > 1
            && let Some(Some(ours)) = warm.first()
        {
            let best = warm[1..].iter().flatten().copied().fold(f64::INFINITY, f64::min);
            if best.is_finite() {
                print!(" {:>9.2}x", ours / best);
                ratios.push(ours / best);
            }
        }
        println!();
        rows.push(json!({ "corpus": name, "instances": instances.len(), "results": results }));
    }
    if !ratios.is_empty() {
        let geomean = (ratios.iter().map(|r| r.ln()).sum::<f64>() / ratios.len() as f64).exp();
        let wins = ratios.iter().filter(|r| **r < 1.0).count();
        println!(
            "\n{} vs the fastest other engine: geometric mean {geomean:.2}x (below 1 is faster), fastest on {wins}/{}",
            engines[0].name,
            ratios.len()
        );
    }
    if let Some(out) = arg("--json") {
        std::fs::write(
            &out,
            serde_json::to_string_pretty(&json!({ "budget_ms": budget.as_millis() as u64, "corpora": rows })).unwrap(),
        )
        .unwrap();
    }
}
