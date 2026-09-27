# ADR 0073. Browser security headers: a library helper the host registers, and a kit that meets a strict policy with no build step

Date: 2026-09-27. Status: **Accepted**. Implementation: **partly, as this record says**. Built: the helper, its startup assertion, the policy with no inline script, and the three served pages with their scripts moved into modules. Not built: the components' styles in constructable stylesheets, so `style-src` still admits inline style. Scope: the HTTP security headers on everything a control-plane host serves, and what the web kit must do to run under them. This closes the header half of GAP-1 of the 2026-08-07 security audit, and builds on [ADR 0041](0041-standards-only-zero-build-elements.md), [ADR 0042](0042-auth-agnostic-host-owns-session.md) and [ADR 0071](0071-authentication-event-telemetry.md).

## Context

Nothing a control-plane host served carried a security header. There was no Content-Security-Policy, no `frame-ancestors` or `X-Frame-Options`, no `nosniff`, no `Referrer-Policy` and no HSTS. The console and the designer could be framed, so a framed click on Revoke or Approve was a governance mutation, audited with the victim as its actor. An injection that got script running ran unconstrained and could send what it read to any origin.

The library does not serve the pages. ADR 0042 gives the session to the host, and the demo host serves `/`, `/designer` and the kit's files under `/ui` from its own `Program.cs`. The library already ships one browser-facing control a host adds with one call, the anti-forgery check, and ADR 0071 set the pattern for a control a secured deployment must not omit: one registration, a startup filter that puts the middleware first, and a control plane that refuses to map without it.

ADR 0041 makes the kit standards-only with no build step, and at the time of the audit the kit's shape meant a policy added then would need `'unsafe-inline'`:

- The three served pages carried inline module scripts, 170, 1,850 and 140 lines.
- 66 of the 71 components put a `<style>` block into their shadow root when they render, and the confirm dialog puts one into the dialog.
- 34 `style="..."` attributes sat in 19 components' templates.
- There were no inline event handlers, no `eval` or `new Function`, and no external origins. The vendored CodeMirror bundle already uses constructable stylesheets where the browser has them.

The kit's two connect popups (`provider-connect`, `github-connect`) close themselves through the opener's reference to the popup, and sign-out is a form POST whose response redirects to the identity provider's end-session endpoint.

## Options

**A. A nonce or hash pass at serve time.** The host stamps a per-response nonce into each page and the policy names it. The kit reads the nonce from the page and puts it on every `<style>` it creates.

**B. A UI build step.** A bundler extracts the styles and scripts into files the policy admits by origin.

**C. A strict script policy and an accepted risk for style.** Move the page scripts out, keep `style-src 'unsafe-inline'`, and record the residual.

**D. Conform inside ADR 0041.** Move the page scripts into modules, carry each component's styles in a constructable stylesheet adopted by its shadow root, and turn the style attributes into classes or CSSOM writes. Then the policy is `'self'` for script and style both.

For where the headers come from, independently of the above: **E**, a library helper the host registers and a secured posture requires, or **F**, guidance only, each host writing its own.

## Antagonistic review

*Against A:* a nonce cannot reach a style attribute, so the 34 attributes have to be converted anyway. A nonce makes every page response unique, so no page can be cached. And every host serving the kit takes on a new obligation, generating the nonce and injecting it, which is the adoption tax ADR 0041 exists to avoid. A hash is worse for the components: their styles are built at runtime, and a hash list would have to change with every component edit.

*Against B:* it contradicts ADR 0041 outright, and it buys nothing D does not.

*Against C:* style injection is not harmless. CSS selectors can read attribute values out of a page character by character and send them to an origin by loading a background image, and the audit notes that the cost of fixing it later grows with every component added. C is the cheapest option now and the most expensive later.

*Against D:* it is a sweep of 66 components, and a component someone adds next year with a `<style>` block breaks the policy. *For:* the sweep is mechanical, a static test that forbids `<style` and `style="` in the kit's source catches the next one, and the policy fails loudly in every smoke and UX test, since they run under it. Constructable stylesheets are a web standard, with no build step, and CSP does not govern them: `style-src` applies to `<style>` elements, `<link>` stylesheets and style attributes, not to a sheet built through the CSSOM. They are also cheaper. One sheet per component class is parsed once and shared by every instance, where a `<style>` block is re-parsed for each.

*Against E:* a host that serves the kit from another origin gains nothing on its own pages from headers on the API's host. *For:* the library can only speak for the host it runs in, and there it covers every response, the API's included. A host that serves the kit elsewhere sends its own headers, and this record names the policy it needs.

*Against F:* the gap returns by omission, as ADR 0071 found for authentication telemetry.

## Decision

**D and E.** The library ships `services.AddArazzoSecurityHeaders()` in `Corvus.Text.Json.Arazzo.Durability.ControlPlane.Server`. It registers a startup filter that puts the headers first in the request pipeline, and `MapArazzoControlPlane` refuses to map in a secured posture without it (Open, the development posture, may run without it).

The headers are written when the response starts, not when the request arrives, so they are on whatever the host sends, an error page an exception handler writes after clearing the response included. A header an endpoint has already set is left as it is, so a host can serve one page under a policy of its own.

The header set, and what each is for:

- **`Content-Security-Policy`**: `default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline'; img-src 'self' data:; font-src 'self'; connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'self'; frame-ancestors 'none'`. `data:` images are the kit's inline SVG glyphs. `'unsafe-inline'` leaves `style-src` when the components carry constructable stylesheets.
- **`X-Frame-Options: DENY`**, for browsers that predate `frame-ancestors`. It is not sent when the host names a framer, since it cannot express an allowlist.
- **`X-Content-Type-Options: nosniff`**.
- **`Referrer-Policy: no-referrer`**. Nothing the control plane does relies on a referrer, and the kit's URLs name workflows and runs.
- **`Cross-Origin-Opener-Policy: same-origin-allow-popups`**. `same-origin` would sever the connect popups from the page that opened them, and they close themselves through that reference.
- **`Cross-Origin-Resource-Policy: same-origin`**.
- **`Strict-Transport-Security: max-age=31536000`** on an HTTPS request to a host that is not a loopback address. ASP.NET's own HSTS middleware excludes the same hosts, and a browser pinned to HTTPS for `localhost` would carry that into every other project on the machine. Subdomains are not covered unless the host says so, since the control plane may share a parent domain with hosts it does not govern.

A host adds origins through `ControlPlaneSecurityHeadersOptions`, and nothing else. `ConnectSources` is for pages that call a control plane on another origin. `FormActionSources` is for the identity provider a sign-out form redirects to, because browsers hold that redirect to `form-action` as well as the submission. `FrameAncestors` is for a portal that embeds the console. Each source is checked when the headers are registered, and must be one origin: a scheme of `http` or `https`, a host and an optional port, with no path, query, user information, wildcard or keyword. A value that would widen the policy beyond one origin, or break out of its directive with a `;`, stops the host at startup.

The kit meets the policy with no build step. The served pages load their scripts as modules by URL. In the second piece of this decision, the components carry their styles in constructable stylesheets and write dynamic style through the CSSOM. The demo host registers the helper in both postures, and in the secured one names each Keycloak endpoint that service discovery may resolve as a `form-action` source.

The controls are exercised, not declared. The kit's smoke server sends the same policy, so every smoke and UX test runs the kit under it. A smoke test proves the policy is live, since an injected inline script is refused and reported, before it asserts that the console and the designer raise no violation of their own. Another proves a page refuses to be framed. The library's tests prove every response carries the headers, an exception handler's included, that configuration adds origins and cannot weaken the policy, and that a secured posture does not start without it. The composition's live test proves the demo host sends them on its pages, its kit files and its API, and that `form-action` admits the Keycloak origin sign-in sends the browser to. The kit's live suite signs out in a real browser against the composition and requires the end-session redirect chain to reach Keycloak with no violation; with the Keycloak origin left out of `form-action`, the browser refuses the redirect and the test fails.

## Consequences

- A framed click on a governance action is impossible in any browser that honours `frame-ancestors` or `X-Frame-Options`, which is every current one.
- Injected markup cannot run script, inline or by `eval`, and cannot send what it reads to another origin with `fetch`, a form or an image load. Until the second piece lands, injected CSS can still read the page, and it can only send what it reads to this host, since `img-src` and `font-src` are `'self'`.
- A host that serves the kit from another origin, or that serves its own pages beside it, sends this policy on those pages itself. The helper covers the responses of the host it runs in.
- A host that embeds the console names its portal in `FrameAncestors`. There is no switch to turn framing protection off.
- A component added with a `<style>` block, a style attribute or an inline script breaks the policy in every smoke and UX test that renders it. After the second piece, a static test refuses it before any browser runs.
- The standalone demo gives its panels their `base-url` only after injecting the mock, because a component with a `base-url` and no fetch hook loads through the browser's own `fetch`, which here is a request to the mock's made-up origin that the policy refuses. The requests had always been made and had always failed; the policy made them visible.