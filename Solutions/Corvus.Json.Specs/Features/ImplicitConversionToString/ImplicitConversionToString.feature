Feature: Implicit conversion to string enabled

Scenario: Create implicit conversion to string
	Given a schema file
		"""
		{
			"type": "string",
			"minLength": 1
		}
		"""
	And the input data value "foo"
	And I generate a type for the schema with implicit conversion to string enabled
	And I construct an instance of the schema type from the data
	When I assign the instance to a string
	Then the assigned string will be "foo"

Scenario: Create implicit conversion to string with what would normally be a built-in string type
	Given a schema file
		"""
		{
			"type": "string"
		}
		"""
	And the input data value "foo"
	And I generate a type for the schema with implicit conversion to string enabled
	And I construct an instance of the schema type from the data
	When I assign the instance to a string
	Then the assigned string will be "foo"

Scenario: Create implicit conversion to string with what would normally be a built-in string format type
	Given a schema file
		"""
		{
			"type": "string",
			"format": "uuid"
		}
		"""
	And the input data value "3af1928d-96f7-4272-a057-beb6194579e6"
	And I generate a type for the schema with implicit conversion to string enabled
	And I construct an instance of the schema type from the data
	When I assign the instance to a string
	Then the assigned string will be "3af1928d-96f7-4272-a057-beb6194579e6"