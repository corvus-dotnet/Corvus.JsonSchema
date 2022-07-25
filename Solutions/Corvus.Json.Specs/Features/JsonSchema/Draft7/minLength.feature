@draft7

Feature: minLength draft7
    In order to use json-schema
    As a developer
    I want to support minLength in draft7

Scenario Outline: minLength validation
/* Schema: 
{"minLength": 2}
*/
    Given the input JSON file "minLength.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | longer is valid                                                                  |
        | #/000/tests/001/data | true  | exact length is valid                                                            |
        | #/000/tests/002/data | false | too short is invalid                                                             |
        | #/000/tests/003/data | true  | ignores non-strings                                                              |
        | #/000/tests/004/data | false | one supplementary Unicode code point is not long enough                          |
