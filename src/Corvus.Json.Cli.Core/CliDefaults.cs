// <copyright file="CliDefaults.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.CodeGenerator;

/// <summary>
/// Ambient defaults that vary between the primary <c>corvusjson</c> tool and the
/// legacy <c>generatejsonschematypes</c> shim.
/// </summary>
public static class CliDefaults
{
    /// <summary>
    /// Gets or sets the default code generation engine used when <c>--engine</c> is not specified.
    /// The <c>corvusjson</c> tool sets this to <see cref="Engine.V5"/>;
    /// the legacy <c>generatejsonschematypes</c> shim sets it to <see cref="Engine.V4"/>.
    /// </summary>
    public static Engine DefaultEngine { get; set; } = Engine.V5;
}