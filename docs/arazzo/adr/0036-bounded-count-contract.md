# ADR 0036. The bounded count contract

Date: 2026-07-21. Status: **Accepted**. Scope: how a list's total is counted. Builds on
[ADR 0035](0035-keyset-pagination-everywhere.md). This records why a count is bounded by a cap and reports
whether it was capped, rather than counting an unbounded total.

## Context

A paged list ([ADR 0035](0035-keyset-pagination-everywhere.md)) often wants a total to show alongside the page
("12 rules"). Counting the true total reintroduces the problem paging solved: an unbounded scan or aggregate
over a set that can be arbitrarily large. A user interface does not need an exact total for a huge set; it
needs enough to say "a lot". So the count should be bounded, and it should say when it hit the bound, so the
caller can render "100+" rather than a wrong exact number.

The count must also agree with the list. If the count used a different filter or a different reach than the
list, the total could disagree with the rows shown.

### Grounded architectural facts

- **The count is bounded and reports capping.** The store seam is
  `ValueTask<(int Count, bool Capped)> CountAsync(<the same filter as the list>, int cap, CancellationToken)`,
  implemented across the count-worthy stores (`IWorkflowCatalogStore`, `IWorkspaceWorkflowStore`,
  `IAvailabilityRequestStore`, `ISecuredWorkflowManagement`, and others). It queries `LIMIT cap+1` and returns
  `(cap, true)` when the extra row exists, else `(actual, false)`.
- **It reuses the list's filter.** `CountAsync` takes the same query the list takes, so the count cannot drift
  from the list, and reach is applied the same way it is for the list.
- **It is allocation-free.** The count materialises a number, not rows.
- **It is exposed per list.** Each count-worthy list has a `GET …/count` endpoint taking the same filters,
  returning `{ "count": N, "capped": bool }`, and the UI renders `${count}+` when capped. The default cap is
  100.

## Decision

A list's total is a **bounded count**: `CountAsync(<same filter>, cap)` returns `(count, capped)`, querying
`LIMIT cap+1` so it materialises no rows and stops at the cap. It reuses the list's filter, so the count
cannot disagree with the rows. The API exposes it as `GET …/count` returning `{ count, capped }`, and a capped
count renders as `${count}+`.

## Consequences

- A total never triggers an unbounded scan. The worst case is counting `cap+1` rows to learn the set is larger
  than the cap.
- The count agrees with the list by construction, because it runs the same filter and reach.
- The UI shows a useful total ("12 rules", "100+ runs") without the cost or the risk of an exact count over a
  large set.
- Counting is allocation-free, consistent with the bytes-native persistence posture
  ([ADR 0037](0037-bytes-native-seams.md)): a count is a number, not a materialised page.
