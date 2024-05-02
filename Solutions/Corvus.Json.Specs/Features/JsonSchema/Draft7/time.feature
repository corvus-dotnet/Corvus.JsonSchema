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
        # 08:30:06Z
        | #/000/tests/006/data | true  | a valid time string                                                              |
        # 008:030:006Z
        | #/000/tests/007/data | false | invalid time string with extra leading zeros                                     |
        # 8:3:6Z
        | #/000/tests/008/data | false | invalid time string with no leading zero for single digit                        |
        # 8:0030:6Z
        | #/000/tests/009/data | false | hour, minute, second must be two digits                                          |
        # 23:59:60Z
        # #/000/tests/010/data | true  | a valid time string with leap second, Zulu                                       |
        # 22:59:60Z
        | #/000/tests/011/data | false | invalid leap second, Zulu (wrong hour)                                           |
        # 23:58:60Z
        | #/000/tests/012/data | false | invalid leap second, Zulu (wrong minute)                                         |
        # 23:59:60+00:00
        # #/000/tests/013/data | true  | valid leap second, zero time-offset                                              |
        # 22:59:60+00:00
        | #/000/tests/014/data | false | invalid leap second, zero time-offset (wrong hour)                               |
        # 23:58:60+00:00
        | #/000/tests/015/data | false | invalid leap second, zero time-offset (wrong minute)                             |
        # 01:29:60+01:30
        # #/000/tests/016/data | true  | valid leap second, positive time-offset                                          |
        # 23:29:60+23:30
        # #/000/tests/017/data | true  | valid leap second, large positive time-offset                                    |
        # 23:59:60+01:00
        | #/000/tests/018/data | false | invalid leap second, positive time-offset (wrong hour)                           |
        # 23:59:60+00:30
        | #/000/tests/019/data | false | invalid leap second, positive time-offset (wrong minute)                         |
        # 15:59:60-08:00
        # #/000/tests/020/data | true  | valid leap second, negative time-offset                                          |
        # 00:29:60-23:30
        # #/000/tests/021/data | true  | valid leap second, large negative time-offset                                    |
        # 23:59:60-01:00
        | #/000/tests/022/data | false | invalid leap second, negative time-offset (wrong hour)                           |
        # 23:59:60-00:30
        | #/000/tests/023/data | false | invalid leap second, negative time-offset (wrong minute)                         |
        # 23:20:50.52Z
        | #/000/tests/024/data | true  | a valid time string with second fraction                                         |
        # 08:30:06.283185Z
        | #/000/tests/025/data | true  | a valid time string with precise second fraction                                 |
        # 08:30:06+00:20
        | #/000/tests/026/data | true  | a valid time string with plus offset                                             |
        # 08:30:06-08:00
        | #/000/tests/027/data | true  | a valid time string with minus offset                                            |
        # 08:30:06-8:000
        | #/000/tests/028/data | false | hour, minute in time-offset must be two digits                                   |
        # 08:30:06z
        | #/000/tests/029/data | true  | a valid time string with case-insensitive Z                                      |
        # 24:00:00Z
        | #/000/tests/030/data | false | an invalid time string with invalid hour                                         |
        # 00:60:00Z
        | #/000/tests/031/data | false | an invalid time string with invalid minute                                       |
        # 00:00:61Z
        | #/000/tests/032/data | false | an invalid time string with invalid second                                       |
        # 22:59:60Z
        | #/000/tests/033/data | false | an invalid time string with invalid leap second (wrong hour)                     |
        # 23:58:60Z
        | #/000/tests/034/data | false | an invalid time string with invalid leap second (wrong minute)                   |
        # 01:02:03+24:00
        | #/000/tests/035/data | false | an invalid time string with invalid time numoffset hour                          |
        # 01:02:03+00:60
        | #/000/tests/036/data | false | an invalid time string with invalid time numoffset minute                        |
        # 01:02:03Z+00:30
        | #/000/tests/037/data | false | an invalid time string with invalid time with both Z and numoffset               |
        # 08:30:06 PST
        | #/000/tests/038/data | false | an invalid offset indicator                                                      |
        # 01:01:01,1111
        | #/000/tests/039/data | false | only RFC3339 not all of ISO 8601 are valid                                       |
        # 12:00:00
        | #/000/tests/040/data | false | no time offset                                                                   |
        # 12:00:00.52
        | #/000/tests/041/data | false | no time offset with second fraction                                              |
        # 1২:00:00Z
        | #/000/tests/042/data | false | invalid non-ASCII '২' (a Bengali 2)                                              |
        # 08:30:06#00:20
        | #/000/tests/043/data | false | offset not starting with plus or minus                                           |
        # ab:cd:ef
        | #/000/tests/044/data | false | contains letters                                                                 |
