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
        # A pattern that is valid only without the u flag is matched by UTF-16 code unit, so a text with a character
        # beyond the Basic Multilingual Plane is written again for it. It is written into a buffer that is kept.
        for pattern in [E.compile(raw"^\-?.+$"), E.compile(raw"\&?[\u{1F600}x]{2}"), E.compile(raw"(?:\ud83d)\-?")]
            @test !pattern.unicode
            @test allocated(pattern, bytes, 1000) == 0
            @test allocated(pattern, view(bytes, 3:40), 1000) == 0
            @test allocated(pattern, codeunits(text), 1000) == 0
            @test allocated(pattern, text, 1000) == 0
            @test allocated(pattern, SubString(text, 5, 30), 1000) == 0
            @test allocated(pattern, "\U10400", 1000) == 0
            @test allocated(pattern, "a\U1F600"^500, 1000) == 0
        end
        @test E.ismatch(E.compile(raw"(?:\ud83d)\-?"), bytes)
        @test !E.ismatch(E.compile(raw"^\-?.$"), "\U10400")
        # The buffer has the length of what was written, whatever was written into it before.
        units = UInt8[]
        @test String(copy(E.codeunits!(units, codeunits("a\U10400\u00e9\U1F600")))) ==
              "a\U000f0001\U000f0400\u00e9\U000f003d\U000f0600"
        @test String(copy(E.codeunits!(units, codeunits("\U10400")))) == "\U000f0001\U000f0400"
        @test isempty(E.codeunits!(units, UInt8[]))
        @test E.codeunits!(units, [0xf0, 0x90, 0x61]) == [0xf0, 0x90, 0x61]
        # A buffer that has grown past MAX_KEPT_UNITS is not kept, and the match is right all the same.
        long = "\U10400"^(E.MAX_KEPT_UNITS ÷ 8 + 1)
        @test !E.ismatch(E.compile(raw"^\-?.$"), long)
        @test E.ismatch(E.compile(raw"^\-?\ud801\udc00\ud801"), long)
        @test all(buffer -> length(buffer) <= E.MAX_KEPT_UNITS, E.UNIT_BUFFERS)
        # Matches of this kind from several tasks at once.
        dot = E.compile(raw"^\-?.{4}$")
        tasks = map(1:8) do task
            Threads.@spawn begin
                wrong = 0
                for round in 1:2000
                    wrong += !E.ismatch(dot, "\U10400"^2)
                    wrong += E.ismatch(dot, "\U10400"^(2 + round % 3 + 1))
                    (round + task) % 97 == 0 && yield()
                end
                wrong
            end
        end
        @test sum(fetch, tasks) == 0
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

    # A match is limited in time. What ends it is a clock and not a count of steps, so the tests are of patterns
    # that would run for hours, or for ever, and of bounds that a loaded machine keeps: a match that is given up
    # is given up in less than twenty times its limit, where it is measured at 1.0 to 1.1 times.
    @testset "a match is limited in time" begin
        # Each a can be matched two ways, so this tries 2^40 ways before it finds no match.
        slow = E.compile("^(?:a|a)+\$")
        @test E.ismatch(slow, "a"^40)
        started = time()
        error = try
            E.ismatch(slow, "a"^40 * "b")
            nothing
        catch e
            e
        end
        took = time() - started
        @test error isa E.MatchError
        @test error.pattern == "^(?:a|a)+\$"
        @test error.limit == :time
        @test error.code == 0
        @test occursin("the limit on its time, which is 1 second", sprint(showerror, error))
        # One second is the limit when none is given.
        @test E.MatchLimit().nanoseconds == 1_000_000_000
        @test 0.9 < took < 20
        # The pattern is still good after an error.
        @test E.ismatch(slow, "aaa")
        @test !E.ismatch(slow, "b")
    end

    @testset "the limit a caller gives is the one that ends a match" begin
        slow = E.compile("^(?:a|a)+\$")
        function given(limit)
            started = time()
            error = try
                E.ismatch(slow, "a"^40 * "b", E.MatchLimit(limit))
                nothing
            catch e
                e
            end
            return error, time() - started
        end
        error, took = given(0.05)
        @test error isa E.MatchError && error.limit == :time
        @test occursin("which is 0.05 seconds", error.message)
        @test 0.04 < took < 1
        error, took = given(2)
        @test error isa E.MatchError && error.limit == :time
        @test occursin("which is 2 seconds", error.message)
        @test 1.9 < took < 40
        # 2^22 ways take a hundredth of a second at least and a few seconds at most. A limit of a millisecond ends
        # the match, and a limit of two minutes lets it answer.
        text = "a"^22 * "b"
        @test_throws E.MatchError E.ismatch(slow, text, E.MatchLimit(0.001))
        @test !E.ismatch(slow, text, E.MatchLimit(120))
        @test E.ismatch(slow, "a"^22, E.MatchLimit(0.001))
        # A limit is a positive time. No time is not "no limit".
        @test_throws ArgumentError E.MatchLimit(0)
        @test_throws ArgumentError E.MatchLimit(-1)
        @test_throws ArgumentError E.MatchLimit(Inf)
        @test_throws ArgumentError E.MatchLimit(NaN)
        @test E.MatchLimit(nothing).nanoseconds == 0
        @test E.MatchLimit(1.5).nanoseconds == 1_500_000_000
    end

    # With no limit a match runs until it answers, or until PCRE2 has counted every step it can be told to allow.
    @testset "the limit can be turned off" begin
        slow = E.compile("^(?:a|a)+\$")
        @test !E.ismatch(slow, "a"^22 * "b", E.MatchLimit(nothing))
        @test E.ismatch(slow, "a"^22, E.MatchLimit(nothing))
        # PCRE2 allows a match ten million steps unless it is told another number, which is too few for three
        # patterns of the ecma262-regex-test-data files on these texts. V8 answers each, and so does the package
        # when the limit on time is off. The first is the one that takes the most steps of any pattern of those
        # files, 157 million. (With the limit on, each is run by the code that is timed once its first steps are
        # used, and may reach the limit.)
        cases = [
            (raw"^(?:(?!$)|(?:(?<n0>(?:(?<n1>(a|)(a|))|(?<n1>(|a)xx)){3,4}(a|))|(?<n0>.a)){3,}^.){2,2}" *
             raw"(?:(?<!a)\6|[^a](((?=a)){2,2}(?:ax*|\10{2}){0,0}?|\8\10|a-){1,2}|(?<=x)\k<n1>*($|(a$(|a))+|a*?x+){1,}?){2,}x$",
                "aaaaa", false),
            (raw"(?:\1?){3,4}?\1(?:(?<n0>a(?=a))|(?<n0>((?:.*?a|\3){3,}|^|(?<=x)){3,}[ax](?<n1>\2.){0,2}?))*?\2a",
                "xaxax", true),
            (raw"^a{2}?(((?!)|((|a){2,}(|a)\B|){2,3}?){2,3}.{3}a{2})\4", "aaaaaaaaaaxa", true),
        ]
        for (pattern, text, wanted) in cases
            @test E.ismatch(E.compile(pattern), text, E.MatchLimit(nothing)) == wanted
            answer = try
                E.ismatch(E.compile(pattern), text)
            catch e
                e isa E.MatchError && e.limit == :time ? wanted : rethrow()
            end
            @test answer == wanted
        end
    end

    # What PCRE2 counts does not bound the time of a match: it counts the steps of each place a match is tried from
    # apart, and one step may read the whole text. Each of these ran for longer than anyone waited, within the
    # 500 million steps a match had before, and all are given up at the limit now.
    @testset "matches that a count of steps does not end" begin
        long = "a"^100_000
        for pattern in [
                # A loop around a lookahead that reads to the end of the text, three deep.
                "^(?:(?=(?:(?=(?:(?=a*)a)*)a)*)a)*b\$",
                # A step that reads the whole text.
                "^(?:(?:a*b|a)*c|a)*d",
                # The same from every place of the text.
                "(?:a*b|a)*c",
                # A hundred and forty lookaheads that each read the whole text, from every place of it.
                "(?=a*)"^140 * "b",
                # Repetitions of one character, which PCRE2 counts a step of only when it comes back to one.
                "a*a*a*a*a*b",
                # A lookbehind that is searched for, at every place.
                "(?<=a*b*)c",
            ]
            compiled = E.compile(pattern)
            started = time()
            error = try
                E.ismatch(compiled, long, E.MatchLimit(0.2))
            catch e
                e
            end
            @test error isa E.MatchError && error.limit == :time
            @test 0.15 < time() - started < 8
        end
        # A lookbehind that is written as a search, with the rest of the pattern inside it, takes time that grows
        # with the square of the text before it. It answers or is given up.
        search = E.compile("(?<=([abc])+)x\\1")
        @test E.ismatch(search, "abc"^300 * "xa", E.MatchLimit(120))
        @test !E.ismatch(search, "abc"^300 * "xc", E.MatchLimit(120))
        started = time()
        answer = try
            E.ismatch(search, "abc"^5333 * "xa", E.MatchLimit(0.2))
        catch e
            e isa E.MatchError && e.limit == :time ? true : rethrow()
        end
        @test answer
        @test time() - started < 8
    end

    # The same through a lookbehind that is a callout, whose body is searched for by matches of its own.
    @testset "the searches for a lookbehind are within the limit of the match" begin
        behind = E.compile("^a*b?(?<=^(?:a|a)+ba*)c")
        @test E.ismatch(behind, "a"^40 * "bc")
        started = time()
        error = try
            E.ismatch(behind, "a"^40 * "c", E.MatchLimit(0.2))
        catch e
            e
        end
        @test error isa E.MatchError && error.limit == :time
        @test 0.15 < time() - started < 8
        # The body of a lookbehind does not run past the place the lookbehind stands at, and its starts are tried
        # from that place backwards, so these two end at once.
        @test !E.ismatch(E.compile("^a*(?<=^(?:a|a)+)c"), "a"^40 * "bc")
        @test !E.ismatch(E.compile("^a*(?<=^(?:a|aa)*)b"), "a"^60 * "c")
        # One search of this one tries a thousand ways and more from each start, and a text of four hundred a
        # has it searched four hundred times, which takes seconds. Thirty answer.
        many = E.compile("^[ab]*(?<=(?:a|aa){10}ba*)c")
        @test E.ismatch(many, "a"^12 * "bc")
        @test !E.ismatch(many, "a"^30 * "c", E.MatchLimit(120))
        @test_throws E.MatchError E.ismatch(many, "a"^400 * "c", E.MatchLimit(0.2))
        @test E.ismatch(many, "a"^12 * "bc")
    end

    # A match that uses the steps it has before it is timed is run again by code that reads a clock, from the
    # start, and answers what the first code would have answered. The second stage is run here by itself, with
    # two minutes, on every text, and through `ismatch`, where a text of twenty and more characters each matched
    # two ways uses the steps of the first stage.
    @testset "a match that is timed answers the same" begin
        function timed(compiled, text, interpreted)
            GC.@preserve compiled text begin
                return E.timedmatch(compiled, pointer(text), sizeof(text), Csize_t(0), time_ns() + 120_000_000_000,
                    UInt64(120_000_000_000), interpreted)
            end
        end
        for (pattern, matching, failing) in [
                ("^(?:a|a)+\$", "a"^14, "a"^14 * "b"),
                ("^(?:(a)|(a))+\\1?\$", "a"^14, "a"^14 * "b"),
                ("(?:x|x)+(?<=^[^y]*)y\$", "x"^14 * "y", "x"^14 * "yz"),
                ("^(?i:(?:k|\\u212a)+)\$", "kK"^7, "kK"^7 * "s"),
                ("(?<=([abc])+)x\\1", "abc"^20 * "xa", "abc"^20 * "xc"),
                ("^\\p{L}+\\d\$", "aé"^500 * "1", "aé"^500),
                ("b(?<=a{3}b)", "ab"^500 * "aaab", "ab"^500),
            ]
            compiled = E.compile(pattern)
            @test isempty(E.timedcodes(compiled))
            for interpreted in (false, true)
                @test timed(compiled, matching, interpreted)
                @test !timed(compiled, failing, interpreted)
                @test !timed(compiled, "", interpreted)
            end
            @test length(E.timedcodes(compiled)) == 1
            @test E.ismatch(compiled, matching, E.MatchLimit(nothing))
            @test !E.ismatch(compiled, failing, E.MatchLimit(nothing))
        end
        # Through ismatch: 2^22 ways are more steps than the first stage of a match has with a limit of a second,
        # and the code that is timed is compiled when a match first needs it. A machine that leaves the match less
        # than its second has it given up, which is not an answer.
        slow = E.compile("^(?:a|a)+\$")
        @test isempty(E.timedcodes(slow))
        answer = try
            E.ismatch(slow, "a"^22 * "b")
        catch e
            e isa E.MatchError && e.limit == :time ? false : rethrow()
        end
        @test !answer
        @test length(E.timedcodes(slow)) == 1
        # A match of a few steps is never timed, and its pattern is compiled once.
        plain = E.compile("^[a-z]+\\d\$")
        @test E.ismatch(plain, "abc1") && !E.ismatch(plain, "abc")
        @test isempty(E.timedcodes(plain))
    end

    # A pattern that is not anchored is tried from every place of the text, and PCRE2 gives each place the steps
    # of the match. The search of a long text is run in parts, between which the clock is read. The parts start at
    # characters, see the whole text behind and ahead, and answer what one search would.
    @testset "the search of a long text is run in parts" begin
        word = E.compile("foo|bar")
        @test word.freelength < 100_000
        for filler in ("x", "é", "€", "\U0001F600", "xé€\U0001F600")
            long = filler^(400_000 ÷ sizeof(filler))
            @test E.ismatch(word, long * "bar")
            @test E.ismatch(word, "foo" * long)
            @test E.ismatch(word, long * "foo" * long)
            @test !E.ismatch(word, long * "baz")
            # A lookbehind and a lookahead across the place two parts meet at, and the end of the text.
            @test E.ismatch(E.compile("(?<=" * filler * ")b(?=" * filler * ")"), long * "b" * long)
            @test !E.ismatch(E.compile("(?<=" * filler * ")b(?=" * filler * "c)"), long * "b" * long)
            @test E.ismatch(E.compile("(?<=" * filler * ")\$"), long)
            @test E.ismatch(E.compile("[^" * first(filler) * "]\$"), long * "!")
            @test !E.ismatch(E.compile("!\\b."), long * "!")
            # A lookbehind that is a callout, in a search in parts.
            @test E.ismatch(E.compile("b(?<=^(?:" * filler * ")*b)"), long * "b")
            @test !E.ismatch(E.compile("b(?<=^(?:" * filler * ")*b)"), long * "ab")
            @test allocated(word, long * "baz", 3) == 0
        end
        # Every place is tried once: a pattern that matches at one place only, wherever that is.
        once = E.compile("b(?<=^a{0,300000}b)")
        for at in (0, 1, 437, 438, 439, 440, 70_000, 199_999, 200_000)
            @test E.ismatch(once, "a"^at * "b" * "a"^(200_000 - at))
        end
        # A search that takes the square of the length of the text, with one step for each place, is given up at
        # the limit by the clock between two parts, and answers when it has the time.
        square = E.compile("a*b")
        started = time()
        @test_throws E.MatchError E.ismatch(square, "a"^300_000, E.MatchLimit(0.05))
        @test time() - started < 8
        @test !E.ismatch(square, "a"^20_000, E.MatchLimit(120))
        @test E.ismatch(square, "a"^20_000 * "b", E.MatchLimit(120))
    end

    # The memory of a match is limited too. JIT code keeps what it may come back to on a stack of a megabyte. A
    # match that needs more is run again by the interpreter, which has 64 MiB, and gives its answer. A match that
    # needs more than that is an error, whatever the limit on time is.
    @testset "a match too deep for the JIT stack" begin
        deep = E.compile("^(?:ab|cd)*\$")
        long = "ab"^100_000
        # JIT code alone does not get through this text.
        frame = unsafe_load(E.threadframe())
        E.setsteps!(frame, E.MOST_STEPS)
        rc = E.pcrematch(deep.code, pointer(long), Csize_t(sizeof(long)), UInt32(0), frame)
        E.setsteps!(frame, E.QUICK_STEPS)
        @test rc == E.PCRE2_ERROR_JIT_STACKLIMIT
        for limit in (E.MatchLimit(), E.MatchLimit(120), E.MatchLimit(nothing))
            @test E.ismatch(deep, long, limit)
            @test !E.ismatch(deep, long * "x", limit)
            @test E.ismatch(deep, long * "cd", limit)
            @test E.ismatch(deep, "abcd", limit)
            # The same in the search for a lookbehind that is a callout, which the match reaches once.
            behind = E.compile("^[a-d]*(?<=^(?:ab|cd)*)x")
            @test E.ismatch(behind, long * "x", limit)
            @test E.ismatch(behind, "abx", limit)
            @test !E.ismatch(behind, "abax", limit)
            error = try
                E.ismatch(deep, "ab"^2_000_000, limit)
                nothing
            catch e
                e
            end
            @test error isa E.MatchError
            @test error.limit == :memory
            @test error.code == E.PCRE2_ERROR_HEAPLIMIT
            @test occursin("more memory than the limit of 64 MiB", sprint(showerror, error))
            @test E.ismatch(deep, "abab", limit)
        end
    end

    # A match sets no limit and makes no context when it ends within its first steps: the frame of the thread and
    # its context are the same after matches as before, they are left with the steps a match starts with, and the
    # matches allocate nothing. Nor does a match that is timed, or one that is given up, once the pattern has been
    # compiled for it.
    @testset "the limit allocates nothing" begin
        before = (E.threadframe(), unsafe_load(E.threadframe()).context)
        @test allocated(E.compile("^[a-z]+\\d\$"), "abc1", 10_000) == 0
        @test allocated(E.compile("(?<=^(?:ab)*)c"), "ababc", 10_000) == 0
        # More steps than a match starts with, and fewer than it has untimed.
        @test allocated(E.compile("^(?:a|a)+\$"), "a"^12 * "b", 100) == 0
        # A match that is timed.
        @test allocated(E.compile("^(?:a|a)+\$"), "a"^17 * "b", 20) == 0
        # A long text for a pattern that is anchored, which is given its steps by its length.
        @test allocated(E.compile("^[a-z]+\\d\$"), "a"^200_000 * "1", 10) == 0
        slow = E.compile("^(?:a|a)+\$")
        text = "a"^40 * "b"
        limit = E.MatchLimit(0.01)
        giveup() = try
            E.ismatch(slow, text, limit)
        catch e
            e isa E.MatchError || rethrow()
            false
        end
        giveup()
        # The error itself is allocated, and its message. Nothing else is, however long the match ran.
        @test (@allocated giveup()) < 2_000
        @test (E.threadframe(), unsafe_load(E.threadframe()).context) == before
        # A thread that has matched nothing yet has the limit too.
        fresh = Threads.@spawn try
            E.ismatch(E.compile("^(?:a|a)+\$"), "a"^40 * "b", E.MatchLimit(0.05))
        catch e
            e
        end
        @test fetch(fresh) isa E.MatchError
    end

    # What a match may do before it is timed is worked out from the translation: the most PCRE2 does between two
    # steps is so much, and so much for each byte of the text.
    @testset "the cost of a step is read off the translation" begin
        @test E.stepcost("a") == (2, 1)
        @test E.stepcost("abc") == (4, 1)
        # A repetition with no upper count runs its body once more for each byte.
        @test E.stepcost("a*") == (2, 2)
        @test E.stepcost("(?:ab)+c") == (8, 4)
        # A count multiplies.
        @test E.stepcost("a{1000}") == (1001, 1)
        @test E.stepcost("(?:a{100}){100}") == (10_101, 1)
        @test E.stepcost("(?:a{0,1000}){0,1000}")[1] > 1_000_000
        @test E.stepcost("(?:(?:a{1000}){1000}){1000}")[1] > 1_000_000_000
        # Alternatives add, a class costs by its length, a call costs as a class of many ranges does.
        @test E.stepcost("a|b|c") == (4, 1)
        @test E.stepcost("[a-c]") == (2, 1)
        for slow in (true, false)
            E.SLOW_CLASSES[] = slow
            try
                # A class of fifty ranges, where PCRE2 tries the ranges one after another and where it halves them.
                @test E.stepcost("[" * "\\x{100}-\\x{101}"^50 * "]")[2] == (slow ? 16 : 5)
            finally
                E.SLOW_CLASSES[] = nothing
            end
        end
        @test E.stepcost("(?&s1)")[2] == 2 * E.CALL_COST
        # Every form the emitter writes is read: nothing is left over, so the cost is not the one of a text that
        # is not understood, which is its length for both numbers.
        for pattern in ["(?<=([abc])+)x\\1", "(?<=a*b*)c", "^\\p{L}+\$", "(?i:(.)\\1)", "^(?:(a)|b)*\\1\$", "(?<n>a)\\k<n>",
                "a{2,}?b*?c+?d??", "(?=a)(?!b)(?<=c)(?<!d)", "[\\]\\\\^-]", "\\u{1F600}{3}", "^(?:(?:a|){1,300}x){1,300}\$",
                "\\p{L}{100000}", "(?<=(ab|a)x)-\\1", "^(?:(?=([ab]))[ab](?:c|ca)\\1)*\$"]
            translation = E.translate(pattern)
            for text in vcat(translation.main, translation.lookbehinds)
                reader = E.CostReader(codeunits(text), 1, 1, 0, false)
                E.sequencecost(reader)
                @test !reader.failed && reader.at == ncodeunits(text) + 1
            end
        end
        # A text that is not one the emitter writes costs its length.
        @test E.stepcost("(a") == (3, 3)
        @test E.stepcost("a)") == (3, 3)
        # The longest text a match of which starts with no working out, and the steps of a longer one.
        anchored = E.compile("^[a-z]+\$")
        @test anchored.anchored && anchored.jit == E.hasjitcode(anchored.code)
        second = E.DEFAULT_MATCH_LIMIT
        steps(len, limit) = E.freesteps(anchored.fixedcost, anchored.textcost, true, anchored.jit, len, limit)
        @test steps(anchored.freelength, second) >= E.QUICK_STEPS
        @test steps(anchored.freelength + 1, second) < E.QUICK_STEPS
        # The first stage is in proportion to the limit, so a short limit is kept too.
        @test steps(1000, second ÷ 10) == steps(1000, second) ÷ 10
        @test abs(steps(100_000, 10 * second) - 10 * steps(100_000, second)) <= 10
        @test steps(1000, UInt64(999_999)) == 0
        searched = E.compile("[a-z]+\\d")
        @test !searched.anchored
        @test searched.freelength < anchored.freelength
        @test E.freelength(E.MOST_COST, E.MOST_COST, true, true) == -1
    end
end
