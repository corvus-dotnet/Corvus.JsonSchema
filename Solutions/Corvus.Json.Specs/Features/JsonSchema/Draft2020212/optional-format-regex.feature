@draft2020-12

Feature: optional-format-regex draft2020-12
    In order to use json-schema
    As a developer
    I want to support optional-format-regex in draft2020-12

Scenario Outline: validation of regular expressions
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "regex"
        }
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
        # 12
        | #/000/tests/000/data | true  | all string formats ignore integers                                               |
        # 13.7
        | #/000/tests/001/data | true  | all string formats ignore floats                                                 |
        # {}
        | #/000/tests/002/data | true  | all string formats ignore objects                                                |
        # []
        | #/000/tests/003/data | true  | all string formats ignore arrays                                                 |
        # False
        | #/000/tests/004/data | true  | all string formats ignore booleans                                               |
        # 
        | #/000/tests/005/data | true  | all string formats ignore nulls                                                  |
        # ([abc])+\s+$
        | #/000/tests/006/data | true  | a valid regular expression                                                       |
        # ^(abc]
        | #/000/tests/007/data | false | a regular expression with unclosed parens is invalid                             |
