# A high-performance JSON Schema evaluator (draft 4, 6, 7, 2019-09 and 2020-12) for R, backed by the
# corvus-json-schema Rust crate, with results collection and annotations.
#
#   validator <- compile_schema(list(type = "object", required = list("id")))
#   is_valid(validator, list(id = 3))          # TRUE
#   is_valid_json(validator, '{"id": 3}')      # TRUE, parsed in Rust
#
# The native code (src/rust) returns a failure as a list of class corvus_native_error, which is raised here.

dialects <- c(draft4 = 4L, draft6 = 6L, draft7 = 7L, "draft2019-09" = 2019L, "draft2020-12" = 2020L)

results_levels <- c(basic = 0L, detailed = 1L, verbose = 2L)

# Raises the condition of a failure the native code returned, or gives the value back.
checked <- function(value, call = sys.call(-1L)) {
  if (!inherits(value, "corvus_native_error")) {
    return(value)
  }
  kind <- switch(value[[1L]],
    compilation = "corvus_compilation_error",
    depth = "corvus_depth_error",
    invalid_json = "corvus_invalid_json_error",
    value = "corvus_value_error",
    callback = "corvus_callback_error",
    "corvus_internal_error"
  )
  stop(structure(
    class = c(kind, "corvus_json_schema_error", "error", "condition"),
    list(message = value[[2L]], call = call)
  ))
}

# Wraps a function so that calling it never signals a condition: it gives list(TRUE, value) or list(FALSE, message).
guarded <- function(f) {
  force(f)
  function(x) {
    tryCatch(
      list(TRUE, f(x)),
      error = function(e) list(FALSE, conditionMessage(e)),
      interrupt = function(e) list(FALSE, "interrupted")
    )
  }
}

one_of <- function(value, choices, what) {
  if (!is.character(value) || length(value) != 1L || is.na(value) || !value %in% names(choices)) {
    stop(sprintf("%s must be one of %s", what, paste0('"', names(choices), '"', collapse = ", ")), call. = FALSE)
  }
  choices[[value]]
}

compile_schema <- function(schema, default_dialect = "draft2020-12", assert_format = NA,
                           assert_format_in_legacy_drafts = FALSE, assert_content = TRUE, formats = NULL,
                           resolver = NULL, base_uri = NULL, entry_point = NULL, max_depth = 128L) {
  dialect <- one_of(default_dialect, dialects, "default_dialect")
  if (!is.null(formats)) {
    if (!is.list(formats) || is.null(names(formats)) || anyNA(names(formats)) || any(names(formats) == "") ||
          !all(vapply(formats, is.function, logical(1L)))) {
      stop("formats must be a named list of functions", call. = FALSE)
    }
    formats <- lapply(formats, guarded)
  }
  if (!is.null(resolver)) {
    if (!is.function(resolver)) {
      stop("resolver must be a function", call. = FALSE)
    }
    resolver <- guarded(resolver)
  }
  pointer <- checked(.Call(
    native_cjsr_compile, schema, dialect, as.logical(assert_format), as.logical(assert_format_in_legacy_drafts),
    as.logical(assert_content), formats, resolver, base_uri, entry_point, as.integer(max_depth)
  ))
  structure(list(pointer = pointer), class = "corvus_json_schema")
}

pointer_of <- function(validator) {
  if (!inherits(validator, "corvus_json_schema")) {
    stop("validator must be a compiled schema (see compile_schema)", call. = FALSE)
  }
  validator$pointer
}

is_valid <- function(validator, value) {
  checked(.Call(native_cjsr_is_valid, pointer_of(validator), value))
}

is_valid_json <- function(validator, json) {
  checked(.Call(native_cjsr_is_valid_json, pointer_of(validator), json))
}

# A list of columns as a data frame.
as_data_frame <- function(columns, names) {
  names(columns) <- names
  structure(columns, class = "data.frame", row.names = .set_row_names(length(columns[[1L]])))
}

# The list the native code gives for an evaluation, as the object evaluate() and evaluate_json() return.
evaluation <- function(native, level) {
  annotations <- native[[3L]]
  if (!is.null(annotations)) {
    annotations <- as_data_frame(annotations, c("instance_location", "keyword", "schema_location", "value"))
  }
  structure(
    list(
      valid = native[[1L]],
      level = level,
      results = as_data_frame(native[[2L]], c("valid", "message", "evaluation_location", "schema_location",
                                              "instance_location")),
      annotations = annotations
    ),
    class = "corvus_evaluation"
  )
}

evaluate <- function(validator, value, level = "detailed") {
  code <- one_of(level, results_levels, "level")
  evaluation(checked(.Call(native_cjsr_evaluate, pointer_of(validator), value, code)), level)
}

evaluate_json <- function(validator, json, level = "detailed") {
  code <- one_of(level, results_levels, "level")
  evaluation(checked(.Call(native_cjsr_evaluate_json, pointer_of(validator), json, code)), level)
}

crate_version <- function() {
  checked(.Call(native_cjsr_crate_version))
}

print.corvus_json_schema <- function(x, ...) {
  cat("<a compiled JSON Schema>\n")
  invisible(x)
}

print.corvus_evaluation <- function(x, ...) {
  cat(sprintf("<a JSON Schema evaluation: %s, %d %s result%s>\n", if (x$valid) "valid" else "not valid",
              nrow(x$results), x$level, if (nrow(x$results) == 1L) "" else "s"))
  failures <- x$results[!x$results$valid, , drop = FALSE]
  for (i in seq_len(min(nrow(failures), 10L))) {
    cat(sprintf("  %s: %s\n", if (nzchar(failures$instance_location[i])) failures$instance_location[i] else "(root)",
                failures$message[i]))
  }
  if (nrow(failures) > 10L) {
    cat(sprintf("  ... and %d more\n", nrow(failures) - 10L))
  }
  invisible(x)
}
