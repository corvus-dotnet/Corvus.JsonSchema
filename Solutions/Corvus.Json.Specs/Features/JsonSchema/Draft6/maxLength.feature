@draft6

Feature: maxLength draft6
    In order to use json-schema
    As a developer
    I want to support maxLength in draft6

Scenario Outline: maxLength validation
/* Schema: 
{"maxLength": 2}
*/
    Given the input JSON file "maxLength.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | shorter is valid                                                                 |
        | #/000/tests/001/data | true  | exact length is valid                                                            |
        | #/000/tests/002/data | false | too long is invalid                                                              |
        | #/000/tests/003/data | true  | ignores non-strings                                                              |
        | #/000/tests/004/data | true  | two supplementary Unicode code points is long enough                             |
