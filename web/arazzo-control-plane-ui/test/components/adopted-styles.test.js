// The kit's styles as constructable stylesheets (ADR 0073): adoptStyles gives a shadow root its sheets, one parsed
// sheet per CSS text shared by every instance; the confirm dialog's sheet survives its host's re-render; and a colour
// chosen at render time travels as a data-tone attribute matched by a rule, never a style attribute.
import { ArazzoElement, SHARED_CSS, adoptStyles, styleSheet, toneCss, confirmDialog, define } from '../../src/components/base.js';
import '../../src/components/status-badge.js';
import { ok, equal, waitFor, mount } from './helpers.js';

class StyleProbe extends ArazzoElement {
  connectedCallback() { this.render(); }
  render() {
    adoptStyles(this.shadowRoot, SHARED_CSS, `
      .probe { outline-width: 5px; outline-style: solid; }
    `, toneCss('.probe', { hot: 'rgb(200, 0, 0)' }, 'rgb(1, 2, 3)'));
    this.shadowRoot.innerHTML = '<span class="probe" data-tone="hot">x</span><span class="probe other" data-tone="unknown">y</span>';
  }
}
define('style-probe', StyleProbe);

describe('adoptStyles', () => {
  const mounted = [];
  afterEach(() => { while (mounted.length) mounted.pop().remove(); });
  function probe() { const el = mount(document.createElement('style-probe')); mounted.push(el); return el; }

  it('styles a shadow root with adopted sheets and no style element', () => {
    const el = probe();
    equal(el.shadowRoot.querySelector('style'), null, 'no style element');
    equal(el.shadowRoot.adoptedStyleSheets.length, 3, 'one sheet for each piece of CSS');
    equal(getComputedStyle(el.$('.probe')).outlineWidth, '5px', 'the component rule applies');
  });

  it('parses each CSS text once, shared by every instance', () => {
    const a = probe();
    const b = probe();
    a.shadowRoot.adoptedStyleSheets.forEach((sheet, i) => ok(sheet === b.shadowRoot.adoptedStyleSheets[i], `sheet ${i} is shared`));
    ok(styleSheet(SHARED_CSS) === a.shadowRoot.adoptedStyleSheets[0], 'the shared sheet is the cached one');
  });

  it('leaves the list alone on a re-render with the same styles', () => {
    const el = probe();
    const before = el.shadowRoot.adoptedStyleSheets;
    el.render();
    ok(el.shadowRoot.adoptedStyleSheets.every((sheet, i) => sheet === before[i]), 'the same sheets, in order');
  });

  it('colours by tone, with the fallback for a tone the map does not name', () => {
    const el = probe();
    equal(getComputedStyle(el.$('.probe')).backgroundColor, 'rgb(200, 0, 0)');
    equal(getComputedStyle(el.$('.probe.other')).backgroundColor, 'rgb(1, 2, 3)');
  });

  it('keeps the confirm dialog styled when its host re-renders under it', async () => {
    const el = probe();
    const answer = confirmDialog(el, { title: 'Sure?' });
    const dialog = await waitFor(() => el.shadowRoot.querySelector('dialog.arazzo-confirm'));
    equal(getComputedStyle(dialog).paddingTop, '0px', 'the dialog rule applies');
    adoptStyles(el.shadowRoot, SHARED_CSS);
    equal(getComputedStyle(dialog).paddingTop, '0px', 'still styled after the host replaced its sheets');
    ok(!dialog.innerHTML.includes('<style'), 'no style element in the dialog');
    dialog.querySelector('.cancel').click();
    equal(await answer, false);
  });
});

describe('<arazzo-status-badge> tone', () => {
  it('takes its colour from the status tone', () => {
    const badge = mount(document.createElement('arazzo-status-badge'));
    badge.setAttribute('status', 'Faulted');
    const inner = badge.shadowRoot.querySelector('.badge');
    equal(inner.getAttribute('style'), null, 'no style attribute');
    equal(getComputedStyle(inner).color, 'rgb(212, 53, 28)');
    badge.remove();
  });
});
