// The corvus-json-schema C++ wrapper: header-only, C++17, over the C library (corvus_json_schema.h).
//
// Each class owns its handle through std::unique_ptr, freeing it with the C library's _free function. A validator
// copies by cloning its handle (cheap: the compiled program is shared); documents, collectors and options are
// move-only. Errors are exceptions (corvus::json_schema::error), but validator::try_validate reports a status for code
// built without them. String views returned by the library are valid as the C API says (see each function).
//
//     #include <corvus_json_schema.hpp>
//     namespace cjs = corvus::json_schema;
//     auto v = cjs::validator::compile(R"({"type": "array", "items": {"type": "integer"}})");
//     bool ok = v.is_valid("[1, 2, 3]");

#ifndef CORVUS_JSON_SCHEMA_HPP
#define CORVUS_JSON_SCHEMA_HPP

#include <cstddef>
#include <cstdint>
#include <functional>
#include <memory>
#include <new>
#include <optional>
#include <stdexcept>
#include <string>
#include <string_view>
#include <utility>

#include "corvus_json_schema.h"

namespace corvus::json_schema {

/// The JSON Schema dialects.
enum class dialect : cjs_dialect {
    draft4 = CJS_DRAFT4,
    draft6 = CJS_DRAFT6,
    draft7 = CJS_DRAFT7,
    draft2019_09 = CJS_DRAFT201909,
    draft2020_12 = CJS_DRAFT202012,
};

/// How much a collector records.
enum class results_level : cjs_results_level {
    /// Failures only, without message text.
    basic = CJS_BASIC,
    /// Failures only, with message text.
    detailed = CJS_DETAILED,
    /// Every evaluation, passing and failing, with message text, including annotations.
    verbose = CJS_VERBOSE,
};

/// A failed call: the C library's status, its message and, for invalid UTF-8 or JSON, the byte offset.
class error : public std::runtime_error {
public:
    error(cjs_status status, const std::string& message, std::size_t offset)
        : std::runtime_error(message), status_(status), offset_(offset) {}

    cjs_status status() const noexcept { return status_; }
    std::size_t offset() const noexcept { return offset_; }

private:
    cjs_status status_;
    std::size_t offset_;
};

/// A format assertion: whether the string (or a number's JSON text) is valid. It runs on whichever thread validates.
using format_validator = std::function<bool(std::string_view value)>;

/// A document resolver: the JSON text of the document at an absolute URI, or nothing for an unknown document. An
/// exception fails the compilation with its message.
using document_resolver = std::function<std::optional<std::string>(std::string_view uri)>;

namespace detail {

inline std::string_view view(cjs_str s) noexcept {
    return s.len == 0 ? std::string_view() : std::string_view(s.ptr, s.len);
}

[[noreturn]] inline void throw_last_error(cjs_status status) {
    throw error(status, std::string(view(cjs_last_error_message())), cjs_last_error_offset());
}

inline void check(cjs_status status) {
    if (status != CJS_OK) {
        throw_last_error(status);
    }
}

template <auto Free>
struct deleter {
    template <class T>
    void operator()(T* handle) const noexcept {
        Free(handle);
    }
};

template <class T>
void delete_user_data(void* user_data) {
    delete static_cast<T*>(user_data);
}

inline bool format_trampoline(void* user_data, const char* value, std::size_t len) {
    try {
        return (*static_cast<format_validator*>(user_data))(std::string_view(value, len));
    } catch (...) {
        // An exception cannot cross into the library: a format that throws rejects the value.
        return false;
    }
}

inline cjs_status resolve_trampoline(void* user_data, const char* uri, std::size_t len, cjs_resolved* out) {
    try {
        std::optional<std::string> json = (*static_cast<document_resolver*>(user_data))(std::string_view(uri, len));
        return json ? cjs_resolved_set_json(out, json->data(), json->size()) : CJS_OK;
    } catch (const std::exception& e) {
        std::string_view message = e.what();
        cjs_resolved_set_error(out, message.data(), message.size());
    } catch (...) {
        std::string_view message = "the resolver threw an exception";
        cjs_resolved_set_error(out, message.data(), message.size());
    }
    return CJS_INVALID_ARGUMENT;
}

}  // namespace detail

/// Compile options: a builder, each setter returning the options.
class options {
public:
    options() : handle_(cjs_options_new()) {
        if (!handle_) {
            throw std::bad_alloc();
        }
    }

    /// The dialect of schemas without `$schema` (default 2020-12).
    options& default_dialect(dialect d) {
        detail::check(cjs_options_set_default_dialect(handle_.get(), static_cast<cjs_dialect>(d)));
        return *this;
    }

    /// Whether `format` is asserted; empty (the default) follows the vocabularies.
    options& assert_format(std::optional<bool> assert) {
        cjs_tristate mode = !assert ? CJS_DEFAULT : *assert ? CJS_TRUE : CJS_FALSE;
        detail::check(cjs_options_set_assert_format(handle_.get(), mode));
        return *this;
    }

    /// With format assertion left to its default, assert it in draft 4 to 7 too.
    options& assert_format_in_legacy_drafts(bool enabled) {
        detail::check(cjs_options_set_assert_format_in_legacy_drafts(handle_.get(), enabled));
        return *this;
    }

    /// Assert contentEncoding and contentMediaType in draft 7 (default true).
    options& assert_content(bool enabled) {
        detail::check(cjs_options_set_assert_content(handle_.get(), enabled));
        return *this;
    }

    /// The base URI of the root schema document.
    options& base_uri(std::string_view uri) {
        detail::check(cjs_options_set_base_uri(handle_.get(), uri.data(), uri.size()));
        return *this;
    }

    /// A reference, relative to the root, to evaluate from (such as "#/$defs/item").
    options& entry_point(std::string_view reference) {
        detail::check(cjs_options_set_entry_point(handle_.get(), reference.data(), reference.size()));
        return *this;
    }

    /// The maximum depth of in-place recursion on a cycle (default 128).
    options& max_depth(std::uint32_t depth) {
        detail::check(cjs_options_set_max_depth(handle_.get(), depth));
        return *this;
    }

    /// A custom format assertion, taking precedence over the built-in one of that name. It must be thread-safe.
    options& format(std::string_view name, format_validator validator) {
        // The library owns the function from the call on, and frees it even if the call fails.
        auto* user_data = new format_validator(std::move(validator));
        detail::check(cjs_options_add_format(handle_.get(), name.data(), name.size(), &detail::format_trampoline,
                                             user_data, &detail::delete_user_data<format_validator>));
        return *this;
    }

    /// The resolver for documents other than the standard metaschemas.
    options& resolver(document_resolver resolver) {
        auto* user_data = new document_resolver(std::move(resolver));
        detail::check(cjs_options_set_resolver(handle_.get(), &detail::resolve_trampoline, user_data,
                                               &detail::delete_user_data<document_resolver>));
        return *this;
    }

    cjs_options* native_handle() const noexcept { return handle_.get(); }
    cjs_options* release() noexcept { return handle_.release(); }

private:
    std::unique_ptr<cjs_options, detail::deleter<cjs_options_free>> handle_;
};

/// One result row, as views valid until the collector is next evaluated into, cleared or destroyed.
struct result_row {
    bool is_match;
    /// The message (empty when the level records none; raw JSON for an annotation row).
    std::string_view message;
    /// The keywords from the root schema, such as "/properties/name/type".
    std::string_view evaluation_location;
    /// The JSON pointer of the evaluated schema or keyword within its document.
    std::string_view schema_location;
    /// The JSON pointer of the evaluated value, such as "/name".
    std::string_view instance_location;
};

/// The results of the latest evaluation into it. Move-only; one thread at a time.
class collector {
public:
    explicit collector(results_level level) : handle_(cjs_collector_new(static_cast<cjs_results_level>(level))) {
        if (!handle_) {
            detail::throw_last_error(CJS_INVALID_ARGUMENT);
        }
    }

    std::size_t size() const noexcept { return cjs_collector_count(handle_.get()); }

    result_row operator[](std::size_t i) const noexcept {
        const cjs_collector* c = handle_.get();
        return result_row{cjs_collector_is_match(c, i), detail::view(cjs_collector_message(c, i)),
                          detail::view(cjs_collector_evaluation_location(c, i)),
                          detail::view(cjs_collector_schema_location(c, i)),
                          detail::view(cjs_collector_instance_location(c, i))};
    }

    /// The annotations of a verbose evaluation as JSON text, grouped by instance location, keyword and schema
    /// location. Valid until the collector next changes.
    std::string_view annotations_json() {
        cjs_str out{};
        detail::check(cjs_collector_annotations_json(handle_.get(), &out));
        return detail::view(out);
    }

    void clear() noexcept { cjs_collector_clear(handle_.get()); }

    cjs_collector* native_handle() const noexcept { return handle_.get(); }
    cjs_collector* release() noexcept { return handle_.release(); }

private:
    std::unique_ptr<cjs_collector, detail::deleter<cjs_collector_free>> handle_;
};

/// Parsed JSON text, for an instance validated more than once. Move-only; immutable, so shareable between threads.
class document {
public:
    /// Parses the text, copying it.
    static document parse(std::string_view json) {
        cjs_document* handle = nullptr;
        detail::check(cjs_document_parse(json.data(), json.size(), &handle));
        return document(handle);
    }

    /// Parses the text without copying it: the text must stay unchanged until the document is destroyed.
    static document parse_borrowed(std::string_view json) {
        cjs_document* handle = nullptr;
        detail::check(cjs_document_parse_borrowed(json.data(), json.size(), &handle));
        return document(handle);
    }

    cjs_document* native_handle() const noexcept { return handle_.get(); }
    cjs_document* release() noexcept { return handle_.release(); }

private:
    explicit document(cjs_document* handle) noexcept : handle_(handle) {}

    std::unique_ptr<cjs_document, detail::deleter<cjs_document_free>> handle_;
};

/// A compiled schema. Copies share the compiled program; any number of threads may use one at once.
class validator {
public:
    /// Compiles a schema from its JSON text.
    static validator compile(std::string_view schema) { return compile(schema, nullptr); }

    static validator compile(std::string_view schema, const options& opts) {
        return compile(schema, opts.native_handle());
    }

    /// Compiles the schema document at an absolute URI, fetched through the options' resolver (or a standard
    /// metaschema).
    static validator compile_uri(std::string_view uri, const options& opts) {
        cjs_validator* handle = nullptr;
        detail::check(cjs_compile_uri(uri.data(), uri.size(), opts.native_handle(), &handle));
        return validator(handle);
    }

    /// Takes ownership of a handle from the C API.
    static validator adopt(cjs_validator* handle) noexcept { return validator(handle); }

    validator(const validator& other) : handle_(cjs_validator_clone(other.handle_.get())) {
        if (other.handle_ && !handle_) {
            throw std::bad_alloc();
        }
    }

    validator& operator=(const validator& other) {
        if (this != &other) {
            validator copy(other);
            handle_ = std::move(copy.handle_);
        }
        return *this;
    }

    validator(validator&&) noexcept = default;
    validator& operator=(validator&&) noexcept = default;

    /// Whether the JSON text is valid. Throws for invalid UTF-8 or JSON, or a schema that recurses without end.
    bool is_valid(std::string_view json) const {
        bool valid = false;
        detail::check(cjs_validator_validate_json(handle_.get(), json.data(), json.size(), &valid));
        return valid;
    }

    /// Whether the parsed document is valid.
    bool is_valid(const document& doc) const {
        bool valid = false;
        detail::check(cjs_validator_validate_document(handle_.get(), doc.native_handle(), &valid));
        return valid;
    }

    /// is_valid without exceptions: the status, and `valid` on CJS_OK.
    cjs_status try_validate(std::string_view json, bool& valid) const noexcept {
        return cjs_validator_validate_json(handle_.get(), json.data(), json.size(), &valid);
    }

    /// Evaluates the JSON text, replacing the collector's results with this evaluation's; whether it is valid.
    bool evaluate(std::string_view json, collector& results) const {
        bool valid = false;
        detail::check(cjs_validator_evaluate_json(handle_.get(), json.data(), json.size(), results.native_handle(),
                                                  &valid));
        return valid;
    }

    bool evaluate(const document& doc, collector& results) const {
        bool valid = false;
        detail::check(cjs_validator_evaluate_document(handle_.get(), doc.native_handle(), results.native_handle(),
                                                      &valid));
        return valid;
    }

    cjs_validator* native_handle() const noexcept { return handle_.get(); }
    cjs_validator* release() noexcept { return handle_.release(); }

private:
    explicit validator(cjs_validator* handle) noexcept : handle_(handle) {}

    static validator compile(std::string_view schema, const cjs_options* opts) {
        cjs_validator* handle = nullptr;
        detail::check(cjs_compile(schema.data(), schema.size(), opts, &handle));
        return validator(handle);
    }

    std::unique_ptr<cjs_validator, detail::deleter<cjs_validator_free>> handle_;
};

/// The library's version, such as "0.1.0".
inline std::string_view version() noexcept {
    return detail::view(cjs_version_string());
}

}  // namespace corvus::json_schema

#endif  // CORVUS_JSON_SCHEMA_HPP
