@draft7

Feature: optional-format-idn-hostname draft7
    In order to use json-schema
    As a developer
    I want to support optional-format-idn-hostname in draft7

Scenario Outline: validation of internationalized host names
/* Schema: 
{ "format": "idn-hostname" }
*/
    Given the input JSON file "optional/format/idn-hostname.json"
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
        # 실례.테스트
        | #/000/tests/006/data | true  | a valid host name (example.test in Hangul)                                       |
        # 〮실례.테스트
        | #/000/tests/007/data | false | illegal first char U+302E Hangul single dot tone mark                            |
        # 실〮례.테스트
        | #/000/tests/008/data | false | contains illegal char U+302E Hangul single dot tone mark                         |
        # 실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실실례례테스트례례례례례례례례례례례례례례례례례테스트례례례례례례례례례례례례례례례례례례례테스트례례례례례례례례례례례례테스트례례실례.테스트
        | #/000/tests/009/data | false | a host name with a component too long                                            |
        # -> $1.00 <--
        | #/000/tests/010/data | false | invalid label, correct Punycode                                                  |
        # xn--ihqwcrb4cv8a8dqg056pqjye
        | #/000/tests/011/data | true  | valid Chinese Punycode                                                           |
        # xn--X
        | #/000/tests/012/data | false | invalid Punycode                                                                 |
        # XN--aa---o47jg78q
        | #/000/tests/013/data | false | U-label contains "--" in the 3rd and 4th position                                |
        # -hello
        | #/000/tests/014/data | false | U-label starts with a dash                                                       |
        # hello-
        | #/000/tests/015/data | false | U-label ends with a dash                                                         |
        # -hello-
        | #/000/tests/016/data | false | U-label starts and ends with a dash                                              |
        # ःhello
        | #/000/tests/017/data | false | Begins with a Spacing Combining Mark                                             |
        # ̀hello
        | #/000/tests/018/data | false | Begins with a Nonspacing Mark                                                    |
        # ҈hello
        | #/000/tests/019/data | false | Begins with an Enclosing Mark                                                    |
        # ßς་〇
        | #/000/tests/020/data | true  | Exceptions that are PVALID, left-to-right chars                                  |
        # ۽۾
        | #/000/tests/021/data | true  | Exceptions that are PVALID, right-to-left chars                                  |
        # ـߺ
        | #/000/tests/022/data | false | Exceptions that are DISALLOWED, right-to-left chars                              |
        # 〱〲〳〴〵〮〯〻
        | #/000/tests/023/data | false | Exceptions that are DISALLOWED, left-to-right chars                              |
        # a·l
        | #/000/tests/024/data | false | MIDDLE DOT with no preceding 'l'                                                 |
        # ·l
        | #/000/tests/025/data | false | MIDDLE DOT with nothing preceding                                                |
        # l·a
        | #/000/tests/026/data | false | MIDDLE DOT with no following 'l'                                                 |
        # l·
        | #/000/tests/027/data | false | MIDDLE DOT with nothing following                                                |
        # l·l
        | #/000/tests/028/data | true  | MIDDLE DOT with surrounding 'l's                                                 |
        # α͵S
        | #/000/tests/029/data | false | Greek KERAIA not followed by Greek                                               |
        # α͵
        | #/000/tests/030/data | false | Greek KERAIA not followed by anything                                            |
        # α͵β
        | #/000/tests/031/data | true  | Greek KERAIA followed by Greek                                                   |
        # A׳ב
        | #/000/tests/032/data | false | Hebrew GERESH not preceded by Hebrew                                             |
        # ׳ב
        | #/000/tests/033/data | false | Hebrew GERESH not preceded by anything                                           |
        # א׳ב
        | #/000/tests/034/data | true  | Hebrew GERESH preceded by Hebrew                                                 |
        # A״ב
        | #/000/tests/035/data | false | Hebrew GERSHAYIM not preceded by Hebrew                                          |
        # ״ב
        | #/000/tests/036/data | false | Hebrew GERSHAYIM not preceded by anything                                        |
        # א״ב
        | #/000/tests/037/data | true  | Hebrew GERSHAYIM preceded by Hebrew                                              |
        # def・abc
        | #/000/tests/038/data | false | KATAKANA MIDDLE DOT with no Hiragana, Katakana, or Han                           |
        # ・
        | #/000/tests/039/data | false | KATAKANA MIDDLE DOT with no other characters                                     |
        # ・ぁ
        | #/000/tests/040/data | true  | KATAKANA MIDDLE DOT with Hiragana                                                |
        # ・ァ
        | #/000/tests/041/data | true  | KATAKANA MIDDLE DOT with Katakana                                                |
        # ・丈
        | #/000/tests/042/data | true  | KATAKANA MIDDLE DOT with Han                                                     |
        # ٠۰
        | #/000/tests/043/data | false | Arabic-Indic digits mixed with Extended Arabic-Indic digits                      |
        # ب٠ب
        | #/000/tests/044/data | true  | Arabic-Indic digits not mixed with Extended Arabic-Indic digits                  |
        # ۰0
        | #/000/tests/045/data | true  | Extended Arabic-Indic digits not mixed with Arabic-Indic digits                  |
        # क‍ष
        | #/000/tests/046/data | false | ZERO WIDTH JOINER not preceded by Virama                                         |
        # ‍ष
        | #/000/tests/047/data | false | ZERO WIDTH JOINER not preceded by anything                                       |
        # क्‍ष
        | #/000/tests/048/data | true  | ZERO WIDTH JOINER preceded by Virama                                             |
        # क्‌ष
        | #/000/tests/049/data | true  | ZERO WIDTH NON-JOINER preceded by Virama                                         |
        # بي‌بي
        | #/000/tests/050/data | true  | ZERO WIDTH NON-JOINER not preceded by Virama but matches regexp                  |
        # hostname
        | #/000/tests/051/data | true  | single label                                                                     |
        # host-name
        | #/000/tests/052/data | true  | single label with hyphen                                                         |
        # h0stn4me
        | #/000/tests/053/data | true  | single label with digits                                                         |
        # hostnam3
        | #/000/tests/054/data | true  | single label ending with digit                                                   |
