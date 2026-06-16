// <copyright file="Extensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Adds the common Aspire service defaults — service discovery, HTTP resilience, health checks, and
/// OpenTelemetry — shared by every host in the Arazzo demo composition (the control-plane host today,
/// the runner host next). Referenced by each host project and opted into from its <c>Program.cs</c> via
/// <see cref="AddServiceDefaults{TBuilder}(TBuilder)"/> and <see cref="MapDefaultEndpoints(WebApplication)"/>.
/// </summary>
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    // The Arazzo execution host's ActivitySource + Meter name (Corvus.Text.Json.Arazzo.ArazzoTelemetry). Registered
    // here, in the shared defaults, so every host in the composition surfaces workflow traces/metrics to the
    // Aspire dashboard without per-host wiring. Kept as a literal to keep ServiceDefaults dependency-light.
    private const string ArazzoTelemetryName = "Corvus.Arazzo";

    /// <summary>Adds service discovery, HTTP resilience, health checks, and OpenTelemetry to the host.</summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder, for chaining.</returns>
    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default.
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default.
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>Configures OpenTelemetry logging, metrics, and tracing, exporting via OTLP to the Aspire dashboard.</summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder, for chaining.</returns>
    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter(ArazzoTelemetryName);
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddSource(ArazzoTelemetryName)
                    .AddAspNetCoreInstrumentation(tracing =>

                        // Exclude health-check requests from tracing.
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath))
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    /// <summary>Adds a default liveness health check.</summary>
    /// <typeparam name="TBuilder">The host application builder type.</typeparam>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder, for chaining.</returns>
    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()

            // Add a default liveness check to ensure the app is responsive.
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    /// <summary>Maps the <c>/health</c> (readiness) and <c>/alive</c> (liveness) endpoints in development.</summary>
    /// <param name="app">The web application.</param>
    /// <returns>The application, for chaining.</returns>
    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health-check endpoints in non-development environments has security implications.
        // See https://aka.ms/aspire/healthchecks for details before enabling these outside development.
        if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for the app to be considered ready to accept traffic.
            app.MapHealthChecks(HealthEndpointPath);

            // Only health checks tagged "live" must pass for the app to be considered alive.
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live"),
            });
        }

        return app;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder)
        where TBuilder : IHostApplicationBuilder
    {
        bool useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        return builder;
    }
}
