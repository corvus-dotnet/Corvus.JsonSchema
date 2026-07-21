# Control-plane use cases

This is the catalog of jobs the control-plane UI serves: what each [persona](UBIQUITOUSLANGUAGE.md#persona)
brings to each surface, stated as the outcome they want rather than the control they click. It is the
acceptance-level reference the surfaces are built and reviewed against. Each entry carries a stable id (for
example `U-RUN-3`) so a design, a review, or a test can cite the exact job it serves.

The jobs are grouped by surface, matching the shell's tabs and the designer. They are stated in the form
*as a [persona], when I am [context], I want to [outcome], so I can [purpose]*. The personas are the ones in the
glossary: operator and SRE, publisher and owner, environment admin, connections admin, security admin, approver,
requester, author, and auditor.

## Runs (U-RUN)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-RUN-1 | operator | on call | see at a glance which runs are unhealthy | triage before anything escalates |
| U-RUN-2 | operator | triaging a faulted run | see which step failed, with the fault detail | decide whether to resume, cancel, or escalate |
| U-RUN-3 | operator | diagnosing | see what a run actually did, step by step | explain an outcome without guessing |
| U-RUN-4 | operator | recovering | resume a faulted run with the right mode | restore service without re-running everything |
| U-RUN-5 | operator | investigating a stuck run | see what it is waiting on (timer or message, correlation) | unblock it or explain the delay |
| U-RUN-6 | operator | cancelling | mark a run cancelled with a reason | stop pointless work and leave an audit trail |
| U-RUN-7 | operator | housekeeping | purge old terminal runs by rule | keep the store lean without hand-picking rows |
| U-RUN-8 | operator | correlating with telemetry | copy a run's correlation id | pivot to traces and logs |
| U-RUN-9 | anyone | filtering | narrow by status, workflow, tags, time | find the runs I mean quickly |

## Catalog (U-CAT)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-CAT-1 | owner | curating | see every workflow, its latest version and status | know what the estate contains |
| U-CAT-2 | owner | inspecting a version | see its identity, description, owner and audit trail | trust what I am about to promote |
| U-CAT-3 | owner | comparing | diff two versions of a workflow | understand what changed before rollout |
| U-CAT-4 | owner | promoting | see where a version is available and where it is ready | move it toward production deliberately |
| U-CAT-5 | owner | governing | see and edit the version's administrator set | hand off or share control safely |
| U-CAT-6 | owner | scoping visibility | manage the version's management tags | control who may see and manage it |
| U-CAT-7 | owner | wiring | see the version's sources and their credential readiness per environment | know why promotion is or is not offered |
| U-CAT-8 | publisher | adding | register a new workflow version | make it runnable and governable |
| U-CAT-9 | owner | retiring | obsolete and purge dead versions | keep the catalog trustworthy |

## Environments and promotion (U-ENV)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-ENV-1 | environment admin | operating | see the environments I administer | know my remit at a glance |
| U-ENV-2 | environment admin | governing | edit metadata, evidence policy, draft-run posture | keep each environment's rules explicit |
| U-ENV-3 | environment admin | governing | manage the environment's administrator set | share operation without oversharing |
| U-ENV-4 | environment admin | auditing | see which workflow versions are available here | know what can run in my environment |
| U-ENV-5 | workflow owner | delivering | request promotion of a version into an environment I cannot write | move toward production through governance |
| U-ENV-6 | environment admin | deciding | approve or deny promotion requests for my environments | control what lands where, with reasons recorded |

## Sources and credentials (U-SRC)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-SRC-1 | connections admin | surveying | list the registered API surfaces | know what workflows may bind to |
| U-SRC-2 | connections admin | wiring an environment | create a credential binding for a source in an environment | make workflows runnable there |
| U-SRC-3 | connections admin | auditing | see every binding, its status, expiry and usage scope | catch expiring or over-shared credentials early |
| U-SRC-4 | connections admin | rotating | edit or duplicate a binding without touching secret material | rotate safely, reference-only |
| U-SRC-5 | connections admin | revoking | revoke a binding | cut access fast when something leaks |
| U-SRC-6 | security admin | scoping | restrict who may use a credential | keep production credentials off shared paths |

## Runners (U-RNR)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-RNR-1 | SRE | operating | see each runner's health, environment and load posture | know execution capacity is alive |
| U-RNR-2 | SRE | rolling out | see which workflow versions a runner has loaded | confirm a deploy actually landed |
| U-RNR-3 | environment admin | gating | authorize or revoke a runner for my environment | control who executes in my environment |
| U-RNR-4 | SRE | diagnosing | get from a stale runner to a next action | recover capacity, not just observe its absence |

## Security (U-SEC)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-SEC-1 | security admin | authoring vocabulary | create and edit named reach rules | reuse one WHERE vocabulary across grants |
| U-SEC-2 | security admin | granting | key a claim to per-verb reach through the picker | grant without hand-typing identities |
| U-SEC-3 | security admin | narrowing | attach several rules to one grant knowingly | intersect constraints without surprises |
| U-SEC-4 | security admin | widening | add a second grant for the same claim | give either/or reach the safe way |
| U-SEC-5 | security admin | auditing one principal | resolve everything a grantee can do and why | answer "what can X touch" in one place |
| U-SEC-6 | security admin | revoking | remove a grant from wherever I see it | cut access at the moment of discovery |
| U-SEC-7 | security admin | maintaining | know which rules are load-bearing before editing or deleting | avoid silently stranding grants |
| U-SEC-8 | auditor | reviewing | read the grant list and understand each row unaided | review policy without the API docs |
| U-SEC-9 | anyone | protecting the floor | be unable to casually break the baseline and founder bindings | avoid locking the deployment out |

## Approvals and requests (U-APR)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-APR-1 | approver | anywhere in the shell | see whether I have work waiting | clear my queue without polling |
| U-APR-2 | approver | deciding access | approve, make eligible, or deny with a recorded reason | elevate people deliberately |
| U-APR-3 | approver | deciding promotion | approve or deny availability requests for my environments | gate delivery with an audit trail |
| U-APR-4 | approver | post-decision | revisit past decisions and revoke an approval | undo a grant that outlived its purpose |
| U-APR-5 | requester | needing access | request scoped, time-boxed access to a workflow I can see | do my job without a standing grant |
| U-APR-6 | requester | waiting | track my requests and their outcomes | know where I stand without asking |

## Designer (U-DSN)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-DSN-1 | author | opening the designer | pick a working copy or start one | get to authoring in one step |
| U-DSN-2 | author | reading a flow | understand the graph, edge kinds and step states at a glance | reason about control flow visually |
| U-DSN-3 | author | editing a step | change bindings, parameters, criteria, outputs next to the step | author without leaving the canvas |
| U-DSN-4 | author | wiring surfaces | attach sources and drag operations onto the canvas | bind steps to real APIs |
| U-DSN-5 | author | validating | see problems with positions and jump to them | fix issues before publish refuses them |
| U-DSN-6 | author | verifying | run against mocks, step, set breakpoints, inspect exchanges | debug the flow like code |
| U-DSN-7 | author | verifying durably | run a draft in a development environment | prove it against real infrastructure |
| U-DSN-8 | author | regression-proofing | save a trace as a scenario and run the suite | keep proven behavior pinned |
| U-DSN-9 | author | collaborating | bind to a branch, commit, pull, compare, roll back | keep the document in version control |
| U-DSN-10 | author | shipping | publish to the catalog with evidence attached | hand the version to governance |

## Shell (U-SH)

| # | As a | When I am | I want to | So I can |
|---|---|---|---|---|
| U-SH-1 | anyone | arriving | sign in with my real identity and see who I am | trust the session I am acting in |
| U-SH-2 | anyone | navigating | find each job's surface from the tab names alone | not memorize a site map |
| U-SH-3 | anyone | reading any list | see real counts, paging and explicit empty states | trust what the list claims |
| U-SH-4 | anyone | acting | find the action on or next to the data it affects | act without hunting |

## See also

- The [security and access UI guide](../guides/security-ui.md) for how the U-SEC jobs become correct-by-construction
  binding authoring.
- The [web UI kit guide](../guides/web-ui-kit.md) and the [UX component catalog](../guides/ux-component-catalog.md)
  for the components that realise these jobs.
- The [workflow designer guide](../guides/workflow-designer.md) for the U-DSN authoring, debug, and scenario jobs.
- The [glossary](UBIQUITOUSLANGUAGE.md) for the personas and the domain terms used above.
