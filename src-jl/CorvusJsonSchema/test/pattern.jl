# The pattern shapes against the regular expression engine, and which patterns take which matcher. Ported from
# pattern_test.go.

const TEST_PATTERNS = String[
    "", ".*", "^.*", ".*\$", "[\\s\\S]*", "^[\\s\\S]*", "^[\\s\\S]*\$", "^.*\$", "^[@\$_#]",
    "^[a-zA-Z0-9_\\.\\-\\|@#]*\$", "^[a-zA-Z0-9_\\-]*\$", ".+", "^\\{\\{[^\\W\\.\\-][\\w\\.\\-]*\\}\\}\$",
    "^.{1,256}\$", "^[A-Z0-9_\\-\\/]+\$", "^[a-zA-Z0-9_\\.\\-]+[\\|]?[a-zA-Z0-9_\\.\\-]+\$",
    "^([a-zA-Z_\$][a-zA-Z0-9_\$]{0,39}\\.)*([a-zA-Z_\$][a-zA-Z0-9_\$]{0,39})\$", "^x-", "^[1-5](?:[0-9]{2}|XX)\$",
    "(base64key|awskms)://(.*)", "^[A-F0-9]{1,32}\$", "^[a-z][a-z0-9]{0,29}\$",
    "^([t|T][o|O][p|P])|([c|C][e|E][n|N][t|T][e|E][r|R])\$", "^[\\w\\*]{0,60}\$",
    "^(?:@[0-9a-z-_.]+\\/)?[a-z][0-9a-z-_.]*\$", "^[a-z][a-z0-9_]+\$", "^\\d+[:-]\\d+\$", "^#[0-9a-fA-F]{6}\$",
    "^[^:]+:[^:]+\$", "^(?=[^!*,;{}[\\]~\\n]+\$)(?=(.*\\w)).+\$", "^.*\\.(?:txt|trie)(?:\\.gz)?\$",
    "^([-\\w_\\s]+)(,[-\\w_\\s]+)*\$", "^(!?[-\\w_\\s]+)|(\\*)\$", "^[0-9]+(ns|ms|us|µs|s|m|h)\$",
    "^\\/[^\\*\\?\\&\\%]*(\\/\\*)?\$", "^[^- @#\$%^&()!]+\$", "^((\\.(?!\\.)\\/)?\\w+\\/?)+\$",
    "^[0-9]{1,}.[0-9]{1,}.[0-9]{1,}\$", "\\{.*\\}", "^[a-z]{1,2}\$", "^abc\$", "^\\/", "^es\$",
    "^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-((?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\\.(?:0|[1-9]" *
    "\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\\+([0-9a-zA-Z-]+(?:\\.[0-9a-zA-Z-]+)*))?\$",
    "^[Ee][Ss]5|[Ee][Ss]6|[Ee][Ss]7\$", "^[a-z]*a\$", "^a*a", "^a+b?a", "^a+b?c*a\$", "^[a-z]+-?[a-z]+\$", "\\bfoo",
    "^\\p{L}+\$", "é", "😀", "[^\\d]x", "^\\S+\$", "a{2}b{1,}c{0,3}", "(?<name>ab)+", "^[\\b]", "\\x41", ".",
    "^.", "^.+", "(.*)", "^(.*)", "^.+\$", "^.{1,3}\$", "^.{2}\$", "^.{2,}\$", "^(ab|cd)\$",
    "^(?:es|ES|x-|a\\.b)\$", "^(a|b|c|d|e|f|g|h|i|j|z)\$", "^ab|cd\$", "^x-|es|ms\$", "a|b", "^a\\\$|b", "\\Bs",
    "a\\b", "^\\p{Lu}", "[\\p{L}\\d]+\$", "^\\P{L}+\$", "^(?=[^a-c\\n]+\$)(?=(.*\\w)).+\$", "^\\-a", "[{}[\\]]",
    "(.+)", "^(.+)\$", "^(.*)\$", "^a(bc)?\$", "^(a|b)c|d(e|f)\$",
    "^([a|A][u|U][t|T][o|O])|([n|N][o|O][n|N][e|E])\$",
    "^[Ee][Ss]2015(\\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?\$",
    "^[Ee][Ss]([356]|20(1[567]|2[02])|[Nn][Ee][Xx][Tt])\$", "^([a-z]+|x)-\$", "^a|[0-9]{2}\$", "^(a|b)*\$",
    "^(?=a)a|b\$", "^/.*", "^a.*", "^a\\\\.*",
    "(^([0-9]+)\\.([0-9]+)\$)|(^\\{[A-F0-9]{2}(-[A-F0-9]{1}){2}\\}\$)", "^[0-9]{1,}.[0-9]{1,}\$",
    "^3\\.1\\.\\d+(-.+)?\$", "^([A-Za-z_][-A-Za-z0-9_.:]*)\$", "^a.c\$", "^[a-z].\$", "x.y|^z", "^es|ms|x-\$",
    "^(ab){2}\$", "^(a|b){2}c\$", "^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*\$",
    "^([a-z][a-z0-9]{0,3})(\\.[a-z][a-z0-9]{0,3})*\$",
    "^([a-z_\$][a-z0-9_\$]{0,3}\\.)*([a-zA-Z_\$][a-zA-Z0-9_\$]{0,3})\$", "^([a-z]+)(,[a-z]+)+\$", "^(a,)*b\$",
    "^(ab,)+a\$", "^a(,a)*\$", "^[a-z]*(-[a-z]*)*\$", "^(a-)*a-b\$", "^(é,)*a\$",
    "^(?=!+[^!*,;{}[\\]~\\n]+\$)(?=(.*\\w)).+\$", "^(?=!+[^a]+\$)(?=(.*\\w)).+\$",
    # Valid only without the u flag (identity escapes).
    "^[\\&\\@\\_]+\$", "a\\&.b", "^[^\\%]{1,3}\$", "😀|\\&",
    # Sequences decided by the length of the string.
    "^a[a-z]{2,5}z\$", "^.*x\$", "^-?[0-9-]{0,3}0\$", "^\\/[^\\*\\?]*\\/\\*\$", "^.{2,}é\$", "^[^,]*,[^,]\$",
    "^(a.*|.+b)\$", "^(.*\\.)*[a-z]\$",
]

@testset "patterns" begin
    # Every matcher agrees with the engine on strings over an alphabet that exercises classes, anchors and
    # characters outside ASCII.
    @testset "matchers agree with the engine" begin
        alphabet = ["a", "b", "z", "A", "X", "Z", "0", "1", "5", "9", "_", "-", ".", ":", "/", "@", "#", "\$", "*",
            "!", "{", "}", "|", " ", "\n", " ", "é", "µ", "😀", " ", "x-", "es", "ES", "ms", "txt", "Au",
            "to", "No", "ne", "2015", "Co", "re", "20", "15", "22", "2", ",", "a,", "ab,", "a-", "%", "&", "?"]
        g = Xorshift(0x2545f4914f6cdd1d)
        engines = 0
        failures = String[]
        for source in TEST_PATTERNS
            reference = C.compile_engine(source)
            compiled = C.compile_pattern(source)
            if reference === nothing || compiled === nothing
                push!(failures, "$(repr(source)) does not compile")
                continue
            end
            engines += compiled.kind == C.MATCH_ENGINE
            for _ in 1:4000
                text = join(String[alphabet[next!(g, length(alphabet))+1] for _ in 1:next!(g, 9)])
                s = C.Bytes(Vector{UInt8}(text))
                got, want = C.pattern_match(compiled, s, isascii(text)), C.engine_match(reference, s)
                if got != want
                    push!(failures, "$(repr(source)) on $(repr(text)): $got, the engine says $want")
                    break
                end
            end
        end
        foreach(println, failures)
        @test isempty(failures)
        @test engines <= length(TEST_PATTERNS) ÷ 2
    end

    @testset "simple patterns take the fast matchers" begin
        kinds(patterns...) = [(p = C.compile_pattern(source); p === nothing ? 0xff : p.kind) for source in patterns]
        all_are(kind, patterns...) = all(==(kind), kinds(patterns...))
        @test all_are(C.MATCH_EVERYTHING, "", ".*", "^.*", "^[\\s\\S]*\$", "^(.*)")
        @test all_are(C.MATCH_SEQUENCE, "^[@\$_#]", "^[a-zA-Z0-9_\\-]*\$", "^#[0-9a-fA-F]{6}\$", "^[a-z][a-z0-9_]+\$",
            "^\\d{4}-\\d{2}-\\d{2}\$",
            # Decided by the length of the string.
            "^[a-z]*a\$", "^\\/[^\\*\\?]*\\/\\*\$")
        @test all_are(C.MATCH_LITERAL, "^x-", "^\\/", "^abc\$", "^\\-a")
        @test all_are(C.MATCH_HAS_CONTENT, ".+", "^.+", "(.+)")
        @test all_are(C.MATCH_LINE, "^.{1,256}\$", "^.+\$", "^.*\$", "^(.*)\$", "^(.+)\$")
        @test all_are(C.MATCH_LITERALS, "^(ab|cd)\$", "^(?:es|ES|x-)\$")
        @test all_are(C.MATCH_ALTERNATIVES, "^ab|cd\$", "a|b", "^([a|A][u|U][t|T][o|O])|([n|N][o|O][n|N][e|E])\$",
            "^[Ee][Ss]2015(\\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?\$", "^[1-5](?:[0-9]{2}|XX)\$",
            "(^([0-9]+)\\.([0-9]+)\$)|(^\\{[A-F0-9]{8}(-[A-F0-9]{4}){3}-[A-F0-9]{12}\\}\$)",
            "^([t|T][o|O][p|P])|([c|C][e|E][n|N][t|T][e|E][r|R])|([b|B][o|O][t|T][t|T][o|O][m|M])\$",
            "^([A-Za-z_][-A-Za-z0-9_.:]*)\$", "^\\/[^\\*\\?\\&\\%]*(\\/\\*)?\$", "^.*\\.(?:txt|trie)(?:\\.gz)?\$")
        @test all_are(C.MATCH_SEPARATED_LIST, "^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*\$",
            "^([a-zA-Z_\$][a-zA-Z0-9_\$]{0,39}\\.)*([a-zA-Z_\$][a-zA-Z0-9_\$]{0,39})\$",
            "^([a-z_\$][a-z0-9_\$]{0,39}\\.)*([a-zA-Z_\$][a-zA-Z0-9_\$]{0,39})\$")
        @test all_are(C.MATCH_EXCLUDED_CLASS_WITH_WORD, "^(?=[^!*,;{}[\\]~\\n]+\$)(?=(.*\\w)).+\$")
        @test all_are(C.MATCH_ENGINE, "(base64key|awskms)://(.*)", "\\bfoo", "^\\p{L}+\$",
            "^((\\.(?!\\.)\\/)?\\w+\\/?)+\$", "^\\1(a)", "a\\&.b", "^[a-z]*a[a-z]*\$")
    end

    @testset "invalid patterns are rejected" begin
        for source in ["a(", "[a", "a{2,1}", "(?<n>a)(?<n>b)", "*a", "^(?=a"]
            @test C.compile_pattern(source) === nothing
        end
    end

    # A class with a member outside ASCII, in the shape "^(?=[^SET]+$)(?=(.*\w)).+$". The shape's set holds ASCII
    # characters only, so such a member must make the shape decline before the set is built (reading it as a bit of
    # the set was a fault in the Rust crate and the Go module). The pattern has no shape and is matched by the engine.
    @testset "an excluded class with a member outside ASCII" begin
        word(c) = c == '_' || '0' <= c <= '9' || 'a' <= c <= 'z' || 'A' <= c <= 'Z'
        cases = [
            ("^(?=[^é]+\$)(?=(.*\\w)).+\$", c -> c == 'é'),
            ("^(?=[^xé]+\$)(?=(?:.*\\w)).+\$", c -> c == 'x' || c == 'é'),
            ("^(?=[^中/]+\$)(?=.*\\w).+\$", c -> c == '中' || c == '/'),
            ("^(?=[^\U0001f600]+\$)(?=(.*\\w)).+\$", c -> c == '\U0001f600'),
            # A range that starts in ASCII and ends outside it.
            ("^(?=[^m-é]+\$)(?=(.*\\w)).+\$", c -> 'm' <= c <= 'é'),
        ]
        texts = ["C1", ")a", "abc", "d-8p", "p.q", "x1", "a/b", "---", "é1", "aé", "中a", "a\U0001f600", "zè", "0",
            "ABC", "a{", "al"]
        for (pattern, excluded) in cases
            @test C.excluded_class_with_word(pattern) === nothing
            compiled = C.compile_pattern(pattern)
            @test compiled !== nothing && compiled.kind == C.MATCH_ENGINE
            v = compile_schema("{\"pattern\": " * quote_json(pattern) * "}")
            for text in texts
                want = any(word, text) && !any(excluded, text)
                @test isvalid(v, Vector{UInt8}(quote_json(text))) == want
            end
        end
    end
end
