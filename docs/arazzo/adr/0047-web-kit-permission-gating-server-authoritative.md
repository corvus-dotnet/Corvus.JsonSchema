# ADR 0047. Web-kit permission gating is server-authoritative

Date: 2026-07-21. Status: **Accepted**. Scope: how the web kit decides whether to show a privileged control.
Builds on [ADR 0040](0040-three-layer-web-kit.md) and
[ADR 0004](0004-fail-closed-non-disclosing-enforcement.md). This records why a component gates a mutating
control from a `scopes` attribute whose absence defers to the server, rather than from a hard-coded scope list
the component decides on its own.

## Context

A web-kit component shows privileged controls: a rules panel's Delete, a catalog detail's Promote, an
administrator panel's Add. Whether the current caller may use them is an authorization decision, and the
control plane already owns and enforces it ([ADR 0004](0004-fail-closed-non-disclosing-enforcement.md)). The
question is only whether the UI should show, hide, or disable the control up front, as a courtesy, so a caller
is not offered an action the server will refuse.

There is a trap. If a component hard-codes the scope list it gates on, that list is a second, client-side copy
of an authorization rule the server owns. The two diverge silently: a control that gates on a hard-coded
partial list passes every test against the in-browser mock (which grants those scopes) yet vanishes against a
live server that scopes the caller differently. The gating looks correct in the demo and is wrong in
production. This is exactly the divergence a real deployment hit.

### Grounded architectural facts

- **Gating reads a `scopes` attribute, and its absence defers.** A component gates its mutating controls from a
  space-separated `scopes` attribute. The shared shape is `scopes.length === 0 || scopes.includes(verb)`, so
  present scopes show or hide the control while an absent attribute returns permission and defers the decision
  to the server. It is used across the privileged surfaces (`administrators-panel`, `access-overview-panel`,
  `catalog-detail`, `availability-matrix`, `environments-panel`, `rules-panel`, and more).
- **The live shell sets no scopes.** The sample console (`wwwroot/index.html`) mounts every panel without a
  `scopes` attribute, so the live UI defers every gating decision to the server. The in-browser mock demo sets
  scopes only to exercise the gated states in tests.
- **The server is the enforcement backstop.** A component that shows a control the caller may not use still
  fails closed: the request is refused with a 403
  ([ADR 0004](0004-fail-closed-non-disclosing-enforcement.md)), so deferring is safe. Gating is a courtesy, not
  the control.
- **A live test pins it.** The permissions UX suite drives real personas through the sample and asserts the
  re-gate (a security admin authors, an operator gets a read-only pane, a reader loses the tab), so the
  server-authoritative behaviour is checked against a running server, not only the mock.

## Options

**Client-side static gating.** Each component hard-codes the scope list it needs and decides visibility itself.
Rejected: the hard-coded list is a second copy of a server-owned rule that diverges silently, correct against
the mock and wrong against a live server.

**Server-authoritative gating (chosen).** A component gates from a `scopes` attribute whose absence defers to
the server, and never ships a hard-coded partial list. The live shell sets no scopes; the server decides.

## Decision

Web-kit permission gating is **server-authoritative**. A component gates a privileged control from a `scopes`
attribute, but an absent attribute means "ask the server", never "deny", and a component never ships a
hard-coded partial scope list. The live shell sets no scopes and lets the server decide; the in-browser mock
sets them only to exercise the gated states. The server's fail-closed enforcement
([ADR 0004](0004-fail-closed-non-disclosing-enforcement.md)) is the real control, and the UI gating is a
courtesy that must not diverge from it.

## Consequences

- The UI cannot silently diverge from the server on who may do what, because it does not carry a second copy of
  the authorization rule; an absent scope defers rather than guesses.
- A control shown to a caller who may not use it still fails closed at the server, so deferring is safe and the
  UI never becomes the security boundary.
- The gating convention is one shape across every panel, documented in the kit README and pinned by a live
  persona test per privileged affordance, so a new panel follows it rather than inventing a scope list.
- A host that does know the caller's scopes may still pass them, to hide rather than disable an unavailable
  control, but omitting them is the safe default, not a bug.
