@draft2020-12

Feature: float-overflow draft2020-12
    In order to use json-schema
    As a developer
    I want to support float-overflow in draft2020-12

Scenario Outline: all integers are multiples of 0.5, if overflow is handled
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "integer",
            "multipleOf": 0.5
        }
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
        | #/000/tests/000/data | true  | valid if optional overflow handling is implemented                               |
