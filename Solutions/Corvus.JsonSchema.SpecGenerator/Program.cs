// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.JsonSchema.SpecGenerator
{
    using System;

    /// <summary>
    /// This program generates feature files for the Json Schema specs.
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// Main entry point.
        /// </summary>
        /// <param name="args">Program arguments ([inputDir] [outputDir] [testselector]).</param>
        /// <returns>Result code.</returns>
        public static int Main(string[] args)
        {
            try
            {
                var result = SpecDirectories.SetupDirectories(args);
                SpecWriter.Write(result);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(ex.Message);
                Console.ResetColor();
                return -1;
            }

            return 0;
        }
    }
}
