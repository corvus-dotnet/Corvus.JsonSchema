// <copyright file="OperationFilterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class OperationFilterTests
{
    [TestMethod]
    public void Matches_NoPatterns_IncludesAll()
    {
        OperationFilter filter = new([], []);
        Assert.IsTrue(filter.Matches("/pets"));
        Assert.IsTrue(filter.Matches("/items/123"));
    }

    [TestMethod]
    public void Matches_IncludePattern_OnlyMatching()
    {
        OperationFilter filter = new(["/pets**"], []);
        Assert.IsTrue(filter.Matches("/pets"));
        Assert.IsTrue(filter.Matches("/pets/123"));
        Assert.IsFalse(filter.Matches("/items"));
    }

    [TestMethod]
    public void Matches_ExcludePattern_ExcludesMatching()
    {
        OperationFilter filter = new([], ["/internal**"]);
        Assert.IsTrue(filter.Matches("/pets"));
        Assert.IsFalse(filter.Matches("/internal/health"));
    }

    [TestMethod]
    public void Matches_IncludeAndExclude_ExcludeTakesPrecedence()
    {
        OperationFilter filter = new(["/pets**"], ["/pets/secret**"]);
        Assert.IsTrue(filter.Matches("/pets"));
        Assert.IsTrue(filter.Matches("/pets/123"));
        Assert.IsFalse(filter.Matches("/pets/secret"));
        Assert.IsFalse(filter.Matches("/pets/secret/data"));
    }
}