@draft4

Feature: optional-zeroTerminatedFloats draft4
    In order to use json-schema
    As a developer
    I want to support optional-zeroTerminatedFloats in draft4

Scenario Outline: some languages do not distinguish between different types of numeric value
/* Schema: 
{
            "type": "integer"
        }
*/
    Given the input JSON file "optional/zeroTerminatedFloats.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # 1.0
        | #/000/tests/000/data | false | a float is not an integer even without fractional part                           |
