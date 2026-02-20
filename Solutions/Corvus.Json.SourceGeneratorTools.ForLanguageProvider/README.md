# Corvus.Json.SourceGeneratorTools.ForLanguageProvider

This provides a set of self-contained libraries for use in Source Generators that wish to generate code from JSON Schema.

Unlike Corvus.Json.SourceGeneratorTools, this library is intended for people who implement their own .NET ILanguageProvider, and do not want to use the
original Corvus.Json.CodeGeneration.CSharp.CSharpLanguageProvider. For example, it is used in the V5 Corvus.Text.Json source generator.

It is only ever intended to be used as a _private asset_ in such generators, and is not intended to be referenced by end-user libraries.

It minimizes external dependencies by compiling in the source from the following libraries:

`Corvus.Json.CodeGeneration`
`Corvus.UriTemplates`
`Corvus.Json.JsonReference`
`Corvus.Json.ExtendedTypes`
`Corvus.Json.JsonSchema`

It also references `8.x` and prior versions of the System libraries which simplifies referencing in your custom Source Generator or Analyzer.

## Using the implementation helpers

We provide a helper class to simplify some of the most common code-generation requirements.

For example, `SourceGeneratorHelpers.GenerateCode()` takes a `SourceProductionContext` and an instance of a type called `TypesToGenerate` which
describes the set of root types to generate (whose dependencies will be inferred and automatically generated if not explicitly specified).

It also takes an instance of a `VocabularyRegistry`. This is a `Corvus.Json.CodeGeneration` type; a default instance can be retrieved by calling `SourceGeneratorHelpers.CreateVocabularyRegistry()` and passing it an `IDocumentResolver` that has been preloaded with the standard vocabulary metaschema.

A suitable `IDocumentResolver` for this purpose can be retrieved by calling `SourceGeneratorHelpers.CreateMetaSchemaResolver()`.

For code generation, you will also need a `PrepoulatedDocumentResolver` containing the relevant additional analyzer files. A method
called `SourceGeneratorHelpers.BuildDocumentResolver()` will create such a document resolver for you, if provided with an `ImmutableArray<AdditionalText>`.

Exactly when you construct these entities and types depends on your approach to source generation, and its caching strategy.

The `GlobalOptions` are the means by which you configure and instantiate your language provider

Your generator code may look something like:

```csharp    
    private static readonly IDocumentResolver MetaSchemaResolver = CreateMetaSchemaResolver();
    private static readonly VocabularyRegistry VocabularyRegistry = CreateVocabularyRegistry(MetaSchemaResolver);

    public void Initialize(IncrementalGeneratorInitializationContext initializationContext)
    {
        // Get global options
        IncrementalValueProvider<MyGlobalOptions> globalOptions = initializationContext.AnalyzerConfigOptionsProvider.Select(GetGlobalOptions);

        IncrementalValuesProvider<AdditionalText> jsonSourceFiles = initializationContext.AdditionalTextsProvider.Where(p => p.Path.EndsWith(".json"));

        IncrementalValueProvider<IDocumentResolver> documentResolver = jsonSourceFiles.Collect().Select(SourceGeneratorHelpers.BuildDocumentResolver);

        IncrementalValueProvider<SourceGeneratorHelpers.GenerationContext<MyGlobalOptions>> generationContext = documentResolver.Combine(globalOptions).Select((r, c) => new GenerationContext(r.Left, r.Right));

        // Typically built from e.g. attributes or other syntax on partial classes.
        IncrementalValuesProvider<SourceGeneratorHelpers.GenerationSpecification<MyGlobalOptions>> generationSpecifications = BuildGenerationSpecifications();
            
        IncrementalValueProvider<SourceGeneratorHelpers.TypesToGenerate<MyGlobalOptions>> typesToGenerate = generationSpecifications.Collect().Combine(generationContext).Select((c, t) => new SourceGeneratorHelpers.TypesToGenerate(c.Left, c.Right));

        initializationContext.RegisterSourceOutput(typesToGenerate, GenerateCode);
    }

    private static void GenerateCode(SourceProductionContext context, SourceGeneratorHelpers.TypesToGenerate<MyGlobalOptions> generationSource)
    {
        SourceGeneratorHelpers.GenerateCode(context, generationSource, VocabularyRegistry);
    }

    private static MyGlobalOptions GetGlobalOptions(AnalyzerConfigOptionsProvider source, CancellationToken token)
    {
        // .. retrieve your global options e.g.
        return MyGlobalOptions.Create(VocabularyRegistry, source, token);
    }

    internal class MyGlobalOptions : IGlobalOptions
    {
        List<MyLanguageProvider.NamedType> namedTypes = [];

        public static MyGlobalOptions Create(VocabularyRegistry vocabularyRegistry, AnalyzerConfigOptionsProvider source, CancellationToken token)
        {
            IVocabulary fallbackVocabulary;
            if (source.GlobalOptions.TryGetValue("build_property.CorvusJsonSchemaFallbackVocabularySchemaUri", out string? fallbackVocabularySchemaUri))
            {
                fallbackVocabulary = vocabularyRegistry.TryGetSchemaDialect(fallbackVocabularySchemaUri);
            }
            else
            {
                fallbackVocabulary = Corvus.Json.CodeGeneration.Draft202012.SchemaVocabulary.DefaultInstance;
            }

            // Add other configuration here

            return new() { FallbackVocabulary = fallbackVocabulary };
        }

        public IVocabulary FallbackVocabulary { get; private set; }

        public void AddNamedType(string schemaLocation, string typeName, string ns, GeneratedTypeAccessibility accessibility)
        {
            namedTypes.Add(new MyLanguageProvider.NamedType(schemaLocation, typeName, ns, accessibility));
        }

        public ILanguageProvider CreateLanguageProvider()
        {
            return new MyLanguageProvider(CreateMyLanguageProviderOptions());
        }

        private MyLanguageProviderOptions CreateMyLanguageProviderOptions()
        {
            // Map from the values we captured in CreateOptions to the options
            // for your language provider.
        }
    }
```