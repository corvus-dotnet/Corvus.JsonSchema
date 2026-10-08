# Keeps the embedded metaschemas in step with those of Corvus.Text.Json (src/Corvus.Text.Json/metaschema). Run with
# UPDATE_METASCHEMAS=1 to copy them again. Ported from metaschemas_test.go.

@testset "metaschemas" begin
    @testset "the embedded metaschemas are current" begin
        source = joinpath(REPOSITORY_ROOT, "src", "Corvus.Text.Json", "metaschema")
        target = joinpath(dirname(pathof(CorvusJsonSchema)), "metaschemas")
        if !isdir(source)
            @info "outside the Corvus.JsonSchema repository: nothing to compare the metaschemas against"
        else
            update = get(ENV, "UPDATE_METASCHEMAS", "") != ""
            expected = Set{String}()
            for draft in ["draft4", "draft6", "draft7", "draft2019-09", "draft2020-12"]
                for (dir, _, files) in walkdir(joinpath(source, draft)), file in files
                    path = joinpath(dir, file)
                    name = replace(relpath(path, source), '\\' => '/')
                    push!(expected, name)
                    want = read(path)
                    if update
                        mkpath(dirname(joinpath(target, name)))
                        write(joinpath(target, name), want)
                    else
                        got = get(C.METASCHEMA_FILES, name, nothing)
                        @test got !== nothing && Vector{UInt8}(got) == want
                    end
                end
            end
            @test Set(keys(C.METASCHEMA_FILES)) == expected
        end
    end

    @testset "every embedded metaschema resolves by its URI" begin
        uris = ["http://json-schema.org/draft-04/schema", "http://json-schema.org/draft-06/schema",
            "http://json-schema.org/draft-07/schema", "https://json-schema.org/draft/2019-09/schema",
            "https://json-schema.org/draft/2020-12/schema"]
        for draft in ["2019-09", "2020-12"]
            prefix = "draft$draft/meta/"
            for name in sort!(collect(keys(C.METASCHEMA_FILES)))
                if startswith(name, prefix)
                    push!(uris, "https://json-schema.org/draft/$draft/meta/" * name[length(prefix)+1:end-5])
                end
            end
        end
        @test length(uris) == 21
        for uri in uris
            text = C.metaschema(uri)
            @test text !== nothing
            text === nothing && continue
            @test parse_document(text) isa Document
            # The hyper-schema vocabulary refers to the links schema, which is not embedded.
            occursin("hyper-schema", uri) && continue
            v = compile_schema_uri(uri)
            @test isvalid(v, """{"type": "object"}""")
            # The root metaschemas (and the validation vocabularies) know that type is a name or a list of names.
            if !occursin("/meta/", uri) || endswith(uri, "/meta/validation")
                @test !isvalid(v, """{"type": 12}""")
            end
        end
        for uri in ["https://json-schema.org/draft/2020-12/meta/../schema", "https://example.com/schema", ""]
            @test C.metaschema(uri) === nothing
        end
    end
end
