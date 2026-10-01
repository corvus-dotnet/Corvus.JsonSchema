// Records the compiler's version for the harness's `start` response.
fn main() {
    let rustc = std::env::var("RUSTC").unwrap_or_else(|_| "rustc".into());
    let version = std::process::Command::new(rustc)
        .arg("--version")
        .output()
        .ok()
        .and_then(|o| String::from_utf8(o.stdout).ok())
        .map(|v| v.trim().to_string())
        .unwrap_or_default();
    println!("cargo:rustc-env=HARNESS_RUSTC_VERSION={version}");
    println!("cargo:rerun-if-changed=build.rs");
}
