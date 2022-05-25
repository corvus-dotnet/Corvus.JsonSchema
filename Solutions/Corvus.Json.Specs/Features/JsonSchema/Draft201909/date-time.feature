@draft2019-09

Feature: date-time draft2019-09
    In order to use json-schema
    As a developer
    I want to support date-time in draft2019-09

Scenario Outline: validation of date-time strings
/* Schema: 
{ "format": "date-time" }
*/
    Given the input JSON file "optional/format/date-time.json"
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
        | #/000/tests/006/data | true  | a valid date-time string                                                         |
        | #/000/tests/007/data | true  | a valid date-time string without second fraction                                 |
        | #/000/tests/008/data | true  | a valid date-time string with plus offset                                        |
        | #/000/tests/009/data | true  | a valid date-time string with minus offset                                       |
        # #/000/tests/010/data | true  | a valid date-time with a leap second, UTC                                        |
        # #/000/tests/011/data | true  | a valid date-time with a leap second, with minus offset                          |
        | #/000/tests/012/data | false | an invalid date-time past leap second, UTC                                       |
        | #/000/tests/013/data | false | an invalid date-time with leap second on a wrong minute, UTC                     |
        | #/000/tests/014/data | false | an invalid date-time with leap second on a wrong hour, UTC                       |
        | #/000/tests/015/data | false | an invalid day in date-time string                                               |
        | #/000/tests/016/data | false | an invalid offset in date-time string                                            |
        | #/000/tests/017/data | false | an invalid closing Z after time-zone offset                                      |
        | #/000/tests/018/data | false | an invalid date-time string                                                      |
        | #/000/tests/019/data | true  | case-insensitive T and Z                                                         |
        | #/000/tests/020/data | false | only RFC3339 not all of ISO 8601 are valid                                       |
        | #/000/tests/021/data | false | invalid non-padded month dates                                                   |
        | #/000/tests/022/data | false | invalid non-padded day dates                                                     |
        | #/000/tests/023/data | false | non-ascii digits should be rejected in the date portion                          |
        | #/000/tests/024/data | false | non-ascii digits should be rejected in the time portion                          |
