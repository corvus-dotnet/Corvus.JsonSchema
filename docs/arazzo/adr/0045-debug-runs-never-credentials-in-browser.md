# ADR 0045. Remote dev-environment debug runs, never credentials in the browser

Date: 2026-07-21. Status: **Accepted**. Scope: how a draft workflow is debugged against real behaviour. Builds
on [ADR 0023](0023-two-process-store-as-queue.md) and [ADR 0027](0027-runner-environment-binding.md). This
records why a draft is debugged by running it in a development-class environment, never by executing it with
credentials held in the browser.

## Context

An author wants to debug a draft against real behaviour: real source calls, real outputs, stepping and
inspection. The tempting shortcut is to run the draft in the browser, calling the sources directly, which means
putting source credentials in the browser. That is a security boundary the platform does not cross: source
credentials are stored as references and resolved by a runner as its own identity
([ADR 0025](0025-integrity-binding-optional-signature.md), the credentials domain), never handed to a client.
A draft also is not a catalogued version, so it needs a way to run without being published.

### Grounded architectural facts

- **A draft is debugged in a development-class environment, never with credentials in the browser.** The
  designer design (`workflow-designer-design.md` §18) runs the draft in a development-class environment rather
  than by credentials in the browser.
- **The debug run rides the runs machinery.** A debug run is a run: it is claimed and executed by a runner
  ([ADR 0023](0023-two-process-store-as-queue.md)), forward-only, with trigger injection and
  capture-then-time-travel inspection, so the debugger sees real execution.
- **It is gated.** A debug run is gated by the environment allowing draft runs (`allowsDraftRuns`) and carries
  a `debugRuns` marker, so an environment opts in to hosting draft debug runs.

## Decision

A draft is debugged by **running it in a development-class environment**, executed by a runner, never by
executing it in the browser with source credentials. The debug run rides the same run machinery as a
production run (claimed by a runner, forward-only, with trigger injection and time-travel inspection), and is
gated on the environment allowing draft runs. Source credentials stay with the runner and never reach the
client.

## Consequences

- Source credentials never enter the browser. The draft runs on a runner that resolves credentials as its own
  identity, upholding the credential boundary.
- The debugger sees real behaviour, because a debug run is a real run on a runner, not a simulation, so what is
  debugged matches what a production run would do.
- Hosting draft debug runs is an environment opt-in (`allowsDraftRuns`), so a production environment does not
  run drafts unless it is configured to.
- The debug run is environment-pinned like any run ([ADR 0027](0027-runner-environment-binding.md)), so it
  executes on a runner authorized for that development environment.
