@draft7

Feature: date-time draft7
    In order to use json-schema
    As a developer
    I want to support date-time in draft7

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
        # 1963-06-19T08:30:06.283185Z
        | #/000/tests/006/data | true  | a valid date-time string                                                         |
        # 1963-06-19T08:30:06Z
        | #/000/tests/007/data | true  | a valid date-time string without second fraction                                 |
        # 1937-01-01T12:00:27.87+00:20
        | #/000/tests/008/data | true  | a valid date-time string with plus offset                                        |
        # 1990-12-31T15:59:50.123-08:00
        | #/000/tests/009/data | true  | a valid date-time string with minus offset                                       |
        # 1998-12-31T23:59:60Z
        # #/000/tests/010/data | true  | a valid date-time with a leap second, UTC                                        |
        # 1998-12-31T15:59:60.123-08:00
        # #/000/tests/011/data | true  | a valid date-time with a leap second, with minus offset                          |
        # 1998-12-31T23:59:61Z
        | #/000/tests/012/data | false | an invalid date-time past leap second, UTC                                       |
        # 1998-12-31T23:58:60Z
        | #/000/tests/013/data | false | an invalid date-time with leap second on a wrong minute, UTC                     |
        # 1998-12-31T22:59:60Z
        | #/000/tests/014/data | false | an invalid date-time with leap second on a wrong hour, UTC                       |
        # 1990-02-31T15:59:59.123-08:00
        | #/000/tests/015/data | false | an invalid day in date-time string                                               |
        # 1990-12-31T15:59:59-24:00
        | #/000/tests/016/data | false | an invalid offset in date-time string                                            |
        # 1963-06-19T08:30:06.28123+01:00Z
        | #/000/tests/017/data | false | an invalid closing Z after time-zone offset                                      |
        # 06/19/1963 08:30:06 PST
        | #/000/tests/018/data | false | an invalid date-time string                                                      |
        # 1963-06-19t08:30:06.283185z
        | #/000/tests/019/data | true  | case-insensitive T and Z                                                         |
        # 2013-350T01:01:01
        | #/000/tests/020/data | false | only RFC3339 not all of ISO 8601 are valid                                       |
        # 1963-6-19T08:30:06.283185Z
        | #/000/tests/021/data | false | invalid non-padded month dates                                                   |
        # 1963-06-1T08:30:06.283185Z
        | #/000/tests/022/data | false | invalid non-padded day dates                                                     |
        # 1963-06-1৪T00:00:00Z
        | #/000/tests/023/data | false | invalid non-ASCII '৪' (a Bengali 4) in date portion                              |
        # 1963-06-11T0৪:00:00Z
        | #/000/tests/024/data | false | invalid non-ASCII '৪' (a Bengali 4) in time portion                              |
