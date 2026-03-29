@draft2019-09

Feature: Path-like unknown keyword draft2019-09

Scenario Outline: reference of an arbitrary keyword of a sub-schema
	Given a schema file
	"""
	{
		"$schema": "https://json-schema.org/draft/2019-09/schema",
		"properties": {
			"foo": {"path/like/keyword": {"type": "integer"}},
			"bar": {"$ref": "#/properties/foo/path~1like~1keyword"}
		}
	}
	"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData        | valid |
	| {"bar": 3}       | true  |
	| {"bar": "hello"} | false |
