@draft2020-12

Feature: duration draft2020-12
    In order to use json-schema
    As a developer
    I want to support duration in draft2020-12

Scenario Outline: validation of duration strings
/* Schema: 
{"format": "duration"}
*/
    Given the input JSON file "duration.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid duration string                                                          |
        | #/000/tests/001/data | false | an invalid duration string                                                       |
        | #/000/tests/002/data | false | no elements present                                                              |
        | #/000/tests/003/data | false | no time elements present                                                         |
        | #/000/tests/004/data | false | no date or time elements present                                                 |
        | #/000/tests/005/data | false | elements out of order                                                            |
        | #/000/tests/006/data | false | missing time separator                                                           |
        | #/000/tests/007/data | false | time element in the date position                                                |
        | #/000/tests/008/data | true  | four years duration                                                              |
        | #/000/tests/009/data | true  | zero time, in seconds                                                            |
        | #/000/tests/010/data | true  | zero time, in days                                                               |
        | #/000/tests/011/data | true  | one month duration                                                               |
        | #/000/tests/012/data | true  | one minute duration                                                              |
        | #/000/tests/013/data | true  | one and a half days, in hours                                                    |
        | #/000/tests/014/data | true  | one and a half days, in days and hours                                           |
        | #/000/tests/015/data | true  | two weeks                                                                        |
        | #/000/tests/016/data | false | weeks cannot be combined with other units                                        |
