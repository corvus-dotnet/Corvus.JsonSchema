"""
    CorvusJsonSchema

A JSON Schema evaluator for draft 4, 6, 7, 2019-09 and 2020-12, ported from the Corvus.Text.Json runtime evaluator.

A schema is compiled once into a node graph with fail-fast plans, then any number of instances are validated against
it. `isvalid` and [`validate`](@ref) fail fast and report nothing. [`evaluate`](@ref) is exhaustive and reports to a
[`ResultsCollector`](@ref) at the `Basic`, `Detailed` or `Verbose` level, with the same rows (paths, messages, order)
as the other Corvus implementations, annotations included.

```julia
using CorvusJsonSchema

validator = compile_schema(\"\"\"{
    "type": "object",
    "properties": {"id": {"type": "integer", "minimum": 1}},
    "required": ["id"]
}\"\"\")
isvalid(validator, \"\"\"{"id": 3}\"\"\")  # true
isvalid(validator, \"\"\"{"id": 0}\"\"\")  # false
```

In the steady state, validating a parsed [`Document`](@ref), or JSON text, allocates nothing.
"""
module CorvusJsonSchema

export Validator, compile_schema, compile_schema_uri, validate, evaluate
export Document, parse_document, ParseError, CompileError, DepthExceededError, MAX_DEPTH
export Dialect, Draft4, Draft6, Draft7, Draft201909, Draft202012
export ResultsCollector, ResultsLevel, Basic, Detailed, Verbose, SchemaResult, results
export Annotation, annotations, collect_annotations, schema_location_fragment

include("document.jl")
include("numbers.jl")
include("values.jl")
include("names.jl")
include("uri.jl")
include("dialect.jl")
include("options.jl")
include("metaschemas.jl")
include("pattern_engine.jl")
include("unicode.jl")
include("pattern.jl")
include("formats.jl")
include("node.jl")
include("loader.jl")
include("compiler.jl")
include("results.jl")
include("plan_types.jl")
include("plan.jl")
include("fused.jl")
include("eval.jl")
include("validator.jl")
include("precompile.jl")

end
