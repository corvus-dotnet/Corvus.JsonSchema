// <copyright file="MongoScheduleRegistry.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Schedules;

using MongoDB.Bson;
using MongoDB.Driver;

namespace Corvus.Text.Json.Arazzo.Durability.Mongo;

/// <summary>
/// A MongoDB-backed <see cref="IScheduleRegistry"/>: one document per <c>scheduleId</c> (the <c>_id</c>) whose
/// automatic <c>_id</c> uniqueness enforces the schedules contract's deployment-global id uniqueness on
/// insert. Under the composite <c>(environment, runId)</c> run key (ADR 0065 decision 9) the registry is the
/// sole guardian of that uniqueness — a run-key collision can no longer observe a schedule created in another
/// environment — so a MongoDB deployment needs this durable registry, not the in-memory one, or every
/// control-plane restart forgets which environment holds each schedule id.
/// </summary>
/// <remarks>
/// The driver pools connections internally, so the registry is naturally concurrent. It needs no provisioning:
/// the collection is created lazily and the <c>_id</c> index is automatic, so
/// <see cref="ConnectAsync(string, string, CancellationToken)"/> is the whole setup.
/// </remarks>
public sealed class MongoScheduleRegistry : IScheduleRegistry, IAsyncDisposable
{
    private readonly IMongoClient client;
    private readonly IMongoCollection<BsonDocument> registrations;
    private readonly bool ownsClient;

    private MongoScheduleRegistry(IMongoClient client, string databaseName, bool ownsClient)
    {
        this.client = client;
        this.ownsClient = ownsClient;
        this.registrations = client.GetDatabase(databaseName).GetCollection<BsonDocument>("schedule_registrations");
    }

    /// <summary>Opens the registry for operation.</summary>
    /// <param name="connectionString">A MongoDB connection string (e.g. <c>mongodb://localhost:27017</c>).</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it owns and disposes the client).</returns>
    public static ValueTask<MongoScheduleRegistry> ConnectAsync(string connectionString, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectionString);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoScheduleRegistry>(new MongoScheduleRegistry(new MongoClient(connectionString), databaseName, ownsClient: true));
    }

    /// <summary>Opens the registry for operation over a caller-supplied client (the caller retains ownership).</summary>
    /// <param name="client">A configured MongoDB client.</param>
    /// <param name="databaseName">The database to use; defaults to <c>arazzo</c>.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The opened registry (it does not dispose the supplied client).</returns>
    public static ValueTask<MongoScheduleRegistry> ConnectAsync(IMongoClient client, string databaseName = "arazzo", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        cancellationToken.ThrowIfCancellationRequested();
        return new ValueTask<MongoScheduleRegistry>(new MongoScheduleRegistry(client, databaseName, ownsClient: false));
    }

    /// <inheritdoc/>
    public async ValueTask RegisterAsync(string scheduleId, ScheduleRegistration registration, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        // The _id insert conflict IS the uniqueness gate; the read below sees the committed occupant.
        var document = new BsonDocument
        {
            ["_id"] = scheduleId,
            ["environment"] = registration.Environment,
            ["runId"] = registration.RunId.Value,
        };
        try
        {
            await this.registrations.InsertOneAsync(document, options: null, cancellationToken).ConfigureAwait(false);
            return;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            // Occupied: fall through to compare with the occupant.
        }

        // Occupied: an identical registration is the crash-retry/redelivery convergence and succeeds; anything
        // else refuses without disclosing the occupant (the caller may have no reach over its environment).
        BsonDocument? existing = await this.registrations.Find(Builders<BsonDocument>.Filter.Eq("_id", scheduleId)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (existing is not null
            && new ScheduleRegistration(existing["environment"].AsString, new WorkflowRunId(existing["runId"].AsString)) == registration)
        {
            return;
        }

        ThrowHelper.ThrowScheduleRegistrationConflict(scheduleId);
    }

    /// <inheritdoc/>
    public async ValueTask<ScheduleRegistration?> ResolveAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        BsonDocument? document = await this.registrations.Find(Builders<BsonDocument>.Filter.Eq("_id", scheduleId)).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return document is null
            ? null
            : new ScheduleRegistration(document["environment"].AsString, new WorkflowRunId(document["runId"].AsString));
    }

    /// <inheritdoc/>
    public async ValueTask UnregisterAsync(string scheduleId, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(scheduleId);

        await this.registrations.DeleteOneAsync(Builders<BsonDocument>.Filter.Eq("_id", scheduleId), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>Disposes the client if this registry created it (from a connection string).</summary>
    /// <returns>A task that completes when disposal finishes.</returns>
    public ValueTask DisposeAsync()
    {
        if (this.ownsClient && this.client is IDisposable disposable)
        {
            disposable.Dispose();
        }

        return default;
    }
}