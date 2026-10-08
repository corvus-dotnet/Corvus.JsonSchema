# Runs every case of the JSON Schema Test Suite's files that exercise patterns through compile, ismatch and
# isvalidpattern. The few other keywords those files use are evaluated here.

function suitefiles(tests::String)
    files = String[]
    for draft in sort!(readdir(tests)), name in (joinpath("optional", "ecmascript-regex.json"),
        joinpath("optional", "format", "regex.json"), "pattern.json", "patternProperties.json",
        joinpath("optional", "non-bmp-regex.json"))
        file = joinpath(tests, draft, name)
        isfile(file) && push!(files, file)
    end
    return files
end

function hastype(name::String, data)
    name == "string" && return data isa String
    name == "object" && return data isa Dict
    name == "array" && return data isa Vector
    name == "boolean" && return data isa Bool
    name == "null" && return data === nothing
    name == "number" && return data isa Float64
    name == "integer" && return data isa Float64 && data == trunc(data)
    name == "any" && return true
    return false
end

suitepattern(patterns::Dict{String,E.Pattern}, p::String) = get!(() -> E.compile(p), patterns, p)

function suitevalid(patterns::Dict{String,E.Pattern}, schema, data)
    schema isa Bool && return schema
    for (keyword, value) in schema::Dict{String,Any}
        if keyword == "\$schema"
        elseif keyword == "type"
            names = value isa String ? Any[value] : value
            any(name -> hastype(name, data), names) || return false
        elseif keyword == "maximum"
            data isa Float64 && data > value && return false
        elseif keyword == "pattern"
            data isa String && !E.ismatch(suitepattern(patterns, value), codeunits(data)) && return false
        elseif keyword == "format"
            value == "regex" || error("unexpected format $value")
            data isa String && !E.isvalidpattern(data) && return false
        elseif keyword == "patternProperties"
            data isa Dict || continue
            for (p, sub) in value, (name, property) in data
                if E.ismatch(suitepattern(patterns, p), name) && !suitevalid(patterns, sub, property)
                    return false
                end
            end
        elseif keyword == "additionalProperties"
            data isa Dict || continue
            named = get(schema, "patternProperties", Dict{String,Any}())
            for (name, property) in data
                additional = !any(p -> E.ismatch(suitepattern(patterns, p), name), keys(named))
                additional && !suitevalid(patterns, value, property) && return false
            end
        else
            error("unexpected keyword $keyword")
        end
    end
    return true
end

@testset "JSON Schema Test Suite" begin
    tests = suitetests()
    if tests === nothing
        @info "The JSON Schema Test Suite is not there, so its pattern files are not run."
    else
        files = suitefiles(tests)
        @test !isempty(files)
        patterns = Dict{String,E.Pattern}()
        cases = 0
        failures = String[]
        for file in files, group in readjson(file), test in group["tests"]
            cases += 1
            got = suitevalid(patterns, group["schema"], test["data"])
            if got != test["valid"]
                push!(failures, string(relpath(file, tests), ": ", group["description"], ": ", test["description"],
                    ": got ", got))
            end
        end
        println("[suite] ", cases, " cases in ", length(files), " files, ", length(patterns), " distinct patterns")
        isempty(failures) || println(join(failures, "\n"))
        @test isempty(failures)
        @test cases > 500
    end
end
