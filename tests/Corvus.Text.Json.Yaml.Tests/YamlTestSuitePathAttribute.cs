// <copyright file="YamlTestSuitePathAttribute.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Yaml.Tests;

/// <summary>
/// Assembly attribute that carries the yaml-test-suite path from MSBuild.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
public sealed class YamlTestSuitePathAttribute : Attribute
{
    public YamlTestSuitePathAttribute(string path) => Path = path;

    public string Path { get; }
}