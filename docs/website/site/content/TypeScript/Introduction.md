---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-06-29T00:00:00.0+00:00
Title: "Introduction"
---
## Introduction

Corvus.Text.Json generates idiomatic, high-performance **TypeScript** from a JSON Schema. From one schema you get a `readonly` type surface, ahead-of-time-compiled validators, and a byte-level construction and mutation API — the same engine that powers the C# generator, emitting TypeScript instead.

This guide walks you through it end to end. You will install the tools, write a schema, generate a module, and then build, validate, and read a value with the generated types.

The code generator is a .NET command-line tool, but the code it produces is plain TypeScript with a small runtime library — your project does not depend on .NET at run time.

We will cover:

- **Installing the tools** — the .NET code-generation CLI and the npm runtime package
- **Defining a schema** — a JSON Schema describing a `Person`
- **Generating a module** — running the generator with the TypeScript engine
- **Using the generated types** — building a value, validating untrusted input, and reading it back
- **The runtime and options** — how the runtime is supplied, and the code-generation options
