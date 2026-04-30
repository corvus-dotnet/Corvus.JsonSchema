// <copyright file="SchemaNavigationRefactoringTests.cs" company="Endjin Limited">
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
using System.Linq;
using System.Threading.Tasks;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeRefactorings;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Text;

using Xunit;

namespace Corvus.Text.Json.Analyzers.Tests;

/// <summary>
/// Tests for CTJ-NAV: Navigate to JSON Schema source.
/// </summary>
public class SchemaNavigationRefactoringTests
{
    private const string SimpleSchemaJson = @"{
  ""$schema"": ""https://json-schema.org/draft/2020-12/schema"",
  ""type"": ""object"",
  ""properties"": {
    ""name"": { ""type"": ""string"" },
    ""address"": {
      ""type"": ""object"",
      ""properties"": {
        ""city"": { ""type"": ""string"" },
        ""zipCode"": { ""type"": ""string"" }
      }
    }
  },
  ""allOf"": [
    {
      ""type"": ""object"",
      ""properties"": {
        ""extra"": { ""type"": ""string"" }
      }
    }
  ]
}";

    private const string AttributeAndInterfaceStubs = @"
using System;

namespace Corvus.Text.Json
{
    [AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
    internal sealed class JsonSchemaTypeGeneratorAttribute : Attribute
    {
        public JsonSchemaTypeGeneratorAttribute(string schemaPath) { }
    }
}

namespace Corvus.Text.Json.Internal
{
    public interface IJsonElement { }

    public interface IJsonElement<T> : IJsonElement
        where T : struct, IJsonElement<T>
    {
    }

    public interface IMutableJsonElement<T> : IJsonElement<T>
        where T : struct, IJsonElement<T>
    {
    }
}
";

    [Fact]
    public async Task TypeDeclarationWithSchemaAttribute_OffersNavigation()
    {
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
    }

    class Test
    {
        void M()
        {
            Widget w = default;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "Widget",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("Go to schema", actions[0].Title);
    }

    [Fact]
    public async Task TypeWithoutSchemaAttribute_DoesNotOfferNavigation()
    {
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    public readonly struct PlainType { }

    class Test
    {
        void M()
        {
            PlainType p = default;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "PlainType",
            useLastIdentifier: true);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task TypeWithSchemaLocation_ShowsPointerInTitle()
    {
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
        public readonly partial struct NameEntity : Corvus.Text.Json.Internal.IJsonElement<NameEntity>
        {
            public static partial class JsonSchema
            {
                public const string SchemaLocation = ""widget.json#/properties/name"";
            }
        }
    }

    class Test
    {
        void M()
        {
            Widget.NameEntity n = default;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "NameEntity",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("#/properties/name", actions[0].Title);
    }

    [Fact]
    public async Task TopLevelType_NoPointerInTitle()
    {
        // Top-level types have SchemaLocation like "widget.json" with no pointer fragment.
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
        public static partial class JsonSchema
        {
            public const string SchemaLocation = ""widget.json"";
        }
    }

    class Test
    {
        void M()
        {
            Widget w = default;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "Widget",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("widget.json", actions[0].Title);
        Assert.DoesNotContain("#", actions[0].Title);
    }

    [Theory]
    [InlineData("/properties/name", 4)]
    [InlineData("/properties/address", 5)]
    [InlineData("/properties/address/properties/city", 8)]
    [InlineData("/properties/address/properties/zipCode", 9)]
    public void ResolveJsonPointer_FindsCorrectLine(string pointer, int expectedLine)
    {
        (int Line, int Column)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(SimpleSchemaJson, pointer);
        Assert.NotNull(pos);
        Assert.Equal(expectedLine, pos!.Value.Line);
    }

    [Fact]
    public void ResolveJsonPointer_FindsCorrectColumn()
    {
        // In SimpleSchemaJson, "name" on line 4 is at column 4 (after 4 spaces of indent).
        (int Line, int Column)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(SimpleSchemaJson, "/properties/name");
        Assert.NotNull(pos);
        Assert.Equal(4, pos!.Value.Column);
    }

    [Fact]
    public void ResolveJsonPointer_SingleLineJson_FindsCorrectColumn()
    {
        const string singleLine = @"{""properties"":{""name"":{""type"":""string""},""age"":{""type"":""integer""}}}";
        (int Line, int Column)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(singleLine, "/properties/name");
        Assert.NotNull(pos);
        Assert.Equal(0, pos!.Value.Line);
        Assert.Equal(15, pos!.Value.Column); // position of "name" in the single line
    }

    [Fact]
    public void ResolveJsonPointer_NullPointer_ReturnsNull()
    {
        (int, int)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(SimpleSchemaJson, null!);
        Assert.Null(pos);
    }

    [Fact]
    public void ResolveJsonPointer_EmptyPointer_ReturnsNull()
    {
        (int, int)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(SimpleSchemaJson, "");
        Assert.Null(pos);
    }

    [Fact]
    public void ResolveJsonPointer_RootSlash_ReturnsNull()
    {
        (int, int)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(SimpleSchemaJson, "/");
        Assert.Null(pos);
    }

    [Fact]
    public void ResolveJsonPointer_NonExistentProperty_ReturnsNull()
    {
        (int, int)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(SimpleSchemaJson, "/properties/nonExistent");
        Assert.Null(pos);
    }

    [Fact]
    public async Task IJsonElementVariable_OffersNavigation()
    {
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
    }

    class Test
    {
        void M()
        {
            Corvus.Text.Json.Internal.IJsonElement<Widget> w = default(Widget);
        }
    }
}";

        // The cursor is on IJsonElement — the type should unwrap to Widget.
        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "IJsonElement",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("Go to schema", actions[0].Title);
    }

    [Fact]
    public async Task IMutableJsonElementVariable_OffersNavigation()
    {
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IMutableJsonElement<Widget>
    {
    }

    class Test
    {
        void M()
        {
            Corvus.Text.Json.Internal.IMutableJsonElement<Widget> w = default(Widget);
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "IMutableJsonElement",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("Go to schema", actions[0].Title);
    }

    [Fact]
    public async Task NonGenericIJsonElement_DoesNotOfferNavigation()
    {
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    class Test
    {
        void M(Corvus.Text.Json.Internal.IJsonElement e)
        {
            _ = e;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "IJsonElement",
            useLastIdentifier: true);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task IJsonElementParameter_OffersNavigation()
    {
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
    }

    class Test
    {
        void M(Corvus.Text.Json.Internal.IJsonElement<Widget> param)
        {
            _ = param;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "IJsonElement",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("Go to schema", actions[0].Title);
    }

    [Fact]
    public async Task PropertyAccessOnGlobalType_FallsBackToContainingTypeSchema()
    {
        // When a property returns a project-global type (like JsonString) that has
        // no schema attribute, fall back to the containing type's schema and append
        // /properties/{jsonPropName} to the pointer.
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
        public static partial class JsonSchema
        {
            public const string SchemaLocation = ""widget.json"";
        }

        public JsonString Name => default;
    }

    public readonly struct JsonString { }

    class Test
    {
        void M()
        {
            Widget w = default;
            _ = w.Name;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "Name",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("#/properties/name", actions[0].Title);
    }

    [Fact]
    public async Task PropertyAccessOnNestedType_FallsBackWithPointerSuffix()
    {
        // When the containing type has a SchemaLocation with a pointer fragment,
        // the property suffix is appended to it.
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
        public readonly partial struct AddressEntity : Corvus.Text.Json.Internal.IJsonElement<AddressEntity>
        {
            public static partial class JsonSchema
            {
                public const string SchemaLocation = ""widget.json#/properties/address"";
            }

            public JsonString City => default;
        }
    }

    public readonly struct JsonString { }

    class Test
    {
        void M()
        {
            Widget.AddressEntity addr = default;
            _ = addr.City;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "City",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("#/properties/address/properties/city", actions[0].Title);
    }

    [Fact]
    public async Task PropertyAccessOnSchemaGeneratedType_OffersBothTypeAndPropertyActions()
    {
        // When a property's type IS a schema-generated entity (e.g., Widget.Address
        // returning AddressEntity), offer two actions: one to navigate to the type's
        // own schema, and one to navigate to the property declaration on the parent.
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
        public static partial class JsonSchema
        {
            public const string SchemaLocation = ""widget.json"";
        }

        public readonly partial struct AddressEntity : Corvus.Text.Json.Internal.IJsonElement<AddressEntity>
        {
            public static partial class JsonSchema
            {
                public const string SchemaLocation = ""widget.json#/properties/address"";
            }
        }

        public AddressEntity Address => default;
    }

    class Test
    {
        void M()
        {
            Widget w = default;
            _ = w.Address;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "Address",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        // Should get two actions: type navigation + property declaration.
        Assert.Equal(2, actions.Count);
        Assert.Contains("Go to schema type", actions[0].Title);
        Assert.Contains("Go to property declaration", actions[1].Title);
        Assert.Contains("#/properties/address", actions[1].Title);
    }

    [Fact]
    public async Task VariableDeclarator_OffersNavigation()
    {
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
    }

    class Test
    {
        void M()
        {
            Widget myWidget = default;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForNode(
            code,
            findNode: root => root.DescendantNodes()
                .OfType<VariableDeclaratorSyntax>()
                .First(v => v.Identifier.Text == "myWidget"),
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("Go to schema", actions[0].Title);
    }

    [Fact]
    public async Task ParameterSyntax_OffersNavigation()
    {
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
    }

    class Test
    {
        void M(Widget w)
        {
            _ = w;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForNode(
            code,
            findNode: root => root.DescendantNodes()
                .OfType<ParameterSyntax>()
                .First(p => p.Identifier.Text == "w"),
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("Go to schema", actions[0].Title);
    }

    [Fact]
    public async Task FieldDeclaration_OffersNavigation()
    {
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
    }

    class Test
    {
        private Widget _widget;
        void M() { _ = _widget; }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "_widget",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("Go to schema", actions[0].Title);
    }

    [Fact]
    public async Task FieldType_OffersNavigation()
    {
        // Cursor on the type name in a field declaration.
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
    }

    class Test
    {
        private Widget _widget;
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "Widget",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("Go to schema", actions[0].Title);
    }

    [Fact]
    public async Task MethodReturnType_OffersNavigation()
    {
        // Cursor on a method name whose return type is schema-generated.
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
    }

    class Test
    {
        Widget GetWidget() => default;
        void M()
        {
            _ = GetWidget();
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "GetWidget",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("Go to schema", actions[0].Title);
    }

    [Fact]
    public async Task GenericTypeArgument_CursorOnWidgetInGeneric_OffersNavigation()
    {
        // Cursor on Widget inside a generic type argument like List<Widget>.
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
    }

    class Test
    {
        void M()
        {
            System.Collections.Generic.List<Widget> list = null;
        }
    }
}";

        // Cursor specifically on the Widget type argument.
        List<CodeAction> actions = await GetRefactoringsForNode(
            code,
            findNode: root => root.DescendantNodes()
                .OfType<IdentifierNameSyntax>()
                .Last(id => id.Identifier.Text == "Widget"),
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("Go to schema", actions[0].Title);
    }

    [Fact]
    public async Task GenericTypeArgument_CursorOnListNotWidget_DoesNotOfferNavigation()
    {
        // Cursor on List (not Widget) — List is not a schema type, no action.
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
    }

    class Test
    {
        void M()
        {
            System.Collections.Generic.List<Widget> list = null;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "List",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task LocalVariableUsage_OffersNavigation()
    {
        // Cursor on a local variable usage (not its declaration).
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
    }

    class Test
    {
        void M()
        {
            Widget w = default;
            Widget w2 = w;
        }
    }
}";

        // The last 'w' is a usage, not a declaration.
        List<CodeAction> actions = await GetRefactoringsForNode(
            code,
            findNode: root =>
            {
                var identifiers = root.DescendantNodes()
                    .OfType<IdentifierNameSyntax>()
                    .Where(id => id.Identifier.Text == "w")
                    .ToList();

                // The last "w" is the usage on the right-hand side of w2 = w.
                return identifiers.Last();
            },
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("Go to schema", actions[0].Title);
    }

    [Fact]
    public async Task CastExpression_TypeName_OffersNavigation()
    {
        // Cursor on the type name in a cast expression.
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
    }

    class Test
    {
        void M()
        {
            object o = default(Widget);
            Widget w = (Widget)o;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "Widget",
            useLastIdentifier: true,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: SimpleSchemaJson);

        Assert.NotEmpty(actions);
        Assert.Contains("Go to schema", actions[0].Title);
    }

    [Fact]
    public async Task CliGeneratedType_NoAttribute_NoSchemaFile_ReturnsNoActions()
    {
        // CLI-generated types have SchemaLocation but no [JsonSchemaTypeGenerator]
        // attribute and the schema file is not in AdditionalFiles.
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
        public static partial class JsonSchema
        {
            public const string SchemaLocation = ""https://example.com/widget"";
        }
    }

    class Test
    {
        void M()
        {
            Widget w = default;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "Widget",
            useLastIdentifier: false);

        Assert.Empty(actions);
    }

    [Fact]
    public async Task CliGeneratedType_NoAttribute_SchemaInAdditionalFiles_ResolvesViaId()
    {
        // CLI-generated type with no attribute, but the schema file IS in
        // AdditionalFiles and matches via $id lookup.
        const string schemaWithId = @"{
  ""$id"": ""https://example.com/widget"",
  ""type"": ""object"",
  ""properties"": {
    ""name"": { ""type"": ""string"" }
  }
}";

        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
        public static partial class JsonSchema
        {
            public const string SchemaLocation = ""https://example.com/widget#/properties/name"";
        }
    }

    class Test
    {
        void M()
        {
            Widget w = default;
        }
    }
}";

        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "Widget",
            useLastIdentifier: false,
            additionalFilePath: "Schemas/widget.json",
            additionalFileContent: schemaWithId);

        Assert.NotEmpty(actions);
        Assert.Contains("#/properties/name", actions[0].Title);
    }

    [Fact]
    public async Task TypeWithAttribute_SchemaFileMissing_ReturnsNoActions()
    {
        // Attribute points to a file path, but no matching AdditionalFile exists.
        // No action should be offered because there is nothing to navigate to.
        const string code = AttributeAndInterfaceStubs + @"
namespace TestApp
{
    [Corvus.Text.Json.JsonSchemaTypeGenerator(""Schemas/widget.json"")]
    public readonly partial struct Widget : Corvus.Text.Json.Internal.IJsonElement<Widget>
    {
        public static partial class JsonSchema
        {
            public const string SchemaLocation = ""widget.json#/properties/name"";
        }
    }

    class Test
    {
        void M()
        {
            Widget w = default;
        }
    }
}";

        // No additional file provided — schema is missing from project.
        List<CodeAction> actions = await GetRefactoringsForIdentifier(
            code,
            "Widget",
            useLastIdentifier: false);

        Assert.Empty(actions);
    }

    [Fact]
    public void ResolveJsonPointer_MalformedJson_ReturnsNull()
    {
        // Malformed JSON should not throw — just return null.
        const string malformed = @"{ ""properties"": { not valid json";
        (int, int)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(
            malformed, "/properties/name");
        Assert.Null(pos);
    }

    [Fact]
    public void ResolveJsonPointer_PartiallyResolvablePointer_ReturnsNull()
    {
        // First segment resolves, second doesn't.
        (int, int)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(
            SimpleSchemaJson, "/properties/name/nonexistent");
        Assert.Null(pos);
    }

    [Fact]
    public void ResolveJsonPointer_EscapedTilde_Resolves()
    {
        // ~0 decodes to ~ in JSON Pointer.
        const string schemaWithTilde = @"{
  ""type"": ""object"",
  ""properties"": {
    ""has~tilde"": { ""type"": ""string"" }
  }
}";
        (int Line, int Column)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(
            schemaWithTilde, "/properties/has~0tilde");
        Assert.NotNull(pos);
        Assert.Equal(3, pos!.Value.Line);
    }

    [Fact]
    public void ResolveJsonPointer_EscapedSlash_Resolves()
    {
        // ~1 decodes to / in JSON Pointer.
        const string schemaWithSlash = @"{
  ""type"": ""object"",
  ""properties"": {
    ""has/slash"": { ""type"": ""string"" }
  }
}";
        (int Line, int Column)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(
            schemaWithSlash, "/properties/has~1slash");
        Assert.NotNull(pos);
        Assert.Equal(3, pos!.Value.Line);
    }

    [Fact]
    public void ResolveJsonPointer_ArrayIndex_Resolves()
    {
        (int Line, int Column)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(
            SimpleSchemaJson, "/allOf/0/properties/extra");
        Assert.NotNull(pos);
    }

    [Fact]
    public void ResolveJsonPointer_ArrayIndexOutOfBounds_ReturnsNull()
    {
        // allOf has only 1 element (index 0); index 5 is out of bounds.
        (int, int)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(
            SimpleSchemaJson, "/allOf/5");
        Assert.Null(pos);
    }

    [Fact]
    public void ResolveJsonPointer_FragmentWithHash_Resolves()
    {
        // JSON Pointer with leading # (URI fragment syntax) should still work.
        (int Line, int Column)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(
            SimpleSchemaJson, "#/properties/name");
        Assert.NotNull(pos);
        Assert.Equal(4, pos!.Value.Line);
    }

    [Fact]
    public void ResolveJsonPointer_PropertyInsideStringValue_NotConfused()
    {
        // Ensure the resolver doesn't match a property name that appears inside
        // a string value rather than as a key.
        const string schemaWithStringValue = @"{
  ""type"": ""object"",
  ""properties"": {
    ""description"": { ""type"": ""string"", ""default"": ""has properties inside"" },
    ""target"": { ""type"": ""number"" }
  }
}";
        (int Line, int Column)? pos = SchemaNavigationRefactoring.ResolveJsonPointerToPosition(
            schemaWithStringValue, "/properties/target");
        Assert.NotNull(pos);
        Assert.Equal(4, pos!.Value.Line);
    }

    private static async Task<List<CodeAction>> GetRefactoringsForIdentifier(
        string code,
        string identifierName,
        bool useLastIdentifier,
        string? additionalFilePath = null,
        string? additionalFileContent = null)
    {
        return await GetRefactoringsForNode(
            code,
            findNode: root =>
            {
                var candidates = root.DescendantNodes()
                    .Where(n =>
                        (n is IdentifierNameSyntax id && id.Identifier.Text == identifierName) ||
                        (n is GenericNameSyntax gn && gn.Identifier.Text == identifierName))
                    .ToList();

                Assert.NotEmpty(candidates);
                return useLastIdentifier ? candidates.Last() : candidates.First();
            },
            additionalFilePath,
            additionalFileContent);
    }

    private static async Task<List<CodeAction>> GetRefactoringsForNode(
        string code,
        Func<SyntaxNode, SyntaxNode> findNode,
        string? additionalFilePath = null,
        string? additionalFileContent = null)
    {
        var workspace = new Microsoft.CodeAnalysis.AdhocWorkspace();
        Project project = workspace.AddProject("TestProject", LanguageNames.CSharp);
        ImmutableArray<MetadataReference> refs = await ReferenceAssemblies.Net.Net80.ResolveAsync(
                LanguageNames.CSharp, default);
        project = project.AddMetadataReferences(refs);
        Document document = project.AddDocument("Test.cs", SourceText.From(code), filePath: "Test.cs");

        if (additionalFilePath is not null && additionalFileContent is not null)
        {
            project = document.Project.AddAdditionalDocument(
                System.IO.Path.GetFileName(additionalFilePath),
                SourceText.From(additionalFileContent),
                folders: null,
                filePath: additionalFilePath).Project;
            document = project.GetDocument(document.Id)!;
        }
        else
        {
            document = document.Project.GetDocument(document.Id)!;
        }

        SyntaxTree? tree = await document.GetSyntaxTreeAsync();
        SyntaxNode root = await tree!.GetRootAsync();

        SyntaxNode target = findNode(root!);

        var provider = new SchemaNavigationRefactoring();
        var actions = new List<CodeAction>();
        var context = new CodeRefactoringContext(
            document,
            target.Span,
            a => actions.Add(a),
            default);

        await provider.ComputeRefactoringsAsync(context);

        return actions;
    }
}