# The formats whose rules read Unicode properties take them from the package's own tables, of one Unicode version,
# and nothing in the package reads Julia's Unicode data. Ported from unicode_test.go.

format_bytes(text::String) = C.Bytes(Vector{UInt8}(text))

@testset "unicode" begin
    # Fails if a source file of the package (outside the pattern engine, which has its own tests) calls a function
    # that reads Julia's Unicode data, or builds a Base.Regex, whose classes read the data of the PCRE2 that Julia
    # bundles. Both are a different version of Unicode in different versions of Julia, so a result that rests on
    # them changes with the Julia version.
    @testset "no Unicode data of the Julia version" begin
        banned = ["isletter", "isuppercase", "islowercase", "isnumeric", "isspace", "ispunct", "isprint", "iscntrl",
            "isxdigit", "uppercase", "lowercase", "titlecase", "uppercasefirst", "lowercasefirst", "textwidth",
            "Unicode.", "Base.Unicode", "normalize(", "graphemes", "isemoji", "Regex(", "occursin(r\"", "match(r\"",
            "eachmatch(", "replace(r\""]
        source = dirname(pathof(CorvusJsonSchema))
        files = [f for f in readdir(source) if endswith(f, ".jl")]
        @test length(files) >= 20
        offences = String[]
        for file in files, (number, line) in enumerate(eachline(joinpath(source, file)))
            code = strip(line)
            startswith(code, "#") && continue
            for name in banned
                at = findfirst(name, code)
                at === nothing && continue
                # A longer identifier that ends in the name (ascii_lower and the like) is not a call of it.
                before = at.start > 1 ? code[prevind(code, at.start)] : ' '
                (isascii(before) && (before == '_' || 'a' <= before <= 'z' || 'A' <= before <= 'Z')) && continue
                push!(offences, "$file:$number: $name")
            end
        end
        foreach(println, offences)
        @test isempty(offences)
    end

    # Format answers that differ between Unicode 15 and the Unicode 17 of the package's tables.
    @testset "the Unicode formats use Unicode 17" begin
        @test C.EcmaRegex.UNICODE_VERSION == "17.0.0"
        # U+10D4A and U+10D4B are Garay letters, assigned in Unicode 16. U+1C89 is an uppercase letter of Unicode
        # 16, and IDNA2008 disallows uppercase letters. U+2FFFF is a noncharacter in every version.
        for (host, want) in ["\U00010D4A\U00010D4B.example" => true, "ᲊ.example" => true,
            "Ᲊ.example" => false, "\U0002FFFF.example" => false]
            @test C.is_idn_hostname(format_bytes(host)) == want
        end
        for (address, want) in ["\U00010D4A\U00010D4B@example.com" => true,
            "\U00016EA0\U00016EBB@example.com" => true, "\U0002FFFF@example.com" => false,
            "δοκιμή@example.com" => true, "a b@example.com" => false]
            @test C.is_email(format_bytes(address), true) == want
        end
        @test C.is_idn_hostname(format_bytes("ελληνικά.例え.بيروت.example"))
        @test C.is_email(format_bytes("δοκιμή.例え@παράδειγμα.example"), true)
        # Only the letters A to Z are lowered when a URI is normalized.
        @test C.ascii_lower("HTTP://ÉXAMPLE.Com/İ") == "http://Éxample.com/İ"
    end
end
