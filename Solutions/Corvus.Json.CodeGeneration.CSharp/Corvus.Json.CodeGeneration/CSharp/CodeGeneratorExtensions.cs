﻿// <copyright file="CodeGeneratorExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extension methods for the <see cref="CodeGenerator"/>.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Appends the relevant operator.
    /// </summary>
    /// <param name="generator">The generator to which to append the operator.</param>
    /// <param name="op">The operator to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendOperator(this CodeGenerator generator, Operator op)
    {
        switch (op)
        {
            case Operator.Equals:
                return generator.Append("==");
            case Operator.NotEquals:
                return generator.Append("!=");
            case Operator.LessThan:
                return generator.Append("<");
            case Operator.LessThanOrEquals:
                return generator.Append("<=");
            case Operator.GreaterThan:
                return generator.Append(">");
            case Operator.GreaterThanOrEquals:
                return generator.Append(">=");
            default:
                Debug.Fail($"Unexpected operator {op}");
                return generator;
        }
    }

    /// <summary>
    /// Appends the text for the relevant operator.
    /// </summary>
    /// <param name="generator">The generator to which to append the operator.</param>
    /// <param name="op">The operator to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTextForOperator(this CodeGenerator generator, Operator op)
    {
        switch (op)
        {
            case Operator.Equals:
                return generator.Append("equals");
            case Operator.NotEquals:
                return generator.Append("does not equal");
            case Operator.LessThan:
                return generator.Append("is less than");
            case Operator.LessThanOrEquals:
                return generator.Append("is less than or equal to");
            case Operator.GreaterThan:
                return generator.Append("is greater than");
            case Operator.GreaterThanOrEquals:
                return generator.Append("is greater than or equal to");
            case Operator.MultipleOf:
                return generator.Append("is a multiple of");
            default:
                Debug.Fail($"Unexpected operator {op}");
                return generator;
        }
    }

    /// <summary>
    /// Appends the text for the inverse of the relevant operator.
    /// </summary>
    /// <param name="generator">The generator to which to append the text.</param>
    /// <param name="op">The operator for which to append the text.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is commonly used for failure cases.
    /// </remarks>
    public static CodeGenerator AppendTextForInverseOperator(this CodeGenerator generator, Operator op)
    {
        switch (op)
        {
            case Operator.Equals:
                return generator.Append("does not equal");
            case Operator.NotEquals:
                return generator.Append("equals");
            case Operator.LessThan:
                return generator.Append("is greater than or equal to");
            case Operator.LessThanOrEquals:
                return generator.Append("is greater than");
            case Operator.GreaterThan:
                return generator.Append("is less than or equal to");
            case Operator.GreaterThanOrEquals:
                return generator.Append("is less than");
            case Operator.MultipleOf:
                return generator.Append("is not a multiple of");
            default:
                Debug.Fail($"Unexpected operator {op}");
                return generator;
        }
    }

    /// <summary>
    /// Append the validation result for a keyword.
    /// </summary>
    /// <param name="generator">The generator to which to append the ignored keyword validation code.</param>
    /// <param name="isValid">Whether the result should be valid.</param>
    /// <param name="keyword">The keyword that has been ignored.</param>
    /// <param name="validationContextIdentifier">The identifier for the validation context to update.</param>
    /// <param name="reasonText">The reason for ignoring the keyword.</param>
    /// <param name="useInterpolatedString">If <see langword="true"/>, then the message string will be an interpolated string.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendKeywordValidationResult(
        this CodeGenerator generator,
        bool isValid,
        IKeyword keyword,
        string validationContextIdentifier,
        string reasonText,
        bool useInterpolatedString = false)
    {
        generator
            .AppendIndent(validationContextIdentifier)
            .Append(" = ")
            .Append(validationContextIdentifier)
            .Append(".WithResult(isValid: ")
            .Append(isValid ? "true" : "false")
            .Append(", ");

        if (useInterpolatedString)
        {
            generator
                .Append('$');
        }

        return generator
            .Append("\"Validation ")
            .Append(keyword.Keyword)
            .Append(" - ")
            .Append(SymbolDisplay.FormatLiteral(reasonText, false))
            .AppendLine("\");");
    }

    /// <summary>
    /// Append the validation result for a keyword.
    /// </summary>
    /// <param name="generator">The generator to which to append the keyword validation code.</param>
    /// <param name="isValid">Whether the result should be valid.</param>
    /// <param name="keyword">The keyword that has been ignored.</param>
    /// <param name="validationContextIdentifier">The identifier for the validation context to update.</param>
    /// <param name="appendReasonText">An function which will append the validation reason to the (optionally interpolated) string for the keyword.</param>
    /// <param name="useInterpolatedString">If <see langword="true"/>, then the message string will be an interpolated string.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendKeywordValidationResult(
        this CodeGenerator generator,
        bool isValid,
        IKeyword keyword,
        string validationContextIdentifier,
        Action<CodeGenerator> appendReasonText,
        bool useInterpolatedString = false)
    {
        generator
            .AppendIndent(validationContextIdentifier)
            .Append(" = ")
            .Append(validationContextIdentifier)
            .Append(".WithResult(isValid: ")
            .Append(isValid ? "true" : "false")
            .Append(", ");

        if (useInterpolatedString)
        {
            generator
                .Append('$');
        }

        generator
            .Append("\"Validation ")
            .Append(keyword.Keyword)
            .Append(" - ");

        appendReasonText(generator);

        return generator
            .AppendLine("\");");
    }

    /// <summary>
    /// Append using statements for the given namespaces.
    /// </summary>
    /// <param name="generator">The generator to which to append usings.</param>
    /// <param name="namespaces">The namespace to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendUsings(this CodeGenerator generator, params ConditionalCodeSpecification[] namespaces)
    {
        ConditionalCodeSpecification.AppendConditionalsInOrder(
            generator,
            namespaces,
            static (g, a, _) => Append(g, a));

        return generator;

        static void Append(CodeGenerator generator, Action<CodeGenerator> action)
        {
            generator.Append("using ");
            action(generator);
            generator.AppendLine(";");
        }
    }

    /// <summary>
    /// Appends a blank line if the previous line ended with a closing brace.
    /// </summary>
    /// <param name="generator">The generator to which to append the separator line.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSeparatorLine(this CodeGenerator generator)
    {
        if ((generator.ScopeType == ScopeType.Type && generator.EndsWith($";{Environment.NewLine}")) ||
            generator.EndsWith($"}}{Environment.NewLine}") ||
            generator.EndsWith($"#endif{Environment.NewLine}"))
        {
            // Append a blank line
            generator.AppendLine();
        }

        return generator;
    }

    /// <summary>
    /// Append a namespace statement.
    /// </summary>
    /// <param name="generator">The generator to which to append usings.</param>
    /// <param name="ns">The namespace to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginNamespace(this CodeGenerator generator, string ns)
    {
        return generator
            .Append("namespace ")
            .Append(ns)
            .AppendLine(";")
            .PushMemberScope(ns, ScopeType.TypeContainer);
    }

    /// <summary>
    /// Append a namespace statement.
    /// </summary>
    /// <param name="generator">The generator to which to append usings.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator EndNamespace(this CodeGenerator generator)
    {
        return generator
            .PopMemberScope();
    }

    /// <summary>
    /// Append the start of a public readonly property declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the property.</param>
    /// <param name="propertyType">The type of the property.</param>
    /// <param name="propertyName">The name of the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginPublicReadOnlyPropertyDeclaration(this CodeGenerator generator, string propertyType, string propertyName)
    {
        return generator
            .AppendLineIndent("public ", propertyType, " ", propertyName)
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("get")
            .AppendLineIndent("{")
            .PushIndent();
    }

    /// <summary>
    /// Append the start of a public readonly property declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator EndReadOnlyPropertyDeclaration(this CodeGenerator generator)
    {
        return generator
            .PopIndent()
            .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Begin a local method declaration for an explicit name which will be reserved in the scope.
    /// </summary>
    /// <param name="generator">The generator to which to append the local method.</param>
    /// <param name="visibilityAndModifiers">The visibility and modifiers for the method.</param>
    /// <param name="returnType">The return type of the method.</param>
    /// <param name="methodName">The method name, which will have been reserved in the scope.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginLocalMethodDeclaration(
        this CodeGenerator generator,
        string visibilityAndModifiers,
        string returnType,
        string methodName,
        params MethodParameter[] parameters)
    {
        return generator
            .AppendSeparatorLine()
            .AppendIndent(visibilityAndModifiers)
            .Append(' ')
            .Append(returnType)
            .Append(' ')
            .Append(methodName)
            .PushMemberScope(methodName, ScopeType.Method) // Then move to the method scope before appending parameters
            .AppendParameterList(parameters)
            .AppendLineIndent("{")
            .PushIndent();
    }

    /// <summary>
    /// Begin a method declaration for an explicit name which will be reserved in the scope.
    /// </summary>
    /// <param name="generator">The generator to which to append the method.</param>
    /// <param name="visibilityAndModifiers">The visibility and modifiers for the method.</param>
    /// <param name="returnType">The return type of the method.</param>
    /// <param name="methodName">The method name, which will be reserved in the scope.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginReservedMethodDeclaration(
        this CodeGenerator generator,
        string visibilityAndModifiers,
        string returnType,
        string methodName,
        params MethodParameter[] parameters)
    {
        return generator
            .AppendSeparatorLine()
            .AppendIndent(visibilityAndModifiers)
            .Append(' ')
            .Append(returnType)
            .Append(' ')
            .Append(methodName)
            .ReserveName(methodName) // Reserve the method name in the parent scope
            .PushMemberScope(methodName, ScopeType.Method) // Then move to the method scope before appending parameters
            .AppendParameterList(parameters)
            .AppendLineIndent("{")
            .PushIndent();
    }

    /// <summary>
    /// Begin a method declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the method.</param>
    /// <param name="visibilityAndModifiers">The visibility and modifiers for the method.</param>
    /// <param name="returnType">The return type of the method.</param>
    /// <param name="methodName">The method name, which will be reserved in the scope.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginMethodDeclaration(
        this CodeGenerator generator,
        string visibilityAndModifiers,
        string returnType,
        MemberName methodName,
        params MethodParameter[] parameters)
    {
        string realisedMethodName = generator.GetOrAddMemberName(methodName);

        return generator
            .AppendIndent(visibilityAndModifiers)
            .Append(' ')
            .Append(returnType)
            .Append(' ')
            .Append(realisedMethodName)
            .PushMemberScope(realisedMethodName, ScopeType.Method)
            .AppendParameterList(parameters)
            .AppendLineIndent("{")
            .PushIndent();
    }

    /// <summary>
    /// Append the backing fields for the implied core types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="impliedCoreTypes">The implied core types.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBackingFields(this CodeGenerator generator, CoreTypes impliedCoreTypes)
    {
        return generator
            .AppendBackingField("Backing", "backing")
            .AppendBackingField("JsonElement", "jsonElementBacking")
            .AppendBackingField("string", "stringBacking", impliedCoreTypes, CoreTypes.String)
            .AppendBackingField("bool", "boolBacking", impliedCoreTypes, CoreTypes.Boolean)
            .AppendBackingField("BinaryJsonNumber", "numberBacking", impliedCoreTypes, CoreTypes.Number | CoreTypes.Integer)
            .AppendBackingField("ImmutableList<JsonAny>", "arrayBacking", impliedCoreTypes, CoreTypes.Array)
            .AppendBackingField("ImmutableList<JsonObjectProperty>", "objectBacking", impliedCoreTypes, CoreTypes.Object);
    }

    /// <summary>
    /// Append the schema location static property for the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSchemaLocationStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("SchemaLocation")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Gets the schema location from which this type was generated.
            /// </summary>
            """)
            .AppendIndent("public static string SchemaLocation { get; } = ")
            .Append(SymbolDisplay.FormatLiteral(typeDeclaration.RelativeSchemaLocation, true))
            .AppendLine(";");
    }

    /// <summary>
    /// Append the static property which provides a null instance of the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendNullInstanceStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("Null")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Gets a Null instance.
            /// </summary>
            """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" Null { get; } = new(JsonValueHelpers.NullElement);");
    }

    /// <summary>
    /// Append the static property which provides a null instance of the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendUndefinedInstanceStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("Undefined")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Gets an Undefined instance.
            /// </summary>
            """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" Undefined { get; }");
    }

    /// <summary>
    /// Append the static property which provides a default instance of the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendDefaultInstanceStaticProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .ReserveName("DefaultInstance")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            """
            /// <summary>
            /// Gets the default instance.
            /// </summary>
            """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" DefaultInstance { get; }");

        return typeDeclaration.DefaultValue().ValueKind switch
        {
            JsonValueKind.Undefined => generator.AppendLine(),
            JsonValueKind.Null => generator
                                    .Append(" = ")
                                    .Append(typeDeclaration.DotnetTypeName())
                                    .AppendLine(".ParseValue(\"null\"u8);"),
            _ => generator
                    .Append(" = ")
                    .Append(typeDeclaration.DotnetTypeName())
                    .Append(".ParseValue(")
                    .Append(SymbolDisplay.FormatLiteral(typeDeclaration.DefaultValue().GetRawText(), true))
                    .AppendLine("u8);"),
        };
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonAny instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsAnyProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsAny")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public JsonAny AsAny")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalConstructFromBacking(
                        "Backing.JsonElement",
                        "jsonElementBacking",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendConditionalConstructFromBacking(
                        "Backing.String",
                        "stringBacking",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.String)
                    .AppendConditionalConstructFromBacking(
                        "Backing.Bool",
                        "boolBacking",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Boolean)
                    .AppendConditionalConstructFromBacking(
                        "Backing.Number",
                        "numberBacking",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Number | CoreTypes.Integer)
                    .AppendConditionalConstructFromBacking(
                        "Backing.Array",
                        "arrayBacking",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Array)
                    .AppendConditionalConstructFromBacking(
                        "Backing.Object",
                        "objectBacking",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Object)
                    .AppendReturnNullInstanceIfNull()
                    .AppendSeparatorLine()
                    .AppendLineIndent("return JsonAny.Undefined;")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonElement instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsJsonElementProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsJsonElement")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public JsonElement AsJsonElement")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return ",
                        "jsonElementBacking",
                        ";",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.String",
                        "return JsonValueHelpers.StringToJsonElement(",
                        "stringBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.String)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Bool",
                        "return JsonValueHelpers.BoolToJsonElement(",
                        "boolBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Boolean)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Number",
                        "return JsonValueHelpers.NumberToJsonElement(",
                        "numberBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Number | CoreTypes.Integer)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Array",
                        "return JsonValueHelpers.ArrayToJsonElement(",
                        "arrayBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Array)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Object",
                        "return JsonValueHelpers.ObjectToJsonElement(",
                        "objectBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Object)
                    .AppendSeparatorLine()
                    .AppendReturnNullJsonElementIfNull()
                    .AppendSeparatorLine()
                    .AppendLineIndent("return default;")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonString instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsStringProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsString")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent(
                (typeDeclaration.ImpliedCoreTypes() & CoreTypes.String) != 0
                    ? "public JsonString AsString"
                    : "JsonString IJsonValue.AsString")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return new(",
                        "jsonElementBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.String",
                        "return new(",
                        "stringBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.String)
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonBoolean instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsBooleanProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsBoolean")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent(
                (typeDeclaration.ImpliedCoreTypes() & CoreTypes.Boolean) != 0
                    ? "public JsonBoolean AsBoolean"
                    : "JsonBoolean IJsonValue.AsBoolean")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return new(",
                        "jsonElementBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Bool",
                        "return new(",
                        "boolBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Boolean)
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonNumber instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsNumberProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsNumber")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent(
                (typeDeclaration.ImpliedCoreTypes() & (CoreTypes.Number | CoreTypes.Integer)) != 0
                    ? "public JsonNumber AsNumber"
                    : "JsonNumber IJsonValue.AsNumber")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return new(",
                        "jsonElementBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Number",
                        "return new(",
                        "numberBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Number | CoreTypes.Integer)
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonObject instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsObjectProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsObject")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent(
                (typeDeclaration.ImpliedCoreTypes() & CoreTypes.Object) != 0
                    ? "public JsonObject AsObject"
                    : "JsonObject IJsonValue.AsObject")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return new(",
                        "jsonElementBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Object",
                        "return new(",
                        "objectBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Object)
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the property which converts this instance to a JsonArray instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsArrayProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("AsArray")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent(
                (typeDeclaration.ImpliedCoreTypes() & CoreTypes.Array) != 0
                    ? "public JsonArray AsArray"
                    : "JsonArray IJsonValue.AsArray")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return new(",
                        "jsonElementBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Any)
                    .AppendSeparatorLine()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Array",
                        "return new(",
                        "arrayBacking",
                        ");",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Array)
                    .AppendSeparatorLine()
                    .AppendLineIndent("throw new InvalidOperationException();")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append a property which gets a value indicating if the instance has a JsonElement backing.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendHasJsonElementBackingProperty(this CodeGenerator generator)
    {
        return generator
            .ReserveName("HasJsonElementBacking")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public bool HasJsonElementBacking")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return ")
                    .AppendTestBacking("Backing.JsonElement")
                    .AppendLine(";")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append a property which gets a value indicating if the instance has a .NET core type backing.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendHasDotnetBackingProperty(this CodeGenerator generator)
    {
        return generator
            .ReserveName("HasDotnetBacking")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public bool HasDotnetBacking")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return ")
                    .AppendTestBacking("Backing.Dotnet")
                    .AppendLine(";")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append a property which gets the <see cref="JsonValueKind"/> for the instance.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the property.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendValueKindProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("ValueKind")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public JsonValueKind ValueKind")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("get")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.JsonElement",
                        "return ",
                        "jsonElementBacking",
                        ".ValueKind;")
                    .AppendConditionalBackingValueLineIndent(
                        "Backing.String",
                        "return JsonValueKind.String;",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.String)
                    .AppendConditionalWrappedBackingValueLineIndent(
                        "Backing.Bool",
                        "return ",
                        "boolBacking",
                        " ? JsonValueKind.True : JsonValueKind.False;",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Boolean)
                    .AppendConditionalBackingValueLineIndent(
                        "Backing.Number",
                        "return JsonValueKind.Number;",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Number | CoreTypes.Integer)
                    .AppendConditionalBackingValueLineIndent(
                        "Backing.Array",
                        "return JsonValueKind.Array;",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Array)
                    .AppendConditionalBackingValueLineIndent(
                        "Backing.Object",
                        "return JsonValueKind.Object;",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Object)
                    .AppendConditionalBackingValueLineIndent(
                        "Backing.Null",
                        "return JsonValueKind.Null;",
                        impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                        forCoreTypes: CoreTypes.Null)
                    .AppendSeparatorLine()
                    .AppendLineIndent("return JsonValueKind.Undefined;")
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the default constructor for the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the constructor.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPublicDefaultConstructor(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.SingleConstantValue().ValueKind != JsonValueKind.Undefined)
        {
            // Don't emit this for a type that has a single constant value.
            return generator;
        }

        CoreTypes impliedCoreTypes = typeDeclaration.ImpliedCoreTypes();

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Initializes a new instance of the ")
            .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
            .AppendLine(" struct.")
            .AppendLineIndent("/// </summary>")
            .AppendIndent("public ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine("()")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBackingFieldAssignment("jsonElementBacking", "default")
                .AppendBackingFieldAssignment("backing", "Backing.JsonElement")
                .AppendBackingFieldAssignment("stringBacking", "string.Empty", impliedCoreTypes, CoreTypes.String)
                .AppendBackingFieldAssignment("boolBacking", "default", impliedCoreTypes, CoreTypes.Boolean)
                .AppendBackingFieldAssignment("numberBacking", "default", impliedCoreTypes, CoreTypes.Number | CoreTypes.Integer)
                .AppendBackingFieldAssignment("arrayBacking", "ImmutableList<JsonAny>.Empty", impliedCoreTypes, CoreTypes.Array)
                .AppendBackingFieldAssignment("objectBacking", "ImmutableList<JsonObjectProperty>.Empty", impliedCoreTypes, CoreTypes.Object)
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the default constructor for the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the constructor.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPublicJsonElementConstructor(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        CoreTypes impliedCoreTypes = typeDeclaration.ImpliedCoreTypes();

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Initializes a new instance of the ")
            .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
            .AppendLine(" struct.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to construct the instance.</param>")
            .AppendIndent("public ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine("(in JsonElement value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBackingFieldAssignment("jsonElementBacking", "value")
                .AppendBackingFieldAssignment("backing", "Backing.JsonElement")
                .AppendBackingFieldAssignment("stringBacking", "string.Empty", impliedCoreTypes, CoreTypes.String)
                .AppendBackingFieldAssignment("boolBacking", "default", impliedCoreTypes, CoreTypes.Boolean)
                .AppendBackingFieldAssignment("numberBacking", "default", impliedCoreTypes, CoreTypes.Number | CoreTypes.Integer)
                .AppendBackingFieldAssignment("arrayBacking", "ImmutableList<JsonAny>.Empty", impliedCoreTypes, CoreTypes.Array)
                .AppendBackingFieldAssignment("objectBacking", "ImmutableList<JsonObjectProperty>.Empty", impliedCoreTypes, CoreTypes.Object)
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends an implicit conversion from <paramref name="sourceType"/> to the
    /// dotnet type of the <paramref name="typeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="sourceType">The name of the source type from which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionFromTypeUsingConstructor(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Conversion from ")
            .AppendTypeAsSeeCref(sourceType)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append('(')
            .Append(sourceType)
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(value);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends an implicit conversion from <paramref name="sourceType"/> to the
    /// dotnet type of the <paramref name="typeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="sourceType">The name of the source type from which to convert.</param>
    /// <param name="sourceValueKind">The expected <see cref="JsonValueKind"/> for the conversion.</param>
    /// <param name="dotnetTypeConversion">The code that converts the "value" to a dotnet value suitable
    /// for a constructor.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionFromJsonValueTypeUsingConstructor(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType,
        JsonValueKind sourceValueKind,
        string dotnetTypeConversion)
    {
        return AppendImplicitConversionFromJsonValueTypeUsingConstructor(
            generator,
            typeDeclaration,
            sourceType,
            [sourceValueKind],
            dotnetTypeConversion);
    }

    /// <summary>
    /// Appends an implicit conversion to bool for a boolean-backed type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionToBoolean(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Conversion to <see langword="bool"/>.
                /// </summary>
                /// <param name="value">The value from which to convert.</param>
                /// <exception cref="InvalidOperationException">The value was not a boolean.</exception>
                """)
            .AppendIndent("public static implicit operator bool(")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendBlockIndent(
                """
                {
                    return value.GetBoolean() ?? throw new InvalidOperationException();
                }
                """);
    }

    /// <summary>
    /// Appends an implicit conversion from <paramref name="sourceType"/> to the
    /// dotnet type of the <paramref name="typeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="sourceType">The name of the source type from which to convert.</param>
    /// <param name="sourceValueKinds">The expected <see cref="JsonValueKind"/> or kinds for the conversion.</param>
    /// <param name="dotnetTypeConversion">The code that converts the "value" to a dotnet value suitable
    /// for a constructor.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionFromJsonValueTypeUsingConstructor(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType,
        JsonValueKind[] sourceValueKinds,
        string dotnetTypeConversion)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Conversion from ")
            .Append(sourceType)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append('(')
            .Append(sourceType)
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("if (value.HasDotnetBacking && ")
                .AppendShortcircuitingOr(sourceValueKinds, static (g, v) => g.AppendJsonValueKindEquals("value", v), includeParensIfMultiple: true)
                .AppendLine(")")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return new(")
                    .PushIndent()
                        .AppendBlockIndent(dotnetTypeConversion, omitLastLineEnd: true)
                    .PopIndent()
                    .AppendLine(");")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("return new(value.AsJsonElement);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends an implicit conversion to <paramref name="targetType"/> from the
    /// dotnet type of the <paramref name="typeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration from which to convert.</param>
    /// <param name="targetType">The name of the target type to which to convert.</param>
    /// <param name="forCoreTypes">The core types for which the conversion applies.</param>
    /// <param name="dotnetTypeConversion">The code that converts the value to the target type.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionToJsonValueType(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string targetType,
        CoreTypes forCoreTypes,
        string dotnetTypeConversion)
    {
        if ((typeDeclaration.ImpliedCoreTypes() & forCoreTypes) == 0)
        {
            return generator;
        }

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Conversion to ")
            .Append(targetType)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator ")
            .Append(targetType)
            .Append('(')
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return")
                .PushIndent()
                    .AppendBlockIndent(dotnetTypeConversion, omitLastLineEnd: true)
                .PopIndent()
                .AppendLine(";")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append <c>&lt;see cref="[typeName]"/&gt;</c>.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeName">The type name to which to append the reference.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTypeAsSeeCref(
        this CodeGenerator generator,
        string typeName)
    {
        generator
            .Append("<see cref=\"");

        foreach (char c in typeName)
        {
            if (c == '<')
            {
                generator.Append('{');
            }
            else if (c == '>')
            {
                generator.Append('}');
            }
            else
            {
                generator.Append(c);
            }
        }

        return generator
            .Append("\"/>");
    }

    /// <summary>
    /// Appends an implicit conversion from the
    /// dotnet type of the <paramref name="typeDeclaration"/> to the <paramref name="targetType"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="targetType">The name of the target type towhich to convert.</param>
    /// <param name="dotnetTypeConversion">The code that converts the "value" to a dotnet value suitable
    /// for a constructor.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionToType(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string targetType,
        string dotnetTypeConversion)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Conversion to ")
            .AppendTypeAsSeeCref(targetType)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator ")
            .Append(targetType)
            .Append('(')
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return")
                .PushIndent()
                    .AppendBlockIndent(dotnetTypeConversion, omitLastLineEnd: true)
                .PopIndent()
                .AppendLine(";")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends an implicit conversion from <paramref name="sourceType"/> to the
    /// dotnet type of the <paramref name="typeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="sourceType">The name of the source type from which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionFromJsonValueTypeUsingAs(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Conversion from ")
            .Append(sourceType)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append('(')
            .Append(sourceType)
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("return value.As<")
                .Append(typeDeclaration.DotnetTypeName())
                .AppendLine(">();")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends an implicit conversion from <paramref name="sourceType"/> to the
    /// dotnet type of the <paramref name="typeDeclaration"/>.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="sourceType">The name of the source type from which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionToJsonValueTypeUsingAs(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Conversion to ")
            .Append(sourceType)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator ")
            .Append(sourceType)
            .Append('(')
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("return value.As<")
                .Append(sourceType)
                .AppendLine(">();")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends an implicit conversion from dotnet type of the <paramref name="typeDeclaration"/>
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendImplicitConversionToJsonAny(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Conversion to JsonAny.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
            .AppendIndent("public static implicit operator JsonAny(")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return value.AsAny;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends the FromJson static factory method.
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromJsonFactoryMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("FromJson")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets an instance of the JSON value from a <see cref="JsonElement"/> value.
                /// </summary>
                /// <param name="value">The <see cref="JsonElement"/> value from which to instantiate the instance.</param>
                /// <returns>An instance of this type, initialized from the <see cref="JsonElement"/>.</returns>
                /// <remarks>The returned value will have a <see cref = "IJsonValue.ValueKind"/> of <see cref = "JsonValueKind.Undefined"/> if the
                /// value cannot be constructed from the given instance (e.g. because they have an incompatible .NET backing type).
                /// </remarks>                
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" FromJson(in JsonElement value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return new(value);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends the FromAny static factory method.
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromAnyFactoryMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("FromAny")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets an instance of the JSON value from a <see cref="JsonAny"/> value.
                /// </summary>
                /// <param name="value">The <see cref="JsonAny"/> value from which to instantiate the instance.</param>
                /// <returns>An instance of this type, initialized from the <see cref="JsonAny"/> value.</returns>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                """)
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" FromAny(in JsonAny value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConversionFromValue("value", typeDeclaration.ImpliedCoreTypes())
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends static factory method of the form FromXXX{TValue}.
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to convert.</param>
    /// <param name="forCoreTypes">The core types for which to append conversions.</param>
    /// <param name="jsonValueTypeBaseName">The base name for the JSON value type (e.g. Boolean, String).</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFromTValueFactoryMethod(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        CoreTypes forCoreTypes,
        string jsonValueTypeBaseName)
    {
        if ((typeDeclaration.ImpliedCoreTypes() & forCoreTypes) != 0)
        {
            return generator
                .ReserveName($"From{jsonValueTypeBaseName}")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Gets an instance of the JSON value from a <see cref="JsonAny"/> value.
                    /// </summary>
                    /// <typeparam name="TValue">The type of the value.</typeparam>
                    /// <param name="value">The <see cref="JsonAny"/> value from which to instantiate the instance.</param>
                    /// <returns>An instance of this type, initialized from the <see cref="JsonAny"/> value.</returns>
                    """)
                .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendIndent("public static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" From")
                .Append(jsonValueTypeBaseName)
                .AppendLine("<TValue>(in TValue value)")
                .PushIndent()
                .AppendIndent("where TValue : struct, IJson")
                .Append(jsonValueTypeBaseName)
                .AppendLine("<TValue>")
                .PopIndent()
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendConversionFromValue("value", forCoreTypes)
                .PopIndent()
                .AppendLineIndent("}");
        }
        else
        {
            return generator
                .ReserveName($"From{jsonValueTypeBaseName}")
                .AppendSeparatorLine()
                .AppendLine("#if NET8_0_OR_GREATER")
                .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Gets an instance of the JSON value from a <see cref="JsonAny"/> value.
                    /// </summary>
                    /// <typeparam name="TValue">The type of the value.</typeparam>
                    /// <param name="value">The <see cref="JsonAny"/> value from which to instantiate the instance.</param>
                    /// <returns>An instance of this type, initialized from the <see cref="JsonAny"/> value.</returns>
                    """)
                .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendIndent("static ")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(" IJsonValue<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">.")
                .Append("From")
                .Append(jsonValueTypeBaseName)
                .AppendLine("<TValue>(in TValue value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("if (")
                    .AppendLine("value.HasJsonElementBacking)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendIndent("return new(")
                        .AppendLine("value.AsJsonElement);")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("return Undefined;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendLine("#endif");
        }
    }

    /// <summary>
    /// Appends a static Parse() method to parse an instance of the <paramref name="sourceType"/> to an instance of the
    /// <paramref name="typeDeclaration"/> type.
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to produce the method.</param>
    /// <param name="sourceType">The type of the source from which to parse.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParseMethod(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType)
    {
        return generator
            .ReserveNameIfNotReserved("Parse")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Parses the ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"source\">The source of the JSON string to parse.</param>")
            .AppendLineIndent("/// <param name=\"options\">The (optional) JsonDocumentOptions.</param>")
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" Parse(")
            .Append(sourceType)
            .AppendLine(" source, JsonDocumentOptions options = default)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("using var jsonDocument = JsonDocument.Parse(source, options);")
                .AppendLineIndent("return new(jsonDocument.RootElement.Clone());")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the As{T} conversion method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to produce the method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAsTMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveNameIfNotReserved("As")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the value as an instance of the target value.
                /// </summary>
                /// <typeparam name="TTarget">The type of the target.</typeparam>
                /// <returns>An instance of the target type.</returns>
                [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public TTarget As<TTarget>()
                    where TTarget : struct, IJsonValue<TTarget>
                {
                """)
            .PushIndent()
            .AppendLine("#if NET8_0_OR_GREATER")
            .AppendConditionalBackingValueLineIndent("Backing.JsonElement", "return TTarget.FromJson(this.jsonElementBacking);")
            .AppendConditionalBackingValueLineIndent("Backing.String", "return TTarget.FromString(this);", typeDeclaration.ImpliedCoreTypes(), CoreTypes.String)
            .AppendConditionalBackingValueLineIndent("Backing.Bool", "return TTarget.FromBoolean(this);", typeDeclaration.ImpliedCoreTypes(), CoreTypes.Boolean)
            .AppendConditionalBackingValueLineIndent("Backing.Number", "return TTarget.FromNumber(this);", typeDeclaration.ImpliedCoreTypes(), CoreTypes.Number | CoreTypes.Integer)
            .AppendConditionalBackingValueLineIndent("Backing.Array", "return TTarget.FromArray(this);", typeDeclaration.ImpliedCoreTypes(), CoreTypes.Array)
            .AppendConditionalBackingValueLineIndent("Backing.Object", "return TTarget.FromObject(this);", typeDeclaration.ImpliedCoreTypes(), CoreTypes.Object)
            .AppendConditionalBackingValueLineIndent("Backing.Null", "return TTarget.Null;")
            .AppendSeparatorLine()
            .AppendLineIndent("return TTarget.Undefined;")
            .AppendLine("#else")
            .AppendIndent("return this.As<")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(", TTarget>();")
            .AppendLine("#endif")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the WriteTo() method.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to produce the method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendWriteToMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveName("WriteTo")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .AppendLineIndent("public void WriteTo(Utf8JsonWriter writer)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalBackingValueCallbackIndent(
                    "Backing.JsonElement",
                    "jsonElementBacking",
                    static (g, name) => g.AppendWriteJsonElementBacking(name),
                    returnFromClause: true)
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.Array",
                    "JsonValueHelpers.WriteItems(",
                    "arrayBacking",
                    ", writer);",
                    impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                    forCoreTypes: CoreTypes.Array,
                    returnFromClause: true)
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.Bool",
                    "writer.WriteBooleanValue(",
                    "boolBacking",
                    ");",
                    impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                    forCoreTypes: CoreTypes.Boolean,
                    returnFromClause: true)
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.Number",
                    string.Empty,
                    "numberBacking",
                    ".WriteTo(writer);",
                    impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                    forCoreTypes: CoreTypes.Number | CoreTypes.Integer,
                    returnFromClause: true)
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.Object",
                    "JsonValueHelpers.WriteProperties(",
                    "objectBacking",
                    ", writer);",
                    impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                    forCoreTypes: CoreTypes.Object,
                    returnFromClause: true)
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.String",
                    "writer.WriteStringValue(",
                    "stringBacking",
                    ");",
                    impliedCoreTypes: typeDeclaration.ImpliedCoreTypes(),
                    forCoreTypes: CoreTypes.String,
                    returnFromClause: true)
                .AppendConditionalBackingValueLineIndent(
                    "Backing.Null",
                    "writer.WriteNullValue();",
                    returnFromClause: true)
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the Equals() method overloads.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to produce the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendEqualsOverloads(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .ReserveNameIfNotReserved("Equals")
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <inheritdoc/>
                public override bool Equals(object? obj)
                {
                    return
                        (obj is IJsonValue jv && this.Equals(jv.AsAny)) ||
                        (obj is null && this.IsNull());
                }

                /// <inheritdoc/>
                public bool Equals<T>(in T other)
                    where T : struct, IJsonValue<T>
                {
                    return JsonValueHelpers.CompareValues(this, other);
                }

                /// <summary>
                /// Equality comparison.
                /// </summary>
                /// <param name = "other">The other item with which to compare.</param>
                /// <returns><see langword="true"/> if the values were equal.</returns>
                """)
            .AppendIndent("public bool Equals(in ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" other)")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("return JsonValueHelpers.CompareValues(this, other);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends a static ParseValue() method to parse an instance of the <paramref name="sourceType"/> to an instance of the
    /// <paramref name="typeDeclaration"/> type.
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to produce the method.</param>
    /// <param name="sourceType">The type of the source from which to parse.</param>
    /// <param name="byRef">Whether the parameter is a by-ref value.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParseValueMethod(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string sourceType,
        bool byRef = false)
    {
        generator
            .ReserveNameIfNotReserved("ParseValue")
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Parses the ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"source\">The source of the JSON string to parse.</param>")
            .AppendIndent("public static ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" ParseValue(");

        if (byRef)
        {
            generator.Append("ref ");
        }

        generator
            .Append(sourceType)
            .AppendLine(" source)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLine("#if NET8_0_OR_GREATER")
                .AppendIndent("return IJsonValue<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">.ParseValue(");

        if (byRef)
        {
            generator.Append("ref ");
        }

        generator
                .AppendLine("source);")
                .AppendLine("#else")
                .AppendIndent("return JsonValueHelpers.ParseValue<")
                .Append(typeDeclaration.DotnetTypeName())
                .Append(">(");

        if (byRef)
        {
            generator.Append("ref ");
        }

        return generator
            .AppendLine("source);")
            .AppendLine("#endif")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends a binary operator for the <paramref name="typeDeclaration"/>
    /// to JsonAny.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to add the operator.</param>
    /// <param name="returnType">The return type of the operator.</param>
    /// <param name="operatorSymbol">The symbol to inject for the operator.</param>
    /// <param name="operatorBody">The body to inject for the operator.</param>
    /// <param name="returnValueDocumentation">The return value documentation.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBinaryOperator(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string returnType,
        string operatorSymbol,
        string operatorBody,
        string returnValueDocumentation)
    {
        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Operator ")
            .Append(operatorSymbol)
            .AppendLine(".")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"left\">The lhs of the operator.</param>")
            .AppendLineIndent("/// <param name=\"right\">The rhs of the operator.</param>")
            .AppendLineIndent("/// <returns>")
            .AppendBlockIndentWithPrefix(returnValueDocumentation, "/// ")
            .AppendLineIndent("/// </returns>")
            .AppendIndent("public static ")
            .Append(returnType)
            .Append(" operator ")
            .Append(operatorSymbol)
            .Append("(in ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append(" left, in ")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(" right)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBlockIndent(operatorBody)
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append an ordinal name for a number.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="number">The number for which to generate the ordinal.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendOrdinalName(this CodeGenerator generator, int number)
    {
        generator
                .Append(number);

        if (number >= 11 && number <= 13)
        {
            return generator
                .Append("th");
        }

        return (number % 10) switch
        {
            1 => generator.Append("st"),
            2 => generator.Append("nd"),
            3 => generator.Append("rd"),
            _ => generator.Append("th"),
        };
    }

    /// <summary>
    /// Append a short-circuiting set of OR (||) operations.
    /// </summary>
    /// <typeparam name="T">The type of the entity to be passed to the <paramref name="appendCallback"/>.</typeparam>
    /// <param name="generator">The generator.</param>
    /// <param name="values">The values to append.</param>
    /// <param name="appendCallback">The callback which appends the value.</param>
    /// <param name="includeParensIfMultiple">Indicates whether to wrap the clause in round brackets if there
    /// are multiple values.
    /// </param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendShortcircuitingOr<T>(this CodeGenerator generator, T[] values, Action<CodeGenerator, T> appendCallback, bool includeParensIfMultiple)
    {
        bool includeParens = values.Length > 1 && includeParensIfMultiple;

        if (includeParens)
        {
            generator.Append('(');
        }

        for (int i = 0; i < values.Length; ++i)
        {
            if (i > 0)
            {
                generator.Append(" || ");
            }

            appendCallback(generator, values[i]);
        }

        if (includeParens)
        {
            generator.Append(')');
        }

        return generator;
    }

    /// <summary>
    /// Append an equality comparison for a JsonValueKind.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="lhs">The left hand side of the comparison.</param>
    /// <param name="jsonValueKind">The value kind to compare.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendJsonValueKindEquals(
        this CodeGenerator generator,
        string lhs,
        JsonValueKind jsonValueKind)
    {
        return generator
            .Append(lhs)
            .Append(".ValueKind == ")
            .AppendJsonValueKind(jsonValueKind);
    }

    /// <summary>
    /// Append a JSON value kind.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="valueKind">The value kind to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendJsonValueKind(this CodeGenerator generator, JsonValueKind valueKind)
    {
        return generator
            .Append("JsonValueKind.")
            .Append(valueKind.ToString());
    }

    /// <summary>
    /// Append the default constructor for the type declaration.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the constructor.</param>
    /// <param name="valueType">The type of the value.</param>
    /// <param name="valueCoreType">The core type of the value type.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPublicValueConstructor(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        string valueType,
        CoreTypes valueCoreType)
    {
        CoreTypes impliedCoreTypes = typeDeclaration.ImpliedCoreTypes();

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendIndent("/// Initializes a new instance of the ")
            .AppendTypeAsSeeCref(typeDeclaration.DotnetTypeName())
            .AppendLine(" struct.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"value\">The value from which to construct the instance.</param>")
            .AppendIndent("public ")
            .Append(typeDeclaration.DotnetTypeName())
            .Append("(")
            .Append(valueType)
            .AppendLine(" value)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendBackingFieldAssignment("backing", GetBacking(valueCoreType))
                .AppendBackingFieldAssignment("jsonElementBacking", "default")
                .AppendBackingFieldAssignment("stringBacking", GetValue(valueCoreType, CoreTypes.String, "string.Empty"), impliedCoreTypes, CoreTypes.String)
                .AppendBackingFieldAssignment("boolBacking", GetValue(valueCoreType, CoreTypes.Boolean, "default"), impliedCoreTypes, CoreTypes.Boolean)
                .AppendBackingFieldAssignment("numberBacking", GetValue(valueCoreType, CoreTypes.Number | CoreTypes.Integer, "default"), impliedCoreTypes, CoreTypes.Number | CoreTypes.Integer)
                .AppendBackingFieldAssignment("arrayBacking", GetValue(valueCoreType, CoreTypes.Array, "ImmutableList<JsonAny>.Empty"), impliedCoreTypes, CoreTypes.Array)
                .AppendBackingFieldAssignment("objectBacking", GetValue(valueCoreType, CoreTypes.Object, "ImmutableList<JsonObjectProperty>.Empty"), impliedCoreTypes, CoreTypes.Object)
            .PopIndent()
            .AppendLineIndent("}");

        static string GetBacking(CoreTypes valueCoreTypes)
        {
            return valueCoreTypes switch
            {
                CoreTypes.String => "Backing.String",
                CoreTypes.Boolean => "Backing.Bool",
                CoreTypes.Number => "Backing.Number",
                CoreTypes.Integer => "Backing.Number",
                CoreTypes.Array => "Backing.Array",
                CoreTypes.Object => "Backing.Object",
                _ => throw new InvalidOperationException($"Unsupported backing type {valueCoreTypes}"),
            };
        }

        static string GetValue(CoreTypes typeDeclarationImpliedCoreTypes, CoreTypes valueCoreTypes, string defaultValue)
        {
            return (typeDeclarationImpliedCoreTypes & valueCoreTypes) != 0
                ? "value"
                : defaultValue;
        }
    }

    /// <summary>
    /// End a method declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator EndMethodDeclaration(this CodeGenerator generator)
    {
        return generator
            .PopMemberScope()
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append a parameter list. This will produce the parameters on a single line
    /// for 0, 1, or 2 parameters, and an indented multi-line list for 3 or more parameters.
    /// </summary>
    /// <param name="generator">The generator to which to append the parameter list.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParameterList(
        this CodeGenerator generator,
        params MethodParameter[] parameters)
    {
        if (parameters.Length < 3)
        {
            return AppendParameterListSingleLine(generator, parameters);
        }

        return AppendParameterListIndent(generator, parameters);
    }

    /// <summary>
    /// Append a parameter list on a single line.
    /// </summary>
    /// <param name="generator">The generator to which to append the parameter list.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParameterListSingleLine(CodeGenerator generator, MethodParameter[] parameters)
    {
        if (parameters.Length == 0)
        {
            // If we have no parameters, just emit the brackets.
            return generator.AppendLine("()");
        }

        generator.Append("(");
        bool first = true;

        foreach (MethodParameter parameter in parameters)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                generator.Append(", ");
            }

            generator.AppendParameter(parameter);
        }

        return generator
            .AppendLine(")");
    }

    /// <summary>
    /// Append a parameter list on multiple lines, indented.
    /// </summary>
    /// <param name="generator">The generator to which to append the parameter list.</param>
    /// <param name="parameters">The parameter list.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParameterListIndent(CodeGenerator generator, MethodParameter[] parameters)
    {
        if (parameters.Length == 0)
        {
            // If we have no parameters, just emit the brackets.
            return generator.AppendLine("()");
        }

        generator.AppendLine("(");
        generator.PushIndent();
        bool first = true;

        foreach (MethodParameter parameter in parameters)
        {
            if (first)
            {
                first = false;
            }
            else
            {
                generator.AppendLine(",");
            }

            generator.AppendParameterIndent(parameter);
        }

        return generator
            .PopIndent()
            .AppendLine(")");
    }

    /// <summary>
    /// Append a parameter in a parameter list.
    /// </summary>
    /// <param name="generator">The generator to which to append the parameter.</param>
    /// <param name="parameter">The parameter to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParameterIndent(
        this CodeGenerator generator,
        MethodParameter parameter)
    {
        string name = parameter.GetName(generator, isDeclaration: true);

        if (!string.IsNullOrEmpty(parameter.Modifiers))
        {
            generator
                .AppendIndent(parameter.Modifiers)
                .Append(' ')
                .Append(parameter.Type);
        }
        else
        {
            generator
                .AppendIndent(parameter.Type);
        }

        return generator
            .Append(' ')
            .Append(name);
    }

    /// <summary>
    /// Append a parameter in a parameter list.
    /// </summary>
    /// <param name="generator">The generator to which to append the parameter.</param>
    /// <param name="parameter">The parameter to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParameter(
        this CodeGenerator generator,
        MethodParameter parameter)
    {
        string name = parameter.GetName(generator, isDeclaration: true);

        if (!string.IsNullOrEmpty(parameter.Modifiers))
        {
            generator
                .Append(parameter.Modifiers)
                .Append(' ');
        }

        return generator
            .Append(parameter.Type)
            .Append(' ')
            .Append(name);
    }

    /// <summary>
    /// Emits the parent/child nesting.
    /// </summary>
    /// <param name="generator">The generator to which to append the parent/child declaration nesting.</param>
    /// <param name="typeDeclaration">The type declaration being emitted.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginTypeDeclarationNesting(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        Stack<TypeDeclaration> parentTypes = new();

        TypeDeclaration? current = typeDeclaration.Parent();

        // We need to reverse the order, so we push them onto a stack...
        while (current is not null)
        {
            parentTypes.Push(current);
            current = current.Parent();
        }

        // ...and then pop them off again.
        while (parentTypes.Count > 0)
        {
            TypeDeclaration parent = parentTypes.Pop();
            generator
                .AppendSeparatorLine()
                .AppendDocumentation(parent)
                .BeginPublicReadonlyPartialStructDeclaration(
                    parent.DotnetTypeName());
        }

        return generator;
    }

    /// <summary>
    /// Closes off the parent/child nesting.
    /// </summary>
    /// <param name="generator">The generator to which to append the nested-type closing.</param>
    /// <param name="typeDeclaration">The type declaration being emitted.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator EndTypeDeclarationNesting(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        TypeDeclaration? current = typeDeclaration.Parent();
        while (current is not null)
        {
            generator.EndClassOrStructDeclaration();
            current = current.Parent();
        }

        return generator;
    }

    /// <summary>
    /// Emits the end of a class or struct declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the end of the struct declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator EndClassOrStructDeclaration(this CodeGenerator generator)
    {
        return generator
            .PopMemberScope()
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append a numeric string.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="value">The numeric value to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendNumericLiteral(this CodeGenerator generator, in JsonElement value)
    {
        Debug.Assert(value.ValueKind == JsonValueKind.Number, "The value must be a number.");

        generator.Append(value.GetRawText());

        if (!value.TryGetDouble(out double _))
        {
            // Fall back to a decimal if we can't process the value with a double.
            generator.Append("M");
        }

        return generator;
    }

    /// <summary>
    /// Append an integer string.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="value">The numeric value to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendIntegerLiteral(this CodeGenerator generator, in JsonElement value)
    {
        Debug.Assert(value.ValueKind == JsonValueKind.Number, "The value must be a number.");

        if (value.TryGetInt64(out long result))
        {
            generator.Append(result);
        }
        else if (value.TryGetDouble(out double resultD))
        {
            double roundedResult = Math.Round(resultD);
            if (roundedResult == resultD)
            {
                generator.Append((long)roundedResult);
            }
        }

        return generator;
    }

    /// <summary>
    /// Append a quoted string value.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="value">The numeric value to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendQuotedStringLiteral(this CodeGenerator generator, in JsonElement value)
    {
        Debug.Assert(value.ValueKind == JsonValueKind.String, "The value must be a string.");

        generator.Append(SymbolDisplay.FormatLiteral(value.GetRawText(), true));

        return generator;
    }

    /// <summary>
    /// Append a quoted string value.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="value">The numeric value to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSerializedBooleanLiteral(this CodeGenerator generator, in JsonElement value)
    {
        Debug.Assert(value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False, "The value must be a boolean.");

        generator.Append(SymbolDisplay.FormatLiteral(value.GetRawText(), true));

        return generator;
    }

    /// <summary>
    /// Append a quoted string value.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="value">The numeric value to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendQuotedStringLiteral(this CodeGenerator generator, string value)
    {
        return generator
            .Append(SymbolDisplay.FormatLiteral(value, true));
    }

    /// <summary>
    /// Append an object serialized as a string literal.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="value">The numeric value to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSerializedObjectStringLiteral(this CodeGenerator generator, in JsonElement value)
    {
        Debug.Assert(value.ValueKind == JsonValueKind.Object, "The value must be an object.");

        return generator
            .Append(SymbolDisplay.FormatLiteral(value.GetRawText(), true));
    }

    /// <summary>
    /// Append an array serialized as a string literal.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="value">The numeric value to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSerializedArrayStringLiteral(this CodeGenerator generator, in JsonElement value)
    {
        Debug.Assert(value.ValueKind == JsonValueKind.Array, "The value must be an array.");

        return generator
            .Append(SymbolDisplay.FormatLiteral(value.GetRawText(), true));
    }

    /// <summary>
    /// Format a type name of the form <c>{genericTypeName}&lt;{typeDeclaration.DotnetTypeName()}&gt;</c>.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="genericTypeName">The name of the genertic type.</param>
    /// <param name="typeDeclaration">The type declaration for which to form the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator GenericTypeOf(
        this CodeGenerator generator,
        string genericTypeName,
        TypeDeclaration typeDeclaration)
    {
        return generator
                .Append(genericTypeName)
                .Append('<')
                .Append(typeDeclaration.FullyQualifiedDotnetTypeName())
                .Append('>');
    }

    /// <summary>
    /// Format a type name of the form
    /// <c>{genericTypeName}&lt;{typeDeclaration1.FullyQualifiedDotnetTypeName()}, {typeDeclaration2.FullyQualifiedDotnetTypeName()}}&gt;</c>.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="genericTypeName">The name of the genertic type.</param>
    /// <param name="typeDeclaration1">The first type declaration from which to form the name.</param>
    /// <param name="typeDeclaration2">The second type declaration from which to form the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator GenericTypeOf(
        this CodeGenerator generator,
        string genericTypeName,
        TypeDeclaration typeDeclaration1,
        TypeDeclaration typeDeclaration2)
    {
        return generator
                .Append(genericTypeName)
                .Append('<')
                .Append(typeDeclaration1.FullyQualifiedDotnetTypeName())
                .Append(", ")
                .Append(typeDeclaration2.FullyQualifiedDotnetTypeName())
                .Append('>');
    }

    /// <summary>
    /// Format a type name of the form
    /// <c>{genericTypeName}&lt;{typeDeclaration1.FullyQualifiedDotnetTypeName()}, {typeDeclaration2.FullyQualifiedDotnetTypeName()}, {typeDeclaration3.FullyQualifiedDotnetTypeName()}&gt;</c>.
    /// </summary>
    /// <param name="generator">The generator to which to append the numeric string.</param>
    /// <param name="genericTypeName">The name of the genertic type.</param>
    /// <param name="typeDeclaration1">The 1st type declaration from which to form the name.</param>
    /// <param name="typeDeclaration2">The 2nd type declaration from which to form the name.</param>
    /// <param name="typeDeclaration3">The 3rd type declaration from which to form the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator GenericTypeOf(
        this CodeGenerator generator,
        string genericTypeName,
        TypeDeclaration typeDeclaration1,
        TypeDeclaration typeDeclaration2,
        TypeDeclaration typeDeclaration3)
    {
        return generator
                .Append(genericTypeName)
                .Append('<')
                .Append(typeDeclaration1.FullyQualifiedDotnetTypeName())
                .Append(", ")
                .Append(typeDeclaration2.FullyQualifiedDotnetTypeName())
                .Append(", ")
                .Append(typeDeclaration3.FullyQualifiedDotnetTypeName())
                .Append('>');
    }

    /// <summary>
    /// Emits the start of a partial struct declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the beginning of the struct declaration.</param>
    /// <param name="dotnetTypeName">The .NET type name for the partial struct.</param>
    /// <param name="interfaces">Interfaces to implement.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginPublicReadonlyPartialStructDeclaration(
        this CodeGenerator generator,
        string dotnetTypeName,
        ConditionalCodeSpecification[]? interfaces = null)
    {
        string name = generator.GetTypeNameInScope(dotnetTypeName);
        generator
            .AppendIndent("public readonly partial struct ")
            .AppendLine(name);

        if (interfaces is ConditionalCodeSpecification[] conditionalSpecifications)
        {
            generator.PushIndent();
            ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(generator, conditionalSpecifications, AppendInterface);
            generator.PopIndent();
        }

        return generator
            .AppendLineIndent("{")
            .PushMemberScope(name, ScopeType.Type)
            .ReserveNameIfNotReserved(name) // Reserve the name of the containing scope in its own scope
            .PushIndent();

        static void AppendInterface(CodeGenerator generator, Action<CodeGenerator> appendFunction, int elementIndexInConditionalBlock)
        {
            if (elementIndexInConditionalBlock == 0)
            {
                generator.AppendIndent(": ");
                appendFunction(generator);
            }
            else
            {
                generator
                    .AppendLine(",")
                    .AppendIndent("  "); // Align with the ": "
                appendFunction(generator);
            }
        }
    }

    /// <summary>
    /// Emits the start of a private static class declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the beginning of the struct declaration.</param>
    /// <param name="dotnetTypeName">The .NET type name for the partial struct.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginPrivateStaticClassDeclaration(this CodeGenerator generator, string dotnetTypeName)
    {
        string name = generator.GetTypeNameInScope(dotnetTypeName);
        return generator
            .AppendIndent("private static class ")
            .AppendLine(name)
            .AppendLineIndent("{")
            .PushMemberScope(name, ScopeType.Type)
            .ReserveNameIfNotReserved(name) // Reserve the name of the containing scope in its own scope
            .PushIndent();
    }

    /// <summary>
    /// Emits the start of a private static class declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the beginning of the struct declaration.</param>
    /// <param name="dotnetTypeName">The .NET type name for the partial struct.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginPrivateStaticPartialClassDeclaration(this CodeGenerator generator, string dotnetTypeName)
    {
        string name = generator.GetTypeNameInScope(dotnetTypeName);
        return generator
            .AppendIndent("private static partial class ")
            .AppendLine(name)
            .AppendLineIndent("{")
            .PushMemberScope(name, ScopeType.Type)
            .ReserveNameIfNotReserved(name) // Reserve the name of the containing scope in its own scope
            .PushIndent();
    }

    /// <summary>
    /// Emits the start of a public static class declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the beginning of the struct declaration.</param>
    /// <param name="dotnetTypeName">The .NET type name for the partial struct.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator BeginPublicStaticClassDeclaration(this CodeGenerator generator, string dotnetTypeName)
    {
        string name = generator.GetTypeNameInScope(dotnetTypeName);
        return generator
            .AppendIndent("public static class ")
            .AppendLine(name)
            .AppendLineIndent("{")
            .PushMemberScope(name, ScopeType.Type)
            .ReserveNameIfNotReserved(name) // Reserve the name of the containing scope in its own scope
            .PushIndent();
    }

    /// <summary>
    /// Emits the auto-generated header.
    /// </summary>
    /// <param name="generator">The generator to which to append the beginning of the struct declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendAutoGeneratedHeader(this CodeGenerator generator)
    {
        return generator
            .AppendLine(
            """
            //------------------------------------------------------------------------------
            // <auto-generated>
            //     This code was generated by a tool.
            //
            //     Changes to this file may cause incorrect behavior and will be lost if
            //     the code is regenerated.
            // </auto-generated>
            //------------------------------------------------------------------------------
            """);
    }

    /// <summary>
    /// Append the JsonConverter attribute.
    /// </summary>
    /// <param name="generator">The generator to which to append the JsonConverter attribute.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendJsonConverterAttribute(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .AppendIndent("[System.Text.Json.Serialization.JsonConverter(typeof(Corvus.Json.Internal.JsonValueConverter<")
            .Append(typeDeclaration.DotnetTypeName())
            .AppendLine(">))]");
    }

    /// <summary>
    /// Append the text as paragraphs, splitting on newline and/or carriage return.
    /// </summary>
    /// <param name="generator">The generator to which to append the paragraphs.</param>
    /// <param name="paragraphs">The text containing the paragraphs to append.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendParagraphs(this CodeGenerator generator, string paragraphs)
    {
        string[] lines = NormalizeAndSplitBlockIntoLines(paragraphs, removeBlankLines: true);
        foreach (string line in lines)
        {
            generator
                .AppendLineIndent("/// <para>")
                .AppendIndent("/// ")
                .AppendLine(SymbolDisplay.FormatLiteral(line, false))
                .AppendLineIndent("/// </para>");
        }

        return generator;
    }

    /// <summary>
    /// Append a multi-line block of text at the given indent.
    /// </summary>
    /// <param name="generator">The generator to which to append the block.</param>
    /// <param name="block">The block to append.</param>
    /// <param name="trimWhitespaceOnlyLines">Whether to trim lines that are whitespace only.</param>
    /// <param name="omitLastLineEnd">If <see langword="true"/> then the last line is appended without an additional line-end, leaving
    /// the generator at the end of the block.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBlockIndent(this CodeGenerator generator, string block, bool trimWhitespaceOnlyLines = true, bool omitLastLineEnd = false)
    {
        string[] lines = NormalizeAndSplitBlockIntoLines(block);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (omitLastLineEnd && i == lines.Length - 1)
            {
                generator
                    .AppendIndent(line);
            }
            else
            {
                generator
                    .AppendLineIndent(line, trimWhitespaceOnlyLines);
            }
        }

        return generator;
    }

    /// <summary>
    /// Append a multi-line block of text at the given indent.
    /// </summary>
    /// <param name="generator">The generator to which to append the block.</param>
    /// <param name="block">The block to append.</param>
    /// <param name="trimWhitespaceOnlyLines">Whether to trim lines that are whitespace only.</param>
    /// <param name="omitLastLineEnd">If <see langword="true"/> then the last line is appended without an additional line-end, leaving
    /// the generator at the end of the block.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBlockIndentWithHashOutdent(this CodeGenerator generator, string block, bool trimWhitespaceOnlyLines = true, bool omitLastLineEnd = false)
    {
        string[] lines = NormalizeAndSplitBlockIntoLines(block);

        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (omitLastLineEnd && i == lines.Length - 1)
            {
                if (line[0] == '#')
                {
                    generator.Append(line);
                }
                else
                {
                    generator
                        .AppendIndent(line);
                }
            }
            else
            {
                if (line.Length > 0 && line[0] == '#')
                {
                    generator.AppendLine(line);
                }
                else
                {
                    generator
                        .AppendLineIndent(line, trimWhitespaceOnlyLines);
                }
            }
        }

        return generator;
    }

    /// <summary>
    /// Append a multi-line block of text at the given indent, with a given line prefix.
    /// </summary>
    /// <param name="generator">The generator to which to append the block.</param>
    /// <param name="block">The block to append.</param>
    /// <param name="linePrefix">The prefix for each line.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendBlockIndentWithPrefix(this CodeGenerator generator, string block, string linePrefix)
    {
        string[] lines = NormalizeAndSplitBlockIntoLines(block);
        foreach (string line in lines)
        {
            generator
                .AppendIndent(linePrefix)
                .AppendLine(line);
        }

        return generator;
    }

    /// <summary>
    /// Appends a public static readonly field.
    /// </summary>
    /// <param name="generator">The generator to which to append the field.</param>
    /// <param name="type">The field type name.</param>
    /// <param name="name">The name of the field.</param>
    /// <param name="value">An (optional) initializer value for the field.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendPublicStaticReadonlyField(
        this CodeGenerator generator,
        string type,
        string name,
        string? value)
    {
        generator
            .AppendIndent("public static readonly ")
            .Append(type)
            .Append(' ')
            .Append(generator.GetStaticReadOnlyFieldNameInScope(name));

        if (value is string intializerValue)
        {
            generator
                .Append(" = ")
                .Append(intializerValue);
        }

        return generator
            .AppendLine(";");
    }

    /// <summary>
    /// Gets the name for a field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetFieldNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets unique name for a field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueFieldNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a static readonly field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetStaticReadOnlyFieldNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets a unique name for a static readonly field.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueStaticReadOnlyFieldNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a property.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetPropertyNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a property.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniquePropertyNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a method.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetMethodNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a method.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueMethodNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Reserves a specific name in a scope.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator ReserveName(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.ReserveName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.Unmodified,
                prefix,
                suffix));
    }

    /// <summary>
    /// Reserves a specific name in a scope.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator ReserveNameIfNotReserved(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.ReserveNameIfNotReserved(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.Unmodified,
                prefix,
                suffix));
    }

    /// <summary>
    /// Tries to reserves a specific name in a scope.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static bool TryReserveName(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.TryReserveName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.Unmodified,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a variable.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetVariableNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets a unique name for a variable.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetUniqueVariableNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetUniqueMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.CamelCase,
                prefix,
                suffix));
    }

    /// <summary>
    /// Gets the name for a type.
    /// </summary>
    /// <param name="generator">The generator from which to get the name.</param>
    /// <param name="baseName">The base name.</param>
    /// <param name="childScope">The (optional) child scope from the root scope.</param>
    /// <param name="rootScope">The (optional) root scope overriding the current scope.</param>
    /// <param name="prefix">The (optional) prefix for the name.</param>
    /// <param name="suffix">The (optional) suffix for the name.</param>
    /// <returns>A unique name in the scope.</returns>
    public static string GetTypeNameInScope(
        this CodeGenerator generator,
        string baseName,
        string? childScope = null,
        string? rootScope = null,
        string? prefix = null,
        string? suffix = null)
    {
        return generator.GetOrAddMemberName(
            new CSharpMemberName(
                generator.GetChildScope(childScope, rootScope),
                baseName,
                Casing.PascalCase,
                prefix,
                suffix));
    }

    private static CodeGenerator AppendConversionFromValue(
        this CodeGenerator generator,
        string identifierName,
        CoreTypes forTypes)
    {
        generator
            .AppendIndent("if (")
            .Append(identifierName)
            .AppendLine(".HasJsonElementBacking)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("return new(")
                .Append(identifierName)
                .AppendLine(".AsJsonElement);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendLine()
            .AppendIndent("return ")
            .Append(identifierName)
            .AppendLine(".ValueKind switch")
            .AppendLineIndent("{")
            .PushIndent();

        if ((forTypes & CoreTypes.String) != 0)
        {
            generator
                .AppendIndent("JsonValueKind.String => new((string)")
                .Append(identifierName)
                .AppendLine(".AsString),");
        }

        if ((forTypes & CoreTypes.Boolean) != 0)
        {
            generator
                .AppendLineIndent("JsonValueKind.True => new(true),")
                .AppendLineIndent("JsonValueKind.False => new(false),");
        }

        if ((forTypes & CoreTypes.Number) != 0 ||
            (forTypes & CoreTypes.Integer) != 0)
        {
            generator
                .AppendIndent("JsonValueKind.Number => new(")
                .Append(identifierName)
                .AppendLine(".AsNumber.AsBinaryJsonNumber),");
        }

        if ((forTypes & CoreTypes.Array) != 0)
        {
            generator
                .AppendIndent("JsonValueKind.Array => new(")
                .Append(identifierName)
                .AppendLine(".AsArray.AsImmutableList()),");
        }

        if ((forTypes & CoreTypes.Object) != 0)
        {
            generator
                .AppendIndent("JsonValueKind.Object => new(")
                .Append(identifierName)
                .AppendLine(".AsObject.AsPropertyBacking()),");
        }

        return generator
            .AppendLineIndent("JsonValueKind.Null => Null,")
            .AppendLineIndent("_ => Undefined,")
            .PopIndent()
            .AppendLineIndent("};");
    }

    private static CodeGenerator AppendBackingField(
        this CodeGenerator generator,
        string fieldType,
        string fieldName,
        CoreTypes impliedCoreTypes = CoreTypes.Any,
        CoreTypes forCoreTypes = CoreTypes.Any)
    {
        if ((impliedCoreTypes & forCoreTypes) != 0)
        {
            string localBackingFieldName = generator.GetFieldNameInScope(fieldName);
            generator
                .AppendIndent("private readonly ")
                .Append(fieldType)
                .Append(' ')
                .Append(localBackingFieldName)
                .AppendLine(";");
        }

        return generator;
    }

    private static CodeGenerator AppendBackingFieldAssignment(
        this CodeGenerator generator,
        string fieldName,
        string fieldValue,
        CoreTypes impliedCoreTypes = CoreTypes.Any,
        CoreTypes forCoreTypes = CoreTypes.Any)
    {
        if ((impliedCoreTypes & forCoreTypes) != 0)
        {
            string localBackingFieldName = generator.GetFieldNameInScope(fieldName);
            generator
                .AppendIndent("this.")
                .Append(localBackingFieldName)
                .Append(" = ")
                .Append(fieldValue)
                .AppendLine(";");
        }

        return generator;
    }

    private static CodeGenerator AppendConditionalConstructFromBacking(
        this CodeGenerator generator,
        string backingType,
        string fieldName,
        string identifier = "this",
        CoreTypes impliedCoreTypes = CoreTypes.Any,
        CoreTypes forCoreTypes = CoreTypes.Any)
    {
        return AppendConditionalWrappedBackingValueLineIndent(
            generator,
            backingType,
            "return new(",
            fieldName,
            ");",
            identifier,
            impliedCoreTypes,
            forCoreTypes);
    }

    private static CodeGenerator AppendConditionalWrappedBackingValueLineIndent(
        this CodeGenerator generator,
        string backingType,
        string prefix,
        string fieldName,
        string suffix,
        string identifier = "this",
        CoreTypes impliedCoreTypes = CoreTypes.Any,
        CoreTypes forCoreTypes = CoreTypes.Any,
        bool returnFromClause = false)
    {
        if ((impliedCoreTypes & forCoreTypes) != 0)
        {
            string backingName = generator.GetFieldNameInScope("backing");
            string localBackingFieldName = generator.GetFieldNameInScope(fieldName);
            generator
                .AppendSeparatorLine()
                .AppendIndent("if ((")
                .Append(identifier)
                .Append('.')
                .Append(backingName)
                .Append(" & ")
                .Append(backingType)
                .AppendLine(") != 0)")
                .AppendLineIndent("{")
                .PushIndent()
                .AppendIndent(prefix)
                .Append(identifier)
                .Append('.')
                .Append(localBackingFieldName)
                .AppendLine(suffix);

            if (returnFromClause)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("return;");
            }

            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendConditionalBackingValueLineIndent(
        this CodeGenerator generator,
        string backingType,
        string content,
        CoreTypes impliedCoreTypes = CoreTypes.Any,
        CoreTypes forCoreTypes = CoreTypes.Any,
        bool returnFromClause = false)
    {
        if ((impliedCoreTypes & forCoreTypes) != 0)
        {
            string backingName = generator.GetFieldNameInScope("backing");
            generator
                .AppendSeparatorLine()
                .AppendIndent("if ((this.")
                .Append(backingName)
                .Append(" & ")
                .Append(backingType)
                .AppendLine(") != 0)")
                .AppendLineIndent("{")
                .PushIndent()
                .AppendLineIndent(content);

            if (returnFromClause)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("return;");
            }

            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendWriteJsonElementBacking(this CodeGenerator generator, string fieldName)
    {
        return generator
            .AppendIndent("if (this.")
            .Append(fieldName)
            .AppendLine(".ValueKind != JsonValueKind.Undefined)")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendIndent("this.")
            .Append(fieldName)
            .AppendLine(".WriteTo(writer);")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendConditionalBackingValueCallbackIndent(
        this CodeGenerator generator,
        string backingType,
        string fieldName,
        Action<CodeGenerator, string> callback,
        string identifier = "this",
        CoreTypes impliedCoreTypes = CoreTypes.Any,
        CoreTypes forCoreTypes = CoreTypes.Any,
        bool returnFromClause = false)
    {
        if ((impliedCoreTypes & forCoreTypes) != 0)
        {
            string backingName = generator.GetFieldNameInScope("backing");
            string localFieldName = generator.GetFieldNameInScope(fieldName);
            generator
                .AppendSeparatorLine()
                .AppendIndent("if ((")
                .Append(identifier)
                .Append('.')
                .Append(backingName)
                .Append(" & ")
                .Append(backingType)
                .AppendLine(") != 0)")
                .AppendLineIndent("{")
                .PushIndent();

            callback(generator, localFieldName);

            if (returnFromClause)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("return;");
            }

            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendReturnNullInstanceIfNull(this CodeGenerator generator)
    {
        return generator
            .AppendSeparatorLine()
            .AppendIndent("if (")
            .AppendTestBacking("Backing.Null")
            .AppendLine(")")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("return JsonAny.Null;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendReturnNullJsonElementIfNull(this CodeGenerator generator)
    {
        return generator
            .AppendIndent("if (")
            .AppendTestBacking("Backing.Null")
            .AppendLine(")")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLineIndent("return JsonValueHelpers.NullElement;")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendTestBacking(
        this CodeGenerator generator,
        string backingType)
    {
        string backingName = generator.GetFieldNameInScope("backing");
        return generator
            .Append("(this.")
            .Append(backingName)
            .Append(" & ")
            .Append(backingType)
            .Append(") != 0");
    }

    private static CodeGenerator AppendBackingFieldAssignment(
        this CodeGenerator generator,
        string fieldName,
        string fieldValue)
    {
        string localBackingFieldName = generator.GetFieldNameInScope(fieldName);
        return generator
            .AppendIndent("this.")
            .Append(localBackingFieldName)
            .Append(" = ")
            .Append(fieldValue)
            .AppendLine(";");
    }

    private static CodeGenerator AppendCommaSeparatedNumericSuffixItems(this CodeGenerator generator, string baseName, int count)
    {
        for (int i = 0; i < count; ++i)
        {
            if (i > 0)
            {
                generator
                    .Append(", ");
            }

            generator
                .Append(baseName)
                .Append(i + 1);
        }

        return generator;
    }

    private static CodeGenerator AppendCommaSeparatedNumericSuffixItems(this CodeGenerator generator, string baseNameFirst, string baseNameSecond, int count, string separator = " ")
    {
        for (int i = 0; i < count; ++i)
        {
            if (i > 0)
            {
                generator
                    .Append(", ");
            }

            generator
                .Append(baseNameFirst)
                .Append(i + 1)
                .Append(separator)
                .Append(baseNameSecond)
                .Append(i + 1);
        }

        return generator;
    }

    private static CodeGenerator AppendCommaSeparatedInParameterAndNumericSuffixItems(this CodeGenerator generator, string baseNameType, string baseNameForNumericSuffix, int count, string separator = " ")
    {
        for (int i = 0; i < count; ++i)
        {
            if (i > 0)
            {
                generator
                    .Append(", ");
            }

            generator
                .Append("in ")
                .Append(baseNameType)
                .Append(separator)
                .Append(baseNameForNumericSuffix)
                .Append(i + 1);
        }

        return generator;
    }

    private static CodeGenerator AppendCommaSeparatedValueItems(this CodeGenerator generator, TupleTypeDeclaration tupleType)
    {
        return generator.AppendCommaSeparatedNumericSuffixItems("value.Item", tupleType.ItemsTypes.Length);
    }

    private static CodeGenerator AppendCommaSeparatedTupleTypes(this CodeGenerator generator, TupleTypeDeclaration tupleType)
    {
        for (int i = 0; i < tupleType.ItemsTypes.Length; ++i)
        {
            if (i > 0)
            {
                generator
                    .Append(", ");
            }

            generator
                .Append(tupleType.ItemsTypes[i].ReducedType.FullyQualifiedDotnetTypeName());
        }

        return generator;
    }

    private static string[] NormalizeAndSplitBlockIntoLines(string block, bool removeBlankLines = false)
    {
        string normalizedBlock = block.Replace("\r\n", "\n");
        string[] lines = normalizedBlock.Split(['\n'], removeBlankLines ? StringSplitOptions.RemoveEmptyEntries : StringSplitOptions.None);
        return lines;
    }
}