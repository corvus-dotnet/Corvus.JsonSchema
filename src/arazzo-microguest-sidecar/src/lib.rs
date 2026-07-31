//! The Arazzo micro-guest sidecar (ADR 0063).
//!
//! The sidecar holds an evolved, snapshotted Hyperlight/Unikraft micro-VM sandbox per (environment, version) and
//! restores it hermetically per advance. It exposes two HTTP surfaces:
//!
//! * The **admin surface** (loopback, for the runner): `PUT /sandboxes/{id}/initrd` stages the guest image,
//!   `PUT /sandboxes/{id}` evolves the sandbox from it (returning the invoke URL the deployer records as the
//!   deployment's function URL), `DELETE /sandboxes/{id}` tears it down, and `POST /invoke/{id}` advances a run:
//!   the invocation body is held for the guest, the snapshot is restored, the guest runs, and the outcome the
//!   guest posted back is returned to the runner — the same invocation/outcome contract a deployed cloud
//!   function speaks.
//! * The **guest surface** (a routable bind — the guest's host-proxied network denies loopback by design):
//!   `GET /guest/{id}` hands the running guest its invocation, `POST /guest/{id}` receives its outcome.
//!
//! Sandbox state lives on a dedicated owner thread per sandbox: the VM is built, restored, and run on that one
//! thread (so the underlying sandbox never crosses threads), and invocations serialize per sandbox by
//! construction — each advance runs against a pristine snapshot restore. The real VM sits behind [`VmFactory`]
//! (the `hyperlight` feature); everything else unit-tests without KVM.

use std::collections::{BTreeMap, HashMap};
use std::io::Read;
use std::sync::atomic::{AtomicBool, Ordering};
use std::sync::mpsc;
use std::sync::{Arc, Mutex};
use std::time::Duration;

use serde::Deserialize;

/// How long a `PUT /sandboxes/{id}` waits for the owner thread to report the evolve result.
const EVOLVE_TIMEOUT: Duration = Duration::from_secs(120);

/// How long a `POST /invoke/{id}` waits for the guest to run to completion. Runs advance one step and
/// checkpoint over HTTP, so minutes-long waits are legitimate (source calls ride the guest's own socket
/// timeouts); this only bounds a wedged guest.
const INVOKE_TIMEOUT: Duration = Duration::from_secs(900);

/// The largest accepted initrd upload (the baked guest is tens of MiB; this bounds a runaway request).
const MAX_INITRD_BYTES: usize = 512 * 1024 * 1024;

/// The largest accepted JSON/invocation body.
const MAX_DOCUMENT_BYTES: usize = 4 * 1024 * 1024;

/// Everything a VM build needs: the staged image, the frozen argv, the VM size, and the egress allowlist
/// (already port-stripped, with the sidecar's own guest host included so the guest can fetch its invocation).
#[derive(Clone, Debug, PartialEq)]
pub struct SandboxSpec {
    pub initrd: Vec<u8>,
    pub args: Vec<String>,
    pub memory_mib: u64,
    pub allowed_hosts: Vec<String>,
}

/// One warm micro-VM. `run_once` restores the post-evolve snapshot, runs the guest to completion, and returns
/// its exit code. Implementations are used only on the owner thread that built them.
pub trait MicroVm {
    fn run_once(&mut self) -> anyhow::Result<i32>;
}

/// Builds a [`MicroVm`] from a spec. Called on the sandbox's owner thread, so the built VM never crosses
/// threads. The exchange is the in-process channel the guest surface serves from; the real factory ignores it
/// (the real guest talks HTTP), a test factory may use it to stand in for the guest.
pub trait VmFactory: Send + Sync {
    fn build(&self, spec: &SandboxSpec, exchange: Arc<GuestExchange>) -> anyhow::Result<Box<dyn MicroVm>>;
}

/// The per-sandbox rendezvous between an in-flight invocation and the guest surface: the invocation the
/// running guest fetches, and the outcome it posts back.
#[derive(Default)]
pub struct GuestExchange {
    pending: Mutex<Option<Vec<u8>>>,
    outcome: Mutex<Option<Vec<u8>>>,
}

impl GuestExchange {
    /// The invocation the running guest fetches (`GET /guest/{id}`), if one is in flight.
    pub fn pending(&self) -> Option<Vec<u8>> {
        self.pending.lock().unwrap().clone()
    }

    /// Stores the outcome the guest posted (`POST /guest/{id}`).
    pub fn post_outcome(&self, outcome: Vec<u8>) {
        *self.outcome.lock().unwrap() = Some(outcome);
    }

    fn begin(&self, invocation: Vec<u8>) {
        *self.pending.lock().unwrap() = Some(invocation);
        *self.outcome.lock().unwrap() = None;
    }

    fn finish(&self) -> Option<Vec<u8>> {
        *self.pending.lock().unwrap() = None;
        self.outcome.lock().unwrap().take()
    }
}

/// The sandbox configuration document the deployer PUTs (see the deployer README for the wire contract).
#[derive(Deserialize)]
#[serde(rename_all = "camelCase", deny_unknown_fields)]
struct SandboxConfigDocument {
    #[serde(default = "default_memory_mib")]
    memory_mib: u64,
    #[serde(default)]
    allowed_hosts: Vec<String>,
    /// A BTreeMap so the baked argv order is deterministic for identical configuration.
    #[serde(default)]
    environment: BTreeMap<String, String>,
}

fn default_memory_mib() -> u64 {
    64
}

enum Command {
    Invoke {
        invocation: Vec<u8>,
        reply: mpsc::Sender<Result<Vec<u8>, String>>,
    },
    Shutdown,
}

struct Worker {
    tx: mpsc::Sender<Command>,
    exchange: Arc<GuestExchange>,
    join: std::thread::JoinHandle<()>,
}

#[derive(Default)]
struct Entry {
    staged_initrd: Option<Vec<u8>>,
    worker: Option<Worker>,
}

/// The advertised addresses the sidecar builds its URLs from: `admin_advertise` names the invoke endpoint the
/// runner calls (typically the loopback admin bind), `guest_advertise` names the routable guest surface baked
/// into each sandbox's argv.
#[derive(Clone, Debug)]
pub struct SidecarAddresses {
    pub admin_advertise: String,
    pub guest_advertise: String,
}

/// The sidecar's sandbox registry and request handlers, shared across the HTTP server threads.
pub struct Sidecar {
    addresses: SidecarAddresses,
    factory: Arc<dyn VmFactory>,
    sandboxes: Mutex<HashMap<String, Entry>>,
}

/// A plain HTTP result: status code, content type, body.
pub struct HttpReply {
    pub status: u16,
    pub content_type: &'static str,
    pub body: Vec<u8>,
}

impl HttpReply {
    fn json(status: u16, body: String) -> Self {
        Self { status, content_type: "application/json", body: body.into_bytes() }
    }

    fn text(status: u16, body: impl Into<String>) -> Self {
        Self { status, content_type: "text/plain; charset=utf-8", body: body.into().into_bytes() }
    }

    fn empty(status: u16) -> Self {
        Self { status, content_type: "text/plain; charset=utf-8", body: Vec::new() }
    }
}

impl Sidecar {
    pub fn new(addresses: SidecarAddresses, factory: Arc<dyn VmFactory>) -> Self {
        Self { addresses, factory, sandboxes: Mutex::new(HashMap::new()) }
    }

    /// `PUT /sandboxes/{id}/initrd`: stage the guest image. Staging never touches a live worker, so a failed
    /// upload cannot replace a running snapshot.
    pub fn stage_initrd(&self, id: &str, initrd: Vec<u8>) -> HttpReply {
        if !valid_id(id) {
            return HttpReply::text(400, format!("invalid sandbox id '{id}'"));
        }

        if initrd.is_empty() {
            return HttpReply::text(400, "the initrd body is empty");
        }

        let mut sandboxes = self.sandboxes.lock().unwrap();
        sandboxes.entry(id.to_string()).or_default().staged_initrd = Some(initrd);
        HttpReply::empty(204)
    }

    /// `PUT /sandboxes/{id}`: evolve the sandbox from the staged initrd. Replaces any previous worker (a
    /// redeploy) only after the new spec is accepted for building.
    pub fn configure(&self, id: &str, body: &[u8]) -> HttpReply {
        if !valid_id(id) {
            return HttpReply::text(400, format!("invalid sandbox id '{id}'"));
        }

        let document: SandboxConfigDocument = match serde_json::from_slice(body) {
            Ok(document) => document,
            Err(error) => return HttpReply::text(400, format!("invalid sandbox configuration: {error}")),
        };

        let staged = {
            let mut sandboxes = self.sandboxes.lock().unwrap();
            match sandboxes.entry(id.to_string()).or_default().staged_initrd.clone() {
                Some(staged) => staged,
                None => {
                    return HttpReply::text(409, format!("no initrd is staged for sandbox '{id}'; PUT /sandboxes/{id}/initrd first"));
                }
            }
        };

        // The frozen argv: the guest surface URL first (the entry template's argv[1]), then the environment
        // pairs in deterministic order. Per-run data never rides argv — it arrives over the guest surface.
        let mut args = vec![format!("http://{}/guest/{}", self.addresses.guest_advertise, id)];
        args.extend(document.environment.iter().map(|(name, value)| format!("{name}={value}")));

        // The egress allowlist is IP/host-level (the policy layer matches by address, not port), so the
        // deployer's host:port entries are stripped to hosts; the sidecar's own guest host joins the list so
        // the guest can fetch its invocation and post its outcome.
        let mut allowed_hosts: Vec<String> = Vec::new();
        for entry in document
            .allowed_hosts
            .iter()
            .map(|entry| strip_port(entry).to_string())
            .chain(std::iter::once(strip_port(&self.addresses.guest_advertise).to_string()))
        {
            if !entry.is_empty() && !allowed_hosts.contains(&entry) {
                allowed_hosts.push(entry);
            }
        }

        let spec = SandboxSpec { initrd: staged, args, memory_mib: document.memory_mib, allowed_hosts };

        let exchange = Arc::new(GuestExchange::default());
        let (command_tx, command_rx) = mpsc::channel();
        let (built_tx, built_rx) = mpsc::channel();
        let factory = Arc::clone(&self.factory);
        let thread_exchange = Arc::clone(&exchange);
        let join = std::thread::spawn(move || owner_thread(factory, spec, thread_exchange, command_rx, built_tx));

        // The evolve happens on the owner thread (the VM must live where it runs); wait for its verdict here
        // so the deployer sees an evolve failure as a failed deploy, not a broken sandbox later.
        match built_rx.recv_timeout(EVOLVE_TIMEOUT) {
            Ok(Ok(())) => {}
            Ok(Err(error)) => {
                let _ = join.join();
                return HttpReply::text(422, format!("evolving sandbox '{id}' failed: {error}"));
            }
            Err(_) => {
                return HttpReply::text(504, format!("evolving sandbox '{id}' did not complete within {}s", EVOLVE_TIMEOUT.as_secs()));
            }
        }

        let previous = {
            let mut sandboxes = self.sandboxes.lock().unwrap();
            sandboxes
                .entry(id.to_string())
                .or_default()
                .worker
                .replace(Worker { tx: command_tx, exchange, join })
        };
        shutdown(previous);

        HttpReply::json(
            200,
            format!("{{\"invokeUrl\":\"http://{}/invoke/{}\"}}", self.addresses.admin_advertise, id),
        )
    }

    /// `DELETE /sandboxes/{id}`: tear the sandbox down (worker and staged image both).
    pub fn remove(&self, id: &str) -> HttpReply {
        let removed = self.sandboxes.lock().unwrap().remove(id);
        match removed {
            Some(entry) => {
                shutdown(entry.worker);
                HttpReply::empty(204)
            }
            None => HttpReply::text(404, format!("no sandbox '{id}'")),
        }
    }

    /// `POST /invoke/{id}`: one run advance — the serverless invocation contract. The invocation is held for
    /// the guest, the snapshot restores, the guest runs and posts its outcome, and that outcome is the reply.
    pub fn invoke(&self, id: &str, invocation: Vec<u8>) -> HttpReply {
        let tx = {
            let sandboxes = self.sandboxes.lock().unwrap();
            match sandboxes.get(id).and_then(|entry| entry.worker.as_ref()) {
                Some(worker) => worker.tx.clone(),
                None => {
                    return HttpReply::text(409, format!("sandbox '{id}' is not evolved; deploy it first"));
                }
            }
        };

        let (reply_tx, reply_rx) = mpsc::channel();
        if tx.send(Command::Invoke { invocation, reply: reply_tx }).is_err() {
            return HttpReply::text(502, format!("sandbox '{id}' worker has stopped"));
        }

        match reply_rx.recv_timeout(INVOKE_TIMEOUT) {
            Ok(Ok(outcome)) => HttpReply { status: 200, content_type: "application/json", body: outcome },
            Ok(Err(error)) => HttpReply::text(502, format!("invoking sandbox '{id}' failed: {error}")),
            Err(_) => HttpReply::text(504, format!("sandbox '{id}' did not complete within {}s", INVOKE_TIMEOUT.as_secs())),
        }
    }

    /// `GET /guest/{id}`: the running guest fetches its invocation.
    pub fn guest_fetch(&self, id: &str) -> HttpReply {
        let exchange = self.exchange(id);
        match exchange.and_then(|exchange| exchange.pending()) {
            Some(invocation) => HttpReply { status: 200, content_type: "application/json", body: invocation },
            None => HttpReply::text(404, format!("no invocation is in flight for sandbox '{id}'")),
        }
    }

    /// `POST /guest/{id}`: the running guest posts its outcome.
    pub fn guest_outcome(&self, id: &str, outcome: Vec<u8>) -> HttpReply {
        match self.exchange(id) {
            Some(exchange) => {
                exchange.post_outcome(outcome);
                HttpReply::empty(204)
            }
            None => HttpReply::text(404, format!("no sandbox '{id}'")),
        }
    }

    fn exchange(&self, id: &str) -> Option<Arc<GuestExchange>> {
        let sandboxes = self.sandboxes.lock().unwrap();
        sandboxes.get(id).and_then(|entry| entry.worker.as_ref()).map(|worker| Arc::clone(&worker.exchange))
    }
}

fn shutdown(worker: Option<Worker>) {
    if let Some(worker) = worker {
        let _ = worker.tx.send(Command::Shutdown);
        drop(worker.tx);
        let _ = worker.join.join();
    }
}

/// The owner thread: builds the VM here (it never crosses threads), reports the evolve verdict, then serves
/// invocations one at a time — each a hermetic snapshot restore plus a run to completion.
fn owner_thread(
    factory: Arc<dyn VmFactory>,
    spec: SandboxSpec,
    exchange: Arc<GuestExchange>,
    commands: mpsc::Receiver<Command>,
    built: mpsc::Sender<Result<(), String>>,
) {
    let mut vm = match factory.build(&spec, Arc::clone(&exchange)) {
        Ok(vm) => {
            let _ = built.send(Ok(()));
            vm
        }
        Err(error) => {
            let _ = built.send(Err(format!("{error:#}")));
            return;
        }
    };

    while let Ok(command) = commands.recv() {
        match command {
            Command::Shutdown => return,
            Command::Invoke { invocation, reply } => {
                exchange.begin(invocation);
                let run = vm.run_once();
                let outcome = exchange.finish();
                let verdict = match (run, outcome) {
                    // The posted outcome is the contract; the exit code is diagnostic.
                    (Ok(_), Some(outcome)) => Ok(outcome),
                    (Ok(code), None) => Err(format!("the guest exited with code {code} without posting an outcome")),
                    (Err(error), _) => Err(format!("the guest run failed: {error:#}")),
                };
                let _ = reply.send(verdict);
            }
        }
    }
}

fn valid_id(id: &str) -> bool {
    !id.is_empty()
        && id.len() <= 64
        && id.chars().all(|c| c.is_ascii_alphanumeric() || c == '-' || c == '_')
}

/// Strips a trailing `:port` (the allowlist matches by host/IP, not port). Bare hosts pass through.
fn strip_port(entry: &str) -> &str {
    match entry.rsplit_once(':') {
        Some((host, port)) if !port.is_empty() && port.bytes().all(|b| b.is_ascii_digit()) => host,
        _ => entry,
    }
}

/// Routes one admin-surface request.
pub fn handle_admin(sidecar: &Sidecar, method: &str, path: &str, body: Vec<u8>) -> HttpReply {
    let segments: Vec<&str> = path.trim_matches('/').split('/').collect();
    match (method, segments.as_slice()) {
        ("GET", ["healthz"]) => HttpReply::text(200, "ok"),
        ("PUT", ["sandboxes", id, "initrd"]) => sidecar.stage_initrd(id, body),
        ("PUT", ["sandboxes", id]) => sidecar.configure(id, &body),
        ("DELETE", ["sandboxes", id]) => sidecar.remove(id),
        ("POST", ["invoke", id]) => sidecar.invoke(id, body),
        _ => HttpReply::text(404, format!("no {method} {path}")),
    }
}

/// Routes one guest-surface request.
pub fn handle_guest(sidecar: &Sidecar, method: &str, path: &str, body: Vec<u8>) -> HttpReply {
    let segments: Vec<&str> = path.trim_matches('/').split('/').collect();
    match (method, segments.as_slice()) {
        ("GET", ["guest", id]) => sidecar.guest_fetch(id),
        ("POST", ["guest", id]) => sidecar.guest_outcome(id, body),
        _ => HttpReply::text(404, format!("no {method} {path}")),
    }
}

/// Serves one `tiny_http` server with `workers` threads routing through `route`. Returns the join handles.
pub fn serve(
    server: Arc<tiny_http::Server>,
    sidecar: Arc<Sidecar>,
    workers: usize,
    route: fn(&Sidecar, &str, &str, Vec<u8>) -> HttpReply,
    stopping: Arc<AtomicBool>,
) -> Vec<std::thread::JoinHandle<()>> {
    (0..workers.max(1))
        .map(|_| {
            let server = Arc::clone(&server);
            let sidecar = Arc::clone(&sidecar);
            let stopping = Arc::clone(&stopping);
            std::thread::spawn(move || loop {
                match server.recv_timeout(Duration::from_millis(250)) {
                    Ok(Some(mut request)) => {
                        let method = request.method().as_str().to_string();
                        let path = request.url().split('?').next().unwrap_or("").to_string();
                        let limit = if path.ends_with("/initrd") { MAX_INITRD_BYTES } else { MAX_DOCUMENT_BYTES };
                        let mut body = Vec::new();
                        let read = request.as_reader().take(limit as u64 + 1).read_to_end(&mut body);
                        let reply = match read {
                            Ok(_) if body.len() > limit => HttpReply::text(413, "request body too large"),
                            Ok(_) => route(&sidecar, &method, &path, body),
                            Err(error) => HttpReply::text(400, format!("reading the request body failed: {error}")),
                        };
                        let response = tiny_http::Response::from_data(reply.body)
                            .with_status_code(reply.status)
                            .with_header(
                                tiny_http::Header::from_bytes(&b"Content-Type"[..], reply.content_type.as_bytes())
                                    .expect("static header"),
                            );
                        let _ = request.respond(response);
                    }
                    Ok(None) | Err(_) => {
                        if stopping.load(Ordering::Relaxed) {
                            return;
                        }
                    }
                }
            })
        })
        .collect()
}

/// The real VM behind the seam: hyperlight-unikraft's evolved sandbox, restored per run (ADR 0063).
#[cfg(feature = "hyperlight")]
pub mod hyperlight {
    use std::path::PathBuf;
    use std::sync::atomic::{AtomicU64, Ordering};
    use std::sync::Arc;

    use hyperlight_unikraft::{AllowList, NetworkPolicy, Sandbox};

    use crate::{GuestExchange, MicroVm, SandboxSpec, VmFactory};

    /// Builds real sandboxes over the pinned guest kernel. The staged initrd is written to a file in the
    /// state directory and the sandbox is built in mapped-initrd mode (`initrd_file`): that is the path the
    /// ADR 0063 spike proved delivers the frozen argv to the guest, and it maps the image copy-on-write, so
    /// each restore resets it without holding a second in-memory copy. The file must outlive the sandbox
    /// (every restore re-maps it); the VM removes it when it is dropped.
    pub struct HyperlightVmFactory {
        pub kernel: PathBuf,
        pub state_dir: PathBuf,
    }

    static NEXT_IMAGE: AtomicU64 = AtomicU64::new(0);

    impl VmFactory for HyperlightVmFactory {
        fn build(&self, spec: &SandboxSpec, _exchange: Arc<GuestExchange>) -> anyhow::Result<Box<dyn MicroVm>> {
            std::fs::create_dir_all(&self.state_dir)?;
            let initrd_path = self.state_dir.join(format!(
                "initrd-{}-{}.cpio",
                std::process::id(),
                NEXT_IMAGE.fetch_add(1, Ordering::Relaxed)
            ));
            std::fs::write(&initrd_path, &spec.initrd)?;

            let policy = NetworkPolicy::AllowList(AllowList::from_hosts(&spec.allowed_hosts)?);
            let built = Sandbox::builder(&self.kernel)
                .initrd_file(&initrd_path)
                .args(spec.args.iter().cloned())
                .heap_size(spec.memory_mib * 1024 * 1024)
                .network(policy)
                .build();
            match built {
                Ok(sandbox) => Ok(Box::new(HyperlightVm { sandbox, initrd_path })),
                Err(error) => {
                    let _ = std::fs::remove_file(&initrd_path);
                    Err(error)
                }
            }
        }
    }

    struct HyperlightVm {
        sandbox: Sandbox,
        initrd_path: PathBuf,
    }

    impl Drop for HyperlightVm {
        fn drop(&mut self) {
            let _ = std::fs::remove_file(&self.initrd_path);
        }
    }

    impl MicroVm for HyperlightVm {
        fn run_once(&mut self) -> anyhow::Result<i32> {
            self.sandbox.restore()?;
            self.sandbox.reset_exit_code();
            self.sandbox.call_run()?;
            Ok(self.sandbox.last_exit_code())
        }
    }
}

#[cfg(test)]
mod tests {
    use std::net::TcpListener;
    use std::sync::atomic::AtomicBool;
    use std::sync::{Arc, Mutex};

    use super::*;

    /// The stand-in guest: on each run it does exactly what the baked entry template does — GET the invocation
    /// from argv[1] over real HTTP, transform it, POST the outcome back — so the guest surface, the exchange,
    /// and the owner-thread sequencing are all proven over the wire.
    struct FakeVm {
        invocation_url: String,
        behaviour: FakeBehaviour,
    }

    #[derive(Clone, Copy, PartialEq)]
    enum FakeBehaviour {
        EchoOverHttp,
        ExitWithoutOutcome,
        FailRun,
    }

    impl MicroVm for FakeVm {
        fn run_once(&mut self) -> anyhow::Result<i32> {
            match self.behaviour {
                FakeBehaviour::FailRun => anyhow::bail!("the kernel faulted"),
                FakeBehaviour::ExitWithoutOutcome => Ok(3),
                FakeBehaviour::EchoOverHttp => {
                    let mut invocation = Vec::new();
                    ureq::get(&self.invocation_url)
                        .call()
                        .expect("guest fetch")
                        .into_reader()
                        .read_to_end(&mut invocation)
                        .expect("guest fetch body");
                    let mut outcome = b"outcome:".to_vec();
                    outcome.extend_from_slice(&invocation);
                    ureq::post(&self.invocation_url)
                        .send_bytes(&outcome)
                        .expect("guest outcome post");
                    Ok(0)
                }
            }
        }
    }

    struct FakeFactory {
        behaviour: FakeBehaviour,
        fail_build: bool,
        specs: Mutex<Vec<SandboxSpec>>,
    }

    impl FakeFactory {
        fn new(behaviour: FakeBehaviour) -> Self {
            Self { behaviour, fail_build: false, specs: Mutex::new(Vec::new()) }
        }
    }

    impl VmFactory for FakeFactory {
        fn build(&self, spec: &SandboxSpec, _exchange: Arc<GuestExchange>) -> anyhow::Result<Box<dyn MicroVm>> {
            self.specs.lock().unwrap().push(spec.clone());
            if self.fail_build {
                anyhow::bail!("no hypervisor");
            }
            Ok(Box::new(FakeVm { invocation_url: spec.args[0].clone(), behaviour: self.behaviour }))
        }
    }

    struct Harness {
        sidecar: Arc<Sidecar>,
        factory: Arc<FakeFactory>,
        admin_base: String,
        stopping: Arc<AtomicBool>,
    }

    /// Boots a full sidecar over real sockets: the guest surface listens on loopback (fine for the fake guest)
    /// and both surfaces run the production `serve` loop.
    fn start(factory: FakeFactory) -> Harness {
        let admin_port = free_port();
        let guest_port = free_port();
        let addresses = SidecarAddresses {
            admin_advertise: format!("127.0.0.1:{admin_port}"),
            guest_advertise: format!("127.0.0.1:{guest_port}"),
        };
        let factory = Arc::new(factory);
        let sidecar = Arc::new(Sidecar::new(addresses, Arc::clone(&factory) as Arc<dyn VmFactory>));
        let stopping = Arc::new(AtomicBool::new(false));

        let admin = Arc::new(tiny_http::Server::http(("127.0.0.1", admin_port)).expect("admin bind"));
        let guest = Arc::new(tiny_http::Server::http(("127.0.0.1", guest_port)).expect("guest bind"));
        serve(admin, Arc::clone(&sidecar), 2, handle_admin, Arc::clone(&stopping));
        serve(guest, Arc::clone(&sidecar), 2, handle_guest, Arc::clone(&stopping));

        Harness { sidecar, factory, admin_base: format!("http://127.0.0.1:{admin_port}"), stopping }
    }

    impl Drop for Harness {
        fn drop(&mut self) {
            self.stopping.store(true, Ordering::Relaxed);
        }
    }

    fn free_port() -> u16 {
        TcpListener::bind("127.0.0.1:0").expect("probe bind").local_addr().expect("probe addr").port()
    }

    fn put(url: &str, body: &[u8]) -> (u16, String) {
        match ureq::put(url).send_bytes(body) {
            Ok(response) => read(response),
            Err(ureq::Error::Status(_, response)) => read(response),
            Err(error) => panic!("PUT {url}: {error}"),
        }
    }

    fn post(url: &str, body: &[u8]) -> (u16, String) {
        match ureq::post(url).send_bytes(body) {
            Ok(response) => read(response),
            Err(ureq::Error::Status(_, response)) => read(response),
            Err(error) => panic!("POST {url}: {error}"),
        }
    }

    fn read(response: ureq::Response) -> (u16, String) {
        let status = response.status();
        (status, response.into_string().expect("body"))
    }

    fn deploy(harness: &Harness, id: &str) -> (u16, String) {
        let (staged, _) = put(&format!("{}/sandboxes/{id}/initrd", harness.admin_base), b"070701-fake-initrd");
        assert_eq!(staged, 204);
        put(
            &format!("{}/sandboxes/{id}", harness.admin_base),
            br#"{"memoryMib": 96, "allowedHosts": ["172.20.0.10:8199", "petstore.example.com:8443"], "environment": {"ARAZZO_SOURCE__petstore": "https://petstore.example.com:8443/api"}}"#,
        )
    }

    #[test]
    fn a_deploy_evolves_and_an_invoke_round_trips_through_the_guest_surface() {
        let harness = start(FakeFactory::new(FakeBehaviour::EchoOverHttp));

        let (status, body) = deploy(&harness, "arazzo-mg-pets-v3");
        assert_eq!(status, 200, "{body}");
        let invoke_url = body
            .strip_prefix("{\"invokeUrl\":\"")
            .and_then(|rest| rest.strip_suffix("\"}"))
            .expect("invokeUrl document")
            .to_string();
        assert_eq!(invoke_url, format!("{}/invoke/arazzo-mg-pets-v3", harness.admin_base));

        let (status, outcome) = post(&invoke_url, br#"{"runId":"r-1"}"#);
        assert_eq!(status, 200);
        assert_eq!(outcome, "outcome:{\"runId\":\"r-1\"}");

        // A second invoke reuses the same warm sandbox.
        let (status, outcome) = post(&invoke_url, br#"{"runId":"r-2"}"#);
        assert_eq!(status, 200);
        assert_eq!(outcome, "outcome:{\"runId\":\"r-2\"}");
    }

    #[test]
    fn the_spec_freezes_argv_and_strips_allowlist_ports_and_adds_the_guest_host() {
        let harness = start(FakeFactory::new(FakeBehaviour::EchoOverHttp));

        let (status, body) = deploy(&harness, "spec-probe");
        assert_eq!(status, 200, "{body}");

        let specs = harness.factory.specs.lock().unwrap();
        let spec = &specs[0];
        assert_eq!(spec.memory_mib, 96);
        assert!(spec.args[0].ends_with("/guest/spec-probe"), "argv[1] must be the guest surface URL: {:?}", spec.args);
        assert_eq!(spec.args[1], "ARAZZO_SOURCE__petstore=https://petstore.example.com:8443/api");
        assert_eq!(spec.allowed_hosts, vec!["172.20.0.10", "petstore.example.com", "127.0.0.1"]);
        assert_eq!(spec.initrd, b"070701-fake-initrd");
    }

    #[test]
    fn configuring_without_a_staged_initrd_is_conflict() {
        let harness = start(FakeFactory::new(FakeBehaviour::EchoOverHttp));

        let (status, body) = put(&format!("{}/sandboxes/never-staged", harness.admin_base), b"{}");
        assert_eq!(status, 409);
        assert!(body.contains("no initrd is staged"), "{body}");
    }

    #[test]
    fn a_failed_evolve_reports_the_factory_error() {
        let mut factory = FakeFactory::new(FakeBehaviour::EchoOverHttp);
        factory.fail_build = true;
        let harness = start(factory);

        let (status, body) = deploy(&harness, "wont-build");
        assert_eq!(status, 422);
        assert!(body.contains("no hypervisor"), "{body}");
    }

    #[test]
    fn invoking_an_unevolved_sandbox_is_conflict_and_bad_ids_are_rejected() {
        let harness = start(FakeFactory::new(FakeBehaviour::EchoOverHttp));

        let (status, _) = post(&format!("{}/invoke/never-deployed", harness.admin_base), b"{}");
        assert_eq!(status, 409);

        let (status, _) = put(&format!("{}/sandboxes/bad%2Fid/initrd", harness.admin_base), b"x");
        assert_eq!(status, 400);
    }

    #[test]
    fn a_guest_that_exits_without_posting_an_outcome_fails_the_invoke() {
        let harness = start(FakeFactory::new(FakeBehaviour::ExitWithoutOutcome));

        let (status, body) = deploy(&harness, "silent-guest");
        assert_eq!(status, 200, "{body}");
        let (status, body) = post(&format!("{}/invoke/silent-guest", harness.admin_base), b"{}");
        assert_eq!(status, 502);
        assert!(body.contains("exited with code 3"), "{body}");
    }

    #[test]
    fn a_faulting_run_fails_the_invoke_with_the_error() {
        let harness = start(FakeFactory::new(FakeBehaviour::FailRun));

        let (status, body) = deploy(&harness, "faulting-guest");
        assert_eq!(status, 200, "{body}");
        let (status, body) = post(&format!("{}/invoke/faulting-guest", harness.admin_base), b"{}");
        assert_eq!(status, 502);
        assert!(body.contains("the kernel faulted"), "{body}");
    }

    #[test]
    fn a_redeploy_replaces_the_worker() {
        let harness = start(FakeFactory::new(FakeBehaviour::EchoOverHttp));

        let (status, _) = deploy(&harness, "redeployed");
        assert_eq!(status, 200);
        let (status, _) = deploy(&harness, "redeployed");
        assert_eq!(status, 200);

        assert_eq!(harness.factory.specs.lock().unwrap().len(), 2, "each deploy must build a fresh sandbox");

        let (status, outcome) = post(&format!("{}/invoke/redeployed", harness.admin_base), b"after-redeploy");
        assert_eq!(status, 200);
        assert_eq!(outcome, "outcome:after-redeploy");
    }

    #[test]
    fn delete_tears_the_sandbox_down() {
        let harness = start(FakeFactory::new(FakeBehaviour::EchoOverHttp));

        let (status, _) = deploy(&harness, "short-lived");
        assert_eq!(status, 200);

        let reply = harness.sidecar.remove("short-lived");
        assert_eq!(reply.status, 204);

        let (status, _) = post(&format!("{}/invoke/short-lived", harness.admin_base), b"{}");
        assert_eq!(status, 409);
    }

    #[test]
    fn strip_port_only_strips_numeric_ports() {
        assert_eq!(strip_port("host:8199"), "host");
        assert_eq!(strip_port("10.0.0.5:80"), "10.0.0.5");
        assert_eq!(strip_port("bare-host"), "bare-host");
        assert_eq!(strip_port("odd:name:12"), "odd:name");
    }
}
