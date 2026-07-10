// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The KYC domain service, hosted as its own process (design §2 external source). It owns all identity verification —
// the synchronous verifyIdentity the onboard-customer workflow calls, and the asynchronous, manual-recovery verdict
// (submitVerdict) it PUBLISHES onto the application's message bus. It is a genuine backend the Arazzo control plane and
// runner call over the network, and it owns its own PostgreSQL database (the AppHost injects ConnectionStrings:kycdb).
using Corvus.Text.Json.Arazzo.Samples.Kyc;
using Corvus.Text.Json.Arazzo.Samples.Notifications;
using Corvus.Text.Json.AsyncApi.Nats;
using Npgsql;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// OpenTelemetry (incl. the Corvus.Arazzo source/meter), health checks, service discovery, HTTP resilience.
builder.AddServiceDefaults();

// The service's own database — provisioned and owned by this service alone (the AppHost injects the connection string).
string connectionString = builder.Configuration.GetConnectionString("kycdb")
    ?? throw new InvalidOperationException("ConnectionStrings:kycdb (the KYC service's own database) is required — run the KYC service under the AppHost.");

// The application-owned message bus (NATS JetStream) — the AppHost injects its URL. The KYC service sits on BOTH sides
// of the async exchange: it SUBSCRIBES to kyc.requests (its manual-recovery inbox — each review request becomes a
// 'pending' verification) and PUBLISHES to kyc.verdict (submitVerdict). Each channel is its own JetStream stream
// (the transport creates a stream capturing one subject), so it needs one transport per channel. JetStream
// (durable, file-backed) + DeliverPolicy.All so a request published before this service subscribed is not lost.
string natsUrl = builder.Configuration["Nats:Url"]
    ?? throw new InvalidOperationException("Nats:Url (the KYC message bus) is required — the AppHost injects it.");
NatsMessageTransport requestsTransport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
{
    Url = natsUrl,
    Name = "kyc-requests-in",
    UseJetStream = true,
    StreamName = "kyc-requests",
    ConsumerName = "kyc-review-consumer",
    DeliverPolicy = DeliverPolicy.All,
    StorageType = StorageType.File,
});
NatsMessageTransport verdictsTransport = await NatsMessageTransport.CreateAsync(new NatsTransportOptions
{
    Url = natsUrl,
    Name = "kyc-verdicts-out",
    UseJetStream = true,
    StreamName = "kyc-verdicts",
    StorageType = StorageType.File,
});
var verdictProducer = new PublishKycVerdictProducer(verdictsTransport);

// Provision the schema (the service owns its DDL), then open the store.
await KycStore.PrepareAsync(connectionString);
NpgsqlDataSource dataSource = NpgsqlDataSource.Create(connectionString);
builder.Services.AddSingleton(dataSource);
KycStore store = await KycStore.ConnectAsync(dataSource);
var handler = new KycService(store, new IdentityVerificationPolicy(), verdictProducer);

// Start the review-request inbox: subscribe to kyc.requests and record each as a pending verification.
var reviewConsumer = new ReceiveKycReviewConsumer(requestsTransport, new KycReviewHandler(store));
await reviewConsumer.StartAsync();

WebApplication app = builder.Build();

// /health (readiness) and /alive (liveness) — the AppHost's health check polls these.
app.MapDefaultEndpoints();

// The generated KYC API: POST /accounts/{id}/identity (verifyIdentity), GET /verifications, GET /accounts/{id}/verification.
ApiEndpointRegistration.MapApiEndpoints(app, handler);

// A tiny identity endpoint so the dashboard's resource link lands somewhere informative.
app.MapGet("/", () => Results.Text("Arazzo KYC service — verifications under /verifications, health at /health.", "text/plain"));

app.Run();
