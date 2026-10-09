//! Validating JSON text allocates nothing in the steady state: the parser's buffers are reused, and the document
//! is evaluated where it was parsed. Counted with a global allocator (this test file is its own program), for the
//! validating thread alone.

use std::alloc::{GlobalAlloc, Layout, System};
use std::cell::Cell;
use std::sync::atomic::{AtomicUsize, Ordering};

use serde_json::json;

struct Counting;

thread_local! {
    /// Whether this thread is the one whose allocations are counted. The test harness's own thread allocates four
    /// times just after it starts the test (its table of running tests, and its wait for the result). When that
    /// thread was held up for a few hundred microseconds, those landed among the validations and were counted.
    static MEASURED: Cell<bool> = const { Cell::new(false) };
}

/// The allocations of the measured thread, and the sizes of the first of them (to say what allocated, on a failure).
static ALLOCATIONS: AtomicUsize = AtomicUsize::new(0);
static SIZES: [AtomicUsize; 8] = [const { AtomicUsize::new(0) }; 8];
/// The allocations of every other thread: reported on a failure, never asserted.
static ELSEWHERE: AtomicUsize = AtomicUsize::new(0);

fn count(size: usize) {
    // A thread that is ending has no thread-local values left: it is not the measured one.
    if MEASURED.try_with(Cell::get).unwrap_or(false) {
        let n = ALLOCATIONS.fetch_add(1, Ordering::Relaxed);
        if let Some(slot) = SIZES.get(n) {
            slot.store(size, Ordering::Relaxed);
        }
    } else {
        ELSEWHERE.fetch_add(1, Ordering::Relaxed);
    }
}

unsafe impl GlobalAlloc for Counting {
    unsafe fn alloc(&self, layout: Layout) -> *mut u8 {
        count(layout.size());
        unsafe { System.alloc(layout) }
    }

    unsafe fn dealloc(&self, ptr: *mut u8, layout: Layout) {
        unsafe { System.dealloc(ptr, layout) }
    }

    unsafe fn realloc(&self, ptr: *mut u8, layout: Layout, new_size: usize) -> *mut u8 {
        count(new_size);
        unsafe { System.realloc(ptr, layout, new_size) }
    }
}

#[global_allocator]
static GLOBAL: Counting = Counting;

#[test]
fn validate_json_allocates_nothing_in_the_steady_state() {
    let v = corvus_json_schema::compile(&json!({
        "type": "object",
        "properties": { "name": { "type": "string", "minLength": 1 }, "tags": { "type": "array", "items": { "type": "string" } } },
        "required": ["name"]
    }))
    .unwrap();
    let texts = [
        r#"{"name": "a", "tags": ["x", "y\nz"]}"#,
        r#"{"name": "", "tags": []}"#,
        r#"{"name": "caf\u00e9", "tags": ["1", "2", "3", "4", "5", "6", "7", "8"], "other": {"deep": [1, [2, [3]]]}}"#,
    ];
    // The first validations size the buffers.
    for t in texts {
        v.validate_json(t).unwrap();
    }
    let elsewhere = ELSEWHERE.load(Ordering::Relaxed);
    MEASURED.set(true);
    for _ in 0..100 {
        for t in texts {
            std::hint::black_box(v.validate_json(t).unwrap());
        }
    }
    MEASURED.set(false);
    let elsewhere = ELSEWHERE.load(Ordering::Relaxed) - elsewhere;
    let allocations = ALLOCATIONS.load(Ordering::Relaxed);
    let sizes: Vec<usize> = SIZES.iter().take(allocations).map(|s| s.load(Ordering::Relaxed)).collect();
    assert_eq!(
        allocations, 0,
        "allocations in 300 validations of JSON text (the first, in bytes: {sizes:?}; other threads made {elsewhere} \
         meanwhile)"
    );
}
