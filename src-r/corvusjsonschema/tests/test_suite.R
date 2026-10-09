# The JSON-Schema-Test-Suite (the repository's submodule, or JSON_SCHEMA_TEST_SUITE): every case with its schema and
# data as JSON text and as R values read in place (as jsonlite parses them), each also at the verbose level through
# evaluate() and evaluate_json(), which must give the same verdict. Remote references are served by a resolver over
# the suite's remotes.
library(corvusjsonschema)

root <- Sys.getenv("JSON_SCHEMA_TEST_SUITE", file.path("..", "..", "..", "JSON-Schema-Test-Suite"))
if (!dir.exists(file.path(root, "tests")) || !requireNamespace("jsonlite", quietly = TRUE)) {
  message("skipped: needs jsonlite and the JSON-Schema-Test-Suite (JSON_SCHEMA_TEST_SUITE)")
  quit(save = "no", status = 0L)
}
root <- normalizePath(root, winslash = "/")

drafts <- c("draft4", "draft6", "draft7", "draft2019-09", "draft2020-12")
# As the other runners: a JSON parser cannot tell 1.0 from 1 here (draft 4 does not count 1.0 as an integer).
excluded <- "draft4/optional/zeroTerminatedFloats.json"

read_text <- function(path) {
  text <- readChar(path, file.info(path)$size, useBytes = TRUE)
  Encoding(text) <- "UTF-8"
  text
}

resolver <- function(uri) {
  prefix <- "http://localhost:1234/"
  if (!startsWith(uri, prefix)) {
    return(NULL)
  }
  path <- file.path(root, "remotes", substring(uri, nchar(prefix) + 1L))
  if (file.exists(path)) read_text(path) else NULL
}

# The JSON text of each group's schema and of each test's data in a suite file, in order, cut from the file's own
# text: writing the parsed values back would lose the digits of numbers beyond a double's precision.
suite_texts <- function(text) {
  chars <- utf8ToInt(text)
  n <- length(chars)
  strings <- gregexpr('"(?:[^"\\\\]|\\\\.)*"', text, perl = TRUE)[[1L]]
  starts <- as.integer(strings)
  ends <- starts + attr(strings, "match.length") - 1L
  in_string <- cumsum(tabulate(starts, n + 1L) - tabulate(ends + 1L, n + 1L))[seq_len(n)] > 0L
  opens <- !in_string & (chars == 91L | chars == 123L)
  closes <- !in_string & (chars == 93L | chars == 125L)
  depth <- cumsum(opens - closes)
  # The values of a key of the objects at a depth: each ends at the object's next comma or at its closing brace.
  values <- function(key, at) {
    keys <- gregexpr(sprintf('"%s"\\s*:', key), text, perl = TRUE)[[1L]]
    colons <- as.integer(keys) + attr(keys, "match.length") - 1L
    colons <- colons[as.integer(keys) %in% starts & depth[as.integer(keys)] == at]
    stops <- which(!in_string & ((chars == 44L & depth == at) | (chars == 125L & depth == at - 1L)))
    trimws(substring(text, colons + 1L, stops[findInterval(colons, stops) + 1L] - 1L))
  }
  # The file is an array of groups (objects at depth two), each with an array of tests (objects at depth four).
  list(schemas = values("schema", 2L), data = values("data", 4L))
}

# R's strings cannot hold a NUL: a schema or an instance with one is checked as JSON text only.
has_nul <- function(text) {
  grepl("\\u0000", text, fixed = TRUE)
}

json_files <- function(dir) {
  sort(list.files(dir, "\\.json$", full.names = TRUE))
}

total <- 0L
text_only <- 0L
failures <- character()
for (draft in drafts) {
  dir <- file.path(root, "tests", draft)
  plain <- c(json_files(dir), json_files(file.path(dir, "optional")))
  formats <- json_files(file.path(dir, "optional", "format"))
  runs <- data.frame(file = c(plain, formats), format = rep(c(FALSE, TRUE), c(length(plain), length(formats))))
  for (r in seq_len(nrow(runs))) {
    label <- substring(runs$file[r], nchar(file.path(root, "tests")) + 2L)
    if (label %in% excluded) {
      next
    }
    file_text <- read_text(runs$file[r])
    texts <- suite_texts(file_text)
    for (group in jsonlite::fromJSON(file_text, simplifyVector = FALSE)) {
      schema_text <- texts$schemas[1L]
      texts$schemas <- texts$schemas[-1L]
      data_texts <- texts$data[seq_along(group$tests)]
      texts$data <- texts$data[-seq_along(group$tests)]
      compiled <- tryCatch({
        options <- list(default_dialect = draft, assert_format = if (runs$format[r]) TRUE else NA,
                        resolver = resolver)
        list(
          text = do.call(compile_schema, c(list(schema_text), options)),
          value = if (!has_nul(schema_text)) do.call(compile_schema, c(list(group$schema), options))
        )
      }, corvus_json_schema_error = function(e) e)
      total <- total + length(group$tests)
      if (inherits(compiled, "condition")) {
        failures <- c(failures, sprintf("%s [%s]: %s", label, group$description, conditionMessage(compiled)))
        next
      }
      for (t in seq_along(group$tests)) {
        test <- group$tests[[t]]
        what <- sprintf("%s [%s] %s", label, group$description, test$description)
        if (runs$format[r] && grepl("leap second", tolower(what), fixed = TRUE)) {
          next
        }
        text <- data_texts[t]
        actual <- tryCatch({
          verdicts <- c(json = is_valid_json(compiled$text, text),
                        evaluate_json = evaluate_json(compiled$text, text, "verbose")$valid)
          if (!is.null(compiled$value) && !has_nul(text)) {
            verdicts <- c(verdicts, value = is_valid(compiled$value, test$data),
                          evaluate = evaluate(compiled$value, test$data, "verbose")$valid)
          } else {
            text_only <- text_only + 1L
          }
          verdicts
        }, corvus_json_schema_error = function(e) conditionMessage(e))
        if (is.character(actual)) {
          failures <- c(failures, sprintf("%s: %s", what, actual))
        } else if (any(actual != test$valid)) {
          failures <- c(failures, sprintf("%s: expected %s, got %s", what, test$valid,
                                          paste(names(actual), actual, collapse = ", ")))
        }
      }
    }
    stopifnot(length(texts$schemas) == 0L, length(texts$data) == 0L)
  }
}

if (length(failures) > 0L) {
  writeLines(failures)
  stop(sprintf("%d of %d cases failed", length(failures), total))
}
stopifnot(total == 7966L)
cat(sprintf("%d cases: every one as JSON text and, but for %d that hold a NUL, read in place from R values\n",
            total, text_only))
