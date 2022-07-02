// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.Patch.SpecGenerator
{
    using System;
    using System.IO;

    /// <summary>
    /// Spec Generator for UriTemplates.
    /// </summary>
    public class Program
    {
        /// <summary>
        /// Program entry point.
        /// </summary>
        /// <param name="args">Input and output paths.</param>
        /// <returns>0 if successul, -1 if incorrect parameters passed.</returns>
        public static int Main(string[] args)
        {
            if (args.Length != 2)
            {
                return -1;
            }

            string path = args[0];
            string outputPath = args[1];
            foreach (string testFile in Directory.EnumerateFiles(path, "*.json"))
            {
                ScenarioArray feature = JsonAny.Parse(File.ReadAllText(testFile));
                if (!feature.IsValid())
                {
                    // Skip anything that isn't a test feature file.
                    continue;
                }

                var spec = new Spec(feature, Path.GetFileNameWithoutExtension(testFile));
                string outputFilename = Path.Combine(outputPath, $"{spec.FeatureName}.feature");
                File.WriteAllText(outputFilename, spec.TransformText());
                Console.WriteLine(outputFilename);

                var builderSpec = new BuilderSpec(feature, $"builder_{Path.GetFileNameWithoutExtension(testFile)}");
                outputFilename = Path.Combine(outputPath, $"{builderSpec.FeatureName}.feature");
                File.WriteAllText(outputFilename, builderSpec.TransformText());
            }

            return 0;
        }
    }
}
