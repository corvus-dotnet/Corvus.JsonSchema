# ADR 0040. The web kit is three layers, and Layer 1 never fetches

Date: 2026-07-21. Status: **Accepted**. Scope: how the web component kit is structured. This records why the
kit is split into a DOM-free client, composable components, and reference screens, with a hard rule that a
component never calls `fetch` itself.

## Context

A reusable UI kit over the control-plane API has to serve two audiences: an application that wants to drop in a
ready screen, and an application that wants to compose its own from the pieces. It also has to be testable
without a network, and adoptable in any framework. If a component fetched its own data, it would be hard to
test, hard to point at a different transport, and hard to compose, because each component would own its
network policy. The data access has to live in one place the components share.

### Grounded architectural facts

- **Three layers.** The kit (design `ui-design.md` §Architecture) is Layer 0, the `ArazzoControlPlaneClient`
  (no DOM); Layer 1, composable components (`runs-table`, `run-detail`, `grants-panel`, and the rest) that
  take a client, render, and emit; and Layer 2, reference screens (`arazzo-control-plane`, `arazzo-catalog`)
  that compose Layer 1 and own layout, filters, and routing.
- **Layer 1 never calls `fetch`.** The design states the hard rule: a Layer 1 component never calls `fetch`
  directly. It takes a Layer 0 client, or is handed one. A component exposes `.client` (or builds one from a
  `base-url` attribute) through `ArazzoElement` (`web/arazzo-control-plane-ui/src/components/base.js`).
- **Components emit, they do not navigate.** A Layer 1 component renders its own loading, empty, and error
  states, and emits events (through `ArazzoElement.emit`, a bubbling, composed `CustomEvent`); the host or
  Layer 2 decides what to do.

## Decision

The web kit is **three layers**. Layer 0 is a DOM-free client. Layer 1 is composable components that take a
client, render, and emit, and **never call `fetch` themselves**. Layer 2 is reference screens that compose
Layer 1 and own layout, filters, and routing. A component receives its data access as a client, so a host
controls the transport in one place.

## Consequences

- The kit is testable without a network: a component takes a client backed by an in-memory mock, which is how
  the component tests run.
- A host controls the transport once (on the client) rather than per component, so authentication, base URL,
  and interceptors are set in one place ([ADR 0042](0042-auth-agnostic-host-owns-session.md)).
- An application composes its own screen from Layer 1, or drops in a Layer 2 screen, from the same pieces.
- Because components emit rather than navigate, the host owns routing and cross-surface deep-links, so the same
  component works in different host layouts.
