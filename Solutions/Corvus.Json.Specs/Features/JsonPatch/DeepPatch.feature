Feature: Deep patching

Scenario Template: Deep patch
	Given the document <document>
	When I deep patch the JSON value <value> at the path <path>
	Then the transformed document should equal <result>

Examples:
	| document       | value       | path           | result                                      |
	| {}             | {"foo": 3 } |                | {"foo": 3 }                                 |
	| {"foo": 2}     | 3           | /foo           | {"foo": 3 }                                 |
	| {"foo": 2}     | 3           | /bar           | { "foo": 2, "bar": 3 }                      |
	| {"foo": 2}     | 3           | /bar/baz       | { "foo": 2, "bar": { "baz": 3} }            |
	| {"foo": 2}     | 3           | /bar/bash/bank | { "foo": 2, "bar": { "bash": {"bank": 3}} } |
	| {"foo": [{}] } | 3           | /foo/0/bar     | { "foo": [ {"bar": 3} ] }                   |
	| {"foo": [] }   | 3           | /foo/0/bar     | { "foo": [ {"bar": 3} ] }                   |
