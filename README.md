# Corvus.JsonSchema
Support for Json Schema validation and entity generation

For an introduction to the concepts here, take a look at [this blog post](https://endjin.com/blog/2021/05/csharp-serialization-with-system-text-json-schema).

## Getting started

To get started, install the dotnet global tool.

```
dotnet tool install --global Corvus.Json.JsonSchema.TypeGeneratorTool
```

Validate it is installed correctly

```
generatejsonschematypes -h
```

This should produce output similar to the following:

```
Description:
  Generate C# types from a JSON schema.

Usage:
  generatejsonschematypes <schemaFile> [options]

Arguments:
  <schemaFile>  The path to the schema file to process

Options:
  --rootNamespace <rootNamespace>            The default root namespace for generated types
  --rootPath <rootPath>                      The path in the document for the root type.
  --useSchema <Draft201909|Draft202012>      The schema variant to use. [default: Draft201909]
  --outputMapFile <outputMapFile>            The name to use for a map file which includes details of the files that
                                             were written.
  --outputPath <outputPath>                  The output directory. It defaults to the same folder as the schema file.
  --outputRootTypeName <outputRootTypeName>  The Dotnet TypeName for the root type. []
  --rebaseToRootPath                         If a --rootPath is specified, rebase the document as if it was rooted on
                                             the specified element.
  --version                                  Show version information
  -?, -h, --help                             Show help and usage information
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
generatejsonschematypes --rootNamespace JsonSchemaSample.Api --rootPath '#/$defs/Person' person-from-api.json
```

Compile this code in a project with a reference to the `Corvus.Json.ExtendedTypes` nuget package, and you can then work with the Dotnet type model. e.g.

```csharp
string jsonText = @"{
    ""name"": {
      ""familyName"": ""Oldroyd"",
      ""givenName"": ""Michael"",
      ""otherNames"": [""Francis"", ""James""]
    },
    ""dateOfBirth"": ""1944-07-14""
}";

var person = Person.Parse(jsonText);
Console.WriteLine(person.Name.FamilyName);
```

We also provide  a [full hands-on-lab](docs/GettingStartedWithJsonSchemaCodeGeneration.md).

## Use of JSON-Schema-Test-Suite

This project uses test suites from https://github.com/json-schema-org/JSON-Schema-Test-Suite to
validate operation. The ./JSON-Schema-Test-Suite folder is a submodule pointing to that test suite
repo. When cloning this repository it is important to clone submodules, because test projects in
this repository depend on that submodule being present. If you've already cloned the project, and
haven't yet got the submodules, run this commands:

```
git submodule update --init --recursive
```

Note that `git pull` does nota utomatically update submodules, so if `git pull` reports that any
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

## Use of benchmark dotnet

While we are still making use of Preview features, you need to add the benchmark dotnet nightlies package feed to your nuget package sources.
Benchmark Dotnet Nightlies

[You can find the benchmark dotnet nightlies here.](https://ci.appveyor.com/nuget/benchmarkdotnet)

(For normal builds, this is done for you in the nuget.config found in the root of the project.)

## Organization of the repository

### Corvus.Json.CodeGenerator

A dotnet command line tool that generates C# code from JSON schema.

### Corvus.Json.ExtendedTypes

Builds on System.Text.Json to provide a rich object model over JSON data, with validation for well-known types.

### Corvus.Json.JsonSchema

An object model for working with JSON Schema documents. *This does not provide validation of data - it is purely a model for reading,  writing, and validating JSON Schema documents of various flavours.*

### Corvus.Json.CodeGeneration.Abstractions

Common code to assist with building code generators for various flavours of JSON schema. It includes a common data model for asbtracting schema into C# types in the form of a `TypeDeclaration.cs`, a set of T4 templates in `/SharedTemplates/*.tt` for common JSON schema features, and useful `Formatting.cs` utilities.

### Corvus.CodeGeneration.202012
### Corvus.CodeGeneration.201909
### Corvus.CodeGeneration.7
### Corvus.CodeGeneration.6

Specific implementations of the code generators for various JSON schema dialects.

The code in `JsonSchemaBuilder` uses the `JsonSchemaWalker` to build the appropriate `TypeDeclaration` instances for the particular schema.

It then passes those to the various T4 code generators to generate the partial classes for each type discovered. It only generates partials for the features needed for that type.

The code for those T4 code generators is produced using the `BuildTemplates.cmd`. This automates the process of taking the T4 templates, both from the SharedTemplates folder in `Corvus.Json.CodeGeneration.Abstractions` and custom templates that are included in the local `Templates/*.tt` for those elements which vary in that particular schema.

If you are building your own generators, or modifying the existing ones, pointers to instructions for using this tool can be found in `BuildTemplates.cmd`.

The T4 templates need "code-behind" partials to provide the context for the generator. These are also generated from a T4 template called `Templates/CodeGeneratorPartial.tt`, by the `BuildTemplates.cmd` command. Again, these can be customized to extend the generator context required by your own generators.

You would not normally need to run those commands to build the solution, as their output is checked into the repository.

### Corvus.Json.Walker

A visitor for arbitrary JSON documents, with the ability to follow JSON references and load documents from multiple sources, including the local filesystem, and over HTTP. This resolution is extensible to arbitrary sources by implementing `IDocumentResovler`.

It is used by the `JsonSchemaBuilder` implementations to walk the schema and populate the `TypeDeclaration` instances for the schema.

### Corvus.Json.Patch

A fast, low-allocation implementation of JSON Patch over `Corvus.Json.ExtendedTypes`.

### Corvus.Json.Specs

Specification/tests for the various components in the solution.

### Corvus.JsonSchema.SpecGenerator

Generates Feature Files in `Corvus.Json.Specs` for the specs found in the JSON-Schema-Test-Suite (see above).

### Corvus.Json.Patch.SpecGenerator

Generates Feature Files in `Corvus.Json.Specs` for the JSON Patch tests.

### Corvus.Json.Benchmarking
### Corvus.JsonPatch.Benchmarking

Benchmark suites for various components.
