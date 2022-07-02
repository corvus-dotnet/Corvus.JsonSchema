
Feature: builder_tests

Scenario: empty list, empty docs [a06b3558-84e9-4077-8a0a-f8b8e8ee1058]
	Given the document {}
	When I build the patch []
	Then the patch result should equal {}

Scenario: empty patch list [13849482-0daa-4ebf-8835-34302de0d068]
	Given the document {"foo":1}
	When I build the patch []
	Then the patch result should equal {"foo":1}

Scenario: rearrangements OK? [59b03d66-1a9c-4492-b311-8ab1ca6ceac8]
	Given the document {"foo":1,"bar":2}
	When I build the patch []
	Then the patch result should equal {"bar":2,"foo":1}

Scenario: rearrangements OK?  How about one level down ... array [f237b173-bdcf-4736-a970-7eef46be24c0]
	Given the document [{"foo":1,"bar":2}]
	When I build the patch []
	Then the patch result should equal [{"bar":2,"foo":1}]

Scenario: rearrangements OK?  How about one level down... [ec5ae55d-affa-493e-877a-2d4c64c68c8e]
	Given the document {"foo":{"foo":1,"bar":2}}
	When I build the patch []
	Then the patch result should equal {"foo":{"bar":2,"foo":1}}

Scenario: add replaces any existing field [9c691c49-4829-406d-8338-fad13fe4da99]
	Given the document {"foo":null}
	When I build the patch [{"op":"add","path":"/foo","value":1}]
	Then the patch result should equal {"foo":1}

Scenario: toplevel array [363fde7d-d255-48a7-90d5-d3e00e18643f]
	Given the document []
	When I build the patch [{"op":"add","path":"/0","value":"foo"}]
	Then the patch result should equal ["foo"]

Scenario: toplevel array, no change [748a4998-1d00-480f-9ebb-a6db81cef53e]
	Given the document ["foo"]
	When I build the patch []
	Then the patch result should equal ["foo"]

Scenario: toplevel object, numeric string [74dd8c9d-b2ae-42d2-b2e7-ee135d48e6cb]
	Given the document {}
	When I build the patch [{"op":"add","path":"/foo","value":"1"}]
	Then the patch result should equal {"foo":"1"}

Scenario: toplevel object, integer [fdfe336d-8cb4-42e4-a7a3-79b0e01fcdde]
	Given the document {}
	When I build the patch [{"op":"add","path":"/foo","value":1}]
	Then the patch result should equal {"foo":1}

Scenario: replace object document with array document? [2f8c5c89-2ee1-4a8c-9cd0-5290da4c867e]
	Given the document {}
	When I build the patch [{"op":"add","path":"","value":[]}]
	Then the patch result should equal []

Scenario: replace array document with object document? [b05b2bb7-9265-4ff6-ad97-d87cbce2a0a6]
	Given the document []
	When I build the patch [{"op":"add","path":"","value":{}}]
	Then the patch result should equal {}

Scenario: append to root array document? [df5157e6-9a5a-45e7-a22e-6334ceb26dd7]
	Given the document []
	When I build the patch [{"op":"add","path":"/-","value":"hi"}]
	Then the patch result should equal ["hi"]

Scenario: Add, / target [aa0e6415-55d3-495f-9da9-e08b3f1bdc4a]
	Given the document {}
	When I build the patch [{"op":"add","path":"/","value":1}]
	Then the patch result should equal {"":1}

Scenario: Add, /foo/ deep target (trailing slash) [e3b0cd92-b536-4687-9d66-dc9ecf8d61a2]
	Given the document {"foo":{}}
	When I build the patch [{"op":"add","path":"/foo/","value":1}]
	Then the patch result should equal {"foo":{"":1}}

Scenario: Add composite value at top level [5c277014-2fab-4d76-99e8-96e9bddb0d41]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":[1,2]}]
	Then the patch result should equal {"foo":1,"bar":[1,2]}

Scenario: Add into composite value [1bbb1033-3ef1-4adb-8baf-8883f828722e]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"add","path":"/baz/0/foo","value":"world"}]
	Then the patch result should equal {"foo":1,"baz":[{"qux":"hello","foo":"world"}]}

Scenario: Undescribed scenario [6a8e23ea-fb83-429b-b91d-2da22e243fda]
	Given the document {"bar":[1,2]}
	When I build the patch [{"op":"add","path":"/bar/8","value":"5"}]
	Then a patch exception should be thrown

Scenario: Undescribed scenario [34b16608-f42d-42db-8318-6ef5edf9a333]
	Given the document {"bar":[1,2]}
	When I build the patch [{"op":"add","path":"/bar/-1","value":"5"}]
	Then a patch exception should be thrown

Scenario: Undescribed scenario [99f66a0d-c066-4aa0-86d6-353cc6b7fcb0]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":true}]
	Then the patch result should equal {"foo":1,"bar":true}

Scenario: Undescribed scenario [fd571274-107e-4f24-9c7d-a80f2eb768db]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":false}]
	Then the patch result should equal {"foo":1,"bar":false}

Scenario: Undescribed scenario [23c4b854-5347-45bc-8b06-edeb1a77c36d]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/bar","value":null}]
	Then the patch result should equal {"foo":1,"bar":null}

Scenario: 0 can be an array index or object element name [a0d104ea-0a99-4136-810f-d03a0aca5848]
	Given the document {"foo":1}
	When I build the patch [{"op":"add","path":"/0","value":"bar"}]
	Then the patch result should equal {"foo":1,"0":"bar"}

Scenario: Undescribed scenario [f365945c-08fb-42a1-a829-e8a5e59f93f2]
	Given the document ["foo"]
	When I build the patch [{"op":"add","path":"/1","value":"bar"}]
	Then the patch result should equal ["foo","bar"]

Scenario: Undescribed scenario [78f8b5ea-206e-4879-8860-811ea3d9a49b]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/1","value":"bar"}]
	Then the patch result should equal ["foo","bar","sil"]

Scenario: Undescribed scenario [0c1dba45-099b-4320-8f0c-7c83e76376b8]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/0","value":"bar"}]
	Then the patch result should equal ["bar","foo","sil"]

Scenario: push item to array via last index + 1 [eb6a8e87-fd69-4546-9587-6b28e45dd1f3]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/2","value":"bar"}]
	Then the patch result should equal ["foo","sil","bar"]

Scenario: add item to array at index > length should fail [10427bea-fa64-430c-89b0-11d737ba073f]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/3","value":"bar"}]
	Then a patch exception should be thrown

Scenario: test against implementation-specific numeric parsing [7e9ddf32-ab33-4499-8a4e-4e2d5d229e08]
	Given the document {"1e0":"foo"}
	When I build the patch [{"op":"test","path":"/1e0","value":"foo"}]
	Then the patch result should equal {"1e0":"foo"}

Scenario: test with bad number should fail [e3f8050e-2c22-458f-9c3f-a37fb70641f5]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"test","path":"/1e0","value":"bar"}]
	Then a patch exception should be thrown

Scenario: Undescribed scenario [dce28221-1072-484b-914d-9d04087d7784]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/bar","value":42}]
	Then a patch exception should be thrown

Scenario: value in array add not flattened [33facf8b-e1a1-4183-a22a-3f318da7a140]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/1","value":["bar","baz"]}]
	Then the patch result should equal ["foo",["bar","baz"],"sil"]

Scenario: Undescribed scenario [65e31197-e9df-4b7c-9f75-4970091f60c2]
	Given the document {"foo":1,"bar":[1,2,3,4]}
	When I build the patch [{"op":"remove","path":"/bar"}]
	Then the patch result should equal {"foo":1}

Scenario: Undescribed scenario [26065e2e-2d7c-4bda-a0a4-78abf3e73781]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"remove","path":"/baz/0/qux"}]
	Then the patch result should equal {"foo":1,"baz":[{}]}

Scenario: Undescribed scenario [72dd1145-225a-4b70-9ac8-13e841cb9020]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"replace","path":"/foo","value":[1,2,3,4]}]
	Then the patch result should equal {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}

Scenario: Undescribed scenario [843a3eb6-e341-4537-a239-f8172da14462]
	Given the document {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"replace","path":"/baz/0/qux","value":"world"}]
	Then the patch result should equal {"foo":[1,2,3,4],"baz":[{"qux":"world"}]}

Scenario: Undescribed scenario [da666c51-2de1-4ece-9833-86c7963e9d04]
	Given the document ["foo"]
	When I build the patch [{"op":"replace","path":"/0","value":"bar"}]
	Then the patch result should equal ["bar"]

Scenario: Undescribed scenario [e1a3f07c-0b11-4590-946e-0a884776a0fb]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":0}]
	Then the patch result should equal [0]

Scenario: Undescribed scenario [3c223e8e-a7aa-4bd9-bfad-101af3acfe42]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":true}]
	Then the patch result should equal [true]

Scenario: Undescribed scenario [abbec6a6-4489-4956-b262-93afc6dfd1b5]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":false}]
	Then the patch result should equal [false]

Scenario: Undescribed scenario [7585b907-7ad2-4b46-a938-c7003cd3b6fe]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/0","value":null}]
	Then the patch result should equal [null]

Scenario: value in array replace not flattened [1de75a7b-a20e-437c-9ca3-9388d6408edc]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"replace","path":"/1","value":["bar","baz"]}]
	Then the patch result should equal ["foo",["bar","baz"]]

Scenario: replace whole document [9307a5b9-984a-4eff-a32c-3f59cdd2f11e]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"replace","path":"","value":{"baz":"qux"}}]
	Then the patch result should equal {"baz":"qux"}

Scenario: test replace with missing parent key should fail [acf4c911-1b1d-4d39-9e0a-e77a8081a429]
	Given the document {"bar":"baz"}
	When I build the patch [{"op":"replace","path":"/foo/bar","value":false}]
	Then a patch exception should be thrown

Scenario: spurious patch properties [710071cf-80c5-4f36-b7b6-6ecdf58fd9da]
	Given the document {"foo":1}
	When I build the patch [{"op":"test","path":"/foo","value":1,"spurious":1}]
	Then the patch result should equal {"foo":1}

Scenario: null value should be valid obj property [cb1d7135-9f48-4e20-83c6-06d052215198]
	Given the document {"foo":null}
	When I build the patch [{"op":"test","path":"/foo","value":null}]
	Then the patch result should equal {"foo":null}

Scenario: null value should be valid obj property to be replaced with something truthy [8928ae88-9546-4ee0-99b2-6421e08345da]
	Given the document {"foo":null}
	When I build the patch [{"op":"replace","path":"/foo","value":"truthy"}]
	Then the patch result should equal {"foo":"truthy"}

Scenario: null value should be valid obj property to be moved [9ff79df9-0ea7-45c7-b98c-dd5f10ab1d51]
	Given the document {"foo":null}
	When I build the patch [{"op":"move","from":"/foo","path":"/bar"}]
	Then the patch result should equal {"bar":null}

Scenario: null value should be valid obj property to be copied [5fdd9081-d59a-4431-91e9-c64d493d3db2]
	Given the document {"foo":null}
	When I build the patch [{"op":"copy","from":"/foo","path":"/bar"}]
	Then the patch result should equal {"foo":null,"bar":null}

Scenario: null value should be valid obj property to be removed [9a9dc075-6fa0-4297-bbbe-3e44ed402f11]
	Given the document {"foo":null}
	When I build the patch [{"op":"remove","path":"/foo"}]
	Then the patch result should equal {}

Scenario: null value should still be valid obj property replace other value [dadb8779-a051-4471-893f-2602291f59d8]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"replace","path":"/foo","value":null}]
	Then the patch result should equal {"foo":null}

Scenario: test should pass despite rearrangement [0c80de83-5c17-4104-93d1-51327aaa95a4]
	Given the document {"foo":{"foo":1,"bar":2}}
	When I build the patch [{"op":"test","path":"/foo","value":{"bar":2,"foo":1}}]
	Then the patch result should equal {"foo":{"foo":1,"bar":2}}

Scenario: test should pass despite (nested) rearrangement [cf810cbd-aba1-4052-9f08-7eb556473607]
	Given the document {"foo":[{"foo":1,"bar":2}]}
	When I build the patch [{"op":"test","path":"/foo","value":[{"bar":2,"foo":1}]}]
	Then the patch result should equal {"foo":[{"foo":1,"bar":2}]}

Scenario: test should pass - no error [839ec063-76bd-4110-b8f5-9354861b8c97]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	When I build the patch [{"op":"test","path":"/foo","value":{"bar":[1,2,5,4]}}]
	Then the patch result should equal {"foo":{"bar":[1,2,5,4]}}

Scenario: Undescribed scenario [0df126ee-937c-4d6c-80b3-e65bd6054520]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	When I build the patch [{"op":"test","path":"/foo","value":[1,2]}]
	Then a patch exception should be thrown

Scenario: Empty-string element [5b53a1b1-d871-4b4c-921a-85458a04bab2]
	Given the document {"":1}
	When I build the patch [{"op":"test","path":"/","value":1}]
	Then the patch result should equal {"":1}

Scenario: Undescribed scenario [fbb6c9f2-689e-4c70-a216-4a8596d1b4f2]
	Given the document {"foo":["bar","baz"],"":0,"a/b":1,"c%d":2,"e^f":3,"g|h":4,"i\\j":5,"k\u0022l":6," ":7,"m~n":8}
	When I build the patch [{"op":"test","path":"/foo","value":["bar","baz"]},{"op":"test","path":"/foo/0","value":"bar"},{"op":"test","path":"/","value":0},{"op":"test","path":"/a~1b","value":1},{"op":"test","path":"/c%d","value":2},{"op":"test","path":"/e^f","value":3},{"op":"test","path":"/g|h","value":4},{"op":"test","path":"/i\\j","value":5},{"op":"test","path":"/k\u0022l","value":6},{"op":"test","path":"/ ","value":7},{"op":"test","path":"/m~0n","value":8}]
	Then the patch result should equal {"":0," ":7,"a/b":1,"c%d":2,"e^f":3,"foo":["bar","baz"],"g|h":4,"i\\j":5,"k\u0022l":6,"m~n":8}

Scenario: Move to same location has no effect [77bc64d3-0149-44ec-b25a-33057ccc9e83]
	Given the document {"foo":1}
	When I build the patch [{"op":"move","from":"/foo","path":"/foo"}]
	Then the patch result should equal {"foo":1}

Scenario: Undescribed scenario [9824ffb1-575f-44bd-bd40-1f552ca56b81]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"move","from":"/foo","path":"/bar"}]
	Then the patch result should equal {"baz":[{"qux":"hello"}],"bar":1}

Scenario: Undescribed scenario [6ef50438-620d-4e63-8966-701275ae6a27]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	When I build the patch [{"op":"move","from":"/baz/0/qux","path":"/baz/1"}]
	Then the patch result should equal {"baz":[{},"hello"],"bar":1}

Scenario: Undescribed scenario [dd58f357-afb5-4f66-ad95-aa41500d62b0]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	When I build the patch [{"op":"copy","from":"/baz/0","path":"/boo"}]
	Then the patch result should equal {"baz":[{"qux":"hello"}],"bar":1,"boo":{"qux":"hello"}}

Scenario: replacing the root of the document is possible with add [71f46e80-e9cb-4cab-aad6-7e0043ba4426]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"","value":{"baz":"qux"}}]
	Then the patch result should equal {"baz":"qux"}

Scenario: Adding to "/-" adds to the end of the array [2ca47763-d30b-4e9e-b204-c709168d5de5]
	Given the document [1,2]
	When I build the patch [{"op":"add","path":"/-","value":{"foo":["bar","baz"]}}]
	Then the patch result should equal [1,2,{"foo":["bar","baz"]}]

Scenario: Adding to "/-" adds to the end of the array, even n levels down [61fbc775-2191-4ee2-84c8-6772b55b3b5e]
	Given the document [1,2,[3,[4,5]]]
	When I build the patch [{"op":"add","path":"/2/1/-","value":{"foo":["bar","baz"]}}]
	Then the patch result should equal [1,2,[3,[4,5,{"foo":["bar","baz"]}]]]

Scenario: test remove with bad number should fail [d2b184f0-c1e7-4382-ae6f-58e0d45bdf59]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	When I build the patch [{"op":"remove","path":"/baz/1e0/qux"}]
	Then a patch exception should be thrown

Scenario: test remove on array [55fc0a67-31a3-44b4-a214-f61f8d60bd82]
	Given the document [1,2,3,4]
	When I build the patch [{"op":"remove","path":"/0"}]
	Then the patch result should equal [2,3,4]

Scenario: test repeated removes [acbd19b8-1a86-4809-bf4a-9c537dfe5176]
	Given the document [1,2,3,4]
	When I build the patch [{"op":"remove","path":"/1"},{"op":"remove","path":"/2"}]
	Then the patch result should equal [1,3]

Scenario: test remove with bad index should fail [a2f2f410-ba09-4f98-84b8-40b726fbf53a]
	Given the document [1,2,3,4]
	When I build the patch [{"op":"remove","path":"/1e0"}]
	Then a patch exception should be thrown

Scenario: test replace with bad number should fail [40b0e655-13ba-4d13-a6ac-5b6d8c3db4aa]
	Given the document [""]
	When I build the patch [{"op":"replace","path":"/1e0","value":false}]
	Then a patch exception should be thrown

Scenario: test copy with bad number should fail [07b09e32-67e6-447a-b5de-dbf7e7ed2634]
	Given the document {"baz":[1,2,3],"bar":1}
	When I build the patch [{"op":"copy","from":"/baz/1e0","path":"/boo"}]
	Then a patch exception should be thrown

Scenario: test move with bad number should fail [316396ab-2bbb-4249-8f94-59ec8aed0de2]
	Given the document {"foo":1,"baz":[1,2,3,4]}
	When I build the patch [{"op":"move","from":"/baz/1e0","path":"/foo"}]
	Then a patch exception should be thrown

Scenario: test add with bad number should fail [bf2a3488-ed23-46d3-bf1c-a6ff7ebe1d32]
	Given the document ["foo","sil"]
	When I build the patch [{"op":"add","path":"/1e0","value":"bar"}]
	Then a patch exception should be thrown

Scenario: missing 'path' parameter [44ee32a3-d9f3-466a-8ba5-ea10d1585457]
	Given the document {}
	When I build the patch [{"op":"add","value":"bar"}]
	Then a patch exception should be thrown

Scenario: 'path' parameter with null value [5d7130c2-3455-4ec1-afab-d01946902536]
	Given the document {}
	When I build the patch [{"op":"add","path":null,"value":"bar"}]
	Then a patch exception should be thrown

Scenario: invalid JSON Pointer token [0b2b55f3-440d-4626-b596-ffae4aefb7ba]
	Given the document {}
	When I build the patch [{"op":"add","path":"foo","value":"bar"}]
	Then a patch exception should be thrown

Scenario: missing 'value' parameter to add [cb1d87c5-e607-4090-a90f-d7fa9fc71046]
	Given the document [1]
	When I build the patch [{"op":"add","path":"/-"}]
	Then a patch exception should be thrown

Scenario: missing 'value' parameter to replace [49cc0d8c-2b5b-4312-a8c1-8b2fcdbdc58b]
	Given the document [1]
	When I build the patch [{"op":"replace","path":"/0"}]
	Then a patch exception should be thrown

Scenario: missing 'value' parameter to test [1a1188a9-53d5-499e-8afe-cdfce17bfaf0]
	Given the document [null]
	When I build the patch [{"op":"test","path":"/0"}]
	Then a patch exception should be thrown

Scenario: missing value parameter to test - where undef is falsy [9ff17a35-cbaf-4c60-af5c-ad2e49e2fc9b]
	Given the document [false]
	When I build the patch [{"op":"test","path":"/0"}]
	Then a patch exception should be thrown

Scenario: missing from parameter to copy [0e846f82-f9c0-400f-a5a0-d15faf0e755e]
	Given the document [1]
	When I build the patch [{"op":"copy","path":"/-"}]
	Then a patch exception should be thrown

Scenario: missing from location to copy [25647aba-cfd3-4221-acad-33057037f80f]
	Given the document {"foo":1}
	When I build the patch [{"op":"copy","from":"/bar","path":"/foo"}]
	Then a patch exception should be thrown

Scenario: missing from parameter to move [1bfc98a8-b6af-4f45-b718-3dfb5ace9774]
	Given the document {"foo":1}
	When I build the patch [{"op":"move","path":""}]
	Then a patch exception should be thrown

Scenario: missing from location to move [a626e8cc-0c1b-447d-b699-1644c5636274]
	Given the document {"foo":1}
	When I build the patch [{"op":"move","from":"/bar","path":"/foo"}]
	Then a patch exception should be thrown

Scenario: unrecognized op should fail [c189f15a-375a-406f-8391-1e28b26b99c7]
	Given the document {"foo":1}
	When I build the patch [{"op":"spam","path":"/foo","value":1}]
	Then a patch exception should be thrown

Scenario: test with bad array number that has leading zeros [5cded2ca-773c-44eb-ae99-c0fdb390ddfb]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"test","path":"/00","value":"foo"}]
	Then a patch exception should be thrown

Scenario: test with bad array number that has leading zeros [2e3c414a-6203-4c2a-b3f2-979817a54c96]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"test","path":"/01","value":"bar"}]
	Then a patch exception should be thrown

Scenario: Removing nonexistent field [9a5de0b3-b3aa-4319-8bd3-abba1d96ad26]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"remove","path":"/baz"}]
	Then a patch exception should be thrown

Scenario: Removing deep nonexistent path [97b2630a-f8f3-4d60-8ecc-0e6ca4aac331]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"remove","path":"/missing1/missing2"}]
	Then a patch exception should be thrown

Scenario: Removing nonexistent index [4301d8fe-4045-4c10-8c50-3849585925a1]
	Given the document ["foo","bar"]
	When I build the patch [{"op":"remove","path":"/2"}]
	Then a patch exception should be thrown

Scenario: Patch with different capitalisation than doc [f48570cb-716a-4142-ae25-1e7cf23c1190]
	Given the document {"foo":"bar"}
	When I build the patch [{"op":"add","path":"/FOO","value":"BAR"}]
	Then the patch result should equal {"foo":"bar","FOO":"BAR"}
