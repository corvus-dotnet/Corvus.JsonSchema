// <copyright file="DuplicateKeyBehavior.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
namespace Corvus.Yaml;
#else
namespace Corvus.Text.Json.Yaml;
#endif

/// <summary>
/// Specifies the behavior when duplicate mapping keys are encountered.
/// </summary>
public enum DuplicateKeyBehavior
{
    /// <summary>
    /// Throw a <see cref="YamlException"/> when a duplicate key is encountered (default).
    /// YAML 1.2 says mapping keys SHOULD be unique.
    /// </summary>
    Error,

    /// <summary>
    /// The last value for a duplicate key wins. Earlier values are silently overwritten.
    /// This matches the behavior of many YAML 1.1 implementations.
    /// </summary>
    LastWins,
}