@draft2020-12

Feature: regex draft2020-12
    In order to use json-schema
    As a developer
    I want to support regex in draft2020-12

Scenario Outline: validation of regular expressions
/* Schema: 
{"format": "regex"}
*/
    Given the input JSON file "regex.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid regular expression                                                       |
        | #/000/tests/001/data | false | a regular expression with unclosed parens is invalid                             |
