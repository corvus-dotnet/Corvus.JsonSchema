# ADR 0075. Sign-out revokes: server-side session tickets and a per-subject epoch

Date: 2026-09-28. Status: **Accepted**. Implementation: **complete**. Built: a ticket store in the control-plane server library over the host's `IDistributedCache`, tickets data-protected at rest, a maximum session lifetime the store enforces, sign-out everywhere by a per-subject epoch, the demo host on Aspire Redis, and a kit sign-out that offers "everywhere" when the host says it can. Scope: GAP-2 of the 2026-08-07 security audit, the half that piece 2a left open. It builds on [ADR 0042](0042-auth-agnostic-host-owns-session.md), which makes the session the host's.

## Context

The BFF session is a cookie authentication ticket. With no ticket store, the whole ticket rides in the cookie: the principal, its claims, and, because the OIDC handler saves its tokens, the id token and the refresh token. Sign-out deletes the cookie in the browser that signs out, and nothing else. A copy of the cookie taken before sign-out (a stolen cookie, a second device, a shared machine's other profile) stays valid until it expires, and it carries a refresh token that outlives it.

So sign-out does not revoke, and "sign out everywhere" cannot be built at all: the server holds nothing it could forget.

ADR 0042 leaves the session to the host, and the library ships nothing for it. Every host would have to solve this on its own, and the one host we ship had not.

## Options

**A. Ticket store with a per-subject index.** Keep each ticket server-side, keyed by a random session key; keep, per subject, the set of that subject's session keys; sign out everywhere deletes every key in the set.

**B. Ticket store with a per-subject epoch.** Keep each ticket server-side, stamped with the time its session began. Keep, per subject, one timestamp: sessions that began at or before it are revoked. Sign out everywhere writes the timestamp.

**C. Short cookie lifetime and no store.** Keep the ticket in the cookie and make it short-lived, so a copy dies soon.

**D. Revocation list of ticket ids.** Keep the ticket in the cookie, and keep server-side the ids of tickets signed out.

## Antagonistic review

*Against C:* it shortens the window and closes nothing. The refresh token still rides in the cookie, and "everywhere" still has nothing to act on.

*Against D:* sign-out works, for the one ticket signed out. "Everywhere" needs the ids of tickets the server never saw after issuing, so it cannot be built. The tokens still ride in the cookie.

*Against A:* the index is state that has to agree with the tickets. A ticket that expires leaves its key in the set, so the set needs its own expiry and pruning, and a lost update to it leaves a live session out of "everywhere". It also lists a subject's sessions, which invites a "your active sessions" surface that we have decided never to build.

*Against B:* the epoch has to outlive every session that began before it, or a revoked session comes back when the epoch expires. That needs a bound on how long a session can live, which cookie sliding expiration alone does not give: each renewal issues a ticket with a fresh expiry. *Answer:* the store enforces a maximum session lifetime from the stamped start, and keeps each epoch exactly that long. After that, every session it could revoke has ended on its own.

*Against B:* two cache reads on every authenticated request, the ticket and the epoch. *Answer:* both are single-key reads against the cache the host picked. The cost is the price of revocation, and it is bounded.

*Against a store at all:* the cache becomes a dependency of every request, and losing it signs everyone out. *Answer:* that failure is closed. Users sign in again; no one keeps a session they should not have.

*The eviction hazard:* a cache that evicts entries before their expiry under memory pressure could drop an epoch and keep an older ticket, which would revive a revoked session. *Answer:* the cache must not evict early. Redis's default `maxmemory-policy` is `noeviction`, and a deployment that changes it must keep epochs, which is stated as a requirement of the store.

## Decision

**B.** The control-plane server library ships `ControlPlaneSessionTickets`, an `ITicketStore` over the host's `IDistributedCache`, registered for a cookie scheme with `AddArazzoControlPlaneSessionTickets`. The host picks the cache.

- **The cookie carries a key and nothing else.** The session key is 32 random bytes; the cookie is still data-protected, as before. The ticket, with its claims and saved tokens, is stored under the key, serialized and then protected with a data protector of its own purpose, so the cache never holds a readable token.
- **A session has a maximum lifetime.** The store stamps the time a session began when it first stores the ticket and keeps the stamp through every renewal. A ticket older than `MaximumLifetime` is refused and removed, and each cache entry expires no later than the session's end. The host's idle timeout remains the cookie handler's sliding expiration.
- **Sign-out revokes.** Signing out removes the ticket, so a copy of the cookie finds nothing.
- **Sign out everywhere writes an epoch.** `RevokeAllAsync(subject)` records the current time for the subject, under a key derived by hashing the subject, kept for `MaximumLifetime`. A ticket whose session began at or before its subject's epoch is refused and removed. No index is kept, and nothing lists a subject's sessions.
- **A session has a subject or it is not stored.** The subject is the principal's `NameIdentifier` claim, or `sub`. A ticket with neither is refused at sign-in, because sign-out everywhere could not reach it.

The demo host keeps the cache in Aspire Redis (`sessions`), and in an in-memory cache when run on its own. `/logout` accepts `scope=everywhere`, which revokes the subject's sessions before signing this one out. `/me` says `signOutEverywhere: true`, and the kit's `arazzo-auth-status` offers "Sign out everywhere" only when it does, so a host without the store is never shown an action it cannot honour.

## Consequences

- Sign-out ends the session wherever the cookie has been copied, and the refresh token no longer leaves the server.
- A user can end every session of theirs from any one of them. Nothing shows them a list of sessions.
- Every authenticated browser request reads the cache twice. The cache's availability is the session's availability, and its loss signs everyone out.
- The cache must not evict entries early, since an evicted epoch would revive the sessions it revoked.
- The data-protection key ring now protects stored tickets too. A deployment with several replicas shares it (it had to already, for the cookie), and rotating it out ends existing sessions.
- An administrator ending another user's sessions is not built. `RevokeAllAsync` is the seam it would use.