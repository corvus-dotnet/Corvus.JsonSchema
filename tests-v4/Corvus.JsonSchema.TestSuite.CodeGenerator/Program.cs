// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.JsonSchema.TestSuite.CodeGenerator;

Console.WriteLine("=== Generating V4 JSON Schema Test Suite xUnit tests ===");
Console.WriteLine();

TestCaseGenerator.GenerateTests(testFile => Console.WriteLine($"  {testFile}"));

Console.WriteLine();
Console.WriteLine("Done.");