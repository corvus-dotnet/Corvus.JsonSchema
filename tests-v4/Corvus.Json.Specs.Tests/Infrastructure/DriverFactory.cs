// <copyright file="DriverFactory.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Drivers;
using Microsoft.Extensions.Configuration;

namespace Corvus.Json.Specs.Tests.Infrastructure;

/// <summary>
/// Factory for creating <see cref="JsonSchemaBuilderDriver"/> instances.
/// </summary>
public static class DriverFactory
{
    private static readonly Lazy<IConfiguration> LazyConfiguration = new(BuildConfiguration);
    private static readonly Lazy<string> LazyRepoRoot = new(FindRepoRoot);

    /// <summary>
    /// Ensures the Corvus.Json.ExtendedTypes assembly is loaded into the default context.
    /// The driver uses AssemblyLoadContext.Default.Assemblies to find built-in types.
    /// </summary>
#if NET
    [System.Runtime.CompilerServices.ModuleInitializer]
    internal static void EnsureExtendedTypesLoaded()
    {
        // Reference a type to force the assembly to load
        _ = typeof(Corvus.Json.JsonAny);

        // Set CWD to the assembly output directory so that content files
        // (metaschema/*.json) copied to the output are resolvable via relative paths.
        Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
    }
#else
    private static bool _initialized;

    internal static void EnsureExtendedTypesLoaded()
    {
        if (_initialized)
        {
            return;
        }

        _initialized = true;

        // Reference a type to force the assembly to load
        _ = typeof(Corvus.Json.JsonAny);

        // Set CWD to the assembly output directory so that content files
        // (metaschema/*.json) copied to the output are resolvable via relative paths.
        Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
    }
#endif

    /// <summary>
    /// Gets the shared configuration.
    /// </summary>
    public static IConfiguration Configuration => LazyConfiguration.Value;

    /// <summary>
    /// Gets the repository root directory.
    /// </summary>
    public static string RepoRoot => LazyRepoRoot.Value;

    /// <summary>
    /// Creates a driver for draft 2020-12 schemas.
    /// </summary>
    /// <returns>A configured <see cref="JsonSchemaBuilderDriver"/>.</returns>
    public static JsonSchemaBuilderDriver CreateDraft202012Driver()
    {
        return CreateDriver(
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            "jsonSchemaBuilder202012DriverSettings");
    }

    /// <summary>
    /// Creates a driver for draft 2019-09 schemas.
    /// </summary>
    /// <returns>A configured <see cref="JsonSchemaBuilderDriver"/>.</returns>
    public static JsonSchemaBuilderDriver CreateDraft201909Driver()
    {
        return CreateDriver(
            Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.DefaultVocabulary,
            "jsonSchemaBuilder201909DriverSettings");
    }

    /// <summary>
    /// Creates a driver for draft 7 schemas.
    /// </summary>
    /// <returns>A configured <see cref="JsonSchemaBuilderDriver"/>.</returns>
    public static JsonSchemaBuilderDriver CreateDraft7Driver()
    {
        return CreateDriver(
            Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.DefaultVocabulary,
            "jsonSchemaBuilder7DriverSettings");
    }

    /// <summary>
    /// Creates a driver for draft 6 schemas.
    /// </summary>
    /// <returns>A configured <see cref="JsonSchemaBuilderDriver"/>.</returns>
    public static JsonSchemaBuilderDriver CreateDraft6Driver()
    {
        return CreateDriver(
            Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.DefaultVocabulary,
            "jsonSchemaBuilder6DriverSettings");
    }

    /// <summary>
    /// Creates a driver for draft 4 schemas.
    /// </summary>
    /// <returns>A configured <see cref="JsonSchemaBuilderDriver"/>.</returns>
    public static JsonSchemaBuilderDriver CreateDraft4Driver()
    {
        return CreateDriver(
            Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.DefaultVocabulary,
            "jsonSchemaBuilder4DriverSettings");
    }

    /// <summary>
    /// Creates a driver for OpenAPI 3.0 schemas.
    /// </summary>
    /// <returns>A configured <see cref="JsonSchemaBuilderDriver"/>.</returns>
    public static JsonSchemaBuilderDriver CreateOpenApi30Driver()
    {
        return CreateDriver(
            Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.DefaultVocabulary,
            "jsonSchemaBuilderOpenApi30DriverSettings");
    }

    private static JsonSchemaBuilderDriver CreateDriver(IVocabulary vocabulary, string settingsKey)
    {
#if !NET
        EnsureExtendedTypesLoaded();
#endif
        IConfiguration config = Configuration;
        string remotesPath = config[$"{settingsKey}:remotesBaseDirectory"]
            ?? config["jsonSchemaBuilderDriverSettings:remotesBaseDirectory"]!;

        IDocumentResolver documentResolver = new CompoundDocumentResolver(
            new FakeWebDocumentResolver(remotesPath),
            new FileSystemDocumentResolver()).AddMetaschema();

        VocabularyRegistry registry = new();
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, registry);
        Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, registry);
        Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(registry);
        Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(registry);
        Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(registry);
        Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(registry);

        var builder = new JsonSchemaTypeBuilder(documentResolver, registry);

        return new JsonSchemaBuilderDriver(config, builder, vocabulary, settingsKey);
    }

    private static IConfiguration BuildConfiguration()
    {
        string basePath = AppDomain.CurrentDomain.BaseDirectory;
        string repoRoot = RepoRoot;

        // Build configuration with paths resolved against the repository root.
        // The appsettings.json contains paths relative to repo root (e.g. "JSON-Schema-Test-Suite/remotes").
        // We resolve them to absolute paths here so the driver doesn't depend on CWD.
        IConfigurationRoot config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        // Resolve all *Directory values against the repo root
        foreach (IConfigurationSection section in config.GetChildren())
        {
            foreach (IConfigurationSection child in section.GetChildren())
            {
                if (child.Key.Contains("Directory", StringComparison.OrdinalIgnoreCase) && child.Value is not null)
                {
                    child.Value = Path.GetFullPath(Path.Combine(repoRoot, child.Value));
                }
            }
        }

        return config;
    }

    private static string FindRepoRoot()
    {
        string dir = AppDomain.CurrentDomain.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "Corvus.Text.Json.slnx")))
        {
            string? parent = Path.GetDirectoryName(dir);
            if (parent is null || parent == dir)
            {
                throw new InvalidOperationException(
                    "Could not find repository root (Corvus.Text.Json.slnx) from " +
                    AppDomain.CurrentDomain.BaseDirectory);
            }

            dir = parent;
        }

        return dir;
    }
}