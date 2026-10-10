# The Unicode data of ECMA-262 patterns, and the way a set of it is written for PCRE2. This is the Java port's
# EcmaUnicodeTest. The data itself was compared, when it was generated for the Java port, with the Unicode Character
# Database and with V8. These tests check that it is read whole, that it is the data of the Java port, and that a
# class written from a set matches exactly the set on the PCRE2 of the Julia the tests run on.

function propertyexpressions()
    all = String[]
    for name in E.CATEGORY_NAMES
        push!(all, name, "gc=" * name, "General_Category=" * name)
    end
    append!(all, E.BINARY_NAMES)
    for name in E.SCRIPT_NAMES
        push!(all, "sc=" * name, "Script=" * name, "scx=" * name, "Script_Extensions=" * name)
    end
    return all
end

function bitsof(set::E.CodeSet)
    bits = falses(0x110000)
    for i in 1:2:length(set)
        bits[(set[i] + 1):(set[i + 1] + 1)] .= true
    end
    return bits
end

# The distinct sets of some expressions, each with the first expression that names it.
function distinctsets(expressions)
    sets = Pair{String,E.CodeSet}[]
    seen = Set{UInt}()
    for expression in expressions
        set = E.property(expression)::E.CodeSet
        if !(objectid(set) in seen)
            push!(seen, objectid(set))
            push!(sets, expression => set)
        end
    end
    return sets
end

# The text of a class for a set, as the emitter writes it.
classtext(set::E.CodeSet) = sprint(E.writeset, set)

# The ordered pairs of characters with a case variant that the pattern ^(.)(?i:\1)$ does not match and the data holds
# equivalent, and those it matches and the data does not. `bytes` is each of the characters as text.
function comparedpairs(pattern::E.Pattern, folding::E.Folding, bytes::Vector{Vector{UInt8}})
    lacking = Tuple{Int,Int}[]
    added = Tuple{Int,Int}[]
    text = UInt8[]
    for (i, c) in enumerate(folding.points), (j, d) in enumerate(folding.points)
        i == j && continue
        empty!(text)
        append!(text, bytes[i])
        append!(text, bytes[j])
        equivalent = d in folding.classes[i]
        E.ismatch(pattern, text) == equivalent || push!(equivalent ? lacking : added, (Int(c), Int(d)))
    end
    return lacking, added
end

@testset "Unicode data" begin
    # Every table is a sorted list of ranges that neither overlap nor touch.
    @testset "every set is well formed" begin
        bad = String[]
        for expression in propertyexpressions()
            set = E.property(expression)
            ok = set !== nothing && iseven(length(set)) && (isempty(set) || set[end] <= E.MAX_CODE_POINT)
            if ok
                for i in 1:2:length(set)
                    ok &= set[i] <= set[i + 1] && (i == 1 ? set[i] >= 0 : set[i] > set[i - 1] + 1)
                end
            end
            ok || push!(bad, expression)
        end
        @test isempty(bad)
        @test length(propertyexpressions()) > 1700
    end

    # The General_Category values that are not unions hold every code point once, and the unions are their parts.
    @testset "general categories partition the code points" begin
        leaves = ["Lu", "Ll", "Lt", "Lm", "Lo", "Mn", "Mc", "Me", "Nd", "Nl", "No", "Pc", "Pd", "Ps", "Pe", "Pi",
            "Pf", "Po", "Sm", "Sc", "Sk", "So", "Zs", "Zl", "Zp", "Cc", "Cf", "Cs", "Co", "Cn"]
        all = falses(0x110000)
        total = 0
        for leaf in leaves
            b = bitsof(E.property(leaf))
            @test !any(b .& all)
            all .|= b
            total += count(b)
        end
        @test total == 0x110000
        unions = [["LC", "Lu", "Ll", "Lt"], ["L", "Lu", "Ll", "Lt", "Lm", "Lo"], ["M", "Mn", "Mc", "Me"],
            ["N", "Nd", "Nl", "No"], ["P", "Pc", "Pd", "Ps", "Pe", "Pi", "Pf", "Po"], ["S", "Sm", "Sc", "Sk", "So"],
            ["Z", "Zs", "Zl", "Zp"], ["C", "Cc", "Cf", "Cs", "Co", "Cn"]]
        for union in unions
            b = falses(0x110000)
            for part in union[2:end]
                b .|= bitsof(E.property(part))
            end
            @test b == bitsof(E.property(union[1]))
        end
        @test E.complement(E.property("Cn")) == E.property("Assigned")
        @test E.property("Any") == Int32[0, 0x10FFFF]
        @test E.property("ASCII") == Int32[0, 0x7F]
        @test E.property("Cs") == Int32[0xD800, 0xDFFF]
    end

    # The counts ECMA-262 gives: 53 binary properties, 38 General_Category values. The scripts hold every code point.
    @testset "the names are those of ECMA-262" begin
        @test length(distinctsets(E.BINARY_NAMES)) == 53
        @test length(distinctsets(E.CATEGORY_NAMES)) == 38
        scripts = distinctsets("sc=" * name for name in E.SCRIPT_NAMES)
        @test sum(count(bitsof(set)) for (_, set) in scripts) == 0x110000
        @test E.UNICODE_VERSION == "17.0.0"
        # A property always gives the same array.
        @test E.property("L") === E.property("gc=Letter")
        @test E.property("sc=Grek") === E.property("Script=Greek")
        # Names are matched exactly, and a name of one kind is not a name of another.
        for bad in ["lu", "Greek", "sc=Lu", "gc=Greek", "gc=Alphabetic", "Script", "", "=", "sc=", "Lu=", "scx=Hrkt"]
            @test E.property(bad) === nothing
            @test !E.isproperty(bad)
        end
        bytes = codeunits("x\\p{scx=Grek}")
        @test E.isproperty(bytes, 5, 12)
        @test !E.isproperty(bytes, 5, 11)
    end

    # The tables are those of the Java port, which were checked against the Unicode Character Database and V8.
    @testset "the data is the Java port's" begin
        java = joinpath(@__DIR__, "..", "..", "..", "..", "src-java", "corvus-json-schema", "src", "main", "java",
            "io", "github", "corvusdotnet", "jsonschema", "EcmaUnicodeData.java")
        if !isfile(java)
            @info "EcmaUnicodeData.java is not there, so the data is not compared with it."
        else
            # A checkout on Windows has the file with CRLF line endings.
            source = replace(read(java, String), "\r\n" => "\n")
            # A string constant of the Java file, whose pieces are joined by +.
            function constant(name::String)
                m = match(Regex("String $name =\\s*((?:\\+?\\s*\"[^\"]*\"\\s*)+);"), source)
                m === nothing && error("no constant $name")
                return join(piece.captures[1] for piece in eachmatch(r"\"([^\"]*)\"", m.captures[1]))
            end
            function list(name::String)
                m = match(Regex("$name = \\{(.*?)\\};", "s"), source)
                m === nothing && error("no array $name")
                return [strip(item, ['"', ' ', '\n']) for item in split(m.captures[1], ',') if !isempty(strip(item))]
            end
            @test occursin("TABLE_COUNT = $(length(E.TABLE_TEXTS));", source)
            @test all(constant("T$(k - 1)") == E.TABLE_TEXTS[k] for k in eachindex(E.TABLE_TEXTS))
            @test constant("SIMPLE_FOLDING") == E.SIMPLE_FOLDING
            @test constant("UPPERCASE") == E.UPPERCASE
            @test list("CATEGORY_NAMES") == E.CATEGORY_NAMES
            @test [parse(UInt32, x) for x in list("CATEGORY_TABLES")] == E.CATEGORY_TABLES
            @test list("BINARY_NAMES") == E.BINARY_NAMES
            @test [parse(Int, x) for x in list("BINARY_TABLES")] == E.BINARY_TABLES
            @test list("SCRIPT_NAMES") == E.SCRIPT_NAMES
            @test [parse(Int, x) for x in list("SCRIPT_TABLES")] == E.SCRIPT_TABLES
            @test [parse(Int, x) for x in list("SCRIPT_EXTENSION_TABLES")] == E.SCRIPT_EXTENSION_TABLES
        end
    end

    # A class written from a set matches exactly the set. Every distinct set of the data and its complement are
    # written, and each is asked about every code point in one pass over a text that holds them all. A set of many
    # ranges is written in one of two forms, by the PCRE2 in use, and both are tried here on the one in use.
    @testset "a written class matches its set" begin
        sets = distinctsets(propertyexpressions())
        @test length(sets) > 350
        bad = String[]
        called = 0
        try
            for trees in (false, true)
                E.SEARCH_TREES[] = trees
                for (expression, set) in sets, (escape, s) in (("\\p", set), ("\\P", E.complement(set)))
                    isempty(E.intersection(s, E.NOT_SURROGATE_SET)) && continue
                    pattern = E.compile("$escape{$expression}")
                    called += occursin("(?&s1)", pattern.translation.main)
                    matchedset(pattern) == astext(s) || push!(bad, "$escape{$expression} with trees $trees")
                    trees && continue
                    matchedset(rawpattern(classtext(s))) == astext(s) || push!(bad, "the class of $expression")
                end
            end
        finally
            E.SEARCH_TREES[] = nothing
        end
        @test isempty(bad)
        @test called > 100
    end

    # The same for sets of ranges drawn at random, with ends at and beside the code points where the encoding of a
    # character grows by a byte, and with sets that hold both U+00FF and U+0100 (see `writeset`).
    @testset "a class of any ranges matches its set" begin
        random = Xorshift(0x0badcafe)
        edges = [0, 0x7f, 0x80, 0xff, 0x100, 0x7ff, 0x800, 0x7fff, 0x8000, 0xffff, 0x10000, 0xd7ff, 0xe000, 0x10ffff]
        bad = String[]
        negated = 0
        called = 0
        function point()
            kind = nextbelow!(random, 6)
            kind == 0 && return nextbelow!(random, 0x300)
            kind == 1 && return nextbelow!(random, 0x10000)
            kind == 2 && return nextbelow!(random, 0x110000)
            edge = edges[nextbelow!(random, length(edges)) + 1]
            kind == 3 && return clamp(edge + nextbelow!(random, 7) - 3, 0, 0x10ffff)
            kind == 4 && return edge
            return nextbelow!(random, 0x100)
        end
        try
            for round in 1:200
                E.SEARCH_TREES[] = isodd(round)
                points = sort!(unique!([point() for _ in 1:(2 * (1 + nextbelow!(random, 120)))]))
                pairs = Int32[]
                for i in 1:2:(length(points) - 1)
                    push!(pairs, points[i], nextbelow!(random, 3) == 0 ? points[i] : points[i + 1])
                end
                set = E.setof(pairs)
                # The class as an ECMA-262 pattern, with a surrogate at the end of a range written as an escape.
                io = IOBuffer()
                for i in 1:2:length(set)
                    print(io, "\\u{", string(set[i], base=16), "}-\\u{", string(set[i + 1], base=16), "}")
                end
                members = String(take!(io))
                for (pattern, s) in (("[$members]", set), ("[^$members]", E.complement(set)))
                    isempty(E.intersection(s, E.NOT_SURROGATE_SET)) && continue
                    compiled = E.compile(pattern)
                    negated += occursin("[^", compiled.translation.main)
                    called += occursin("(?&s1)", compiled.translation.main)
                    matchedset(compiled) == astext(s) || push!(bad, pattern)
                end
            end
        finally
            E.SEARCH_TREES[] = nothing
        end
        isempty(bad) || println(first.(bad[1:min(end, 3)], 300))
        @test isempty(bad)
        @test negated > 50
        @test called > 50
    end

    # Some sets are asked about each code point alone, through ismatch, with the surrogates as three bytes.
    @testset "a written class matches its set on every code point" begin
        buffer = UInt8[]
        for expression in ["L", "Grapheme_Base", "scx=Common", "Emoji", "Cs", "C"]
            set = E.property(expression)::E.CodeSet
            pattern = E.compile("^\\p{$expression}\$")
            wrong = 0
            for c in 0:0x10FFFF
                empty!(buffer)
                if c < 0x80
                    push!(buffer, c)
                elseif c < 0x800
                    push!(buffer, 0xC0 | (c >> 6), 0x80 | (c & 0x3F))
                elseif c < 0x10000
                    push!(buffer, 0xE0 | (c >> 12), 0x80 | ((c >> 6) & 0x3F), 0x80 | (c & 0x3F))
                else
                    push!(buffer, 0xF0 | (c >> 18), 0x80 | ((c >> 12) & 0x3F), 0x80 | ((c >> 6) & 0x3F),
                        0x80 | (c & 0x3F))
                end
                expected = E.inset(set, c) && !(0xD800 <= c <= 0xDFFF)
                wrong += E.ismatch(pattern, buffer) != expected
            end
            @test wrong == 0
        end
    end

    # A property escape as the translator writes it, alone, negated and in a class with other members, matches
    # exactly its set. (The Java port writes some of these with a class of java.util.regex that it corrects. Here
    # every one is written from the data.)
    @testset "a property escape matches its set" begin
        names = vcat(E.CATEGORY_NAMES, ["Alphabetic", "Lowercase", "Uppercase", "Dash", "ID_Continue"])
        extra = Int32['_', '_', 0x378, 0x379, 0x1F600, 0x1F600]
        bad = String[]
        function written(pattern::String, set::E.CodeSet)
            try
                for trees in (false, true)
                    E.SEARCH_TREES[] = trees
                    matchedset(E.compile(pattern)) == astext(set) || push!(bad, "$pattern with trees $trees")
                end
            finally
                E.SEARCH_TREES[] = nothing
            end
        end
        sets = distinctsets(names)
        @test length(sets) == 43
        for (name, set) in sets
            written("\\p{$name}", set)
            written("\\P{$name}", E.complement(set))
            written("[\\p{$name}_\\u0378\\u0379\\u{1F600}]", E.setunion(set, extra))
            written("[^\\p{$name}_\\u0378\\u0379\\u{1F600}]", E.complement(E.setunion(set, extra)))
            written("(?i:\\p{$name})", E.foldclosure(set, true))
            written("(?i:[^\\p{$name}])", E.complement(E.foldclosure(set, true)))
        end
        written("[\\p{Lu}\\p{Nd}\\p{Alphabetic}\\P{L}]", E.setunion(E.setunion(E.property("Lu"), E.property("Nd")),
            E.setunion(E.property("Alphabetic"), E.complement(E.property("L")))))
        written(".", E.DOT_SET)
        written("(?s:.)", E.ANY_SET)
        written("\\s", E.SPACE_SET)
        written("\\S", E.complement(E.SPACE_SET))
        written("\\w", E.WORD_SET)
        written("\\W", E.complement(E.WORD_SET))
        written("\\d", E.DIGIT_SET)
        written("\\D", E.complement(E.DIGIT_SET))
        written("(?i:\\w)", E.FOLD_WORD_SET)
        written("(?i:\\W)", E.complement(E.FOLD_WORD_SET))
        written("[^]", E.ANY_SET)
        @test isempty(bad)
    end

    # The set operations agree with a bit set.
    @testset "set operations" begin
        random = Xorshift(0x00000b0b)
        bits(set) = bitsof(set)[1:80]
        for round in 1:500
            n = nextbelow!(random, 12)
            pairs = Int32[]
            expected = falses(80)
            for k in 1:n
                lo = nextbelow!(random, 60)
                hi = lo + nextbelow!(random, 8)
                push!(pairs, lo, hi)
                expected[(lo + 1):(hi + 1)] .= true
            end
            push!(pairs, 7, 9)
            set = E.setof(pairs, 2n)
            @test bits(set) == expected
            @test all(set[i] > set[i - 1] + 1 for i in 3:2:length(set))
            @test bits(E.complement(set)) == .!expected
            @test E.complement(E.complement(set)) == set
            other = E.setof(Int32[nextbelow!(random, 70), 70 + nextbelow!(random, 5)])
            @test bits(E.setunion(set, other)) == (expected .| bits(other))
            @test bits(E.intersection(set, other)) == (expected .& bits(other))
            @test bits(E.difference(set, other)) == (expected .& .!bits(other))
            @test E.intersects(set, other) == any(expected .& bits(other))
            @test all(E.inset(set, c) == expected[c + 1] for c in 0:79)
        end
    end

    # The two Canonicalize functions of ECMA-262.
    @testset "case folding" begin
        closure(set, unicode) = E.foldclosure(Int32[set...], unicode)
        # With the u flag grammar: simple case folding.
        @test closure(('k', 'k'), true) == Int32['K', 'K', 'k', 'k', 0x212A, 0x212A]
        @test closure(('S', 'S'), true) == Int32['S', 'S', 's', 's', 0x17F, 0x17F]
        @test closure((0xDF, 0xDF), true) == Int32[0xDF, 0xDF, 0x1E9E, 0x1E9E]
        @test closure((0x3C2, 0x3C2), true) == Int32[0x3A3, 0x3A3, 0x3C2, 0x3C3]
        @test closure((0x130, 0x131), true) == Int32[0x130, 0x131]
        @test closure((0x10400, 0x10400), true) == Int32[0x10400, 0x10400, 0x10428, 0x10428]
        @test closure(('a', 'z'), true) == Int32['A', 'Z', 'a', 'z', 0x17F, 0x17F, 0x212A, 0x212A]
        # With no flag: the uppercase of one UTF-16 code unit, never from outside ASCII into ASCII.
        @test closure(('k', 'k'), false) == Int32['K', 'K', 'k', 'k']
        @test closure((0x17F, 0x17F), false) == Int32[0x17F, 0x17F]
        @test closure((0xDF, 0xDF), false) == Int32[0xDF, 0xDF]
        @test closure((0x1F80, 0x1F80), false) == Int32[0x1F80, 0x1F80]
        @test closure((0x10400, 0x10400), false) == Int32[0x10400, 0x10400]
        @test closure((0xB5, 0xB5), false) == Int32[0xB5, 0xB5, 0x39C, 0x39C, 0x3BC, 0x3BC]
        @test closure((0x1C5, 0x1C5), false) == Int32[0x1C4, 0x1C6]
        @test E.inset(E.cased(true), 0x212A)
        @test !E.inset(E.cased(false), 0x212A)
        @test !E.inset(E.cased(true), Int32('1'))
    end

    # The characters of a group name.
    @testset "group name characters" begin
        @test E.isnamestart(Int32('a'))
        @test E.isnamestart(Int32('$'))
        @test E.isnamestart(Int32('_'))
        @test !E.isnamestart(Int32('1'))
        @test !E.isnamestart(Int32('-'))
        @test E.isnamestart(0x3C0)
        @test E.isnamestart(0x1D4D1)
        @test !E.isnamestart(0x1F600)
        @test !E.isnamestart(0x200D)
        @test E.isnamepart(Int32('1'))
        @test E.isnamepart(0x200C)
        @test E.isnamepart(0x200D)
        @test E.isnamepart(0x301)
        @test !E.isnamepart(Int32(' '))
        @test !E.isnamepart(0x1F600)
    end

    # What the module leaves to PCRE2 of case-insensitive matching is the comparison of a backreference. PCRE2
    # compares by Unicode simple case folding, as ECMA-262 does with the u flag, in the Unicode version it was built
    # with. Every pair of characters with a case variant is tried here. PCRE2 holds no pair equivalent that the data
    # does not, and the pairs of the data it lacks are exactly those Unicode has added since its version.
    @testset "what PCRE2 holds equivalent" begin
        engine = VersionNumber(E.pcreunicodeversion())
        @test engine <= VersionNumber(E.UNICODE_VERSION)
        # PCRE2 holds no character with no case variant in the data equivalent to one that has a variant.
        caseless = E.complement(E.setunion(E.cased(true), E.SURROGATE_SET))
        @test matchedset(rawpattern("(?i:" * classtext(caseless) * ")")) == astext(caseless)
        folding = E.folding(true)
        pattern = E.compile("^(.)(?i:\\1)\$")
        bytes = [Vector{UInt8}(string(Char(c))) for c in folding.points]
        lacking, added = comparedpairs(pattern, folding, bytes)
        @test isempty(added)
        expected = Tuple{Int,Int}[]
        for (a, b) in vcat(engine < v"17" ? PAIRS_OF_UNICODE_17 : [], engine < v"15" ? PAIRS_SINCE_UNICODE_14 : [])
            push!(expected, (a, b), (b, a))
        end
        if engine >= v"16" || engine < v"15"
            @test sort(lacking) == sort(expected)
        else
            # A PCRE2 with the data of Unicode 15, which no Julia this was run on has, lacks some of the thirty.
            @test issubset(expected, lacking)
            @test all(p -> minmax(p...) in vcat(PAIRS_OF_UNICODE_17, PAIRS_SINCE_UNICODE_14), lacking)
        end
        # Every character is equivalent to itself.
        @test all(b -> E.ismatch(pattern, vcat(b, b)), bytes)
    end
end
