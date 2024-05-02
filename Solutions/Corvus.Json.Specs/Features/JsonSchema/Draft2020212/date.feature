@draft2020-12

Feature: date draft2020-12
    In order to use json-schema
    As a developer
    I want to support date in draft2020-12

Scenario Outline: validation of date strings
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "date"
        }
*/
    Given the input JSON file "optional/format/date.json"
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
        # 1963-06-19
        | #/000/tests/006/data | true  | a valid date string                                                              |
        # 2020-01-31
        | #/000/tests/007/data | true  | a valid date string with 31 days in January                                      |
        # 2020-01-32
        | #/000/tests/008/data | false | a invalid date string with 32 days in January                                    |
        # 2021-02-28
        | #/000/tests/009/data | true  | a valid date string with 28 days in February (normal)                            |
        # 2021-02-29
        | #/000/tests/010/data | false | a invalid date string with 29 days in February (normal)                          |
        # 2020-02-29
        | #/000/tests/011/data | true  | a valid date string with 29 days in February (leap)                              |
        # 2020-02-30
        | #/000/tests/012/data | false | a invalid date string with 30 days in February (leap)                            |
        # 2020-03-31
        | #/000/tests/013/data | true  | a valid date string with 31 days in March                                        |
        # 2020-03-32
        | #/000/tests/014/data | false | a invalid date string with 32 days in March                                      |
        # 2020-04-30
        | #/000/tests/015/data | true  | a valid date string with 30 days in April                                        |
        # 2020-04-31
        | #/000/tests/016/data | false | a invalid date string with 31 days in April                                      |
        # 2020-05-31
        | #/000/tests/017/data | true  | a valid date string with 31 days in May                                          |
        # 2020-05-32
        | #/000/tests/018/data | false | a invalid date string with 32 days in May                                        |
        # 2020-06-30
        | #/000/tests/019/data | true  | a valid date string with 30 days in June                                         |
        # 2020-06-31
        | #/000/tests/020/data | false | a invalid date string with 31 days in June                                       |
        # 2020-07-31
        | #/000/tests/021/data | true  | a valid date string with 31 days in July                                         |
        # 2020-07-32
        | #/000/tests/022/data | false | a invalid date string with 32 days in July                                       |
        # 2020-08-31
        | #/000/tests/023/data | true  | a valid date string with 31 days in August                                       |
        # 2020-08-32
        | #/000/tests/024/data | false | a invalid date string with 32 days in August                                     |
        # 2020-09-30
        | #/000/tests/025/data | true  | a valid date string with 30 days in September                                    |
        # 2020-09-31
        | #/000/tests/026/data | false | a invalid date string with 31 days in September                                  |
        # 2020-10-31
        | #/000/tests/027/data | true  | a valid date string with 31 days in October                                      |
        # 2020-10-32
        | #/000/tests/028/data | false | a invalid date string with 32 days in October                                    |
        # 2020-11-30
        | #/000/tests/029/data | true  | a valid date string with 30 days in November                                     |
        # 2020-11-31
        | #/000/tests/030/data | false | a invalid date string with 31 days in November                                   |
        # 2020-12-31
        | #/000/tests/031/data | true  | a valid date string with 31 days in December                                     |
        # 2020-12-32
        | #/000/tests/032/data | false | a invalid date string with 32 days in December                                   |
        # 2020-13-01
        | #/000/tests/033/data | false | a invalid date string with invalid month                                         |
        # 06/19/1963
        | #/000/tests/034/data | false | an invalid date string                                                           |
        # 2013-350
        | #/000/tests/035/data | false | only RFC3339 not all of ISO 8601 are valid                                       |
        # 1998-1-20
        | #/000/tests/036/data | false | non-padded month dates are not valid                                             |
        # 1998-01-1
        | #/000/tests/037/data | false | non-padded day dates are not valid                                               |
        # 1998-13-01
        | #/000/tests/038/data | false | invalid month                                                                    |
        # 1998-04-31
        | #/000/tests/039/data | false | invalid month-day combination                                                    |
        # 2021-02-29
        | #/000/tests/040/data | false | 2021 is not a leap year                                                          |
        # 2020-02-29
        | #/000/tests/041/data | true  | 2020 is a leap year                                                              |
        # 1963-06-1৪
        | #/000/tests/042/data | false | invalid non-ASCII '৪' (a Bengali 4)                                              |
        # 20230328
        | #/000/tests/043/data | false | ISO8601 / non-RFC3339: YYYYMMDD without dashes (2023-03-28)                      |
        # 2023-W01
        | #/000/tests/044/data | false | ISO8601 / non-RFC3339: week number implicit day of week (2023-01-02)             |
        # 2023-W13-2
        | #/000/tests/045/data | false | ISO8601 / non-RFC3339: week number with day of week (2023-03-28)                 |
        # 2022W527
        | #/000/tests/046/data | false | ISO8601 / non-RFC3339: week number rollover to next year (2023-01-01)            |
