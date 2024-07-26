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
    /// <param name="modifiers">The parameter modifiers.</param>
    /// <param name="type">The parameter type.</param>
    /// <param name="parameterName">The specific (reserved) parameter name.</param>
    /// <param name="typeIsNullable">If true, the type is nullable.</param>
    /// <param name="defaultValue">The (optional) default value for the parameter.</param>
    public MethodParameter(string modifiers, string type, string parameterName, bool typeIsNullable = false, string? defaultValue = null)
    {
        this.Modifiers = modifiers;
        this.Type = type;
        this.specificParameterName = parameterName;
        this.TypeIsNullable = typeIsNullable;
        this.DefaultValue = defaultValue;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MethodParameter"/> struct.
    /// </summary>
    /// <param name="modifiers">The parameter modifiers.</param>
    /// <param name="type">The parameter type and modifiers.</param>
    /// <param name="parameterName">The specific (reserved) parameter name.</param>
    /// <param name="defaultValue">The (optional) default value for the parameter.</param>
    public MethodParameter(string modifiers, string type, MemberName parameterName, string? defaultValue = null)
    {
        this.Modifiers = modifiers;
        this.Type = type;
        this.parameterMemberName = parameterName;
        this.DefaultValue = defaultValue;
    }

    /// <summary>
    /// Gets the explicit modifiers.
    /// </summary>
    public string Modifiers { get; }

    /// <summary>
    /// Gets the type for the parameter.
    /// </summary>
    public string Type { get; }

    /// <summary>
    /// Gets a value indicating whether the type is nullable.
    /// </summary>
    public bool TypeIsNullable { get; }

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
    public static implicit operator MethodParameter((string TypeAndModifiers, string ParameterName) tuple) => new(string.Empty, tuple.TypeAndModifiers, tuple.ParameterName);

    /// <summary>
    /// Implicit conversion from type, modifiers, parameter name, nullability and default value.
    /// </summary>
    /// <param name="tuple">The tuple from which to convert.</param>
    public static implicit operator MethodParameter((string TypeAndModifiers, string ParameterName, bool TypeIsNullable, string DefaultValue) tuple) => new(string.Empty, tuple.TypeAndModifiers, tuple.ParameterName, tuple.TypeIsNullable, tuple.DefaultValue);

    /// <summary>
    /// Implicit conversion from type, modifiers, parameter name and default value.
    /// </summary>
    /// <param name="tuple">The tuple from which to convert.</param>
    public static implicit operator MethodParameter((string TypeAndModifiers, string ParameterName, string DefaultValue) tuple) => new(string.Empty, tuple.TypeAndModifiers, tuple.ParameterName, defaultValue: tuple.DefaultValue);

    /// <summary>
    /// Implicit conversion from type, modifiers and parameter name.
    /// </summary>
    /// <param name="tuple">The tuple from which to convert.</param>
    public static implicit operator MethodParameter((string TypeAndModifiers, MemberName ParameterName) tuple) => new(string.Empty, tuple.TypeAndModifiers, tuple.ParameterName);

    /// <summary>
    /// Implicit conversion from type, modifiers, parameter name and default value.
    /// </summary>
    /// <param name="tuple">The tuple from which to convert.</param>
    public static implicit operator MethodParameter((string TypeAndModifiers, MemberName ParameterName, string DefaultValue) tuple) => new(string.Empty, tuple.TypeAndModifiers, tuple.ParameterName, tuple.DefaultValue);

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
                generator.ReserveNameIfNotReserved(specificName);
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