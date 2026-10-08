# A minimal JSON reader for the test data. Objects are Dict{String,Any}, arrays are Vector{Any}, numbers are Float64.
# A surrogate pair of \u escapes is one character, and a lone surrogate is kept as Julia writes one.

mutable struct JsonReader
    data::Vector{UInt8}
    at::Int
end

readjson(path::AbstractString) = parsejson(read(path))

function parsejson(data::Vector{UInt8})
    reader = JsonReader(data, 1)
    value = jsonvalue(reader)
    skipspace(reader)
    reader.at > length(data) || error("text after the JSON value at byte $(reader.at)")
    return value
end

function skipspace(r::JsonReader)
    while r.at <= length(r.data) && r.data[r.at] in (0x20, 0x09, 0x0a, 0x0d)
        r.at += 1
    end
    return nothing
end

function expect(r::JsonReader, word::String)
    for b in codeunits(word)
        (r.at <= length(r.data) && r.data[r.at] == b) || error("expected $word at byte $(r.at)")
        r.at += 1
    end
    return nothing
end

function jsonvalue(r::JsonReader)
    skipspace(r)
    r.at <= length(r.data) || error("the JSON text ends early")
    b = r.data[r.at]
    if b == UInt8('{')
        r.at += 1
        object = Dict{String,Any}()
        skipspace(r)
        if r.data[r.at] == UInt8('}')
            r.at += 1
            return object
        end
        while true
            skipspace(r)
            name = jsonstring(r)
            skipspace(r)
            expect(r, ":")
            object[name] = jsonvalue(r)
            skipspace(r)
            if r.data[r.at] == UInt8(',')
                r.at += 1
            else
                expect(r, "}")
                return object
            end
        end
    elseif b == UInt8('[')
        r.at += 1
        array = Any[]
        skipspace(r)
        if r.data[r.at] == UInt8(']')
            r.at += 1
            return array
        end
        while true
            push!(array, jsonvalue(r))
            skipspace(r)
            if r.data[r.at] == UInt8(',')
                r.at += 1
            else
                expect(r, "]")
                return array
            end
        end
    elseif b == UInt8('"')
        return jsonstring(r)
    elseif b == UInt8('t')
        expect(r, "true")
        return true
    elseif b == UInt8('f')
        expect(r, "false")
        return false
    elseif b == UInt8('n')
        expect(r, "null")
        return nothing
    end
    start = r.at
    while r.at <= length(r.data) && (r.data[r.at] in UInt8('0'):UInt8('9') || r.data[r.at] in codeunits("+-.eE"))
        r.at += 1
    end
    return parse(Float64, String(r.data[start:(r.at - 1)]))
end

function hex4(r::JsonReader)
    value = parse(UInt32, String(r.data[r.at:(r.at + 3)]); base=16)
    r.at += 4
    return value
end

function jsonstring(r::JsonReader)
    expect(r, "\"")
    out = IOBuffer()
    while true
        b = r.data[r.at]
        r.at += 1
        if b == UInt8('"')
            return String(take!(out))
        elseif b != UInt8('\\')
            write(out, b)
            continue
        end
        e = r.data[r.at]
        r.at += 1
        if e == UInt8('u')
            u = hex4(r)
            if 0xD800 <= u <= 0xDBFF && r.at + 5 <= length(r.data) && r.data[r.at] == UInt8('\\') &&
               r.data[r.at + 1] == UInt8('u')
                save = r.at
                r.at += 2
                low = hex4(r)
                if 0xDC00 <= low <= 0xDFFF
                    u = 0x10000 + ((u - 0xD800) << 10) + (low - 0xDC00)
                else
                    r.at = save
                end
            end
            print(out, Char(u))
        else
            write(out, e == UInt8('n') ? 0x0a : e == UInt8('t') ? 0x09 : e == UInt8('r') ? 0x0d :
                       e == UInt8('b') ? 0x08 : e == UInt8('f') ? 0x0c : e)
        end
    end
end
