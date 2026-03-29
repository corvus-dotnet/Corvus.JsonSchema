@draft2020-12

Feature: Unknown content encoding keyword draft2020-12

Scenario Outline: reference of an arbitrary keyword of a sub-schema
	Given a schema file
	"""
	{
		"$schema": "https://json-schema.org/draft/2020-12/schema",
		"type": "string",
		"contentEncoding": "bech32",
		"pattern": "^(stake|stake_test)1[0-9a-z]*$"
	}
	"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                                     | valid |
	| "stake179kzq4qulejydh045yzxwk4ksx780khkl4gdve9kzwd9vjcek9u8h" | true  |
	| "kzq4qulejydh045yzxwk4ksx780khkl4gdve9kzwd9vjcek9u8h"         | false |
