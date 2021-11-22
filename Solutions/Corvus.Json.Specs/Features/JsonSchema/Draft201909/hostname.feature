@draft2019-09

Feature: hostname draft2019-09
    In order to use json-schema
    As a developer
    I want to support hostname in draft2019-09

Scenario Outline: validation of host names
/* Schema: 
{"format": "hostname"}
*/
    Given the input JSON file "hostname.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid host name                                                                |
        | #/000/tests/001/data | true  | a valid punycoded IDN hostname                                                   |
        | #/000/tests/002/data | false | a host name starting with an illegal character                                   |
        | #/000/tests/003/data | false | a host name containing illegal characters                                        |
        | #/000/tests/004/data | false | a host name with a component too long                                            |
        | #/000/tests/005/data | false | starts with hyphen                                                               |
        | #/000/tests/006/data | false | ends with hyphen                                                                 |
        | #/000/tests/007/data | false | starts with underscore                                                           |
        | #/000/tests/008/data | false | ends with underscore                                                             |
        | #/000/tests/009/data | false | contains underscore                                                              |
        | #/000/tests/010/data | true  | maximum label length                                                             |
        | #/000/tests/011/data | false | exceeds maximum label length                                                     |
