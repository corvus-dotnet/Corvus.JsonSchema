// Simple entry point for the V4 migration example.
// Build this project to see analyzer warnings on all V4 patterns.

using V4MigrationExample;

Console.WriteLine("=== V4 Migration Example ===");
Console.WriteLine("Build this project to see migration analyzer warnings.");
Console.WriteLine();

V4Patterns.ParsingExample();
V4Patterns.ValidationExample();
V4Patterns.TypeCoercionExample();
V4Patterns.AsAccessorExample();
V4Patterns.CountExample();
V4Patterns.FromJsonExample();
V4Patterns.JsonDocumentParseExample();
V4Patterns.PropertyMutationExample();
V4Patterns.ArrayOperationsExample();
V4Patterns.ArrayFactoryExample();
V4Patterns.NestedFactoryExample();
V4Patterns.WriteToExample();
V4Patterns.TryGetStringExample();
V4Patterns.BackingModelExample();
V4Patterns.PatternMatchingExample();
V4Patterns.ComplexNestedMutationExample();
