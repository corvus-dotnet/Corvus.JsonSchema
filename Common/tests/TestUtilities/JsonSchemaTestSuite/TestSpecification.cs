// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
using Corvus.Json;

namespace TestUtilities.JsonSchemaTestSuite;

/// <summary>
/// A group of JSON Schema test files
/// </summary>
/// <param name="name">The name of the test group.</param>
/// <param name="defaultVocabulary">The default vocabulary for the test suite.</param>
/// <param name="files">The test files in the test group.</param>
public class TestGroup(string name, string defaultVocabulary, List<TestFile> files)
{
    public string Name { get; } = name;

    public List<TestFile> Files { get; } = files;

    public string DefaultVocabulary { get; } = defaultVocabulary;
}

/// <summary>
/// A JSON Schema Test file containing one or more test suites.
/// </summary>
/// <param name="baseDirectory">The base directory from which the tests file is loaded.</param>
/// <param name="relativePath">The relative path to the file from the base directory.</param>
/// <param name="namespaceRelativePath">The relative path within the group for namespace creation.</param>
/// <param name="testSuites"></param>
public class TestFile(string baseDirectory, string relativePath, string namespaceRelativePath, List<TestSuite> testSuites)
{
    public string BaseDirectory { get; } = baseDirectory;

    public string RelativePath { get; } = relativePath;

    public string NamespaceRelativePath { get; } = namespaceRelativePath;

    public List<TestSuite> TestSuites { get; } = testSuites;
}

/// <summary>
/// A JSON Schema Test suite within a test file.
/// </summary>
/// <param name="suiteName">The name of the suite.</param>
/// <param name="schema">The schema text for the test suite.</param>
/// <param name="testSpecifications">The test specifications.</param>
/// <param name="validateFormat">Whether to always assert format.</param>
public class TestSuite(
    string suiteName,
    JsonAny schema,
    List<TestSpecification> testSpecifications,
    bool validateFormat)
{
    public string SuiteName { get; } = suiteName;

    public List<TestSpecification> TestSpecifications { get; } = testSpecifications;

    public bool ValidateFormat { get; } = validateFormat;

    public JsonAny Schema { get; } = schema;
}

/// <summary>
/// A JSON Schema Test specification within a test suite.
/// </summary>
/// <param name="testDescription">The description of the test case.</param>
/// <param name="instance">The instance to be evaluated.</param>
/// <param name="expectation">The expected result of evaluation.</param>
public class TestSpecification(
    string testDescription,
    JsonAny instance,
    bool expectation)
{
    public string TestDescription { get; } = testDescription;

    public JsonAny Instance { get; } = instance;

    public bool Expectation { get; } = expectation;
}