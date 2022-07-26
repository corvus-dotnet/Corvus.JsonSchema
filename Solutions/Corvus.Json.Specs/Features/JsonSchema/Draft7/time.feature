@draft7

Feature: time draft7
    In order to use json-schema
    As a developer
    I want to support time in draft7

Scenario Outline: validation of time strings
/* Schema: 
{ "format": "time" }
*/
    Given the input JSON file "optional/format/time.json"
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
        | #/000/tests/006/data | true  | a valid time string                                                              |
        # #/000/tests/007/data | true  | a valid time string with leap second, Zulu                                       |
        | #/000/tests/008/data | false | invalid leap second, Zulu (wrong hour)                                           |
        | #/000/tests/009/data | false | invalid leap second, Zulu (wrong minute)                                         |
        # #/000/tests/010/data | true  | valid leap second, zero time-offset                                              |
        | #/000/tests/011/data | false | invalid leap second, zero time-offset (wrong hour)                               |
        | #/000/tests/012/data | false | invalid leap second, zero time-offset (wrong minute)                             |
        # #/000/tests/013/data | true  | valid leap second, positive time-offset                                          |
        # #/000/tests/014/data | true  | valid leap second, large positive time-offset                                    |
        | #/000/tests/015/data | false | invalid leap second, positive time-offset (wrong hour)                           |
        | #/000/tests/016/data | false | invalid leap second, positive time-offset (wrong minute)                         |
        # #/000/tests/017/data | true  | valid leap second, negative time-offset                                          |
        # #/000/tests/018/data | true  | valid leap second, large negative time-offset                                    |
        | #/000/tests/019/data | false | invalid leap second, negative time-offset (wrong hour)                           |
        | #/000/tests/020/data | false | invalid leap second, negative time-offset (wrong minute)                         |
        | #/000/tests/021/data | true  | a valid time string with second fraction                                         |
        | #/000/tests/022/data | true  | a valid time string with precise second fraction                                 |
        | #/000/tests/023/data | true  | a valid time string with plus offset                                             |
        | #/000/tests/024/data | true  | a valid time string with minus offset                                            |
        | #/000/tests/025/data | true  | a valid time string with case-insensitive Z                                      |
        | #/000/tests/026/data | false | an invalid time string with invalid hour                                         |
        | #/000/tests/027/data | false | an invalid time string with invalid minute                                       |
        | #/000/tests/028/data | false | an invalid time string with invalid second                                       |
        | #/000/tests/029/data | false | an invalid time string with invalid leap second (wrong hour)                     |
        | #/000/tests/030/data | false | an invalid time string with invalid leap second (wrong minute)                   |
        | #/000/tests/031/data | false | an invalid time string with invalid time numoffset hour                          |
        | #/000/tests/032/data | false | an invalid time string with invalid time numoffset minute                        |
        | #/000/tests/033/data | false | an invalid time string with invalid time with both Z and numoffset               |
        | #/000/tests/034/data | false | an invalid offset indicator                                                      |
        | #/000/tests/035/data | false | only RFC3339 not all of ISO 8601 are valid                                       |
        | #/000/tests/036/data | false | no time offset                                                                   |
        | #/000/tests/037/data | false | non-ascii digits should be rejected                                              |
