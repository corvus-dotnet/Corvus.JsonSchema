# Code generation

This is the reference for generating Python from a JSON Schema. The `corvusjson` command, its options, the
shape of the output, and the type-check gate.

## Overview

The generator is a .NET command-line tool that reads a JSON Schema and writes Python. The same engine backs the
C# and TypeScript generators. The `--engine Python` option selects the Python emitter. The output is plain
Python with one small runtime library, so nothing in your project depends on .NET at run time.

## Installation

The generator is a .NET global tool and requires the [.NET SDK](https://dotnet.microsoft.com/download).

```bash
dotnet tool install --global Corvus.Json.Cli
```

This installs the `corvusjson` command.

## Generating a module

```bash
corvusjson jsonschema <schema.json> --engine Python --outputPath <directory>
```

For example.

```bash
corvusjson jsonschema person.json --engine Python --outputPath ./generated
```

This writes a single file, `generated/__init__.py`, containing every type in the schema, its validators, and
its builders. The directory becomes an importable package, so you use the type by name and its companion
functions module-level.

```python
from generated import Person, evaluate, parse, build_person, patch_person, from_birth_date
```

## Single-file output

Unlike the C# engine, which writes one file per type, the Python engine emits one `__init__.py` per schema. All
types, validators, builders, brand factories, and the root `evaluate` / `parse` entry points live in that one
module. The first line marks it auto-generated. Do not edit it. Regenerate from the schema instead.

## The runtime dependency

The generated module imports `corvus_json_runtime`, a small pure-Python package that provides the parts the
language cannot express on its own: format checks, exact numeric comparison, the byte-level edit engine, and
the temporal conversions. Install it into the environment that runs the generated code.

```bash
pip install corvus-json-runtime
```

By default the module imports the runtime as `corvus_json_runtime`. To import it under a different name, for
example a vendored copy, set the `CORVUS_PY_RUNTIME_MODULE` environment variable when you generate.

```bash
CORVUS_PY_RUNTIME_MODULE=myproject.vendored_runtime \
  corvusjson jsonschema person.json --engine Python --outputPath ./generated
```

See [The runtime](./runtime.md) for what the package provides and its own dependencies.

## Options

| Option | Default | Effect |
|---|---|---|
| `--engine Python` | C# | Select the Python emitter. |
| `--outputPath <directory>` | schema directory | Directory for the generated `__init__.py`. |
| `--assertFormat <bool>` | `true` | Whether `format` keywords are enforced by `evaluate`. |

By default `format` keywords (`date`, `uuid`, `uri`, and so on) are asserted. `evaluate` rejects a value whose
format is invalid, and a `format` becomes a branded `NewType` with a validating `from_<t>` factory. Pass
`--assertFormat false` to treat `format` as an annotation, recorded but not enforced by `evaluate`, which is
the JSON Schema 2020-12 default.

## The type-check is the gate

The generated code targets Python 3.12 and type-checks clean under both `mypy --strict` and pyright. Those two
checkers, run against the generated module and your code that uses it, are the review gate. If your usage
type-checks, it matches the generated surface.

```bash
mypy --strict generated your_module.py
pyright generated your_module.py
```

The example recipes under [`examples/`](./examples/) are gated exactly this way. `pwsh examples/regenerate.ps1
-Check` regenerates every recipe and runs `mypy --strict`, pyright, and the demo.

## JSON Schema draft support

The generator supports JSON Schema draft 4, 6, 7, 2019-09, and 2020-12, selected from the schema's `$schema`
keyword and defaulting to 2020-12. The Python engine reuses the same language-neutral schema-loading and
vocabulary machinery as the C# and TypeScript engines.

## See also

- [The runtime](./runtime.md), the runtime library the generated code imports.
- [The type surface](./the-type-surface.md), every JSON Schema construct and its generated Python.
- [Reading and validating](./reading-and-validating.md), the read surface and the evaluators.
