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

    # Group names. Two groups may share a name only in separate alternatives of one disjunction, and a named
    # reference names a group that is written anywhere in the pattern.
    @testset "group names" begin
        for (pattern, valid) in [
                ("(?<a>x)|(?<a>y)", true), ("(?<a>x)(?<a>y)", false), ("(?<a>x(?<a>y))", false),
                ("(?<a>x|(?<a>y))", false), ("(?:(?<a>x)|(?<a>y))\\k<a>", true), ("(?:(?<a>x)|y)(?<a>z)", false),
                ("(?:(?<a>x)|y)|(?<a>z)", true), ("(?:(?<a>x)|(?<a>y))|(?<a>z)", true),
                ("(?<a>x)|(?:(?<a>y)|(?<a>z))", true), ("(?<a>x)|(?:(?<a>y)(?<a>z))", false),
                ("(?<a>x)|(?:(?<b>y)|z)(?<a>w)", true), ("(?:(?<a>x)|(?<b>y))(?:(?<b>z)|(?<a>w))", false),
                ("(?:(?<a>x)|(?<b>y))(?:(?<c>z)|(?<d>w))", true), ("(?=(?<a>x))|(?<!(?<a>y))", true),
                ("(?=(?<a>x))(?<!(?<a>y))", false), ("(?<a>x)|(?<b>y)|(?<a>z)|(?<b>w)", true),
                ("(?<a>x)|(?<b>y)(?<a>z)|(?<b>w)", true), ("(?<a>x)|(?<b>y(?<a>z)(?<b>w))", false),
                ("(?<ab>x)(?<a>y)(?<abc>z)\\k<a>\\k<ab>\\k<abc>", true), ("(?<ab>x)(?<a>y)\\k<b>", false),
                ("\\k<a>(?<a>x)", true), ("\\k<a>(?<b>x)", false), ("(?<a>x)\\k<aa>", false),
                ("(?<\u03c0>a)|(?<\\u03c0>b)", true), ("(?<\u03c0>a)(?<\\u{3c0}>b)", false),
                ("(?<b>(?<a>x)|(?<a>y))|(?<b>z)", true), ("((?<a>x)|(?<a>y))(?<a>z)", false),
            ]
            @test E.isvalidpattern(pattern) == valid
            @test E.parsesunicode(pattern) == valid
        end
        # Patterns of groups with few names, alternatives and references, as the parser reads them.
        tokens = ["(?<a>", "(?<b>", "(?<ab>", "(?:", "(?=", "(?<!", "(", ")", ")", ")", "|", "|", "x", "\\k<a>",
            "\\k<b>", "\\k<ab>", "(?<\\u0061>"]
        random = Xorshift(0x00000011)
        patterns = String[]
        for i in 1:100_000
            n = 1 + nextbelow!(random, 14)
            push!(patterns, join(tokens[nextbelow!(random, length(tokens)) + 1] for _ in 1:n))
        end
        @test disagreements(patterns) == String[]
        valid = count(E.isvalidpattern, patterns)
        println("[validator] ", valid, " of ", length(patterns), " generated patterns of group names are valid")
        @test 1_000 < valid < 90_000
    end

    # The regex format reads a string of the instance, of a megabyte at most. Each group name was looked for among
    # all those before it, and so was each named reference, so the time went with the square of their number, and
    # the alternatives around each name were kept with it: 60,000 groups of one name in as many alternatives took
    # some 40 seconds, and inside 150 groups they took minutes and hundreds of megabytes. The names are put in
    # order now and each is compared with the one before it.
    @testset "many group names" begin
        started = time()
        n = 60_000
        m = 30_000
        distinct = join("(?<n$i>a)" for i in 1:m)
        references = join("\\k<n$i>" for i in 1:m)
        apart = join(("(?<n$i>a)" for i in 1:n), "|")
        long = "n"^200
        longnames = join("(?<$long$i>a)" for i in 1:4_000)
        escaped = join("(?<\\u{$(string(0x4e00 + i, base=16))}>a)" for i in 1:20_000)
        cases = [
            "(?<n>a)|"^n * "b" => true,
            "(?<n>a)|"^n * "(?<n>b)(?<n>c)" => false,
            "(?<n>a)(?<n>b)|" * "(?<n>a)|"^n => false,
            "(?:"^150 * "(?<n>a)|"^n * "b" * ")"^150 => true,
            "(?:"^150 * "(?<n>a)|"^n * "b" * ")"^150 * "(?<n>c)" => false,
            distinct => true,
            distinct * references => true,
            references * distinct => true,
            distinct * references * "\\k<n0>" => false,
            distinct * "(?<n777>a)" => false,
            apart * "|(?<n1>a)|(?<n$n>a)" => true,
            apart * "|(?<n5>a)(?<n5>b)" => false,
            longnames * "\\k<$(long)4000>" => true,
            longnames * "\\k<$(long)4001>" => false,
            escaped * "\\k<\u4e01>\\k<\u9999>" => true,
            escaped * "(?<\u9999>a)" => false,
        ]
        # A pattern is not shown when a test fails: each is some hundreds of kilobytes.
        @test all(c -> ncodeunits(first(c)) <= E.MAX_PATTERN_LENGTH, cases)
        @test [E.isvalidpattern(first(c)) for c in cases] == [last(c) for c in cases]
        @test time() - started < 10
        # What a validator keeps from one pattern to the next does not grow with a pattern of many names.
        for validator in E.VALIDATORS
            @test sum(length, (validator.names, validator.opened, validator.places, validator.order, validator.refs)) <=
                  5 * E.MAX_KEPT_NUMBERS
        end
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
