@draft7

Feature: unknown draft7
    In order to use json-schema
    As a developer
    I want to support unknown in draft7

Scenario Outline: unknown format
/* Schema: 
{ "format": "unknown" }
*/
    Given the input JSON file "optional/format/unknown.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
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
