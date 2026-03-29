Feature: NamespaceMapping
	In order to control the namespaces of generated types
	As a developer
	I want to map schema base URIs to .NET namespaces

Scenario Outline: Exact match namespace mapping
	Given I have a namespace map with "<baseUri>" mapped to "<namespace>"
	When I try to get the namespace for "<schemaUri>"
	Then the namespace should be "<expectedNamespace>"
	And the namespace lookup result should be <found>

	Examples:
		| baseUri                                     | namespace | schemaUri                                   | expectedNamespace | found |
		| https://example.com/schemas/                | Example   | https://example.com/schemas/                | Example           | true  |
		| https://example.com/schemas/person.json    | Person    | https://example.com/schemas/person.json     | Person            | true  |
		| https://example.com/schemas/               | Example   | https://example.com/other/                  |                   | false |

Scenario Outline: Prefix match namespace mapping
	Given I have a namespace map with "<baseUri>" mapped to "<namespace>"
	When I try to get the namespace for "<schemaUri>"
	Then the namespace should be "<expectedNamespace>"
	And the namespace lookup result should be <found>

	Examples:
		| baseUri                                        | namespace | schemaUri                                                | expectedNamespace | found |
		| https://myschema.io/contracts/v2/messages      | Messages  | https://myschema.io/contracts/v2/messages/helloWorld.yml | Messages          | true  |
		| https://example.com/schemas/                   | Example   | https://example.com/schemas/person.json                  | Example           | true  |
		| https://example.com/schemas/                   | Example   | https://example.com/schemas/nested/deep/type.json       | Example           | true  |
		| https://example.com/                           | Root      | https://example.com/schemas/type.json                    | Root              | true  |

Scenario Outline: Longest prefix wins when multiple mappings match
	Given I have a namespace map with the following entries
		| BaseUri                          | Namespace |
		| https://example.com/             | Root      |
		| https://example.com/schemas/     | Schemas   |
		| https://example.com/schemas/v2/  | SchemasV2 |
	When I try to get the namespace for "<schemaUri>"
	Then the namespace should be "<expectedNamespace>"
	And the namespace lookup result should be <found>

	Examples:
		| schemaUri                                    | expectedNamespace | found |
		| https://example.com/other/type.json         | Root              | true  |
		| https://example.com/schemas/type.json       | Schemas           | true  |
		| https://example.com/schemas/v2/type.json    | SchemasV2         | true  |
		| https://example.com/schemas/v2/nested/a.json| SchemasV2         | true  |
		| https://other.com/schemas/type.json         |                   | false |

Scenario: Non-absolute URI returns false
	Given I have a namespace map with "https://example.com/" mapped to "Example"
	When I try to get the namespace for a relative URI "schemas/type.json"
	Then the namespace lookup result should be false
