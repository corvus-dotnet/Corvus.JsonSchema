// <copyright file="CodeGenerationExtensions.JsonSchema.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration
{
    /// <summary>
    /// Code generation extensions for JSON Schema related functionality.
    /// </summary>
    internal static partial class CodeGenerationExtensions
    {
        private const string JsonSchemaClassNameKey = "CSharp_JsonSchema_JsonSchemaClassNameKey";
        private const string BuilderClassNameKey = "CSharp_JsonSchema_BuilderClassNameKey";
        private const string SourceClassNameKey = "CSharp_JsonSchema_SourceClassNameKey";
        private const string JsonPropertyNamesEscapedClassNameKey = "CSharp_JsonSchema_JsonPropertyNamesEscapedClassNameKey";
        private const string JsonPropertyNamesClassNameKey = "CSharp_JsonSchema_JsonPropertyNamesClassNameKey";
        private const string BuilderClassBaseName = "Builder";
        private const string SourceClassBaseName = "Source";
        private const string ArrayBuilderClassBaseName = "ArrayBuilder";
        private const string ObjectBuilderClassBaseName = "ObjectBuilder";
        private const string JsonSchemaClassBaseName = "JsonSchema";
        private const string JsonPropertyNamesEscapedClassBaseName = "JsonPropertyNamesEscaped";
        private const string JsonPropertyNamesClassBaseName = "JsonPropertyNames";

        /// <summary>
        /// Make the scoped json schema class name available.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>A reference to the generator having completed the operation.</returns>
        public static CodeGenerator PushJsonSchemaClassNameAndScope(this CodeGenerator generator)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (!generator.TryPeekMetadata(JsonSchemaClassNameKey, out (string, string) _))
            {
                string jsonSchemaClassName = generator.GetTypeNameInScope(JsonSchemaClassBaseName);
                return generator
                    .PushMetadata(JsonSchemaClassNameKey, (jsonSchemaClassName, generator.GetChildScope(jsonSchemaClassName, null)));
            }

            return generator;
        }

        /// <summary>
        /// Remove the scoped json schema class name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>A reference to the generator having completed the operation.</returns>
        public static CodeGenerator PopJsonSchemaClassNameAndScope(this CodeGenerator generator)
        {
            return generator
                .PopMetadata(JsonSchemaClassNameKey);
        }

        /// <summary>
        /// Make the escaped json property names class name available.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>A reference to the generator having completed the operation.</returns>
        /// <remarks>
        /// This is safe to call multiple times.
        /// </remarks>
        public static CodeGenerator PushJsonPropertyNamesEscapedClassNameAndScope(this CodeGenerator generator)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (generator.TryPeekMetadata(JsonPropertyNamesEscapedClassNameKey, out (string, string) _))
            {
                return generator;
            }

            string jsonPropertyNamesClass = generator.GetTypeNameInScope(JsonPropertyNamesEscapedClassBaseName);
            return generator
                .PushMetadata(JsonPropertyNamesEscapedClassNameKey, (jsonPropertyNamesClass, generator.GetChildScope(jsonPropertyNamesClass, null)));
        }

        /// <summary>
        /// Remove the scoped escaped json property names class name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>A reference to the generator having completed the operation.</returns>
        public static CodeGenerator PopJsonPropertyNamesEscapedClassNameAndScope(this CodeGenerator generator)
        {
            return generator
                .PopMetadata(JsonPropertyNamesEscapedClassNameKey);
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
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (generator.TryPeekMetadata(JsonPropertyNamesClassNameKey, out (string, string) _))
            {
                return generator;
            }

            string jsonPropertyNamesClass = generator.GetTypeNameInScope(JsonPropertyNamesClassBaseName);
            return generator
                .PushMetadata(JsonPropertyNamesClassNameKey, (jsonPropertyNamesClass, generator.GetChildScope(jsonPropertyNamesClass, null)));
        }

        /// <summary>
        /// Remove the scoped json property names class name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>A reference to the generator having completed the operation.</returns>
        public static CodeGenerator PopJsonPropertyNamesClassNameAndScope(this CodeGenerator generator)
        {
            return generator
                .PopMetadata(JsonPropertyNamesClassNameKey);
        }

        /// <summary>
        /// Make the Builder class name available.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>A reference to the generator having completed the operation.</returns>
        /// <remarks>
        /// This is safe to call multiple times.
        /// </remarks>
        public static CodeGenerator PushBuilderClassNameAndScope(this CodeGenerator generator)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (generator.TryPeekMetadata(BuilderClassNameKey, out (string, string, string, string) _))
            {
                return generator;
            }

            string builderClass = generator.GetTypeNameInScope(BuilderClassBaseName);
            string arrayBuilderClass = generator.GetTypeNameInScope(ArrayBuilderClassBaseName);
            string objectBuilderClass = generator.GetTypeNameInScope(ObjectBuilderClassBaseName);
            return generator
                .PushMetadata(BuilderClassNameKey, (builderClass, arrayBuilderClass, objectBuilderClass, generator.GetChildScope(builderClass, null)));
        }

        /// <summary>
        /// Remove the scoped Builder class name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>A reference to the generator having completed the operation.</returns>
        public static CodeGenerator PopBuilderClassNameAndScope(this CodeGenerator generator)
        {
            return generator
                .PopMetadata(BuilderClassNameKey);
        }

        /// <summary>
        /// Make the Source class name available.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>A reference to the generator having completed the operation.</returns>
        /// <remarks>
        /// This is safe to call multiple times.
        /// </remarks>
        public static CodeGenerator PushSourceClassNameAndScope(this CodeGenerator generator)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (generator.TryPeekMetadata(SourceClassNameKey, out (string, string) _))
            {
                return generator;
            }

            string builderClass = generator.GetTypeNameInScope(SourceClassBaseName);
            return generator
                .PushMetadata(SourceClassNameKey, (builderClass, generator.GetChildScope(builderClass, null)));
        }

        /// <summary>
        /// Remove the Source class name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>A reference to the generator having completed the operation.</returns>
        public static CodeGenerator PopSourceClassNameAndScope(this CodeGenerator generator)
        {
            return generator
                .PopMetadata(SourceClassNameKey);
        }

        /// <summary>
        /// Gets the JsonSchema class name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>The validation class name.</returns>
        public static string JsonSchemaClassName(this CodeGenerator generator)
        {
            if (generator.TryPeekMetadata(JsonSchemaClassNameKey, out (string, string)? value) &&
                value is (string className, string _))
            {
                return className;
            }

            throw new InvalidOperationException("The JSON Schema class name has not been created.");
        }

        /// <summary>
        /// Gets the JsonSchema class scope.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>The fully-qualified validation class scope.</returns>
        public static string JsonSchemaClassScope(this CodeGenerator generator)
        {
            if (generator.TryPeekMetadata(JsonSchemaClassNameKey, out (string, string)? value) &&
                value is (string _, string scope))
            {
                return scope;
            }

            throw new InvalidOperationException("The JSON Schema class scope  has not been created.");
        }


        /// <summary>
        /// Gets the JsonPropertyNamesEscaped class name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>The class name.</returns>
        public static string JsonPropertyNamesEscapedClassName(this CodeGenerator generator)
        {
            if (generator.TryPeekMetadata(JsonPropertyNamesEscapedClassNameKey, out (string, string)? value) &&
                value is (string className, string _))
            {
                return className;
            }

            throw new InvalidOperationException("The JsonPropertyNamesEscaped class name has not been created.");
        }

        /// <summary>
        /// Gets the JsonPropertyNamesEscaped class scope.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>The fully-qualified class scope.</returns>
        public static string JsonPropertyNamesEscapedScope(this CodeGenerator generator)
        {
            if (generator.TryPeekMetadata(JsonPropertyNamesEscapedClassNameKey, out (string, string)? value) &&
                value is (string _, string scope))
            {
                return scope;
            }

            throw new InvalidOperationException("The JsonPropertyNamesEscaped class scope  has not been created.");
        }

        /// <summary>
        /// Gets the JsonPropertyNames class name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>The class name.</returns>
        public static string JsonPropertyNamesClassName(this CodeGenerator generator)
        {
            if (generator.TryPeekMetadata(JsonPropertyNamesClassNameKey, out (string, string)? value) &&
                value is (string className, string _))
            {
                return className;
            }

            throw new InvalidOperationException("The JsonPropertyNames class name has not been created.");
        }

        /// <summary>
        /// Gets the JsonPropertyNames class scope.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>The fully-qualified class scope.</returns>
        public static string JsonPropertyNamesScope(this CodeGenerator generator)
        {
            if (generator.TryPeekMetadata(JsonPropertyNamesClassNameKey, out (string, string)? value) &&
                value is (string _, string scope))
            {
                return scope;
            }

            throw new InvalidOperationException("The JsonPropertyNames class scope  has not been created.");
        }

        /// <summary>
        /// Gets the JsonSchema class name for a particular type name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <param name="fullyQualifiedTypeName">The fully qualified type name.</param>
        /// <returns>The class name.</returns>
        public static string JsonSchemaClassName(this CodeGenerator generator, string fullyQualifiedTypeName)
        {
            return generator.GetTypeNameInScope(JsonSchemaClassBaseName, rootScope: fullyQualifiedTypeName);
        }


        /// <summary>
        /// Gets the Builder class name for a particular type name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <param name="fullyQualifiedTypeName">The fully qualified type name.</param>
        /// <returns>The class name.</returns>
        public static string BuilderClassName(this CodeGenerator generator, string fullyQualifiedTypeName)
        {
            return generator.GetTypeNameInScope(BuilderClassBaseName, rootScope: fullyQualifiedTypeName);
        }

        /// <summary>
        /// Gets the ArrayBuilder class name for a particular type name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <param name="fullyQualifiedTypeName">The fully qualified type name.</param>
        /// <returns>The class name.</returns>
        public static string ArrayBuilderClassName(this CodeGenerator generator, string fullyQualifiedTypeName)
        {
            return generator.GetTypeNameInScope(ArrayBuilderClassBaseName, rootScope: fullyQualifiedTypeName);
        }

        /// <summary>
        /// Gets the ObjectBuilder class name for a particular type name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <param name="fullyQualifiedTypeName">The fully qualified type name.</param>
        /// <returns>The class name.</returns>
        public static string ObjectBuilderClassName(this CodeGenerator generator, string fullyQualifiedTypeName)
        {
            return generator.GetTypeNameInScope(ObjectBuilderClassBaseName, rootScope: fullyQualifiedTypeName);
        }

        /// <summary>
        /// Gets the Builder class name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>The class name.</returns>
        public static string BuilderClassName(this CodeGenerator generator)
        {
            if (generator.TryPeekMetadata(BuilderClassNameKey, out (string, string, string, string)? value) &&
                value is (string className, string _, string _, string _))
            {
                return className;
            }

            throw new InvalidOperationException("The Builder class name has not been created.");
        }

        /// <summary>
        /// Gets the ArrayBuilder class name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>The class name.</returns>
        public static string ArrayBuilderClassName(this CodeGenerator generator)
        {
            if (generator.TryPeekMetadata(BuilderClassNameKey, out (string, string, string, string)? value) &&
                value is (string _, string arrayClassName, string _, string _))
            {
                return arrayClassName;
            }

            throw new InvalidOperationException("The ArrayBuilder class name has not been created.");
        }

        /// <summary>
        /// Gets the ArrayBuilder class name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>The class name.</returns>
        public static string ObjectBuilderClassName(this CodeGenerator generator)
        {
            if (generator.TryPeekMetadata(BuilderClassNameKey, out (string, string, string, string)? value) &&
                value is (string _, string _, string objectClassName, string _))
            {
                return objectClassName;
            }

            throw new InvalidOperationException("The ObjectBuilder class name has not been created.");
        }

        /// <summary>
        /// Gets the Builder class scope.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>The fully-qualified class scope.</returns>
        public static string BuilderScope(this CodeGenerator generator)
        {
            if (generator.TryPeekMetadata(BuilderClassNameKey, out (string, string, string, string)? value) &&
                value is (string _, string _, string _, string scope))
            {
                return scope;
            }

            throw new InvalidOperationException("The Builder class scope  has not been created.");
        }

        /// <summary>
        /// Gets the Source class name for a particular type name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <param name="fullyQualifiedTypeName">The fully qualified type name.</param>
        /// <returns>The class name.</returns>
        public static string SourceClassName(this CodeGenerator generator, string fullyQualifiedTypeName)
        {
            return generator.GetTypeNameInScope(SourceClassBaseName, rootScope: fullyQualifiedTypeName);
        }

        /// <summary>
        /// Gets the Source class name.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>The class name.</returns>
        public static string SourceClassName(this CodeGenerator generator)
        {
            if (generator.TryPeekMetadata(SourceClassNameKey, out (string, string)? value) &&
                value is (string className, string _))
            {
                return className;
            }

            throw new InvalidOperationException("The Source class name has not been created.");
        }

        /// <summary>
        /// Gets the Source class scope.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>The fully-qualified class scope.</returns>
        public static string SourceScope(this CodeGenerator generator)
        {
            if (generator.TryPeekMetadata(SourceClassNameKey, out (string, string)? value) &&
                value is (string _, string scope))
            {
                return scope;
            }

            throw new InvalidOperationException("The Source class scope  has not been created.");
        }

        /// <summary>
        /// Appends an EvaluateSchema method to the generated type.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <returns>A reference to the generator having completed the operation.</returns>
        public static CodeGenerator AppendEvaluateSchemaMethod(this CodeGenerator generator)
        {
            return generator
                .ReserveName("EvaluateSchema")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    $$"""
                    /// <summary>
                    /// Evaluate this instance against the JSON Schema for this type.
                    /// </summary>
                    /// <params name="resultsCollector">The (optional) results collector.</params>
                    /// <returns><see langword="true" /> if the instance evaluates against the schema.</returns>
                    [MethodImpl(MethodImplOptions.AggressiveInlining)]
                    public bool EvaluateSchema(IJsonSchemaResultsCollector? resultsCollector = null)
                    {
                        return {{generator.JsonSchemaClassName()}}.Evaluate(_parent, _idx, resultsCollector);
                    }
                    """);
        }

        /// <summary>
        /// Appends a static JsonSchema Evaluate method to the generated type.
        /// </summary>
        /// <param name="generator">The code generator.</param>
        /// <param name="typeDeclaration">The type declaration for which to generate the evaluation method.</param>
        /// <returns>A reference to the generator having completed the operation.</returns>
        public static CodeGenerator AppendJsonSchemaEvaluateMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
        {
            return generator
                .ReserveName("Evaluate")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    """
                    /// <summary>
                    /// Applies the JSON schema semantics defined by this type to the instance determined by the given document and index.
                    /// </summary>
                    /// <param name="parentDocument">The parent document.</param>
                    /// <param name="parentIndex">The parent index.</param>
                    /// <param name="context">A reference to the validation context, configured with the appropriate values.</param>
                    internal static void Evaluate(IJsonDocument parentDocument, int parentIndex, ref JsonSchemaContext context)
                    {
                        // NOP AS YET
                    }
                    """)
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    $$"""
                    internal static bool Evaluate(IJsonDocument parentDocument, int parentIndex, IJsonSchemaResultsCollector? resultsCollector = null)
                    {
                        JsonSchemaContext context = JsonSchemaContext.BeginContext(
                            parentDocument,
                            parentIndex,
                            usingEvaluatedItems: {{(typeDeclaration.ExplicitUnevaluatedItemsType() is not null ? "true" : "false")}},
                            usingEvaluatedProperties: {{(typeDeclaration.LocalEvaluatedPropertyType() is not null || typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is not null ? "true" : "false")}},
                            resultsCollector: resultsCollector);

                        try
                        {
                            Evaluate(parentDocument, parentIndex, ref context);
                            context.EndContext();
                            return context.IsMatch;
                        }
                        finally
                        {
                            context.Dispose();
                        }
                    }
                    """);
        }
    }
}