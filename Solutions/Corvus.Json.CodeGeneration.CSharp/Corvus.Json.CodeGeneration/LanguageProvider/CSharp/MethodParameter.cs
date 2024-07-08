// <copyright file="MethodParameter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A parameter for a method.
/// </summary>
public readonly struct MethodParameter
{
    private readonly string? specificParameterName;
    private readonly MemberName? parameterMemberName;

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodParameter"/> struct.
    /// </summary>
    /// <param name="typeAndModifiers">The parameter type and modifiers.</param>
    /// <param name="parameterName">The specific (reserved) parameter name.</param>
    /// <param name="defaultValue">The (optional) default value for the parameter.</param>
    public MethodParameter(string typeAndModifiers, string parameterName, string? defaultValue = null)
    {
        this.TypeAndModifiers = typeAndModifiers;
        this.specificParameterName = parameterName;
        this.DefaultValue = defaultValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodParameter"/> struct.
    /// </summary>
    /// <param name="typeAndModifiers">The parameter type and modifiers.</param>
    /// <param name="parameterName">The specific (reserved) parameter name.</param>
    /// <param name="defaultValue">The (optional) default value for the parameter.</param>
    public MethodParameter(string typeAndModifiers, MemberName parameterName, string? defaultValue = null)
    {
        this.TypeAndModifiers = typeAndModifiers;
        this.parameterMemberName = parameterName;
        this.DefaultValue = defaultValue;
    }

    /// <summary>
    /// Gets the type and modifiers for the parameter.
    /// </summary>
    public string TypeAndModifiers { get; }

    /// <summary>
    /// Gets the (optional) default value for the parameter.
    /// </summary>
    public string? DefaultValue { get; }

    /// <summary>
    /// Gets a value indicating whether this requires a specifically reserved name in the scope.
    /// </summary>
    public bool RequiresReservedName => this.specificParameterName is not null;

    /// <summary>
    /// Implicit conversion from type, modifiers and parameter name.
    /// </summary>
    /// <param name="tuple">The tuple from which to convert.</param>
    public static implicit operator MethodParameter((string TypeAndModifiers, string ParameterName) tuple) => new(tuple.TypeAndModifiers, tuple.ParameterName);

    /// <summary>
    /// Implicit conversion from type, modifiers, parameter name and default value.
    /// </summary>
    /// <param name="tuple">The tuple from which to convert.</param>
    public static implicit operator MethodParameter((string TypeAndModifiers, string ParameterName, string DefaultValue) tuple) => new(tuple.TypeAndModifiers, tuple.ParameterName, tuple.DefaultValue);

    /// <summary>
    /// Implicit conversion from type, modifiers and parameter name.
    /// </summary>
    /// <param name="tuple">The tuple from which to convert.</param>
    public static implicit operator MethodParameter((string TypeAndModifiers, MemberName ParameterName) tuple) => new(tuple.TypeAndModifiers, tuple.ParameterName);

    /// <summary>
    /// Implicit conversion from type, modifiers, parameter name and default value.
    /// </summary>
    /// <param name="tuple">The tuple from which to convert.</param>
    public static implicit operator MethodParameter((string TypeAndModifiers, MemberName ParameterName, string DefaultValue) tuple) => new(tuple.TypeAndModifiers, tuple.ParameterName, tuple.DefaultValue);

    /// <summary>
    /// Get the name for the parameter, reserving it in the scope
    /// if necessary.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="isDeclaration">
    /// If true, this is a declaration, and the name will be
    /// reserved if required.
    /// </param>
    /// <returns>The name for the parameter.</returns>
    public string GetName(CodeGenerator generator, bool isDeclaration = false)
    {
        if (this.specificParameterName is string specificName)
        {
            if (isDeclaration)
            {
                generator.ReserveName(specificName);
            }

            return specificName;
        }

        if (this.parameterMemberName is MemberName memberName)
        {
            return generator.GetOrAddMemberName(memberName);
        }

        throw new InvalidOperationException("No parameter name was set.");
    }
}