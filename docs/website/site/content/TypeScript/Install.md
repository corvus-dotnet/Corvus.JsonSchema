---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-06-29T00:00:00.0+00:00
Title: "Install the tools"
---
## Install the tools

First, the code generator. It is distributed as a .NET global tool and requires the [.NET SDK](https://dotnet.microsoft.com/download):

```bash
dotnet tool install --global Corvus.Json.Cli
```

This installs the `corvusjson` command. The same tool generates both C# and TypeScript; the `--engine` option selects which.

Second, the runtime. The generated code imports a small npm package at run time:

```bash
npm install @endjin/corvus-json-runtime
```

The runtime is ESM-only and pulls in three dependencies: `lossless-json` for exact numeric evaluation, `@js-temporal/polyfill` for dates and times, and `tr46` for internationalised formats.
