//! The Arazzo micro-guest sidecar entry point (ADR 0063). See `lib.rs` for the surfaces and the model.

use std::sync::atomic::AtomicBool;
use std::sync::Arc;

use clap::Parser;

/// The warm micro-guest sidecar: one evolved, snapshotted Hyperlight/Unikraft sandbox per (environment,
/// version), restored hermetically per advance (ADR 0063).
#[derive(Parser)]
#[command(name = "arazzo-microguest-sidecar")]
struct Args {
    /// The pinned guest kernel image (built by the kernel build script; its baked exec path must match the
    /// deployer's guest binary path).
    #[arg(long, env = "ARAZZO_SIDECAR_KERNEL")]
    kernel: std::path::PathBuf,

    /// The admin surface bind (the runner's side: staging, evolve, invoke). Keep it loopback.
    #[arg(long, env = "ARAZZO_SIDECAR_ADMIN_BIND", default_value = "127.0.0.1:9411")]
    admin_bind: String,

    /// The address the invoke URLs advertise; defaults to the admin bind.
    #[arg(long, env = "ARAZZO_SIDECAR_ADMIN_ADVERTISE")]
    admin_advertise: Option<String>,

    /// The guest surface bind. The guest's host-proxied network denies loopback, so this must be reachable on
    /// a routable address.
    #[arg(long, env = "ARAZZO_SIDECAR_GUEST_BIND", default_value = "0.0.0.0:9412")]
    guest_bind: String,

    /// The host:port the guests reach the guest surface at (baked into each sandbox's argv and added to its
    /// egress allowlist). There is no reliable autodetection, so it is explicit.
    #[arg(long, env = "ARAZZO_SIDECAR_GUEST_ADVERTISE")]
    guest_advertise: String,

    /// HTTP worker threads per surface.
    #[arg(long, default_value_t = 4)]
    workers: usize,

    /// Where sandbox initrd images are staged on disk (mapped copy-on-write into each sandbox; the files
    /// live for the sandbox's life). Defaults to arazzo-microguest-sidecar under the system temp directory.
    #[arg(long, env = "ARAZZO_SIDECAR_STATE_DIR")]
    state_dir: Option<std::path::PathBuf>,
}

fn main() -> anyhow::Result<()> {
    let args = Args::parse();

    let factory = build_factory(&args)?;
    let addresses = arazzo_microguest_sidecar::SidecarAddresses {
        admin_advertise: args.admin_advertise.clone().unwrap_or_else(|| args.admin_bind.clone()),
        guest_advertise: args.guest_advertise.clone(),
    };
    let sidecar = Arc::new(arazzo_microguest_sidecar::Sidecar::new(addresses, factory));

    let admin = Arc::new(
        tiny_http::Server::http(args.admin_bind.as_str())
            .map_err(|error| anyhow::anyhow!("binding the admin surface at {} failed: {error}", args.admin_bind))?,
    );
    let guest = Arc::new(
        tiny_http::Server::http(args.guest_bind.as_str())
            .map_err(|error| anyhow::anyhow!("binding the guest surface at {} failed: {error}", args.guest_bind))?,
    );

    eprintln!(
        "arazzo-microguest-sidecar: admin on {}, guest surface on {} (advertised {}), kernel {:?}",
        args.admin_bind, args.guest_bind, args.guest_advertise, args.kernel
    );

    let stopping = Arc::new(AtomicBool::new(false));
    let mut handles = arazzo_microguest_sidecar::serve(
        admin,
        Arc::clone(&sidecar),
        args.workers,
        arazzo_microguest_sidecar::handle_admin,
        Arc::clone(&stopping),
    );
    handles.extend(arazzo_microguest_sidecar::serve(
        guest,
        sidecar,
        args.workers,
        arazzo_microguest_sidecar::handle_guest,
        stopping,
    ));

    for handle in handles {
        let _ = handle.join();
    }

    Ok(())
}

#[cfg(feature = "hyperlight")]
fn build_factory(args: &Args) -> anyhow::Result<Arc<dyn arazzo_microguest_sidecar::VmFactory>> {
    if !args.kernel.exists() {
        anyhow::bail!("the guest kernel {:?} does not exist", args.kernel);
    }

    let state_dir = args
        .state_dir
        .clone()
        .unwrap_or_else(|| std::env::temp_dir().join("arazzo-microguest-sidecar"));
    Ok(Arc::new(arazzo_microguest_sidecar::hyperlight::HyperlightVmFactory { kernel: args.kernel.clone(), state_dir }))
}

#[cfg(not(feature = "hyperlight"))]
fn build_factory(_args: &Args) -> anyhow::Result<Arc<dyn arazzo_microguest_sidecar::VmFactory>> {
    anyhow::bail!("this sidecar was built without the 'hyperlight' feature and cannot run sandboxes")
}
