// Tier 3 — <arazzo-splitbar>: the draggable pane divider. It drives ONE CSS custom property on a
// target element (the host grid consumes it), clamps to [min, max], persists per storage-key,
// resets on double-click / Enter, and nudges from the keyboard.
import '../../src/components/splitbar.js';
import { ok, equal, mount } from './helpers.js';

describe('<arazzo-splitbar>', () => {
  let wrap;
  afterEach(() => {
    wrap?.remove();
    localStorage.removeItem('test.split');
  });

  function make(attrs = '') {
    wrap = document.createElement('div');
    wrap.id = 'wrap';
    wrap.style.cssText = 'display:grid; grid-template-columns: minmax(0,1fr) auto var(--w, 200px); width:600px; height:120px; --w: 200px;';
    wrap.innerHTML = `
      <div></div>
      <arazzo-splitbar orientation="vertical" target="#wrap" prop="--w" invert min="100" max="400"
                       storage-key="test.split" aria-label="test split" ${attrs}></arazzo-splitbar>
      <div></div>`;
    mount(wrap);
    return wrap.querySelector('arazzo-splitbar');
  }

  const pointer = (el, type, x) =>
    el.dispatchEvent(new PointerEvent(type, { bubbles: true, pointerId: 1, button: 0, clientX: x, clientY: 0 }));

  it('dragging drives the property on the target, inverted for a trailing pane, and persists on release', () => {
    const bar = make();
    pointer(bar, 'pointerdown', 300);
    pointer(bar, 'pointermove', 280); // 20px left, invert → the pane GROWS
    equal(wrap.style.getPropertyValue('--w'), '220px', 'the drag resizes through the property');
    pointer(bar, 'pointermove', 350); // 50px right of start → shrinks below the default
    equal(wrap.style.getPropertyValue('--w'), '150px');
    pointer(bar, 'pointerup', 350);
    equal(localStorage.getItem('test.split'), '150', 'the size persists on release');
  });

  it('clamps to min/max', () => {
    const bar = make();
    pointer(bar, 'pointerdown', 300);
    pointer(bar, 'pointermove', 1300); // shrink far past min
    equal(wrap.style.getPropertyValue('--w'), '100px', 'clamped to min');
    pointer(bar, 'pointermove', -1300); // grow far past max
    equal(wrap.style.getPropertyValue('--w'), '400px', 'clamped to max');
    pointer(bar, 'pointerup', -1300);
  });

  it('restores a persisted size on connect and resets to the stylesheet default on double-click', () => {
    localStorage.setItem('test.split', '333');
    const bar = make();
    equal(wrap.style.getPropertyValue('--w'), '333px', 'the stored size restores');
    bar.dispatchEvent(new MouseEvent('dblclick', { bubbles: true }));
    equal(wrap.style.getPropertyValue('--w'), '', 'double-click clears the override');
    equal(localStorage.getItem('test.split'), null, 'and forgets the stored size');
  });

  it('is a keyboard-operable separator: arrows nudge, Enter resets', () => {
    const bar = make();
    equal(bar.getAttribute('role'), 'separator');
    equal(bar.getAttribute('aria-orientation'), 'vertical');
    bar.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowLeft', bubbles: true }));
    equal(wrap.style.getPropertyValue('--w'), '216px', 'ArrowLeft grows the trailing pane (invert)');
    bar.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowRight', bubbles: true }));
    equal(wrap.style.getPropertyValue('--w'), '200px', 'ArrowRight shrinks it back');
    bar.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }));
    equal(wrap.style.getPropertyValue('--w'), '', 'Enter resets');
  });
});
