// <copyright file="SchemaPointerBuilderTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.OpenApi.CodeGeneration.Tests;

[TestClass]
public class SchemaPointerBuilderTests
{
    // ── OpenAPI 2.0 (Swagger) pointer shapes: the Parameter/Header Object is the
    // ── schema position itself, with no /schema tail; responses have a direct
    // ── /schema member with no content media-type map.
    [TestMethod]
    public void BuildParameterObjectPointer_OperationLevel_BuildsPointerWithoutSchemaTail()
    {
        string result = SchemaPointerBuilder.BuildParameterObjectPointer(
            "paths"u8, "/pets"u8, OperationMethod.Get, 0, isPathLevel: false);

        Assert.AreEqual("#/paths/~1pets/get/parameters/0", result);
    }

    [TestMethod]
    public void BuildParameterObjectPointer_PathLevel_OmitsMethodSegment()
    {
        string result = SchemaPointerBuilder.BuildParameterObjectPointer(
            "paths"u8, "/pets/{petId}"u8, OperationMethod.Get, 2, isPathLevel: true);

        Assert.AreEqual("#/paths/~1pets~1{petId}/parameters/2", result);
    }

    [TestMethod]
    public void BuildResponseSchemaPointer_BuildsDirectSchemaPointer()
    {
        string result = SchemaPointerBuilder.BuildResponseSchemaPointer(
            "paths"u8, "/pets"u8, OperationMethod.Post, "201"u8);

        Assert.AreEqual("#/paths/~1pets/post/responses/201/schema", result);
    }

    [TestMethod]
    public void BuildResponseHeaderObjectPointer_BuildsPointerWithoutSchemaTail()
    {
        string result = SchemaPointerBuilder.BuildResponseHeaderObjectPointer(
            "paths"u8, "/pets"u8, OperationMethod.Get, "200"u8, "X-Rate-Limit"u8);

        Assert.AreEqual("#/paths/~1pets/get/responses/200/headers/X-Rate-Limit", result);
    }

    [TestMethod]
    public void BuildParameterObjectSubPath_OperationLevel_BuildsSubPath()
    {
        string result = SchemaPointerBuilder.BuildParameterObjectSubPath(
            OperationMethod.Delete, 1, isPathLevel: false);

        Assert.AreEqual("/delete/parameters/1", result);
    }

    [TestMethod]
    public void BuildParameterObjectSubPath_PathLevel_OmitsMethodSegment()
    {
        string result = SchemaPointerBuilder.BuildParameterObjectSubPath(
            OperationMethod.Get, 0, isPathLevel: true);

        Assert.AreEqual("/parameters/0", result);
    }

    [TestMethod]
    public void BuildResponseSchemaSubPath_BuildsDirectSchemaSubPath()
    {
        string result = SchemaPointerBuilder.BuildResponseSchemaSubPath(
            OperationMethod.Get, "default"u8);

        Assert.AreEqual("/get/responses/default/schema", result);
    }

    [TestMethod]
    public void BuildResponseHeaderObjectSubPath_BuildsSubPathWithoutSchemaTail()
    {
        string result = SchemaPointerBuilder.BuildResponseHeaderObjectSubPath(
            OperationMethod.Put, "200"u8, "ETag"u8);

        Assert.AreEqual("/put/responses/200/headers/ETag", result);
    }

    [TestMethod]
    public void BuildParameterObjectPointer_SegmentWithTildeAndSlash_AppliesRfc6901Escaping()
    {
        string result = SchemaPointerBuilder.BuildParameterObjectPointer(
            "paths"u8, "/a~b/c"u8, OperationMethod.Get, 0, isPathLevel: false);

        Assert.AreEqual("#/paths/~1a~0b~1c/get/parameters/0", result);
    }
}