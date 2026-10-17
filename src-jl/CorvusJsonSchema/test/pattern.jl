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
    # Lists with an anchor or an assertion inside them, and with an escaped anchor.
    raw"^x(,a$)*$", raw"^a(,a$)+$", raw"^x$(,a)*$", raw"^(a$)(,a)*$", raw"^$(,a)*$", raw"^(a,$)*b$",
    raw"^([a-z]+,$)+a$", raw"^a(^,a)*$", raw"^(^a,)*b$", raw"^a(,a\b)*$", raw"^a(,a\$)*$", raw"^a\$(,a)*$",
    raw"^(a\$,)*b$",
]

# Patterns with an anchor, a word boundary or a lookaround in a place where a matcher for a shape could lose it, and
# patterns with an escaped "$" or "^", which is a literal character. Each has the texts it matches and the texts it does
# not. The answers are V8's: new RegExp(pattern, "u").test(text).
const ANCHOR_CASES = Tuple{String,Vector{String},Vector{String}}[
    (raw"^x(,a$)*$", String["x", "x,a"], String["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^x(,a$)+$", String["x,a"], String["x", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^x(?:,a$)*$", String["x", "x,a"], String["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^x$(,a)*$", String["x"], String["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^(x$)(,a)*$", String["x"], String["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^(?:x$)(,a)+$", String[], String["x", "x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^$(,a)*$", String[""], String[",a", ",a,a", "a"]),
    (raw"^(a,$)*b$", String["b"], String["a,b", "a,a,b", "a,", "", "a,a"]),
    (raw"^(a,$)+b$", String[], String["b", "a,b", "a,a,b", "a,", "", "a,a"]),
    (raw"^(?:a,$)*b$", String["b"], String["a,b", "a,a,b", "a,", "", "a,a"]),
    (raw"^([a-z]+)(\.[a-z]+$)*$", String["a", "a.b"], String["a.b.c", "a.", ""]),
    (raw"^([a-z]+\.$)*[a-z]+$", String["a"], String["a.b", "a.b.c", ".a", ""]),
    (raw"^x(,a$$)*$", String["x", "x,a"], String["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^x(^,a)*$", String["x"], String["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^x(,^a)*$", String["x"], String["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^^x(,a)*$", String["x", "x,a", "x,a,a", "x,a,a,a"], String["", "x,", "x,b"]),
    (raw"^(^x)(,a)*$", String["x", "x,a", "x,a,a", "x,a,a,a"], String["", "x,", "x,b"]),
    (raw"^(^a,)*b$", String["b", "a,b"], String["a,a,b", "a,", "", "a,a"]),
    (raw"^(a,)*^b$", String["b"], String["a,b", "a,a,b", "a,", "", "a,a"]),
    (raw"^x(,(a$))*$", String["x", "x,a"], String["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^x(,(?:a$))*$", String["x", "x,a"], String["x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^x(,a|$)*$", String["x", "x,a", "x,a,a", "x,a,a,a"], String["", "x,", "x,b"]),
    (raw"^x(,(a|$))*$", String["x", "x,a", "x,a,a", "x,a,a,a", "x,"], String["", "x,b"]),
    (raw"^x((^|,)a)*$", String["x", "x,a", "x,a,a", "x,a,a,a"], String["", "x,", "x,b"]),
    (raw"^(a,)*(b$)$", String["b", "a,b", "a,a,b"], String["a,", "", "a,a"]),
    (raw"^(a,)*b$$", String["b", "a,b", "a,a,b"], String["a,", "", "a,a"]),
    (raw"^x(,a\b)*$", String["x", "x,a", "x,a,a", "x,a,a,a"], String["", "x,", "x,b", "x,ab"]),
    (raw"^x(\b,a)*$", String["x", "x,a", "x,a,a", "x,a,a,a"], String["", "x,", "x,b"]),
    (raw"^x(,\Ba)*$", String["x"], String["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^x(\B,a)*$", String["x"], String["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^x\b(,a)*$", String["x", "x,a", "x,a,a", "x,a,a,a"], String["", "x,", "x,b"]),
    (raw"^x\B(,a)*$", String[], String["x", "x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^(a\b,)*b$", String["b", "a,b", "a,a,b"], String["a,", "", "a,a"]),
    (raw"^(a,\B)*b$", String["b"], String["a,b", "a,a,b", "a,", "", "a,a"]),
    (raw"^x(,a(?=,|$))*$", String["x", "x,a", "x,a,a", "x,a,a,a"], String["", "x,", "x,b"]),
    (raw"^x(,(?!a)a)*$", String["x"], String["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^x(?=,)(,a)*$", String["x,a", "x,a,a", "x,a,a,a"], String["x", "", "x,", "x,b"]),
    (raw"^x(,a(?<=a))*$", String["x", "x,a", "x,a,a", "x,a,a,a"], String["", "x,", "x,b"]),
    (raw"^x(,(?<!,)a)*$", String["x"], String["x,a", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^x(?=,a$)(,a)*$", String["x,a"], String["x", "x,a,a", "x,a,a,a", "", "x,", "x,b"]),
    (raw"^(a,(?=b))*b$", String["b", "a,b"], String["a,a,b", "a,", "", "a,a"]),
    (raw"^(?!a,a)(a,)*b$", String["b", "a,b"], String["a,a,b", "a,", "", "a,a"]),
    (raw"^x(,a\$)*$", String["x", "x,a\$", "x,a\$,a\$"], String["x,a", "x,a,a", "x,a\$,a"]),
    (raw"^x\$(,a)*$", String["x\$", "x\$,a", "x\$,a,a"], String["x", "x,a"]),
    (raw"^(a\$,)*b$", String["b", "a\$,b", "a\$,a\$,b"], String["a,b", "a\$,a,b"]),
    (raw"^x(,\^a)*$", String["x", "x,^a", "x,^a,^a"], String["x,a", "x,a,a"]),
    (raw"^(a,)*b\$$", String["b\$", "a,b\$", "a,a,b\$"], String["b", "a,b"]),
    (raw"^x(,[$^]a)*$", String["x", "x,\$a", "x,^a,\$a"], String["x,a", "x,a,a"]),
    (raw"^a$b", String[], String["a", "ab", "a\$b", ""]),
    (raw"^a^b", String[], String["a", "ab", "b"]),
    (raw"^a$$", String["a"], String["ab", ""]),
    (raw"^^a", String["a", "ab"], String["ba"]),
    (raw"^a\$b$", String["a\$b"], String["ab", "a"]),
    (raw"^a\b", String["a", "a b", "a,"], String["ab"]),
    (raw"^a\Bb", String["ab"], String["a b", "a"]),
    (raw"^\ba", String["a"], String["ba", ""]),
    (raw"^a(?=b)", String["ab"], String["a", "ac"]),
    (raw"^a(?!b)", String["a", "ac"], String["ab"]),
    (raw"^[a-c]+\b$", String["a", "abc"], String[""]),
    (raw"^[a-c]+$\b", String["a", "abc"], String[""]),
    (raw"^$", String[""], String["a"]),
    (raw"^a$.*", String["a"], String["ab", ""]),
    (raw"^a\b.*", String["a", "a b"], String["ab"]),
    (raw"^a^.*", String[], String["a", "ab"]),
    (raw"^\$.*", String["\$", "\$a"], String["a"]),
    (raw"^a$|b", String["a", "ab", "b", "cb"], String["c"]),
    (raw"a$|^b", String["a", "ba", "bc"], String["ab", "cb"]),
    (raw"^(a$|b)$", String["a", "b"], String["ab", ""]),
    (raw"^(a|^b)$", String["a", "b"], String["ab"]),
    (raw"^(a|b\b)$", String["a", "b"], String["ab"]),
    (raw"^(?:a\$|b)$", String["a\$", "b"], String["a"]),
    (raw"^(a|$)$", String["a", ""], String["b"]),
    (raw"^(^|b)$", String["", "b"], String["a"]),
    (raw"^(a$|b)c", String["bc", "bcd"], String["a", "ac"]),
    (raw"(^a|b)c$", String["ac", "bc", "xbc"], String["xac", "acx"]),
    (raw"a(b|^c)", String["ab", "xab"], String["ac", "c"]),
    (raw"(a$){2}", String[], String["a", "aa", ""]),
    (raw"(^a){2}", String[], String["a", "aa"]),
    (raw"x(a$)?b", String["xb"], String["xab", "xa"]),
    (raw"^x(a$)?", String["x", "xa", "xab", "xb"], String[]),
    (raw"(a|b$)c", String["ac"], String["bc", "b"]),
    (raw"a|\bb", String["a", "b", "c b"], String["cb"]),
    (raw"a\b|c", String["a", "c", "b a"], String["ab"]),
    (raw"(?=a)a|b", String["a", "b"], String["c"]),
    (raw"(?<=a)b|c", String["ab", "c"], String["b"]),
    (raw"a^|b", String["b"], String["a", ""]),
    (raw"$a|b", String["b"], String["a", ""]),
    (raw"^|a", String["", "b"], String[]),
    (raw"a|$", String["", "b"], String[]),
    (raw"(a|b)$c", String[], String["ac", "a", "bc"]),
    (raw"^(a|b)^c", String[], String["ac", "c"]),
    (raw"[a-c]{2}$|^x", String["ab", "x", "yab"], String["abx", "yx"]),
    (raw"[a-c]\b|x", String["a", "x", "d a"], String["ad"]),
    (raw"^[a-c]+$|^\d$", String["abc", "1"], String["a1", ""]),
    (raw"^(a|b){2}$", String["ab"], String["a", "abc"]),
    (raw"^.{1,3}$$", String["a"], String["abcd", ""]),
    (raw"^.$^", String[], String["a", ""]),
    (raw"^(.*)$", String["", "a"], String[]),
    (raw"^es$|ms", String["es", "xms"], String["esx", "x"]),
    (raw"^x-$", String["x-"], String["x-a"]),
    (raw"^x-\b", String["x-a"], String["x-", "x--"]),
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
                got, want = C.pattern_match(compiled, s, isascii(text), C.PatternMatchLimit()), C.engine_match(reference, s, C.PatternMatchLimit())
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
            "^([a-z_\$][a-z0-9_\$]{0,39}\\.)*([a-zA-Z_\$][a-zA-Z0-9_\$]{0,39})\$",
            # An escaped "$" or "^" is a literal character of the list.
            raw"^x(,a\$)*$", raw"^x\$(,a)*$", raw"^(a\$,)*b$", raw"^x(,\^a)*$", raw"^(a,)*b\$$", raw"^x(,[$^]a)*$")
        @test all_are(C.MATCH_EXCLUDED_CLASS_WITH_WORD, "^(?=[^!*,;{}[\\]~\\n]+\$)(?=(.*\\w)).+\$")
        @test all_are(C.MATCH_ENGINE, "(base64key|awskms)://(.*)", "\\bfoo", "^\\p{L}+\$",
            "^((\\.(?!\\.)\\/)?\\w+\\/?)+\$", "^\\1(a)", "a\\&.b", "^[a-z]*a[a-z]*\$",
            # A list with an anchor or an assertion inside it.
            raw"^x(,a$)*$", raw"^x(?:,a$)+$", raw"^x$(,a)*$", raw"^(x$)(,a)*$", raw"^$(,a)*$", raw"^(a,$)*b$",
            raw"^x(^,a)*$", raw"^(^a,)*b$", raw"^x(,(a$))*$", raw"^x(,a\b)*$", raw"^x\B(,a)*$", raw"^x(,a(?=,|$))*$",
            raw"^x(?=,)(,a)*$")
    end

    # A shape that cannot express an anchor or an assertion leaves the pattern to the engine. The separated list
    # dropped a "$" at the end of its repeated group and after its first item, so {"pattern": "^x(,a$)*$"} took "x,a,a",
    # which has a "$" before its second ",a" (a fault in the Rust crate and the Go module, and in this port of them).
    @testset "an anchor or an assertion inside a shape is not dropped" begin
        failures = String[]
        for (pattern, matches, fails) in ANCHOR_CASES
            v = compile_schema("{\"pattern\": " * quote_json(pattern) * "}")
            for (texts, want) in ((matches, true), (fails, false)), text in texts
                isvalid(v, Vector{UInt8}(quote_json(text))) == want ||
                    push!(failures, "$pattern on $(repr(text)): want $want")
            end
        end
        foreach(println, failures)
        @test isempty(failures)
        v = compile_schema("{\"patternProperties\": {" * quote_json(raw"^x(,a$)*$") * ": false}}")
        @test !isvalid(v, "{\"x,a\": 1}")
        @test isvalid(v, "{\"x,a,a\": 1}")
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

    # \k in a class is not an escape in a pattern with a named group, in either grammar, so the schema does not
    # compile. In a pattern with no named group it is the letter k.
    @testset "\\k in a class, in a pattern with a named group" begin
        for pattern in [raw"(?<n>a)[\k]", raw"[\k](?<n>a)", raw"(?<a>x)[\k<a>]"]
            @test !C.valid_regex(pattern)
            @test_throws CompileError compile_schema("{\"pattern\": " * quote_json(pattern) * "}")
            @test_throws CompileError compile_schema("{\"patternProperties\": {" * quote_json(pattern) * ": true}}")
        end
        v = compile_schema("{\"pattern\": " * quote_json(raw"^(a)[\k]$") * "}")
        @test isvalid(v, "\"ak\"")
        @test !isvalid(v, "\"aa\"")
    end

    # A count whose first number is the greater is an error, also where both are past what a count is kept as.
    @testset "the numbers of a count out of order" begin
        for pattern in [raw"a{2147483648,2147483647}", raw"^a{2,1}$", raw"x{4294967296,4294967295}"]
            @test !C.valid_regex(pattern)
            @test_throws CompileError compile_schema("{\"pattern\": " * quote_json(pattern) * "}")
        end
        @test C.valid_regex(raw"a{2147483647,2147483648}")
    end

    # The script that no character has is a name ECMA-262 has.
    @testset "the script Katakana_Or_Hiragana" begin
        @test C.valid_regex(raw"\p{sc=Hrkt}")
        v = compile_schema("{\"pattern\": " * quote_json(raw"^\P{Script_Extensions=Katakana_Or_Hiragana}$") * "}")
        @test isvalid(v, "\"\u30ab\"")
        @test !isvalid(v, "\"\u30ab\u3042\"")
        @test !isvalid(compile_schema("{\"pattern\": " * quote_json(raw"\p{sc=Hrkt}") * "}"), "\"\u30ab\"")
    end

    # A count no text reaches is a pattern like another.
    @testset "a count of a thousand million" begin
        v = compile_schema("{\"pattern\": " * quote_json(raw"\p{1000000000}") * "}")
        @test !isvalid(v, "\"ppp\"")
        @test !isvalid(v, "\"p{1000000000}\"")
        v = compile_schema("{\"pattern\": " * quote_json(raw"^a{0,1000000000}$") * "}")
        @test isvalid(v, "\"aaa\"")
        @test !isvalid(v, "\"aab\"")
    end

    # A pattern that is valid only without the u flag (one with an identity escape such as \- or \&) matches UTF-16
    # code units, as ECMA-262 says, so a character beyond the Basic Multilingual Plane is two characters to it. The
    # faster matchers go by character. They decide the strings within the Basic Multilingual Plane, where the two are
    # the same, and the engine decides the others. Each pattern is with the texts V8 13.6 matches, of the eleven.
    @testset "a pattern valid only without the u flag matches code units" begin
        texts = ["", "a", "ab", "\U10400", "a\U10400", "\U10400\U10400", "\U1F600", "-a", "_", "&\U10400",
            "\u00e9\u00e9"]
        cases = [
            ("^[^\\_]{2}\$", ["ab", "\U10400", "\U1F600", "-a", "\u00e9\u00e9"]),
            ("^[\\&\\@\\_]+\$", ["_"]),
            ("^[^\\%]{1,3}\$", ["a", "ab", "\U10400", "a\U10400", "\U1F600", "-a", "_", "&\U10400", "\u00e9\u00e9"]),
            ("^\\-?.{1,2}\$", ["a", "ab", "\U10400", "\U1F600", "-a", "_", "\u00e9\u00e9"]),
            ("^\\-a", ["-a"]),
            ("^.\\&?\$", ["a", "_"]),
            ("\\&|^.{2}\$", ["ab", "\U10400", "\U1F600", "-a", "&\U10400", "\u00e9\u00e9"]),
            ("^\\-?\U10400*\$", ["\U10400"]),
        ]
        fast = 0
        for (pattern, matched) in cases
            @test !C.valid_regex(pattern)
            compiled = C.compile_pattern(pattern)
            @test compiled !== nothing
            fast += compiled.kind != C.MATCH_ENGINE
            v = compile_schema("{\"pattern\": " * quote_json(pattern) * "}")
            for text in texts
                @test C.match_string(compiled, text, C.PatternMatchLimit()) == (text in matched)
                @test isvalid(v, Vector{UInt8}(quote_json(text))) == (text in matched)
            end
        end
        # Some of them have a faster matcher, which is what the strings without such a character go to.
        @test fast >= 3
        # A pattern with such a character in it has none: a quantifier after the character repeats its second half.
        @test C.compile_pattern("^\\-?\U10400*\$").kind == C.MATCH_ENGINE
        # A pattern that is valid with the u flag matches characters.
        @test isvalid(compile_schema("{\"pattern\": \"^[^_]{2}\$\"}"), Vector{UInt8}("\"a\U10400\""))
        @test !isvalid(compile_schema("{\"pattern\": \"^[^_]{2}\$\"}"), Vector{UInt8}("\"\U10400\""))
        # A class range between two such characters is an error without the flag, and the pattern is valid with it.
        @test C.compile_pattern("\\-{0}^[\U10400-\U10428]\$") === nothing
        @test C.compile_pattern("^[\U10400-\U10428]\$") !== nothing
    end

    # A long pattern compiles in time and memory proportional to its length. The matcher that multiplies out the
    # alternatives of a pattern added each character it copied to every branch, one character at a time, which
    # copies the branch for each: a literal pattern of 140,000 characters took 1.2 seconds to compile and one of a
    # megabyte two minutes. The characters are added a run at a time. The test counts the bytes allocated, which
    # no load on the machine changes, through every matcher a literal can take.
    @testset "a long pattern compiles in memory proportional to its length" begin
        round = 0
        function allocated(n, shape)
            # A pattern is compiled once for the process, so each must be a new one.
            round += 1
            pattern = shape("a"^(n - 8) * lpad(round, 8, '0'))
            bytes = @allocated compiled = C.compile_pattern(pattern)
            @test compiled !== nothing
            return bytes
        end
        shapes = [
            ("a literal", s -> s),
            ("anchored", s -> "^" * s * "\$"),
            ("alternatives", s -> "^(?:" * s[1:end-200] * "|x" * s[end-100:end] * ")\$"),
            ("with an escape", s -> s[1:end÷2] * "\\." * s[end÷2+3:end]),
            ("with a class", s -> s[1:end÷2] * "[ab]" * s[end÷2:end]),
            ("in a group", s -> "(" * s * ")"),
        ]
        for (name, shape) in shapes
            allocated(1000, shape)
            short, long = allocated(1000, shape), allocated(16_000, shape)
            # Sixteen times the length. Growth with its square is 256 times.
            long > 64 * short && println("[patterns] ", name, ": 16,000 characters allocate ", long >> 10,
                " KB and 1,000 allocate ", short >> 10, " KB")
            @test long <= 64 * short
        end
    end
end
