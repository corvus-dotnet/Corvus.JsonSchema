using System.Reflection;
using System.Text.RegularExpressions;
using Corvus.Text.Json.Playground.Models;

namespace Corvus.Text.Json.Playground.Services;

/// <summary>
/// Discovers and loads playground samples from embedded recipe resources.
/// Each recipe from the ExampleRecipes directory becomes a selectable sample
/// that bundles JSON schema files and a transformed Program.cs.
/// </summary>
public static partial class SampleRegistry
{
    private const string ResourcePrefix = "Corvus.Text.Json.Playground.Recipes.";

    // Maps recipe directory suffix (e.g. "DataObject") to a display-friendly name.
    // Also defines which schema files are root types (all of them in the recipes).
    // TypeNameOverrides maps filename -> explicit type name for schemas where
    // the desired type name differs from what the codegen would derive from the filename.
    private static readonly RecipeDefinition[] RecipeDefinitions =
    [
        new("001", "DataObject", "Data Object"),
        new("002", "DataObjectValidation", "Data Object Validation"),
        new("003", "ReusingCommonTypes", "Reusing Common Types"),
        new("004", "OpenVersusClosedTypes", "Open vs Closed Types"),
        new("005", "ExtendingABaseType", "Extending a Base Type"),
        new("006", "ConstrainingABaseType", "Constraining a Base Type"),
        new("007", "CreatingAStronglyTypedArray", "Strongly Typed Array"),
        new("008", "CreatingAnArrayOfHigherRank", "Array of Higher Rank"),
        new("009", "WorkingWithTensors", "Working with Tensors"),
        new("010", "CreatingTuples", "Creating Tuples"),
        new("011", "InterfacesAndMixInTypes", "Interfaces & Mix-In Types"),
        new("012", "PatternMatching", "Pattern Matching"),
        new("013", "PolymorphismWithDiscriminators", "Polymorphism with Discriminators"),
        new("014", "StringEnumerations", "String Enumerations"),
        new("015", "NumericEnumerations", "Numeric Enumerations"),
        new("016", "Maps", "Maps"),
        new("017", "MappingInputAndOutputValues", "Mapping Input & Output Values",
            new Dictionary<string, string>
            {
                ["source.json"] = "SourceType",
                ["target.json"] = "TargetType",
                ["crm.json"] = "CrmType",
            }),
    ];

    /// <summary>
    /// Load all playground samples from embedded resources.
    /// </summary>
    public static IReadOnlyList<PlaygroundSample> LoadAll()
    {
        Assembly assembly = typeof(SampleRegistry).Assembly;
        string[] allResources = assembly.GetManifestResourceNames();

        var samples = new List<PlaygroundSample>();

        foreach (RecipeDefinition recipe in RecipeDefinitions)
        {
            // Resource names look like:
            // Corvus.Text.Json.Playground.Recipes._001_DataObject.person.json
            // Corvus.Text.Json.Playground.Recipes._001_DataObject.Program.cs
            string prefix = $"{ResourcePrefix}_{recipe.Number}_{recipe.DirectorySuffix}.";

            // Find schema files (.json) and Program.cs
            var schemaResources = allResources
                .Where(r => r.StartsWith(prefix, StringComparison.Ordinal) && r.EndsWith(".json", StringComparison.Ordinal))
                .ToList();

            string? programResource = allResources
                .FirstOrDefault(r => r.StartsWith(prefix, StringComparison.Ordinal) && r.EndsWith(".Program.cs", StringComparison.Ordinal));

            if (schemaResources.Count == 0)
            {
                continue;
            }

            var schemas = new List<SampleSchemaFile>();
            foreach (string resourceName in schemaResources)
            {
                // Extract the filename: everything after the prefix
                // e.g. "person.json" from "...Recipes._001_DataObject.person.json"
                // Note: hyphens in filenames are preserved by MSBuild
                string fileName = resourceName[prefix.Length..];
                string content = LoadResource(assembly, resourceName);

                schemas.Add(new SampleSchemaFile
                {
                    Name = fileName,
                    Content = content,
                    IsRootType = true, // All schemas in recipes are root types
                    TypeName = recipe.TypeNameOverrides?.GetValueOrDefault(fileName),
                });
            }

            string userCode = programResource is not null
                ? TransformProgramCs(LoadResource(assembly, programResource))
                : "// No user code available for this sample.\n";

            samples.Add(new PlaygroundSample
            {
                Id = recipe.Number,
                DisplayName = recipe.DisplayName,
                Schemas = schemas,
                UserCode = userCode,
            });
        }

        return samples;
    }

    /// <summary>
    /// Transform a recipe's Program.cs for the playground:
    /// - Replace project-specific "using X.Models;" with "using Playground;"
    /// - Remove file-scoped namespace declarations
    /// </summary>
    private static string TransformProgramCs(string source)
    {
        // Replace "using <ProjectName>.Models;" with "using Playground;"
        string transformed = ModelsUsingRegex().Replace(source, "using Playground;");

        return transformed;
    }

    private static string LoadResource(Assembly assembly, string name)
    {
        using Stream? stream = assembly.GetManifestResourceStream(name);
        if (stream is null)
        {
            return $"// Resource '{name}' not found";
        }

        using StreamReader reader = new(stream);
        return reader.ReadToEnd();
    }

    [GeneratedRegex(@"using\s+\w+\.Models\s*;")]
    private static partial Regex ModelsUsingRegex();

    private record RecipeDefinition(
        string Number,
        string DirectorySuffix,
        string DisplayName,
        Dictionary<string, string>? TypeNameOverrides = null);
}
