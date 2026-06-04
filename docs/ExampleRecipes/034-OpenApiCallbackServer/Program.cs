// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using EventSubscription.CallbackServer;
using EventSubscription.CallbackServer.Models;

// ── Hosting a generated callback server ───────────────────────────────────────
// The openapi-callback-server generator walks OpenAPI callbacks and webhooks and
// produces the same kinds of artifacts as openapi-server:
//   • Handler interfaces grouped by tag (IApiCallbacksHandler, IApiWebhooksHandler)
//   • ApiEndpointRegistration.MapApiEndpoints() for ASP.NET Core minimal APIs
//   • Params structs for parsed, validated request bodies
//   • Result structs for typed status-code responses
//
// This example intentionally ships without generated code so the ExampleRecipes
// solution still builds cleanly. Regenerate Generated/ first, then use this file
// as a starting point for your real webhook receiver.

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
WebApplication app = builder.Build();

// A single class can implement multiple generated handler interfaces.
CallbackReceiver handler = new(app.Logger);

// The generated extension method registers one endpoint per callback/webhook.
// Named arguments make it clear which handler instance serves which tag group.
//
// The optional configureEndpoint callback is an additional overload (the bare
// MapApiEndpoints(...) is preserved unchanged). It runs once per generated endpoint,
// after the route is mapped, with a descriptor for the operation and the route's
// IEndpointConventionBuilder — the standard ASP.NET extension point for naming, tags,
// authorization, output caching, rate limiting, and so on.
app.MapApiEndpoints(
    callbacksHandler: handler,
    webhooksHandler: handler,
    OnEventCallbackRoute: "/callbacks/onEvent",
    configureEndpoint: static (in EndpointDescriptor endpoint, IEndpointConventionBuilder builder) =>
    {
        // Give every endpoint a stable name and its OpenAPI tags.
        builder.WithName(endpoint.OperationId ?? endpoint.MethodName);
        builder.WithTags([.. endpoint.Tags]);

        // IsCallback distinguishes webhook/callback deliveries from regular /paths
        // operations — here every endpoint is a delivery, so group them together.
        if (endpoint.IsCallback)
        {
            builder.WithMetadata(new EndpointGroupNameAttribute("event-deliveries"));
        }

        // Translate any declared OpenAPI security into the authorization policies your
        // app has registered. event-api.json declares no security, so this list is empty
        // and the endpoints stay anonymous (the curl calls below return 200). For a spec
        // that does declare security, this is where RequireAuthorization() is applied —
        // see docs/OpenApi.md, "Customizing Generated Endpoints".
        foreach (EndpointSecurityRequirement requirement in endpoint.SecurityRequirements)
        {
            builder.RequireAuthorization($"{requirement.SchemeName}:{string.Join('+', requirement.Scopes)}");
        }
    });

app.Lifetime.ApplicationStarted.Register(() =>
{
    string baseUrl = app.Urls.FirstOrDefault(static url => url.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
        ?? app.Urls.FirstOrDefault()
        ?? "http://localhost:64910";

    Console.WriteLine();
    Console.WriteLine("OpenAPI Callback Server example is running.");
    Console.WriteLine($"Base URL: {baseUrl}");
    Console.WriteLine();
    Console.WriteLine("Registered endpoints:");
    Console.WriteLine("  POST /systemAlert          — receives top-level webhook deliveries");
    Console.WriteLine("  POST /callbacks/onEvent    — receives per-subscription callback deliveries");
    Console.WriteLine();
    Console.WriteLine("Try these requests from another terminal:");
    Console.WriteLine($@"  curl -X POST {baseUrl}/systemAlert -H ""Content-Type: application/json"" -d '{{""alertId"":""alert-001"",""severity"":""critical"",""message"":""Disk usage exceeded 90%.""}}'");
    Console.WriteLine($@"  curl -X POST {baseUrl}/callbacks/onEvent -H ""Content-Type: application/json"" -d '{{""eventId"":""evt-123"",""eventType"":""subscription.created"",""timestamp"":""2026-01-15T10:30:00Z""}}'");
    Console.WriteLine();
});

app.Run();

// ── Implementing callback/webhook handlers ────────────────────────────────────
// Each handler method runs after the generated middleware has:
//   • Parsed the incoming JSON request body
//   • Validated it against the schema from event-api.json
//   • Constructed the strongly-typed Params object
//
// Your code focuses on business logic: persisting the event, dispatching work,
// logging, and returning the typed result that matches the OpenAPI response.

/// <summary>
/// Demonstrates a single receiver handling both callback and webhook traffic.
/// </summary>
internal sealed class CallbackReceiver : IApiCallbacksHandler, IApiWebhooksHandler
{
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CallbackReceiver"/> class.
    /// </summary>
    /// <param name="logger">The application logger.</param>
    public CallbackReceiver(ILogger logger)
    {
        this.logger = logger;
    }

    /// <summary>
    /// Handles POST callback deliveries sent to the subscriber callback URL.
    /// </summary>
    public ValueTask<OnEventCallbackResult> HandleOnEventCallbackAsync(
        OnEventCallbackParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        var body = parameters.Body;
        string eventId = (string)body.EventId;
        string eventType = (string)body.EventType;

        // The schema guarantees the required fields exist. You can still apply
        // domain-specific rules before acknowledging the event.
        if (string.IsNullOrWhiteSpace(eventType))
        {
            this.logger.LogWarning("Rejected callback {EventId} because eventType was empty.", eventId);
            return ValueTask.FromResult(OnEventCallbackResult.BadRequest());
        }

        this.logger.LogInformation(
            "Received callback {EventId} of type {EventType} at {Timestamp}.",
            eventId,
            eventType,
            body.Timestamp);

        return ValueTask.FromResult(OnEventCallbackResult.Ok());
    }

    /// <summary>
    /// Handles POST webhook deliveries defined under the spec's top-level webhooks.
    /// </summary>
    public ValueTask<SystemAlertWebhookResult> HandleSystemAlertWebhookAsync(
        SystemAlertWebhookParams parameters,
        JsonWorkspace workspace,
        CancellationToken cancellationToken = default)
    {
        var body = parameters.Body;

        this.logger.LogInformation(
            "Received {Severity} alert {AlertId}: {Message}",
            body.Severity,
            body.AlertId,
            body.Message);

        return ValueTask.FromResult(SystemAlertWebhookResult.Ok());
    }
}