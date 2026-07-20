// <copyright file="ExampleSeed.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.Arazzo.Durability;
using Corvus.Text.Json.Arazzo.Durability.Availability;
using Corvus.Text.Json.Arazzo.Durability.Environments;
using Corvus.Text.Json.Arazzo.Durability.Security;
using Corvus.Text.Json.Arazzo.Durability.Sources;

using CpEnvironment = Corvus.Text.Json.Arazzo.Durability.Environments.Environment;
using VerbGrant = Corvus.Text.Json.Arazzo.Durability.Security.SecurityBindingDocument.VerbGrantInfo;

namespace Corvus.Text.Json.Arazzo.ControlPlane.Demo;

/// <summary>
/// The connected durable stores an <see cref="IExampleSeed"/> writes its demo content into. The seam carries the
/// stores as their backend-agnostic interfaces (the sample happens to bind Postgres implementations), so the seed
/// itself is decoupled from the chosen store backend.
/// </summary>
/// <param name="Catalog">The secured workflow catalog the demo's catalogued versions are added to.</param>
/// <param name="SourceCredentials">The §13 source-credential store the demo's credential references are added to.</param>
/// <param name="EnvironmentStore">The governed environment store the demo's environments are added to.</param>
/// <param name="EnvironmentAdministrators">The per-environment administrator store (§7.7) the demo grants admin on.</param>
/// <param name="Sources">The source registry (§7.8) the demo's referenced source specs are registered in.</param>
/// <param name="Availability">The availability store (§7.6) the demo marks catalogued versions "Available in".</param>
/// <param name="AccessRequests">The access-request store (§16.5) the demo seeds a pending request into.</param>
/// <param name="AvailabilityRequests">The availability (promotion) request store (§7.6) the demo seeds a pending request into.</param>
/// <param name="SecurityPolicy">The §14.2 rule/binding store the demo's persona reach rules + grants are written to.</param>
/// <param name="SpecsDir">The directory holding the demo's Arazzo + OpenAPI specification packages.</param>
public sealed record ExampleSeedContext(
    SecuredWorkflowCatalog Catalog,
    ISourceCredentialStore SourceCredentials,
    IEnvironmentStore EnvironmentStore,
    IEnvironmentAdministratorStore EnvironmentAdministrators,
    ISourceStore Sources,
    IAvailabilityStore Availability,
    IAccessRequestStore AccessRequests,
    IAvailabilityRequestStore AvailabilityRequests,
    ISecurityPolicyStore SecurityPolicy,
    string SpecsDir);

/// <summary>
/// Seeds the sample's <em>example</em> content — the demo fiction: catalogued workflow versions, source-credential
/// references, a developer sandbox environment, and a live-executed run. This is the counterpart to
/// <c>IDeploymentBootstrap</c> (the real, config-driven bootstrapping every deployment performs): a production
/// deployment runs only the bootstrap, whereas the sample additionally runs this seed when <c>seedExampleData</c>
/// is set. Keeping the two apart is what lets the real bootstrap graduate into a configurable deployment capability
/// without dragging the demo fiction along.
/// </summary>
public interface IExampleSeed
{
    /// <summary>
    /// Seeds the demo's example content into the connected stores: catalogued workflow versions, source-credential
    /// references (references only — never secret material), the governed environments (development / staging /
    /// production) with the arazzo-admins group as their administrator, the registered source specs, an availability
    /// entry, and a pending access + promotion request — so every governance surface has content out of the box.
    /// </summary>
    /// <param name="context">The connected stores + specification directory to seed into.</param>
    /// <param name="cancellationToken">Cancels the seed.</param>
    /// <returns>A task that completes when the demo content has been written.</returns>
    ValueTask SeedAsync(ExampleSeedContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes one fresh onboarding run live once the host is listening, so the demo shows a genuinely-executed run
    /// rather than only hand-seeded states.
    /// </summary>
    /// <param name="stateStore">The run store the live run is written to.</param>
    /// <param name="resumer">The live resumer that re-enters the run's compiled executor against the demo services.</param>
    /// <param name="log">An optional progress sink.</param>
    /// <returns>A task that completes when the live run has advanced to its first pause (or completion).</returns>
    ValueTask RunLiveSampleAsync(IWorkflowStateStore stateStore, WorkflowResumer resumer, Action<string>? log = null);
}

/// <summary>
/// The Arazzo control-plane sample's <see cref="IExampleSeed"/>: the demo fiction that makes the browsable sample
/// self-contained. It delegates the catalogued workflow content to <see cref="DemoData"/> and adds the
/// source-credential references + developer sandbox environment out of the box.
/// </summary>
public sealed class ArazzoExampleSeed : IExampleSeed
{
    /// <inheritdoc/>
    public async ValueTask SeedAsync(ExampleSeedContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Catalogued workflow versions from real packages (an Arazzo workflow + its OpenAPI sources), so the browsable
        // demo exercises the genuine metadata generator and JSON Schema validator.
        await DemoData.SeedAsync(context.Catalog, context.SpecsDir);

        // The arazzo-admins group's resolved identity (sys:group + the pinned sys:iss issuer) — the identity the
        // governance seeds below key on: production credential usage, environment administration.
        SecurityTagSet adminGroup = DemoData.GroupIdentity("arazzo-admins");

        // Source-credential bindings — references only (the §13 invariant: never secret material). Each points at the
        // Vault path the AppHost's provisioner seeds (vault://secret/arazzo/<source>#api-key); the runner resolves it
        // with its read-only token. This populates /credentials (and the CLI + web UI) out of the box. The PRODUCTION
        // bindings are usage-scoped to the admin group (§13: only runs carrying the admin identity are entitled), so
        // the access overview shows the admin team's credential rights; development stays shared (empty usage tags) so
        // any debug run resolves its credentials.
        // The preprod-zone label: staging's environment row AND its credential set both carry it, so the same
        // zone-access:preprod rule that lets erin/wanda see the environment also lets them see its credentials —
        // which is what makes the availability-request dialog's readiness computation work for them (it reads
        // GET /credentials under the CALLER's reach; an untagged row is visible only to unrestricted readers,
        // because a rule has no dimensions to match on an empty tag set).
        SecurityTagSet preprodZone = SecurityTagSet.FromTags([new SecurityTag("zone", "preprod")]);

        // The prod zone mirrors it (§14.2 realism, backlog #862): production's row and credentials carry zone=prod,
        // so rule-based routes to production exist at all — prod-ops administers through one, payments sees its
        // promotion target through another. Founder-group membership stops being the only door.
        SecurityTagSet prodZone = SecurityTagSet.FromTags([new SecurityTag("zone", "prod")]);

        foreach (string environment in new[] { "production", "staging", "development" })
        {
            // production is usage-scoped to the admin group; staging and development stay shared (empty usage
            // tags). Credentialing STAGING keeps the promotion story real: the availability-request dialog offers
            // only environments where the version is READY (every referenced source credentialed there), so
            // wanda's request for staging is constructible end to end.
            SecurityTagSet usage = environment == "production" ? adminGroup : SecurityTagSet.Empty;
            SecurityTagSet management = environment switch
            {
                "staging" => preprodZone,
                "production" => prodZone, // visible to prod-zone routes, so a production promotion's readiness computes for its requester
                _ => SecurityTagSet.Empty,
            };

            // 'notifications' is the AsyncAPI (NATS) source onboard-customer-async binds (kyc.requests / kyc.verdict). Its
            // transport is the message binder, not a per-source API key, but the debug-run readiness gate requires a binding
            // for EVERY referenced source — so it is seeded here too (its Vault #api-key is provisioned but never resolved
            // for the channel). Without it, a debug run of the async workflow fails 409 "No credential bound for: notifications".
            foreach (string source in new[] { "onboarding", "ledger", "kyc", "notifications" })
            {
                // AddAsync returns the persisted binding as a pooled document — dispose it (the seed doesn't read it back).
                using ParsedJsonDocument<SourceCredentialBinding> seeded = await context.SourceCredentials.AddAsync(
                    new SourceCredentialDefinition(
                        source,
                        environment,
                        SourceCredentialKind.ApiKey,
                        // The ApiKey provider resolves the key from the secret reference named "value" (default header
                        // X-API-Key); the reference target is the Vault path's #api-key field the provisioner seeds.
                        [new SecretReferenceDefinition("value", $"vault://secret/arazzo/{source}#api-key")],
                        ManagementTags: management,
                        UsageTags: usage),
                    "demo",
                    cancellationToken);
            }
        }

        // Governed environments (§7.7): the developer sandbox (working-copy drafts may run here as §18 debug runs — a
        // deliberate per-environment decision, off everywhere else) plus the two promoted environments a real
        // deployment ships with. development's two sources are credentialed above, so the run dialog's readiness gate
        // passes and a debug run executes against the real external source services.
        foreach ((string name, string displayName, string description, bool allowsDraftRuns) in new[]
        {
            ("development", "Development", "Developer sandbox — working-copy drafts may run here as debug runs (§18).", true),
            ("staging", "Staging", "Pre-production verification environment.", false),
            ("production", "Production", "Live production environment.", false),
        })
        {
            // Reach stamps (§14.2): the deployment stamps who may SEE an environment at create — the HTTP create
            // path does exactly this with the creator's internal identity, so the seed mirrors it with the founder
            // group. Staging ADDITIONALLY carries the user tag the zone-access:preprod rule (seeded with the persona
            // bindings below) matches: administration alone never confers reach, so erin (env-admins, administers
            // staging) and wanda (reconcile-owners, names staging as a promotion target) route their staging
            // visibility through that rule while development/production stay founder-only.
            SecurityTagSet reach = name switch
            {
                "staging" => SecurityTagSet.FromTags(
                    [
                        new SecurityTag(SecurityShell.DefaultInternalPrefix + "group", "arazzo-admins"),
                        DemoData.IssuerTag,
                        new SecurityTag("zone", "preprod"),
                    ]),
                // production carries zone=prod so rule-based routes to it EXIST (prod-ops administration,
                // payments' promotion-target visibility) — founders are no longer the only door (#862).
                "production" => SecurityTagSet.FromTags(
                    [
                        new SecurityTag(SecurityShell.DefaultInternalPrefix + "group", "arazzo-admins"),
                        DemoData.IssuerTag,
                        new SecurityTag("zone", "prod"),
                    ]),
                // development carries zone=dev so a non-founder route to it exists (#896): a scheduling runner must SEE
                // the environment it fires schedules into, and a governed run start reach-gates the environment read
                // (§5.5). Additive — founders still see it via the group tag.
                "development" => SecurityTagSet.FromTags(
                    [
                        new SecurityTag(SecurityShell.DefaultInternalPrefix + "group", "arazzo-admins"),
                        DemoData.IssuerTag,
                        new SecurityTag("zone", "dev"),
                    ]),
                _ => adminGroup,
            };
            using ParsedJsonDocument<CpEnvironment.SecurityTagInfoArray> reachTags =
                PersistedJson.ToPooledDocument<CpEnvironment.SecurityTagInfoArray>(reach.RawJson);

            // The generated Create() builds the draft in one pooled pass (and, unlike the interpolated-string parse it
            // replaces, cannot produce invalid JSON from a value containing a quote). The server-stamped fields are
            // omitted via default Sources for the store to stamp.
            using ParsedJsonDocument<CpEnvironment> environment = CpEnvironment.Create(
                createdAt: default,
                createdBy: default,
                etag: default,
                name: name,
                allowsDraftRuns: allowsDraftRuns,
                description: description,
                displayName: displayName,
                managementTags: (CpEnvironment.SecurityTagInfoArray.Source)reachTags.RootElement);
            (await context.EnvironmentStore.AddAsync(environment.RootElement, "demo", cancellationToken)).Dispose();
        }

        // Environment administrators (§7.7): the arazzo-admins group administers every environment — the key unblock so
        // an administrator can manage grants, promotions, and availability per environment (without this, no interactive
        // administrator exists for the governance surfaces). The identity is the resolved sys:group=arazzo-admins tag.
        var administration = new SecuredEnvironmentAdministration(context.EnvironmentAdministrators, "demo");
        foreach (string environmentName in new[] { "development", "staging", "production" })
        {
            await administration.EstablishAsync(environmentName, adminGroup, default, false, default, false, cancellationToken);
        }

        // Source registry (§7.8): register the four referenced sources from their real specs, so the sources panel and
        // the workflow authoring surface resolve them. onboarding/ledger/kyc are OpenAPI; notifications is the AsyncAPI
        // (NATS) channel. Management tags are empty (a real deployment would tag by reach).
        foreach ((string name, string file, string type, string displayName, string description) in new[]
        {
            ("onboarding", "onboarding.openapi.json", "openapi", "Onboarding API", "Customer onboarding service."),
            ("ledger", "ledger.openapi.json", "openapi", "Ledger API", "Reconciliation and ledger service."),
            ("kyc", "kyc.openapi.json", "openapi", "KYC API", "Identity-verification service."),
            ("notifications", "notifications.asyncapi.json", "asyncapi", "Notifications", "KYC review request/verdict channel (NATS)."),
        })
        {
            ReadOnlyMemory<byte> documentUtf8 = File.ReadAllBytes(Path.Combine(context.SpecsDir, file));
            using ParsedJsonDocument<RegisteredSource> source = RegisteredSource.Draft(
                name, type, documentUtf8, displayName, description, SecurityTagSet.Empty);
            (await context.Sources.AddAsync(source.RootElement, "demo", cancellationToken)).Dispose();
        }

        // Availability (§7.6): the first catalogued onboarding version is "Available in" production, so the runs surface
        // can start it there and the availability panel is not empty out of the box.
        (await context.Availability.MakeAvailableAsync("onboard-customer", 1, "production", "demo", cancellationToken)).Entry.Dispose();

        // The active nightly-reconcile version is "Available in" development so the durable schedule (#896) can fire it
        // there through the governed run endpoint — a governed scheduled start is gated by §7.8 availability exactly as
        // an operator start is.
        (await context.Availability.MakeAvailableAsync("nightly-reconcile", 2, "development", "demo", cancellationToken)).Entry.Dispose();

        // Security personas (§14.2/§15/§16.5): three archetypes beyond the genesis admin, so the governance surfaces
        // show reach-scoping, the administration split, and the PIM lifecycle out of the box. The realm import defines
        // the matching Keycloak groups + users (oscar/observers, erin/env-admins, wanda/reconcile-owners); everyone
        // gets the claims transformer's read-scope baseline by membership, so the bindings below carry only what each
        // persona holds BEYOND it.
        await SeedPersonasAsync(context, adminGroup, cancellationToken);

        // Inbox content for the admin governance surfaces: a pending promotion request (§7.6, asking to make a
        // catalogued version available in production). The pending ACCESS request is oscar's, seeded with his persona.
        using (ParsedJsonDocument<AvailabilityRequest> promotion = AvailabilityRequest.Draft(
            "onboard-customer", 2, "production", "Please promote v2 to production.", requesterLabel: "Alice Payments"))
        {
            (await context.AvailabilityRequests.CreateAsync(promotion.RootElement, "alice", cancellationToken)).Dispose();
        }
    }

    // The persona seed: reach rules + bindings, the administration split, and the PIM lifecycle documents.
    //   observers        — reach-scoped READ: a rule-bounded read reach over onboard-customer rows only.
    //   env-admins       — environment administration only: staging, plus the environments/availability scopes.
    //   reconcile-owners — single-workflow administration: nightly-reconcile (routes its requests to wanda's inbox).
    //   prod-ops         — production administration through the zone=prod route (pia); founders shrink toward break-glass (#862).
    //   payments         — alice's team: reads onboarding + sees the prod zone, so her seeded promotion request is constructible.
    //   alice            — an APPROVED, time-boxed runs grant (its request decided, binding written the §16.5 approval
    //                      shape) plus an eligible-only purge assignment (self-elevation, conferring nothing active).
    //   oscar            — the PENDING access request that populates the approver inboxes.
    private static async ValueTask SeedPersonasAsync(ExampleSeedContext context, SecurityTagSet adminGroup, CancellationToken cancellationToken)
    {
        // Per-workflow reach rules, named exactly as the §16.5 approval service names them ("workflow-access:<id>"),
        // so an approval that later needs the same rule finds these idempotently instead of duplicating.
        foreach (string baseWorkflowId in new[] { "onboard-customer", "nightly-reconcile" })
        {
            using ParsedJsonDocument<SecurityRuleDocument> rule = SecurityRuleDocument.Draft(
                WorkflowIdentity.WorkflowTagKey + " == '" + baseWorkflowId + "'", "Run access to workflow " + baseWorkflowId + ".");
            (await context.SecurityPolicy.AddRuleAsync("workflow-access:" + baseWorkflowId, rule.RootElement, "demo", cancellationToken)).Dispose();
        }

        // The preprod ZONE rule matches staging's user tag (stamped at environment create): the reach route for the
        // two non-founder groups that must SEE staging — erin to operate it, wanda to promote into it.
        using (ParsedJsonDocument<SecurityRuleDocument> zoneRule = SecurityRuleDocument.Draft(
            "zone == 'preprod'", "The pre-production zone (staging)."))
        {
            (await context.SecurityPolicy.AddRuleAsync("zone-access:preprod", zoneRule.RootElement, "demo", cancellationToken)).Dispose();
        }

        // The prod-zone rule (#862): the rule-based door to production the zone=prod stamps above open.
        using (ParsedJsonDocument<SecurityRuleDocument> prodRule = SecurityRuleDocument.Draft(
            "zone == 'prod'", "The production zone."))
        {
            (await context.SecurityPolicy.AddRuleAsync("zone-access:prod", prodRule.RootElement, "demo", cancellationToken)).Dispose();
        }

        // The dev-zone rule (#896): the rule-based door to development the zone=dev stamp above opens — the reach route
        // for the scheduling runner to SEE the environment it fires schedules into.
        using (ParsedJsonDocument<SecurityRuleDocument> devRule = SecurityRuleDocument.Draft(
            "zone == 'dev'", "The development zone (sandbox)."))
        {
            (await context.SecurityPolicy.AddRuleAsync("zone-access:dev", devRule.RootElement, "demo", cancellationToken)).Dispose();
        }

        VerbGrant onboardReach = VerbGrant.Rules("workflow-access:onboard-customer");
        VerbGrant reconcileReach = VerbGrant.Rules("workflow-access:nightly-reconcile");
        VerbGrant preprodReach = VerbGrant.Rules("zone-access:preprod");
        VerbGrant prodReach = VerbGrant.Rules("zone-access:prod");
        VerbGrant devReach = VerbGrant.Rules("zone-access:dev");
        (string Dimension, string? Value)[] issuerPin = [("iss", DemoData.KeycloakIssuer)];

        // observers: reach only (read, bounded to onboard-customer rows) — capability stays the membership baseline.
        using (ParsedJsonDocument<SecurityBindingDocument> observers = SecurityBindingDocument.Draft(
            "group", "observers", onboardReach, VerbGrant.None, VerbGrant.None, order: 20,
            description: "Observers may read onboard-customer rows only (reach-scoped read).",
            additionalClauses: issuerPin))
        {
            (await context.SecurityPolicy.AddBindingAsync(observers.RootElement, "demo", cancellationToken)).Dispose();
        }

        // env-admins: the environments/availability capability scopes, plus read reach over the preprod zone —
        // administration alone never confers reach, so seeing the staging row they administer routes through the rule.
        using (ParsedJsonDocument<SecurityBindingDocument> envAdmins = SecurityBindingDocument.Draft(
            "group", "env-admins", preprodReach, VerbGrant.None, VerbGrant.None, order: 21,
            description: "Environment administrators hold the environments/availability capability scopes and see the preprod zone.",
            scopes: ["environments:read", "environments:write", "availability:read", "availability:write"],
            additionalClauses: issuerPin))
        {
            (await context.SecurityPolicy.AddBindingAsync(envAdmins.RootElement, "demo", cancellationToken)).Dispose();
        }

        // reconcile-owners: single-workflow administration — reach over nightly-reconcile rows plus the scopes to
        // manage its administrator set and remediate its runs. The environments/availability read scopes ride here
        // too, so a reconcile owner can construct a promotion request naming staging as the target (§7.8).
        using (ParsedJsonDocument<SecurityBindingDocument> reconcileOwners = SecurityBindingDocument.Draft(
            "group", "reconcile-owners", reconcileReach, reconcileReach, VerbGrant.None, order: 22,
            description: "Reconcile owners administer nightly-reconcile: its rows, its administrator set, its runs.",
            scopes: ["administrators:write", "runs:write", "environments:read", "availability:read"],
            additionalClauses: issuerPin))
        {
            (await context.SecurityPolicy.AddBindingAsync(reconcileOwners.RootElement, "demo", cancellationToken)).Dispose();
        }

        // A verb grant's rules are a CONJUNCTION (every rule must admit the row), so the preprod-zone reach is its
        // own binding — a second route, unioned with the one above: nightly rows through workflow-access, the
        // staging environment row through zone-access. One binding with both rules would demand rows satisfying
        // BOTH, which nothing does.
        using (ParsedJsonDocument<SecurityBindingDocument> reconcileZone = SecurityBindingDocument.Draft(
            "group", "reconcile-owners", preprodReach, VerbGrant.None, VerbGrant.None, order: 23,
            description: "Reconcile owners see the preprod zone (staging) they promote into.",
            additionalClauses: issuerPin))
        {
            (await context.SecurityPolicy.AddBindingAsync(reconcileZone.RootElement, "demo", cancellationToken)).Dispose();
        }

        // prod-ops: the production analogue of env-admins (#862) — the environments/availability capability scopes
        // plus read reach over the prod zone, so production is administered by a dedicated operations group rather
        // than the founder group alone (which shrinks toward break-glass).
        using (ParsedJsonDocument<SecurityBindingDocument> prodOps = SecurityBindingDocument.Draft(
            "group", "prod-ops", prodReach, VerbGrant.None, VerbGrant.None, order: 24,
            description: "Production operators hold the environments/availability capability scopes and see the prod zone.",
            scopes: ["environments:read", "environments:write", "availability:read", "availability:write"],
            additionalClauses: issuerPin))
        {
            (await context.SecurityPolicy.AddBindingAsync(prodOps.RootElement, "demo", cancellationToken)).Dispose();
        }

        // payments: the seeded production promotion request must be CONSTRUCTIBLE by its requester (#862) —
        // alice needs the target environment in reach AND the workflow's rows readable, each its own binding
        // (a verb grant's rules are a conjunction; two routes union). Plus the read scopes the request dialog
        // uses, exactly wanda's shape for staging.
        using (ParsedJsonDocument<SecurityBindingDocument> paymentsProd = SecurityBindingDocument.Draft(
            "group", "payments", prodReach, VerbGrant.None, VerbGrant.None, order: 25,
            description: "Payments see the prod zone they request promotions into.",
            scopes: ["environments:read", "availability:read"],
            additionalClauses: issuerPin))
        {
            (await context.SecurityPolicy.AddBindingAsync(paymentsProd.RootElement, "demo", cancellationToken)).Dispose();
        }

        using (ParsedJsonDocument<SecurityBindingDocument> paymentsOnboarding = SecurityBindingDocument.Draft(
            "group", "payments", onboardReach, VerbGrant.None, VerbGrant.None, order: 26,
            description: "Payments read the onboarding workflow they own.",
            additionalClauses: issuerPin))
        {
            (await context.SecurityPolicy.AddBindingAsync(paymentsOnboarding.RootElement, "demo", cancellationToken)).Dispose();
        }

        // arazzo-schedulers (#896): the scheduling runner's machine principal (the arazzo-runner client hardcodes this
        // group). It holds READ reach over nightly-reconcile rows ONLY — enough to read the version and fire the schedule
        // through the governed run endpoint, and nothing more (no write reach, no administration). Its runs:write
        // capability rides its Keycloak client scope, not this grant. This is the reach a scheduling principal genuinely
        // needs: to SEE what it schedules. Granting more would make the runner an operator over those rows.
        using (ParsedJsonDocument<SecurityBindingDocument> schedulers = SecurityBindingDocument.Draft(
            "group", "arazzo-schedulers", reconcileReach, VerbGrant.None, VerbGrant.None, order: 27,
            description: "The scheduling runner reads nightly-reconcile rows to fire its schedule (read reach only).",
            additionalClauses: issuerPin))
        {
            (await context.SecurityPolicy.AddBindingAsync(schedulers.RootElement, "demo", cancellationToken)).Dispose();
        }

        // A verb grant's rules are a conjunction, so the environment reach is its own binding (a second route, unioned):
        // the workflow row through workflow-access, the development environment row through zone-access. A governed run
        // start reach-gates BOTH the version and the environment (§5.5), so the scheduling runner needs both routes.
        using (ParsedJsonDocument<SecurityBindingDocument> schedulerZone = SecurityBindingDocument.Draft(
            "group", "arazzo-schedulers", devReach, VerbGrant.None, VerbGrant.None, order: 28,
            description: "The scheduling runner sees the development environment it fires schedules into (read reach only).",
            additionalClauses: issuerPin))
        {
            (await context.SecurityPolicy.AddBindingAsync(schedulerZone.RootElement, "demo", cancellationToken)).Dispose();
        }

        // The administration split: erin's group co-administers staging (arazzo-admins established it above); wanda's
        // group co-administers nightly-reconcile (the arazzo-admins founder adds it, exactly as the API would);
        // pia's prod-ops co-administers production (#862).
        var administration = new SecuredEnvironmentAdministration(context.EnvironmentAdministrators, "demo");
        (await administration.AddAdministratorAsync(
            "staging", DemoData.GroupIdentity("env-admins"), default, false, default, false, adminGroup, cancellationToken)).Dispose();
        (await administration.AddAdministratorAsync(
            "production", DemoData.GroupIdentity("prod-ops"), default, false, default, false, adminGroup, cancellationToken)).Dispose();
        (await context.Catalog.AddAdministratorAsync(
            "nightly-reconcile", DemoData.GroupIdentity("reconcile-owners"), default, false, default, false, adminGroup, cancellationToken)).Dispose();

        // alice's PIM lifecycle. The ACTIVE grant is seeded the exact §16.5 approval shape (subject claim binding,
        // granted scopes + their matching reach, time-boxed), then her request is decided Approved pointing at it —
        // so the requests panel, the grants list, and the access overview all tell the same story.
        DateTimeOffset grantedUntil = DateTimeOffset.UtcNow.AddHours(8);
        string grantedBindingId;
        using (ParsedJsonDocument<SecurityBindingDocument> grant = SecurityBindingDocument.Draft(
            "sub", "alice", onboardReach, onboardReach, VerbGrant.None, order: 30,
            description: "Access request: on-call incident response (approved).",
            scopes: ["runs:read", "runs:write"],
            expiresAt: grantedUntil))
        {
            using ParsedJsonDocument<SecurityBindingDocument> added = await context.SecurityPolicy.AddBindingAsync(grant.RootElement, "arazzo-admin", cancellationToken);
            grantedBindingId = added.RootElement.IdValue;
        }

        using (ParsedJsonDocument<AccessRequest> approved = AccessRequest.Draft(
            "onboard-customer", ["runs:write", "runs:read"], "sub", "alice", "Alice (Payments)", "On-call incident response.", 8 * 3600))
        {
            string requestId;
            using (ParsedJsonDocument<AccessRequest> created = await context.AccessRequests.CreateAsync(approved.RootElement, "alice", cancellationToken))
            {
                requestId = created.RootElement.IdValue;
            }

            (await context.AccessRequests.DecideAsync(
                requestId,
                new AccessRequestDecision(AccessRequestStatus.Approved, "Approved for the on-call window.", grantedBindingId, grantedUntil),
                WorkflowEtag.None,
                "arazzo-admin",
                cancellationToken))?.Dispose();
        }

        // alice's eligible-only assignment (§16.5.3): records that she MAY self-elevate to runs:purge; it confers
        // nothing active (the resolver skips eligibleOnly bindings) until an explicit, audited activation.
        using (ParsedJsonDocument<SecurityBindingDocument> eligible = SecurityBindingDocument.Draft(
            "sub", "alice", VerbGrant.None, VerbGrant.None, VerbGrant.None, order: 31,
            description: "Eligibility: alice may self-elevate to purge onboard-customer runs.",
            scopes: ["runs:purge"], eligibleOnly: true))
        {
            (await context.SecurityPolicy.AddBindingAsync(eligible.RootElement, "arazzo-admin", cancellationToken)).Dispose();
        }

        // oscar's PENDING request: the approver-inbox content (routed to the onboard-customer administrators).
        using (ParsedJsonDocument<AccessRequest> pending = AccessRequest.Draft(
            "onboard-customer", ["runs:write"], "sub", "oscar", "Oscar (Observer)", "Investigating a stuck onboarding run.", 4 * 3600))
        {
            (await context.AccessRequests.CreateAsync(pending.RootElement, "oscar", cancellationToken)).Dispose();
        }
    }

    /// <inheritdoc/>
    public ValueTask RunLiveSampleAsync(IWorkflowStateStore stateStore, WorkflowResumer resumer, Action<string>? log = null)
        => DemoData.RunLiveOnboardingAsync(stateStore, resumer, log);
}