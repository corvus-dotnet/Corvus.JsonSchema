---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "Introduction"
---
## Introduction

In this tutorial, you will learn how to define a JSON Schema, generate strongly-typed C# structs from it, and use those types to parse, query, create, mutate, serialize, and validate JSON data — all with zero-allocation performance on the hot path.

We will cover:

- **Defining a schema** — writing a JSON Schema that describes a `Person` with nested objects, arrays, and composition types
- **Project setup** — installing the NuGet package and configuring the source generator
- **Using generated types** — parsing JSON, accessing properties, working with arrays, pattern matching on `oneOf` variants, and converting to .NET types
- **Creating and mutating** — building documents from scratch, modifying properties and arrays, copying values between documents, and serializing with pooled writers
- **Validation** — checking data against the schema and collecting detailed error reports
