# The type algebra, the name tables and the property lookup of the plans. Ported from plan_test.go.

name_bytes(name::String) = C.Bytes(Vector{UInt8}(name))

@testset "plans" begin
    @testset "meet treats integers as numbers" begin
        for (a, b, want) in [(C.TYPE_INTEGER, C.TYPE_NUMBER, C.TYPE_INTEGER),
            (C.TYPE_NUMBER, C.TYPE_NUMBER | C.TYPE_STRING, C.TYPE_NUMBER | C.TYPE_INTEGER),
            (C.ANY_TYPE, C.TYPE_STRING | C.TYPE_ARRAY, C.TYPE_STRING | C.TYPE_ARRAY),
            (C.TYPE_STRING, C.TYPE_INTEGER, 0x00)]
            @test C.meet_types(a, b) == want
        end
        for json in ["1", "1.5", "\"a\""]
            d = parse_document(json)
            for (a, b) in [(C.TYPE_INTEGER, C.TYPE_NUMBER), (C.TYPE_NUMBER, C.TYPE_INTEGER),
                (C.TYPE_NUMBER | C.TYPE_STRING, C.TYPE_INTEGER | C.TYPE_STRING), (C.ANY_TYPE, C.TYPE_NUMBER)]
                @test C.type_ok(C.meet_types(a, b), d, d.root) == (C.type_ok(a, d, d.root) && C.type_ok(b, d, d.root))
            end
        end
    end

    @testset "the name table finds every name" begin
        list = String["p$i" * repeat("x", i % 13) for i in 0:199]
        append!(list, ["", "a", "ab", "abc", "abcd", "abcdefgh", "abcdefghi", "é", "日本", repeat("y", 300),
            "abcdefghijkl", "abcdefghijkm", "abcdefghXjkl",
            # Longer than sixteen bytes with the same first and last eight: only the text tells them apart.
            "aaaaaaaa-1-bbbbbbbb", "aaaaaaaa-2-bbbbbbbb", "aaaaaaaa-3-bbbbbbbb"])
        ns = C.Names(list)
        @test all(C.find(ns, name_bytes(n)) == i - 1 for (i, n) in enumerate(list))
        for miss in ["p", "q1", "p1000", "p0x", "b", "abce", "abcdefgj", "abcdefghj", "è", "y", repeat("y", 299),
            "abcdefghijkn", "abcdefghXjkm", "aaaaaaaa-4-bbbbbbbb", repeat("y", 150) * "z" * repeat("y", 149)]
            @test C.find(ns, name_bytes(miss)) == -1
        end
        for few in [String[], ["a"], ["ab", "ba"], ["alpha", "gamma", "delta", "omega"],
            ["abcdefghij", "abcdefghik"], ["twice", "twice", "once"]]
            small = C.Names(few)
            for n in few
                @test C.find(small, name_bytes(n)) == findfirst(==(n), few) - 1
            end
            @test C.find(small, name_bytes("zeta!")) == -1
        end
    end

    @testset "names follow declared or sorted order" begin
        declared = ["name", "version", "repository", "alias"]
        ns = C.Names(declared)
        # Every order of the names, from every starting hint, finds each one.
        for order in [["name", "version", "repository", "alias"], ["alias", "name", "repository", "version"],
            ["version", "alias", "name", "repository"], ["repository", "repository", "name"]]
            for start in 0:length(declared)
                hint = start
                for n in order
                    got, hint = C.find_next(ns, name_bytes(n), hint)
                    @test got == findfirst(==(n), declared) - 1
                end
            end
        end
        @test C.find_next(ns, name_bytes("other"), 0)[1] == -1
        @test C.find_next(ns, name_bytes("names"), 2)[1] == -1
        # The hint follows names found in the declared order, with or without gaps, and is given up for the rest of
        # an object at a name found before the one expected.
        @test C.find_next(ns, name_bytes("name"), 0) == (0, 1)
        @test C.find_next(ns, name_bytes("repository"), 1) == (2, 3)
        @test C.find_next(ns, name_bytes("version"), 3) == (1, -1)
        @test C.find_next(ns, name_bytes("alias"), -1) == (3, -1)
        @test C.find_next(ns, name_bytes("other"), 2) == (-1, 2)
        many = ["property-number-" * lpad((i * 7) % 40, 2, '0') for i in 0:39]
        large = C.Names(many)
        for start in 0:13:length(many)
            @test all(C.find_next(large, name_bytes(n), start)[1] == i - 1 for (i, n) in enumerate(many))
            @test C.find_next(large, name_bytes("property-number-40"), start)[1] == -1
        end
    end

    # A name inside a document is read a word at a time with the text after it masked away, and a name at the end of
    # its vector byte by byte. Both must give the key the set was built with, for every length.
    @testset "a name is found wherever it lies in its vector" begin
        alphabet = repeat("abcdefghijklmnopqrstuvwxyz", 2)
        list = [alphabet[1:n] for n in 0:40]
        others = [n[1:end-1] * "#" for n in list if !isempty(n)]
        ns = C.Names(list)
        for (i, n) in enumerate(vcat(list, others)), before in (0, 1, 7, 9), after in (0, 1, 5, 7, 8, 17)
            b = vcat(fill(UInt8('x'), before), Vector{UInt8}(n), fill(UInt8('y'), after))
            name = C.Bytes(b, before, ncodeunits(n))
            expected = i <= length(list) ? i - 1 : -1
            @test C.find(ns, name) == expected
            @test all(C.find_next(ns, name, hint)[1] == expected for hint in (-1, 0, max(i - 1, 0), length(list)))
            @test C.name_word(name) == C.name_word(C.Bytes(Vector{UInt8}(n)))
        end
        @test_throws BoundsError C.name_word(C.Bytes(UInt8[1, 2, 3], -1, 2))
        @test_throws BoundsError C.name_word(C.Bytes(UInt8[1, 2, 3], 2, 2))
        @test_throws BoundsError C.le64(collect(0x01:0x10), 9)
        @test_throws BoundsError C.le64(collect(0x01:0x10), -1)
        @test_throws BoundsError C.le64(collect(0x01:0x10), typemin(Int))
        @test C.le64(collect(0x01:0x10), 8) == 0x100f0e0d0c0b0a09
    end

    @testset "linear names find from any hint" begin
        ns = C.Names(["a", "b", "c"])
        for start in 0:2
            for (i, n) in enumerate(["a", "b", "c"])
                @test C.find_next(ns, name_bytes(n), start)[1] == i - 1
            end
            @test C.find_next(ns, name_bytes("d"), start)[1] == -1
        end
    end

    # A plan of a few names looks each one up in a small object (visit_lookup): every name is found by its length
    # and word, the text deciding beyond eight bytes, and a name that is not there is not.
    @testset "property lookup by name" begin
        instance = "{\"a\": 1, \"ab\": 2, \"abcdefgh\": 3, \"abcdefghi\": 4, \"abcdefghj\": 5, \"é\": 6, \"\": 7, " *
                   "\"a\\nb\": 8}"
        d = parse_document(instance)
        for (name, want) in ["a" => "1", "ab" => "2", "abcdefgh" => "3", "abcdefghi" => "4", "abcdefghj" => "5",
            "é" => "6", "" => "7", "a\nb" => "8"]
            v = C.property(d, d.root, name)
            @test v >= 0 && String(C.number_text(d, v)) == want
            for (value, valid) in [want => true, "0" => false]
                q = quote_json(name)
                validator = compile_schema("{\"properties\": {$q: {\"const\": $value}}, \"required\": [$q]}")
                p = validator.program
                object = C.plan(p, p.entry).body.object
                @test object !== nothing && object.lookup
                @test isvalid(validator, d) == valid
            end
        end
        for miss in ["b", "ba", "abcdefgi", "abcdefghk", "abcdefgh ", "è"]
            @test C.property(d, d.root, miss) == -1
            q = quote_json(miss)
            # Found, the property would fail its schema. Required, it is missed.
            @test isvalid(compile_schema("{\"properties\": {$q: false}}"), d)
            @test !isvalid(compile_schema("{\"properties\": {$q: true}, \"required\": [$q]}"), d)
        end
    end

    @testset "length_ok agrees with counting" begin
        for s in ["", "a", "abcd", "é", "éé", "😀😀", "a😀b"]
            d = parse_document("\"" * s * "\"")
            chars = length(s)
            for lo in 0:5, hi in 0:5
                @test C.length_ok(d, d.root, UInt64(lo), UInt64(hi)) == (lo <= chars <= hi)
            end
        end
    end
end
