// <copyright file="ContainerConfiguration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Drivers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SolidToken.SpecFlow.DependencyInjection;
using TechTalk.SpecFlow;

namespace Hooks;

/// <summary>
/// Container configuration class.
/// </summary>
public static class ContainerConfiguration
{
    /// <summary>
    /// Setup the service collection for dependency injection.
    /// </summary>
    /// <returns>The <see cref="IServiceCollection"/> configured with relevant services.</returns>
    /// <remarks>AddScoped for feature-level items, AddTransient for scenario level services.</remarks>
    [ScenarioDependencies]
    public static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();

        services.AddTransient<IDocumentResolver>(serviceProvider => new CompoundDocumentResolver(new FakeWebDocumentResolver(serviceProvider.GetRequiredService<IConfiguration>()["jsonSchemaBuilderDriverSettings:remotesBaseDirectory"]), new FileSystemDocumentResolver(), new HttpClientDocumentResolver(new HttpClient())));
        services.AddTransient<JsonWalker>();
        services.AddTransient<Corvus.Json.CodeGeneration.JsonSchemaBuilder>();
        services.AddTransient<JsonSchemaBuilderDriver202012>();
        ////services.AddTransient<JsonSchemaBuilderDriver201909>();

        services.AddTransient<IJsonSchemaBuilderDriver>(sp =>
        {
            ScenarioContext scenarioContext = sp.GetRequiredService<ScenarioContext>();
            if (scenarioContext.ScenarioInfo.ScenarioAndFeatureTags.Any(t => t == "draft2020-12"))
            {
                return sp.GetRequiredService<JsonSchemaBuilderDriver202012>();
            }

            ////if (scenarioContext.ScenarioInfo.ScenarioAndFeatureTags.Any(t => t == "draft2019-09"))
            ////{
            ////    return sp.GetRequiredService<JsonSchemaBuilderDriver201909>();
            ////}

            // Default to 202012
            return sp.GetRequiredService<JsonSchemaBuilderDriver202012>();
        });

        services.AddTransient<IConfiguration>(sp =>
        {
            IConfigurationBuilder configurationBuilder = new ConfigurationBuilder();
            configurationBuilder.AddJsonFile("appsettings.json", true);
            configurationBuilder.AddJsonFile("appsettings.local.json", true);
            return configurationBuilder.Build();
        });

        return services;
    }
}