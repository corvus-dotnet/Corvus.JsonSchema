@draft2020-12

Feature: hostname draft2020-12
    In order to use json-schema
    As a developer
    I want to support hostname in draft2020-12

Scenario Outline: validation of host names
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "format": "hostname"
        }
*/
    Given the input JSON file "optional/format/hostname.json"
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
        # www.example.com
        | #/000/tests/006/data | true  | a valid host name                                                                |
        # xn--4gbwdl.xn--wgbh1c
        | #/000/tests/007/data | true  | a valid punycoded IDN hostname                                                   |
        # -a-host-name-that-starts-with--
        | #/000/tests/008/data | false | a host name starting with an illegal character                                   |
        # not_a_valid_host_name
        | #/000/tests/009/data | false | a host name containing illegal characters                                        |
        # a-vvvvvvvvvvvvvvvveeeeeeeeeeeeeeeerrrrrrrrrrrrrrrryyyyyyyyyyyyyyyy-long-host-name-component
        | #/000/tests/010/data | false | a host name with a component too long                                            |
        # -hostname
        | #/000/tests/011/data | false | starts with hyphen                                                               |
        # hostname-
        | #/000/tests/012/data | false | ends with hyphen                                                                 |
        # _hostname
        | #/000/tests/013/data | false | starts with underscore                                                           |
        # hostname_
        | #/000/tests/014/data | false | ends with underscore                                                             |
        # host_name
        | #/000/tests/015/data | false | contains underscore                                                              |
        # abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com
        | #/000/tests/016/data | true  | maximum label length                                                             |
        # abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl.com
        | #/000/tests/017/data | false | exceeds maximum label length                                                     |
        # hostname
        | #/000/tests/018/data | true  | single label                                                                     |
        # host-name
        | #/000/tests/019/data | true  | single label with hyphen                                                         |
        # h0stn4me
        | #/000/tests/020/data | true  | single label with digits                                                         |
        # 1host
        | #/000/tests/021/data | true  | single label starting with digit                                                 |
        # hostnam3
        | #/000/tests/022/data | true  | single label ending with digit                                                   |
