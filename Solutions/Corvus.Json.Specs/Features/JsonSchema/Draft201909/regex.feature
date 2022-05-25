@draft2019-09

Feature: regex draft2019-09
    In order to use json-schema
    As a developer
    I want to support regex in draft2019-09

Scenario Outline: validation of regular expressions
/* Schema: 
{ "format": "regex" }
*/
    Given the input JSON file "optional/format/regex.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | all string formats ignore integers                                               |
        | #/000/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/000/tests/002/data | true  | all string formats ignore objects                                                |
        | #/000/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/000/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/000/tests/005/data | true  | all string formats ignore nulls                                                  |
        | #/000/tests/006/data | true  | a valid regular expression                                                       |
        | #/000/tests/007/data | false | a regular expression with unclosed parens is invalid                             |
