# Version History

## V5.0

V5 introduces the new **Corvus.Text.Json** engine — a brand new code generator and runtime library that uses the existing Corvus.Json.CodeGeneration framework, and builds on the patterns of `System.Text.Json` with pooled-memory parsing, mutable document building via `JsonWorkspace`, and familiar strongly-typed `readonly struct` wrappers generated from JSON Schema, with a streamlined API and substantial performance improvements.

What we now call the V4 Engine continues to be maintained in this library - and with the same command line tool - and provides our solution for a side-effect-free mutation model.
### New features

- **Pooled-memory parsing** — `ParsedJsonDocument<T>` backed by `ArrayPool<byte>`. Just 120 bytes per document vs 1,528 bytes for `JsonNode`.
- **Mutable documents** — `JsonDocumentBuilder<T>` and `JsonWorkspace` provide a builder pattern for creating and modifying JSON 'in place', with versioned elements that detect stale references.
- **Extended numeric types** — `BigNumber` for arbitrary-precision decimals, `BigInteger` for large integers, plus `Int128`, `UInt128`, and `Half`.
- **NodaTime integration** — First-class support for `LocalDate`, `OffsetDateTime`, `Period`, and other NodaTime types via `date`, `date-time`, `time`, and `duration` formats.
- **Pattern matching** — Type-safe `Match()` for `oneOf`/`anyOf` discriminated unions with exhaustive dispatch.
- **Roslyn source generator** — `[JsonSchemaTypeGenerator]` attribute generates types at build time with full IntelliSense.

### Breaking changes

- The `generatejsonschematypes` CLI tool now defaults to the V5 engine. To continue generating V4 types, you must specify `--engine V4`.
- V5 generated types use the `Corvus.Text.Json` namespace and require the `Corvus.Text.Json` NuGet package at runtime, rather than `Corvus.Json.ExtendedTypes`.
- The immutable functional API from V4 (`WithProperty()`, `SetItem()`, etc.) is replaced by the mutable builder pattern (`CreateBuilder()`, `SetProperty()`, etc.).

## V4.6 Updates

### Breaking changes

We had a long-standing bug with the pseudo-generic type pattern and `$dynamicRef` where you would get an extra level of indirection because the anchoring `$ref` was not reducible. This is now fixed, but any code using `$dynamicRef` will need to be simplified to remove the redundant indirection.

## V4.5 Updates

### Breaking changes (Language Provider Implementers only)

The `IKeywordValidationHandler` interface contains a number of APIs that are, with hindsight, specific to the particular implementation in the `CSharpLanguageProvider` implementation.

It forces you into a method definition / method call pattern, and also implementing the "child handler" pattern.

While the child-handler pattern is still likely useful, it may have a completely different implementation in other providers.

The method definition / method call pattern is very much an "implementer's choice" and should not be imposed on all future implementations.

This breaking change applies to *language provider implementers* only, and it splits `IKeywordValidationHandler` into `IKeywordValidationHandler` and `IMethodBasedKeywordValidationHandlerWithChildren`

If you have any existing code that depends on `IKeywordValidationHandler` you will need to update it to use `IMethodBasedKeywordValidationHandlerWithChildren` instead. This can be done with a global search and replace.

You will likely also need to use the new overload of the `TypeDeclaration` extension method `OrderedValidationHandlers<T>()` to retrieve the handlers using the correct interface.

For example, the CSharpLanguageProvider has been updated in three places to use the new overload, so we can access the handler via the new interface. Unsurprisingly, these are the three places that make use of the method based/child handler pattern. Here's one of those.

```csharp
    private static CodeGenerator AppendValidationHandlerSetup(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator.AppendUsingEvaluatedItems(typeDeclaration);
        generator.AppendUsingEvaluatedProperties(typeDeclaration);

        foreach (IMethodBasedKeywordValidationHandlerWithChildren handler in typeDeclaration.OrderedValidationHandlers<IMethodBasedKeywordValidationHandlerWithChildren>(generator.LanguageProvider))
        {
            handler.AppendValidationSetup(generator, typeDeclaration);
        }

        return generator;
    }
```

## V4.4.3 Updates

Added <CorvusJsonSchemaFallbackVocabulary>Corvus202012</CorvusJsonSchemaFallbackVocabulary> to support the `$corvusTypeName` keyword without requiring you to specify an explicit `$schema` for the vocabulary.

## V4.4 Updates

### Breaking changes

The property accessor mechanism now respects the default value of the property type.

If the schema for the property defines a `default` value, then the property accessor will return this value if and only if the value actual value is `ValueKind.Undefined`.

This does *not* affect equality or other comparisons for the object as a whole - if one value has the property *explicitly* set to the default value, and the other is relying on the "default" value, then the instances *will not* be equal.

If you use the `TryGetProperty()` mechanism to get the property value, this *will not* return the default from its `JsonObjectProperty.Value`. However, if you have access to a `JsonObjectProperty<TValue>` where the type of the property is known, then the `Value` will respect the default in the same way as the property itself.

### Fixes

In the latest version of the .NET SourceGenerator codebase, the behaviour when properties are missing has changed (we would suggest "is broken"). It no longer returns false and a null value for a missing property, but instead provides an empty string. This broke our default-value logic for settings - the net effect of which is that forcing format validation by default is accidentally switched off.

We have restored the previous behaviour, regardless of which version of the source generator infrastructure is in use.

## V4.3.17 Updates

Added netstandard2.1 packages for `Corvus.Json.ExtendedTypes` and `Corvus.Json.JsonReference` in order to support Unity builds.

## V4.3.16 Updates

### Use of IndexRange package is deprecated.

As of V1.1 of IndexRange, it now type-forwards to the recently shipped `Microsoft.Bcl.Memory` library. We will be removing the dependency on IndexRange in the V4.4 release cycle (some time after .NET 10 ships), and replacing it directly with `Microsoft.Bcl.Memory`. You should make the changes in your own code base if you have a direct dependency on `IndexRange` with this releasee in order to prepare for that change.

## V4.3.10 Updates

Added `<CorvusJsonSchemaUseImplicitOperatorString>true</CorvusJsonSchemaUseImplicitOperatorString>` to enable implicit conversion to `string`.

WARNING: Although this is very convenient for string-heavy code, it may cause unintended allocations if used without care.

## V4.3.0 Updates

### Type Accessibility using the Source Generator

The source generator now respects the accessibility of the model type.

For example

```csharp
[JsonSchemaTypeGenerator("../test.json#/$defs/FlimFlam")]
internal readonly partial struct FlimFlam
{
}
```

Any nested types will be generated with `public` accessibility.

Only `internal` and `public` are supported. The source generator will fail for an unsupported accessiblity declaration.

You can override the default accessibility for all generated types with a build property:

`<CorvusJsonSchemaDefaultAccessibility>Internal</CorvusJsonSchemaDefaultAccessibility>`

Note that you can still generate code that will not compile if you incorrectly mix-and-match `public` and `internal`. It is your responsibility to ensure that your types have compatible accessibility.

## V4.2.0 Updates

### Breaking change

The heuristic for naming (but not ordering) parameters to the `JsonObject.Create()` function has changed, to fix an issue with a parameter naming where properties differ only by case.

This could affect code that is using explicit named parameters with `Create()`, if your parameter changes its name.

If this causes a significant problem in your codebase, please raise an issue here and we will work with you to resolve the problem.

## V4.1.2 Updates

Added the `--addExplicitUsings` switch to the code generator (and a corresponding property to the `generator-config.json` schema). If `true`, then
the source generator will emit the standard global usings explicitly into the generated source files. You can then use the generated code in a project that does not have `<ImplicitUsings>enable</ImplicitUsings>`.

```csharp
using global::System;
using global::System.Collections.Generic;
using global::System.IO;
using global::System.Linq;
using global::System.Net.Http;
using global::System.Threading;
using global::System.Threading.Tasks;
```

## V4.1.1 Updates

## Help for people building analyzers and source generators with JSON Schema code generation

We have built a self-contained package called Corvus.Json.SourceGeneratorTools for people looking to build .NET Analyzers or Source Generators that take advantage of JSON Schema code generation.

See the [README](./Solutions/Corvus.Json.SourceGeneratorTools/README.md) for details.

## V4.1 Updates

### YAML support

We now support YAML documents for the CLI tool.

You can mix-and-match YAML and JSON documents in the same schema set, and the tool will generate code for either.

Your JSON schema can be embedded in a YAML document (such as a YAML-based OpenAPI or AsyncAPI document), and you can resolve internal references just as with a JSON document.

Add the `--yaml` command line option to enable YAML support, or set the `supportYaml: true` property in a generator config file

#### Example

*schema.yaml*
```yaml
type: array
prefixItems:
  - $ref: ./positiveInt32.yaml
  - type: string
  - type: string
    format: date-time
unevaluatedItems: false
```

*positiveInt32.yaml*
```yaml
type: integer
format: int32
minimum: 0
```

```
generatejsonschematypes --rootNamespace TestYaml --outputPath .\Model --yaml schema.yaml
```

## V4.0 Updates

There are a number of significant changes in this release

### Support for cross-vocabulary schema generation.

  So if you are upgrading a draft6 or draft7 schema set to 2020-12, for example, you can do it piecemeal and reference a schema with one dialect from a schema with another.

### Opt-in support for .NET nullable properties

  Where JSON Schema object properties are optional or nullable, use the `--optionalAsNullable` command line switch to emit nullable properties.

### Opt-in support for implicit conversions to `string` from JSON `string` types

If you have a JSON `string` type, we currently emit an `explicit` operator to convert to a .NET `string` (the counterpart of the `implicit` conversion operator *from* a .NET `string`).

We do this because conversion to string causes an allocation, and it is very easy to inadvertently do this when working with APIs that offer `string`-based overloads, in addition to e.g.
`ReadOnlySpan<char>` overloads. When passing an instance of the generated type directly to the API, the implicit conversion would kick in, allocating a string, with no warning that this
is what you have done. In a high-performance/low-allocation scenario this would be undesirable, and you would prefer to use the `GetValue()` method on the instance,
and pass the `ReadOnlySpan<char>` provided to the callback for that method.

However, sometimes you just want the convenience of being able to behave as if your JSON value is a `string`.

If so, you can now use the `--useImplicitOperatorString` command line switch to emit an implicit conversion operator to `string` for JSON `string` types.

Note: this means you will never use the built-in `Corvus.Json` types for your string-like types. This could increase the amount of code generated for your schema.

### New Source Generator

We now have a source generator that can generate types at compile time, rather than using the `generatejsonschematypes` tool.

### Using the source generator

Add a reference to the `Corvus.Json.SourceGenerator` nuget package in addition to `Corvus.Json.ExtendedTypes`. [Note, you may need to restart Visual Studio once you have done this.]
Add your JSON schema file(s), and set the Build Action to _C# analyzer additional file_.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Corvus.Json.ExtendedTypes" Version="4.3.9" />
    <PackageReference Include="Corvus.Json.SourceGenerator" Version="4.3.9">
      <PrivateAssets>all</PrivateAssets>
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
    </PackageReference>
  </ItemGroup>

  <ItemGroup>
    <AdditionalFiles Include="test.json" />
  </ItemGroup>

</Project>
```

Now, create a `readonly partial struct` as a placeholder for your root generated type, and attribute it with
`[JsonSchemaTypeGenerator]`. The path to the schema file is relative to the file containing the attribute. You can
provide a pointer fragment in the usual way, if you need to e.g. `"./somefile.json#/components/schema/mySchema"`

```csharp
namespace SourceGenTest2.Model;

using Corvus.Json;

[JsonSchemaTypeGenerator("../test.json")]
public readonly partial struct FlimFlam
{
}
```

The source generator will now automatically emit code for your schema, and you can use the generated types in your code.

```
using Corvus.Json;
using SourceGenTest2.Model;

FlimFlam flimFlam = JsonAny.ParseValue("[1,2,3]"u8);
Console.WriteLine(flimFlam);
JsonArray array = flimFlam.As<JsonArray>();
Console.WriteLine(array);
```

You can find an example project here: [Sandbox.SourceGenerator](./Solutions/Sandbox.SourceGenerator)

We'd like to credit our Google Summer of Code 2024 contributor, [Pranay Joshi](https://github.com/pranayjoshi) and mentor [Greg Dennis](https://github.com/gregsdennis) for their work on this tool.

#### Configuring the source generator

There are a number of global configuration options for the source generator. These can be added to a `PropertyGroup` in your `.csproj` file.

e.g.

```xml
<PropertyGroup>
   <CorvusJsonSchemaOptionalAsNullable>None</CorvusJsonSchemaOptionalAsNullable>
</PropertyGroup>
```

`CorvusJsonSchemaOptionalAsNullable`
  - `None` - Do not emit nullable properties for optional properties
  - `NullOrUndefined` - Emit nullable properties for optional properties

`CorvusJsonSchemaDisableOptionalNamingHeuristics`
  - `False` - Enable optional naming heuristics [default]
  - `True` - Disable optional naming heuristics

`CorvusJsonSchemaDisabledNamingHeuristics`
  - Semi-colon separated list of naming heuristics to disable. You can list the available name heuristics with the `generatejsonschematypes listNameHeuristics` command in the CLI.

`CorvusJsonSchemaAlwaysAssertFormat`
  - `False` - Respect the vocabulary's format assertion
  - `True` - Always assert format assertions [default]

### New dynamic schema validation

There is a new `Corvus.Json.Validator` assembly, containing a `JsonSchema` type.

This is a new *dynamic* JSON Schema validator that can validate JSON data against a JSON Schema document, without the need to generate code ahead-of-time.

This is useful for scenarios where you have a JSON Schema document that is not known at compile time, and you only require validation, not deserialization.

You can load the schema with

```csharp
var corvusSchema = CorvusValidator.JsonSchema.FromFile("./person-array-schema.json");
```

This builds and caches a schema object from the file, and you can then validate JSON data against it with

```csharp
JsonElement elementToValidate = ...
ValidationContext result = this.corvusSchema.Validate(elementToValidate);
```

Note that this uses dynamic code generation under the hood, with Roslyn, so there is an appreciable cold-start cost for the very first schema you validate in this way
while the Roslyn components are jitted. Subsequent schema are much faster, and reused schema come from the cache.

If you reference the `Corvus.Validator` package directly in your executing assembly, it will include a target that ensures `<PreserveCompilationContext>true</PreserveCompilationContext>`
is added to a `<PropertyGroup>` in your project.

If you are using the `Corvus.Json.Validator` package in a library, you should ensure that the consuming project has this property set, to avoid issues with dynamic code generation.

You will have to do this manually if it is consumed via a Project Reference.

### New `generatejsonschematypes config` command

  Supply a json config file to the generate command, to configure and generate 1 or many schema in a single command.

  The configuration file also allows you to explicitly name arbitrary types, and optionally map them in to a specific .NET namespace.

  You can also map json schema base file URIs to specific .NET namespaces, and pre-load known-good versions of file reference dependencies.

  The [schema for the configuration file is here](./Corvus.Json.CodeGenerator/generator-config.json).

### New command line validator with `generatejsonschematypes validateDocument`

This command will validate a JSON document against a JSON schema, and output the results to the console.

For example, given schema `schema.json`

```json
{
    "$schema": "https://corvus-oss.org/json-schema/2020-12/schema",
    "type": "array",
    "prefixItems": [
        {
            "$corvusTypeName": "PositiveInt32",
            "type": "integer",
            "format": "int32",
            "minimum": 0
        },
        { "type": "string" },
        {
            "type": "string",
            "format": "date-time"
        }
    ],
    "unevaluatedItems": false
}
```

and the document `document_to_validate.json`

```json
[
    -1,
    "Hello",
    "Goodbye"
]
```

If we run:

```
generatejsonschematypes validateDocument ./schema.json ./document_to_validate.json`
```

We see the output:

```
Validation minimum - -1 is less than 0 (#/prefixItems/0/minimum, #/prefixItems/0, #/0, ./testdoc.json#1:4)
Validation type - should have been 'string' with format 'datetime' but was 'Goodbye'. (#/prefixItems/2, , #/2, ./testdoc.json#3:4)
```
### Multi-language code generator engine

- Brand new JSON Schema analyser engine, which is now language independent.
- Brand new code generation engine, which is more flexible and extensible, and uses the result of the schema analyser.
- An extensible C# language provider which generates code-using-code. No more T4 templates in the language engine.

### Additional features

- Opt-out of optional naming heuristics introduced in V3.0 with the `--disableOptionalNamingHeuristics` command line switch.
- Opt-out of specific naming heuristics by specifying `--disableNamingHeuristic`. You can list the available name heuristics with the new `generatejsonschematypes listNameHeuristics` command
- Safe truncation for extremely long file names
- Access to all JSON schema validation constants via the `CorvusValidation` nested static class.
- All formatted types (e.g. string or number formats) are now convertible to the equivalent core types (e.g. your custom `"format": "date"` type is freely convertible to and from `JsonDate`) and offer the same accessors and conversions as the core types.

### Upgrading to V4
- Code generated using V3.1 of the generator can still be built against V4 of Corvus.Json.ExtendedTypes, and used interoperably.

  This allows you to upgrade your code piecemeal to the new version of the generator. You do not need to update everything all at once.

- For the vast majority of schema, the new naming heuristics will continue to work as they did in V3.
  However, if you have a schema that is not generating the names you expect, you can inject `$corvusTypeName` into the schema to provide a hint to the generator.
  If you  hit one of these cases, please [open an issue in github](https://github.com/corvus-dotnet/Corvus.JsonSchema/issues).

- We now generate fewer files for each type. You should delete your previous generated files before running the new version of the generator, to avoid leaving duplicate partial definitions.

### Breaking changes
- .NET 6 and .NET 7 are now out-of-support. We no longer support these versions. The `netstandard2.0` builds will fail at runtime.

- - We no longer generate the property 'default' accessors.

  Prior to V4 we emitted methods like `TryGetDefault(in JsonPropertyName name, out JsonAny value)` on objects whose properties had types with default values.

  This was somewhat redundant code, as
  a) it lacked strong typing and
  b) it had unncessary overhead - you could go directly to the property type to get the default value, rather than doing a lookup by the property name.

  If you want to discover the default value for a property, you must now do so by inspecting the `Default` static property of its type.
  If you are affected by this change, you can copy the `[typename].Default.cs` file for the relevant type from your V3 code base to provide the capability.

  However, we recommend refactoring to use the static `Default` property on the property type instead.

## V3.0 Updates

The big change with v3.0 is support for older (supported) versions of .NET, including the .NET Framework, through netstandard2.0.

As of v3.0.23 we also support draft4 and OpenAPI3.0 schema.

Additional changes include:

- Pattern matching methods for anyOf, oneOf and enum types.
- Implicit cast to bool for boolean types
- Specify an explicit type name hint for a schema with the $corvusTypeName keyword
- Improved heuristic for type naming based on `title` and `documentation` as fallbacks if no better name can be dervied.

## V2.0 Updates

There have been considerable breaking changes with V2.0 of the generator. This section will help you understand what has changed, and how to update your code.

### Json Schema Models

The JSON Schema Models have been broken out into separate projects.

  - Corvus.Json.JsonSchema.Draft6
  - Corvus.Json.JsonSchema.Draft7
  - Corvus.Json.JsonSchema.Draft201909
  - Corvus.Json.JsonSchema.Draft202012

### Code Generation

### Property Names

The static values for JSON Property Names have been moved from the root type, to a nested subtype called `PropertyNamesEntity`

### Conversions and operators

The implicit/explicit conversions and operators have been rationalised. More explicit conversions are required, at the expense of the implicit conversions.

However, most implicit conversions from/to intrinsic types are still supported.

One significant change is that there is *no* implicit conversion to `string` - this must be done explicitly, or directly through one of the comparison functions like `EqualsString()` or `EqualsUtf8String()`. This is to prevent a common source of accidental allocations and the corresponding performance hit.

## System.Text.Json support by other projects

There is a thriving ecosystem of System.Text.Json-based projects out there.

In particular I would point you at

[JsonEverything](https://github.com/gregsdennis/json-everything) by [@gregsdennis](https://github.com/gregsdennis)

- JSON Schema, drafts 6 and higher ([Specification](https://json-schema.org))
- JSON Path ([RFC in progress](https://github.com/ietf-wg-jsonpath/draft-ietf-jsonpath-jsonpath)) (.NET Standard 2.1)
- JSON Patch ([RFC 6902](https://tools.ietf.org/html/rfc6902))
- JsonLogic ([Website](https://jsonlogic.com)) (.NET Standard 2.1)
- JSON Pointer ([RFC 6901](https://tools.ietf.org/html/rfc6901))
- Relative JSON Pointer ([Specification](https://tools.ietf.org/id/draft-handrews-relative-json-pointer-00.html))
- Json.More.Net (Useful System.Text.Json extensions)
- Yaml2JsonNode

[JsonCons.Net](https://github.com/danielaparker/JsonCons.Net) by [@danielParker](https://github.com/danielaparker)

- JSON Pointer
- JSON Patch
- JSON Merge Patch
- JSON Path
- JMES Path
