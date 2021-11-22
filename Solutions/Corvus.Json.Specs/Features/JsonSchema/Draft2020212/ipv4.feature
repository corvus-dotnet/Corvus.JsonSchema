@draft2020-12

Feature: ipv4 draft2020-12
    In order to use json-schema
    As a developer
    I want to support ipv4 in draft2020-12

Scenario Outline: validation of IP addresses
/* Schema: 
{"format": "ipv4"}
*/
    Given the input JSON file "ipv4.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        | #/000/tests/000/data | true  | a valid IP address                                                               |
        | #/000/tests/001/data | false | an IP address with too many components                                           |
        | #/000/tests/002/data | false | an IP address with out-of-range values                                           |
        | #/000/tests/003/data | false | an IP address without 4 components                                               |
        | #/000/tests/004/data | false | an IP address as an integer                                                      |
        | #/000/tests/005/data | false | an IP address as an integer (decimal)                                            |
        | #/000/tests/006/data | false | leading zeroes should be rejected, as they are treated as octals                 |
        | #/000/tests/007/data | true  | value without leading zero is valid                                              |
