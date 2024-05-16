@openApi30

Feature: optional-format-ipv4 openApi30
    In order to use json-schema
    As a developer
    I want to support optional-format-ipv4 in openApi30

Scenario Outline: validation of IP addresses
/* Schema: 
{ "format": "ipv4" }
*/
    Given the input JSON file "optional/format/ipv4.json"
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
        # 192.168.0.1
        | #/000/tests/006/data | true  | a valid IP address                                                               |
        # 127.0.0.0.1
        | #/000/tests/007/data | false | an IP address with too many components                                           |
        # 256.256.256.256
        | #/000/tests/008/data | false | an IP address with out-of-range values                                           |
        # 127.0
        | #/000/tests/009/data | false | an IP address without 4 components                                               |
        # 0x7f000001
        | #/000/tests/010/data | false | an IP address as an integer                                                      |
        # 2130706433
        | #/000/tests/011/data | false | an IP address as an integer (decimal)                                            |
        # 087.10.0.1
        | #/000/tests/012/data | false | invalid leading zeroes, as they are treated as octals                            |
        # 87.10.0.1
        | #/000/tests/013/data | true  | value without leading zero is valid                                              |
        # 1২7.0.0.1
        | #/000/tests/014/data | false | invalid non-ASCII '২' (a Bengali 2)                                              |
        # 192.168.1.0/24
        | #/000/tests/015/data | false | netmask is not a part of ipv4 address                                            |
