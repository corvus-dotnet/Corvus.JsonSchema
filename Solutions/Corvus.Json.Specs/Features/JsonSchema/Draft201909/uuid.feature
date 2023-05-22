@draft2019-09

Feature: uuid draft2019-09
    In order to use json-schema
    As a developer
    I want to support uuid in draft2019-09

Scenario Outline: uuid format
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "format": "uuid"
        }
*/
    Given the input JSON file "optional/format/uuid.json"
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
        | #/000/tests/006/data | true  | all upper-case                                                                   |
        | #/000/tests/007/data | true  | all lower-case                                                                   |
        | #/000/tests/008/data | true  | mixed case                                                                       |
        | #/000/tests/009/data | true  | all zeroes is valid                                                              |
        | #/000/tests/010/data | false | wrong length                                                                     |
        | #/000/tests/011/data | false | missing section                                                                  |
        | #/000/tests/012/data | false | bad characters (not hex)                                                         |
        | #/000/tests/013/data | false | no dashes                                                                        |
        | #/000/tests/014/data | false | too few dashes                                                                   |
        | #/000/tests/015/data | false | too many dashes                                                                  |
        | #/000/tests/016/data | false | dashes in the wrong spot                                                         |
        | #/000/tests/017/data | true  | valid version 4                                                                  |
        | #/000/tests/018/data | true  | valid version 5                                                                  |
        | #/000/tests/019/data | true  | hypothetical version 6                                                           |
        | #/000/tests/020/data | true  | hypothetical version 15                                                          |
