# How a compiled pattern is matched. The tests are of what it takes as text, that a match allocates nothing, that
# one pattern may be matched from several tasks at once, and that a match that is given up on is an error.

# The allocation is measured inside a function, after a call that compiles it.
function allocated(pattern, text, rounds::Int)
    E.ismatch(pattern, text)
    return @allocated for _ in 1:rounds
        E.ismatch(pattern, text)
    end
end

function allocatedvalidating(pattern, rounds::Int)
    E.isvalidpattern(pattern)
    return @allocated for _ in 1:rounds
        E.isvalidpattern(pattern)
    end
end

@testset "matching" begin
    @testset "the text is matched where it is" begin
        pattern = E.compile("^[a-c]+\$")
        bytes = Vector{UInt8}("xabcx")
        @test !E.ismatch(pattern, bytes)
        @test E.ismatch(pattern, view(bytes, 2:4))
        @test E.ismatch(pattern, @view bytes[2:2])
        @test !E.ismatch(pattern, view(bytes, 2:5))
        @test E.ismatch(pattern, codeunits("abc"))
        @test E.ismatch(pattern, "abc")
        @test E.ismatch(pattern, SubString("xabcx", 2, 4))
        @test !E.ismatch(pattern, SubString("xabcx", 1, 4))
        # A vector that is not contiguous bytes is copied.
        @test E.ismatch(pattern, view(bytes, 4:-1:2))
        @test E.ismatch(pattern, view(bytes, 2:2:4))
        @test E.ismatch(pattern, 0x61:0x63)
        @test !E.ismatch(pattern, 0x61:0x64)
        # The empty text.
        empty = E.compile("^\$")
        @test E.ismatch(empty, UInt8[])
        @test E.ismatch(empty, "")
        @test E.ismatch(empty, view(bytes, 3:2))
        @test E.ismatch(empty, codeunits(""))
        @test !E.ismatch(E.compile("a"), UInt8[])
        @test E.ismatch(E.compile(""), UInt8[])
        # A zero byte is a character like another, and the text does not end at it.
        @test E.ismatch(E.compile("^a\\0b\$"), UInt8[0x61, 0x00, 0x62])
        @test !E.ismatch(E.compile("^a\$"), UInt8[0x61, 0x00, 0x62])
        @test E.compile("a") isa E.Pattern
        @test sprint(show, E.compile("a+")) == "EcmaRegex.Pattern(\"a+\")"
        @test E.compile(SubString("xa+y", 2, 3)).source == "a+"
    end

    @testset "a match allocates nothing" begin
        bytes = Vector{UInt8}("The quick brown fox \u00e9\U1F600 jumps over 2015-10-08")
        text = String(copy(bytes))
        patterns = [
            E.compile("^[a-z]+\$"),
            E.compile("\\d{4}-\\d{2}-\\d{2}"),
            E.compile("(?i:QUICK).*\\p{L}\\P{L}"),
            # A backreference, with a group that may not have taken part.
            E.compile("(?:(o)|x)\\w*\\1"),
            # A lookbehind PCRE2 runs itself, and lookbehinds that are callouts, one inside the other.
            E.compile("(?<=fox )\u00e9"),
            E.compile("(?<=quick.*)fox"),
            E.compile("(?<=(?<=T.*)quick.*)fox"),
            E.compile("(?<!The.*)fox"),
            E.compile("\\bjumps\\b(?m:\$)?"),
        ]
        for pattern in patterns
            @test allocated(pattern, bytes, 1000) == 0
            @test allocated(pattern, view(bytes, 3:40), 1000) == 0
            @test allocated(pattern, codeunits(text), 1000) == 0
            @test allocated(pattern, text, 1000) == 0
            @test allocated(pattern, SubString(text, 5, 30), 1000) == 0
        end
        @test E.ismatch(patterns[7], bytes)
        @test !E.ismatch(patterns[8], bytes)
    end

    @testset "validating a pattern allocates nothing" begin
        for pattern in ["^[a-z][a-z0-9_]*\$", "(?<year>\\d{4})-(?<month>\\d{2})\\k<year>", "\\p{Script=Greek}+",
            "(?:(?<a>x)|(?<a>y))\\k<a>[\\u{1F600}-\\u{1F64F}]", "a{2,3}?(?i-s:b)\u00e9\U1F600"]
            @test E.isvalidpattern(pattern)
            @test allocatedvalidating(pattern, 1000) == 0
        end
    end

    @testset "one pattern from several tasks" begin
        patterns = [E.compile("^(?:[a-z]+\\d)+\$"), E.compile("(?<=^(?:ab)*)c(\\d)\\1\$"),
            E.compile("^(?:(a)|b)+\\1\\p{Lu}")]
        texts = [Vector{UInt8}("ab" ^ (k % 7) * "c" * string(k % 10) * string((k ÷ 3) % 10)) for k in 1:64]
        append!(texts, [Vector{UInt8}("abc" ^ (k % 5) * string(k % 10)) for k in 1:32])
        append!(texts, [Vector{UInt8}("ab" ^ (k % 3) * "a" ^ (k % 4) * "aZ") for k in 1:32])
        expected = [[E.ismatch(p, t) for t in texts] for p in patterns]
        @test all(any, expected)
        @test !any(all, expected)
        tasks = map(1:16) do task
            Threads.@spawn begin
                wrong = 0
                for round in 1:300, (i, p) in enumerate(patterns), (k, t) in enumerate(texts)
                    wrong += E.ismatch(p, t) != expected[i][k]
                    # A task may move to another thread between two matches.
                    (round + k + task) % 97 == 0 && yield()
                end
                wrong
            end
        end
        @test sum(fetch, tasks) == 0
        # A pattern compiled by one task and matched by others, while patterns are compiled.
        compiling = map(1:8) do task
            Threads.@spawn begin
                wrong = 0
                for round in 1:200
                    p = E.compile("^(?<=^.*)x{$(round % 5)}(?i:k)\$")
                    inner = Threads.@spawn E.ismatch(p, "x" ^ (round % 5) * "\u212a")
                    wrong += !fetch(inner)
                    wrong += E.ismatch(p, "x" ^ 6 * "k")
                end
                wrong
            end
        end
        @test sum(fetch, compiling) == 0
    end

    # A match PCRE2 gives up on is an error, never "no match".
    @testset "a match that reaches a limit is an error" begin
        # Each a can be matched two ways, so this backtracks through more ways than PCRE2's limit of ten million.
        slow = E.compile("^(?:a|a)+\$")
        @test E.ismatch(slow, "a"^40)
        @test_throws E.MatchError E.ismatch(slow, "a"^40 * "b")
        error = try
            E.ismatch(slow, "a"^40 * "b")
            nothing
        catch e
            e
        end
        @test error isa E.MatchError
        @test error.pattern == "^(?:a|a)+\$"
        @test error.code == -47
        @test occursin("match limit", sprint(showerror, error))
        # The same through a lookbehind that is a callout. The error comes out of the search for its body.
        behind = E.compile("^a*(?<=^(?:a|a)+)c")
        @test E.ismatch(behind, "a"^40 * "c")
        @test_throws E.MatchError E.ismatch(behind, "a"^40 * "bc")
        # The searches for the lookbehinds of one match have a limit together.
        many = E.compile("^a*(?<=^(?:a|aa)*)b")
        @test E.ismatch(many, "aaab")
        @test_throws E.MatchError E.ismatch(many, "a"^60 * "c")
        # The pattern is still good after an error.
        @test E.ismatch(slow, "aaa")
        @test !E.ismatch(slow, "b")
    end

    # JIT code keeps what it may backtrack to on a stack of a megabyte. A match that needs more is run again by the
    # interpreter, which has more room (64 MiB), and gives its answer. A match that needs more than that is an error.
    @testset "a match too deep for the JIT stack" begin
        deep = E.compile("^(?:ab|cd)*\$")
        long = "ab"^100_000
        # JIT code alone does not get through this text.
        frame = unsafe_load(E.threadframe())
        @test E.pcrematch(deep.code, pointer(long), Csize_t(sizeof(long)), UInt32(0), frame) ==
              E.PCRE2_ERROR_JIT_STACKLIMIT
        @test E.ismatch(deep, long)
        @test !E.ismatch(deep, long * "x")
        @test E.ismatch(deep, long * "cd")
        @test E.ismatch(deep, "abcd")
        # The same in the search for a lookbehind that is a callout, which the match reaches once.
        behind = E.compile("^[a-d]*(?<=^(?:ab|cd)*)x")
        @test E.ismatch(behind, long * "x")
        @test E.ismatch(behind, "abx")
        @test !E.ismatch(behind, "abax")
        @test_throws E.MatchError E.ismatch(deep, "ab"^2_000_000)
        @test E.ismatch(deep, "abab")
    end
end
