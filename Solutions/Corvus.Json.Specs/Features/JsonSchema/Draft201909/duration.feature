@draft2019-09

Feature: duration draft2019-09
    In order to use json-schema
    As a developer
    I want to support duration in draft2019-09

Scenario Outline: validation of duration strings
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "format": "duration"
        }
*/
    Given the input JSON file "optional/format/duration.json"
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
        | #/000/tests/006/data | true  | a valid duration string                                                          |
        | #/000/tests/007/data | false | an invalid duration string                                                       |
        | #/000/tests/008/data | false | no elements present                                                              |
        | #/000/tests/009/data | false | no time elements present                                                         |
        | #/000/tests/010/data | false | no date or time elements present                                                 |
        | #/000/tests/011/data | false | elements out of order                                                            |
        | #/000/tests/012/data | false | missing time separator                                                           |
        | #/000/tests/013/data | false | time element in the date position                                                |
        | #/000/tests/014/data | true  | four years duration                                                              |
        | #/000/tests/015/data | true  | zero time, in seconds                                                            |
        | #/000/tests/016/data | true  | zero time, in days                                                               |
        | #/000/tests/017/data | true  | one month duration                                                               |
        | #/000/tests/018/data | true  | one minute duration                                                              |
        | #/000/tests/019/data | true  | one and a half days, in hours                                                    |
        | #/000/tests/020/data | true  | one and a half days, in days and hours                                           |
        | #/000/tests/021/data | true  | two weeks                                                                        |
        | #/000/tests/022/data | false | weeks cannot be combined with other units                                        |
        | #/000/tests/023/data | false | invalid non-ASCII '২' (a Bengali 2)                                              |
