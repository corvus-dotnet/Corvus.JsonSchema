@draft4

Feature: optional-float-overflow draft4
    In order to use json-schema
    As a developer
    I want to support optional-float-overflow in draft4

Scenario Outline: all integers are multiples of 0.5, if overflow is handled
/* Schema: 
{"type": "number", "multipleOf": 0.5}
*/
    Given the input JSON file "optional/float-overflow.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1e308
        | #/000/tests/000/data | true  | valid if optional overflow handling is implemented                               |
