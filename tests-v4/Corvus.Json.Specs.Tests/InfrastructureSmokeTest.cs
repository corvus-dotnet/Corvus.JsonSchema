// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Corvus.Json.Specs.Tests;

/// <summary>
/// Smoke test to verify the test infrastructure compiles and runs.
/// </summary>
public class InfrastructureSmokeTest : IDisposable
{
    private readonly IConfiguration configuration;
    private readonly JsonSchemaBuilderDriver driver;
    private readonly IDocumentResolver documentResolver;

    public InfrastructureSmokeTest()
    {
        this.configuration = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        string remotesPath = this.configuration["jsonSchemaBuilderDriverSettings:remotesBaseDirectory"]!;

        this.documentResolver = new CompoundDocumentResolver(
            new FakeWebDocumentResolver(remotesPath),
            new FileSystemDocumentResolver()).AddMetaschema();

        VocabularyRegistry registry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(this.documentResolver, registry);
        Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(this.documentResolver, registry);
        Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(registry);
        Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(registry);
        Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(registry);
        Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(registry);

        var builder = new JsonSchemaTypeBuilder(this.documentResolver, registry);

        this.driver = new JsonSchemaBuilderDriver(
            this.configuration,
            builder,
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            "jsonSchemaBuilder202012DriverSettings");
    }

    public void Dispose()
    {
        this.driver.Dispose();
        this.documentResolver.Dispose();
        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task CanGenerateTypeFromInlineSchema()
    {
        Type generatedType = await this.driver.GenerateTypeForVirtualFile(
            """{"type": "string"}""",
            "smoke-test.json",
            "SmokeTest",
            "Scenario1",
            validateFormat: false,
            optionalAsNullable: false,
            useImplicitOperatorString: false);

        Assert.NotNull(generatedType);

        using var doc = JsonDocument.Parse("\"hello\"");
        IJsonValue instance = JsonSchemaBuilderDriver.CreateInstance(generatedType, doc.RootElement.Clone());
        Assert.Equal(JsonValueKind.String, instance.ValueKind);
    }
}