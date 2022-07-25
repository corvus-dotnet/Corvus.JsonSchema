@draft2019-09

Feature: uuid draft2019-09
    In order to use json-schema
    As a developer
    I want to support uuid in draft2019-09

Scenario Outline: uuid format
/* Schema: 
{
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
        | #/000/tests/000/data | true  | all upper-case                                                                   |
        | #/000/tests/001/data | true  | all lower-case                                                                   |
        | #/000/tests/002/data | true  | mixed case                                                                       |
        | #/000/tests/003/data | true  | all zeroes is valid                                                              |
        | #/000/tests/004/data | false | wrong length                                                                     |
        | #/000/tests/005/data | false | missing section                                                                  |
        | #/000/tests/006/data | false | bad characters (not hex)                                                         |
        | #/000/tests/007/data | false | no dashes                                                                        |
        | #/000/tests/008/data | false | too few dashes                                                                   |
        | #/000/tests/009/data | false | too many dashes                                                                  |
        | #/000/tests/010/data | false | dashes in the wrong spot                                                         |
        | #/000/tests/011/data | true  | valid version 4                                                                  |
        | #/000/tests/012/data | true  | valid version 5                                                                  |
        | #/000/tests/013/data | true  | hypothetical version 6                                                           |
        | #/000/tests/014/data | true  | hypothetical version 15                                                          |
