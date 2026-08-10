// <copyright file="ScheduleRegistryConformance.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Arazzo.Durability.Schedules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Conformance;

/// <summary>
/// The shared contract every <see cref="IScheduleRegistry"/> must satisfy: the deployment-global
/// <c>scheduleId</c> to <see cref="ScheduleRegistration"/> map whose atomic insert enforces the schedules
/// contract's global uniqueness. Registration is idempotent for an identical registration (the crash-retry
/// and redelivery convergence) and refuses anything else — another environment's schedule under the same id,
/// or another run — with <see cref="ScheduleRegistrationConflictException"/>. A backend's test project
/// derives a concrete <see cref="TestClassAttribute"/> and implements <see cref="CreateRegistryAsync"/>;
/// the in-memory registry is the reference implementation and runs the same suite.
/// </summary>
public abstract class ScheduleRegistryConformance
{
    private readonly List<IAsyncDisposable> disposables = [];

    /// <summary>Creates a fresh, empty registry backed by the implementation under test.</summary>
    /// <returns>The registry.</returns>
    protected abstract ValueTask<IScheduleRegistry> CreateRegistryAsync();

    /// <summary>Disposes any registries created during the test.</summary>
    /// <returns>A task that completes when cleanup is done.</returns>
    [TestCleanup]
    public async Task CleanupAsync()
    {
        foreach (IAsyncDisposable disposable in this.disposables)
        {
            await disposable.DisposeAsync();
        }

        this.disposables.Clear();
    }

    [TestMethod]
    public async Task A_registration_round_trips_through_resolve()
    {
        IScheduleRegistry registry = await this.NewRegistryAsync();
        var registration = new ScheduleRegistration("production", new WorkflowRunId("0123456789abcdef0123456789abcdef"));

        await registry.RegisterAsync("nightly", registration, default);

        (await registry.ResolveAsync("nightly", default)).ShouldBe(registration);
    }

    [TestMethod]
    public async Task An_unregistered_id_resolves_to_null()
    {
        IScheduleRegistry registry = await this.NewRegistryAsync();

        (await registry.ResolveAsync("absent", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Re_registering_the_identical_registration_is_idempotent()
    {
        // The crash-retry convergence: a create that crashed between registration and run creation re-registers
        // the same schedule and must find its own row, not a conflict.
        IScheduleRegistry registry = await this.NewRegistryAsync();
        var registration = new ScheduleRegistration("production", new WorkflowRunId("0123456789abcdef0123456789abcdef"));

        await registry.RegisterAsync("nightly", registration, default);
        await registry.RegisterAsync("nightly", registration, default);

        (await registry.ResolveAsync("nightly", default)).ShouldBe(registration);
    }

    [TestMethod]
    public async Task Registering_an_id_held_by_another_environment_is_refused()
    {
        // The whole point of the registry (ADR 0065 §9): under the composite (environment, runId) run key the
        // same run id can exist in two environments, so only this insert conflict can enforce the schedules
        // contract's deployment-global uniqueness.
        IScheduleRegistry registry = await this.NewRegistryAsync();
        var runId = new WorkflowRunId("0123456789abcdef0123456789abcdef");

        await registry.RegisterAsync("nightly", new ScheduleRegistration("development", runId), default);

        await Should.ThrowAsync<ScheduleRegistrationConflictException>(
            async () => await registry.RegisterAsync("nightly", new ScheduleRegistration("production", runId), default));
    }

    [TestMethod]
    public async Task Registering_an_id_held_by_another_run_is_refused()
    {
        IScheduleRegistry registry = await this.NewRegistryAsync();

        await registry.RegisterAsync("nightly", new ScheduleRegistration("production", new WorkflowRunId("0123456789abcdef0123456789abcdef")), default);

        await Should.ThrowAsync<ScheduleRegistrationConflictException>(
            async () => await registry.RegisterAsync("nightly", new ScheduleRegistration("production", new WorkflowRunId("fedcba9876543210fedcba9876543210")), default));
    }

    [TestMethod]
    public async Task A_refused_registration_leaves_the_existing_one_untouched()
    {
        IScheduleRegistry registry = await this.NewRegistryAsync();
        var original = new ScheduleRegistration("development", new WorkflowRunId("0123456789abcdef0123456789abcdef"));

        await registry.RegisterAsync("nightly", original, default);
        try
        {
            await registry.RegisterAsync("nightly", new ScheduleRegistration("production", original.RunId), default);
        }
        catch (ScheduleRegistrationConflictException)
        {
        }

        (await registry.ResolveAsync("nightly", default)).ShouldBe(original);
    }

    [TestMethod]
    public async Task Unregistering_frees_the_id_for_a_different_registration()
    {
        // The rollback path: a registration whose run creation was refused is removed, so the id does not stay
        // shadowed by a row that points at nothing.
        IScheduleRegistry registry = await this.NewRegistryAsync();
        var runId = new WorkflowRunId("0123456789abcdef0123456789abcdef");

        await registry.RegisterAsync("nightly", new ScheduleRegistration("development", runId), default);
        await registry.UnregisterAsync("nightly", default);

        (await registry.ResolveAsync("nightly", default)).ShouldBeNull();
        await registry.RegisterAsync("nightly", new ScheduleRegistration("production", runId), default);
        (await registry.ResolveAsync("nightly", default)).ShouldBe(new ScheduleRegistration("production", runId));
    }

    [TestMethod]
    public async Task Unregistering_an_unknown_id_is_a_no_op()
    {
        IScheduleRegistry registry = await this.NewRegistryAsync();

        await registry.UnregisterAsync("absent", default);

        (await registry.ResolveAsync("absent", default)).ShouldBeNull();
    }

    [TestMethod]
    public async Task Distinct_ids_register_independently()
    {
        IScheduleRegistry registry = await this.NewRegistryAsync();
        var first = new ScheduleRegistration("development", new WorkflowRunId("0123456789abcdef0123456789abcdef"));
        var second = new ScheduleRegistration("production", new WorkflowRunId("fedcba9876543210fedcba9876543210"));

        await registry.RegisterAsync("nightly", first, default);
        await registry.RegisterAsync("weekly", second, default);

        (await registry.ResolveAsync("nightly", default)).ShouldBe(first);
        (await registry.ResolveAsync("weekly", default)).ShouldBe(second);
    }

    private async ValueTask<IScheduleRegistry> NewRegistryAsync()
    {
        IScheduleRegistry registry = await this.CreateRegistryAsync();
        if (registry is IAsyncDisposable disposable)
        {
            this.disposables.Add(disposable);
        }

        return registry;
    }
}