# Differential testing of the fail-fast plans (fused objects, type dispatch, name tables and the rest) against the
# collecting evaluator, which walks the general keyword-by-keyword path: every instance of the jsonschema-benchmark
# corpora, and mutations of it (a property removed, given another type, nulled or added, an array item changed, at
# every depth), must get the same verdict from both. Ported from differential_test.go.
#
# Set JSONSCHEMA_BENCHMARK to a checkout of https://github.com/sourcemeta-research/jsonschema-benchmark. The test is
# skipped without it. DIFF_ONLY narrows the corpora, DIFF_INSTANCES caps the instances of each corpus (default 60).
# It takes a minute or two.

# A JSON value as a tree the test can change: nothing, a Bool, a number as its text, a String, a Vector or a list of
# name and value pairs.
struct NumberText
    text::String
end

function to_tree(d::Document, n::Int)
    k = C.kind(d, n)
    k == C.KIND_NULL && return nothing
    k == C.KIND_BOOL && return C.boolean(d, n)
    k == C.KIND_NUMBER && return NumberText(String(C.number_text(d, n)))
    k == C.KIND_STRING && return text_of(d, n)
    k == C.KIND_ARRAY && return Any[to_tree(d, item) for item in items_of(d, n)]
    return Pair{String,Any}[name => to_tree(d, value) for (name, value) in members_of(d, n)]
end

function write_tree(io::IO, v)
    if v === nothing
        print(io, "null")
    elseif v isa Bool
        print(io, v ? "true" : "false")
    elseif v isa NumberText
        print(io, v.text)
    elseif v isa String
        print(io, quote_json(v))
    elseif v isa Vector{Any}
        print(io, '[')
        for (i, item) in enumerate(v)
            i > 1 && print(io, ',')
            write_tree(io, item)
        end
        print(io, ']')
    else
        print(io, '{')
        for (i, (name, value)) in enumerate(v)
            i > 1 && print(io, ',')
            print(io, quote_json(name), ':')
            write_tree(io, value)
        end
        print(io, '}')
    end
end

tree_json(v) = sprint(write_tree, v)

# Values of every JSON type, tried in place of a value.
replacements() = Any[nothing, true, NumberText("0"), NumberText("-1.5"), "", "x", Any[], Pair{String,Any}[],
    Any[NumberText("1"), "a"]]

function with_property(o::Vector{Pair{String,Any}}, name::String, value, remove::Bool)
    out = Pair{String,Any}[p for p in o if p.first != name]
    remove || push!(out, name => value)
    return sort!(out; by=p -> p.first)
end

with_item(a::Vector{Any}, i::Int, value) = (out = copy(a); out[i] = value; out)

# Mutations of v: at the root and, recursively, inside it (bounded at each level to keep the count sane).
function mutations(v, depth::Int)
    out = Any[]
    depth > 6 && return out
    if v isa Vector{Pair{String,Any}}
        for (i, (k, value)) in enumerate(sort(v; by=p -> p.first))
            i > 12 && break
            push!(out, with_property(v, k, nothing, true))
            all_values = replacements()
            for j in ((i-1)%3+1):3:length(all_values)
                push!(out, with_property(v, k, all_values[j], false))
            end
            inner = mutations(value, depth + 1)
            for j in 1:min(length(inner), 40)
                push!(out, with_property(v, k, inner[j], false))
            end
        end
        push!(out, with_property(v, "zzUnknownProperty", NumberText("1"), false))
    elseif v isa Vector{Any}
        for i in 1:min(length(v), 4)
            all_values = replacements()
            for j in ((i-1)%2+1):2:length(all_values)
                push!(out, with_item(v, i, all_values[j]))
            end
            inner = mutations(v[i], depth + 1)
            for j in 1:min(length(inner), 40)
                push!(out, with_item(v, i, inner[j]))
            end
        end
        isempty(v) || push!(out, vcat(v, Any[v[1]]))
    else
        append!(out, replacements())
    end
    return out
end

function strip_format(v)
    if v isa Vector{Pair{String,Any}}
        return Pair{String,Any}[k => strip_format(value) for (k, value) in v if !(k == "format" && value isa String)]
    elseif v isa Vector{Any}
        return Any[strip_format(item) for item in v]
    end
    return v
end

@testset "plans agree with the general evaluator on the benchmark corpora" begin
    root = get(ENV, "JSONSCHEMA_BENCHMARK", "")
    if root == ""
        @info "set JSONSCHEMA_BENCHMARK to a checkout of jsonschema-benchmark to run the differential test"
    else
        isdir(joinpath(root, "schemas")) || error("jsonschema-benchmark not found at $root")
        only = get(ENV, "DIFF_ONLY", "")
        limit = parse(Int, get(ENV, "DIFF_INSTANCES", "60"))
        checked = 0
        problems = String[]
        for name in sort!(readdir(joinpath(root, "schemas")))
            (only != "" && !occursin("," * name * ",", "," * only * ",")) && continue
            schema_file = joinpath(root, "schemas", name, "schema.json")
            isfile(schema_file) || continue
            schema = parse_document(read(schema_file))
            v = try
                compile_schema(tree_json(strip_format(to_tree(schema, schema.root))))
            catch err
                push!(problems, "$name: compile error " * sprint(showerror, err))
                continue
            end
            failures, lines = 0, 0
            for line in eachline(joinpath(root, "schemas", name, "instances.jsonl"))
                isempty(strip(line)) && continue
                lines += 1
                lines > limit && break
                document = parse_document(line)
                isvalid(v, line) || push!(problems, "$name: instance $lines is not valid")
                x = to_tree(document, document.root)
                for candidate in vcat(Any[x], mutations(x, 0))
                    checked += 1
                    text = tree_json(candidate)
                    fast = isvalid(v, text)
                    collected = evaluate(v, text, ResultsCollector(Basic))
                    fast == collected && continue
                    failures += 1
                    if failures <= 3
                        push!(problems, "$name: fail-fast $fast, collecting $collected on " * first(text, 400))
                    end
                end
            end
            failures > 3 && push!(problems, "$name: $failures disagreements in all")
        end
        foreach(println, problems)
        println(checked, " instances checked")
        @test isempty(problems)
        @test checked > 0
    end
end
