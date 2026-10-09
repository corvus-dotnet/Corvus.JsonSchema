# The validator of the regex format, which allocates nothing, agrees with the parser's reading of ECMA-262 with the
# u flag. This is the Java port's EcmaRegexValidatorTest.

const VALIDATOR_HAND = [
    "", "a", "^a\$", "a|b", "(a)", "(?:a)", "(?=a)", "(?!a)", "(?<=a)", "(?<!a)", "(?<n>a)\\k<n>", "\\k<n>",
    "(?<n>a)(?<n>b)", "(?<\$x_1>a)", "(?<1a>a)", "a{2}", "a{2,}", "a{2,3}", "a{3,2}", "a{", "a{,2}", "{", "}", "]",
    "[", "[]", "[^]", "[a-z]", "[z-a]", "[\\d-z]", "[a-\\d]", "[\\b]", "[\\-]", "\\-", "\\a", "\\c", "\\cA", "\\c1",
    "\\0", "\\01", "\\1", "(a)\\1", "(a)\\2", "\\x4", "\\x41", "\\u004", "\\u0041", "\\u{1F600}", "\\u{110000}",
    "\\ud83d\\ude00", "[\\ud83d\\ude00-\\ud83d\\ude4f]", "\\p{L}", "\\p{Letter}", "\\p{digit}", "\\p{Nope}",
    "\\p{gc=Lu}", "\\p{Script=Greek}", "\\p{sc=Grek}", "\\p{Script=Nope}", "\\P{ASCII}", "\\p", "\\p{", "a**", "a+?",
    "*a", "(?=a)*", "^*", "\$+", "\\b+", "a)", "(a", "(?a)", "\\/", "\\.", "a\\", "[a", "x{1}{2}",
    "\\w+@\\w+\\.\\w+", "^[a-z][a-z0-9_]*\$", "(?<a>.)\\k<a>", "[\\p{L}\\d]", "\\s\\S\\w\\W\\d\\D",
    # Characters of more than one byte, which the validator reads as bytes and the parser as characters.
    "\u00e9", "[\u03b1-\u03c9]", "[\u03c9-\u03b1]", "\U1F600+", "[\U1F600-\U1F64F]", "[\U1F64F-\U1F600]",
    "(?<\u03c0>a)\\k<\u03c0>", "(?<\u03c0>a)\\k<\\u03c0>", "(?<\U1D4D1>a)\\k<\\ud835\\udcd1>",
    "(?<\U1D4D1>a)\\k<\\u{1D4D1}>", "(?<a\U1F600>a)", "\\\u00e9", "[\\\u00e9]", "\\c\u00e9", "\u00e9{2}", "\u00e9{",
    "(?<\u00e9>a)|(?<\\u00e9>b)", "(?<\u00e9>a)(?<\\u00e9>b)", "[\\u{1F600}-\U1F64F]", "[\U1F64F-\\u{1F600}]",
]

# Every pattern, every name of patternProperties and every string instance of every file of the suite.
function suitecorpus()
    out = String[]
    tests = suitetests()
    tests === nothing && return out
    function collect!(value)
        if value isa Vector
            foreach(collect!, value)
        elseif value isa Dict
            for (name, sub) in value
                name == "pattern" && sub isa String && push!(out, sub)
                name == "patternProperties" && sub isa Dict && append!(out, keys(sub))
                # The instances of the regex format tests are patterns too.
                name == "data" && sub isa String && push!(out, sub)
                collect!(sub)
            end
        end
    end
    for (directory, _, files) in walkdir(tests), file in files
        endswith(file, ".json") && collect!(readjson(joinpath(directory, file)))
    end
    return unique!(out)
end

@testset "validator" begin
    disagreements(patterns) = [p for p in patterns if E.parsesunicode(p) != E.isvalidpattern(p)]

    @testset "agrees on hand-written patterns" begin
        @test disagreements(VALIDATOR_HAND) == String[]
        @test count(E.isvalidpattern, VALIDATOR_HAND) > 40
        @test count(!E.isvalidpattern, VALIDATOR_HAND) > 30
    end

    @testset "agrees on the suite's patterns" begin
        corpus = suitecorpus()
        println("[validator] ", length(corpus), " strings of the suite")
        @test disagreements(corpus) == String[]
        @test suitetests() === nothing || length(corpus) > 500
    end

    @testset "agrees on generated patterns" begin
        alphabet = collect("ab0\\\\^\$.*+?()[]{}|-,:=!<>dwsbpkuxc1L")
        random = Xorshift(0x00000007)
        patterns = String[]
        for i in 1:50_000
            n = 1 + nextbelow!(random, 8)
            push!(patterns, String([alphabet[nextbelow!(random, length(alphabet)) + 1] for _ in 1:n]))
        end
        @test disagreements(patterns) == String[]
        valid = count(E.isvalidpattern, patterns)
        @test 10_000 < valid < 40_000
    end

    # Text that is not UTF-8 is no pattern, to the validator and to the parser alike.
    @testset "a pattern is UTF-8" begin
        for bytes in (UInt8[0xFF], UInt8[0x61, 0x80], UInt8[0xC3], UInt8[0xC0, 0x80], UInt8[0xF5, 0x80, 0x80, 0x80],
            UInt8[0x5B, 0xE2, 0x82, 0x5D], UInt8[0xF0, 0x9F, 0x98])
            pattern = String(copy(bytes))
            @test !E.isvalidpattern(pattern)
            @test !E.parsesunicode(pattern)
            @test isnotapattern(pattern)
        end
        @test E.isvalidpattern(SubString("xa+y", 2, 3))
        @test !E.isvalidpattern(SubString("xa+*y", 2, 4))
    end
end
