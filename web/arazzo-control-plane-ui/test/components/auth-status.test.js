// <arazzo-auth-status> — who is signed in, and sign-out. "Sign out everywhere" is offered only when the host's /me says
// its sessions are revocable (ADR 0075), and it posts scope=everywhere to /logout.
import '../../src/components/auth-status.js';
import { ok, equal, waitFor, mount } from './helpers.js';

const meAt = (me) => `data:application/json,${encodeURIComponent(JSON.stringify(me))}`;

function status(me) {
  const el = document.createElement('arazzo-auth-status');
  el.setAttribute('me-url', meAt(me));
  el.setAttribute('logout-url', '/logout-under-test');
  return mount(el);
}

// Capture the sign-out form instead of navigating the test page away.
async function submittedForm(click) {
  const submit = HTMLFormElement.prototype.submit;
  let form;
  HTMLFormElement.prototype.submit = function () { form = this; };
  try {
    click();
  } finally {
    HTMLFormElement.prototype.submit = submit;
  }
  form?.remove();
  return form;
}

describe('<arazzo-auth-status>', () => {
  afterEach(() => document.querySelectorAll('arazzo-auth-status').forEach((el) => el.remove()));

  it('offers only Sign out when the host does not say its sessions are revocable', async () => {
    const el = status({ name: 'wanda', groups: [] });
    await waitFor(() => el.getAttribute('data-state') === 'in');
    const buttons = [...el.shadowRoot.querySelectorAll('button')].map((b) => b.textContent);
    equal(buttons.join('|'), 'Sign out');
  });

  it('offers Sign out everywhere when the host says it can, and posts scope=everywhere', async () => {
    const el = status({ name: 'wanda', groups: [], signOutEverywhere: true });
    const everywhere = await waitFor(() => el.shadowRoot.querySelector('button.everywhere'));
    equal(everywhere.textContent, 'Sign out everywhere');

    const form = await submittedForm(() => everywhere.click());
    ok(form, 'a form was submitted');
    equal(form.method, 'post');
    equal(new URL(form.action).pathname, '/logout-under-test');
    equal(new FormData(form).get('scope'), 'everywhere');
  });

  it('posts no scope for a plain Sign out', async () => {
    const el = status({ name: 'wanda', groups: [], signOutEverywhere: true });
    await waitFor(() => el.shadowRoot.querySelector('button.everywhere'));
    const plain = [...el.shadowRoot.querySelectorAll('button')].find((b) => b.textContent === 'Sign out');

    const form = await submittedForm(() => plain.click());
    equal(new FormData(form).get('scope'), null);
  });
});
