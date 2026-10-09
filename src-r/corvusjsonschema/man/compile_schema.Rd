\name{compile_schema}
\alias{compile_schema}
\alias{print.corvus_json_schema}
\title{Compile a JSON Schema}
\description{
Compiles a JSON Schema (draft 4, 6, 7, 2019-09 or 2020-12) into a validator. Compile a schema once and use the
validator for every value: compiling does the work that makes validating fast.
}
\usage{
compile_schema(schema, default_dialect = "draft2020-12", assert_format = NA,
               assert_format_in_legacy_drafts = FALSE, assert_content = TRUE,
               formats = NULL, resolver = NULL, base_uri = NULL,
               entry_point = NULL, max_depth = 128L)
}
\arguments{
\item{schema}{The schema: one string of JSON text, or R values (a list, or \code{TRUE} or \code{FALSE}) read as
  \code{\link{is_valid}} reads a value.}
\item{default_dialect}{The dialect of a schema that has no \code{$schema}: \code{"draft4"}, \code{"draft6"},
  \code{"draft7"}, \code{"draft2019-09"} or \code{"draft2020-12"}.}
\item{assert_format}{Whether \code{format} is asserted. \code{NA} follows the dialect's vocabularies, under which
  \code{format} is an annotation unless the schema's metaschema says otherwise.}
\item{assert_format_in_legacy_drafts}{With \code{assert_format = NA}, whether \code{format} is asserted in draft 4
  to 7.}
\item{assert_content}{Whether \code{contentEncoding} and \code{contentMediaType} are asserted in draft 7, the only
  draft that asserts them.}
\item{formats}{A named list of functions, one for each custom format. Each takes the string and returns whether it is
  valid. They take precedence over the built-in formats.}
\item{resolver}{A function that gives the document of an absolute URI the schema refers to: JSON text, R values, or
  \code{NULL} when it does not know the document. The standard metaschemas are always known.}
\item{base_uri}{The base URI of the schema, for a schema with relative references and no \code{$id}.}
\item{entry_point}{A reference, relative to the schema's root, to the subschema to validate against, such as
  \code{"#/$defs/item"}.}
\item{max_depth}{How many times a schema may refer to itself without moving through the value before evaluation is
  abandoned with an error.}
}
\value{
An object of class \code{corvus_json_schema}, for \code{\link{is_valid}}, \code{\link{is_valid_json}},
\code{\link{evaluate}} and \code{\link{evaluate_json}}. It holds compiled native code, so it does not survive
\code{\link{saveRDS}} or a saved workspace: compile the schema again in a new session.
}
\section{Conditions}{
A schema that cannot be compiled raises a condition of class \code{corvus_compilation_error}; schema text that is not
JSON, \code{corvus_invalid_json_error}; and an R value that is not a JSON value, \code{corvus_value_error}. All
inherit from \code{corvus_json_schema_error}.
}
\examples{
validator <- compile_schema('{
  "type": "object",
  "required": ["id"],
  "properties": {"id": {"type": "integer", "minimum": 1}}
}')
is_valid(validator, list(id = 3))
is_valid_json(validator, '{"id": 0}')

# The same schema as R values.
compile_schema(list(type = "object", required = list("id")))

# A custom format.
even <- compile_schema('{"format": "even-length"}', assert_format = TRUE,
                       formats = list("even-length" = function(s) nchar(s) \%\% 2 == 0))
is_valid(even, "ab")
}
