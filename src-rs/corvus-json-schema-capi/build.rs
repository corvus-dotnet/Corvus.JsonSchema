//! The shared library's name for the dynamic loader: without a SONAME (Linux) or an @rpath install name (macOS), a
//! program linked against the library by its full path would record that path and fail to load it anywhere else.
fn main() {
    let os = std::env::var("CARGO_CFG_TARGET_OS").unwrap_or_default();
    match os.as_str() {
        "linux" | "android" | "freebsd" => {
            println!("cargo:rustc-cdylib-link-arg=-Wl,-soname,libcorvus_json_schema.so");
        }
        "macos" | "ios" => {
            println!("cargo:rustc-cdylib-link-arg=-Wl,-install_name,@rpath/libcorvus_json_schema.dylib");
        }
        _ => {}
    }
}
