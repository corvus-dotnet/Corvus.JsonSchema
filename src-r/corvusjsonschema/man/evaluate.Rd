\name{evaluate}
\alias{evaluate}
\alias{evaluate_json}
\alias{print.corvus_evaluation}
\title{Evaluate a Value and Collect the Results}
\description{
Evaluates an R value (\code{evaluate}) or JSON text (\code{evaluate_json}) against a compiled schema without stopping
at the first failure, and returns what each keyword found.
}
\usage{
evaluate(validator, value, level = "detailed")

evaluate_json(validator, json, level = "detailed")
}
\arguments{
\item{validator}{A compiled schema, from \code{\link{compile_schema}}.}
\item{value}{An R value, read as \code{\link{is_valid}} reads one.}
\item{json}{One string of JSON text.}
\item{level}{How much to report: \code{"basic"} (the failures, without messages), \code{"detailed"} (the failures,
  with messages) or \code{"verbose"} (every keyword evaluated, passing ones included, and the annotations).}
}
\value{
An object of class \code{corvus_evaluation}, a list with:
\item{valid}{Whether the value is valid.}
\item{level}{The level asked for.}
\item{results}{A data frame with a row for each result, the root's own among them: \code{valid}, \code{message}, \code{evaluation_location}
  (the path of keywords followed, through references), \code{schema_location} (where the keyword is in the schema)
  and \code{instance_location} (where the value is in the instance), the three locations as JSON Pointers.}
\item{annotations}{At the \code{"verbose"} level, a data frame with a row for each annotation:
  \code{instance_location}, \code{keyword}, \code{schema_location} and \code{value} (a list column of the values,
  as R values). \code{NULL} at the other levels.}
}
\examples{
validator <- compile_schema('{
  "type": "object",
  "required": ["name"],
  "properties": {"name": {"type": "string", "title": "Name"},
                 "age": {"type": "integer", "minimum": 0}}
}')
result <- evaluate(validator, list(age = -1))
result
result$results[, c("instance_location", "message")]

evaluate_json(validator, '{"name": "Ada"}', level = "verbose")$annotations
}
