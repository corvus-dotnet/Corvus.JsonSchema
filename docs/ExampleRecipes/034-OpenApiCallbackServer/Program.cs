// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using EventSubscription.CallbackServer;

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
app.MapApiEndpoints(
    callbacksHandler: handler,
    webhooksHandler: handler);

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