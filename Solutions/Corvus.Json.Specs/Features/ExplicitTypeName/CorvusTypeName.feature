@draft2020-12

Feature: corvus type name code generation draft2020-12
    In order to use json-schema
    As a developer
    I want to support explicit type name hints in draft2020-12

Scenario Outline: corvus type name
	Given a schema file
		"""
		{
			"type": "array",
			"prefixItems": [
				{
					"$corvusTypeName": "PositiveInt32",
					"type": "integer",
					"format": "int32",
					"minimum": 1
				},
				{ "type": "string" },
				{
					"type": "string",
					"format": "date-time"
				}
			],
			"unevaluatedItems": false
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                | valid |
	| [1, "hello", "2012-04-23T18:25:43.511Z"] | true  |
	| [0, "hello", "2012-04-23T18:25:43.511Z"] | false |

Scenario Outline: invalid corvus type name is ignored
	Given a schema file
		"""
		{
			"type": "array",
			"prefixItems": [
				{
					"$corvusTypeName": "",
					"type": "integer",
					"format": "int32",
					"minimum": 1
				},
				{ "type": "string" },
				{
					"type": "string",
					"format": "date-time"
				}
			],
			"unevaluatedItems": false
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                | valid |
	| [1, "hello", "2012-04-23T18:25:43.511Z"] | true  |
	| [0, "hello", "2012-04-23T18:25:43.511Z"] | false |
