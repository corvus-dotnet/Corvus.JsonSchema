@draft2020-12

Feature: optional-format-duration draft2020-12
    In order to use json-schema
    As a developer
    I want to support optional-format-duration in draft2020-12

Scenario Outline: validation of duration strings
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "duration"
        }
*/
    Given the input JSON file "optional/format/duration.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
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
        # P4DT12H30M5S
        | #/000/tests/006/data | true  | a valid duration string                                                          |
        # PT1D
        | #/000/tests/007/data | false | an invalid duration string                                                       |
        # P
        | #/000/tests/008/data | false | no elements present                                                              |
        # P1YT
        | #/000/tests/009/data | false | no time elements present                                                         |
        # PT
        | #/000/tests/010/data | false | no date or time elements present                                                 |
        # P2D1Y
        | #/000/tests/011/data | false | elements out of order                                                            |
        # P1D2H
        | #/000/tests/012/data | false | missing time separator                                                           |
        # P2S
        | #/000/tests/013/data | false | time element in the date position                                                |
        # P4Y
        | #/000/tests/014/data | true  | four years duration                                                              |
        # PT0S
        | #/000/tests/015/data | true  | zero time, in seconds                                                            |
        # P0D
        | #/000/tests/016/data | true  | zero time, in days                                                               |
        # P1M
        | #/000/tests/017/data | true  | one month duration                                                               |
        # PT1M
        | #/000/tests/018/data | true  | one minute duration                                                              |
        # PT36H
        | #/000/tests/019/data | true  | one and a half days, in hours                                                    |
        # P1DT12H
        | #/000/tests/020/data | true  | one and a half days, in days and hours                                           |
        # P2W
        | #/000/tests/021/data | true  | two weeks                                                                        |
        # P1Y2W
        | #/000/tests/022/data | false | weeks cannot be combined with other units                                        |
        # P২Y
        | #/000/tests/023/data | false | invalid non-ASCII '২' (a Bengali 2)                                              |
        # P1
        | #/000/tests/024/data | false | element without unit                                                             |
