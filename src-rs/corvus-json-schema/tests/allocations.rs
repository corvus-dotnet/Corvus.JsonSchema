//! Validating JSON text allocates nothing in the steady state: the parser's buffers are reused, and the document
//! is evaluated where it was parsed. Counted with a global allocator (this test file is its own program).

use std::alloc::{GlobalAlloc, Layout, System};
use std::sync::atomic::{AtomicUsize, Ordering};

use serde_json::json;

struct Counting;

static ALLOCATIONS: AtomicUsize = AtomicUsize::new(0);

unsafe impl GlobalAlloc for Counting {
    unsafe fn alloc(&self, layout: Layout) -> *mut u8 {
        ALLOCATIONS.fetch_add(1, Ordering::Relaxed);
        unsafe { System.alloc(layout) }
    }

    unsafe fn dealloc(&self, ptr: *mut u8, layout: Layout) {
        unsafe { System.dealloc(ptr, layout) }
    }

    unsafe fn realloc(&self, ptr: *mut u8, layout: Layout, new_size: usize) -> *mut u8 {
        ALLOCATIONS.fetch_add(1, Ordering::Relaxed);
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
    let before = ALLOCATIONS.load(Ordering::Relaxed);
    for _ in 0..100 {
        for t in texts {
            std::hint::black_box(v.validate_json(t).unwrap());
        }
    }
    assert_eq!(ALLOCATIONS.load(Ordering::Relaxed) - before, 0, "allocations in 300 validations of JSON text");
}
