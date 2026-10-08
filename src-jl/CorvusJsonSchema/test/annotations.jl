# Runs the JSON-Schema-Test-Suite annotation tests (JSON-Schema-Test-Suite/annotations) through a verbose results
# collector, as the C#, Rust and Go annotation tests do: every draft, cases filtered by "compatibility", and each
# assertion compared with the annotations grouped by instance location, keyword and schema location. Ported from
# annotations_test.go.

const COMPATIBILITY_ORDER = ["3", "4", "6", "7", "2019", "2020"]

function compatible(level::String, compatibility::String)
    at = findfirst(==(level), COMPATIBILITY_ORDER)
    if startswith(compatibility, "<=") && length(compatibility) > 2
        limit = findfirst(==(compatibility[3:end]), COMPATIBILITY_ORDER)
        return limit !== nothing && at <= limit
    end
    least = findfirst(==(compatibility), COMPATIBILITY_ORDER)
    return least !== nothing && at >= least
end

# Compares the produced annotations of a keyword at a location with the expected ones: the same schema locations,
# each with an equal JSON value (numbers by value, objects unordered).
function same_annotations(actual::Dict{String,String}, expected::Dict{String,String})
    length(actual) == length(expected) || return false
    for (location, want) in expected
        haskey(actual, location) || return false
        a, b = parse_document(actual[location]), parse_document(want)
        C.values_equal(a, a.root, b, b.root) || return false
    end
    return true
end

@testset "annotation suite" begin
    root = suite_root()
    dir = joinpath(root, "annotations", "tests")
    isdir(dir) || error("annotation tests not found at $dir (set JSON_SCHEMA_TEST_SUITE, or check out the submodule)")
    resolver = remote_resolver(root)
    total, failures = 0, String[]
    for (name, dialect, level) in [("draft4", Draft4, "4"), ("draft6", Draft6, "6"), ("draft7", Draft7, "7"),
        ("draft2019-09", Draft201909, "2019"), ("draft2020-12", Draft202012, "2020")]
        for file in json_files(dir)
            d = parse_document(read(file))
            label = name * "/" * basename(file)
            for group in items_of(d, member(d, d.root, "suite"))
                description = text_of(d, member(d, group, "description"))
                compatibility = member(d, group, "compatibility")
                if compatibility >= 0 && !compatible(level, text_of(d, compatibility))
                    continue
                end
                validator = compile_schema(json_of(d, member(d, group, "schema")); default_dialect=dialect,
                    resolver=resolver)
                for test in items_of(d, member(d, group, "tests"))
                    instance = json_of(d, member(d, test, "instance"))
                    collector = ResultsCollector(Verbose)
                    evaluate(validator, instance, collector)
                    produced = collect_annotations(collector)
                    for assertion in items_of(d, member(d, test, "assertions"))
                        total += 1
                        location = text_of(d, member(d, assertion, "location"))
                        keyword = text_of(d, member(d, assertion, "keyword"))
                        expected = Dict{String,String}(k => json_of(d, v)
                                                       for (k, v) in members_of(d, member(d, assertion, "expected")))
                        actual = get(get(produced, location, Dict{String,Dict{String,String}}()), keyword,
                            Dict{String,String}())
                        if !same_annotations(actual, expected)
                            push!(failures, "$label [$description] instance $instance '$location' $keyword: " *
                                            "expected $expected, actual $actual")
                        end
                    end
                end
            end
        end
    end
    foreach(println, failures)
    println(total - length(failures), "/", total, " annotation assertions passed")
    @test isempty(failures)
    @test total > 0
end
