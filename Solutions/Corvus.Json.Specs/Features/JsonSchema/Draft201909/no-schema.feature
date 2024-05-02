@draft2019-09

Feature: no-schema draft2019-09
    In order to use json-schema
    As a developer
    I want to support no-schema in draft2019-09

Scenario Outline: validation without $schema
/* Schema: 
{
            "minLength": 2
        }
*/
    Given the input JSON file "optional/no-schema.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # foo
        | #/000/tests/000/data | true  | a 3-character string is valid                                                    |
        # a
        | #/000/tests/001/data | false | a 1-character string is not valid                                                |
        # 5
        | #/000/tests/002/data | true  | a non-string is valid                                                            |
