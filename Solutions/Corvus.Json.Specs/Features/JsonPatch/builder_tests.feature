
Feature: builder_tests

Scenario: empty list, empty docs [65aaab02-aefb-4fde-87d6-a4cd639fd616]
	Given the document {}
	When I build the patch []
	Then the patch result should equal {}

Scenario: empty patch list [8cb8e43e-1adb-4610-9414-54125b728e86]
	Given the document {"foo":1}
	When I build the patch []
	Then the patch result should equal {"foo":1}

Scenario: rearrangements OK? [fd727ff0-11f7-48d1-b897-bd628fcc89e0]
	Given the document {"foo":1,"bar":2}
	When I build the patch []
	Then the patch result should equal {"bar":2,"foo":1}

Scenario: rearrangements OK?  How about one level down ... array [c9871db4-19dc-4df5-a993-ecbf75864759]
	Given the document [{"foo":1,"bar":2}]
	When I build the patch []
	Then the patch result should equal [{"bar":2,"foo":1}]

Scenario: rearrangements OK?  How about one level down... [190c047d-22d2-4b79-aa21-29a91d1a824a]
	Given the document {"foo":{"foo":1,"bar":2}}
	When I build the patch []
	Then the patch result should equal {"foo":{"bar":2,"foo":1}}

Scenario: add replaces any existing field [1f3cbf58-186c-4367-bede-41152713b8a7]
	Given the document {"foo":null}
	When I build the patch [{"op":"add","path":"/foo","value":1}]
	Then the patch result should equal {"foo":1}

Scenario: toplevel array [8a8beb5b-c40c-4d69-8634-9e4299c0f87d]
	Given the document []
	When I build the patch [{"op":"add","path":"/0","value":"foo"}]
	Then the patch result should equal ["foo"]

Scenario: toplevel array, no change [24579b57-6a5b-43f8-b06d-5aa461c16cec]
	Given the document ["foo"]
	When I build the patch []
	Then the patch result should equal ["foo"]

Scenario: toplevel object, numeric string [2c9fc267-3c8f-49f7-b45d-3d1193e3b663]
	Given the document {}
	When I build the patch [{"op":"add","path":"/foo","value":"1"}]
	Then the patch result should equal {"foo":"1"}

Scenario: toplevel object, integer [bdc22e11-5f6b-4da2-b5c9-c52bdee0b9e9]
	Given the document {}
	When I build the patch [{"op":"add","path":"/foo","value":1}]
	Then the patch result should equal {"foo":1}

Scenario: replace object document with array document? [8c7c0603-f1c3-46dd-aa6a-04e3942dd6dd]
	Given the document {}
	When I build the patch [{"op":"add","path":"","value":[]}]
	Then the patch result should equal []

Scenario: replace array document with object document? [f9ce0936-0d8f-4a4a-9156-5b09f93acbce]
	Given the document []
	When I build the patch [{"op":"add","path":"","value":{}}]
	Then the patch result should equal {}

Scenario: append to root array document? [c4c190da-d30d-4013-8579-12b3bf241004]
	Given the document []
	When I build the patch [{"op":"add","path":"/-","value":"hi"}]
	Then the patch result should equal ["hi"]

Scenario: Add, / target [8784babb-7e20-499d-a908-91821811a119]
	Given the document {}
	When I build the patch [{"op":"add","path":"/","value":1}]
	Then the patch result should equal {"":1}

Scenario: Add, /foo/ deep target (trailing slash) [7bc4e13d-350a-468d-aabf-95cb9c159f02]
	Given the document {"foo":{}}
	When I build the patch [{"op":"add","path":"/foo/","value":1}]
	Then the patch result should equal {"foo":{"":1}}

Scenario: Add composite value at top level [274582b1-e86f-4210-95d2-03f322c46edf]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":[1,2]}]
	Then the patch result should equal {"foo":1,"bar":[1,2]}

Scenario: Add into composite value [417d3604-1750-48fa-9975-1e8b3a8a9969]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"add","path":"/baz/0/foo","value":"world"}]
	Then the patch result should equal {"foo":1,"baz":[{"qux":"hello","foo":"world"}]}

Scenario: Undescribed scenario [08e94755-5da6-4dd3-99ce-ef24e5263180]
	Given the document {"bar":[1,2]}
	When I build the patch [{"op":"add","path":"/bar/8","value":"5"}]
	Then a patch exception should be thrown

Scenario: Undescribed scenario [ddc8463d-ac32-415c-9327-dab095ac6c67]
	Given the document {"bar":[1,2]}
	When I build the patch [{"op":"add","path":"/bar/-1","value":"5"}]
	Then a patch exception should be thrown

Scenario: Undescribed scenario [37ad638a-cced-4eae-b456-fac64e5bf805]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":true}]
	Then the patch result should equal {"foo":1,"bar":true}

Scenario: Undescribed scenario [037ebfd9-24e0-4f8c-b5ae-53ad67fadc46]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":false}]
	Then the patch result should equal {"foo":1,"bar":false}

Scenario: Undescribed scenario [bf0dd719-0516-4516-819f-9d09bf074719]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":null}]
	Then the patch result should equal {"foo":1,"bar":null}

Scenario: 0 can be an array index or object element name [be4c77a4-f6be-43f4-a049-bf0a92cda610]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/0","value":"bar"}]
	Then the patch result should equal {"foo":1,"0":"bar"}

Scenario: Undescribed scenario [6b22435d-bb8b-4230-b591-4ea7b6375020]
	Given the document ["foo"]
	When I build the patch [{"op":"add","path":"/1","value":"bar"}]
	Then the patch result should equal ["foo","bar"]

Scenario: Undescribed scenario [02c5cdb2-5557-47ac-a3f0-0c56d8509bd3]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/1","value":"bar"}]
	Then the patch result should equal ["foo","bar","sil"]

Scenario: Undescribed scenario [9a5e6ea3-9684-4bb9-8ee8-dc9ef0553f3f]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/0","value":"bar"}]
	Then the patch result should equal ["bar","foo","sil"]

Scenario: push item to array via last index + 1 [e3226b41-8eda-449e-89b8-3ac904a3644e]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/2","value":"bar"}]
	Then the patch result should equal ["foo","sil","bar"]

Scenario: add item to array at index > length should fail [c75ae750-5e27-473a-aa82-f1110e0bbcf8]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/3","value":"bar"}]
	Then a patch exception should be thrown

Scenario: test against implementation-specific numeric parsing [4784abe9-aa06-49ed-a09d-fb51b67b4cfd]
	Given the document {"1e0":"foo"}
	When I build the patch [{"op":"test","path":"/1e0","value":"foo"}]
	Then the patch result should equal {"1e0":"foo"}

Scenario: test with bad number should fail [68bf187a-87b1-4926-9062-60c87fc9c4bd]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"test","path":"/1e0","value":"bar"}]
	Then a patch exception should be thrown

Scenario: Undescribed scenario [f220a130-b30f-401b-b7fa-228bf926e17c]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/bar","value":42}]
	Then a patch exception should be thrown

Scenario: value in array add not flattened [1dfdb9b2-9752-470d-92bf-eae8679e3bbf]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/1","value":["bar","baz"]}]
	Then the patch result should equal ["foo",["bar","baz"],"sil"]

Scenario: Undescribed scenario [43e8870f-53a7-439f-9253-b3646e0eed16]
	Given the document {"foo":1,"bar":[1,2,3,4]}
	When I build the patch [{"op":"remove","path":"/bar"}]
	Then the patch result should equal {"foo":1}

Scenario: Undescribed scenario [48c3940a-3ddd-4a33-9e22-412afbae822e]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"remove","path":"/baz/0/qux"}]
	Then the patch result should equal {"foo":1,"baz":[{}]}

Scenario: Undescribed scenario [b6c4985e-9c17-459d-a27d-ec4c3fb809a8]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"replace","path":"/foo","value":[1,2,3,4]}]
	Then the patch result should equal {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}

Scenario: Undescribed scenario [61ce2c72-e500-4541-b231-5bdd4d3034ae]
	Given the document {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"replace","path":"/baz/0/qux","value":"world"}]
	Then the patch result should equal {"foo":[1,2,3,4],"baz":[{"qux":"world"}]}

Scenario: Undescribed scenario [93aec3b7-67a6-44a6-af04-b9afd8c044fb]
	Given the document ["foo"]
	When I build the patch [{"op":"replace","path":"/0","value":"bar"}]
	Then the patch result should equal ["bar"]

Scenario: Undescribed scenario [685dbb91-8b95-4521-a74c-ec28953950ea]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":0}]
	Then the patch result should equal [0]

Scenario: Undescribed scenario [af78f25e-80cc-4d51-b529-d0ad39d2d861]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":true}]
	Then the patch result should equal [true]

Scenario: Undescribed scenario [5e8a4a20-c55b-483b-86fa-4e83276f1c1e]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":false}]
	Then the patch result should equal [false]

Scenario: Undescribed scenario [76ff6468-12ed-44ba-957b-2c8666982c0f]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":null}]
	Then the patch result should equal [null]

Scenario: value in array replace not flattened [00c4473a-b473-4905-8a7e-a6995eca1f3d]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"replace","path":"/1","value":["bar","baz"]}]
	Then the patch result should equal ["foo",["bar","baz"]]

Scenario: replace whole document [f83d6995-5fe1-4885-badd-005d88b9ab04]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"replace","path":"","value":{"baz":"qux"}}]
	Then the patch result should equal {"baz":"qux"}

Scenario: test replace with missing parent key should fail [a193fb0b-20d4-4bde-9cc7-0cedf6e5b448]
	Given the document {"bar":"baz"}
	When I build the patch [{"op":"replace","path":"/foo/bar","value":false}]
	Then a patch exception should be thrown

Scenario: spurious patch properties [32a8af79-0a70-41cd-8f8c-3db00f4f16c1]
	Given the document {"foo":1}
	When I build the patch [{"op":"test","path":"/foo","value":1,"spurious":1}]
	Then the patch result should equal {"foo":1}

Scenario: null value should be valid obj property [1ff7f875-8b7f-44e7-8df3-7cfa6d7f5373]
	Given the document {"foo":null}
	When I build the patch [{"op":"test","path":"/foo","value":null}]
	Then the patch result should equal {"foo":null}

Scenario: null value should be valid obj property to be replaced with something truthy [b38afd08-8077-4473-ae1e-57ea1541025f]
	Given the document {"foo":null}
	When I build the patch [{"op":"replace","path":"/foo","value":"truthy"}]
	Then the patch result should equal {"foo":"truthy"}

Scenario: null value should be valid obj property to be moved [c5878c94-f6d5-455f-aec7-b1d655437f32]
	Given the document {"foo":null}
	When I build the patch [{"op":"move","from":"/foo","path":"/bar"}]
	Then the patch result should equal {"bar":null}

Scenario: null value should be valid obj property to be copied [7c10c0d3-105b-412a-8591-5f33526bbe67]
	Given the document {"foo":null}
	When I build the patch [{"op":"copy","from":"/foo","path":"/bar"}]
	Then the patch result should equal {"foo":null,"bar":null}

Scenario: null value should be valid obj property to be removed [a01de2cd-2fd1-4b47-8f72-c91be7f0352a]
	Given the document {"foo":null}
	When I build the patch [{"op":"remove","path":"/foo"}]
	Then the patch result should equal {}

Scenario: null value should still be valid obj property replace other value [4064068a-f1ef-428a-bc4c-528b1c3217d7]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"replace","path":"/foo","value":null}]
	Then the patch result should equal {"foo":null}

Scenario: test should pass despite rearrangement [0810c115-5617-474b-8fd8-9989ed1fd392]
	Given the document {"foo":{"foo":1,"bar":2}}
	When I build the patch [{"op":"test","path":"/foo","value":{"bar":2,"foo":1}}]
	Then the patch result should equal {"foo":{"foo":1,"bar":2}}

Scenario: test should pass despite (nested) rearrangement [c60ee4cc-7c3e-409b-8611-b446b22d9f95]
	Given the document {"foo":[{"foo":1,"bar":2}]}
	When I build the patch [{"op":"test","path":"/foo","value":[{"bar":2,"foo":1}]}]
	Then the patch result should equal {"foo":[{"foo":1,"bar":2}]}

Scenario: test should pass - no error [9377b712-efb9-45e0-befb-70b51d4aa266]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	When I build the patch [{"op":"test","path":"/foo","value":{"bar":[1,2,5,4]}}]
	Then the patch result should equal {"foo":{"bar":[1,2,5,4]}}

Scenario: Undescribed scenario [62e8a073-547d-4d13-869e-6a593bdd9f10]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	When I build the patch [{"op":"test","path":"/foo","value":[1,2]}]
	Then a patch exception should be thrown

Scenario: Empty-string element [83011935-b8be-41c8-93b5-e1ebff0df279]
	Given the document {"":1}
	When I build the patch [{"op":"test","path":"/","value":1}]
	Then the patch result should equal {"":1}

Scenario: Undescribed scenario [7eaab010-5498-4e7f-af97-f15c23b6e5df]
	Given the document {"foo":["bar","baz"],"":0,"a/b":1,"c%d":2,"e^f":3,"g|h":4,"i\\j":5,"k\u0022l":6," ":7,"m~n":8}
	When I build the patch [{"op":"test","path":"/foo","value":["bar","baz"]},{"op":"test","path":"/foo/0","value":"bar"},{"op":"test","path":"/","value":0},{"op":"test","path":"/a~1b","value":1},{"op":"test","path":"/c%d","value":2},{"op":"test","path":"/e^f","value":3},{"op":"test","path":"/g|h","value":4},{"op":"test","path":"/i\\j","value":5},{"op":"test","path":"/k\u0022l","value":6},{"op":"test","path":"/ ","value":7},{"op":"test","path":"/m~0n","value":8}]
	Then the patch result should equal {"":0," ":7,"a/b":1,"c%d":2,"e^f":3,"foo":["bar","baz"],"g|h":4,"i\\j":5,"k\u0022l":6,"m~n":8}

Scenario: Move to same location has no effect [e4271114-a290-4900-913d-04726dfaa660]
	Given the document {"foo":1}
	When I build the patch [{"op":"move","from":"/foo","path":"/foo"}]
	Then the patch result should equal {"foo":1}

Scenario: Undescribed scenario [d9eab1d9-da1a-4cba-9e82-30d48a18319c]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"move","from":"/foo","path":"/bar"}]
	Then the patch result should equal {"baz":[{"qux":"hello"}],"bar":1}

Scenario: Undescribed scenario [1cef0973-6b06-41c5-b755-fb0cc6243451]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	When I build the patch [{"op":"move","from":"/baz/0/qux","path":"/baz/1"}]
	Then the patch result should equal {"baz":[{},"hello"],"bar":1}

Scenario: Undescribed scenario [4cb50dcc-95d7-4783-be4f-9b25c9fa20c3]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	When I build the patch [{"op":"copy","from":"/baz/0","path":"/boo"}]
	Then the patch result should equal {"baz":[{"qux":"hello"}],"bar":1,"boo":{"qux":"hello"}}

Scenario: replacing the root of the document is possible with add [49b6d9d7-bf21-4c65-8b71-99e192ccd28c]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"","value":{"baz":"qux"}}]
	Then the patch result should equal {"baz":"qux"}

Scenario: Adding to "/-" adds to the end of the array [befe892b-d41d-4b23-940c-dd7aa80adb44]
	Given the document [1,2]
	When I build the patch [{"op":"add","path":"/-","value":{"foo":["bar","baz"]}}]
	Then the patch result should equal [1,2,{"foo":["bar","baz"]}]

Scenario: Adding to "/-" adds to the end of the array, even n levels down [d083e522-2bd6-4685-8b53-09334bf178b8]
	Given the document [1,2,[3,[4,5]]]
	When I build the patch [{"op":"add","path":"/2/1/-","value":{"foo":["bar","baz"]}}]
	Then the patch result should equal [1,2,[3,[4,5,{"foo":["bar","baz"]}]]]

Scenario: test remove with bad number should fail [d2656873-5c60-490e-8922-538baa7b5072]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"remove","path":"/baz/1e0/qux"}]
	Then a patch exception should be thrown

Scenario: test remove on array [78a297dd-d225-4d49-be4b-9a0038064efc]
	Given the document [1,2,3,4]
	When I build the patch [{"op":"remove","path":"/0"}]
	Then the patch result should equal [2,3,4]

Scenario: test repeated removes [f79646e7-a591-43d5-bcbb-0fb05c90e7bc]
	Given the document [1,2,3,4]
	When I build the patch [{"op":"remove","path":"/1"},{"op":"remove","path":"/2"}]
	Then the patch result should equal [1,3]

Scenario: test remove with bad index should fail [34d59d1d-7bc9-404c-97f7-8ae3f72dbd5a]
	Given the document [1,2,3,4]
	When I build the patch [{"op":"remove","path":"/1e0"}]
	Then a patch exception should be thrown

Scenario: test replace with bad number should fail [3dccba9b-5602-4a60-9e87-da7e1c19bff5]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/1e0","value":false}]
	Then a patch exception should be thrown

Scenario: test copy with bad number should fail [1d9c5583-2623-486d-99a2-d7322a6211b7]
	Given the document {"baz":[1,2,3],"bar":1}
	When I build the patch [{"op":"copy","from":"/baz/1e0","path":"/boo"}]
	Then a patch exception should be thrown

Scenario: test move with bad number should fail [df5b67d4-a6a5-4569-9dd0-fc572f3cbdac]
	Given the document {"foo":1,"baz":[1,2,3,4]}
	When I build the patch [{"op":"move","from":"/baz/1e0","path":"/foo"}]
	Then a patch exception should be thrown

Scenario: test add with bad number should fail [4dabce6f-1256-4c1b-ae8d-2641fc60edbe]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/1e0","value":"bar"}]
	Then a patch exception should be thrown

Scenario: missing 'path' parameter [6b00ae33-7f59-4172-869f-6a9dea76319f]
	Given the document {}
	When I build the patch [{"op":"add","value":"bar"}]
	Then a patch exception should be thrown

Scenario: 'path' parameter with null value [447e3338-0952-4295-bb14-240e95c09f6d]
	Given the document {}
	When I build the patch [{"op":"add","path":null,"value":"bar"}]
	Then a patch exception should be thrown

Scenario: invalid JSON Pointer token [daf56299-c10d-4568-8d10-4d78e7b5281a]
	Given the document {}
	When I build the patch [{"op":"add","path":"foo","value":"bar"}]
	Then a patch exception should be thrown

Scenario: missing 'value' parameter to add [f9876c70-3514-49cb-a4b7-f5421a3202c6]
	Given the document [1]
	When I build the patch [{"op":"add","path":"/-"}]
	Then a patch exception should be thrown

Scenario: missing 'value' parameter to replace [2f13da60-0c23-4d01-aae0-b5977ef6823b]
	Given the document [1]
	When I build the patch [{"op":"replace","path":"/0"}]
	Then a patch exception should be thrown

Scenario: missing 'value' parameter to test [49ea2fb8-0307-4c68-add5-77a15c8947df]
	Given the document [null]
	When I build the patch [{"op":"test","path":"/0"}]
	Then a patch exception should be thrown

Scenario: missing value parameter to test - where undef is falsy [6f6675f2-e8cf-4f95-8c45-5d08418c476e]
	Given the document [false]
	When I build the patch [{"op":"test","path":"/0"}]
	Then a patch exception should be thrown

Scenario: missing from parameter to copy [c72e1a45-9622-4382-9f96-2a45510a5d90]
	Given the document [1]
	When I build the patch [{"op":"copy","path":"/-"}]
	Then a patch exception should be thrown

Scenario: missing from location to copy [febefd40-b430-4f87-9b25-54e1dbb01525]
	Given the document {"foo":1}
	When I build the patch [{"op":"copy","from":"/bar","path":"/foo"}]
	Then a patch exception should be thrown

Scenario: missing from parameter to move [c51e7387-94c6-48a3-91b3-6499bf463d10]
	Given the document {"foo":1}
	When I build the patch [{"op":"move","path":""}]
	Then a patch exception should be thrown

Scenario: missing from location to move [94cc8d0f-3d1d-4135-beaf-98baaadd080f]
	Given the document {"foo":1}
	When I build the patch [{"op":"move","from":"/bar","path":"/foo"}]
	Then a patch exception should be thrown

Scenario: unrecognized op should fail [beb508c6-5fe8-455f-84e8-579f303f35b2]
	Given the document {"foo":1}
	When I build the patch [{"op":"spam","path":"/foo","value":1}]
	Then a patch exception should be thrown

Scenario: test with bad array number that has leading zeros [3f310d3b-f144-483d-b0d4-d924f2df0a73]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"test","path":"/00","value":"foo"}]
	Then a patch exception should be thrown

Scenario: test with bad array number that has leading zeros [59de35f8-0f04-4eb6-a4be-d341855e8257]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"test","path":"/01","value":"bar"}]
	Then a patch exception should be thrown

Scenario: Removing nonexistent field [ab020c89-a5ac-44ea-b2e2-ac9769f9129a]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"remove","path":"/baz"}]
	Then a patch exception should be thrown

Scenario: Removing deep nonexistent path [d1df167b-9135-49a4-91da-e7054c9bf070]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"remove","path":"/missing1/missing2"}]
	Then a patch exception should be thrown

Scenario: Removing nonexistent index [6d62d9f9-184e-44c9-914e-29264ea9873b]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"remove","path":"/2"}]
	Then a patch exception should be thrown

Scenario: Patch with different capitalisation than doc [5252afb2-09e0-4726-bc4d-b178be18e3a4]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/FOO","value":"BAR"}]
	Then the patch result should equal {"foo":"bar","FOO":"BAR"}
