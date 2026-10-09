# The parser and the document: round trips, rejections, duplicate keys, numbers, equality, hashing and uniqueness.
# Ported from document_test.go.

doc_equal(a::Document, b::Document) = C.values_equal(a, a.root, b, b.root)

@testset "document" begin
    @testset "parse round trips" begin
        for json in ["null", "true", "false", "0", "-0", "1.5e3", "\"\"", "\"a\"", "[]", "{}",
            "[1,[2,[3]],{\"a\":null}]", "{\"a\":1,\"b\":[true,false],\"c\":{\"d\":\"e\"}}",
            "\"café \\n \\\"q\\\" \\\\ 😀\"", "18446744073709551615", "-9223372036854775808",
            "123456789012345678901234567890"]
            d = parse_document(json)
            again = parse_document(String(d))
            @test doc_equal(d, again)
        end
        @test String(parse_document(" { \"a\" : [ 1 , 2 ] } ")) == "{\"a\":[1,2]}"
        @test String(parse_document("\"\\u0001\\t\\/\"")) == "\"\\u0001\\t/\""
    end

    @testset "parse rejects invalid JSON" begin
        invalid = Any["", " ", "{", "[", "[1,]", "{\"a\":1,}", "{\"a\"}", "{a:1}", "01", "1.", ".5", "-", "1e", "+1",
            "tru", "nul", "\"abc", "\"\\x\"", "\"\\u12\"", "\"\\ud800\"", "\"\\udc00\"", "\"\\ud800A\"", "\"a\nb\"",
            "1 2", "[1] x", "1e999", b"\"\xff\"", b"\"\xc0\x80\"", b"\"\xed\xa0\x80\"", "{\"a\":1 \"b\":2}",
            "[1 2]", "]"]
        for json in invalid
            bytes = Vector{UInt8}(json)
            @test throws(() -> parse_document(bytes), ParseError)
            @test !C.is_valid_json!(C.Parser(), bytes)
        end
        deep = repeat("[", MAX_DEPTH) * repeat("]", MAX_DEPTH)
        @test parse_document(deep) isa Document
        @test throws(() -> parse_document("[" * deep * "]"), ParseError)
        offset = try
            parse_document("[1, x]")
            -1
        catch err
            err.offset
        end
        @test offset == 4
    end

    @testset "duplicate keys keep the last value at the first position" begin
        @test String(parse_document("{\"a\":1,\"b\":2,\"a\":3,\"c\":4,\"b\":5}")) == "{\"a\":3,\"b\":5,\"c\":4}"
        text = "{" * join(["\"k$i\":$i," for i in 0:39]) * "\"k7\":\"seven\",\"k3\":\"three\"}"
        d = parse_document(text)
        @test C.count(d, d.root) == 40
        @test text_of(d, C.property(d, d.root, "k7")) == "seven"
        @test text_of(d, C.property(d, d.root, "k3")) == "three"
        @test text_of(d, C.first(d, d.root) + 2 * 7) == "k7"
    end

    number(json) = (d = parse_document(json); (C.flags(d, d.root), C.data(d, d.root)))

    @testset "numbers" begin
        for (json, flag) in [("0", C.NUM_INT), ("-1", C.NUM_INT), ("9223372036854775807", C.NUM_INT),
            ("-9223372036854775808", C.NUM_INT), ("9223372036854775808", C.NUM_UINT),
            ("18446744073709551615", C.NUM_UINT), ("18446744073709551616", C.NUM_FLOAT),
            ("-9223372036854775809", C.NUM_FLOAT), ("-0", C.NUM_FLOAT), ("1.0", C.NUM_FLOAT), ("1e2", C.NUM_FLOAT)]
            @test number(json)[1] == flag
        end
        for json in ["0.1", "1e22", "1e23", "123456789012345678.0", "0.000001", "5e-324", "1.7976931348623157e308",
            "2.2250738585072014e-308", "9007199254740993.0", "0.30000000000000004", "1e-400",
            "123456789012345678901234567890.123456789"]
            _, v = number(json)
            want = json == "1e-400" ? 0.0 : parse(Float64, json)
            @test reinterpret(Float64, v) === want
        end
        function compare(a, b)
            fa, va = number(a)
            fb, vb = number(b)
            return C.compare_numbers(fa, va, fb, vb)
        end
        for (a, b, want) in [("1", "1.0", 0), ("-1", "-0.5", -1), ("9007199254740993", "9007199254740992.0", 1),
            ("0", "-0", 0), ("18446744073709551615", "1.8446744073709552e19", -1),
            ("9223372036854775808", "9223372036854775808.0", 0), ("9223372036854775807", "9223372036854775808", -1),
            ("1e30", "18446744073709551615", 1), ("-1e30", "-9223372036854775808", -1), ("2", "1.5", 1),
            ("1", "1.5", -1)]
            @test compare(a, b) == want
            @test compare(b, a) == -want
        end
    end

    @testset "multipleOf is exact" begin
        for (x, d, want) in [("0.0075", "0.0001", true), ("0.00751", "0.0001", false), ("4.5", "1.5", true),
            ("35", "1.5", false), ("10", "5", true), ("1e308", "0.123456789", false), ("1e308", "0.5", true),
            ("0", "0.3", true), ("1e-300", "1e-7", false), ("7", "2", false), ("-8", "2", true), ("1.0", "1", true),
            ("3.0", "1.5", true), ("12391239123", "0.01", true),
            ("1.23456789012345678901234567890", "0.00000000000000000000000000001", true),
            ("1.5", "0.1234567890123456789012", false),
            ("0.2469135780246913578024", "0.1234567890123456789012", true), ("18446744073709551615", "5", true),
            ("1", "0", false), ("0.0", "0.0", false)]
            xd, dd = parse_document(x), parse_document(d)
            @test C.divides(C.Divisor(dd, dd.root), xd, xd.root) == want
        end
    end

    @testset "equality and hashing" begin
        for group in [["1", "1.0", "1e0", "10e-1"], ["{\"a\":1,\"b\":[1,2]}", "{\"b\":[1.0,2],\"a\":1}"],
            ["\"ab\"", "\"ab\""], ["[]", "[ ]"], ["0", "-0", "0.0"],
            ["18446744073709551615", "18446744073709551615"], ["9223372036854775808", "9223372036854775808.0"]]
            a = parse_document(group[1])
            for other in group[2:end]
                b = parse_document(other)
                @test doc_equal(a, b) && doc_equal(b, a)
                @test C.value_hash(a, a.root) == C.value_hash(b, b.root)
            end
        end
        distinct = ["1", "\"1\"", "[1]", "{\"a\":1}", "{\"a\":2}", "{\"b\":1}", "null", "false", "true", "1.5",
            "[1,2]", "[2,1]"]
        for (i, x) in enumerate(distinct), (j, y) in enumerate(distinct)
            @test doc_equal(parse_document(x), parse_document(y)) == (i == j)
        end
    end

    @testset "all unique" begin
        scratch = UInt64[]
        unique_items(json) = (d = parse_document(json); C.all_unique(d, d.root, scratch))
        for (json, want) in ["[]" => true, "[1]" => true, "[1,2]" => true, "[1,1.0]" => false,
            "[\"a\",\"b\",\"a\"]" => false, "[\"a\",\"b\",\"ab\"]" => true,
            "[{\"a\":1,\"b\":2},{\"b\":2,\"a\":1}]" => false, "[[1],[1,2],[2,1]]" => true,
            "[1,\"1\",true,null,[1],{\"1\":1}]" => true]
            @test unique_items(json) == want
        end
        items = String[]
        for i in 0:99
            push!(items, "{\"n\":$i,\"s\":\"$(i % 7)\"}", string(i), "\"$i\"")
        end
        @test unique_items("[" * join(items, ",") * "]")
        @test !unique_items("[" * join(items, ",") * ",{\"s\":\"3\",\"n\":3.0}]")
    end

    @testset "the string hash reads every byte" begin
        base = Vector{UInt8}("abcdefghijklmnopqrstuvwxyz")
        for n in 0:length(base)
            h = C.str_hash(C.Bytes(base[1:n]))
            for i in 1:n
                changed = base[1:n]
                changed[i] ⊻= 0x01
                @test C.str_hash(C.Bytes(changed)) != h
            end
        end
    end

    @testset "in-place sort" begin
        g = Xorshift(0x2545f4914f6cdd1d)
        for n in [0, 1, 2, 15, 16, 17, 100, 1000]
            v = UInt64[UInt64(next!(g, 50)) << 32 | UInt64(i) for i in 1:n]
            @test C.sort_words!(copy(v), 1, n) == sort(v)
        end
    end

    # The reference for the strings test: what the escapes of JSON text between quotes stand for.
    function unescape_reference(text::String)
        out = IOBuffer()
        chars = collect(text)
        i = 1
        while i <= length(chars)
            c = chars[i]
            if c != '\\'
                print(out, c)
                i += 1
                continue
            end
            e = chars[i+1]
            if e == 'u'
                unit = parse(UInt32, String(chars[i+2:i+5]); base=16)
                i += 6
                if 0xd800 <= unit <= 0xdbff
                    low = parse(UInt32, String(chars[i+2:i+5]); base=16)
                    i += 6
                    unit = 0x10000 + ((unit - 0xd800) << 10) + (low - 0xdc00)
                end
                print(out, Char(unit))
            else
                print(out, e == 'n' ? '\n' : e == 't' ? '\t' : e == 'b' ? '\b' : e == 'f' ? '\f' : e == 'r' ? '\r' : e)
                i += 2
            end
        end
        return String(take!(out))
    end

    @testset "strings agree with a reference decoder" begin
        pieces = Any["a", "b", "z", " ", "0", "/", "é", "日", "😀", "\\n", "\\t", "\\\"", "\\\\", "\\/", "\\u0041",
            "\\u00e9", "\\ud83d\\ude00", "\\b", "\\f", "\\r", "abcdefgh", "ABCDEFGHIJKLMNOP", "\u007f"]
        breakers = Any["\n", "\0", "\x1f", "\\x", "\\u12G", b"\xff", b"\xc3", "\\ud800", "\\udc00x"]
        g = Xorshift(0x2545f4914f6cdd1d)
        failures = 0
        for _ in 1:20000
            text = UInt8[UInt8('"')]
            for _ in 1:next!(g, 24)
                append!(text, Vector{UInt8}(pieces[next!(g, length(pieces))+1]))
            end
            broken = next!(g, 8) == 0
            if broken
                append!(text, Vector{UInt8}(breakers[next!(g, length(breakers))+1]))
                for _ in 1:next!(g, 12)
                    append!(text, Vector{UInt8}(pieces[next!(g, length(pieces))+1]))
                end
            end
            push!(text, UInt8('"'))
            if broken
                ok = throws(() -> parse_document(copy(text)), ParseError) && !C.is_valid_json!(C.Parser(), text)
                failures += !ok
                continue
            end
            d = parse_document(copy(text))
            want = unescape_reference(String(text[2:end-1]))
            failures += text_of(d, d.root) != want
            failures += C.str_ascii(d, d.root) != isascii(want)
        end
        @test failures == 0
    end

    @testset "numbers agree with Base" begin
        g = Xorshift(0x9e3779b97f4a7c15)
        digits(n) = String(UInt8[UInt8('0') + next!(g, 10) for _ in 1:n])
        failures = 0
        for _ in 1:50000
            io = IOBuffer()
            next!(g, 3) == 0 && print(io, '-')
            if next!(g, 6) == 0
                print(io, '0')
            else
                print(io, Char(UInt8('1') + next!(g, 9)))
                print(io, digits(next!(g, 24)))
            end
            floating = false
            if next!(g, 2) == 0
                floating = true
                print(io, '.', digits(1 + next!(g, 24)))
            end
            if next!(g, 3) == 0
                floating = true
                print(io, "eE"[next!(g, 2)+1], ["", "+", "-"][next!(g, 3)+1], next!(g, 40))
            end
            text = String(take!(io))
            d = parse_document(" " * text * " ")
            flag, value = C.flags(d, d.root), C.data(d, d.root)
            failures += String(C.number_text(d, d.root)) != text
            if !floating
                signed = tryparse(Int64, text)
                if signed !== nothing && text != "-0"
                    failures += !(flag == C.NUM_INT && (value % Int64) == signed)
                    continue
                end
                unsigned = tryparse(UInt64, text)
                if unsigned !== nothing
                    failures += !(flag == C.NUM_UINT && value == unsigned)
                    continue
                end
            end
            want = parse(Float64, text)
            failures += !(flag == C.NUM_FLOAT && value == reinterpret(UInt64, want))
        end
        @test failures == 0
    end
end
