# The package's API: how R values read as JSON, the options of compile_schema(), results and annotations, and the
# conditions raised.
library(corvusjsonschema)

# The condition a call raises (NULL when it raises none).
condition_of <- function(expr) {
  tryCatch({
    expr
    NULL
  }, condition = function(e) e)
}

raises <- function(expr, class, pattern = NULL) {
  e <- condition_of(expr)
  # The package's own conditions share a class; a bad argument is a plain error.
  ok <- inherits(e, class) && (!startsWith(class, "corvus_") || inherits(e, "corvus_json_schema_error")) &&
    (is.null(pattern) || grepl(pattern, conditionMessage(e)))
  if (!ok) {
    stop(sprintf("expected a %s%s, got %s", class, if (is.null(pattern)) "" else paste0(" matching ", pattern),
                 if (is.null(e)) "no condition" else paste(class(e)[1L], conditionMessage(e))), call. = FALSE)
  }
  invisible(TRUE)
}

accepts <- function(schema, value) {
  is_valid(compile_schema(schema), value)
}

stopifnot(is.character(crate_version()), nzchar(crate_version()))

# ---------------------------------------------------------------------------------------------------------------------
# A schema from JSON text or from R values, and the two ways to validate

validator <- compile_schema('{"type": "object", "required": ["id"], "properties": {"id": {"type": "integer"}}}')
stopifnot(inherits(validator, "corvus_json_schema"))
stopifnot(is_valid(validator, list(id = 3L)), !is_valid(validator, list(id = "3")), !is_valid(validator, list()))
stopifnot(is_valid_json(validator, '{"id": 3}'), !is_valid_json(validator, '{"id": "3"}'))

from_values <- compile_schema(list(type = "object", required = list("id"),
                                   properties = list(id = list(type = "integer"))))
stopifnot(is_valid(from_values, list(id = 3L)), !is_valid(from_values, list(name = "x")))
stopifnot(compile_schema(TRUE) |> is_valid(1), !is_valid(compile_schema(FALSE), 1))

# is_valid_json() takes a character vector: one verdict for each text, and NA for NA.
stopifnot(identical(is_valid_json(validator, c('{"id": 1}', '{}', NA, '{"id": 2.5}')), c(TRUE, FALSE, NA, FALSE)))
stopifnot(identical(is_valid_json(validator, character()), logical()))

# ---------------------------------------------------------------------------------------------------------------------
# How R values read as JSON

# NULL is null, and NA of any type is null.
stopifnot(accepts('{"type": "null"}', NULL), accepts('{"type": "null"}', NA), accepts('{"type": "null"}', NA_integer_),
          accepts('{"type": "null"}', NA_real_), accepts('{"type": "null"}', NA_character_))
stopifnot(accepts('{"items": {"type": ["integer", "null"]}}', c(1L, NA, 3L)))

# A vector of length one is a scalar; of any other length, or wrapped in I(), an array.
stopifnot(accepts('{"type": "boolean"}', TRUE), accepts('{"type": "string"}', "a"), accepts('{"type": "number"}', 1.5))
stopifnot(accepts('{"type": "array", "maxItems": 0}', character()), accepts('{"type": "array", "minItems": 3}', 1:3))
stopifnot(accepts('{"type": "array", "items": {"type": "string"}}', c("a", "b")))
stopifnot(accepts('{"type": "array", "minItems": 1, "maxItems": 1}', I("a")), !accepts('{"type": "array"}', "a"))
stopifnot(accepts('{"type": "array", "items": {"type": "number"}}', c(a = 1, b = 2)))

# A list with names is an object and a list without is an array: list() is [] and an empty named list is {}.
stopifnot(accepts('{"type": "array"}', list()), accepts('{"type": "object"}', setNames(list(), character())))
stopifnot(accepts('{"type": "array", "items": {"type": "integer"}}', list(1L, 2L)))
stopifnot(accepts('{"type": "object", "required": ["a", "b"]}', list(a = 1, b = list(2, 3))))
stopifnot(accepts('{"properties": {"a": {"const": [1, [2, {"b": null}]]}}}', list(a = list(1, list(2, list(b = NULL))))))
# The first of two properties with one name is the one read.
stopifnot(accepts('{"properties": {"a": {"const": 1}}}', list(a = 1, a = 2)))

# A whole double is an integer, in every dialect; integers and doubles compare by value.
stopifnot(accepts('{"type": "integer"}', 3), accepts('{"type": "integer"}', 3L), !accepts('{"type": "integer"}', 3.5))
stopifnot(is_valid(compile_schema('{"type": "integer"}', default_dialect = "draft4"), 3))
stopifnot(accepts('{"const": 3}', 3L), accepts('{"enum": [1, 2]}', 2), accepts('{"maximum": 1e300}', 1e299))
stopifnot(accepts('{"type": "integer", "minimum": 9007199254740992}', 2^53), accepts('{"multipleOf": 0.5}', 2.5))

# Strings are UTF-8 whatever encoding R marks them with.
utf8 <- "café"
latin1 <- iconv(utf8, "UTF-8", "latin1")
stopifnot(Encoding(latin1) == "latin1")
stopifnot(accepts('{"const": "café"}', utf8), accepts('{"const": "café"}', latin1))
stopifnot(accepts('{"maxLength": 4, "minLength": 4}', latin1))
stopifnot(accepts('{"required": ["café"]}', setNames(list(1), latin1)))
stopifnot(accepts('{"type": "string", "minLength": 2, "maxLength": 2}', "\U0001F600\U0001F600"))

# What is not a JSON value is refused, when the schema examines it.
raises(accepts('{"type": "object"}', data.frame(a = 1)), "corvus_value_error", "data.frame")
raises(accepts('{"type": "string"}', factor("a")), "corvus_value_error", "factor")
raises(accepts('{"type": "string"}', Sys.Date()), "corvus_value_error", "Date")
raises(accepts('{"type": "string"}', mean), "corvus_value_error", "function")
raises(accepts('{"type": "string"}', globalenv()), "corvus_value_error", "environment")
raises(accepts('{"type": "number"}', NaN), "corvus_value_error", "NaN")
raises(accepts('{"items": {"type": "number"}}', c(1, Inf)), "corvus_value_error", "infinities")
raises(accepts('{"type": "number"}', 1i), "corvus_value_error", "complex")
raises(accepts('{"type": "string"}', as.raw(1)), "corvus_value_error", "raw")
raises(accepts('{"additionalProperties": {"type": "integer"}}', setNames(list(1L, 2L), c("a", NA))),
       "corvus_value_error", "NA name")
# A value the schema does not examine is not read.
stopifnot(accepts('{"properties": {"a": {"type": "integer"}}}', list(a = 1L, b = mean)))
raises(compile_schema(list(type = mean)), "corvus_value_error", "function")

# Lists nested more deeply than the reader follows in place are converted, up to a limit.
nested <- function(depth) {
  value <- list()
  for (i in seq_len(depth)) value <- list(value)
  value
}
stopifnot(accepts('{"type": "array"}', nested(10L)))
recursive <- compile_schema('{"$ref": "#/$defs/a", "$defs": {"a": {"type": "array", "items": {"$ref": "#/$defs/a"}}}}')
stopifnot(is_valid(recursive, nested(100L)), is_valid(recursive, nested(600L)), !is_valid(recursive, list(list(1))))
raises(is_valid(recursive, nested(1500L)), "corvus_value_error", "nests too deeply")

# A schema that refers to itself without moving through the instance stops at max_depth.
looping <- compile_schema('{"$ref": "#/$defs/a", "$defs": {"a": {"$ref": "#/$defs/a"}}}', max_depth = 16L)
raises(is_valid(looping, 1), "corvus_depth_error")

# A not that leads back to the schema it is in is under the same guard. (Version 0.1.0 overflowed the stack on the
# first of these, which ends the R session.)
for (schema in c('{"not": {"$ref": "#"}}', '{"not": {"not": {"$ref": "#"}}}', '{"allOf": [{"not": {"$ref": "#"}}]}',
                 '{"$defs": {"loop": {"allOf": [{"$ref": "#/$defs/loop"}]}}, "not": {"$ref": "#/$defs/loop"}}')) {
  under_not <- compile_schema(schema, max_depth = 16L)
  raises(is_valid(under_not, 1), "corvus_depth_error")
  raises(is_valid(under_not, list(a = 1)), "corvus_depth_error")
  raises(is_valid_json(under_not, "1"), "corvus_depth_error")
  raises(evaluate(under_not, 1, level = "verbose"), "corvus_depth_error")
  raises(evaluate_json(under_not, "[1]"), "corvus_depth_error")
}
raises(is_valid_json(looping, "1"), "corvus_depth_error")
raises(evaluate(looping, 1), "corvus_depth_error")

# ---------------------------------------------------------------------------------------------------------------------
# Options

stopifnot(is_valid(compile_schema('{"type": "integer"}', default_dialect = "draft7"), 1))
stopifnot(!is_valid(compile_schema('{"id": "x", "$ref": "#/definitions/a", "definitions": {"a": {"type": "string"}}}',
                                   default_dialect = "draft4"), 1))
raises(compile_schema("{}", default_dialect = "draft3"), "error", "default_dialect must be one of")

email <- '{"format": "email"}'
stopifnot(is_valid(compile_schema(email), "not an email"))
stopifnot(!is_valid(compile_schema(email, assert_format = TRUE), "not an email"))
stopifnot(is_valid(compile_schema(email, assert_format = TRUE), "a@example.com"))
stopifnot(!is_valid(compile_schema(email, default_dialect = "draft7", assert_format_in_legacy_drafts = TRUE), "x"))
stopifnot(is_valid(compile_schema(email, default_dialect = "draft7", assert_format = FALSE,
                                  assert_format_in_legacy_drafts = TRUE), "x"))

content <- '{"contentEncoding": "base64"}'
stopifnot(!is_valid(compile_schema(content, default_dialect = "draft7"), "***"))
stopifnot(is_valid(compile_schema(content, default_dialect = "draft7", assert_content = FALSE), "***"))

entry <- compile_schema('{"type": "string", "$defs": {"n": {"type": "integer"}}}', entry_point = "#/$defs/n")
stopifnot(is_valid(entry, 1L), !is_valid(entry, "a"))

# A custom format is an R function of the string.
seen <- character()
even <- compile_schema('{"format": "even-length"}', assert_format = TRUE, formats = list("even-length" = function(s) {
  seen <<- c(seen, s)
  nchar(s) %% 2L == 0L
}))
stopifnot(is_valid(even, "ab"), !is_valid(even, "abc"), is_valid_json(even, '"abcd"'), identical(seen, c("ab", "abc", "abcd")))
stopifnot(is_valid(even, 5))
failing <- compile_schema('{"format": "f"}', assert_format = TRUE, formats = list(f = function(s) stop("no: ", s)))
raises(is_valid(failing, "x"), "corvus_callback_error", "no: x")
raises(is_valid_json(failing, '"y"'), "corvus_callback_error", "no: y")
raises(evaluate(failing, "z"), "corvus_callback_error", "no: z")
stopifnot(is_valid(failing, 1))
raises(compile_schema("{}", formats = list(function(s) TRUE)), "error", "named list of functions")
raises(compile_schema("{}", formats = list(a = 1)), "error", "named list of functions")

# A resolver gives the document of an absolute URI: JSON text, R values, or NULL when it does not know it.
documents <- list(
  "https://example.com/text.json" = '{"type": "integer"}',
  "https://example.com/values.json" = list(type = "string")
)
asked <- character()
resolve <- function(uri) {
  asked <<- c(asked, uri)
  documents[[uri]]
}
referring <- compile_schema(
  '{"properties": {"n": {"$ref": "https://example.com/text.json"}, "s": {"$ref": "https://example.com/values.json"}}}',
  resolver = resolve
)
stopifnot(is_valid(referring, list(n = 1L, s = "a")), !is_valid(referring, list(n = "a")), !is_valid(referring, list(s = 1)))
stopifnot(setequal(asked, names(documents)))
raises(compile_schema('{"$ref": "https://example.com/unknown.json"}', resolver = resolve), "corvus_compilation_error")
raises(compile_schema('{"$ref": "https://example.com/x.json"}', resolver = function(uri) stop("offline")),
       "corvus_compilation_error", "offline")
raises(compile_schema('{"$ref": "https://example.com/x.json"}', resolver = function(uri) "{not json"),
       "corvus_compilation_error", "invalid JSON")
based <- compile_schema('{"$ref": "text.json"}', base_uri = "https://example.com/root.json", resolver = resolve)
stopifnot(is_valid(based, 1L), !is_valid(based, "a"))
raises(compile_schema("{}", resolver = "x"), "error", "resolver must be a function")

# ---------------------------------------------------------------------------------------------------------------------
# Results and annotations

person <- compile_schema(paste0(
  '{"title": "Person", "type": "object", "required": ["name"], ',
  '"properties": {"name": {"type": "string", "title": "Name"}, "age": {"type": "integer", "minimum": 0}}}'
))
bad <- evaluate(person, list(age = -1L))
stopifnot(inherits(bad, "corvus_evaluation"), identical(bad$valid, FALSE), identical(bad$level, "detailed"))
stopifnot(is.data.frame(bad$results), nrow(bad$results) >= 2L, is.null(bad$annotations))
stopifnot(identical(names(bad$results),
                    c("valid", "message", "evaluation_location", "schema_location", "instance_location")))
stopifnot(is.logical(bad$results$valid), is.character(bad$results$message), !any(bad$results$valid))
# The detailed level, the default, has the messages; the basic level has the same rows without them.
stopifnot(all(nzchar(bad$results$message)), any(grepl("greater than or equal to '0'", bad$results$message)))
basic <- evaluate(person, list(age = -1L), level = "basic")
stopifnot(identical(basic$level, "basic"), !any(nzchar(basic$results$message)))
stopifnot(identical(basic$results[-2L], bad$results[-2L]))
# The root's own result is a row too, with empty locations.
stopifnot(any(bad$results$instance_location == "" & bad$results$evaluation_location == ""))
stopifnot("/age" %in% bad$results$instance_location, any(grepl("minimum", bad$results$evaluation_location)))
stopifnot(any(grepl("not valid", capture.output(print(bad)))), any(grepl("/age", capture.output(print(bad)))))

good <- evaluate(person, list(name = "Ada", age = 36L), level = "verbose")
stopifnot(good$valid, nrow(good$results) > nrow(evaluate(person, list(name = "Ada", age = 36L))$results))
notes <- good$annotations
stopifnot(is.data.frame(notes), identical(names(notes), c("instance_location", "keyword", "schema_location", "value")))
titles <- notes[notes$keyword == "title", ]
titles <- titles[order(titles$instance_location), ]
stopifnot(identical(titles$instance_location, c("", "/name")), identical(titles$schema_location, c("#", "#/properties/name")))
stopifnot(identical(titles$value, list("Person", "Name")))
# An annotation's value is the JSON value as R reads it back: an array is a list.
listed <- evaluate(compile_schema('{"properties": {"a": true, "b": true}, "examples": [1, {"x": null}]}'),
                   list(a = 1, b = 2), level = "verbose")$annotations
stopifnot(identical(listed$value[listed$keyword == "examples"], list(list(1L, list(x = NULL)))))
stopifnot(any(grepl("valid", capture.output(print(good)))))

# The same rows whether the instance is R values or JSON text.
as_text <- evaluate_json(person, '{"age": -1}')
stopifnot(identical(as_text$results, bad$results), identical(as_text$valid, bad$valid))
passing <- evaluate_json(person, '{"name": "Ada"}', level = "basic")
stopifnot(passing$valid, identical(passing$level, "basic"), all(passing$results$valid))
raises(evaluate(person, list(), level = "everything"), "error", "level must be one of")

# ---------------------------------------------------------------------------------------------------------------------
# Conditions

raises(compile_schema("{not json"), "corvus_invalid_json_error", "not valid JSON")
raises(compile_schema('{"pattern": "("}'), "corvus_compilation_error", "regular expression")
raises(compile_schema('{"$ref": "#/nowhere"}'), "corvus_compilation_error")
raises(is_valid_json(validator, "{"), "corvus_invalid_json_error")
raises(is_valid_json(validator, c("{}", "[1,")), "corvus_invalid_json_error", "element 2")
raises(is_valid_json(validator, 1), "corvus_value_error", "character vector")
raises(evaluate_json(validator, c("{}", "{}")), "corvus_value_error", "one string")
raises(evaluate_json(validator, "nope"), "corvus_invalid_json_error")
raises(is_valid(list(), 1), "corvus_value_error", "compiled schema")
raises(is_valid_json(NULL, "1"), "corvus_value_error", "compiled schema")
raises(evaluate(structure(list(), class = "corvus_json_schema"), 1), "corvus_value_error", "compiled schema")
stopifnot(any(grepl("compiled JSON Schema", capture.output(print(validator)))))

# A compiled schema does not survive being saved and loaded: it says so.
path <- tempfile(fileext = ".rds")
saveRDS(validator, path)
restored <- readRDS(path)
raises(is_valid(restored, list(id = 1L)), "corvus_value_error", "no longer valid")

# Validators are freed by the collector, with their callbacks.
for (i in 1:200) compile_schema('{"format": "f"}', formats = list(f = function(s) TRUE))
invisible(gc())

cat("the API tests passed\n")
