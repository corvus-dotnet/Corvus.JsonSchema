@openApi30

Feature: minItems openApi30
    In order to use json-schema
    As a developer
    I want to support minItems in openApi30

Scenario Outline: minItems validation
/* Schema: 
{"minItems": 1}
*/
    Given the input JSON file "minItems.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # [1, 2]
        | #/000/tests/000/data | true  | longer is valid                                                                  |
        # [1]
        | #/000/tests/001/data | true  | exact length is valid                                                            |
        # []
        | #/000/tests/002/data | false | too short is invalid                                                             |
        # 
        | #/000/tests/003/data | true  | ignores non-arrays                                                               |
