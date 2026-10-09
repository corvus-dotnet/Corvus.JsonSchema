\name{corvusjsonschema-package}
\alias{corvusjsonschema-package}
\alias{corvusjsonschema}
\docType{package}
\title{A High-Performance JSON Schema Validator}
\description{
Validates R values and JSON text against JSON Schema drafts 4, 6, 7, 2019-09 and 2020-12, with results collection and
annotations.

Compile a schema once with \code{\link{compile_schema}}. Then \code{\link{is_valid}} validates an R value, read in
place, and \code{\link{is_valid_json}} validates JSON text. \code{\link{evaluate}} and \code{\link{evaluate_json}}
report why a value is not valid, and the annotations a schema makes.

The evaluator is the \code{corvus-json-schema} Rust crate, one of a family of implementations of one design in
several languages. It passes every test of the JSON-Schema-Test-Suite.
}
\author{
Matthew Adams, Endjin Limited.
}
\seealso{
\url{https://github.com/corvus-dotnet/Corvus.JsonSchema}
}
\keyword{package}
