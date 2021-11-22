@draft2019-09

Feature: minimum draft2019-09
    In order to use json-schema
    As a developer
    I want to support minimum in draft2019-09

Scenario Outline: minimum validation
/* Schema: 
{"minimum": 1.1}
*/
    Given the input JSON file "minimum.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | above the minimum is valid                                                       |
        | #/000/tests/001/data | true  | boundary point is valid                                                          |
        | #/000/tests/002/data | false | below the minimum is invalid                                                     |
        | #/000/tests/003/data | true  | ignores non-numbers                                                              |

Scenario Outline: minimum validation with signed integer
/* Schema: 
{"minimum": -2}
*/
    Given the input JSON file "minimum.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/001/tests/000/data | true  | negative above the minimum is valid                                              |
        | #/001/tests/001/data | true  | positive above the minimum is valid                                              |
        | #/001/tests/002/data | true  | boundary point is valid                                                          |
        | #/001/tests/003/data | true  | boundary point with float is valid                                               |
        | #/001/tests/004/data | false | float below the minimum is invalid                                               |
        | #/001/tests/005/data | false | int below the minimum is invalid                                                 |
        | #/001/tests/006/data | true  | ignores non-numbers                                                              |
