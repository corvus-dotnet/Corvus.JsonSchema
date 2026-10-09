\name{is_valid}
\alias{is_valid}
\alias{is_valid_json}
\title{Validate a Value or JSON Text}
\description{
\code{is_valid} says whether an R value is valid against a compiled schema, reading the value in place.
\code{is_valid_json} says whether JSON text is, parsing the text itself, for each element of a character vector.
Both stop at the first failure. Use \code{\link{evaluate}} to find out why a value is not valid.
}
\usage{
is_valid(validator, value)

is_valid_json(validator, json)
}
\arguments{
\item{validator}{A compiled schema, from \code{\link{compile_schema}}.}
\item{value}{An R value, read as JSON as described below.}
\item{json}{A character vector of JSON texts.}
}
\value{
\code{is_valid} returns \code{TRUE} or \code{FALSE}. \code{is_valid_json} returns a logical vector as long as
\code{json}, with \code{NA} for an \code{NA} element.
}
\section{How R values read as JSON}{
\itemize{
\item \code{NULL} is \code{null}.
\item A list with names is an object, and a list without names is an array. So \code{list()} is \code{[]}, and an
  empty named list is \code{{}}. This is how the \pkg{jsonlite} package's \code{fromJSON(simplifyVector = FALSE)}
  gives JSON values. Where two elements have one name, the first is read.
\item A logical, integer, double or character vector of length one is a scalar. A vector of any other length is an
  array of scalars, and its names are ignored. Wrap a vector of length one in \code{\link{I}} for it to read as an
  array of one element.
\item \code{NA} of any type is \code{null}.
\item A whole double, such as \code{3}, is an integer as well as a number, in every dialect.
\item Strings are read as UTF-8 whatever encoding they are marked with.
}
Nothing else is a JSON value: not \code{NaN} or an infinity, a function, an environment, a raw or complex vector, or
an object with a class, such as a data frame, a factor or a date. Convert such a value first. A value is read only
where the schema examines it, so a value the schema ignores is never an error.

An R string cannot hold a NUL character, so JSON text with one in a string can only be validated as text.
}
\section{Conditions}{
A value that is not a JSON value raises \code{corvus_value_error}; text that is not JSON,
\code{corvus_invalid_json_error}; a schema that refers to itself more than \code{max_depth} times without moving
through the value, \code{corvus_depth_error}; and an error in a custom format function,
\code{corvus_callback_error}. All inherit from \code{corvus_json_schema_error}.
}
\examples{
validator <- compile_schema('{"type": "array", "items": {"type": "integer"}}')
is_valid(validator, 1:3)
is_valid(validator, list(1, 2.5))
is_valid(validator, 1)        # a scalar, not an array
is_valid(validator, I(1))     # an array of one
is_valid_json(validator, c("[1, 2]", '["a"]', NA))
}
