@draft202012
@

Feature: Repro 633 draft202012

Scenario Outline: Validate required property defined in reference
	Given a schema file
		"""
		{
			"$schema": "https://json-schema.org/draft/2020-12/schema",
			"type": "object",
			"$ref": "#/$defs/ifThisThenThat",
			"required": [
				"match"
			],
			"maxProperties": 1,
			"$defs": {
				"ifThisThenThat": {
					"type": "object",
					"properties": {
						"match": {
							"type": "string"
						}
					},
					"unevaluatedProperties": {
						"type": "number"
					}
				}
			}
		}
		"""
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level Detailed
	Then the result will be <valid>

Examples:
	| inputData        | valid |
	| {"match": "foo"} | true  |
	| {}               | false |


Scenario Outline: Validate required property required in reference
	Given a schema file
		"""
		{
			"$schema": "https://json-schema.org/draft/2020-12/schema",
			"type": "object",
			"$ref": "#/$defs/ifThisThenThat",
			"maxProperties": 1,
			"$defs": {
				"ifThisThenThat": {
					"type": "object",
					"required": [
						"match"
					],
					"properties": {
						"match": {
							"type": "string"
						}
					},
					"unevaluatedProperties": {
						"type": "number"
					}
				}
			}
		}
		"""
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level Detailed
	Then the result will be <valid>

Examples:
	| inputData        | valid |
	| {"match": "foo"} | true  |
	| {}               | false |


Scenario Outline: Validate overrides
	Given a schema file
		"""
		{
			"$schema": "https://json-schema.org/draft/2020-12/schema",
			"type": "object",
			"$ref": "#/$defs/ifThisThenThat",
			"properties": {
				"match": {
					"minimum": 3
				}
			},
			"$defs": {
				"ifThisThenThat": {
					"type": "object",
					"properties": {
						"match": {
							"minimum": 2
						}
					}
				}
			}
		}
		"""
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level Detailed
	Then the result will be <valid>

Examples:
	| inputData     | valid |
	| {"match": 3 } | true  |
	| {"match": 2 } | false |


Scenario Outline: Validate reverse overrides
	Given a schema file
		"""
		{
			"$schema": "https://json-schema.org/draft/2020-12/schema",
			"type": "object",
			"$ref": "#/$defs/ifThisThenThat",
			"properties": {
				"match": {
					"minimum": 2
				}
			},
			"$defs": {
				"ifThisThenThat": {
					"type": "object",
					"properties": {
						"match": {
							"minimum": 3
						}
					}
				}
			}
		}
		"""
	And the input data value <inputData>
	And I assert format
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance with level Detailed
	Then the result will be <valid>

Examples:
	| inputData     | valid |
	| {"match": 3 } | true  |
	| {"match": 2 } | false |