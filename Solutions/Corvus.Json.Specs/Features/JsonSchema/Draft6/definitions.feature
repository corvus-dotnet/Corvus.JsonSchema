@draft6

Feature: definitions draft6
    In order to use json-schema
    As a developer
    I want to support definitions in draft6

Scenario Outline: validate definition against metaschema
/* Schema: 
{"$ref": "http://json-schema.org/draft-06/schema#"}
*/
    Given the input JSON file "definitions.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # { "definitions": { "foo": {"type": "integer"} } }
        | #/000/tests/000/data | true  | valid definition schema                                                          |
        # { "definitions": { "foo": {"type": 1} } }
        | #/000/tests/001/data | false | invalid definition schema                                                        |
