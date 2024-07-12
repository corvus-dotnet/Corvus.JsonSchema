// <copyright file="ContainerConfiguration.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if !NET8_0_OR_GREATER
using System.Net.Http;
#endif
using Corvus.Json;
using Corvus.Json.CodeGeneration;
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

        services.AddTransient<IDocumentResolver>(serviceProvider => new CompoundDocumentResolver(new FakeWebDocumentResolver(serviceProvider.GetRequiredService<IConfiguration>()["jsonSchemaBuilderDriverSettings:remotesBaseDirectory"]!), new FileSystemDocumentResolver(), new HttpClientDocumentResolver(new HttpClient())));
        services.AddTransient<JsonSchemaTypeBuilder>();
        services.AddTransient(sp =>
        {
            VocabularyRegistry registry = new();
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(sp.GetRequiredService<IDocumentResolver>(), registry);
            Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(sp.GetRequiredService<IDocumentResolver>(), registry);
            Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(registry);
            Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(registry);
            Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(registry);
            return registry;
        });

        services.AddTransient(sp =>
        {
            ScenarioContext scenarioContext = sp.GetRequiredService<ScenarioContext>();
            if (scenarioContext.ScenarioInfo.ScenarioAndFeatureTags.Any(t => t == "draft2020-12"))
            {
                return new JsonSchemaBuilderDriver(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<JsonSchemaTypeBuilder>(), Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary, "jsonSchemaBuilder202012DriverSettings");
            }

            if (scenarioContext.ScenarioInfo.ScenarioAndFeatureTags.Any(t => t == "draft2019-09"))
            {
                return new JsonSchemaBuilderDriver(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<JsonSchemaTypeBuilder>(), Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.DefaultVocabulary, "jsonSchemaBuilder201909DriverSettings");
            }

            if (scenarioContext.ScenarioInfo.ScenarioAndFeatureTags.Any(t => t == "draft7"))
            {
                return new JsonSchemaBuilderDriver(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<JsonSchemaTypeBuilder>(), Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary, "jsonSchemaBuilder7DriverSettings");
            }

            if (scenarioContext.ScenarioInfo.ScenarioAndFeatureTags.Any(t => t == "draft6"))
            {
                return new JsonSchemaBuilderDriver(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<JsonSchemaTypeBuilder>(), Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.DefaultVocabulary, "jsonSchemaBuilder6DriverSettings");
            }

            if (scenarioContext.ScenarioInfo.ScenarioAndFeatureTags.Any(t => t == "draft4"))
            {
                return new JsonSchemaBuilderDriver(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<JsonSchemaTypeBuilder>(), Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary, "jsonSchemaBuilder4DriverSettings");
            }

            if (scenarioContext.ScenarioInfo.ScenarioAndFeatureTags.Any(t => t == "openApi30"))
            {
                ////return new JsonSchemaBuilderDriver(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<JsonSchemaTypeBuilder>(), Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary, "jsonSchemaBuilderOpenApi30DriverSettings");
            }

            // Default to 202012
            return new JsonSchemaBuilderDriver(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<JsonSchemaTypeBuilder>(), Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary, "jsonSchemaBuilder202012DriverSettings");
        });

        services.AddTransient<IConfiguration>(static _ =>
        {
            ConfigurationBuilder configurationBuilder = new();
            configurationBuilder.AddJsonFile("appsettings.json", true);
            configurationBuilder.AddJsonFile("appsettings.local.json", true);
            return configurationBuilder.Build();
        });

        return services;
    }
}