@draft2020-12

Feature: corvus nullable properties code generation draft2020-12
    In order to use json-schema
    As a developer
    I want to support generating optional properties as nullable types

Scenario Outline: Create nullable optional properties
	Given a schema file
		"""
		{
			"type": "object",
			"required": ["foo"],
			"properties": {
				"foo": { "type": "string" },
				"bar": { "type": ["string", "null"] }
			}
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema with optional properties nullable
	When I construct an instance of the schema type from the data
	Then the property 'Foo' from the instance has the value '<fooValue>'
	And the nullable property 'Bar' from the instance has the value '<barValue>'

Examples:
	| inputData                   | fooValue | barValue  |
	| {"foo": "p1", "bar": "p2" } | p1       | p2        |
	| {"foo": "p1", "bar": null } | p1       | null      |
	| {"foo": "p1" }              | p1       | null      |

	Scenario Outline: Create not nullable optional properties
	Given a schema file
		"""
		{
			"type": "object",
			"required": ["foo"],
			"properties": {
				"foo": { "type": "string" },
				"bar": { "type": ["string", "null"] }
			}
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema with optional properties not nullable
	When I construct an instance of the schema type from the data
	Then the property 'Foo' from the instance has the value '<fooValue>'
	And the nullable property 'Bar' from the instance has the value '<barValue>'

Examples:
	| inputData                   | fooValue | barValue  |
	| {"foo": "p1", "bar": "p2" } | p1       | p2        |
	| {"foo": "p1", "bar": null } | p1       | Null      |
	| {"foo": "p1" }              | p1       | Undefined |