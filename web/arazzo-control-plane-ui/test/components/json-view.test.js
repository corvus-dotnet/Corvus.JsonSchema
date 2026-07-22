// Tier 3 — <arazzo-json-view>: the read-only CM6 document view. The regression that matters here is
// scroll OWNERSHIP on a long document: CodeMirror's own .cm-scroller must be the scroller (so its
// viewport virtualisation measures against a scroller that moves), NOT the outer .scroll container.
// When the outer div scrolled instead, .cm-scroller grew to the full document height and CM painted
// only the top ~30 lines over blank space (the reported "line numbers but blank content" bug).
import { ArazzoJsonView } from '../../src/components/json-view.js';
import { ok, equal, waitFor, mount } from './helpers.js';

// A document tall enough that the editor would far exceed the 320px view (one line per property).
const LONG_DOC = JSON.stringify(
  Object.fromEntries(Array.from({ length: 120 }, (_, i) => [`property_number_${i}`, `value ${i}`])),
  null,
  2,
);

describe('<arazzo-json-view>', () => {
  let el;
  afterEach(() => { el?.remove(); });

  it('lets CodeMirror own the scroll on a long document (viewport virtualises, no blank gaps)', async function () {
    this.timeout(20000);
    el = document.createElement('arazzo-json-view');
    el.value = LONG_DOC;
    mount(el);
    await ArazzoJsonView.loadCm(); // vendored — no network
    await waitFor(() => el.shadowRoot.querySelector('.cm-editor'), 15000);

    const scroll = el.shadowRoot.querySelector('.scroll');
    const editor = el.shadowRoot.querySelector('.cm-editor');
    const scroller = el.shadowRoot.querySelector('.cm-scroller');

    // The editor is bounded to the view's max-height (default 320) — it does not grow to the full document.
    ok(editor.getBoundingClientRect().height <= 322, `editor height bounded, got ${editor.getBoundingClientRect().height}`);

    // CM's OWN scroller is the scroller: its content overflows the viewport, so it can scroll.
    ok(scroller.scrollHeight > scroller.clientHeight + 200,
      `.cm-scroller must overflow (scrollHeight ${scroller.scrollHeight} vs clientHeight ${scroller.clientHeight})`);
    scroller.scrollTop = 1000;
    ok(scroller.scrollTop > 0, 'the .cm-scroller actually scrolls');

    // The outer container does NOT also scroll — it yields ownership to CM (cm-mounted → overflow hidden).
    ok(scroll.classList.contains('cm-mounted'), 'the outer .scroll is marked cm-mounted');
    ok(scroll.scrollHeight <= scroll.clientHeight + 4,
      `.scroll must not itself overflow (scrollHeight ${scroll.scrollHeight} vs clientHeight ${scroll.clientHeight})`);

    // Virtualisation follows the scroller: after scrolling, CM renders a different window of lines than at the top.
    const topLast = el.shadowRoot.querySelector('.cm-content').lastElementChild?.textContent;
    scroller.scrollTop = 1800;
    await waitFor(() => el.shadowRoot.querySelector('.cm-content').lastElementChild?.textContent !== topLast, 5000);
    ok(true, 'CM re-rendered a new line window on scroll');
  });

  it('carries the document value through to the mounted editor', async function () {
    this.timeout(20000);
    el = document.createElement('arazzo-json-view');
    el.value = LONG_DOC;
    mount(el);
    await ArazzoJsonView.loadCm();
    await waitFor(() => el.shadowRoot.querySelector('.cm-editor'), 15000);
    equal(el.value, LONG_DOC, 'value round-trips');
    ok(el.shadowRoot.querySelector('.cm-line'), 'lines rendered');
  });
});
