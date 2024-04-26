TL;DR - this is a getting started Hands-On-Lab that walks you through our new JSON Schema-based code generation library and tools for C#. It builds on `System.Text.Json` to provide rich serialization, deserialization, composition, and validation support.

## Goals

- Understand how to generate C# code from JSON schema, supporting the full capabilities of JSON Schema, using the `Corvus.Json.CodeGenerator` tool.
- Understand how to serialize and deserialize JSON documents using the generated code
- Understand how to validate JSON documents against schema using the generated code
- Understand how to navigate a JSON document using the generated code
- Understand how to explore an unknown JSON document (or undocumented extensions) using the `Corvus.Json.ExtendedTypes`
- Understand how to create new JSON documents using the generated code and `Corvus.Json.ExtendedTypes`.
- Understand how to transform and compose JSON from various sources, without unnecessary allocations or copying.

## Context

In my [previous post](https://endjin.com/blog/2021/05/csharp-serialization-with-system-text-json-schema), I introduced the concepts behind our JSON object model extensions, built over [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json?view=net-8.0).

> You don't need to read that post to work with this lab.

In summary, we looked at how a code generation tool could take JSON Schema and emit a full-fidelity dotnet type model for that schema, including well-optimised schema validation, with great allocation and compute performance for common scenarios where traditional JSON serialization would be the norm.

It also demonstrated how this model could support interoperability between types generated from different schema, and even compiled into different libraries, without any shared user code.

I'm pleased to say that we've now published this tooling over on github/nuget. This is the [library containing the core extensions to the JSON object model](https://www.nuget.org/packages/Corvus.Json.ExtendedTypes) and this is the [tool which generates code from JSON-schema definitions](https://www.nuget.org/packages/Corvus.Json.JsonSchema.TypeGeneratorTool).

If you want to incorporate this into your tool chain, read on!

## Hands on Lab - The Rules

This is a hands-on-lab. While you'll get a lot from reading this as "documentation", you'll get a whole lot more from following along and working through code as you go.

Other than that, there are no rules. Pause, stop, go and explore things for yourself as you go along, make lists of questions and post them here. We're around to help you get familiar with the tools and code.

Also, you don't have to use exactly the tools we recommend. If you are proficient with another IDE, go ahead and use that instead.

But this is intended to be a step-by-step guide. Please let us know in the comments if we've glossed over anything, and we'll add some more detail or explanatory notes.


> ### A note for non-C# dotnet developers
> If you aren't a C# dotnet developer... I guess you're used to translating from C# examples to F# or VB. Sorry, this is another one of those articles.
>
> While it's also true that the code generator emits C# code, you can compile it into a dotnet library for use with your preferred language. The actual generation is templated and extensible, so if you were tempted, you could emit code in the language of your choice. The translation would not be trivial, but *PRs are Love*.
>
> F# would be particularly well suited to an idiomatic implementation!

## Prerequisites

### You'll need

- the [.NET 8 SDK or later](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) (maybe you've already installed [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) and acquired it that way; but you don't have to install Visual Studio to get started; you can download these SDK bits and use the completely free/OSS toolchain to follow along.)
- a shell, with the SDK developer tools in the path. I'm using [PowerShell](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) in the [Windows Terminal](https://docs.microsoft.com/en-us/windows/terminal/install), [configured with the Visual Studio "Developer" config for PowerShell](https://blog.yannickreekmans.be/developer-powershell-command-prompt-visual-studio-windows-terminal/).
- A text editor or IDE. I'm using [VS Code](https://code.visualstudio.com/).

### Things that would help

- Some familiarity with building C# code with dotnet8.0
- Some familiarity with [json-schema](https://json-schema.org/understanding-json-schema/)
- Some familiarity with JSON reading, writing, and serialization, preferably with `System.Text.Json`

## Getting started

First, you need to install the code generator. I choose to do so globally. From a developer command prompt, use the following syntax:

```
dotnet tool install --global Corvus.Json.JsonSchema.TypeGeneratorTool --prerelease
```

We'll also create a console app to host our sample, using dotnet8.0s

```
dotnet new console -o JsonSchemaSample -f net8.0
cd JsonSchemaSample
```

And we'll add a reference to our JSON object model extensions to the project. We can use the `dotnet add` command to do that, or you could use your favourite package manager, or IDE.

```
dotnet add package Corvus.Json.ExtendedTypes
```

Just to make sure that's all OK, and our editor is also working, let's inspect `JsonSchemaSample.csproj`.

```
code JsonSchemaSample.csproj
```

> Remember, I'm using [VS Code](https://code.visualstudio.com/Download). But you can use whatever tooling you like.

When the editor loads up the project file, it should look something like this. Notice that we have a package reference to `Corvus.Json.ExtendedTypes`.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Corvus.Json.ExtendedTypes" Version="2.0.15" />
  </ItemGroup>

</Project>
```

## Designing with JSON schema

We are going to start with a JSON schema document. The first one we will be working with is a simple representation of a "person". Maybe it is the schema from a CRM service's API?

Here's the whole thing, and we'll break it down in more detail in a second.

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

Let's get that into the project. As before, these steps are using PowerShell and VS Code, but you can use whatever IDE and/or tools you like. We'll create an `api` folder and drop a document called `person-from-api.json` in there.

```
mkdir api
cd api
code person-from-api.json
```

Then copy and paste the schema above, and save.

Right! Let's take a look at this schema in more detail.

First, you can see that it is a draft2020-12 schema.

```json
"$schema": "https://json-schema.org/draft/2020-12/schema",
```

We support [draft 2020-12](http://json-schema.org/draft/2020-12/json-schema-core.html) and [draft 2019-09](http://json-schema.org/draft/2019-09/json-schema-core.html) with the tooling.

> If people wanted to extend the tools and libraries to support back-level schema versions, it would not be too difficult; the older revisions are largely subsets of the later ones. It's well outside the scope of this introductory tutorial, but [PRs are gratefully received](https://github.com/corvus-dotnet/Corvus.JsonSchema)!

You'll then notice that the root schema is basically empty, The interesting elements are all in the `$defs` section.

This is a matter of style and habit - I tend to use document fragments that are then included by `$ref` in other places (e.g. OpenAPI documents). So everything goes in the `$defs` section.

The first entity we encounter in the `$defs` section is a `Person` with a required `name` property, and an optional `dateOfBirth`.

```json
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
    }
```

The `dateOfBirth` is a standard date string, which, as you probably know, is [defined by json-schema](https://json-schema.org/understanding-json-schema/reference/string.html#dates-and-times) to be in the form `yyyy-mm-dd`.

The `name` is defined by a reference to the `PersonName` schema. If we look up the reference, we see that this is an entity with a required `familyName` property and optional `givenName` and `otherNames`.

```json
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
    }
```

`familyName` and `givenName` are both defined by a reference to a `PersonNameElement`.

```json
"PersonNameElement": {
      "type": "string",
      "minLength": 1,
      "maxLength": 256
    }
```

This turns out to be a string which, if present, must be at least 1 character long, and at most 256 characters long.

`otherNames` is defined by our `OtherNames` schema, and that is a bit more interesting.

```json
"OtherNames": {
        "oneOf": [
            { "$ref": "#/$defs/PersonNameElement" },
            { "$ref": "#/$defs/PersonNameElementArray" }
        ]
    }
```

This says that it will be *either* a `PersonNameElement` *or* a `PersonNameElementCollection` (but not both).

We've already seen `PersonNameElement`, and a `PersonNameElementArray` is, as you might imagine, an array of `PersonNameElement` items.

```json
"PersonNameElementArray": {
      "type": "array",
      "items": {
        "$ref": "#/$defs/PersonNameElement"
      }
    }
```

So, we're saying that you can represent the "other names" as a single string, or an array of strings.

An example might be useful. If a person's full name was `Michael Francis James Oldroyd`, I could represent that in schema-valid JSON as

```json
{
    "familyName": "Oldroyd",
    "givenName": "Michael",
    "otherNames": "Francis James"
}
```

or

```json
{
    "familyName": "Oldroyd",
    "givenName": "Michael",
    "otherNames": ["Francis", "James"]
}
```

You can see how this gives the API flexibility. Maybe a previous version only supported the single `PersonNameElement` form, and we added this array option in a later version, maintaining backwards compatibility, but giving us a way of deconstructing the name with higher fidelity.

> I won't go on about schema-first or code-first, but this is a really good reason to use a schema-first design approach.
>
> C# developers may not have thought of this pattern when extending their API with a code-first approach. *Either-this-or-that* (a form of [union](https://en.wikipedia.org/wiki/Union_type)) is not a language-supported idiom, but it is frequently useful in the rest of the universe! Especially in JSON schema, where it is *everywhere*. You'll see more of this later.

You'll also notice that there is a `Link` schema fragment at `#/$defs/Link`, which isn't referenced elsewhere (for now!). If you take a quick look, you'll see that it is a schematisation of a web link. More on this later...

Anyway, whatever the pros and cons of this design, that's what our schema looks like!

So, let's generate some code...

## Generating C# code

We've already installed our code generator tool. To check that all went well, we can run it, with the `-h` option.

```
generatejsonschematypes -h
```

You should see the help text - something like this at the time of writing.

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

So - how to generate some code from our schema?

The first option we're going to use defines the `--rootNamespace` into which our types will be generated.

> You'll see later that only some of our types will be emitted directly into that namespace - many will become *nested types* embedded in their parent type. This helps us to keep the namespace clean, and minimize type name clashes.

We will use `JsonSchemaSample.Api` as the namespace. This matches our project name and folder path.

Second, we can provide a --rootPath to locate the schema in the document for which we are going to generate code.

We want to generate the code for the schema at #/$defs/Person.

> You'll probably recognize this as the syntax you use for specifying a $ref within a JSON schema document. It is part of the JSON Pointer specification.
>
> (And technically, it is in the URI Fragment Identifier Representation of that format.)

Note also that in most terminals, you will have to wrap the pointer in single quotes to ensure that the command line is parsed correctly, as above.

Finally, we need to provide the path to the json schema document containing the schema for which to generate types. We happen to be in the same directory as the file concerned, so that is just `person-from-api.json`.

> Note that any references to documents either in this parameter on the command line, or in `$ref`s in the documents themselves don't *have* to be in the local file system. You can happily use `http[s]` references to external documents, and it'll work just fine!
>
> We'll see this in action as we develop our example further.

The other defaults mean that we will generate our output files in the same folder as the input schema file.

> Notice that this is *not* the current working directory. You can supply deep paths to the source file from wherever you happen to be, and the output files will be generated next to it. There's no need to specify an `--outputPath` for this common scenario.
>
> We've found that this minimizes the complexity of integrating the tool into a build process.

So we end up with the command.

```
generatejsonschematypes --rootNamespace JsonSchemaSample.Api --rootPath #/$defs/Person person-from-api.json
```

Let's run that now. When it has completed, list the C# files in the directory, using whatever command is appropriate for your shell.

```
ls *.cs
```

> Remember that I'm using PowerShell, so, as with Linux distros, I have access to `ls`. Windows Command Prompt users might want `dir`.

You should see the following file names listed (plus whatever other detail your particular shell adds to its output):
```
Name
----
OtherNames.Array.Add.cs
OtherNames.Array.cs
OtherNames.Array.Remove.cs
OtherNames.Conversions.Accessors.cs
OtherNames.Conversions.Operators.cs
OtherNames.cs
OtherNames.String.cs
OtherNames.Validate.cs
OtherNames.Validate.OneOf.cs
Person.cs
Person.Object.cs
Person.Properties.cs
Person.Validate.cs
Person.Validate.Object.cs
Person.Validate.Type.cs
PersonName.cs
PersonName.Object.cs
PersonName.Properties.cs
PersonName.Validate.cs
PersonName.Validate.Object.cs
PersonName.Validate.Type.cs
PersonNameElement.cs
PersonNameElement.String.cs
PersonNameElement.Validate.cs
PersonNameElement.Validate.Type.cs
PersonNameElementArray.Array.Add.cs
PersonNameElementArray.Array.cs
PersonNameElementArray.Array.Remove.cs
PersonNameElementArray.cs
PersonNameElementArray.Validate.Array.cs
PersonNameElementArray.Validate.cs
PersonNameElementArray.Validate.Type.cs
```

So far so good. Let's have a look at the generated types in more detail.

## The generated types

The first thing that you'll probably notice is that it has generated files for each of the schema elements that the `Person` schema referenced, plus the `Person` schema itself.

| Schema location | Files |
| --- | --- |
| `#/$defs/Person` | `Person.cs`, `Person.Object.cs`, `Person.Properties.cs`, `Person.Validate.cs`, `Person.ValidateObject.cs`, `Person.ValidateType.cs` |
| `#/$defs/PersonName` | `PersonName.cs`, `PersonName.Object.cs`, `PersonName.Validate.cs`, `PersonName.Validate.Object.cs`, `PersonName.Validate.Type.cs` |
| `#/$defs/PersonNameElement` | `PersonNameElement.cs`, `PersonNameElement.String.cs`, `PersonNameLElement.Validate.cs`, `PersonNameElement.Validate.Type.cs` |
| `#/$defs/OtherNames` | `OtherNames.cs`, `OtherNames.Array.cs`, `OtherNames.Array.Add.cs`, `OtherNames.Array.Remove.cs`, `OtherNames.Conversions.Accessors.cs`, `OtherNames.Conversions.Operators.cs`, `OtherNames.String.cs`, `OtherNames.Validate.cs`, `OtherNames.Validate.OneOf.cs` |
| `#/$defs/PersonNameElementArray` | `PersonNameElementArray.cs`, `PersonNameElementArray.Array.Add.cs`. `PersonNameElementArray.Array.cs`, `PersonNameElementArray.Array.Remove.cs`, `PersonNameElementArray.cs`, `PersonNameElementArray.Validate.Array.cs`, `PersonNameElementArray.Validate.cs`, `PersonNameElementArray.Validate.Type.cs` |

Remember the `Link` schema we saw earlier that was *not* referenced by the `Person` schema? It has *not* been generated. The code generator only generates types for schema elements that it sees as it walks the tree from the element it finds at the `rootPath`.

Before we dive into the details, let's build the code and find out what we can do with it.

## Building

We need to change directory back up into the root of our project, and run the build tool.

> If you are working with an IDE, you could load the project and build it in there, but we're following along with the command line tools.

```
cd ..
dotnet build
```

This emits an executable in the bin folder. We can run it...

```
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

 and check we get the standard `Hello, World!` message.

 ```
 Hello, World!
 ```

> If you don't see the output, double check that you've followed all the steps correctly up to this point. If it still doesn't work, ping us a comment and we will see if we can help you.

Let's put the types we've generated to work.

## Consuming JSON - "Deserialization"

A very common scenario is consuming and working over a JSON payload provided by some service, using dotnet types.

This is often called "deserialization", and consists of taking some UTF8-encoded JSON text payload and turning it into a representation in the dotnet type system.

Generally, this requires us to bring all or part of a data stream into memory, and then construct C# objects from it.

`System.Text.Json` has a very efficient way of doing this using its [`JsonElement`](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement?view=net-6.0) and related entities. These are immutable [value types](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/value-types) that provide thin wrappers over the underlying UTF8 bytes, converting into dotnet primitives like `string`, `long`, or `bool` at the point of use.

Our [Corvus.Json.ExtendedTypes library](https://www.nuget.org/packages/Corvus.Json.ExtendedTypes) extends this model to include types which represent all of the JSON-schema primitives and common extensions, including arrays, objects, numbers, and an assortment of formatted string types like dates, times and URIs.

The code generator builds on these to give us an easy way of manipulating the JSON data in the same just-in-time fashion, without creating a copy of the underlying UTF8 bytes, but with all the idiomatic dotnet features like named properties, and conversion to-and-from dotnet primitives.

So, let's ingest a JSON payload using the types we've just generated.

### Creating an instance from a JSON string

First, let's open our `Program.cs` in our favourite editor.

```
code Program.cs
```

It already contains a couple of lines, that produced our "Hello, World!" message. We can delete them.

```csharp
// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");
```

Instead, let's add a `using` statement for our generated code. Recall that we put it in the `JsonSchemaSample.Api` namespace. We'll also need `System.Text.Json`, `Corvus.Json`, and, for our date work, [`NodaTime`](https://nodatime.org/).

> You can use the internal dotnet date and time types, but NodaTime is a lot better in general, and specifically a better fit for the JSON schema date/time specifications. We hope this changes in dotnet vFuture!

```csharp
using System.Text.Json;
using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;
```

Now, let's create a `JsonDocument` from our sample JSON text.

You're probably already familiar with this process. [`JsonDocument.Parse`](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.jsondocument.parse?view=net-6.0) offers several overloads for parsing streams, blocks of bytes, and plain old strings. We'll use the string parser for the purposes of this lab.

Add this code beneath your `using` statements.

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

using JsonDocument document = JsonDocument.Parse(jsonText);
```

You'll see in a moment that we don't *have* to work through `JsonDocument` to create instances of our types; there are tools in the library which can abstract this away for you. But I think it is useful to start out by seeing a little bit behind the curtain, so it doesn't look *too* much like magic.

> One nice thing about building over `JsonDocument`, `JsonElement`, `Utf8JsonReader` etc. is that we benefit immediately from all of the performance work being put into these types by the dotnet team.

Hopefully, you should recognize that JSON text as something that we expect to be valid according to our `Person` schema.

So let's wrap that element in our `Person` type.

Add the following line of code:

```csharp
Person michaelOldroyd = new(document.RootElement);
```

Now we can access the elements of that JSON payload via our dotnet `Person` and related types.

> Unless otherwise indicated, I'm now going to assume that you are adding any code blocks that appear in this Lab at the bottom of the `program.cs` file.

```csharp
string familyName = (string)michaelOldroyd.Name.FamilyName;
string givenName = (string)michaelOldroyd.Name.GivenName;
LocalDate dateOfBirth = michaelOldroyd.DateOfBirth;

Console.WriteLine($"{familyName}, {givenName}: {dateOfBirth}");
```

Notice how we're using normal property accessors, and regular dotnet types like `string`, or NodaTime's `LocalDate`.

Let's build and run that again.

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

As we'd hope, it produces (something like) the following output:

```
Oldroyd, Michael: 14 July 1944
```

> If you are wondering about variations in the date formatting, that is the result of the [`NodaTime.LocalDate`](https://nodatime.org/3.0.x/api/NodaTime.LocalDate.html) `ToString()` implementation.

So far, so "just like serialization". But - and here's the rather nifty thing about this - creating the wrapper didn't really allocate anything. It made a bit of space on the stack for the `Person` value type, which, internally, has a field for the `JsonDocument.RootElement`. And because those are both `readonly struct`, it minimized copying of that value too. The `JsonDocument` itself is a very slim wrapper over the underlying JSON byte payload (along with some indexing information). This is as close to "just a binary blob" as you are going to get with JSON text.

It's true that we then went on to allocate a bunch of strings when we accessed the bits we were interested in, and passed them to `Console.WriteLine()`, but that's just the cost of interoperating with a world where we don't yet have `ReadOnlySpan<char>`-like strings!

> There are optimizations that could still be done, at the expense of ease-of-use. In particular, we're expecting to see a vFuture version of `System.Text.Json` where it is better able to expose the underlying data without unnecessary string allocation, and we intend to invest in that area. Note also that the string allocations are why we required you to explicitly cast to string rather than allow an implicit conversion, as for the `LocalDate`.

But, by and large, we didn't allocate anything - we continued to work over the underlying UTF8 bytes. This, as we will see, is very powerful.

## Serialization

Reading JSON data is very important. But we also need to write our JSON back to an output of some kind.

There are two ways to do this.

All our dotnet JSON types - both our extensions, and any generated code, implement the `IJsonValue` interface. We'll look at that in more detail later. But one feature of `IJsonValue` is that it has a `WriteTo()` method that takes a `Utf8JsonWriter`.

You'd use it something like this (but don't add this code):

```csharp
// Get a writer from somwehere (e.g. an HttpRequest output stream)
Utf8JsonWriter writer;
michaelOldroyd.WriteTo(writer);
```

This is the most efficient approach, and minimizes allocations.

However, we just want to see what the document is like when serialized. We could do all the work of creating an `ArrayBuffer`, and decoding the output to a `string`, but that's a bit of a pain.

Fortunately, we also provide a simple `Serialize()` extension method that does all this for you.

Add the following code:

```csharp
string serializedOldroyd = michaelOldroyd.Serialize();
Console.WriteLine(serializedOldroyd);
```

When we build and run...

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

We get the following output:

```
Oldroyd, Michael: 14 July 1944
{"name":{"familyName":"Oldroyd","givenName":"Michael","otherNames":["Francis","James"]},"dateOfBirth":"1944-07-14"}
```

> Obviously, it is more efficient to use `WriteTo()`, rather than the `Serialize()` codepath, as the former avoids unnecessarily allocating strings.
>
> It's not totally clear-cut, though. Internally, `Serialize()` uses `stackalloc` and/or buffer rental to avoid allocations, so you only pay for the final decoding from UTF8 `byte` to `char`, and string allocation.
>
> On the other hand, for many common cases, using `WriteTo()` means that you will just write the underlying UTF8-encoded byte buffers directly into the output, even when you have modified and composed JSON content from multiple sources.
>
> Prefer `WriteTo()`, where possible

Once you've finished exploring that, you can delete those two lines before we move on. We'll re-introduce serialization again later.

(*lines to delete*)
```csharp
string serializedOldroyd = michaelOldroyd.Serialize();
Console.WriteLine(serializedOldroyd);
```

## Introducing JsonAny

So we've now seen a simple example of roundtripping our JSON data to-and-from our generated types.

One thing I mentioned earlier was that you don't have to go via `JsonDocument` to get JSON text deserialized into our generated types. There is a a type in the `Corvus.Json.ExtendedTypes` library called `Corvus.Json.JsonAny`. This represents any JSON value, and it has a family of `static` methods called `Parse()`, used for parsing JSON text. These are analogous to `JsonDocument.Parse()`.

We can go ahead and replace these two lines:

```csharp
using JsonDocument document = JsonDocument.Parse(jsonText);
Person michaelOldroyd = new Person(document.RootElement);
```

with

```csharp
Person michaelOldroyd = JsonAny.Parse(jsonText);
```


Build and run again...

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

...and we should see the same result.

```
Oldroyd, Michael: 14 July 1944
```

> ### The same but different
>
> It is worth pausing for a quick sidebar on performance characteristics. Feel free to skip ahead if you don't really care right now.
>
> This code is the same *in effect*, and slightly simpler to write, but a little different under the hood.
>
> Because we have not explicitly created the `JsonDocument` we are no longer in control of its lifetime.
>
> The `JsonAny.Parse()` implementation has used [`JsonElement.Clone()`](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement.clone?view=net-8.0) to give us a backing `JsonElement` that outlives the original `JsonDocument`, and has disposed of that underlying document for us.
>
> This creates a clone of the relevant segment of the underlying document, and relies on GC to clean it up when it is no longer in use.
>
> In high-performance applications, you would want to control the lifetime of the `JsonDocument` yourself.

### Converting to-and-from `JsonAny`

All types in `Corvus.Json` that represent JSON elements are `readonly struct`, and, as we've said above, implement an interface called `IJsonValue`.

This includes `JsonAny` itself, and all the types we code generate.

> Performance note: we rarely use `IJsonValue` directly as that would involve boxing the instance. It is more commonly used as a type constraint on a generic method.

`JsonAny` is a very powerful part of the `Corvus.Json` armoury, for two reasons.

1. An instance of any type that implements `IJsonValue` can be converted to an instance of `JsonAny`
1. An instance of `JsonAny` can be converted to an instance of any type that implements `IJsonValue`

That's actually how `JsonAny.Parse()` worked, above. If you look at its declaration, you'll see that it is of the form

```csharp
public static JsonAny Parse(string json, JsonDocumentOptions options = default)
```

So it returns a `JsonAny`. But it has been implicitly converted to a `Person` in the assignment.

```csharp
Person michaelOldroyd = JsonAny.Parse(jsonText);
```

`Person` has implicit conversion operators to-and-from `JsonAny` to give us an optimised means of converting in either direction.

Note that `Person` itself also has a `Parse()` method - you don't need to go via `JsonAny`, and you can avoid that conversion.

We've actually used other implicit conversions several times already in the code we've written.

Look again at the code that is accessing the values to output them to our Console:

```csharp
string familyName = (string)michaelOldroyd.Name.FamilyName;
string givenName = (string)michaelOldroyd.Name.GivenName;
LocalDate dateOfBirth = michaelOldroyd.DateOfBirth;
```

The code is assigning to `string` and `LocalDate`, but what type *is* the value returned from `michaelOldroyd.Name.FamilyName`, or `michaelOldroyd.DateOfBirth`?

If you look at the code in `Person.Properties.cs` you can find the declaration for the `Person.FamilyName` property.

```csharp
 public JsonSchemaSample.Api.PersonNameElement FamilyName
 ```

So that's a `PersonNameElement`. And yet, we can clearly convert it implicitly to a C# `string`.

What happened was that the code generator examined the schema for `PersonNameElement`. It recognized that a `string` primitive is a valid representation for the `PersonNameElement`, so it generated an assortment of conversions for us, making it very simple to use in regular dotnet code.

Similarly for `PersonName.DateOfBirth`

```csharp
public Corvus.Json.JsonDate DateOfBirth
```

`JsonDate` is part of our extended JSON type model and it is implicitly convertible to-and-from `NodaTime.LocalDate` for the same reason.

So conversions are really useful in that they let us write idiomatic dotnet code, while maintaining the benefits of our JSON data model.

But there's another important consequence of this feature.

Remember our conversion rules:

1. An instance of any type that implements `IJsonValue` can be converted to an instance of `JsonAny`
1. An instance of `JsonAny` can be converted to an instance of any type that implements `IJsonValue`

The transitive nature of these statements leads to a significant corollary:

3. An instance of any type that implements `IJsonValue` can be converted into an instance of any other type that implements `IJsonValue`.

What?! What??!! An instance of any type that implements `IJsonValue` can (via explicit conversion to `JsonAny` in the worst case), be converted to an instance of any other type that implements `IJsonValue`? Regardless of the shape, structure etc? Without `TypeA` having any knowledge of the existence of `TypeB`?

Yes. That's absolute true.

In fact, for your convenience, there is even a 'casting' extension method that lets you perform exactly that anything-to-anything conversion.

(Don't add this code, it's just for illustration.)

```csharp
JsonFoo myFoo;
JsonBar myBar = myFoo.As<JsonBar>();
```

So yes, you *can* convert any instance between any JSON types... but it doesn't mean that the instance is then *valid*.

## Validation

Recall that JSON Schema does not offer a strong type system like the one with which we are familiar in C#.

JSON schema is more like a [duck-typing](https://en.wikipedia.org/wiki/Duck_typing) model. It describes the "shape" of the JSON with statements like

- "it must look like this or like this or like this" (`anyOf`/`oneOf`)
- "if it looks like this, then it will also look like that, otherwise it looks like this other thing" (`if`/`then`/`else`)
- "if it has one of these properties, it must look like this" (`dependentSchemas`)
- "it must be a number or an object" (`sarray of primitive types`)

When we construct an instance of one of our C# `IJsonValue` types from some JSON data, we know we can safely use it via that type, if, and only if, it is *valid* according to the schema from which the type was generated.

Fortunately (but not coincidentally!), the code generator emits an implementation `IJsonValue.Validate()` to test for this.

Let's try validating our `Person`, by adding this code to the end of `Program.cs`

```csharp
bool isValid = michaelOldroyd.IsValid();
Console.WriteLine($"michaelOldroyd {(isValid ? "is" : "is not")} valid.");
```

The first thing you'll notice is that we didn't actually call the `Validate()` method I was just talking about!

`Validate()` is capable of returning a whole lot of diagnostic information about failures, about which we don't care in this case.

> We support a number of different diagnostic levels. In this preview release, the detailed diagnostic information is a bit of a mess; we will fix that up before GA.

Instead, we use the `IsValid<T>()` extension method to return a boolean `true`/`false` value, which is simpler to work with.

If we build and run again...

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

we see the output:

```
Oldroyd, Michael: 14 July 1944
michaelOldroyd is valid.
```

Which is just as we expected.

What if we now deliberately create a JSON document which is not valid according to our schema?

Let's add the following code:

```csharp
string invalidJsonText =
    """
        {
        "name": {
          "givenName": "Michael",
          "otherNames": ["Francis", "James"]
        },
        "dateOfBirth": "1944-07-14"
    }
    """;

Person invalidOldroyd = JsonAny.Parse(invalidJsonText);
bool isValid2 = invalidOldroyd.IsValid();
Console.WriteLine($"invalidOldroyd {(isValid2 ? "is" : "is not")} valid.");
```

Notice that we have omitted the `familyName` property from the `name` object. This makes it invalid according to the schema (because `familyName` is a `required` property.)

Build and run...

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

...and we see

```
Oldroyd, Michael: 14 July 1944
michaelOldroyd is valid.
invalidOldroyd is not valid.
```

Which is as we expected. But, in fact, even though it is invalid, we can still manipulate the parts of the entity that *are* valid, through this type system. To do that we need to understand a little bit more about how data is represented.

Let's add a bit of code to inspect the `invalidOldroyd`.

```csharp
string givenName2 = (string)invalidOldroyd.Name.GivenName;
LocalDate dateOfBirth2 = invalidOldroyd.DateOfBirth;

Console.WriteLine($"{givenName2}: {dateOfBirth2}");
```

Build and run...

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

...and we see

```
Oldroyd, Michael: 14 July 1944
michaelOldroyd is valid.
invalidOldroyd is not valid.
Michael: 14 July 1944
```

So, where the data is present, we can still extract it - the type model is very forgiving like that.

> This is really useful for systems that deal with JSON data. If the type model explodes in the face of malformed data, it can be very difficult to diagnose issues and provide effective error reporting, or even self-healing capabilities.
>
> But if it is important to explode, you can do so; e.g. by testing with `Validate()` and throwing an exception. The choice is yours, not the library's.

 But what about the 'missing' data? How do we deal with that.

This is not just a problem for *invalid* schema. We have to be able to deal with optional data in valid schema, too.

To make things clear, let's delete our "invalid data" code, and go back to our valid JSON text.

(*These are the lines to remove*)
```csharp
string invalidJsonText = 
"""
{
    "name": {
      "givenName": "Michael",
      "otherNames": ["Francis", "James"]
    },
    "dateOfBirth": "1944-07-14"
}
""";

Person invalidOldroyd = JsonAny.Parse(invalidJsonText);
bool isValid2 = invalidOldroyd.IsValid();
Console.WriteLine($"invalidOldroyd {(isValid2 ? "is" : "is not")} valid.");

string givenName2 = (string)invalidOldroyd.Name.GivenName;
LocalDate dateOfBirth2 = invalidOldroyd.DateOfBirth;

Console.WriteLine($"{givenName2}: {dateOfBirth2}");
```

Now, let's adjust our valid JSON text, removing the optional `givenName` property, so the `jsonText` assignment looks like this

```csharp
string jsonText = 
"""
{
    "name": {
      "familyName": "Oldroyd",
      "otherNames": ["Francis", "James"]
    },
    "dateOfBirth": "1944-07-14"
}
""";
```

When we build and run...

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

We see...

```
Unhandled exception. System.InvalidOperationException: Operation is not valid due to the current state of the object.
   at JsonSchemaSample.Api.Person.PersonNameElement.op_Implicit(PersonNameElement value) in C:\Users\matth\source\repos\JsonSchemaSample\api\Person.PersonNameElement.String.cs:line 113
   at Program.<Main>$(String[] args) in C:\Users\matth\source\repos\JsonSchemaSample\Program.cs:line 17
```

What that tells us is that we cannot implicitly convert the missing `PersonName` element into a non-nullable string.

What can we do about that?

## Values, Null, and Undefined

Let's remind ourselves about the characteristics of JSON data.

Remember that JSON values can be present (that's the value), present but *null* (if the schema allows `null` values), or not present at all (which we call *undefined*). This is a little different from dotnet properties which are typically only present or (if the type is nullable) `null`.

```json
{ "foo": 3.14 } # Present with a non-null value
{ "foo": null } # Present and null
{}              # Not present
```

> These latter cases correspond with the `JsonValueKind.Null` and `JsonValueKind.Undefined` values for `JsonElement.ValueKind`.
>
> We expose this same information with the `IJsonValue.ValueKind` property.

Because it is so common to need to test these conditions, we have provided a few extension methods to help out.

Let's replace the code that extracts the given name with the following:

```csharp
string givenName =
    michaelOldroyd.Name.GivenName.IsNotUndefined()
        ? (string)michaelOldroyd.Name.GivenName
        : "[no given name specified]";
```

We are using the `IsNotUndefined()` extension to determine whether we actually have the optional value or not. If not, we will inject some appropriate text.

This time, when we build and run...

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

We get:

```
Oldroyd, [no given name specified]: 14 July 1944
michaelOldroyd is valid.
```

There is a family of other similar extensions like `IsNull()`, and `IsNullOrUndefined()` for you to explore.

> Sometimes, you want to be able to map the JSON concept of *null or undefined* directly to the dotnet concept of *nullable*.
>
> We provide an extension method `AsOptional<T>()`, which converts the `IJsonValue` from a `T` to a `Nullable<T>`. The value will be `null` if the JSON element was `JsonValueKind.Null` or `JsonValueKind.Undefined`.

One case when these can come in handy is when you are dealing with the possibility of additional properties on your object.

### Additional properties

Remember that in a JSON schema, the default behaviour is to allow additional properties on an `object`, unless you explicitly exclude them.

We can demonstrate this with an addition to our JSON text.

```csharp
string jsonText = 
"""
{
    "name": {
      "familyName": "Oldroyd",
      "givenName": "Michael",
      "otherNames": ["Francis", "James"]
    },
    "occupation": "Farrier",
    "dateOfBirth": "1944-07-14"
}
""";
```

I've added back the `givenName` property, and included an additional property called `occupation`.

Let's build and run, to verify the output.

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

We are expecting it to produce the usual output, and for the instance still to be valid.

```
Oldroyd, Michael: 14 July 1944
michaelOldroyd is valid.
```

So how can we get hold of the `occupation` property?

Well, any type which represents a JSON `object` implements our `IJsonObject<T>` interface, and that gives us the ability to inspect all of the properties of the object.

> As with `IJsonValue` you should not be using this interface directly, as it would cause your value to be boxed.

In particular, we can search for a well-known additional property with the `TryGetProperty()` method.

Let's add some code to do that. We'll insert it between the line that writes out the name and date of birth, and the code that does the validation, like this.

```csharp
Console.WriteLine($"{familyName}, {givenName}: {dateOfBirth}");

if (michaelOldroyd.TryGetProperty("occupation", out JsonAny occupation) &&
    occupation.ValueKind == JsonValueKind.String)
{
    Console.WriteLine($"occupation: {occupation.AsString}");
}

bool isValid = michaelOldroyd.IsValid();
```

`TryGetProperty` uses the familiar `TryXXX` pattern used throughout the dotnet framework. We pass in the name of the property we wish to receive (as it appears in the JSON document). If it finds such a property, it returns `true` and sets the output value.

Notice how we are also checking that the value provided is a JSON `string`, using the `ValueKind` property. If it is, we know that we can use the `JsonAny.AsString` property to convert to a string.

This is one of a family of `JsonAny` properties that convert to the primitives `JsonObject`, `JsonString`, `JsonArray`, `JsonNumber`, `JsonBoolean`, and  `JsonNull`.

> This kind of ad-hoc validation is very common in "undocumented extension" scenarios, where the schema falls short of the data actually being provided.
>
> In fact, you could navigate a whole JSON document using just our extended JSON types, the `ValueKind` property, `JsonAny.As[Primitive]`, and the `As<T>()` cast, without generating any code at all!

Build and run...

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

And we see the `occupation` added to the output.

```
Oldroyd, Michael: 14 July 1944
occupation: "Farrier"
michaelOldroyd is valid.
```

### Enumerating properties

If we aren't fishing for a well known additional property, but want to operate over whatever we find in the object, we can *enumerate* its properties directly.

Let's add a few more *additional properties* to our JSON document.

```csharp
string jsonText = 
"""
{
    "name": {
      "familyName": "Oldroyd",
      "givenName": "Michael",
      "otherNames": ["Francis", "James"]
    },
    "occupation": "Farrier",
    "selfEmployed": false,
    "salary": 26000,
    "dateOfBirth": "1944-07-14"
}
""";
```

You can see we've added a `bool` property called `selfEmployed` and a `number` property called `salary`.

Now, let's add some code to enumerate the properties in the object. Insert the following after the code that writes the `occupation` to the console.

```csharp
Console.WriteLine("Additional properties:");
foreach(JsonObjectProperty property in michaelOldroyd.EnumerateObject())
{
    Console.WriteLine($"{property.Name}: {property.Value}");
}
```

Build and run...

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

...and we can see that this code enumerates all of the properties on the `Person` object, and writes them out to the console.

```
Oldroyd, Michael: 14 July 1944
occupation: "Farrier"
Additional properties:
name: {"familyName":"Oldroyd","givenName":"Michael","otherNames":["Francis","James"]}
occupation: "Farrier"
selfEmployed: false
salary: 26000
dateOfBirth: "1944-07-14"
michaelOldroyd is valid.
```

The JSON `JsonObjectProperty` type has a `Name` property which gives us the underlying JSON form of the actual property name, as a `string`. Its `Value` property returns the value as a `JsonAny`.

> Note: the name is actually a `JsonPropertyName` which is convertible to a string, either explicitly (via a cast or a call to `(Try)GetString()`. This allows us to optimize the underlying storage mechanism for property names.

It also exposes a `ValueAs<T>()` method to get the value as a specific type of `IJsonValue`, plus `ValueKind` if you want to explore its underlying type. These are analogous to the methods on `JsonAny` but you avoid converting to `JsonAny` explicitly, just to examine property information.

The result of all this is that we have emitted our additional properties to the Console; but we've also got the "well-known" properties in this list. Is there a way to filter those out?

You'll not be surprised to learn that there is.

If we go and look at any of our code generated types, you'll see that the generator has emitted `const` fields for the well-known properties of those `object` types that have them, They are in a nested class `JsonPropertyNames`.

```csharp
/// <summary>
/// The well-known property names in the JSON object.
/// </summary>
public static class JsonPropertyNames
{
    /// <summary>
    /// JSON property name for <see cref = "DateOfBirth"/>.
    /// </summary>
    public static ReadOnlySpan<byte> DateOfBirthUtf8 => "dateOfBirth"u8;

    /// <summary>
    /// JSON property name for <see cref = "DateOfBirth"/>.
    /// </summary>
    public const string DateOfBirth = "dateOfBirth";
    /// <summary>
    /// JSON property name for <see cref = "Name"/>.
    /// </summary>
    public static ReadOnlySpan<byte> NameUtf8 => "name"u8;

    /// <summary>
    /// JSON property name for <see cref = "Name"/>.
    /// </summary>
    public const string Name = "name";
}
```

As you might expect, these fields expose both the `char` and UTF8-encoded `byte` versions of the relevant JSON property names.

We can use these to add a filter to our enumerator, to eliminate the well-known properties, and just work over the additional properties.

Replace the `foreach` loop with the following:

```csharp
foreach(JsonObjectProperty property in michaelOldroyd.EnumerateObject())
{
    if (property.NameEquals(Person.JsonPropertyNames.DateOfBirthUtf8) ||
        property.NameEquals(Person.JsonPropertyNames.NameUtf8))
    {
        // Skip the properties we already know about
        continue;
    }

    Console.WriteLine($"{(string)property.Name}: {property.Value}");
}
```

> Notice how we are using the `NameEquals()` method, with the pre-encoded `XXXUtf8` properties. This allows us to avoid allocating strings to compare property names, if we are operating on data backed by a `JsonElement`, as in this case.

OK - let's build and run again...

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

and the output looks like this:

```
Oldroyd, Michael: 14 July 1944
occupation: "Farrier"
Additional properties:
occupation: "Farrier"
selfEmployed: false
salary: 26000
michaelOldroyd is valid.
```

We're now only looking at the additional properties, and can use our tools to inspect the `ValueKind`, convert to well known types like `JsonObject`, `JsonArray`, `JsonString`, `JsonNumber`, or `JsonBoolean`; and work with them in a generic fashion.

### Preserving information

One challenge with JSON serialization and deserialization is a loss of fidelity as you roundtrip your information.

Using standard code-first serializers, if you are faced with additional properties, or schema extensions, or you are dealing with malformed data and trying to figure out what to do with it for diagnostic or self-healing scenarios, you may lose information as you transform to-and-from the dotnet world.

With most code-first C#-to-JSON (de-)serialization, you lose this characteristic unless it has been explicitly designed-in to your C# classes.

With our generated code and extended type model, we preserve as much information as possible through any transformations. That's true even when we convert between types which are not valid for the underlying data.

> There are edge cases where you can devise a conversion and manipulation process that is *not* information preserving. However, for any `JsonElement`-backed use case like this, all information is preserved between conversions.

Let's serialize our new "extended" `Person` and verify that the additional properties are preserved and written to the output.

Add the following code at the end of the file.

```csharp
Console.WriteLine(michaelOldroyd.Serialize());
```

Build and run...

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

...and we see the serialized output in the console.

```
Oldroyd, Michael: 14 July 1944
occupation: "Farrier"
Additional properties:
occupation: "Farrier"
selfEmployed: false
salary: 26000
michaelOldroyd is valid.
{"name":{"familyName":"Oldroyd","givenName":"Michael","otherNames":["Francis","James"]},"occupation":"Farrier","selfEmployed":false,"salary":26000,"dateOfBirth":"1944-07-14"}
```

Notice how the additional properties are preserved in the serialized output.

### Working with arrays

Working with arrays is very similar. Just as with `JsonArray` there is an `EnumerateArray()` method to iterate the items in the array.

To see that in action, let's revert to our starting point. Delete all the text in Program.cs and replace it with this.

```csharp
using System.Text.Json;
using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;

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

var michaelOldroyd = Person.Parse(jsonText);
```

> Notice that we are now using the `Person`-specific version of `Parse()` to avoid the implicit cast from `JsonAny`.

and now add some code to enumerate the array:

```csharp
foreach(PersonNameElement otherName in michaelOldroyd.Name.OtherNames.EnumerateArray())
{
  Console.WriteLine(otherName);
}
```

Build and run again...

```
dotnet build
.\bin\Debug\net8.0\JsonSchemaSample.exe
```

...and we see the other names written to the console.

```
Francis
James
```

> You'll notice that we are just assuming that `OtherNames` is an array type, and calling the `EnumerateArray()` method that it exposes. But what if it was in the string representation? We could always check the `ValueKind` to make sure it was safe to do so, but we'll look at more reliable techniques in our section on Union types later in this Lab.

In addition to enumeration, we can also find the `Length` of the array. We might use this to pre-allocate a working buffer of some kind, for example, before going on to enumerate the array.

We can also index directly into the array using the standard `myArray[index]` syntax.

## Creating JSON

So far, we've deserialized existing JSON data, examined it, and serialized the object back to a UTF8 output form. But what about creating new JSON entities?

### Using anonymous types

We've seen one way of quickly generating a JSON model - by deserializing a JSON string.

Another way to create JSON quickly is to use anonymous types. This is slightly less visually crufty than using a constant string, but, behind the scenes, causes a round-trip out to a UTF8 representation and back. You would not normally use it in production code, but it is great for examples.

Instead of our JSON string.

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
```

we could use anonymous types like this:

```csharp
var jsonAnon = new {
    name = new {
      familyName = "Oldroyd",
      givenName = "Michael",
      otherNames = new [] { "Francis", "James" }
    },
    dateOfBirth = "1944-07-14"
  };
```

That can be passed to an overload of `JsonAny.From<T>()` which will use the built in `System.Text.Json.Serializer` to round trip the value into our model.

```csharp
var michaelOldroyd = JsonAny.From(jsonAnon);
```

### Building a JSON document

That's all very well if you are creating a whole document in one go, but what if you want to build or compose a document from constituent parts?

In dotnet7, `System.Text.Json` added the `Nodes` namespace with [`JsonObject`](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.nodes.jsonobject?view=net-6.0) and [`JsonArray`](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.nodes.jsonarray?view=net-6.0) types to help you build JSON documents.

Our extended and generated types *do not* use `JsonObject` under the covers to create new JSON documents, but they *do* build on similar factory patterns. We use our knowledge of the JSON schema from which the types were generated to help us create semantically valid data structures.

Let's get started with the simplest entities - the primitives `string`, `boolean`, `number`, and `null`.

### Creating primitives

We've actually seen some examples of how to create instances of primitive types already.

You can either use an implicit cast from a dotnet type, or new up the object from the value. Let's have a look at some examples of doing that.

Let's delete all the text from `program.cs` for the time being, and add back our using statements.

```csharp
using System.Text.Json;
using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;
```

Then, try adding the following:

```csharp
JsonString myImplicitString = "a string, implicitly";
JsonString myExplicitString = new JsonString("a string, explicitly");
JsonNumber myImplicitNumber = 1.1;
JsonNumber myExplicitNumber = new JsonNumber(1.1);
JsonBoolean myImplicitBoolean = true;
JsonBoolean myExplicitBoolean = new JsonBoolean(true);
JsonNull myImplicitNull = default;
JsonNull myExplicitNull = new JsonNull();
JsonNull myNull = JsonAny.Null;
```

Take a bit of time to explore the other primitive types in the extended type model, like `JsonDateTime`, `JsonInteger` and `JsonEmail`, and see how they can be constructed.

### Creating arrays

Creating an instance of an array is also fairly simple. Remember that we generated an `array` type called `PersonNameElementArray`. Because it has a simple item type of `PersonNameElement`, you will see a family of static methods called `PersonNameElementArray.From()`, which take one or more `PersonNameElement` instances.

> Sometimes, the collection of items in an array cannot be described by a single schema. Perhaps there is a different schema for items at different indices, for example. In those cases, this convenience method will *not* be emitted, and you should use `JsonArray.From()` instead. See below for examples of this.

Let's use `PersonNameElementArray.FromItems()` to create an array of name elements.

```csharp
var otherNames = PersonNameElementArray.FromItems("Margaret", "Nancy");
```

The implicit conversion from `string` to `PersonNameElement` meant we avoided having to `new` elements explicitly.

Although just syntactic sugar, it makes the code much more readable. Compare with this:

*(don't add this - it is for comparison only)*
```csharp
// We avoided having to write...
var otherNames = PersonNameElementArray.FromItems(new PersonNameElement("Margaret"), new PersonNameElement("Nancy"));
```

We can also create arrays from existing collections of items. There's a general-purpose type called `JsonArray` which has static factory methods to create arrays from a variety of different primitives. For example, let's take an existing list of strings, and create a `PersonNameElementArray` from them, using `JsonArray.From(IEnumerable<string>)`

```csharp
var someNameStrings = new List<string> { "Margaret", "Nancy" };
PersonNameElementArray array = PersonNameElementArray.From(someNameStrings);
```

Or here's a version that uses an enumerable of the appropriate JSON value type.

```csharp
var someNameValues = new List<PersonNameElement> { "Margaret", "Nancy" };
PersonNameElementArray valueArray = PersonNameElementArray.FromRange(someNameValues);
```

> There are various overloads of `From()` (Which may cause serialization) and `FromRange()` (which always operates on `IJsonValue` types) to create arrays of all sorts of primitive types. Take some time to explore the overloads in the `PersonNameElementArray.Array*.cs` files to find out what is available, and what their different characteristics might be.

### Using `Create()` to create objects

Because our code generator understands the structure of `object` schema, including which properties are optional, and which are `required`, it is able to emit *factory methods* that assist in the creation of valid instances.

> Like constructors, factory methods create new instances of objects. To avoid collisions between the constructors we emit as standard, and the ones we want to create for our properties, we always generate a factory method rather than an additional constructor.
>
> One problematic case we are trying to avoid comes up when we have an object with nothing but optional properties. In that case, we would wish to generate a constructor with nothing but optional parameters. This can cause issues with the default, parameterless constructor.

Let's look at the definition of the `Create()` method emitted for `Person`.

```csharp
public static Person Create(JsonSchemaSample.Api.PersonName name, Corvus.Json.JsonDate? dateOfBirth = null)
```

`Name` is a required property, so we have to pass an instance as the first parameter. `DateOfBirth` is optional, so it is passed as a nullable value, with a default value of `null`.

Let's delete the code we've already added (apart from our `using` statements) and try to apply everything we've learned so far to create a new `Person`.

```csharp
Person audreyJones =
    Person.Create(
        name: PersonName.Create(
                givenName: "Audrey",
                otherNames: PersonNameElementArray.FromItems("Margaret", "Nancy"),
                familyName: "Jones"),
        dateOfBirth: new LocalDate(1947, 11, 7));
```

You'll notice that I've used the C# syntax that lets me specify parameter names explicitly. I like to do this when building up entities like this, as it allows me to reorder the parameters in whatever way I feel is natural, and I can just remove "optional" items, wherever they appear in the list.

For example, a minimal valid person could just be created like this:

*(don't add this code - it's just an example)*
```csharp
var minPerson = Person.Create(PersonName.Create("Jones"));
```

Or a person with neither `givenName` nor `otherNames` could be created like this:

*(again, no need to add this code)*
```csharp
Person.Create(
    PersonName.Create(
        "Jones",
        null,
        "Margaret Nancy"));
```

But it isn't nearly so clear what is being done.

Incidentally, we don't have to use the `array` form for the `otherNames` property - we could have just used the `string`. Similarly for the `dateOfBirth`, we could have used a suitable date string.

*(don't add this code - it's just an example)*
```csharp
Person.Create(
    name: PersonName.Create(
            givenName: "Audrey",
            otherNames: "Margaret Nancy",
            familyName: "Jones"),
    dateOfBirth: "1947-11-07");
```

#### Optional v. Null

If you look again at the definition of the `Create()` methods we have been using, you may spot another point of friction. Once again, it centres on the distinction between a value, a value which is `null`, and an undefined value.

When we pass (dotnet) `null` as the value for an optional parameter to one of our `Create()` methods, we are saying *do not set a value for the property*.

For example

*(don't add this code)*
```csharp
Person.Create(
  name: PersonName.Create("Jones"),
  dateOfBirth: null);
```

This represents

```json
{ "name": {"familyName": "Jones"} }
```

In order to say *set the value for this property to null* we have to use `JsonAny.Null`.

*(don't add this code)*
```csharp
Person.Create(
  name: PersonName.Create("Jones"),
  dateOfBirth: JsonAny.Null);
```

This represents

```json
{ "name": {
    "familyName": "Jones",
    },
  "dateOfBirth": null
}
```

## Modifying JSON

TODO: Immutability, With() and walking the tree.

## JSON Schema and Union types

We now know how to use our generated dotnet types in standard "serialization" scenarios. We have seen property accessors that, thanks to the implicit conversions, let us treat our JSON primitives as their dotnet equivalents: `string`, `bool`, and `null`, or even more sophisticated entities like `LocalDate`.

We've seen that object hierarchies are supported just as we'd expect for any dotnet types, but that we automatically get extensions which allow us to enumerate `array` items and `object` properties, examine the type of the values we discover, and determine whether properties are present or not.

Now, we're going to have a look at how we represent some more sophisticated JSON schema constraints. To do that we are going to examine the `otherNames` property on a `PersonName`.

First, revert our code back to just our original example person, parsed from some JSON text.

```csharp
using System.Text.Json;
using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;

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

var michaelOldroyd = Person.Parse(jsonText);
```

Now, let's look at the `OtherNames` property in a bit more detail.

We know from the schema that it can be an array type (`PersonNameElementArray`), or a string type (`PersonNameElement`). But how do we know which to choose?

We've already seen the basic techniques available to use - we used them when we explored array enumeration, earlier.

Let's remind ourselves of those basic techniques.

We could check to see if the `ValueKind` is `Array` and cast on that basis.

```csharp
if (michaelOldroyd.Name.OtherNames.ValueKind == JsonValueKind.Array)
{
    JsonArray otherNames = michaelOldroyd.Name.OtherNames.AsArray();
}
```

or, because we know that is (or should be!) a `PersonNameElementArray` we could explicitly cast to that type.

```csharp
if (michaelOldroyd.Name.OtherNames.ValueKind == JsonValueKind.Array)
{
    PersonNameElementArray otherNames = michaelOldroyd.Name.OtherNames.As<PersonNameElementArray>();
}
```

Both of those techniques depend on us knowing that "being an array type" is enough to tell us how to treat the value. That means having an intimate knowledge of the existing schema.

A more general approach might be to cast to the desired target type (`PersonNameElementArray`) and check whether it is valid

```csharp
if (michaelOldroyd.Name.OtherNames.As<PersonNameElementArray>().IsValid())
{
    PersonNameElementArray otherNames = michaelOldroyd.Name.OtherNames.As<PersonNameElementArray>();
}
```

That's a little better, but it still depends on us knowing what type the property *might* be (i.e. remembering what is in the schema).

> Both of these are the kind of thing you might do with `JsonAny` with unschematized data. (Or, indeed, the raw `JsonElement` itself.)

Fortunately, the code generator has helped us out with some additional members on our `OtherNames` type.

Let's remind ourselves of the schema for `OtherNames` again.

```json
"OtherNames": {
    "oneOf": [
        { "$ref": "#/$defs/PersonNameElement" },
        { "$ref": "#/$defs/PersonNameElementArray" }
    ]
}
```

Because it uses the `oneOf` syntax, the code generator has recognized that `OtherNames` is a *union* of the `PersonNameElement` and `PersonNameElementArray` types.

So it has generated us some properties of the form `Is<UnionTypeName>` and `As<UnionTypeName>`.

In this case, they are `IsPersonNameElement` and `AsPersonNameElement`, and their array equivalents `IsPersonNameElementArray` and `AsPersonNameElementArray`.

So, the most discoverable way to write the code above might be

```csharp

OtherNames otherNames = michaelOldroyd.Name.OtherNames;
if (otherNames.IsPersonNameElementArray)
{
    PersonNameElementArray otherNamesArray = otherNames.AsPersonNameElementArray;

    // Use the array
    otherNamesArray.EnumerateArray();
}
```

This test-and-use pattern is so common, that we also add a conditional cast method.

```csharp
if (michaelOldroyd.Name.OtherNames.TryGetAsPersonNameElementArray(out PersonNameElementArray otherNamesArray))
{
    // Use the array
    otherNamesArray.EnumerateArray();
}
```