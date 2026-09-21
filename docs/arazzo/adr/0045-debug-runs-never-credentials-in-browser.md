# ADR 0045. Remote dev-environment debug runs, never credentials in the browser

Date: 2026-07-21. Status: **Accepted**. Implementation: **partly, with divergences**. Verified against the code 2026-09-21. Built and tested: a debug run executes on the server, the environment must allow draft runs, a calling source must have a binding in the target environment, the run is budgeted, and the trace is metadata. Divergences: there is no `debugRuns` marker on a run or an environment, which is an OpenAPI tag only, and the markers are the reserved `$draft` workflow id and the `sys:workingCopy` tag; and interactive debugging requires a runner hosted inside the control-plane process (`DebugRunsOffered` needs the in-process draft runner, and message injection calls it directly), against "a debug run is claimed and executed by a runner" and the two-process model. On the web side "never in the browser" is an absence and not a control: nothing tests that the designer holds no source secret, and the credential dialog does collect secrets in the browser to send to the control plane. Scope: how a draft workflow is debugged against real behaviour. Builds
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
- **It is budgeted.** Because it makes real source calls, a debug run is held to an execution budget exactly as a
  catalogued run in the same environment is ([ADR 0068](0068-execution-budget-fuel-wall-clock-depth.md)): the
  deployment's ceiling, tightened by the development environment's override. A draft that loops is stopped by
  its fuel.

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
