# Helpers shared by the tests. Test data is read with the package's own parser, since the package has no dependency
# on a JSON library.

const C = CorvusJsonSchema

# The value of a property of an object value, or -1.
member(d::Document, n::Int, name::String) = C.member(d, n, name)

# The text of a string value.
text_of(d::Document, n::Int) = String(C.str(d, n))

# A value as JSON text.
json_of(d::Document, n::Int) = String(C.append_json!(UInt8[], d, n))

# The items of an array value.
items_of(d::Document, n::Int) = [C.first(d, n) + i for i in 0:C.count(d, n)-1]

# The names and values of an object value.
members_of(d::Document, n::Int) =
    [(text_of(d, C.first(d, n) + 2i), C.first(d, n) + 2i + 1) for i in 0:C.count(d, n)-1]

is_true(d::Document, n::Int) = C.kind(d, n) == C.KIND_BOOL && C.boolean(d, n)

const REPOSITORY_ROOT = normpath(joinpath(@__DIR__, "..", "..", ".."))

# The JSON-Schema-Test-Suite checkout: the repository's submodule, or JSON_SCHEMA_TEST_SUITE.
suite_root() = get(ENV, "JSON_SCHEMA_TEST_SUITE", joinpath(REPOSITORY_ROOT, "JSON-Schema-Test-Suite"))

json_files(dir::String) = isdir(dir) ? sort!([joinpath(dir, f) for f in readdir(dir) if endswith(f, ".json") &&
                                              isfile(joinpath(dir, f))]) : String[]
