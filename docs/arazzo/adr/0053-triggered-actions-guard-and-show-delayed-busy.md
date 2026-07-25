# ADR 0053. A triggered async action guards itself and shows delayed busy feedback

Date: 2026-07-25. Status: **Accepted**. Scope: how a web-kit control behaves while the async operation it
triggers is in flight. Builds on [ADR 0040](0040-three-layer-web-kit.md) and
[ADR 0047](0047-web-kit-permission-gating-server-authoritative.md). This records why every user-triggered
service action runs through one shared helper that blocks re-entry immediately and shows a spinner only once
the work runs long, rather than each control inventing its own in-flight handling.

## Context

A web-kit control triggers a service roundtrip: a dialog's Create, a panel's Delete, an approval queue's
Approve, a Git dialog's Commit. Most come back inside a few tens of milliseconds, but some run longer, and a few
(an approval that enacts through a workflow run) take on the order of a second. Two problems followed from
leaving each control to fend for itself.

- **No feedback once an action runs past about 100ms.** The control looked idle while the request was in
  flight, so a user had no signal that anything was happening.
- **The control stayed live while the request was in flight.** A second click issued the operation again. For a
  non-idempotent create this made a duplicate, and the reported symptom was exactly that: clicking Create twice
  in the new-working-copy dialog created two.

The controls that did handle it did so inconsistently. Some set `.disabled = true` and re-enabled in a
`finally`; one swapped its label to "Saving…"; the run and schedule panels kept a busy flag and re-rendered.
Most create, delete, and decision actions had no in-flight handling at all.

There is a timing trap in the naive fix. Disabling the control the instant it is clicked prevents the double
submit, but it is a visible change at 0ms, so a 30ms operation flickers a disabled state for no reason. Delaying
the visible change to avoid the flicker leaves a window in which the control is still live, which is precisely
the window a double-click lands in (a double-click is well under 100ms). The two requirements pull in opposite
directions only if one mechanism serves both.

### Grounded architectural facts

- **Every service call funnels through one client.** A component reaches the Layer-0 client and awaits a method
  that performs the fetch ([ADR 0040](0040-three-layer-web-kit.md)); Layer 1 never fetches directly. A
  triggered action is therefore always "a control, then an awaited client call".
- **`disabled` is already owned by validation.** Many controls compute `disabled` from field validity on every
  render (a create button that lights only once a name is typed). An in-flight mechanism that also wrote
  `disabled` would fight that logic.
- **The server is the real guard.** A non-idempotent action is enforced server-side; the UI never is
  ([ADR 0047](0047-web-kit-permission-gating-server-authoritative.md)). Whatever the UI does about a double
  click is a courtesy on top of that, not a correctness boundary.

## Decision

Add one helper to the shared base class, `ArazzoElement.runAction(trigger, work, { delay })`, and route every
user-triggered service action through it. It separates the two requirements onto two clocks.

- **Re-entrancy is blocked at 0ms, invisibly.** The trigger is marked in flight the instant `runAction` is
  called. A second activation while the work is unsettled resolves to `undefined` without running the work
  again, so the operation issues exactly once. Because the mark is invisible, a fast action shows nothing.
- **The busy affordance appears only after a delay (about 150ms).** If the work outlives the delay the trigger
  gets `aria-busy="true"`, and the shared stylesheet hides its label in place and centres a spinner where the
  label was, so the control keeps its width and does not shift. Work that settles first never flashes a
  spinner. The spinner replaces the label rather than sitting beside it, so it needs no per-action text.
- **It never writes `disabled`.** The busy layer and the validity layer stay separate, so they cannot fight.
  For a control fronting a confirm or a name prompt, the modal blocks re-entry on its own and the helper spins
  only the network portion that follows, never while the dialog is open.

Actions that already carried a richer in-flight state keep it: the scenario and schedule run controls re-render
a whole busy region, and the GitHub and provider connect buttons run a polling handshake with their own
waiting state. These already meet both requirements, so converting them to a single-button spinner would lose
behaviour, not gain it.

This is a UX courtesy against an accidental second submission, not a guarantee against a duplicate. A
non-idempotent server action still defends itself; the client is never the authority
([ADR 0047](0047-web-kit-permission-gating-server-authoritative.md)).

## Consequences

- Every mutating control in the kit gives the same feedback and cannot be double-submitted by a fast double
  click, from one helper with one unit-tested contract, rather than a per-control reinvention.
- Fast actions stay visually silent. The spinner is reserved for work that actually runs long, so the common
  case does not flicker.
- The helper is not a rate limiter or a queue. It blocks re-entry on the same trigger while one operation is in
  flight; it does not serialise different controls, and it does not replace server-side idempotency.
- A control re-rendered on success (its list rebuilt underneath it) drops its own busy marker with the old
  element, which is harmless. A control that stays put (an error leaves the dialog open) is restored so the
  action can be retried.
