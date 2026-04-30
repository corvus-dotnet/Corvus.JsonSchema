@draft7

Feature: Repro620 Nested anyOf draft7

Scenario Outline: Repro 620 - names collide in Is and As Matchers
	Given a schema file
		"""
		{
			"$schema": "http://json-schema.org/draft-07/schema",
			"type": "object",
			"anyOf": [
				{
					"type": "object",
					"required": [
						"kind",
						"data"
					],
					"additionalProperties": false,
					"properties": {
						"kind": {
							"const": "foo"
						},
						"data": {
							"anyOf": [
								{
									"type": "object",
									"required": [
										"kind"
									],
									"additionalProperties": false,
									"properties": {
										"kind": {
											"const": "bar"
										}
									}
								},
								{
									"type": "object",
									"required": [
										"kind"
									],
									"additionalProperties": false,
									"properties": {
										"kind": {
											"const": "baz"
										}
									}
								}
							]
						}
					}
				},
				{
					"$ref": "#/anyOf/0/properties/data/anyOf/0"
				}
			]
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                | valid |
	| {"kind": "foo", "data": {"kind": "bar"} } | true  |
	| {"kind": "foo", "data": {"kind": "bat"} } | false |
