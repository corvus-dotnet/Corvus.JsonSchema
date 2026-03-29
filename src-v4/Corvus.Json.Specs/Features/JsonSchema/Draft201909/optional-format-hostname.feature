@draft2019-09

Feature: optional-format-hostname draft2019-09
    In order to use json-schema
    As a developer
    I want to support optional-format-hostname in draft2019-09

Scenario Outline: validation of host names
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
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
        # hostname
        | #/000/tests/007/data | true  | single label                                                                     |
        # h0stn4me
        | #/000/tests/008/data | true  | single label with digits                                                         |
        # 1host
        | #/000/tests/009/data | true  | single label starting with digit                                                 |
        # hostnam3
        | #/000/tests/010/data | true  | single label ending with digit                                                   |
        # 
        | #/000/tests/011/data | false | empty string                                                                     |
        # .
        | #/000/tests/012/data | false | single dot                                                                       |
        # .example
        | #/000/tests/013/data | false | leading dot                                                                      |
        # example.
        | #/000/tests/014/data | false | trailing dot                                                                     |
        # example．com
        | #/000/tests/015/data | false | IDN label separator                                                              |
        # host-name
        | #/000/tests/016/data | true  | single label with hyphen                                                         |
        # -hostname
        | #/000/tests/017/data | false | starts with hyphen                                                               |
        # hostname-
        | #/000/tests/018/data | false | ends with hyphen                                                                 |
        # XN--aa---o47jg78q
        | #/000/tests/019/data | false | contains "--" in the 3rd and 4th position                                        |
        # host_name
        | #/000/tests/020/data | false | contains underscore                                                              |
        # abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com
        | #/000/tests/021/data | false | exceeds maximum overall length (256)                                             |
        # abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijk.com
        | #/000/tests/022/data | true  | maximum label length (63)                                                        |
        # abcdefghijklmnopqrstuvwxyzabcdefghijklmnopqrstuvwxyzabcdefghijkl.com
        | #/000/tests/023/data | false | exceeds maximum label length (63)                                                |

Scenario Outline: validation of A-label (punycode) host names
/* Schema: 
{
            "$schema": "https://json-schema.org/draft/2019-09/schema",
            "format": "hostname"
        }
*/
    Given the input JSON file "optional/format/hostname.json"
    And the schema at "#/1/schema"
    And the input data at "<inputDataReference>"
    And I generate a type for the schema
    And I construct an instance of the schema type from the data
    When I validate the instance
    Then the result will be <valid>

    Examples:
        | inputDataReference   | valid | description                                                                      |
        # xn--X
        | #/001/tests/000/data | false | invalid Punycode                                                                 |
        # xn--9n2bp8q.xn--9t4b11yi5a
        | #/001/tests/001/data | true  | a valid host name (example.test in Hangul)                                       |
        # xn--07jt112bpxg.xn--9t4b11yi5a
        | #/001/tests/002/data | false | contains illegal char U+302E Hangul single dot tone mark                         |
        # xn--hello-txk
        | #/001/tests/003/data | false | Begins with a Spacing Combining Mark                                             |
        # xn--hello-zed
        | #/001/tests/004/data | false | Begins with a Nonspacing Mark                                                    |
        # xn--hello-6bf
        | #/001/tests/005/data | false | Begins with an Enclosing Mark                                                    |
        # xn--zca29lwxobi7a
        | #/001/tests/006/data | true  | Exceptions that are PVALID, left-to-right chars                                  |
        # xn--qmbc
        | #/001/tests/007/data | true  | Exceptions that are PVALID, right-to-left chars                                  |
        # xn--chb89f
        | #/001/tests/008/data | false | Exceptions that are DISALLOWED, right-to-left chars                              |
        # xn--07jceefgh4c
        | #/001/tests/009/data | false | Exceptions that are DISALLOWED, left-to-right chars                              |
        # xn--al-0ea
        | #/001/tests/010/data | false | MIDDLE DOT with no preceding 'l'                                                 |
        # xn--l-fda
        | #/001/tests/011/data | false | MIDDLE DOT with nothing preceding                                                |
        # xn--la-0ea
        | #/001/tests/012/data | false | MIDDLE DOT with no following 'l'                                                 |
        # xn--l-gda
        | #/001/tests/013/data | false | MIDDLE DOT with nothing following                                                |
        # xn--ll-0ea
        | #/001/tests/014/data | true  | MIDDLE DOT with surrounding 'l's                                                 |
        # xn--S-jib3p
        | #/001/tests/015/data | false | Greek KERAIA not followed by Greek                                               |
        # xn--wva3j
        | #/001/tests/016/data | false | Greek KERAIA not followed by anything                                            |
        # xn--wva3je
        | #/001/tests/017/data | true  | Greek KERAIA followed by Greek                                                   |
        # xn--A-2hc5h
        | #/001/tests/018/data | false | Hebrew GERESH not preceded by Hebrew                                             |
        # xn--5db1e
        | #/001/tests/019/data | false | Hebrew GERESH not preceded by anything                                           |
        # xn--4dbc5h
        | #/001/tests/020/data | true  | Hebrew GERESH preceded by Hebrew                                                 |
        # xn--A-2hc8h
        | #/001/tests/021/data | false | Hebrew GERSHAYIM not preceded by Hebrew                                          |
        # xn--5db3e
        | #/001/tests/022/data | false | Hebrew GERSHAYIM not preceded by anything                                        |
        # xn--4dbc8h
        | #/001/tests/023/data | true  | Hebrew GERSHAYIM preceded by Hebrew                                              |
        # xn--defabc-k64e
        | #/001/tests/024/data | false | KATAKANA MIDDLE DOT with no Hiragana, Katakana, or Han                           |
        # xn--vek
        | #/001/tests/025/data | false | KATAKANA MIDDLE DOT with no other characters                                     |
        # xn--k8j5u
        | #/001/tests/026/data | true  | KATAKANA MIDDLE DOT with Hiragana                                                |
        # xn--bck0j
        | #/001/tests/027/data | true  | KATAKANA MIDDLE DOT with Katakana                                                |
        # xn--vek778f
        | #/001/tests/028/data | true  | KATAKANA MIDDLE DOT with Han                                                     |
        # xn--ngb6iyr
        | #/001/tests/029/data | false | Arabic-Indic digits mixed with Extended Arabic-Indic digits                      |
        # xn--ngba1o
        | #/001/tests/030/data | true  | Arabic-Indic digits not mixed with Extended Arabic-Indic digits                  |
        # xn--0-gyc
        | #/001/tests/031/data | true  | Extended Arabic-Indic digits not mixed with Arabic-Indic digits                  |
        # xn--11b2er09f
        | #/001/tests/032/data | false | ZERO WIDTH JOINER not preceded by Virama                                         |
        # xn--02b508i
        | #/001/tests/033/data | false | ZERO WIDTH JOINER not preceded by anything                                       |
        # xn--11b2ezcw70k
        | #/001/tests/034/data | true  | ZERO WIDTH JOINER preceded by Virama                                             |
        # xn--11b2ezcs70k
        | #/001/tests/035/data | true  | ZERO WIDTH NON-JOINER preceded by Virama                                         |
        # xn--ngba5hb2804a
        | #/001/tests/036/data | true  | ZERO WIDTH NON-JOINER not preceded by Virama but matches regexp                  |
