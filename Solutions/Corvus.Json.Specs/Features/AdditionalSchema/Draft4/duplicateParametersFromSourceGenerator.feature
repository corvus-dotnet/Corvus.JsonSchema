@draft4

Feature: Duplication parameters from source generator #547
 
Scenario: A schema that produces duplicate documentation
	Given a schema file
		"""
		{
		  "$schema": "http://json-schema.org/draft-04/schema#",
		  "type": "object",
		  "properties": {
		    "oauth2DevicePollingInterval": {
		      "format": "int32",
		      "type": "integer",
		      "minimum": -2147483648,
		      "maximum": 2147483647,
		      "title": "OAuth 2.0 Device Polling Interval",
		      "description": "The minimum amount of time in seconds that the client should wait between polling requests to the token endpoint."
		    },
		    "oAuth2DevicePollingInterval": {
		      "format": "int32",
		      "type": "integer",
		      "minimum": -2147483648,
		      "maximum": 2147483647
		    }
		  },
		    "additionalProperties": false
		}
		"""
	And the input data value <inputData>
	And I generate a type for the schema
	And I construct an instance of the schema type from the data
	When I validate the instance
	Then the result will be <valid>

Examples:
	| inputData                          | valid |
	| {"oauth2DevicePollingInterval":33} | true  |
	| {"oAuth2DevicePollingInterval":33} | true  |
	| {"foo": 33}                        | false |
