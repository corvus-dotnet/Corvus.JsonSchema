TL;DR - this is a getting started Hands-On-Lab that walks you through our new JSON Schema code generation library and tools for C#.

In my [previous post](https://endjin.com/blog/2021/05/csharp-serialization-with-system-text-json-schema), I introduced the concept behind our JSON object model extensions, built over [System.Text.Json](https://docs.microsoft.com/en-us/dotnet/api/system.text.json?view=net-6.0).

We also looked at how a code generation tool could take JSON Schema and emit a full-fidelity dotnet type model for that schema, including well-optimised schema validation, with great allocation and compute performance for common scenarios where traditional JSON serialization would be the norm.

Finally, we looked at how this model supports interoperability between types generated from different schema, and even compiled into different libraries, without any shared user code.

I'm pleased to say that we've now published our initial preview release of this tooling over on github/nuget. This is the [library containing the core extensions to the JSON object model](https://www.nuget.org/packages/Corvus.Json.ExtendedTypes) and this is the [tool which generates code from JSON-schema definitions](https://www.nuget.org/packages/Corvus.Json.JsonSchema.TypeGeneratorTool) (including those embedded in OpenAPI 3.1 documents).

If you want to incorporate this into your tool chain, read on!

## Prerequisites

You'll need

- the [.NET 6 SDK](https://docs.microsoft.com/en-us/dotnet/core/sdk) (maybe you've already installed Visual Studio 2022 and acquired it that way; but you don't have to install Visual Studio to get started; you can download these SDK bits and use the completely free/OSS toolchain to follow along.)
- a shell, with the SDK developer tools in the path. I'm using [PowerShell](https://docs.microsoft.com/en-us/powershell/scripting/install/installing-powershell?view=powershell-7.2) in the [Windows Terminal](https://docs.microsoft.com/en-us/windows/terminal/install), [configured with the Visual Studio "Developer" config for PowerShell](https://blog.yannickreekmans.be/developer-powershell-command-prompt-visual-studio-windows-terminal/).
- A text editor or IDE. I'm using [VS Code](https://code.visualstudio.com/).

## Getting started

First, you need to install the code generator. I choose to do so globally. From a developer command prompt, use the following syntax:

```
dotnet tool install --global Corvus.Json.JsonSchema.TypeGeneratorTool
```

We'll also create a console app to host our sample, using dotnet6.0 (the current LTS version as of the time of writing.)

```
dotnet new console -o JsonSchemaSample -f net6.0
cd JsonSchemaSample
```

We now want to add a reference to our JSON object model extensions to the project. We can use the `dotnet add` command to do that at the command line, or you could use your favourite package manager or IDE.

```
dotnet add package Corvus.Json.ExtendedTypes
```

First, let's inspect `JsonSchemaSample.csproj`.

```
code JsonSchemaSample.csproj
```

> Remember, I'm using [VS Code](https://code.visualstudio.com/Download). But you can use whatever tooling you like.

It should look something like this. Notice that we have a package reference to `Corvus.Json.ExtendedTypes`.

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net6.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Corvus.Json.ExtendedTypes" Version="0.1.8" />
  </ItemGroup>

</Project>
```

## Designing with json-schema

Now, we are going to create a JSON schema document. This one is a simple representation of a person. Here's the whole thing, and we'll break it down in more detail in a second. 

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
    }
  }
}
```

Let's get that into the solution. Again, these steps are using VS Code, but you can use whatever IDE and/or tools you like. We're going to create an `api` folder and create a `person-from-api.json` document in there.

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

> If people wanted to extend the tools and libraries to support backlevel schema versions, it would not be too difficult; the older revisions are largely subsets of the later ones. It's well outside the scope of this introductory tutorial, but [PRs are gratefully received](https://github.com/corvus-dotnet/Corvus.JsonSchema)! 

You'll then notice that I'm not defining any particular object at the root level - the interesting types are all in the `$defs` section.

This is a matter of style and habit - I tend to use document fragments that are then included by `$ref` in other places (e.g. OpenAPI documents). So everything goes in the `$defs` section.

> This does have some implications for how code is generated. Personally I prefer what is emitted if you do things this way, but your mileage may vary! We'll see the differences later on. 

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

`otherNames` is defined by our `OtherNames` schema fragment, and that is a bit more interesting.

```json
"OtherNames": {
        "oneOf": [
            { "$ref": "#/$defs/PersonNameElement" },
            { "$ref": "#/$defs/PersonNameElementArray" }
        ] 
    }
```

This says that it will be *either* a `PersonNameElement` *or* a `PersonNameElementCollection`.

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

So, if a person's full name was `Michael Francis James Oldroyd`, I could represent that in schema-valid JSON as

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

> This is a really good reason to use a schema-first design approach. C# developers may not have thought of this pattern when extending their API with a code-first approach. *Either-this-or-that* (a form of [union](https://en.wikipedia.org/wiki/Union_type)) is not a language-supported idiom, but it is frequently useful in the rest of the universe! Esepcially in JSON schema, where it is *everywhere*. You'll see more of this later.

Anyway, whatever the pros and cons of this design, that's what our API does!

So, now, let's generate some code...

## Generating C# code

The tool has installed a dotnet executable for us, so let's see how to use it. At the command prompt, type

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

First, we have an option to define the `--rootNamespace` in which our types will be generated.

> You'll see later that only some of our types will be emitted directly into that namespace - many will become *nested types* embedded in their parent type. This helps us to keep the namespace clean, and minimize type name clashes.

In this case we will use `JsonSchemaSample.Api` to match our project name and folder path.

Second, we can provide a `--rootPath` to find a schema for which to generate code, somewhere in the document. This is in the same [JSON Pointer format](https://www.rfc-editor.org/rfc/rfc6901) that you are familiar with for `$ref` instances inside the document, in the standard *URI Fragment Identifier Representation*. Note also that in most terminals, you will have to wrap it in single quotes to ensure that the command line is parsed correctly.

We will use `'#/$defs/Person'`.

There's one more slighlty annoying thing to do. In this preview version, we don't support inferring the json schema version from the document itself, so we also need to provide that explicity. For us that will be `--useSchema Draft202012`.

Finally, we need to provide the path to the schema document itself. For us, we are in the same directory as the file concerned, so it will be just `person-from-api.json`.

> Note that any references to documents either in this parameter on the command line, or in `$ref`s in the documents themselves don't *have* to be in the local file system. You can happily use `http[s]` references to external documents, and it'll work just fine!
>
> We'll see this in action as we develop our example further.

The other defaults mean that we will generate our output files in the same folder as the input schema file (*not* the current working directory, so you can supply deep paths to the source file, without needing an explicit `--outputPath` on your command line).

There will be one file per type that is emitted into the root namespace, named for the single enclosing type in that file.

So we end up with the command.

```
generatejsonschematypes --rootNamespace JsonSchemaSample.Api --rootPath '#/$defs/Person' --useSchema Draft202012 person-from-api.json
```

Let's run that now. When it has completed, list the files in the directory, using whatever command is appropriate for your shell.

```
ls *.cs
```

> Remember I'm using Powershell, so, as with Linux distros, I have `ls`. Windows Command Prompt users might want `dir`. 

You should see the following file names listed (plus whatever other detail your particular shell adds to its output):
```
Name
----
OtherNames.cs
Person.cs
PersonName.cs
PersonNameElement.cs
PersonNameElementArray.cs
```

So far so good. Let's have a look at the generated types in more detail.

## The generated types

The first thing that you'll probably notice is that it has generated files for all of the schema elements that the `Person` schema referenced, that were found in the `$defs` section of the root, plus the `Person` schema itself.

> The rule the tool is using is that anything that appears in the `$defs` section at the root of the schema will be in the root namespace. Anything else will be nested in the type generated for its parent scope. We'll go into that in more detail later.

Before we dive into the details, let's build the code and find out what we can do with it.

## Building

We need to change directory back up into the root of our project, and run the build tool.

> If you are working with an IDE, you could load the project and build it in there, but we're following along with the command line tools.

```
cd ..
dotnet build
```

This will have emitted an executable in the bin folder. We can run it...

```
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

 and check we get the standard `Hello, World!` message.

 ```
 Hello, World!
 ```

So far so good. Now, let's put the types we've generated to work.

## Consuming JSON - "Deserialization"

A very common scenario is consuming and working over a JSON payload provided by some service, using dotnet types.

This is often called "deserialization", and consists of taking some UTF8-encoded JSON text payload and turning it ito a representation in the dotnet type system.

Generally, this requires us to bring all or part of a data stream into memory, and then construct C# objects from it.

`System.Text.Json` has a very efficient way of doing this using its [`JsonElement`](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement?view=net-6.0) and related entities. These are immutable [value types](https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/value-types) that provide thin wrappers over the underlying UTF8 bytes, converting into dotnet primitives like `string`, `long`, or `bool` at the point of use.

Our [Corvus.Json.ExtendedTypes library](https://www.nuget.org/packages/Corvus.Json.ExtendedTypes) extends this model to include types which represent all of the JSON-schema primitives and common extensions, including arrays, objects, an assortment of numeric and date types etc.

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

Instead, let's add a using statement for our generated code. Recall that we put it in the `JsonSchemaSample.Api` namespace. So let's add that. We'll also need `System.Text.Json`, `Corvus.Json`, and, for our date work, [`NodaTime`](https://nodatime.org/).

> You can use the internal dotnet Date and Time classes, but NodaTime is a lot better in general, and specfically a better fit for the JSON date/time specifications. We hope this changes in dotnet vFuture!

```csharp
using System.Text.Json;
using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;
```

Now, let's explicitly create a `JsonDocument` from our sample JSON text.

[`JsonDocument.Parse`](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.jsondocument.parse?view=net-6.0) offers several overloads for parsing streams, blocks of bytes, and plain old strings. We'll use the string parser for the purposes of this exercise.

Add this code beneath your `using` statements.

```csharp
string jsonText = @"{
    ""name"": {
      ""familyName"": ""Oldroyd"",
      ""givenName"": ""Michael"",
      ""otherNames"": [""Francis"", ""James""]
    },
    ""dateOfBirth"": ""1944-07-14""
}";

using JsonDocument document = JsonDocument.Parse(jsonText);
```

You'll see in a moment that we don't *have* to indirect through `JsonDocument` to create instances of our types; there are tools in the library which can abstract this away for you. But I think it is useful to start out by seeing a little bit behind the curtain, so it doesn't look *too* much like magic.

Hopefully, you should recognize that JSON text as something that we expect to be valid according to our `Person` schema.

So let's wrap that element in our `Person` type. That's simple enough.

Add the following line of code:

```csharp
Person michaelOldroyd = new Person(document.RootElement);
```

Now we can access the elements of that JSON payload via our `Person` type.

```csharp
string familyName = michaelOldroyd.Name.FamilyName;
string givenName = michaelOldroyd.Name.GivenName;
LocalDate dateOfBirth = michaelOldroyd.DateOfBirth;

Console.WriteLine($"{familyName}, {givenName}: {dateOfBirth}");
```

Notice how we're using normal property accessors, and dotnet types like `string` and NodaTime's `LocalDate`.

Let's build and run that again.

```
dotnet build
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

As we'd hope, it produces (something like) the following output:

```
Oldroyd, Michael: 14 July 1944
```

> If you are wondering about variations in the date formatting, that is the result of the [`NodaTime.LocalDate`](https://nodatime.org/3.0.x/api/NodaTime.LocalDate.html) `ToString()` implementation.

So far, so "just like serialization". But - and here's the rather nifty thing about this - creating the wrapper didn't allocate anything. It made a bit of space on the stack for the `Person` value type, which, internally, has a field for the `JsonDocument.RootElement`. And because those are both `readonly struct`, it didn't even need to make a copy of that value.

It's true that we then went on to allocate a bunch of strings when we accessed the bits we were interested in, and passed them to `Console.WriteLine()`

> There are optimizations that could be done, at the expense of ease-of-use. In particular, we're expecting to see a vFuture version of `System.Text.Json` where it is better able to expose the underlying data without unecessary string allocation.

But anything we didn't use remained entirely untouched. This is very powerful.

## Serialization

We can also write our entity back to an output of some kind.

There are two ways to do this.

First, any `IJsonValue` has a `WriteTo()` method that takes a `Utf8JsonWriter`.

You'd use it something like this (but don't add this code):

```csharp
// Get a writer from somwehere (e.g. an HttpRequest output stream)
Utf8JsonWriter writer;
michaelOldroyd.WriteTo(writer);
```

But we also provide a simple `Serialize()` extension method to turn your output into a string.

We can use that to confirm that our JSON round-trips correctly.

Add the following code:

```csharp
string serializedOldroyd = michaelOldroyd.Serialize();
Console.WriteLine(serializedOldroyd);
```

When we build and run...

```
dotnet build
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

We get the following output:

```
Oldroyd, Michael: 14 July 1944
{"name":{"familyName":"Oldroyd","givenName":"Michael","otherNames":["Francis","James"]},"dateOfBirth":"1944-07-14"}
```

> Obviously, it is more efficient to use `WriteTo()`, rather than the `Serialize()` codepath, as the former avoids unnecessarily allocating strings.
>
> In fact, for many common cases, using `WriteTo()` means that you will just be writing the underlying UTF8-encoded byte buffers directly into the output, even when you have modified and composed JSON content from multiple sources.

Once you've finished exploring that, you can delete those two lines before we move on. We'll re-introduce serialization again later.

(*lines to delete*)
```csharp
string serializedOldroyd = michaelOldroyd.Serialize();
Console.WriteLine(serializedOldroyd);
```

## Introducing JsonAny

So we've now seen a simple example of roundtripping our JSON data to- and from- our generated type system.

One thing I mentioned earlier was that you don't have to go via `JsonDocument` to get JSON text deserialized into our generated types. There is a a type in the `Corvus.Json.ExtendedTypes` library called `Corvus.Json.JsonAny`. This represents any JSON value, and it has a family of static methods called `Parse()`, used for parsing JSON text. These are analogous to `JsonDocument.Parse()`.

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
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

...and we should see the same result.

```
Oldroyd, Michael: 14 July 1944
```

> ### The same but different
> 
> It is worth pausing for a quick side note on performance characteristics. Feel free to skip ahead if you don't really care right now.
>
> This code is the same *in effect*, and slightly simpler to write, but a little different under the hood.
>
> Because we have not explicitly created the `JsonDocument` we are no longer in control of its lifetime.
>
> The `JsonAny.Parse()` implementation has used [`JsonElement.Clone()`](https://docs.microsoft.com/en-us/dotnet/api/system.text.json.jsonelement.clone?view=net-6.0) to give us a backing `JsonElement` that outlives the original `JsonDocument`, and has disposed of that underlying document for us.
>
> This creates a clone of the relevant segment of the underlying document, and relies on GC to clean it up when it is no longer in use.
>
> In high-performance applications, you would want to control the lifetime of the `JsonDocument` yourself.

### Converting to and from `JsonAny`

All types in `Corvus.Json` that represent JSON elements are `readonly struct`, and implement an interface called `IJsonValue`.

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

`Person` has implicit conversion operators to- and from- `JsonAny` to give us an optimised means of converting in either direction.

We've actually used other implicit conversions several times already in the code we've written.

Look again at the code that is accessing the values to output them to our Console:

```csharp
string familyName = michaelOldroyd.Name.FamilyName;
string givenName = michaelOldroyd.Name.GivenName;
LocalDate dateOfBirth = michaelOldroyd.DateOfBirth;
```

we've happily written `string` and `LocalDate` - but what type *is* the value returned from `michaelOldroyd.Name.FamilyName` or `michaelOldroyd.DateOfBirth`?

Here's how the first of those is declared:

```csharp
 public JsonSchemaSample.Api.PersonNameElement FamilyName
 ```

So that's a `PersonNameElement` - but we can convert it implicitly to a C# `string`.

The code generator knows that the schema defined `PersonNameElement` can be represented as a `string` primitive, so it generated conversions for us, which make it simple to use in idiomatic dotnet code.

Similarly for `PersonName.DateOfBirth`

```csharp
public Corvus.Json.JsonDate DateOfBirth
```

`JsonDate` is part of our extended JSON type model and it is implicitly covertible to `NodaTime.LocalDate` for the same reason. 

So conversions are really useful in that they let us write idiomatic dotnet code, while maintaining the benefits of our JSON data model. But there's another important consequence of this feature.

Remember our conversion rules:

1. An instance of any type that implements `IJsonValue` can be converted to an instance of `JsonAny`
1. An instance of `JsonAny` can be converted to an instance of any type that implements `IJsonValue`

The transitive nature of these statements leads to a significant corollary:

3. An instance of any type that implements `IJsonValue` can be converted into an instance of any other type that implements `IJsonValue`.

What?! What??!! An instance of any type that implements `IJsonValue` can (via explicit conversion to `JsonAny` in the worst case), be converted to an instance of any other type that implements `IJsonValue`? Regardless of the shape, structure etc? Without `TypeA` having any knowledge of the existence of `TypeB`?

Yes. That's absolute true.

In fact, for your convenience, there is a 'casting' extension method that lets you perform exactly that anything-to-anything conversion.

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
- "if it looks like this, then it will also look like that, otherwise - - it looks like this other thing" (`if`/`then`/`else`)
- "if it has one of these properties, it must look like this" (`dependentSchemas`)
- "it must be a number or an object" (`sarray of primitive types`)

When we construct an instance of one of our JSON types from some JSON data, we know we can safely use it via that type, if it is *valid* according to the schema from which the type was generated.

Fortunately (but not coincidentally!), the code generator emits a `Validate()` method to test for this.

Let's try validating our `Person`.

```csharp
bool isValid = michaelOldroyd.IsValid();
Console.WriteLine($"michaelOldroyd {(isValid ? "is" : "is not")} valid.");
```

The first thing you'll notice is that we didn't actually call the `Validate()` method I was just talking about!

`Validate()` is capable of returning a whole lot of diagnostic information about failures, about which we don't care in this case.

 Instead, we use the `IsValid<T>()` extension method to return a boolean `true`/`false` value, which is simpler to work with.

 If we build and run again...

```
dotnet build
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

we see the output:

```
Oldroyd, Michael: 14 July 1944
michaelOldroyd is valid.
```

Which is just as we expected.

What if we now deliberately create an invalid document.

Let's add the following code:

```csharp
string invalidJsonText = @"{
    ""name"": {
      ""givenName"": ""Michael"",
      ""otherNames"": [""Francis"", ""James""]
    },
    ""dateOfBirth"": ""1944-07-14""
}";

Person invalidOldroyd = JsonAny.Parse(invalidJsonText);
bool isValid2 = invalidOldroyd.IsValid();
Console.WriteLine($"invalidOldroyd {(isValid2 ? "is" : "is not")} valid.");
```

Notice that we have omitted the `familyName` from the `name` object. This makes it invalid according to the schema (because `familyName` is a `required` property.) 

Build and run...

```
dotnet build
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

...and we see

```
Oldroyd, Michael: 14 July 1944
michaelOldroyd is valid.
invalidOldroyd is not valid.
```

Which is as we expected. But, in fact, even though it is invalid, we can still manipulate the parts of the entity that *are* valid, through this type system. To do that we need to understand a little bit more about how data is represented.

## Values, Null, and Undefined

Let's add a bit of code to inspect the `invalidOldroyd`.

```csharp
string givenName2 = invalidOldroyd.Name.GivenName;
LocalDate dateOfBirth2 = invalidOldroyd.DateOfBirth;

Console.WriteLine($"{givenName2}: {dateOfBirth2}");
```

Build and run...

```
dotnet build
.\bin\Debug\net6.0\JsonSchemaSample.exe
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

 But what about the missing data? How do we deal with that.

This is not just a problem for *invalid* schema. We have to be able to deal with optional data in our schema, too.

Let's delete our "invalid data" code, and go back to our valid JSON text.

(*These are the lines to remove*)
```csharp
string invalidJsonText = @"{
    ""name"": {
      ""givenName"": ""Michael"",
      ""otherNames"": [""Francis"", ""James""]
    },
    ""dateOfBirth"": ""1944-07-14""
}";

Person invalidOldroyd = JsonAny.Parse(invalidJsonText);
bool isValid2 = invalidOldroyd.IsValid();
Console.WriteLine($"invalidOldroyd {(isValid2 ? "is" : "is not")} valid.");

string givenName2 = invalidOldroyd.Name.GivenName;
LocalDate dateOfBirth2 = invalidOldroyd.DateOfBirth;

Console.WriteLine($"{givenName2}: {dateOfBirth2}");
```

Now, let's adjust our valid JSON text, removing the optional `givenName` property.

```csharp
string jsonText = @"{
    ""name"": {
      ""familyName"": ""Oldroyd"",
      ""otherNames"": [""Francis"", ""James""]
    },
    ""dateOfBirth"": ""1944-07-14""
}";
```

When we build and run...

```
dotnet build
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

We see...

```
Oldroyd, : 14 July 1944
michaelOldroyd is valid.
```

So what's happened? Well, as we expected, `michaelOldroyd` is still valid.

But we've got a missing `givenName`, which leads to a trailing comma in our output. What can we about that?

Remember that JSON schema values can be present (that's the value), present but *null*, or not present at all (which we call *undefined*). This is a little different from dotnet properties which are typically only present or (if nullable) `null`.

> These latter cases correspond with the `JsonValueKind.Null` and `JsonValueKind.Undefined` values for `JsonElement.ValueKind`.
>
> We expose the same values from the `IJsonValue.ValueKind` property.

We can distinguish these conditions with a few extension methods.

Let's replace the code that extracts the given name with the following:

```csharp
string givenName =
    michaelOldroyd.Name.GivenName.IsNotNullOrUndefined() 
        ? michaelOldroyd.Name.GivenName
        : "[no given name specified]";
```

We are using the `IsNotNullOrUndefined()` extension to determine whether we actually have the optional value or not. If not, we will inject some appropriate text.

This time, when we build and run...

```
dotnet build
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

We get:

```
Oldroyd, [not specified]: 14 July 1944
michaelOldroyd is valid.
```

There is a family of other similar extensions like `IsNull()`, and `IsUndefined()` for you to explore.

These can come in handy when you are dealing with the possibility of additional properties on your object.

### Additional properties

Remember that in a JSON schema, the default behaviour is to allow additional properties on an `object`, unless you explicitly exclude them.

We can demonstrate this with an addition to our JSON text.

```csharp
string jsonText = @"{
    ""name"": {
      ""familyName"": ""Oldroyd"",
      ""givenName"": ""Michael"",    
      ""otherNames"": [""Francis"", ""James""]
    },
    ""occupation"": ""Farrier"",
    ""dateOfBirth"": ""1944-07-14""
}";
```

I've added back the `givenName` property, and included an additional property `occupation`.

Let's build and run, to verify the output.

```
dotnet build
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

We are expecting it to produce the usual output, and for the instance to be valid.

```
Oldroyd, Michael: 14 July 1944
michaelOldroyd is valid.
```

So how can we get hold of the `occupation` property?

Well, any type which represents a JSON `object` implements our `IJsonObject<T>` interface, and that gives us the ability to inspect all of the properties of the object.

In particular, we can search for a well-known additional property if we wish, with the `TryGetPropertyMethod()`.

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

`TryGetProperty` uses the familiar `TryXXX` pattern used throughout the dotnet framework. Notice how we are also checking that the value provided is a string, using the `ValueKind` property. If it is, we know that we can use the `JsonAny.AsString` property to convert to a string.

> This kind of ad-hoc validation is very common in "undocumented extension" scenarios, where the schema falls short of the data actually being provided.

Build and run

```
dotnet build
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

And we see the occupation added to the output.

```
Oldroyd, Michael: 14 July 1944
occupation: Farrier
michaelOldroyd is valid.
```

### Enumerating properties

If we aren't fishing for a well known additional property, but want to operate over whatever we find in the object, we can *enumerate* the properties in the object.

Let's add a few more additional properties to our JSON document.

```csharp
string jsonText = @"{
    ""name"": {
      ""familyName"": ""Oldroyd"",
      ""givenName"": ""Michael"",    
      ""otherNames"": [""Francis"", ""James""]
    },
    ""occupation"": ""Farrier"",
    ""selfEmployed"": false,
    ""salary"": 26000,
    ""dateOfBirth"": ""1944-07-14""
}";
```

You can see we've added a `bool` property called `selfEmployed` and a `number` property called `salary`.

Now, let's add some code to enumerate the properties in the object. Insert the following after the code that write the occupation to the console.

```csharp
Console.WriteLine("Additional properties:");
foreach(Property property in michaelOldroyd.EnumerateObject())
{
    Console.WriteLine($"{property.Name}: {property.Value}");
}
```

Build and run...

```
dotnet build
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

...and we can see that this code enumerates all of the properties on the `Person` object, and writes them out to the console.

```
Oldroyd, Michael: 14 July 1944
occupation: Farrier
Additional properties:
name: {"familyName":"Oldroyd","givenName":"Michael","otherNames":["Francis","James"]}
occupation: "Farrier"
selfEmployed: false
salary: 26000
dateOfBirth: "1944-07-14"
michaelOldroyd is valid.
```

Notice that it is provided with the underlying JSON form of the actual property names. The the string formatter uses `ToString()` under the covers, and the default implementation of that serializes the value to the string.

You can see that this has emmitted our additinal properites, but we've also got the "well-known" properties in this list. Is there a way to filter those out?

You'll not be surprised to learn that there is.

If we go and look at any of our code generated types, you'll see that the generator has emitted `const` fields for the well-known properties of any `object`. They take the form `<PropertyName>JsonPropertyName` and `<PropertyName>Utf8JsonPropertyName`.

As you might expect, these fields expose the `char` and UTF8-encoded `byte` versions of the relevant JSON property name.

We can use these to add a filter to our enumerator, to eliminate the well-known properties, and just work over the additional properties.

Replace the `foreach` loop with the following:

```csharp
foreach(Property property in michaelOldroyd.EnumerateObject())
{
    if (property.NameEquals(Person.DateOfBirthUtf8JsonPropertyName.Span) ||
        property.NameEquals(Person.NameUtf8JsonPropertyName.Span))
    {
        // Skip the properties we already know about
        continue;
    }
    
    Console.WriteLine($"{property.Name}: {property.Value}");
}
```

> Notice how we are using the `NameEquals()` method, with the pre-encoded `UTF8JsonPropertyName` properties. This allows us to avoid allocating strings to compare property names, if we are operating on data backed by a `JsonElement`, as in this case.

OK - let's build and run again...

```
dotnet build
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

and the output looks like this:

```
Oldroyd, Michael: 14 July 1944
occupation: Farrier
Additional properties:
occupation: Farrier
selfEmployed: false
salary: 26000
michaelOldroyd is valid.
```

We're now only looking at the additional properties, and can use our tools to inspect the `ValueKind`, convert to well known types like `JsonObject`, `JsonArray`, `JsonString`, `JsonNumber`, or `JsonBoolean`; and work with them in a generic fashion.

### Preserving information

It's important to note that, as far as is possible, we preserve information with conversions and extensions.

> There are edge cases where you can devise a conversion and manipulation process that is *not* information preserving. Howver, for any `JsonElement`-backed use case like this, all information is preserved between conversions.

Let's serialize our new "extended" `Person` and verify that the additional properties are correctly written to the output. 

Add the following code at the end of the file.

```csharp
Console.WriteLine(michaelOldroyd.Serialize());
```

Build and run...

```
dotnet build
.\bin\Debug\net6.0\JsonSchemaSample.exe
```

...and we see the serialized output in the console.

```
Oldroyd, Michael: 14 July 1944
occupation: Farrier
Additional properties:
occupation: Farrier
selfEmployed: false
salary: 26000
michaelOldroyd is valid.
{"name":{"familyName":"Oldroyd","givenName":"Michael","otherNames":["Francis","James"]},"occupation":"Farrier","selfEmployed":false,"salary":26000,"dateOfBirth":"1944-07-14"}
```

Notice how the additional properties are preserved in the serialized output.

## JSON Schema and Union types

We're now reasonably confident about using our generated dotnet types for the standard json primitives like `object`, `string`, `bool` and `number`. We've seen how to enumerate `object` properties, examine the type of the values we discover, and determine whether properties are present or not. 

Now, we're going to have a look at how represent some more sophisticated JSON schema constraints, and to do that we are going to look at the `otherNames` property on a `PersonName`.


---

[TODO: This comes much later]

## Under the hood

Now that we've seen what we get from our basic code generation, let's take a deeper dive into what has happened under the hood, before we move on to look at some more advanced use cases. If you want to skip ahead, and come back to this later, feel free - but I think it is useful to get a better understanding of what is happening at this stage. It will help you in your JSON Schema design, and in understanding why 3rd party schema are producing the results they do.

### Naming

You have seen that the tool has picked type names based on the structure of the schema document - in this case the property names in the `$defs` section, converted into valid, style-compliant C# names.

There is a heuristic to generate the "best" names it can, based on the structure of the document. So even a random third-party schema definition should produce fairly nice-looking, self-explanatory code.

> In general, you should favour *named, referenced elements* over *inline schema* to produce the most readable, self-documenting type and property names.
>
> Use standard JSON conventions for naming in your schema - the tool will automatically translate into C# conventions for you, and avoid name collisions.

### What actually gets generated?

The code generator walks the JSON schema document, starting at the provided `--rootPath`, and follows all the references it finds from that point on.

This means that if we had entities defined in our Json Schema document that were *not* referenced by the entity we pick as our `--rootPath`, the tool would *not* generate code for them.

This allows you to generate subsets of types for a given fragment of schema, and is one reason we encourage the `$defs` approach for all schema.

If you reference the root of a document, you will walk the whole schema including the `$defs` in the root and generate types for everything in the document whether they are "needed" or not.

> There are good reasons why you might want to take either approach - but recall that you *don't* have to generate all your types at the same time, into the same library, for them to be interoperable. So the need for "holisitc" generation is considerably reduced, and we recommend this "partial document" approach.

### Nested types and namespaces
Another consequence of choosing one style over the other is that the code generation will be slightly different.

Recall the rule I mentioned above. Types are generated as nested types of their most immediate parent type. If no type is to be generated for any parent, then they are generated into the root namespace.

So, a C# type generated from a schema placed in the `$defs` section of the root, and `$ref`-erencing schema in that same `$defs` section will, if set as the `--rootPath` to generate will have its type placed into the root namespace, and those referenced types will *also* be placed into the root alongside it (because no type has been generated for the schema that contains that `$defs` section). This is the case we have just seen.

Whereas the *same schema* placed at the root of schema document will be placed into the root namespace, and `$ref`-erences that it makes to schema in its own `$defs` section will then be nested types of that root type (because its `$defs` section is scoped to a type that *has* been generated - the type found at the schema root.)

Let's give that a try.

First, edit the schema to move the `Person` schema up to the root. For your convenience, I've reproduced that here.

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "JSON Schema for a Person entity coming back from a 3rd party API (e.g. a storage format in a database)",

  "type": "object",  
  "required":  ["name"],
  "properties": {
    "name": { "$ref": "#/$defs/PersonName" },
    "dateOfBirth": {
      "type": "string",
      "format": "date"
    }
  },

  "$defs": {
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
    }
  }
}
```

Let's delete the types we generated before. I'm using PowerShell as my terminal, so I can delete the C# files with:

```
rm *.cs
```

If we now generate types for that schema, we don't need a `--rootPath`, but we *do* want to provide an explicit `--outputRootTypeName` (or we will get a fairly boring default value).

```
generatejsonschematypes --rootNamespace JsonSchemaSample.Api --outputRootTypeName Person --useSchema Draft202012 person-from-api.json
```

If you list the generated files now, you'll see that we only have 1 file.

```
Name
----
  Person.cs
```

If you inspect the contents of that file, you'll find that we have our Person type at the root

```csharp
public readonly struct Person
```

with nested type definitions called things like

```csharp
public readonly struct PersonNameElementValue
```

and

```csharp
public readonly struct PersonNameElementValueArray
```

#### Common suffixes

Why the difference in naming from the types that appeared in the root namespace? Where do these `Value` and `Array` suffixes come into it?

This was a choice to maintain consistency in naming.

For schema defintions for properties, we generate the type name for the nested type based on the property name and the suffix `Value` (for a simple `string`, `number`, or `bool`); or `Entity` (for more complex types). Schemas that are simple `array`s will typically be named for the name of the type generated from its `items` schema, with the suffix `Array`.

The suffix helps us avoid the naming clash between the property name and the type of the property that would otherwise occur.

When those nested types are defined in `$ref`-enced schema, we simply take the property name we would have generated if it were emitted at the root, and append the corresponding suffix.

We find that this helps give you some cognitive clues as to where types are defined and help prevent you getting lost in what can be quite complex type hierarchies for larger schema.