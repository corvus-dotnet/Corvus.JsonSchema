@draft202012

Feature: Repro 526 draft202012

Scenario Outline: Base64 encoded application/octet-stream.
	Given a schema file
		"""
		{
		  "$schema": "https://json-schema.org/draft/2020-12/schema",
		  "type": "object",
		  "properties": {
			"file": {
			  "type": "object",
			  "properties": {
				"content": {
				  "type": "string",
				  "contentEncoding": "base64",
				  "contentMediaType": "application/octet-stream"
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
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                                         | valid |
	| { "file": { "content": "eyJmb28iOiAiYmFyIn0K" } } | true  |
	# Invalid items validate successfully even when asserted, and fail during content decoding checks.
	| { "file": { "content": "eyJib28iOiAyMH0=" } }     | true  |
