// <copyright file="YamlTestHelper.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
using System.Text.Json;
using Corvus.Yaml;

namespace Corvus.Yaml.SystemTextJson.Tests;
#else
using Corvus.Text.Json;
using Corvus.Text.Json.Yaml;

namespace Corvus.Text.Json.Yaml.Tests;
#endif

/// <summary>
/// Helper that abstracts the Parse call so test files can be shared between
/// the CTJ (<see cref="ParsedJsonDocument{T}"/>) and STJ (<see cref="JsonDocument"/>) projects.
/// </summary>
internal static class YamlTestHelper
{
#if STJ
    internal static JsonDocument Parse(byte[] yaml, YamlReaderOptions options = default)
        => YamlDocument.Parse(yaml, options);
#else
    internal static ParsedJsonDocument<JsonElement> Parse(byte[] yaml, YamlReaderOptions options = default)
        => YamlDocument.Parse<JsonElement>(yaml, options);
#endif
}