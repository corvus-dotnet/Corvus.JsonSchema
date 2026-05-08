// <copyright file="YamlSchemaSourceGeneratorTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.Tests.GeneratedModels.Draft202012;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;

/// <summary>
/// Tests that the source generator correctly processes YAML schema files
/// and generates usable types.
/// </summary>
[TestClass]
public class YamlSchemaSourceGeneratorTests
{
    private const string ValidJson =
        """
        {"name":"Alice","age":30,"email":"alice@example.com"}
        """;

    private const string RequiredOnlyJson =
        """
        {"name":"Bob","age":25}
        """;

    /// <summary>
    /// Verify that a type generated from a YAML schema can be parsed from JSON.
    /// </summary>
    [TestMethod]
    public void Parse_ValidJson_Succeeds()
    {
        using var doc = ParsedJsonDocument<SimpleObject>.Parse(ValidJson);
        SimpleObject root = doc.RootElement;
        Assert.AreEqual("Alice", root.Name.ToString());
        Assert.AreEqual(30, (int)root.Age);
        Assert.AreEqual("alice@example.com", root.Email.ToString());
    }

    /// <summary>
    /// Verify that optional properties are handled correctly.
    /// </summary>
    [TestMethod]
    public void Parse_RequiredOnly_OptionalIsUndefined()
    {
        using var doc = ParsedJsonDocument<SimpleObject>.Parse(RequiredOnlyJson);
        SimpleObject root = doc.RootElement;
        Assert.AreEqual("Bob", root.Name.ToString());
        Assert.AreEqual(25, (int)root.Age);
        Assert.IsTrue(root.Email.IsUndefined());
    }

    /// <summary>
    /// Verify that a YAML-generated type can be validated against its schema.
    /// </summary>
    [TestMethod]
    public void Validate_ValidInstance_IsValid()
    {
        using var doc = ParsedJsonDocument<SimpleObject>.Parse(ValidJson);
        SimpleObject root = doc.RootElement;
        Assert.IsTrue(root.IsValid());
    }

    /// <summary>
    /// Verify that a YAML-generated type reports invalid for missing required properties.
    /// </summary>
    [TestMethod]
    public void Validate_MissingRequired_IsInvalid()
    {
        using var doc = ParsedJsonDocument<SimpleObject>.Parse("""{"name":"Charlie"}""");
        SimpleObject root = doc.RootElement;
        Assert.IsFalse(root.IsValid());
    }

    /// <summary>
    /// Verify that mutation works on a YAML-generated type.
    /// </summary>
    [TestMethod]
    public void Mutate_SetProperty_UpdatesValue()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<SimpleObject>.Parse(ValidJson);
        using JsonDocumentBuilder<SimpleObject.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        SimpleObject.Mutable root = builder.RootElement;
        root.SetName("Updated");
        Assert.AreEqual("Updated", root.Name.ToString());
    }

    #region AppConfig (complex YAML schema with $ref, arrays, additionalProperties)

    private const string AppConfigJson =
        """
        {"appName":"MyApp","version":"1.2.3","database":{"host":"localhost","port":5432,"name":"mydb","ssl":true},"features":["auth","logging"],"tags":{"env":"prod","region":"us-east"}}
        """;

    /// <summary>
    /// Verify that a complex type generated from a YAML schema with $ref can be parsed.
    /// </summary>
    [TestMethod]
    public void AppConfig_Parse_ValidJson_Succeeds()
    {
        using var doc = ParsedJsonDocument<AppConfig>.Parse(AppConfigJson);
        AppConfig root = doc.RootElement;
        Assert.AreEqual("MyApp", root.AppName.ToString());
        Assert.AreEqual("1.2.3", root.Version.ToString());
    }

    /// <summary>
    /// Verify that the $ref-generated nested DatabaseConfig type is accessible.
    /// </summary>
    [TestMethod]
    public void AppConfig_NestedRef_DatabaseConfig_Accessible()
    {
        using var doc = ParsedJsonDocument<AppConfig>.Parse(AppConfigJson);
        AppConfig root = doc.RootElement;
        AppConfig.DatabaseConfig db = root.Database;
        Assert.AreEqual("localhost", db.Host.ToString());
        Assert.AreEqual(5432, (int)db.Port);
        Assert.AreEqual("mydb", db.Name.ToString());
        Assert.IsTrue((bool)db.Ssl);
    }

    /// <summary>
    /// Verify that the array property (features) works correctly.
    /// </summary>
    [TestMethod]
    public void AppConfig_ArrayProperty_EnumeratesItems()
    {
        using var doc = ParsedJsonDocument<AppConfig>.Parse(AppConfigJson);
        AppConfig root = doc.RootElement;
        AppConfig.JsonStringArray features = root.Features;
        Assert.AreEqual(2, features.GetArrayLength());
    }

    /// <summary>
    /// Verify that validation works on the complex YAML-generated type.
    /// </summary>
    [TestMethod]
    public void AppConfig_Validate_ValidInstance_IsValid()
    {
        using var doc = ParsedJsonDocument<AppConfig>.Parse(AppConfigJson);
        AppConfig root = doc.RootElement;
        Assert.IsTrue(root.IsValid());
    }

    /// <summary>
    /// Verify that validation catches a port number out of range.
    /// </summary>
    [TestMethod]
    public void AppConfig_Validate_PortOutOfRange_IsInvalid()
    {
        using var doc = ParsedJsonDocument<AppConfig>.Parse(
            """{"appName":"X","database":{"host":"h","port":99999}}""");
        AppConfig root = doc.RootElement;
        Assert.IsFalse(root.IsValid());
    }

    /// <summary>
    /// Verify that mutation works on the nested $ref type.
    /// </summary>
    [TestMethod]
    public void AppConfig_Mutate_NestedDatabase_UpdatesHost()
    {
        using var workspace = JsonWorkspace.Create();
        using var doc = ParsedJsonDocument<AppConfig>.Parse(AppConfigJson);
        using JsonDocumentBuilder<AppConfig.Mutable> builder = doc.RootElement.CreateBuilder(workspace);

        AppConfig.Mutable root = builder.RootElement;
        AppConfig.DatabaseConfig.Mutable db = root.Database;
        db.SetHost("newhost.example.com");
        Assert.AreEqual("newhost.example.com", root.Database.Host.ToString());
    }

    #endregion
}
