# The JSON-Schema-Test-Suite (the repository's submodule, or JSON_SCHEMA_TEST_SUITE): every case with its schema and
# data as JSON text and as R values read in place (as jsonlite parses them), each also at the verbose level through
# evaluate() and evaluate_json(), which must give the same verdict. Every dialect's cases are in one tree
# (validation/tests, required and optional): each case's compatibility selects the drafts it applies to, its tags
# whether format is asserted, and its externalSchemas are served to that case alone.
library(corvusjsonschema)

root <- Sys.getenv("JSON_SCHEMA_TEST_SUITE", file.path("..", "..", "..", "JSON-Schema-Test-Suite"))
if (!dir.exists(file.path(root, "validation", "tests")) || !requireNamespace("jsonlite", quietly = TRUE)) {
  message("skipped: needs jsonlite and the JSON-Schema-Test-Suite (JSON_SCHEMA_TEST_SUITE)")
  quit(save = "no", status = 0L)
}
root <- normalizePath(root, winslash = "/")

drafts <- c("draft4" = 4L, "draft6" = 6L, "draft7" = 7L, "draft2019-09" = 2019L, "draft2020-12" = 2020L)
tests_root <- file.path(root, "validation", "tests")
# As the other runners: a JSON parser cannot tell 1.0 from 1 here (draft 4 does not count 1.0 as an integer).
excluded <- "draft4/optional/zero-terminated-floats.json"
# As the crate's runner, test cases left out by name (file and test description): the high-precision cases of
# optional/bignum.json, whose digits R's doubles and the crate's serde_json values do not hold.
excluded_cases <- paste("bignum.json", c(
  "below the exclusive maximum is valid",
  "above the exclusive minimum is valid",
  "just above the maximum is invalid",
  "just below the minimum is invalid",
  "a slightly smaller decimal is invalid",
  "a slightly larger decimal is invalid"
), sep = "|")

# Whether a test case's compatibility (a comma-separated list of minimums, <= maximums and = releases, from the
# smallest release to the largest) admits the release, by the algorithm of the suite's validation/README.md.
compatible <- function(compatibility, release) {
  if (is.null(compatibility)) {
    return(TRUE)
  }
  valid <- TRUE
  for (constraint in strsplit(compatibility, ",", fixed = TRUE)[[1L]]) {
    op <- if (startsWith(constraint, "<=")) "<=" else if (startsWith(constraint, "=")) "=" else ""
    number <- as.integer(trimws(substring(constraint, nchar(op) + 1L)))
    valid <- switch(op, "<=" = release <= number, "=" = release == number, release >= number)
    if (release <= number) {
      break
    }
  }
  valid
}

has_tag <- function(group, tag) {
  tag %in% unlist(group$tags)
}

read_text <- function(path) {
  text <- readChar(path, file.info(path)$size, useBytes = TRUE)
  Encoding(text) <- "UTF-8"
  text
}

# A resolver serving a test case's externalSchemas (as jsonlite parses them) at their retrieval URIs.
resolver_for <- function(group) {
  schemas <- group$externalSchemas
  if (is.null(schemas)) {
    return(NULL)
  }
  function(uri) schemas[[uri]]
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
  # The file is an object whose tests are an array of groups (objects at depth three), each with an array of tests
  # (objects at depth five).
  list(schemas = values("schema", 3L), data = values("data", 5L))
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
files <- c(json_files(tests_root), json_files(file.path(tests_root, "optional")))
for (draft in names(drafts)) {
  release <- drafts[[draft]]
  for (file in files) {
    label <- paste0(draft, "/", substring(file, nchar(tests_root) + 2L))
    if (label %in% excluded) {
      next
    }
    file_text <- read_text(file)
    texts <- suite_texts(file_text)
    for (group in jsonlite::fromJSON(file_text, simplifyVector = FALSE)$tests) {
      schema_text <- texts$schemas[1L]
      texts$schemas <- texts$schemas[-1L]
      data_texts <- texts$data[seq_along(group$tests)]
      texts$data <- texts$data[-seq_along(group$tests)]
      if (!compatible(group$compatibility, release)) {
        next
      }
      # The evaluator asserts format by default up to draft7, so a format-annotation case does not apply there.
      if (has_tag(group, "format-annotation") && release <= 7L) {
        next
      }
      assert_format <- has_tag(group, "format-assertion")
      compiled <- tryCatch({
        options <- list(default_dialect = draft, assert_format = if (assert_format) TRUE else NA,
                        resolver = resolver_for(group))
        list(
          text = do.call(compile_schema, c(list(schema_text), options)),
          value = if (!has_nul(schema_text)) do.call(compile_schema, c(list(group$schema), options))
        )
      }, corvus_json_schema_error = function(e) e)
      keep <- !(paste(basename(file), vapply(group$tests, function(t) t$description, ""), sep = "|") %in%
                  excluded_cases)
      total <- total + sum(keep)
      if (inherits(compiled, "condition")) {
        failures <- c(failures, sprintf("%s [%s]: %s", label, group$description, conditionMessage(compiled)))
        next
      }
      for (t in which(keep)) {
        test <- group$tests[[t]]
        what <- sprintf("%s [%s] %s", label, group$description, test$description)
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
stopifnot(total == 8573L)
cat(sprintf("%d cases: every one as JSON text and, but for %d that hold a NUL, read in place from R values\n",
            total, text_only))
