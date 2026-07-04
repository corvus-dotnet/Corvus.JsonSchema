---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-06-29T00:00:00.0+00:00
Title: "Generate the module"
---
## Generate the module

Run the generator, selecting the TypeScript engine:

```bash
corvusjson jsonschema person.json --engine TypeScript --tsRuntimeModule @endjin/corvus-json-runtime --outputPath ./src/generated
```

This writes a single file, `./src/generated/generated.ts`, containing the types, validators, and builders for the schema. `--tsRuntimeModule @endjin/corvus-json-runtime` tells the generator to import the runtime from the npm package you installed. (Omit that option and it writes a self-contained `corvus-runtime.ts` next to the module instead — see [The runtime and options](#the-runtime) below.)
