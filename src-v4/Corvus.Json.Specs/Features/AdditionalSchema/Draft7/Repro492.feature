@draft7

Feature: Repro 492 draft7

Scenario Outline: Generation error with optional nullable and dependent schemas.
	Given a schema file
		"""
		{
			"$schema": "https://json-schema.org/draft-07/schema",
			"type": "object",
			"dependencies": {
				"recognition": { }
			}
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema with optional properties nullable
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                | valid |
	| { "recognition": "foo" } | true  |


Scenario Outline: Generation error with optional nullable and dependent schemas non-JsonAny
	Given a schema file
		"""
		{
			"$schema": "https://json-schema.org/draft-07/schema",
			"type": "object",
			"dependencies": {
				"recognition": { "type": "object", "required": ["bar"], "properties": { "bar": {"type": "string" } } }
			}
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema with optional properties nullable
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                | valid |
	| { "recognition": "foo", "bar": "there" } | true  |
	| { "recognition": "foo" }                 | false |

