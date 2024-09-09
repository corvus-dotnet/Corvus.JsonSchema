# Corvus.JsonSchema
Build-time code generation for [Json Schema](https://json-schema.org/) validation, and serialization.

It supports serialization of *every feature of JSON schema* from draft4 to draft2020-12, including the OpenApi3.0 variant of draft4. (i.e. it doesn't give up on complex structure and lapse back to 'anonymous JSON objects' like most dotnet tooling.)

## Supported platforms

### netstandard2.0
It now works with **every supported .NET version** by providing netstandard2.0 packages.

### net80 and later
There are also optimized packages that take advantage of features in NET8.0 and later.

Note that if you are building libraries using `Corvus.Json.ExtendedTypes`, and generated schema types, you should ensure
that you target *both* `netsandard2.0` *and* `net80` (or later) to ensure that your library can be consumed
by the widest possible range of projects

If you build your library against `netstandard2.0` only, and are consumed by a `net80` or later project, you will see type load errors.

## Support schema dialects

In V4 we have full support for the following schema dialects:

- Draft 4
- OpenAPI 3.0
- Draft 6
- Draft 7
- 2019-09
- 2020-12 (Including OpenAPI 3.1)

You can see full details of the supported schema dialects [on the bowtie website](https://bowtie.report/#/implementations/dotnet-corvus-jsonschema).

Bowtie is tool for understanding and comparing implementations of the JSON Schema specification across all programming languages.

It uses the official JSON Schema Test Suite to display bugs or functionality gaps in implementations.

## Project Sponsor

This project is sponsored by [endjin](https://endjin.com), a UK based Microsoft Gold Partner for Cloud Platform, Data Platform, Data Analytics, DevOps, and a Power BI Partner.

For more information about our products and services, or for commercial support of this project, please [contact us](https://endjin.com/contact-us). 

We produce two free weekly newsletters; [Azure Weekly](https://azureweekly.info) for all things about the Microsoft Azure Platform, and [Power BI Weekly](https://powerbiweekly.info).

Keep up with everything that's going on at endjin via our [blog](https://endjin.com/blog), follow us on [Twitter](https://twitter.com/endjin), or [LinkedIn](https://www.linkedin.com/company/1671851/).

Our other Open Source projects can be found at [https://endjin.com/open-source](https://endjin.com/open-source)

## Code of conduct

This project has adopted a code of conduct adapted from the [Contributor Covenant](http://contributor-covenant.org/) to clarify expected behavior in our community. This code of conduct has been [adopted by many other projects](http://contributor-covenant.org/adopters/). For more information see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [&#104;&#101;&#108;&#108;&#111;&#064;&#101;&#110;&#100;&#106;&#105;&#110;&#046;&#099;&#111;&#109;](&#109;&#097;&#105;&#108;&#116;&#111;:&#104;&#101;&#108;&#108;&#111;&#064;&#101;&#110;&#100;&#106;&#105;&#110;&#046;&#099;&#111;&#109;) with any additional questions or comments.

## Concepts

### Introduction

For a quick introduction, you could read [this blog post by Ian Griffiths (@idg10)](https://endjin.com/blog/2024/04/dotnet-jsonelement-schema), a C# MVP and Technical Fellow at [endjin](https://endjin.com).

There's also [a talk by Ian](https://endjin.com/what-we-think/talks/high-performance-json-serialization-with-code-generation-on-csharp-11-and-dotnet-7-0) on the techniques used in this library.

If you want to see some well-worn patterns with JSON Schema, and how they translate into common .NET idioms, then [this series](https://endjin.com/blog/2024/05/json-schema-patterns-dotnet-data-object) is very useful. The corresponding sample code is found [here](./docs/ExampleRecipes). 

### History

For a more detailed introduction to the concepts, take a look at [this blog post](https://endjin.com/blog/2021/05/csharp-serialization-with-system-text-json-schema).

### What kind of things is Corvus.JsonSchema good for?

There are 2 key features: 

### Serialization

You use our `generatejsonschematypes` tool to generate code (on Windows, Linux or MacOS) from an existing JSON Schema document, and compile it in a standard .NET assembly.

The generated code provides object models for JSON Schema documents that give you rich, idiomatic C# types with strongly typed properties, pattern matching and efficient cast operations.

You can operate directly over the JSON data, or mix-and-match building new JSON models from .NET primitive types.

```csharp
string jsonText = 
    """
    {
        "name": {
            "familyName": "Oldroyd",
            "givenName": "Michael",
            "otherNames": ["Francis", "James"]
        },
        "dateOfBirth": "1944-07-14"
    }
    """;

var person = Person.Parse(jsonText);

Console.WriteLine($"{person.Name.FamilyName}"});
```

### Validation

The same object-model provides ultra-fast, zero/low allocation validation of JSON data against a JSON Schema.

Having "deserialized" (really 'mapped') the JSON into the object model you can make use of the validation:

```csharp
string jsonText = 
    """
    {
        "name": {
            "familyName": "Oldroyd",
            "givenName": "Michael",
            "otherNames": ["Francis", "James"]
        },
        "dateOfBirth": "1944-07-14"
    }
    """;

var person = Person.Parse(jsonText);

Console.WriteLine($"The person {person.IsValid() ? "is" : "is not"} valid JSON");
```

Or you can retrieve detailed validation results including JSON Schema output format location information:

```csharp
var result = person.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed);

if (!result.IsValid)
{
    foreach (ValidationResult error in result.Results)
    {
        Console.WriteLine(error);
    }
}
```
## Getting started

To get started, install the dotnet global tool.

```
dotnet tool install --global Corvus.Json.JsonSchema.TypeGeneratorTool
```

[On Linux/MacOS you may need to ensure that the `.dotnet/tools` folder is in your path.]

Validate it is installed correctly

```
generatejsonschematypes -h
```

This should produce output similar to the following:

```
USAGE:
    generatejsonschematypes <schemaFile> [OPTIONS]

ARGUMENTS:
    <schemaFile>    The path to the schema file to process

OPTIONS:
                                             DEFAULT
    -h, --help                                               Prints help information
        --rootNamespace                                      The default root namespace for generated types
        --rootPath                                           The path in the document for the root type
        --useSchema                          NotSpecified    Override the fallback schema variant to use. If
                                                             NotSpecified, and it cannot be inferred from the schema
                                                             itself, it will use Draft2020-12
        --outputMapFile                                      The name to use for a map file which includes details of
                                                             the files that were written
        --outputPath                                         The path to which to write the generated code
        --outputRootTypeName                                 The .NET type name for the root type
        --rebaseToRootPath                                   If a --rootPath is specified, rebase the document as if it
                                                             was rooted on the specified element
        --assertFormat                       True            If --assertFormat is specified, assert format
                                                             specifications
        --disableOptionalNamingHeuristics                    Disables optional naming heuristics
        --optionalAsNullable                 None            If NullOrUndefined, optional properties are emitted as .NET
                                                             nullable values
  ```

To run it against a JSON Schema file in the local file system:

e.g. Create a JSON schema file called `person-from-api.json`

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "JSON Schema for a Person entity coming back from a 3rd party API (e.g. a storage format in a database)",
  "$defs": {
    "Person": {
      "type": "object",

      "required":  ["name"],
      "properties": {
        "name": { "$ref": "#/$defs/PersonName" },
        "dateOfBirth": {
          "type": "string",
          "format": "date"
        }
      }
    },
    "PersonName": {
      "type": "object",
      "description": "A name of a person.",
      "required": [ "familyName" ],
      "properties": {
        "givenName": {
          "$ref": "#/$defs/PersonNameElement",
          "description": "The person's given name."
        },
        "familyName": {
          "$ref": "#/$defs/PersonNameElement",
          "description": "The person's family name."
        },
        "otherNames": {
          "$ref": "#/$defs/OtherNames",
          "description": "Other (middle) names for the person"
        }
      }
    },
    "OtherNames": {
        "oneOf": [
            { "$ref": "#/$defs/PersonNameElement" },
            { "$ref": "#/$defs/PersonNameElementArray" }
        ]
    },
    "PersonNameElementArray": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/PersonNameElement"
      }
    },
    "PersonNameElement": {
      "type": "string",
      "minLength": 1,
      "maxLength": 256
    },
    "Link":
    {
      "required": [
        "href"
      ],
      "type": "object",
      "properties": {
        "href": {
          "title": "URI of the target resource",
          "type": "string",
          "description": "Either a URI [RFC3986] or URI Template [RFC6570] of the target resource."
        },
        "templated": {
          "title": "URI Template",
          "type": "boolean",
          "description": "Is true when the link object's href property is a URI Template. Defaults to false.",
          "default": false
        },
        "type": {
          "title": "Media type indication of the target resource",
          "pattern": "^(application|audio|example|image|message|model|multipart|text|video)\\\\/[a-zA-Z0-9!#\\\\$&\\\\.\\\\+-\\\\^_]{1,127}$",
          "type": "string",
          "description": "When present, used as a hint to indicate the media type expected when dereferencing the target resource."
        },
        "name": {
          "title": "Secondary key",
          "type": "string",
          "description": "When present, may be used as a secondary key for selecting link objects that contain the same relation type."
        },
        "profile": {
          "title": "Additional semantics of the target resource",
          "type": "string",
          "description": "A URI that, when dereferenced, results in a profile to allow clients to learn about additional semantics (constraints, conventions, extensions) that are associated with the target resource representation, in addition to those defined by the HAL media type and relations.",
          "format": "uri"
        },
        "description": {
          "title": "Human-readable identifier",
          "type": "string",
          "description": "When present, is used to label the destination of a link such that it can be used as a human-readable identifier (e.g. a menu entry) in the language indicated by the Content-Language header (if present)."
        },
        "hreflang": {
          "title": "Language indication of the target resource [RFC5988]",
          "pattern": "^([a-zA-Z]{2,3}(-[a-zA-Z]{3}(-[a-zA-Z]{3}){0,2})?(-[a-zA-Z]{4})?(-([a-zA-Z]{2}|[0-9]{3}))?(-([a-zA-Z0-9]{5,8}|[0-9][a-zA-Z0-9]{3}))*([0-9A-WY-Za-wy-z](-[a-zA-Z0-9]{2,8}){1,})*(x-[a-zA-Z0-9]{2,8})?)|(x-[a-zA-Z0-9]{2,8})|(en-GB-oed)|(i-ami)|(i-bnn)|(i-default)|(i-enochian)|(i-hak)|(i-klingon)|(i-lux)|(i-mingo)|(i-navajo)|(i-pwn)|(i-tao)|(i-tay)|(i-tsu)|(sgn-BE-FR)|(sgn-BE-NL)|(sgn-CH-DE)|(art-lojban)|(cel-gaulish)|(no-bok)|(no-nyn)|(zh-guoyu)|(zh-hakka)|(zh-min)|(zh-min-nan)|(zh-xiang)$",
          "type": "string",
          "description": "When present, is a hint in RFC5646 format indicating what the language of the result of dereferencing the link should be.  Note that this is only a hint; for example, it does not override the Content-Language header of a HTTP response obtained by actually following the link."
        }
      }
    }
  }
}
```

Then run the tool to generate C# files for that schema in the JsonSchemaSample.Api namespace, adjacent to that document.

```
generatejsonschematypes --rootNamespace JsonSchemaSample.Api --rootPath #/$defs/Person person-from-api.json
```

Compile this code in a project with a reference to the `Corvus.Json.ExtendedTypes` nuget package, and you can then work with the Dotnet type model, and JSON Schema validation e.g.

```csharp
string jsonText =
    """{
           "name": {
               "familyName": "Oldroyd",
               "givenName": "Michael",
               "otherNames": ["Francis", "James"]
           },
           "dateOfBirth": "1944-07-14"
       }""";

var person = Person.Parse(jsonText);
Console.WriteLine(person.Name.FamilyName);
Console.WriteLine($"The person {person.IsValid() ? "is" : "is not"} valid JSON");
```

We also provide  a [full hands-on-lab](docs/GettingStartedWithJsonSchemaCodeGeneration.md).

# Development environment

## Use of PowerShell

This project uses its own code generation to generate code for the built-in JSON types in `Corvus.Json.ExtendedTypes`, and the
types for working with various schema dialects such as `Corvus.Json.JsonSchema.Draft202012`.
If you add or update core types, you will need to run the `./Solutions/generatetypes.ps1` script file to regenerate them.

## Use of JSON-Schema-Test-Suite

This project uses test suites from https://github.com/json-schema-org/JSON-Schema-Test-Suite to
validate operation. The ./JSON-Schema-Test-Suite folder is a submodule pointing to that test suite
repo. When cloning this repository it is important to clone submodules, because test projects in
this repository depend on that submodule being present. If you've already cloned the project, and
haven't yet got the submodules, run this commands:

```
git submodule update --init --recursive
```

Note that `git pull` does not automatically update submodules, so if `git pull` reports that any
submodules have changed, you can use the preceding command again, used to update the existing
submodule reference.

When updating to newer versions of the test suite, we can update the submodule reference thus:

```
cd JSON-Schema-Test-Suite
git fetch
git merge origin/main
cd ..
git commit -a -m "Updated to the lastest JSON Schema Test Suite"
```

(Or you can use `git submodule update --remote` instead of `cd`ing into the submodule folder and
updating from there.)

## Organization of the repository

### Corvus.Json.CodeGenerator

A .NET command line tool that generates C# code from JSON schema.

### Corvus.Json.ExtendedTypes

Builds on System.Text.Json to provide a rich object model over JSON data, with validation for well-known types.

### Corvus.Json.JsonSchema.*

Object models for working with JSON Schema documents. *This does not provide validation of your own JSON Schema - it is purely a model for reading,  writing, and validating JSON Schema documents of various flavours.*

### Corvus.Json.CodeGeneration

Common code to assist with building code generators for various flavours of JSON schema. It includes a common data model for abstracting schema into an object model which can be consumed by an `ILanguageProvider` (or other analyser).

### Corvus.Json.CodeGenerator.CSharp

A C# `ILanguageProvider` that can take the anlysis of a JSON Schema document from Corvus.Json.CodeGeneration and generate C# code from it.

### Corvus.Json.CodeGeneration.202012, Corvus.Json.CodeGeneration.201909, Corvus.Json.CodeGeneration.7, Corvus.Json.CodeGeneration.6, Corvus.Json.CodeGeneration.4, Corvus.Json.CodeGeneration.OpenApi30

Specific dialects that collect keywords into vocabularies, and provide analysers to determine the specific vocabulary in play for a particular JSON Schema document.

### Corvus.Json.Validator

Dynamic JSON Schema validator that can validate JSON data against a JSON Schema document loaded at runtime, without the need to generate code ahead-of-time.

### Corvus.Json.Patch

An implementation of JSON Patch over `Corvus.Json.ExtendedTypes`.

### Corvus.Json.Specs

Specification/tests for the various components in the solution.

### Corvus.JsonSchema.SpecGenerator

Generates Feature Files in `Corvus.Json.Specs` for the specs found in the JSON-Schema-Test-Suite (see above).

### Corvus.Json.Patch.SpecGenerator

Generates Feature Files in `Corvus.Json.Specs` for the JSON Patch tests.

### Corvus.Json.Benchmarking
### Corvus.JsonPatch.Benchmarking

Benchmark suites for various components.

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
- We no longer generate the property 'default' accessors.

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
