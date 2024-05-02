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
        # 2EB8AA08-AA98-11EA-B4AA-73B441D16380
        | #/000/tests/006/data | true  | all upper-case                                                                   |
        # 2eb8aa08-aa98-11ea-b4aa-73b441d16380
        | #/000/tests/007/data | true  | all lower-case                                                                   |
        # 2eb8aa08-AA98-11ea-B4Aa-73B441D16380
        | #/000/tests/008/data | true  | mixed case                                                                       |
        # 00000000-0000-0000-0000-000000000000
        | #/000/tests/009/data | true  | all zeroes is valid                                                              |
        # 2eb8aa08-aa98-11ea-b4aa-73b441d1638
        | #/000/tests/010/data | false | wrong length                                                                     |
        # 2eb8aa08-aa98-11ea-73b441d16380
        | #/000/tests/011/data | false | missing section                                                                  |
        # 2eb8aa08-aa98-11ea-b4ga-73b441d16380
        | #/000/tests/012/data | false | bad characters (not hex)                                                         |
        # 2eb8aa08aa9811eab4aa73b441d16380
        | #/000/tests/013/data | false | no dashes                                                                        |
        # 2eb8aa08aa98-11ea-b4aa73b441d16380
        | #/000/tests/014/data | false | too few dashes                                                                   |
        # 2eb8-aa08-aa98-11ea-b4aa73b44-1d16380
        | #/000/tests/015/data | false | too many dashes                                                                  |
        # 2eb8aa08aa9811eab4aa73b441d16380----
        | #/000/tests/016/data | false | dashes in the wrong spot                                                         |
        # 98d80576-482e-427f-8434-7f86890ab222
        | #/000/tests/017/data | true  | valid version 4                                                                  |
        # 99c17cbb-656f-564a-940f-1a4568f03487
        | #/000/tests/018/data | true  | valid version 5                                                                  |
        # 99c17cbb-656f-664a-940f-1a4568f03487
        | #/000/tests/019/data | true  | hypothetical version 6                                                           |
        # 99c17cbb-656f-f64a-940f-1a4568f03487
        | #/000/tests/020/data | true  | hypothetical version 15                                                          |
