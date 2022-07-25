@draft2019-09

Feature: ipv6 draft2019-09
    In order to use json-schema
    As a developer
    I want to support ipv6 in draft2019-09

Scenario Outline: validation of IPv6 addresses
/* Schema: 
{ "format": "ipv6" }
*/
    Given the input JSON file "optional/format/ipv6.json"
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
        | #/000/tests/006/data | true  | a valid IPv6 address                                                             |
        | #/000/tests/007/data | false | an IPv6 address with out-of-range values                                         |
        | #/000/tests/008/data | true  | trailing 4 hex symbols is valid                                                  |
        | #/000/tests/009/data | false | trailing 5 hex symbols is invalid                                                |
        | #/000/tests/010/data | false | an IPv6 address with too many components                                         |
        | #/000/tests/011/data | false | an IPv6 address containing illegal characters                                    |
        | #/000/tests/012/data | true  | no digits is valid                                                               |
        | #/000/tests/013/data | true  | leading colons is valid                                                          |
        | #/000/tests/014/data | true  | trailing colons is valid                                                         |
        | #/000/tests/015/data | false | missing leading octet is invalid                                                 |
        | #/000/tests/016/data | false | missing trailing octet is invalid                                                |
        | #/000/tests/017/data | false | missing leading octet with omitted octets later                                  |
        | #/000/tests/018/data | true  | single set of double colons in the middle is valid                               |
        | #/000/tests/019/data | false | two sets of double colons is invalid                                             |
        | #/000/tests/020/data | true  | mixed format with the ipv4 section as decimal octets                             |
        | #/000/tests/021/data | true  | mixed format with double colons between the sections                             |
        | #/000/tests/022/data | false | mixed format with ipv4 section with octet out of range                           |
        | #/000/tests/023/data | false | mixed format with ipv4 section with a hex octet                                  |
        | #/000/tests/024/data | true  | mixed format with leading double colons (ipv4-mapped ipv6 address)               |
        | #/000/tests/025/data | false | triple colons is invalid                                                         |
        | #/000/tests/026/data | true  | 8 octets                                                                         |
        | #/000/tests/027/data | false | insufficient octets without double colons                                        |
        | #/000/tests/028/data | false | no colons is invalid                                                             |
        | #/000/tests/029/data | false | ipv4 is not ipv6                                                                 |
        | #/000/tests/030/data | false | ipv4 segment must have 4 octets                                                  |
        | #/000/tests/031/data | false | leading whitespace is invalid                                                    |
        | #/000/tests/032/data | false | trailing whitespace is invalid                                                   |
        | #/000/tests/033/data | false | netmask is not a part of ipv6 address                                            |
        | #/000/tests/034/data | false | zone id is not a part of ipv6 address                                            |
        | #/000/tests/035/data | true  | a long valid ipv6                                                                |
        | #/000/tests/036/data | false | a long invalid ipv6, below length limit, first                                   |
        | #/000/tests/037/data | false | a long invalid ipv6, below length limit, second                                  |
        | #/000/tests/038/data | false | non-ascii digits should be rejected                                              |
        | #/000/tests/039/data | false | non-ascii digits should be rejected in the ipv4 portion also                     |
