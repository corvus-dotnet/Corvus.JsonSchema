//! Drives the harness over IHOP as Bowtie does (start, dialect, run with each test case's externalSchemas as the
//! registry, stop): every required case of the JSON-Schema-Test-Suite, and the annotation suite's assertions, compared
//! with the expected results. No containers.

use std::io::{BufRead, BufReader, Write};
use std::path::{Path, PathBuf};
use std::process::{Child, ChildStdin, ChildStdout, Command, Stdio};

use serde_json::{Map, Value, json};

const DRAFTS: [(&str, &str, u32); 5] = [
    ("draft4", "http://json-schema.org/draft-04/schema#", 4),
    ("draft6", "http://json-schema.org/draft-06/schema#", 6),
    ("draft7", "http://json-schema.org/draft-07/schema#", 7),
    ("draft2019-09", "https://json-schema.org/draft/2019-09/schema", 2019),
    ("draft2020-12", "https://json-schema.org/draft/2020-12/schema", 2020),
];

struct Harness {
    child: Child,
    input: ChildStdin,
    output: BufReader<ChildStdout>,
}

impl Harness {
    fn start() -> Harness {
        let mut child = Command::new(env!("CARGO_BIN_EXE_bowtie-corvus-jsonschema"))
            .stdin(Stdio::piped())
            .stdout(Stdio::piped())
            .spawn()
            .expect("start the harness");
        let input = child.stdin.take().unwrap();
        let output = BufReader::new(child.stdout.take().unwrap());
        let mut harness = Harness { child, input, output };
        let started = harness.send(&json!({ "cmd": "start", "version": 1 }));
        assert_eq!(started["implementation"]["language"], "rust");
        harness
    }

    fn send(&mut self, request: &Value) -> Value {
        writeln!(self.input, "{request}").unwrap();
        let mut line = String::new();
        self.output.read_line(&mut line).unwrap();
        serde_json::from_str(&line).unwrap_or_else(|e| panic!("{e}: {line:?}"))
    }

    fn stop(mut self) {
        writeln!(self.input, "{}", json!({ "cmd": "stop" })).unwrap();
        assert!(self.child.wait().unwrap().success());
    }
}

fn suite_root() -> Option<PathBuf> {
    let root = std::env::var_os("JSON_SCHEMA_TEST_SUITE")
        .map(PathBuf::from)
        .unwrap_or_else(|| Path::new(env!("CARGO_MANIFEST_DIR")).join("../../JSON-Schema-Test-Suite"));
    root.join("validation").join("tests").exists().then_some(root)
}

fn read_json(path: &Path) -> Value {
    serde_json::from_str(&std::fs::read_to_string(path).unwrap()).unwrap()
}

/// Bowtie's registry for a test case: its externalSchemas, keyed by their retrieval URIs.
fn registry(case: &Value) -> Value {
    case.get("externalSchemas").cloned().unwrap_or_else(|| Value::Object(Map::new()))
}

/// Whether a test case's `compatibility` admits the release, by the algorithm of the suite's validation/README.md.
fn compatible(compatibility: Option<&str>, release: u32) -> bool {
    let Some(compatibility) = compatibility else { return true };
    let mut valid = true;
    for constraint in compatibility.split(',') {
        let (op, version) = if let Some(v) = constraint.strip_prefix("<=") {
            ("<=", v)
        } else if let Some(v) = constraint.strip_prefix('=') {
            ("=", v)
        } else {
            ("", constraint)
        };
        let version: u32 = version.trim().parse().unwrap();
        valid = match op {
            "<=" => release <= version,
            "=" => release == version,
            _ => release >= version,
        };
        if release <= version {
            break;
        }
    }
    valid
}

fn has_tag(case: &Value, tag: &str) -> bool {
    case["tags"].as_array().is_some_and(|tags| tags.iter().any(|t| t == tag))
}

fn json_files(dir: &Path) -> Vec<PathBuf> {
    let mut files: Vec<PathBuf> = std::fs::read_dir(dir)
        .unwrap()
        .filter_map(|e| e.ok().map(|e| e.path()))
        .filter(|p| p.extension().is_some_and(|x| x == "json"))
        .collect();
    files.sort();
    files
}

#[test]
fn required_suite_over_ihop() {
    let Some(root) = suite_root() else {
        eprintln!("JSON-Schema-Test-Suite not found; skipping");
        return;
    };
    let mut harness = Harness::start();
    let (mut seq, mut total, mut failures) = (0, 0, Vec::new());
    for (draft, dialect, release) in DRAFTS {
        assert_eq!(harness.send(&json!({ "cmd": "dialect", "dialect": dialect }))["ok"], true);
        for file in json_files(&root.join("validation").join("tests")) {
            let file_name = file.file_name().unwrap().to_string_lossy().to_string();
            for group in read_json(&file)["tests"].as_array().unwrap() {
                if !compatible(group["compatibility"].as_str(), release) {
                    continue;
                }
                // The harness treats format as an annotation in every dialect, as Bowtie expects.
                if has_tag(group, "format-assertion") {
                    continue;
                }
                seq += 1;
                let tests: Vec<Value> = group["tests"]
                    .as_array()
                    .unwrap()
                    .iter()
                    .map(|t| json!({ "description": t["description"], "instance": t["data"] }))
                    .collect();
                let request = json!({
                    "cmd": "run",
                    "seq": seq,
                    "case": { "description": group["description"], "schema": group["schema"], "registry": registry(group), "tests": tests },
                });
                let response = harness.send(&request);
                assert_eq!(response["seq"], seq);
                for (i, test) in group["tests"].as_array().unwrap().iter().enumerate() {
                    total += 1;
                    let result = &response["results"][i];
                    if response["errored"] == true || result["valid"] != test["valid"] {
                        failures.push(format!(
                            "{draft}/{file_name}: {} / {}: {response}",
                            group["description"], test["description"]
                        ));
                    }
                }
            }
        }
    }
    harness.stop();
    assert!(failures.is_empty(), "{} of {total} failed:\n{}", failures.len(), failures.join("\n"));
    assert!(total > 5000, "only {total} tests ran");
}

/// JSON equality with numbers compared by value.
fn same(a: &Value, b: &Value) -> bool {
    match (a, b) {
        (Value::Number(x), Value::Number(y)) => x.as_f64() == y.as_f64(),
        (Value::Array(x), Value::Array(y)) => x.len() == y.len() && x.iter().zip(y).all(|(x, y)| same(x, y)),
        (Value::Object(x), Value::Object(y)) => {
            x.len() == y.len() && x.iter().all(|(k, v)| y.get(k).is_some_and(|w| same(v, w)))
        }
        _ => a == b,
    }
}

#[test]
fn annotation_suite_over_ihop() {
    let Some(root) = suite_root() else {
        eprintln!("JSON-Schema-Test-Suite not found; skipping");
        return;
    };
    let dir = root.join("annotations").join("tests");
    if !dir.exists() {
        eprintln!("annotation tests not found; skipping");
        return;
    }
    let mut harness = Harness::start();
    let (mut seq, mut total, mut failures) = (0, 0, Vec::new());
    for (_, dialect, release) in DRAFTS {
        assert_eq!(harness.send(&json!({ "cmd": "dialect", "dialect": dialect }))["ok"], true);
        for file in json_files(&dir) {
            for group in read_json(&file)["suite"].as_array().unwrap() {
                if !compatible(group["compatibility"].as_str(), release) {
                    continue;
                }
                seq += 1;
                let tests: Vec<Value> = group["tests"]
                    .as_array()
                    .unwrap()
                    .iter()
                    .map(|t| json!({ "description": "", "instance": t["instance"] }))
                    .collect();
                let request = json!({
                    "cmd": "run",
                    "seq": seq,
                    "output": "annotations",
                    "case": { "description": group["description"], "schema": group["schema"], "registry": registry(group), "tests": tests },
                });
                let response = harness.send(&request);
                for (i, test) in group["tests"].as_array().unwrap().iter().enumerate() {
                    let found = response["results"][i]["annotations"].as_array().cloned().unwrap_or_default();
                    for assertion in test["assertions"].as_array().unwrap() {
                        total += 1;
                        // The annotations for this instance location and keyword, by keyword location.
                        let actual: Map<String, Value> = found
                            .iter()
                            .filter(|a| {
                                a["instanceLocation"] == assertion["location"] && a["keyword"] == assertion["keyword"]
                            })
                            .map(|a| {
                                let at = a["keywordLocation"].as_str().unwrap();
                                let schema =
                                    at.strip_suffix(&format!("/{}", a["keyword"].as_str().unwrap())).unwrap_or(at);
                                (schema.to_string(), a["annotation"].clone())
                            })
                            .collect();
                        if !same(&Value::Object(actual.clone()), &assertion["expected"]) {
                            failures.push(format!(
                                "{} ({dialect}): {} {}: expected {} got {}",
                                file.file_name().unwrap().to_string_lossy(),
                                assertion["location"],
                                assertion["keyword"],
                                assertion["expected"],
                                Value::Object(actual)
                            ));
                        }
                    }
                }
            }
        }
    }
    harness.stop();
    assert!(failures.is_empty(), "{} of {total} failed:\n{}", failures.len(), failures.join("\n"));
}
