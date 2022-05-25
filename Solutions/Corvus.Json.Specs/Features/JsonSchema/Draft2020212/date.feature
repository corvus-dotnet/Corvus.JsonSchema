@draft2020-12

Feature: date draft2020-12
    In order to use json-schema
    As a developer
    I want to support date in draft2020-12

Scenario Outline: validation of date strings
/* Schema: 
{ "format": "date" }
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
        | #/000/tests/000/data | true  | all string formats ignore integers                                               |
        | #/000/tests/001/data | true  | all string formats ignore floats                                                 |
        | #/000/tests/002/data | true  | all string formats ignore objects                                                |
        | #/000/tests/003/data | true  | all string formats ignore arrays                                                 |
        | #/000/tests/004/data | true  | all string formats ignore booleans                                               |
        | #/000/tests/005/data | true  | all string formats ignore nulls                                                  |
        | #/000/tests/006/data | true  | a valid date string                                                              |
        | #/000/tests/007/data | true  | a valid date string with 31 days in January                                      |
        | #/000/tests/008/data | false | a invalid date string with 32 days in January                                    |
        | #/000/tests/009/data | true  | a valid date string with 28 days in February (normal)                            |
        | #/000/tests/010/data | false | a invalid date string with 29 days in February (normal)                          |
        | #/000/tests/011/data | true  | a valid date string with 29 days in February (leap)                              |
        | #/000/tests/012/data | false | a invalid date string with 30 days in February (leap)                            |
        | #/000/tests/013/data | true  | a valid date string with 31 days in March                                        |
        | #/000/tests/014/data | false | a invalid date string with 32 days in March                                      |
        | #/000/tests/015/data | true  | a valid date string with 30 days in April                                        |
        | #/000/tests/016/data | false | a invalid date string with 31 days in April                                      |
        | #/000/tests/017/data | true  | a valid date string with 31 days in May                                          |
        | #/000/tests/018/data | false | a invalid date string with 32 days in May                                        |
        | #/000/tests/019/data | true  | a valid date string with 30 days in June                                         |
        | #/000/tests/020/data | false | a invalid date string with 31 days in June                                       |
        | #/000/tests/021/data | true  | a valid date string with 31 days in July                                         |
        | #/000/tests/022/data | false | a invalid date string with 32 days in July                                       |
        | #/000/tests/023/data | true  | a valid date string with 31 days in August                                       |
        | #/000/tests/024/data | false | a invalid date string with 32 days in August                                     |
        | #/000/tests/025/data | true  | a valid date string with 30 days in September                                    |
        | #/000/tests/026/data | false | a invalid date string with 31 days in September                                  |
        | #/000/tests/027/data | true  | a valid date string with 31 days in October                                      |
        | #/000/tests/028/data | false | a invalid date string with 32 days in October                                    |
        | #/000/tests/029/data | true  | a valid date string with 30 days in November                                     |
        | #/000/tests/030/data | false | a invalid date string with 31 days in November                                   |
        | #/000/tests/031/data | true  | a valid date string with 31 days in December                                     |
        | #/000/tests/032/data | false | a invalid date string with 32 days in December                                   |
        | #/000/tests/033/data | false | a invalid date string with invalid month                                         |
        | #/000/tests/034/data | false | an invalid date string                                                           |
        | #/000/tests/035/data | false | only RFC3339 not all of ISO 8601 are valid                                       |
        | #/000/tests/036/data | false | non-padded month dates are not valid                                             |
        | #/000/tests/037/data | false | non-padded day dates are not valid                                               |
        | #/000/tests/038/data | false | invalid month                                                                    |
        | #/000/tests/039/data | false | invalid month-day combination                                                    |
        | #/000/tests/040/data | false | 2021 is not a leap year                                                          |
        | #/000/tests/041/data | true  | 2020 is a leap year                                                              |
        | #/000/tests/042/data | false | non-ascii digits should be rejected                                              |
