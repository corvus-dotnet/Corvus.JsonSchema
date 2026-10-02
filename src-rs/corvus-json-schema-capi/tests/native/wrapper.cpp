// The C++ wrapper: ownership (copies, moves), options with std::function callbacks (including ones that throw),
// exceptions and try_validate, documents, collectors, and threads sharing validators.

#include <atomic>
#include <cstdio>
#include <memory>
#include <string>
#include <thread>
#include <utility>
#include <vector>

#include "corvus_json_schema.hpp"

namespace cjs = corvus::json_schema;

static int failures = 0;

#define CHECK(condition)                                                                        \
    do {                                                                                        \
        if (!(condition)) {                                                                     \
            std::fprintf(stderr, "%s:%d: check failed: %s\n", __FILE__, __LINE__, #condition); \
            failures++;                                                                         \
        }                                                                                       \
    } while (0)

template <class F>
static bool throws(cjs_status status, F&& f, std::string_view text = {}) {
    try {
        f();
    } catch (const cjs::error& e) {
        return e.status() == status && std::string_view(e.what()).find(text) != std::string_view::npos;
    }
    return false;
}

int main() {
    // Compile, validate, copy and move.
    auto v = cjs::validator::compile(R"({"type": "array", "items": {"type": "integer"}})");
    CHECK(v.is_valid("[1, 2, 3]"));
    CHECK(!v.is_valid(R"([1, "2"])"));
    cjs::validator copy = v;
    cjs::validator moved = std::move(v);
    CHECK(copy.is_valid("[4]") && moved.is_valid("[5]"));
    CHECK(copy.native_handle() != moved.native_handle());
    copy = moved;
    CHECK(copy.is_valid("[]"));

    // Errors as exceptions, and without them.
    try {
        moved.is_valid("[1, 2");
        CHECK(false);
    } catch (const cjs::error& e) {
        CHECK(e.status() == CJS_INVALID_JSON && e.offset() == 5);
    }
    bool valid = true;
    CHECK(moved.try_validate("[1, \"x\"]", valid) == CJS_OK && !valid);
    CHECK(moved.try_validate("{", valid) == CJS_INVALID_JSON);
    CHECK(throws(CJS_COMPILATION_FAILED, [] { cjs::validator::compile(R"({"$ref": "http://example.com/x.json"})"); },
                 "x.json"));
    CHECK(throws(CJS_INVALID_ARGUMENT, [] { cjs::options().default_dialect(static_cast<cjs::dialect>(99)); }));

    // Options: a format (one that throws rejects), and a resolver (one that throws fails the compilation).
    auto calls = std::make_shared<std::atomic<int>>(0);
    cjs::options options;
    options.assert_format(true)
        .format("even",
                [calls](std::string_view s) {
                    ++*calls;
                    return s.size() % 2 == 0;
                })
        .format("never", [](std::string_view) -> bool { throw std::runtime_error("no"); })
        .resolver([](std::string_view uri) -> std::optional<std::string> {
            if (uri == "http://example.com/positive.json") {
                return R"({"type": "integer", "minimum": 1})";
            }
            return std::nullopt;
        });
    auto f = cjs::validator::compile(
        R"({"properties": {"code": {"format": "even"}, "other": {"format": "never"},
            "count": {"$ref": "http://example.com/positive.json"}}})",
        options);
    CHECK(f.is_valid(R"({"code": "ab", "count": 2})"));
    CHECK(!f.is_valid(R"({"code": "abc"})"));
    CHECK(!f.is_valid(R"({"other": "x"})"));
    CHECK(!f.is_valid(R"({"count": 0})"));
    CHECK(calls->load() == 2);

    cjs::options failing;
    failing.resolver([](std::string_view) -> std::optional<std::string> { throw std::runtime_error("offline"); });
    CHECK(throws(CJS_COMPILATION_FAILED,
                 [&] { cjs::validator::compile(R"({"$ref": "http://example.com/y.json"})", failing); }, "offline"));

    // Documents and collectors.
    auto named = cjs::validator::compile(R"({"properties": {"name": {"type": "string", "title": "Name"}}})");
    auto bad = cjs::document::parse(R"({"name": 1})");
    CHECK(!named.is_valid(bad));
    cjs::collector results(cjs::results_level::detailed);
    CHECK(!named.evaluate(bad, results));
    bool found = false;
    for (std::size_t i = 0; i < results.size(); i++) {
        cjs::result_row row = results[i];
        found |= !row.is_match && row.instance_location == "/name" && !row.message.empty();
    }
    CHECK(found);
    std::string text = R"({"name": "a"})";
    auto good = cjs::document::parse_borrowed(text);
    cjs::collector verbose(cjs::results_level::verbose);
    CHECK(named.evaluate(good, verbose));
    CHECK(verbose.annotations_json().find(R"("#/properties/name":"Name")") != std::string_view::npos);
    CHECK(named.evaluate(R"({"name": "b"})", verbose) && verbose.size() > 0);
    verbose.clear();
    CHECK(verbose.size() == 0);

    // Threads, each with its own copy of a validator (and one shared document).
    auto shared = cjs::validator::compile(R"({"type": "array", "items": {"type": "integer", "minimum": 0}})");
    auto doc = cjs::document::parse("[1, 2, 3]");
    std::atomic<int> wrong{0};
    std::vector<std::thread> threads;
    for (int t = 0; t < 8; t++) {
        threads.emplace_back([&wrong, &doc, local = shared, t] {
            for (int i = 0; i < 1000; i++) {
                std::string json = "[" + std::to_string(t) + "," + std::to_string(i % 7 == 0 ? -1 : i) + "]";
                if (local.is_valid(json) != (i % 7 != 0) || !local.is_valid(doc)) {
                    ++wrong;
                }
            }
        });
    }
    for (auto& th : threads) {
        th.join();
    }
    CHECK(wrong.load() == 0);

    if (failures == 0) {
        std::printf("C++: all checks passed (corvus_json_schema %.*s)\n", static_cast<int>(cjs::version().size()),
                    cjs::version().data());
    }
    return failures == 0 ? 0 : 1;
}
