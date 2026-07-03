# The control-plane access model: capability vs reach

Every access decision in the control plane is **WHO can do WHAT, WHERE**. Two independent systems
answer the WHAT and the WHERE, and they are easy to conflate. A **grant** configures only one of
them (the WHERE). This note exists because the `read` / `write` / `purge` verbs on a grant are a
recurring source of confusion: they look like they should be scopes, and they are not.

## Two planes

There are two separate permission systems. Both must pass for an action to succeed.

| | Capability (scopes) | Reach (grants + rules) |
|---|---|---|
| Question it answers | Which operations may you call. | Which data rows may you touch. |
| Examples | `catalog:read`, `runs:write`, `security:write`. | Rows where `domain == payments`. |
| Granularity | Per resource type and operation. | Per row, matched on security tags (`domain`, `tenant`, ...), across resource types. |
| Where it comes from | The caller's role, as token scopes (§14.1). Issued by the IdP, configured by the deployment. Not authored in this UI. | Grants and rules (§14.2). Authored in the security UI. |
| Denied result | `403 Forbidden`. The operation exists but you may not call it. Disclosing. | The row is absent (`404`). Non-disclosing: an out-of-reach row is indistinguishable from one that does not exist. |

Capability is coarse and standing. It follows your job function, such as a payments operator or a
platform administrator. Reach is finer and shifts with team membership and per-request elevation.
Keeping them separate is what lets two people who both hold `catalog:write` have completely
different row reach.

## What a grant is

A grant (the API calls it a **security binding**, §14.2) is a **claim to reach** mapping. In one
sentence: a caller carrying claim `team = payments` gets, per verb, this much reach over the rows.

```mermaid
flowchart LR
  Claim["WHO: claim team = payments"] --> R["read: rows where domain == payments"]
  Claim --> W["write: rows where domain == payments"]
  Claim --> P["purge: Denied"]
```

Each verb's reach is one of `Denied`, `Unrestricted`, or scoped to one or more rules.

It has two parts:

- **WHO.** A claim (`claimType` / `claimValue`, for example `team = payments`). A group or role,
  resolved through the grantee picker, or a raw claim. A *person* is not granted here; per-person
  elevation goes through the access-request flow instead.
- **WHERE.** For each of `read`, `write` and `purge`, a reach setting of `Denied`, `Unrestricted`,
  or scoped to one or more named **rules**. A rule is a reusable row-filter expression such as
  `domain == payments`.

So a grant is `claim -> { read: <rows>, write: <rows>, purge: <rows> }`. The verb is the key; the
value is the set of rows that verb may touch.

## Why read / write / purge, and not scopes

The three verbs are the three levels of access to a data **row**.

- `read`: see the row.
- `write`: modify the row.
- `purge`: hard-delete the row.

Each gets its own row set because they genuinely vary. You might let the payments team read every
payments row, write a subset of them, and purge none. That independence is the point of reach, and
it is why a grant is expressed as three row sets rather than as scopes.

Scopes (`catalog:read`, `runs:write`, ...) answer a different question, "which API may you invoke",
and they live in the caller's token, not in a grant. A grant never widens or narrows a scope. If
you go looking for `catalog:write`-style entries on a grant you will not find them, because that
plane is configured upstream in the IdP and the role mapping, not here.

## How they compose

Both planes are checked, in order. To edit a catalog version a caller needs the scope `catalog:write`
(may they invoke the update operation at all) **and** write reach over that specific row (does their
reach admit it).

```mermaid
flowchart TD
  Req["Request: modify catalog version X"] --> Cap{"Capability plane (§14.1)<br/>is catalog:write in the token?"}
  Cap -- "no" --> F["403 Forbidden<br/>you may not call this operation"]
  Cap -- "yes" --> Reach{"Reach plane (§14.2, via a grant)<br/>does write reach admit row X's tags?"}
  Reach -- "no" --> Abs["Absent / 404<br/>non-disclosing"]
  Reach -- "yes" --> Ok["Allowed"]
```

A worked example, a payments-team operator:

- **Token** (from their role): `catalog:read catalog:write runs:read runs:write`. No `environments:write`.
- **Grant**: `team = payments` gives read `domain == payments`, write `domain == payments`, purge `Denied`.
- **Result**: they read and write catalog versions and runs, but only rows tagged `domain == payments`.
  They purge nothing. They cannot touch environments at all, because they lack `environments:write`,
  which is a `403` decided before reach is even consulted. A `domain == finance` version is simply
  absent to them (`404`), never a `403`.

## Where each is authored

- **Scopes (capability).** The deployment's IdP and role mapping (§14.1). Standing, coarse, outside
  this UI.
- **Reach (grants + rules).** The security UI. Grants bind a claim to per-verb reach; rules are the
  reusable WHERE vocabulary a grant points at. See the grants and rules panels, and the access
  overview, in [`security-ui-design.md`](./security-ui-design.md).
