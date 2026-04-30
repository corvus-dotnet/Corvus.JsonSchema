@draft2020-12

Feature: optional-format-unknown draft2020-12
    In order to use json-schema
    As a developer
    I want to support optional-format-unknown in draft2020-12

Scenario Outline: unknown format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "unknown"
        }
*/
    Given the input JSON file "optional/format/unknown.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 12
        | #/000/tests/000/data | true  | unknown formats ignore integers                                                  |
        # 13.7
        | #/000/tests/001/data | true  | unknown formats ignore floats                                                    |
        # {}
        | #/000/tests/002/data | true  | unknown formats ignore objects                                                   |
        # []
        | #/000/tests/003/data | true  | unknown formats ignore arrays                                                    |
        # False
        | #/000/tests/004/data | true  | unknown formats ignore booleans                                                  |
        # 
        | #/000/tests/005/data | true  | unknown formats ignore nulls                                                     |
        # string
        | #/000/tests/006/data | true  | unknown formats ignore strings                                                   |
