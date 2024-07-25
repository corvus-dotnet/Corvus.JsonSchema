// <copyright file="ValidationCodeGeneratorExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.CodeAnalysis.CSharp;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extensions to <see cref="CodeGenerator"/> for validation.
/// </summary>
public static partial class ValidationCodeGeneratorExtensions
{
    private const string ValidationClassNameKey = "CSharp_Validation_ValidationClassNameKey";
    private const string JsonPropertyNamesClassNameKey = "CSharp_Validation_JsonPropertyNamesClassNameKey";
    private const string ValidationClassBaseName = "CorvusValidation";
    private const string JsonPropertyNamesClassBaseName = "JsonPropertyNames";
    private const string ResultIdentifierNameKey = "CSharp_Validation_Result_IdentifierName";
    private const string LevelIdentifierNameKey = "CSharp_Validation_Level_IdentifierName";
    private const string ValueKindIdentifierNameKey = "CSharp_Validation_ValueKind_IdentifierName";

    /// <summary>
    /// Gets the result identifier name.
    /// </summary>
    /// <param name="generator">The generator for which to get the result identifier name.</param>
    /// <returns>The result identifier name.</returns>
    /// <exception cref="InvalidOperationException">The result identifier name was not set.</exception>
    public static string ResultIdentifierName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(ResultIdentifierNameKey, out string? value) &&
            value is string identifierName)
        {
            return identifierName;
        }

        throw new InvalidOperationException("Result identifier name not available.");
    }

    /// <summary>
    /// Gets the level identifier name.
    /// </summary>
    /// <param name="generator">The generator for which to get the result identifier name.</param>
    /// <returns>The result identifier name.</returns>
    /// <exception cref="InvalidOperationException">The result identifier name was not set.</exception>
    public static string LevelIdentifierName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(LevelIdentifierNameKey, out string? value) &&
            value is string identifierName)
        {
            return identifierName;
        }

        throw new InvalidOperationException("Level identifier name not available.");
    }

    /// <summary>
    /// Gets the valueKind identifier name.
    /// </summary>
    /// <param name="generator">The generator for which to get the result identifier name.</param>
    /// <returns>The result identifier name.</returns>
    /// <exception cref="InvalidOperationException">The result identifier name was not set.</exception>
    public static string ValueKindIdentifierName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(ValueKindIdentifierNameKey, out string? value) &&
            value is string identifierName)
        {
            return identifierName;
        }

        throw new InvalidOperationException("ValueKind identifier name not available.");
    }

    /// <summary>
    /// Gets the validation class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The validation class name.</returns>
    public static string ValidationClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(ValidationClassNameKey, out (string, string)? value) &&
            value is (string className, string _))
        {
            return className;
        }

        throw new InvalidOperationException("The validation class name has not been created.");
    }

    /// <summary>
    /// Gets the validation class scope.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The fully-qualified validation class scope.</returns>
    public static string ValidationClassScope(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(ValidationClassNameKey, out (string, string)? value) &&
            value is (string _, string scope))
        {
            return scope;
        }

        throw new InvalidOperationException("The validation class scope  has not been created.");
    }

    /// <summary>
    /// Gets the JSON Property Names class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The validation class name.</returns>
    public static string JsonPropertyNamesClassName(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(JsonPropertyNamesClassNameKey, out (string, string)? value) &&
            value is (string className, string _))
        {
            return className;
        }

        throw new InvalidOperationException("The JSON property names class name has not been created.");
    }

    /// <summary>
    /// Gets the validation class scope.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>The fully-qualified validation class scope.</returns>
    public static string JsonPropertyNamesClassScope(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(JsonPropertyNamesClassNameKey, out (string, string)? value) &&
            value is (string _, string scope))
        {
            return scope;
        }

        throw new InvalidOperationException("The JSON property class scope  has not been created.");
    }

    /// <summary>
    /// Gets the validation handler method name for the given keyword.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="handler">The handler for which to get the validation handler method name.</param>
    /// <returns>The validation handler method name for the given handler.</returns>
    /// <remarks>Note that this generates but does not reserve the name in the scope.</remarks>
    public static string ValidationHandlerMethodName(this CodeGenerator generator, IKeywordValidationHandler handler)
    {
        if (generator.TryPeekMetadata(handler.GetType().FullName!, out string? value) &&
            value is string methodName)
        {
            return methodName;
        }

        throw new InvalidOperationException($"The method name has not been created for the keyword {handler.GetType().Name}");
    }

    /// <summary>
    /// Append a call to a validation method.
    /// </summary>
    /// <param name="generator">The generator for the type.</param>
    /// <param name="validationClassName">The validation class name.</param>
    /// <param name="validationMethodName">The validation method name.</param>
    /// <param name="parameters">The parameters to pass to the validation metho.Vad call.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendValidationMethodCall(
        this CodeGenerator generator,
        string validationClassName,
        string validationMethodName,
        string[] parameters)
    {
        return generator
            .AppendSeparatorLine()
            .AppendIndent(generator.ResultIdentifierName())
            .Append(" = ")
            .Append(validationClassName)
            .Append('.')
            .Append(validationMethodName)
            .Append('(')
            .AppendMethodCallParameters(parameters)
            .AppendLine(");")
            .AppendShortCircuit();
    }

    /// <summary>
    /// Append code if the type declaration is using evaluated items.
    /// </summary>
    /// <param name="generator">The generator for the type.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendUsingEvaluatedItems(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.RequiresItemsEvaluationTracking())
        {
            generator
                .AppendIndent(generator.ResultIdentifierName())
                .Append(" = ")
                .Append(generator.ResultIdentifierName())
                .AppendLine(".UsingEvaluatedItems();");
        }

        return generator;
    }

    /// <summary>
    /// Append code if the type declaration is using evaluated properties.
    /// </summary>
    /// <param name="generator">The generator for the type.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendUsingEvaluatedProperties(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.RequiresPropertyEvaluationTracking())
        {
            generator
                .AppendIndent(generator.ResultIdentifierName())
                .Append(" = ")
                .Append(generator.ResultIdentifierName())
                .AppendLine(".UsingEvaluatedProperties();");
        }

        return generator;
    }

    /// <summary>
    /// Prepend validation setup code for children.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration which requires validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <param name="parentHandlerPriority">The parent validation handler priority.</param>
    public static CodeGenerator PrependChildValidationSetup(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children,
        uint parentHandlerPriority)
    {
        foreach (IChildValidationHandler child in children
            .Where(c => c.ValidationHandlerPriority <= parentHandlerPriority)
            .OrderBy(c => c.ValidationHandlerPriority))
        {
            child.AppendValidationSetup(generator, typeDeclaration);
        }

        return generator;
    }

    /// <summary>
    /// Append validation setup code for children.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration which requires validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <param name="parentHandlerPriority">The parent validation handler priority.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendChildValidationSetup(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children,
        uint parentHandlerPriority)
    {
        foreach (IChildValidationHandler child in children
            .Where(c => c.ValidationHandlerPriority > parentHandlerPriority)
            .OrderBy(c => c.ValidationHandlerPriority))
        {
            child.AppendValidationSetup(generator, typeDeclaration);
        }

        return generator;
    }

    /// <summary>
    /// Prepend validation code for appropriate children.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration which requires validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <param name="parentHandlerPriority">The parent validation handler priority.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PrependChildValidationCode(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children,
        uint parentHandlerPriority)
    {
        foreach (IChildValidationHandler child in children
            .Where(c => c.ValidationHandlerPriority <= parentHandlerPriority)
            .OrderBy(c => c.ValidationHandlerPriority))
        {
            child.AppendValidationCode(generator, typeDeclaration);
        }

        return generator;
    }

    /// <summary>
    /// Append validation code for appropriate children.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration which requires validation.</param>
    /// <param name="children">The child handlers for the <see cref="IKeywordValidationHandler"/>.</param>
    /// <param name="parentHandlerPriority">The parent validation handler priority.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendChildValidationCode(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        IReadOnlyCollection<IChildValidationHandler> children,
        uint parentHandlerPriority)
    {
        foreach (IChildValidationHandler child in children
            .Where(c => c.ValidationHandlerPriority > parentHandlerPriority)
            .OrderBy(c => c.ValidationHandlerPriority))
        {
            child.AppendValidationCode(generator, typeDeclaration);
        }

        return generator;
    }

    /// <summary>
    /// Make the scoped validation class name available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PushValidationClassNameAndScope(this CodeGenerator generator)
    {
        string validationClassName = generator.GetTypeNameInScope(ValidationClassBaseName);
        return generator
            .PushMetadata(ValidationClassNameKey, (validationClassName, generator.GetChildScope(validationClassName, null)));
    }

    /// <summary>
    /// Remove the scoped validation class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopValidationClassNameAndScope(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(ValidationClassNameKey);
    }

    /// <summary>
    /// Make the json property names class name available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>
    /// This is safe to call multiple times.
    /// </remarks>
    public static CodeGenerator PushJsonPropertyNamesClassNameAndScope(this CodeGenerator generator)
    {
        if (generator.TryPeekMetadata(JsonPropertyNamesClassNameKey, out (string, string) _))
        {
            return generator;
        }

        string jsonPropertyNamesClass = generator.GetTypeNameInScope(JsonPropertyNamesClassBaseName);
        return generator
            .PushMetadata(JsonPropertyNamesClassNameKey, (jsonPropertyNamesClass, generator.GetChildScope(jsonPropertyNamesClass, null)));
    }

    /// <summary>
    /// Remove the scoped validation class name.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopJsonPropertyNamesClassNameAndScope(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(JsonPropertyNamesClassNameKey);
    }

    /// <summary>
    /// Make the scoped validation handler method names available.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PushValidationHandlerMethodNames(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        foreach (IKeywordValidationHandler handler in typeDeclaration.OrderedValidationHandlers(generator.LanguageProvider))
        {
            string validateMethodName = generator.GetMethodNameInScope(handler.GetType().Name, rootScope: generator.ValidationClassScope());
            generator
                .PushMetadata(handler.GetType().FullName!, validateMethodName);
        }

        return generator;
    }

    /// <summary>
    /// Remove the scoped validation handler method names.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator PopValidationHandlerMethodNames(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        foreach (IKeywordValidationHandler handler in typeDeclaration.OrderedValidationHandlers(generator.LanguageProvider))
        {
            generator
                .PopMetadata(handler.GetType().FullName!);
        }

        return generator;
    }

    /// <summary>
    /// Append the validation constants class.
    /// </summary>
    /// <param name="generator">The generator for the type.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendValidationClass(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return generator
            .BeginValidationClass()
                .AppendValidationConstantFields(typeDeclaration)
                .AppendRegexValidationFields(typeDeclaration)
                .AppendStringValidationConstantProperties(typeDeclaration)
                .AppendValidationMethods(typeDeclaration)
                .AppendRegexValidationFactoryMethods(typeDeclaration)
            .EndValidationClass();
    }

    /// <summary>
    /// Append the Validate() method.
    /// </summary>
    /// <param name="generator">The generator for the type.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendValidateMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <inheritdoc/>")
            .BeginReservedMethodDeclaration(
                "public",
                "ValidationContext",
                "Validate",
                ("in ValidationContext", "validationContext"),
                ("ValidationLevel", "level", "ValidationLevel.Flag"))
                .AppendBlockIndent(
                    $$"""
                    ValidationContext result = validationContext;
                    if (level > ValidationLevel.Flag)
                    {
                        result = result.UsingResults();
                    }
        
                    if (level > ValidationLevel.Basic)
                    {
                        result = result.UsingStack();
                        result = result.PushSchemaLocation({{SymbolDisplay.FormatLiteral(typeDeclaration.RelativeSchemaLocation, true)}});
                    }
                    """)
                .PushResultIdentifierName("result") // Make result...
                .PushLevelIdentifierName("level") // ...and level available to handlers
                .AppendLine()
                .AppendRequiresJsonValueKind(typeDeclaration)
                .AppendValidationHandlerSetup(typeDeclaration)
                .AppendSeparatorLine()
                .AppendValidationHandlerMethodCalls(typeDeclaration)
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    """
                    if (level > ValidationLevel.Basic)
                    {
                        result = result.PopLocation();
                    }

                    return result;
                    """)
                .PopLevelIdentifierName()
                .PopResultIdentifierName()
                .PopIdentifierIfRequiresJsonValueKind(typeDeclaration)
            .EndMethodDeclaration();

        return generator;
    }

    /// <summary>
    /// Append code if the type declaration is using the JsonValueKind.
    /// </summary>
    /// <param name="generator">The generator for the type.</param>
    /// <param name="typeDeclaration">The type declaration.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    private static CodeGenerator AppendRequiresJsonValueKind(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.RequiresJsonValueKind())
        {
            generator
                .ReserveName("valueKind")
                .PushValueKindIdentifier("valueKind") // Make this accessible in deeper scopes
                .AppendLineIndent("JsonValueKind valueKind = this.ValueKind;");
        }

        return generator;
    }

    private static CodeGenerator PopIdentifierIfRequiresJsonValueKind(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.RequiresJsonValueKind())
        {
            generator
                .PopValueKindIdentifier();
        }

        return generator;
    }

    /// <summary>
    /// Append a short-circuit return.
    /// </summary>
    /// <param name="generator">The generator for the type.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    private static CodeGenerator AppendShortCircuit(this CodeGenerator generator)
    {
        return generator
            .AppendSeparatorLine()
            .AppendIndent("if (")
            .Append(generator.LevelIdentifierName())
            .Append(" == ValidationLevel.Flag && !")
            .Append(generator.ResultIdentifierName())
            .AppendLine(".IsValid)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("return ")
                .Append(generator.ResultIdentifierName())
                .AppendLine(";")
            .PopIndent()
            .AppendLineIndent("}");
    }

    /// <summary>
    /// Append the parameters for a call to a method.
    /// </summary>
    /// <param name="generator">The generator for the type.</param>
    /// <param name="parameters">The parameters to pass to the method call.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    /// <remarks>This does not insert the parenthesis.</remarks>
    private static CodeGenerator AppendMethodCallParameters(
    this CodeGenerator generator,
    string[] parameters)
    {
        bool isFirst = true;
        foreach (string parameter in parameters)
        {
            if (isFirst)
            {
                isFirst = false;
            }
            else
            {
                generator.Append(", ");
            }

            generator.Append(parameter);
        }

        return generator;
    }

    private static CodeGenerator BeginValidationClass(this CodeGenerator generator)
    {
        string validationClassName = generator.ValidationClassName();

        return generator
            .AppendSeparatorLine()
            .BeginReservedPrivateStaticPartialClassDeclaration(validationClassName);
    }

    private static CodeGenerator EndValidationClass(this CodeGenerator generator)
    {
        return generator.EndClassOrStructDeclaration();
    }

    private static CodeGenerator AppendValidationHandlerSetup(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator.AppendUsingEvaluatedItems(typeDeclaration);
        generator.AppendUsingEvaluatedProperties(typeDeclaration);

        foreach (IKeywordValidationHandler handler in typeDeclaration.OrderedValidationHandlers(generator.LanguageProvider))
        {
            handler.AppendValidationSetup(generator, typeDeclaration);
        }

        return generator;
    }

    private static CodeGenerator AppendValidationHandlerMethodCalls(
        this CodeGenerator generator,
        TypeDeclaration typeDeclaration)
    {
        foreach (IKeywordValidationHandler handler in typeDeclaration.OrderedValidationHandlers(generator.LanguageProvider))
        {
            handler.AppendValidationMethodCall(generator, typeDeclaration);
        }

        return generator;
    }

    private static CodeGenerator AppendValidationMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        foreach (IKeywordValidationHandler handler in typeDeclaration.OrderedValidationHandlers(generator.LanguageProvider))
        {
            handler.AppendValidationMethod(generator, typeDeclaration);
        }

        return generator;
    }

    private static CodeGenerator PushValueKindIdentifier(this CodeGenerator generator, string valueKindIdentifierName)
    {
        return generator
            .PushMetadata(ValueKindIdentifierNameKey, valueKindIdentifierName);
    }

    private static CodeGenerator PopValueKindIdentifier(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(ValueKindIdentifierNameKey);
    }

    private static CodeGenerator PushResultIdentifierName(this CodeGenerator generator, string resultIdentifierName)
    {
        return generator
            .PushMetadata(ResultIdentifierNameKey, resultIdentifierName);
    }

    private static CodeGenerator PushLevelIdentifierName(this CodeGenerator generator, string levelIdentifierName)
    {
        return generator
            .PushMetadata(LevelIdentifierNameKey, levelIdentifierName);
    }

    private static CodeGenerator PopResultIdentifierName(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(ResultIdentifierNameKey);
    }

    private static CodeGenerator PopLevelIdentifierName(this CodeGenerator generator)
    {
        return generator
            .PopMetadata(LevelIdentifierNameKey);
    }
}