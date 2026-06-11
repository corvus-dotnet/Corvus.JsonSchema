// Minimal dependency-free browser test helpers (Mocha provides it/describe; assertions throw on failure).

export function ok(condition, message) {
  if (!condition) throw new Error(message || 'assertion failed');
}

export function equal(actual, expected, message) {
  if (actual !== expected) throw new Error(`${message || 'not equal'}: expected ${expected}, got ${actual}`);
}

/** Resolve on the next dispatch of `type` from `el`, or reject after `timeout` ms. */
export function nextEvent(el, type, timeout = 4000) {
  return new Promise((resolve, reject) => {
    const t = setTimeout(() => reject(new Error(`timed out waiting for '${type}'`)), timeout);
    el.addEventListener(type, (e) => { clearTimeout(t); resolve(e); }, { once: true });
  });
}

/** Poll `fn` until it returns truthy, or reject after `timeout` ms. Returns the truthy value. */
export async function waitFor(fn, timeout = 4000, interval = 25) {
  const start = Date.now();
  for (;;) {
    const value = fn();
    if (value) return value;
    if (Date.now() - start > timeout) throw new Error('timed out waiting for condition');
    await new Promise((r) => setTimeout(r, interval));
  }
}

/** Remove an element after a test. */
export function mount(el) {
  document.body.appendChild(el);
  return el;
}
