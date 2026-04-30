// <copyright file="ObjectPropertyValidatorParameter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

namespace Corvus.Text.Json.CodeGeneration.ValidationHandlers.ObjectChildHandlers;

/// <summary>
/// A parameter for a validator delegate for an <see cref="IChildObjectPropertyValidationHandler2"/>
/// </summary>
public class ObjectPropertyValidatorParameter
{
    /// <summary>
    /// Creates a new instance of <see cref="ObjectPropertyValidatorParameter"/>.
    /// </summary>
    /// <param name="dotnetTypeName">The .NET type name of the parameter.</param>
    /// <param name="name">The name of the parameter.</param>
    public ObjectPropertyValidatorParameter(string dotnetTypeName, string name)
    {
        DotnetTypeName = dotnetTypeName;
        Name = name;
    }

    /// <summary>
    /// Gets the .NET type name for the parameter. This is the type that will be used in the generated code for the validator delegate.
    /// </summary>
    public string DotnetTypeName { get; }

    /// <summary>
    /// Gets the name of the parameter.
    /// </summary>
    public string Name { get; }
}