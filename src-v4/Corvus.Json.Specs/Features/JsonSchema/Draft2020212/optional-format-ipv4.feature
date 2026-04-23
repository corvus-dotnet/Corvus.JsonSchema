@draft2020-12

Feature: optional-format-ipv4 draft2020-12
    In order to use json-schema
    As a developer
    I want to support optional-format-ipv4 in draft2020-12

Scenario Outline: validation of IP addresses
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "ipv4"
        }
*/
    Given the input JSON file "optional/format/ipv4.json"
    And the schema at "#/0/schema"
    And the input data at "<inputDataReference>"
    And I assert format
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
        # 1২7.0.0.1
        | #/000/tests/012/data | false | invalid non-ASCII '২' (a Bengali 2)                                              |
        # 192.168.1.0/24
        | #/000/tests/013/data | false | netmask is not a part of ipv4 address                                            |
        #  192.168.0.1
        | #/000/tests/014/data | false | leading whitespace is invalid                                                    |
        # 192.168.0.1 
        | #/000/tests/015/data | false | trailing whitespace is invalid                                                   |
        # 192.168.0.1 
        | #/000/tests/016/data | false | trailing newline is invalid                                                      |
        # 0x7f.0.0.1
        | #/000/tests/017/data | false | hexadecimal notation is invalid                                                  |
        # 0o10.0.0.1
        | #/000/tests/018/data | false | octal notation explicit is invalid                                               |
        # 192.168..1
        | #/000/tests/019/data | false | empty part (double dot) is invalid                                               |
        # .192.168.0.1
        | #/000/tests/020/data | false | leading dot is invalid                                                           |
        # 192.168.0.1.
        | #/000/tests/021/data | false | trailing dot is invalid                                                          |
        # 0.0.0.0
        | #/000/tests/022/data | true  | minimum valid IPv4 address                                                       |
        # 255.255.255.255
        | #/000/tests/023/data | true  | maximum valid IPv4 address                                                       |
        # 
        | #/000/tests/024/data | false | empty string is invalid                                                          |
        # +1.2.3.4
        | #/000/tests/025/data | false | plus sign is invalid                                                             |
        # -1.2.3.4
        | #/000/tests/026/data | false | negative sign is invalid                                                         |
        # 1e2.0.0.1
        | #/000/tests/027/data | false | exponential notation is invalid                                                  |
        # 192.168.a.1
        | #/000/tests/028/data | false | alpha characters are invalid                                                     |
        # 192. 168.0.1
        | #/000/tests/029/data | false | internal whitespace is invalid                                                   |
        # 192.168.0.1 
        | #/000/tests/030/data | false | tab character is invalid                                                         |
        # 192.168.0.1:80
        | #/000/tests/031/data | false | with port number is invalid                                                      |
        # 192.168.0.256
        | #/000/tests/032/data | false | single octet out of range in last position                                       |
