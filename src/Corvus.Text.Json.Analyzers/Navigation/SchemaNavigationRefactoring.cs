// <copyright file="SchemaNavigationRefactoring.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Corvus.Text.Json.Analyzers;

/// <summary>
/// CTJ-NAV: Provides a "Go to Schema Definition" refactoring for types generated
/// from JSON Schema via <c>JsonSchemaTypeGeneratorAttribute</c>.
/// </summary>
/// <remarks>
/// <para>
/// When the cursor is on an identifier that resolves to a schema-generated type
/// (or to a variable/property/parameter of such a type, including those typed as
/// <c>IJsonElement&lt;T&gt;</c> or <c>IMutableJsonElement&lt;T&gt;</c>), this
/// refactoring offers a "Go to schema" action that opens the source JSON Schema file.
/// </para>
/// <para>
/// If the generated type has a <c>SchemaLocation</c> const containing a JSON pointer
/// fragment (e.g., <c>"person.json#/properties/name"</c>), the action navigates to
/// the corresponding line within the schema file.
/// </para>
/// </remarks>
[ExportCodeRefactoringProvider(LanguageNames.CSharp, Name = nameof(SchemaNavigationRefactoring))]
[Shared]
public sealed class SchemaNavigationRefactoring : CodeRefactoringProvider
{
    private const string GeneratorAttributeName = "JsonSchemaTypeGeneratorAttribute";
    private const string GeneratorAttributeShortName = "JsonSchemaTypeGenerator";
    private const string JsonSchemaNestedClassName = "JsonSchema";
    private const string SchemaLocationFieldName = "SchemaLocation";

    /// <inheritdoc/>
    public override async Task ComputeRefactoringsAsync(CodeRefactoringContext context)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
        {
            return;
        }

        // Find the node under the cursor.
        SyntaxNode? node = root.FindNode(context.Span);
        if (node is null)
        {
            return;
        }

        SemanticModel? semanticModel = await context.Document.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
        if (semanticModel is null)
        {
            return;
        }

        IPropertySymbol? accessedProperty = null;

        ITypeSymbol? typeSymbol;
        if (node is VariableDeclaratorSyntax variableDeclarator)
        {
            // Handle cursor on variable name in declarations: Order order = ...
            ISymbol? declared = semanticModel.GetDeclaredSymbol(variableDeclarator, context.CancellationToken);
            typeSymbol = (declared as ILocalSymbol)?.Type;
        }
        else if (node is ParameterSyntax parameterSyntax)
        {
            // Handle cursor on parameter name: void Method(Order order)
            ISymbol? declared = semanticModel.GetDeclaredSymbol(parameterSyntax, context.CancellationToken);
            typeSymbol = (declared as IParameterSymbol)?.Type;
        }
        else
        {
            // We're looking for type name identifiers.
            IdentifierNameSyntax? identifier = node as IdentifierNameSyntax
                ?? node.Parent as IdentifierNameSyntax;

            // Also handle GenericNameSyntax (e.g., IJsonElement<Widget>).
            GenericNameSyntax? genericName = null;

            if (identifier is null)
            {
                // Also handle qualified names like V5Example.Model.Person
                if (node is QualifiedNameSyntax qualifiedName)
                {
                    identifier = qualifiedName.Right as IdentifierNameSyntax;
                    genericName = qualifiedName.Right as GenericNameSyntax;
                }
                else if (node is GenericNameSyntax gns)
                {
                    genericName = gns;
                }
                else if (node.Parent is GenericNameSyntax parentGns)
                {
                    genericName = parentGns;
                }
            }

            if (identifier is null && genericName is null)
            {
                return;
            }

            SyntaxNode nodeForSymbol = (SyntaxNode?)identifier ?? genericName!;

            // Resolve the symbol.
            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(nodeForSymbol, context.CancellationToken);
            ISymbol? symbol = symbolInfo.Symbol ?? symbolInfo.CandidateSymbols.FirstOrDefault();

            // If the symbol is a local/parameter/field of a generated type, get the type.
            typeSymbol = symbol switch
            {
                ITypeSymbol t => t,
                ILocalSymbol l => l.Type,
                IParameterSymbol p => p.Type,
                IFieldSymbol f => f.Type,
                IPropertySymbol prop => prop.Type,
                IMethodSymbol m => m.ReturnType,
                _ => null,
            };

            // Track property access for fallback to containing type.
            if (symbol is IPropertySymbol accessedProp)
            {
                accessedProperty = accessedProp;
            }
        }

        if (typeSymbol is not INamedTypeSymbol namedType)
        {
            return;
        }

        // Unwrap IJsonElement<T> / IMutableJsonElement<T> to get the concrete type T.
        namedType = UnwrapJsonElementInterface(namedType);

        // Find the JsonSchemaTypeGeneratorAttribute on the type.
        SchemaInfo? schemaInfo = FindSchemaInfo(namedType, context.Document.Project, context.CancellationToken);

        // Read the SchemaLocation const to get the JSON pointer fragment.
        string? schemaLocation = GetSchemaLocation(namedType);
        string? jsonPointer = ExtractJsonPointer(schemaLocation);

        // If the type's SchemaLocation contains a full URL with $id, resolve the
        // schema file by matching $id in AdditionalDocuments.
        if (schemaInfo is null && schemaLocation?.Length > 0)
        {
            string? baseUrl = ExtractBaseUrl(schemaLocation);
            if (baseUrl is not null)
            {
                schemaInfo = await FindSchemaByIdAsync(baseUrl, context.Document.Project, context.CancellationToken).ConfigureAwait(false);
            }
        }

        // If the property's own type is not schema-generated (e.g. a project-global
        // type like JsonString), fall back to the containing type's schema and append
        // /properties/{jsonPropName} to the pointer.
        bool usedPropertyFallback = false;
        if (schemaInfo is null && accessedProperty is not null)
        {
            (SchemaInfo Info, string? Pointer)? parentResult = await ResolveContainingTypeSchemaAsync(
                accessedProperty, context.Document.Project, context.CancellationToken).ConfigureAwait(false);

            if (parentResult is not null)
            {
                schemaInfo = parentResult.Value.Info;
                jsonPointer = (parentResult.Value.Pointer ?? string.Empty) +
                    "/properties/" + ToCamelCase(accessedProperty.Name);
                usedPropertyFallback = true;
            }
        }

        if (schemaInfo is null)
        {
            return;
        }

        // Register the primary action ("Go to schema type" or "Go to schema").
        string primaryPrefix = accessedProperty is not null && !usedPropertyFallback
            ? "Go to schema type"
            : "Go to schema";
        NavigateToSchemaAction? primaryAction = await BuildSchemaActionAsync(
            primaryPrefix, schemaInfo.Value, jsonPointer,
            context.Document.Project, context.CancellationToken).ConfigureAwait(false);

        if (primaryAction is not null)
        {
            context.RegisterRefactoring(primaryAction);
        }

        // If we navigated to a type that has its own schema (not the fallback case),
        // and this was a property access, also offer to navigate to the property
        // declaration on the parent type's schema.
        if (accessedProperty is not null && !usedPropertyFallback)
        {
            (SchemaInfo Info, string? Pointer)? parentResult = await ResolveContainingTypeSchemaAsync(
                accessedProperty, context.Document.Project, context.CancellationToken).ConfigureAwait(false);

            if (parentResult is not null)
            {
                string propPointer = (parentResult.Value.Pointer ?? string.Empty) +
                    "/properties/" + ToCamelCase(accessedProperty.Name);

                NavigateToSchemaAction? propAction = await BuildSchemaActionAsync(
                    "Go to property declaration", parentResult.Value.Info, propPointer,
                    context.Document.Project, context.CancellationToken).ConfigureAwait(false);

                if (propAction is not null)
                {
                    context.RegisterRefactoring(propAction);
                }
            }
        }
    }

    /// <summary>
    /// Resolves the schema info for the containing type of the given property, using
    /// attribute-based lookup and <c>$id</c>-based fallback.
    /// </summary>
    private async Task<(SchemaInfo Info, string? Pointer)?> ResolveContainingTypeSchemaAsync(
        IPropertySymbol accessedProperty,
        Project project,
        CancellationToken cancellationToken)
    {
        INamedTypeSymbol? containingType = accessedProperty.ContainingType;
        if (containingType is null)
        {
            return null;
        }

        containingType = UnwrapJsonElementInterface(containingType);
        SchemaInfo? schemaInfo = FindSchemaInfo(containingType, project, cancellationToken);
        string? schemaLocation = GetSchemaLocation(containingType);

        if (schemaInfo is null && schemaLocation?.Length > 0)
        {
            string? baseUrl = ExtractBaseUrl(schemaLocation);
            if (baseUrl is not null)
            {
                schemaInfo = await FindSchemaByIdAsync(baseUrl, project, cancellationToken).ConfigureAwait(false);
            }
        }

        if (schemaInfo is null)
        {
            return null;
        }

        string? pointer = ExtractJsonPointer(schemaLocation);
        return (schemaInfo.Value, pointer);
    }

    /// <summary>
    /// Builds a <see cref="NavigateToSchemaAction"/> by resolving the pointer to a
    /// line/column position within the schema file.
    /// </summary>
    private static async Task<NavigateToSchemaAction?> BuildSchemaActionAsync(
        string titlePrefix,
        SchemaInfo schemaInfo,
        string? jsonPointer,
        Project project,
        CancellationToken cancellationToken)
    {
        int? targetLine = null;
        int? targetColumn = null;

        if (jsonPointer is not null && schemaInfo.DocumentId is not null)
        {
            TextDocument? schemaDoc = project.GetAdditionalDocument(schemaInfo.DocumentId);
            if (schemaDoc is not null)
            {
                SourceText? schemaText = await schemaDoc.GetTextAsync(cancellationToken).ConfigureAwait(false);
                if (schemaText is not null)
                {
                    (int Line, int Column)? pos = ResolveJsonPointerToPosition(schemaText.ToString(), jsonPointer);
                    if (pos.HasValue)
                    {
                        targetLine = pos.Value.Line;
                        targetColumn = pos.Value.Column;
                    }
                }
            }
        }

        string displayName = jsonPointer is not null
            ? $"{Path.GetFileName(schemaInfo.FilePath)}#{jsonPointer}"
            : Path.GetFileName(schemaInfo.FilePath);

        string lineHint = targetLine.HasValue ? $" (line {targetLine.Value + 1})" : string.Empty;
        string title = $"{titlePrefix}: {displayName}{lineHint}";

        return new NavigateToSchemaAction(
            title,
            schemaInfo.FilePath,
            schemaInfo.DocumentId,
            targetLine,
            targetColumn);
    }

    /// <summary>
    /// If the type is <c>IJsonElement&lt;T&gt;</c> or <c>IMutableJsonElement&lt;T&gt;</c>,
    /// returns the <c>T</c> type argument. Otherwise returns the original type.
    /// </summary>
    private static INamedTypeSymbol UnwrapJsonElementInterface(INamedTypeSymbol namedType)
    {
        if (namedType.IsGenericType && namedType.TypeArguments.Length == 1)
        {
            string typeName = namedType.OriginalDefinition.Name;
            string? ns = namedType.OriginalDefinition.ContainingNamespace?.ToDisplayString();

            if (ns == "Corvus.Text.Json.Internal" &&
                (typeName == "IJsonElement" || typeName == "IMutableJsonElement"))
            {
                if (namedType.TypeArguments[0] is INamedTypeSymbol concreteType)
                {
                    return concreteType;
                }
            }
        }

        // Also check if the type implements IJsonElement<T> but doesn't have the attribute itself.
        // In the CRTP pattern (T : struct, IJsonElement<T>), the type IS T, so this is already
        // handled by the existing ContainingType walk in FindSchemaInfo.
        return namedType;
    }

    /// <summary>
    /// Reads the <c>SchemaLocation</c> const from the type's nested <c>JsonSchema</c> static class.
    /// </summary>
    private static string? GetSchemaLocation(INamedTypeSymbol typeSymbol)
    {
        // Look for a nested type called "JsonSchema" containing a const "SchemaLocation".
        foreach (INamedTypeSymbol nestedType in typeSymbol.GetTypeMembers())
        {
            if (nestedType.Name == JsonSchemaNestedClassName)
            {
                foreach (ISymbol member in nestedType.GetMembers(SchemaLocationFieldName))
                {
                    if (member is IFieldSymbol { IsConst: true, ConstantValue: string location } &&
                        !string.IsNullOrEmpty(location))
                    {
                        return location;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Extracts the JSON pointer fragment from a SchemaLocation value.
    /// Returns <c>null</c> if no pointer is present.
    /// </summary>
    private static string? ExtractJsonPointer(string? schemaLocation)
    {
        if (schemaLocation is null)
        {
            return null;
        }

        int hashIndex = schemaLocation.IndexOf('#');
        if (hashIndex >= 0 && hashIndex < schemaLocation.Length - 1)
        {
            return schemaLocation.Substring(hashIndex + 1);
        }

        return null;
    }

    /// <summary>
    /// Extracts the base URL (everything before <c>#</c>) from a SchemaLocation value.
    /// Returns <c>null</c> if the value doesn't look like a URL.
    /// </summary>
    private static string? ExtractBaseUrl(string schemaLocation)
    {
        int hashIndex = schemaLocation.IndexOf('#');
        string baseUrl = hashIndex >= 0
            ? schemaLocation.Substring(0, hashIndex)
            : schemaLocation;

        // Only treat as a URL if it contains "://"
        if (baseUrl.Contains("://"))
        {
            return baseUrl;
        }

        return null;
    }

    /// <summary>
    /// Finds an AdditionalDocument whose JSON content contains a <c>$id</c> matching
    /// the given URL.
    /// </summary>
    private static async Task<SchemaInfo?> FindSchemaByIdAsync(
        string idUrl,
        Project project,
        CancellationToken cancellationToken)
    {
        const string idPattern = "\"$id\"";

        foreach (TextDocument doc in project.AdditionalDocuments)
        {
            if (doc.FilePath is null)
            {
                continue;
            }

            // Only check .json files.
            if (!doc.FilePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            SourceText? text = await doc.GetTextAsync(cancellationToken).ConfigureAwait(false);
            if (text is null)
            {
                continue;
            }

            string content = text.ToString();

            // Quick check: does the file contain "$id" at all?
            if (content.IndexOf(idPattern, StringComparison.Ordinal) < 0)
            {
                continue;
            }

            // Look for "$id": "<idUrl>" (with flexible whitespace).
            if (content.Contains(idUrl))
            {
                return new SchemaInfo(doc.FilePath, doc.Id);
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a PascalCase property name to camelCase for JSON property name matching.
    /// </summary>
    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static SchemaInfo? FindSchemaInfo(
        INamedTypeSymbol typeSymbol,
        Project project,
        CancellationToken cancellationToken)
    {
        // Walk up through containing types to find the one with the attribute.
        INamedTypeSymbol? current = typeSymbol;
        while (current is not null)
        {
            string? schemaPath = GetSchemaPathFromAttribute(current);
            if (schemaPath is not null)
            {
                string? resolvedPath = ResolveSchemaPath(current, schemaPath, project);
                if (resolvedPath is not null)
                {
                    // Find matching additional document.
                    DocumentId? docId = FindAdditionalDocument(project, resolvedPath);
                    return new SchemaInfo(resolvedPath, docId);
                }
            }

            current = current.ContainingType;
        }

        return null;
    }

    private static string? GetSchemaPathFromAttribute(INamedTypeSymbol typeSymbol)
    {
        foreach (AttributeData attribute in typeSymbol.GetAttributes())
        {
            string? attrName = attribute.AttributeClass?.Name;
            if (attrName == GeneratorAttributeName || attrName == GeneratorAttributeShortName)
            {
                if (attribute.ConstructorArguments.Length > 0 &&
                    attribute.ConstructorArguments[0].Value is string path)
                {
                    return path;
                }
            }
        }

        return null;
    }

    private static string? ResolveSchemaPath(
        INamedTypeSymbol typeSymbol,
        string schemaRelativePath,
        Project project)
    {
        string normalizedRelative = schemaRelativePath.Replace('\\', '/');

        // Resolve relative to the source file containing the attribute declaration.
        foreach (SyntaxReference syntaxRef in typeSymbol.DeclaringSyntaxReferences)
        {
            string? sourceFilePath = syntaxRef.SyntaxTree.FilePath;
            if (string.IsNullOrEmpty(sourceFilePath))
            {
                continue;
            }

            string? sourceDir = Path.GetDirectoryName(sourceFilePath);
            if (sourceDir is null)
            {
                continue;
            }

            string resolved = Path.GetFullPath(Path.Combine(sourceDir, schemaRelativePath));

            // Verify the resolved path matches an AdditionalDocument.
            foreach (TextDocument doc in project.AdditionalDocuments)
            {
                if (doc.FilePath is not null &&
                    Path.GetFullPath(doc.FilePath).Equals(resolved, StringComparison.OrdinalIgnoreCase))
                {
                    return resolved;
                }
            }
        }

        // Fallback: search additional documents whose path ends with the schema path.
        foreach (TextDocument doc in project.AdditionalDocuments)
        {
            if (doc.FilePath is null)
            {
                continue;
            }

            string docPathNormalized = doc.FilePath.Replace('\\', '/');
            if (docPathNormalized.EndsWith(normalizedRelative, StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileName(doc.FilePath).Equals(
                    Path.GetFileName(schemaRelativePath), StringComparison.OrdinalIgnoreCase))
            {
                return doc.FilePath;
            }
        }

        return null;
    }

    private static DocumentId? FindAdditionalDocument(Project project, string filePath)
    {
        string normalizedPath = Path.GetFullPath(filePath);

        foreach (TextDocument doc in project.AdditionalDocuments)
        {
            if (doc.FilePath is not null &&
                Path.GetFullPath(doc.FilePath).Equals(normalizedPath, StringComparison.OrdinalIgnoreCase))
            {
                return doc.Id;
            }
        }

        return null;
    }

    private readonly struct SchemaInfo
    {
        public SchemaInfo(string filePath, DocumentId? documentId)
        {
            FilePath = filePath;
            DocumentId = documentId;
        }

        public string FilePath { get; }

        public DocumentId? DocumentId { get; }
    }

    /// <summary>
    /// A code action that navigates to the JSON Schema file. In Visual Studio,
    /// this opens the file in the editor. In other environments, the action title
    /// shows the schema path for reference.
    /// </summary>
    private sealed class NavigateToSchemaAction : CodeAction
    {
        private readonly string title;
        private readonly string filePath;
        private readonly DocumentId? documentId;
        private readonly int? targetLine;
        private readonly int? targetColumn;

        public NavigateToSchemaAction(string title, string filePath, DocumentId? documentId, int? targetLine, int? targetColumn)
        {
            this.title = title;
            this.filePath = filePath;
            this.documentId = documentId;
            this.targetLine = targetLine;
            this.targetColumn = targetColumn;
        }

        /// <inheritdoc/>
        public override string Title => title;

        /// <inheritdoc/>
        public override string? EquivalenceKey => "CTJ-NAV";

        /// <inheritdoc/>
        protected override Task<IEnumerable<CodeActionOperation>> ComputeOperationsAsync(
            CancellationToken cancellationToken)
        {
            var operations = new List<CodeActionOperation>
            {
                new OpenSchemaFileOperation(filePath, documentId, targetLine, targetColumn),
            };

            return Task.FromResult<IEnumerable<CodeActionOperation>>(operations);
        }
    }

    /// <summary>
    /// A <see cref="CodeActionOperation"/> that attempts to open a schema file
    /// in the host workspace. In Visual Studio this opens the document in the editor.
    /// If a JSON pointer is provided, the operation attempts to navigate to the
    /// corresponding line within the file.
    /// </summary>
    private sealed class OpenSchemaFileOperation : CodeActionOperation
    {
        private readonly string filePath;
        private readonly DocumentId? documentId;
        private readonly int? targetLine;
        private readonly int? targetColumn;

        public OpenSchemaFileOperation(string filePath, DocumentId? documentId, int? targetLine, int? targetColumn)
        {
            this.filePath = filePath;
            this.documentId = documentId;
            this.targetLine = targetLine;
            this.targetColumn = targetColumn;
        }

        /// <inheritdoc/>
        public override string Title => $"Open {Path.GetFileName(filePath)}";

        /// <inheritdoc/>
        public override void Apply(Workspace workspace, CancellationToken cancellationToken)
        {
            // Try DTE-based open + navigate first.
            // This avoids timing issues with workspace.OpenDocument not immediately
            // making the document active. DTE.ItemOperations.OpenFile is synchronous.
            if (TryOpenAndNavigateViaDte(workspace))
            {
                return;
            }

            // Fall back to Roslyn workspace API (no line navigation available).
            if (documentId is not null)
            {
                try
                {
                    workspace.OpenDocument(documentId);
                }
                catch (NotSupportedException)
                {
                    // Not all workspace implementations support OpenDocument.
                }
                catch (InvalidOperationException)
                {
                    // Document not found in the solution.
                }
            }
        }

        /// <summary>
        /// Attempts to open the schema file and navigate to the target line using the
        /// Visual Studio DTE automation model. Uses reflection to avoid hard dependencies
        /// on VS assemblies. Returns <c>false</c> if DTE is not available.
        /// </summary>
        private bool TryOpenAndNavigateViaDte(Workspace workspace)
        {
            try
            {
                // Try to find DTE types.
                Type? dteType = FindTypeInLoadedAssemblies("EnvDTE.DTE");

                // The service is registered under EnvDTE.DTE (the base type).
                // GetGlobalService(typeof(DTE)) returns a DTE2 instance.
                if (dteType is null)
                {
                    return false;
                }

                object? dte = GetDteViaMethods(workspace, dteType);
                if (dte is null)
                {
                    return false;
                }

                // Use DTE.ItemOperations.OpenFile(filePath, viewKind).
                // COM objects are __ComObject — normal GetProperty/GetValue
                // doesn't work. Use InvokeMember which dispatches through IDispatch.
                const System.Reflection.BindingFlags comGet =
                    System.Reflection.BindingFlags.GetProperty;
                const System.Reflection.BindingFlags comInvoke =
                    System.Reflection.BindingFlags.InvokeMethod;

                object? itemOperations = dte.GetType().InvokeMember(
                    "ItemOperations", comGet, null, dte, null);

                if (itemOperations is null)
                {
                    return false;
                }

                const string vsViewKindTextView = "{7651A703-06E5-11D1-8EBD-00A0C90F26EA}";

                itemOperations.GetType().InvokeMember(
                    "OpenFile", comInvoke, null, itemOperations,
                    new object[] { filePath, vsViewKindTextView });

                // Navigate to the target position.
                if (targetLine.HasValue)
                {
                    object? activeDoc = dte.GetType().InvokeMember(
                        "ActiveDocument", comGet, null, dte, null);

                    if (activeDoc is not null)
                    {
                        object? selection = activeDoc.GetType().InvokeMember(
                            "Selection", comGet, null, activeDoc, null);

                        if (selection is not null)
                        {
                            // DTE lines and offsets are 1-based; ours are 0-based.
                            int dteLine = targetLine.Value + 1;
                            int dteOffset = (targetColumn ?? 0) + 1;
                            selection.GetType().InvokeMember(
                                "MoveToLineAndOffset", comInvoke, null, selection,
                                new object[] { dteLine, dteOffset, false });
                        }
                    }
                }

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>
        /// Tries multiple strategies to obtain the VS DTE automation object.
        /// </summary>
        private static object? GetDteViaMethods(Workspace workspace, Type dteType)
        {
            // Strategy 1: Package.GetGlobalService(typeof(DTE))
            Type? packageType = FindTypeInLoadedAssemblies("Microsoft.VisualStudio.Shell.Package");

            if (packageType is not null)
            {
                System.Reflection.MethodInfo? getGlobalService = packageType.GetMethod(
                    "GetGlobalService",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static,
                    null,
                    new[] { typeof(Type) },
                    null);

                if (getGlobalService is not null)
                {
                    object? dte = getGlobalService.Invoke(null, new object[] { dteType });

                    if (dte is not null)
                    {
                        return dte;
                    }
                }
            }

            // Strategy 2: ServiceProvider.GlobalProvider.GetService(typeof(DTE))
            Type? spType = FindTypeInLoadedAssemblies("Microsoft.VisualStudio.Shell.ServiceProvider");

            if (spType is not null)
            {
                object? globalProvider = spType.GetProperty(
                    "GlobalProvider",
                    System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.GetValue(null);

                if (globalProvider is IServiceProvider sp)
                {
                    object? dte = sp.GetService(dteType);

                    if (dte is not null)
                    {
                        return dte;
                    }
                }
            }

            // Strategy 3: Workspace's internal service provider.
            object? workspaceSp = workspace.GetType().GetProperty(
                "ServiceProvider",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.Public)
                ?.GetValue(workspace);

            if (workspaceSp is not null)
            {
                object? dte = workspaceSp.GetType()
                    .GetMethod("GetService", new[] { typeof(Type) })
                    ?.Invoke(workspaceSp, new object[] { dteType });

                if (dte is not null)
                {
                    return dte;
                }
            }

            return null;
        }

        private static Type? FindTypeInLoadedAssemblies(string fullName)
        {
            // First try Type.GetType with assembly-qualified names, which can
            // trigger assembly loading (unlike scanning already-loaded assemblies).
            string? assemblyQualified = fullName switch
            {
                "EnvDTE.DTE" => "EnvDTE.DTE, EnvDTE, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                "EnvDTE80.DTE2" => "EnvDTE80.DTE2, EnvDTE80, Version=8.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a",
                _ => null,
            };

            if (assemblyQualified is not null)
            {
                var t = Type.GetType(assemblyQualified, throwOnError: false);
                if (t is not null)
                {
                    return t;
                }
            }

            // Fall back to scanning loaded assemblies.
            foreach (System.Reflection.Assembly asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type? t = asm.GetType(fullName);
                if (t is not null)
                {
                    return t;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Resolves a JSON pointer (e.g., <c>/properties/name</c>) to a line and column
    /// position within the given schema text. Returns <c>null</c> if the pointer
    /// cannot be resolved.
    /// </summary>
    /// <remarks>
    /// Uses <see cref="Utf8JsonReader"/> to walk the JSON structure, following
    /// pointer segments through objects and arrays. This approach handles all
    /// JSON edge cases (string escaping, single-line files, nested structures)
    /// correctly. The returned position points to the opening quote of the
    /// matched property key, or the start of the matched array element.
    /// </remarks>
    internal static (int Line, int Column)? ResolveJsonPointerToPosition(string schemaText, string jsonPointer)
    {
        if (string.IsNullOrEmpty(jsonPointer) || jsonPointer == "/")
        {
            return null;
        }

        // Strip leading # (URI fragment syntax).
        string pointer = jsonPointer;
        if (pointer[0] == '#')
        {
            pointer = pointer.Substring(1);
        }

        if (string.IsNullOrEmpty(pointer))
        {
            return null;
        }

        // Split into segments and decode JSON Pointer escapes (~1 → /, ~0 → ~).
        string[] segments = pointer.TrimStart('/').Split('/');
        if (segments.Length == 0)
        {
            return null;
        }

        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = segments[i].Replace("~1", "/").Replace("~0", "~");
        }

        byte[] utf8Bytes = Encoding.UTF8.GetBytes(schemaText);

        try
        {
            var reader = new Utf8JsonReader(
                utf8Bytes,
                new JsonReaderOptions { CommentHandling = JsonCommentHandling.Skip });

            if (!reader.Read())
            {
                return null;
            }

            long matchedByteOffset = 0;

            for (int s = 0; s < segments.Length; s++)
            {
                string segment = segments[s];

                if (reader.TokenType == JsonTokenType.StartObject)
                {
                    reader.Read();
                    bool found = false;

                    while (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        if (reader.ValueTextEquals(segment))
                        {
                            // Record the property key position.
                            matchedByteOffset = reader.TokenStartIndex;

                            // Advance to the property value for further navigation.
                            reader.Read();
                            found = true;
                            break;
                        }

                        // Skip this property's name and value.
                        reader.Skip();
                        reader.Read();
                    }

                    if (!found)
                    {
                        return null;
                    }
                }
                else if (reader.TokenType == JsonTokenType.StartArray)
                {
                    if (!int.TryParse(segment, out int targetArrayIndex))
                    {
                        return null;
                    }

                    // Move to the first element.
                    reader.Read();

                    int currentIndex = 0;
                    while (currentIndex < targetArrayIndex && reader.TokenType != JsonTokenType.EndArray)
                    {
                        currentIndex++;
                        reader.Skip();
                        reader.Read();
                    }

                    if (currentIndex != targetArrayIndex || reader.TokenType == JsonTokenType.EndArray)
                    {
                        return null;
                    }

                    // Record the element position.
                    matchedByteOffset = reader.TokenStartIndex;
                }
                else
                {
                    // Can't navigate further into a primitive value.
                    return null;
                }
            }

            return ByteOffsetToLineColumn(utf8Bytes, matchedByteOffset);
        }
        catch (JsonException)
        {
            // Malformed JSON — can't resolve the pointer.
            return null;
        }
    }

    /// <summary>
    /// Converts a byte offset within a UTF-8 byte array to a 0-based line and column.
    /// </summary>
    private static (int Line, int Column) ByteOffsetToLineColumn(byte[] utf8Bytes, long byteOffset)
    {
        int line = 0;
        long lineStart = 0;

        for (long i = 0; i < byteOffset && i < utf8Bytes.Length; i++)
        {
            if (utf8Bytes[i] == (byte)'\n')
            {
                line++;
                lineStart = i + 1;
            }
        }

        return (line, (int)(byteOffset - lineStart));
    }
}