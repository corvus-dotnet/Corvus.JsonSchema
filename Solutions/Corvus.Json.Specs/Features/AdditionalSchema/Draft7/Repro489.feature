@draft7

Feature: Repro 489 draft7

Scenario Outline: Generation error with multiple pattern properties.
	Given a schema file
		"""
		{
			"$schema": "https://json-schema.org/draft-07/schema",
			"type": "object",
			"patternProperties": {
				"^(?!.*?\\$schema).*$": {
					"type": "object",
					"patternProperties": {
						".*_doc$|^doc$": {
							"type": "string"
						},
						".*_code$|^code$": {
							"type": "string"
						}
					}
				}
			}
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                                                                                                                                                                                                                                                | valid |
	| { "foo": { "a_doc": "aaa", "b_doc": "bbb" } } | true  |

	Scenario Outline: Generation error with single pattern properties.
	Given a schema file
		"""
		{
			"$schema": "https://json-schema.org/draft-07/schema",
			"type": "object",
			"patternProperties": {
				".*_doc$|^doc$": {
					"type": "string"
				}
			}
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                                                                                                                                                                                                                                                | valid |
	| { "a_doc": "aaa", "b_doc": "bbb" } | true  |