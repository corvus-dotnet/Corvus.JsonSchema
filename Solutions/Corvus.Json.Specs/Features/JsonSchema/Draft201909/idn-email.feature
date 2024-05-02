@draft2019-09

Feature: idn-email draft2019-09
    In order to use json-schema
    As a developer
    I want to support idn-email in draft2019-09

Scenario Outline: validation of an internationalized e-mail addresses
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "format": "idn-email"
        }
*/
    Given the input JSON file "optional/format/idn-email.json"
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
        # 실례@실례.테스트
        | #/000/tests/006/data | true  | a valid idn e-mail (example@example.test in Hangul)                              |
        # 2962
        | #/000/tests/007/data | false | an invalid idn e-mail address                                                    |
        # joe.bloggs@example.com
        | #/000/tests/008/data | true  | a valid e-mail address                                                           |
        # 2962
        | #/000/tests/009/data | false | an invalid e-mail address                                                        |
