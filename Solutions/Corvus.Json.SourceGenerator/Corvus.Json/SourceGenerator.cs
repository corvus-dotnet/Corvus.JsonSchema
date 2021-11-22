// <copyright file="SourceGenerator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;

    /// <summary>
    /// A source generator for Corvus JsonSchema-based type models.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For each JSON schema file for which you want to generate schema,
    /// include a JSON file in the csproj, and set the GenerateSchema option to "true".
    /// </para>
    /// <code>
    /// <![CDATA[
    /// <ItemGroup>
    ///   <AdditionalFiles Include="SomeSchema.json" GenerateSchema="true" />
    ///   <AdditionalFiles Include="SomeSchema.json" GenerateSchema="true" RootNamespace="Some.Example" />
    ///   <AdditionalFiles Include="AnotherSchema.csv" GenerateSchema="true" RootPath="#/foo/bar/baz" RebaseToRootPath="true" />
    /// </ItemGroup>
    /// ]]>
    /// </code>
    /// <para>
    /// You can include an optional "RootPath" property which will point the generator
    /// into the JSON document and use that as the root element to generate. You can optionally rebase
    /// to that element as if it were a root document itself (the rest of the document is then "invisible" to it,
    /// except as an external reference).
    /// </para>
    /// <para>
    /// It will generate all the types found in the dependency tree for the element to which you point
    /// it. The namespace will be that of the ambient location of the JsonSchema document, unless overridden
    /// with the RootNamespace option.
    /// </para>
    /// </remarks>
    [Generator]
    public class SourceGenerator : ISourceGenerator
    {
        /// <inheritdoc/>
        public void Execute(GeneratorExecutionContext context)
        {
            ImmutableDictionary<string, (string, string)> sources = ImmutableDictionary<string, (string, string)>.Empty;

            foreach (AdditionalText additionalFile in context.AdditionalFiles)
            {
                if (Path.GetExtension(additionalFile.Path).Equals(".json", StringComparison.OrdinalIgnoreCase))
                {
                    sources = Merge(sources, HandleJsonFile(additionalFile, GetGenerateSchema(context, additionalFile), GetRootPath(context, additionalFile), GetRootNamespace(context, additionalFile), GetRebaseToRootPath(context, additionalFile)));
                }
            }

            Generate(context, sources);
        }

        /// <inheritdoc/>
        public void Initialize(GeneratorInitializationContext context)
        {
        }

        private static ImmutableDictionary<string, (string, string)> HandleJsonFile(AdditionalText additionalFile, bool generateSchema, string rootPath, string rootNamespace, bool rebaseToRootPath)
        {
            if (!generateSchema)
            {
                return ImmutableDictionary<string, (string, string)>.Empty;
            }

            string arguments = string.Empty;
            if (!string.IsNullOrEmpty(rootNamespace))
            {
                arguments += $"--rootNamespace \"{rootNamespace}\" ";
            }

            if (!string.IsNullOrEmpty(rootPath))
            {
                arguments += $"--rootPath \"{rootPath}\" ";
            }

            if (rebaseToRootPath)
            {
                arguments += $"--rebaseToRootPath \"{rebaseToRootPath}\" ";
            }

            string tempPath = Path.GetTempPath();
            tempPath = Path.Combine(tempPath, Path.GetRandomFileName());

            arguments += $"--outputPath \"{tempPath}\" ";

            arguments += $"--outputMapFile mapfile.json \"{additionalFile.Path}\"";

            var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "Corvus",
                    Arguments = arguments,
                    CreateNoWindow = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                },
            };
            process.Start();
            process.WaitForExit();

            try
            {
                if (process.ExitCode == 0)
                {
                    ImmutableDictionary<string, (string, string)>.Builder result = ImmutableDictionary.CreateBuilder<string, (string, string)>();

                    foreach ((string k, string c, string p) in Parse(File.ReadAllText(Path.Combine(tempPath, "mapfile.json"))))
                    {
                        result.Add(k, (c, File.ReadAllText(p)));
                    }

                    return result.ToImmutable();
                }
                else
                {
                    throw new InvalidOperationException(process.StandardError.ReadToEnd());
                }
            }
            finally
            {
                Directory.Delete(tempPath, true);
            }
        }

        private static IEnumerable<(string, string, string)> Parse(string text)
        {
            // Find the array
            int currentIndex = text.IndexOf('[');
            if (currentIndex == -1)
            {
                yield break;
            }

            bool isFirst = true;

            while (true)
            {
                if (isFirst)
                {
                    isFirst = false;
                }
                else
                {
                    currentIndex = text.IndexOf(',', currentIndex + 1);
                    if (currentIndex == -1)
                    {
                        break;
                    }
                }

                int lookAhead = text.IndexOf('{', currentIndex + 1);
                if (lookAhead == -1)
                {
                    throw new InvalidOperationException("Expected '{'");
                }

                currentIndex = lookAhead;

                lookAhead = text.IndexOf("\"key\":", currentIndex + 1);
                if (lookAhead == -1)
                {
                    throw new InvalidOperationException("Expected 'key' property.");
                }

                lookAhead = text.IndexOf('"', lookAhead + 6);
                if (lookAhead == -1)
                {
                    throw new InvalidOperationException("Expected 'key' property value start.");
                }

                int endLookAhead = text.IndexOf('"', lookAhead + 1);
                if (endLookAhead == -1)
                {
                    throw new InvalidOperationException("Expected 'key' property value end.");
                }

                string keyProperty = text[(lookAhead + 1) ..endLookAhead];

                lookAhead = text.IndexOf("\"class\":", currentIndex + 1);
                if (lookAhead == -1)
                {
                    throw new InvalidOperationException("Expected 'class' property.");
                }

                lookAhead = text.IndexOf('"', lookAhead + 8);
                if (lookAhead == -1)
                {
                    throw new InvalidOperationException("Expected 'class' property value start.");
                }

                endLookAhead = text.IndexOf('"', lookAhead + 1);
                if (endLookAhead == -1)
                {
                    throw new InvalidOperationException("Expected 'class' property value end.");
                }

                string classProperty = text[(lookAhead + 1) ..endLookAhead];

                lookAhead = text.IndexOf("\"path\":", currentIndex + 1);
                if (lookAhead == -1)
                {
                    throw new InvalidOperationException("Expected 'path' property.");
                }

                lookAhead = text.IndexOf('"', lookAhead + 7);
                if (lookAhead == -1)
                {
                    throw new InvalidOperationException("Expected 'path' property value start.");
                }

                endLookAhead = text.IndexOf('"', lookAhead + 1);
                if (endLookAhead == -1)
                {
                    throw new InvalidOperationException("Expected 'path' property value end.");
                }

                string pathProperty = text[(lookAhead + 1) .. endLookAhead];

                currentIndex = text.IndexOf('}', currentIndex + 1);
                if (currentIndex == -1)
                {
                    throw new InvalidOperationException("Expected '}'");
                }

                yield return (keyProperty, classProperty, pathProperty);
            }

            currentIndex = text.IndexOf(']', currentIndex + 1);
            if (currentIndex == -1)
            {
                throw new InvalidOperationException("Expected ']'");
            }
        }

        private static bool GetGenerateSchema(GeneratorExecutionContext context, AdditionalText additionalFile)
        {
            if (context.AnalyzerConfigOptions.GetOptions(additionalFile)
                .TryGetValue("build_metadata.AdditionalFiles.GenerateSchema", out string? rootPathToGenerate))
            {
                if (bool.TryParse(rootPathToGenerate, out bool rpg))
                {
                    return rpg;
                }
            }

            return false;
        }

        private static string GetRootPath(GeneratorExecutionContext context, AdditionalText additionalFile)
        {
            if (context.AnalyzerConfigOptions.GetOptions(additionalFile)
                .TryGetValue("build_metadata.AdditionalFiles.RootPath", out string? rootPathToGenerate))
            {
                return string.IsNullOrEmpty(rootPathToGenerate) ? rootPathToGenerate : "#" + rootPathToGenerate;
            }
            else
            {
                return string.Empty;
            }
        }

        private static bool GetRebaseToRootPath(GeneratorExecutionContext context, AdditionalText additionalFile)
        {
            if (context.AnalyzerConfigOptions.GetOptions(additionalFile)
                .TryGetValue("build_metadata.AdditionalFiles.RebaseToRootPath", out string? rebase))
            {
                if (bool.TryParse(rebase, out bool r))
                {
                    return r;
                }
            }

            return false;
        }

        private static string GetRootNamespace(GeneratorExecutionContext context, AdditionalText additionalFile)
        {
            if (context.AnalyzerConfigOptions.GetOptions(additionalFile)
                .TryGetValue("build_metadata.AdditionalFiles.RootNamespace", out string? rootNamepsace))
            {
                return rootNamepsace;
            }

            return context.Compilation.GlobalNamespace.Name;
        }

        private static ImmutableDictionary<string, (string, string)> Merge(ImmutableDictionary<string, (string, string)> sources, ImmutableDictionary<string, (string, string)> added)
        {
            ImmutableDictionary<string, (string, string)>.Builder? builder = ImmutableDictionary.CreateBuilder<string, (string, string)>();
            builder.AddRange(sources);
            foreach (KeyValuePair<string, (string, string)> item in added)
            {
                if (!builder.ContainsKey(item.Key))
                {
                    builder.Add(item);
                }
            }

            return builder.ToImmutable();
        }

        private static void Generate(GeneratorExecutionContext context, ImmutableDictionary<string, (string, string)> sources)
        {
            foreach (KeyValuePair<string, (string typeName, string sourceCode)> source in sources)
            {
                context.AddSource(
                    source.Value.typeName,
                    SyntaxFactory.ParseCompilationUnit(source.Value.sourceCode)
                      .NormalizeWhitespace()
                      .GetText()
                      .ToString());
            }
        }
    }
}
