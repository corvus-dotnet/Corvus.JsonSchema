
Feature: tests

Scenario: empty list, empty docs [a93b876d-a689-4509-9399-d95297ecc1da]
	Given the document {}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: empty patch list [5fa809a2-c35c-4626-8cd4-46d4c5eb3b8a]
	Given the document {"foo":1}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: rearrangements OK? [4a4dcebf-5931-4c90-82c6-55ecd61c7252]
	Given the document {"foo":1,"bar":2}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"bar":2,"foo":1}

Scenario: rearrangements OK?  How about one level down ... array [c9d4e4d7-442e-4ed1-99dc-dcf660b07818]
	Given the document [{"foo":1,"bar":2}]
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal [{"bar":2,"foo":1}]

Scenario: rearrangements OK?  How about one level down... [87dd0009-396f-40f6-9818-c84e9d671ece]
	Given the document {"foo":{"foo":1,"bar":2}}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":2,"foo":1}}

Scenario: add replaces any existing field [a05dccb6-5c3b-42ce-b3a2-7ef8a395a35b]
	Given the document {"foo":null}
	And the patch [{"op":"add","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: toplevel array [80193eea-6adb-4090-8b65-c22ea2a99c36]
	Given the document []
	And the patch [{"op":"add","path":"/0","value":"foo"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo"]

Scenario: toplevel array, no change [9d0d6e85-f100-493b-befa-8e11969213ab]
	Given the document ["foo"]
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal ["foo"]

Scenario: toplevel object, numeric string [c5f13054-53b3-4aae-bb23-1ce006972e8f]
	Given the document {}
	And the patch [{"op":"add","path":"/foo","value":"1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"1"}

Scenario: toplevel object, integer [ca209dfd-cfb0-4a33-81ca-4230a1d0e42a]
	Given the document {}
	And the patch [{"op":"add","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: replace object document with array document? [3c5b40e8-e1ca-47a0-a64b-0a58be9d5c22]
	Given the document {}
	And the patch [{"op":"add","path":"","value":[]}]
	When I apply the patch to the document
	Then the transformed document should equal []

Scenario: replace array document with object document? [8a6cf38c-6395-4c51-a155-8f62a2bd6742]
	Given the document []
	And the patch [{"op":"add","path":"","value":{}}]
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: append to root array document? [0c9c89e3-e271-4924-b3e2-279a4acd29ad]
	Given the document []
	And the patch [{"op":"add","path":"/-","value":"hi"}]
	When I apply the patch to the document
	Then the transformed document should equal ["hi"]

Scenario: Add, / target [2fa6b2da-4e13-4696-8eca-87bb72cf5dda]
	Given the document {}
	And the patch [{"op":"add","path":"/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"":1}

Scenario: Add, /foo/ deep target (trailing slash) [36fff972-aca8-48e0-9761-168f7a87841f]
	Given the document {"foo":{}}
	And the patch [{"op":"add","path":"/foo/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"":1}}

Scenario: Add composite value at top level [4abb5172-b070-4095-8222-b965d41f7972]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":[1,2]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":[1,2]}

Scenario: Add into composite value [7c9b8475-cdfd-492b-a7f4-eeecda86a845]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"add","path":"/baz/0/foo","value":"world"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"baz":[{"qux":"hello","foo":"world"}]}

Scenario: Undescribed scenario [398cd589-4c74-4dab-9ad4-e816d3dcace4]
	Given the document {"bar":[1,2]}
	And the patch [{"op":"add","path":"/bar/8","value":"5"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [0453efa9-e201-46ce-a24a-61f9c0c17100]
	Given the document {"bar":[1,2]}
	And the patch [{"op":"add","path":"/bar/-1","value":"5"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [d9a7ad47-8576-405b-9112-ab1aaf64a8fe]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":true}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":true}

Scenario: Undescribed scenario [952f0780-2feb-44e6-8e22-bf8d1083c27d]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":false}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":false}

Scenario: Undescribed scenario [8f76a263-24ee-4e17-8f0f-bbc8f0bcfce2]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":null}

Scenario: 0 can be an array index or object element name [cdda4f9c-4084-49a5-b9b4-4c586c07129d]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"0":"bar"}

Scenario: Undescribed scenario [ec6b9545-cee8-4031-84be-8559afd88ec5]
	Given the document ["foo"]
	And the patch [{"op":"add","path":"/1","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","bar"]

Scenario: Undescribed scenario [eeb9b237-74ce-4309-96c1-098c1b0e2864]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","bar","sil"]

Scenario: Undescribed scenario [7f199b20-b341-45a5-aa03-c5a44d00ff8d]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["bar","foo","sil"]

Scenario: push item to array via last index + 1 [a61d0766-cbf1-46d8-97bd-b1e3d320b2a1]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/2","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","sil","bar"]

Scenario: add item to array at index > length should fail [8bcc8ab0-1086-4cf3-974d-eba45538c40b]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/3","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test against implementation-specific numeric parsing [d73c94f5-f4b1-4b10-aebd-9e434e3c4c4a]
	Given the document {"1e0":"foo"}
	And the patch [{"op":"test","path":"/1e0","value":"foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"1e0":"foo"}

Scenario: test with bad number should fail [1b524118-e6ac-4da6-bfd5-e3ef0901c3c9]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/1e0","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [0ea55319-9b0a-443a-9a34-dd54ff9615ed]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/bar","value":42}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: value in array add not flattened [1eed4a25-fa33-4a79-941f-64f8abd38de6]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1","value":["bar","baz"]}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo",["bar","baz"],"sil"]

Scenario: Undescribed scenario [93f1e141-d360-42c4-9551-9d17b2cc6f21]
	Given the document {"foo":1,"bar":[1,2,3,4]}
	And the patch [{"op":"remove","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: Undescribed scenario [0441b4c1-fd3f-4889-a8e4-25746689e59a]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"remove","path":"/baz/0/qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"baz":[{}]}

Scenario: Undescribed scenario [4494c932-0e77-49f2-9fd0-a172da108586]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"replace","path":"/foo","value":[1,2,3,4]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}

Scenario: Undescribed scenario [9d576afc-5dfc-4230-aa86-b821853416a3]
	Given the document {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}
	And the patch [{"op":"replace","path":"/baz/0/qux","value":"world"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[1,2,3,4],"baz":[{"qux":"world"}]}

Scenario: Undescribed scenario [7f45700a-9571-4511-8685-b35a866ef48c]
	Given the document ["foo"]
	And the patch [{"op":"replace","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["bar"]

Scenario: Undescribed scenario [64bfc017-729f-4289-b3fb-3eca1b847486]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":0}]
	When I apply the patch to the document
	Then the transformed document should equal [0]

Scenario: Undescribed scenario [9006d68f-3e1f-4e34-a20f-bfc4d0951e7b]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":true}]
	When I apply the patch to the document
	Then the transformed document should equal [true]

Scenario: Undescribed scenario [4e9405c2-4cfd-4ab6-a03e-be61f5be5d98]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":false}]
	When I apply the patch to the document
	Then the transformed document should equal [false]

Scenario: Undescribed scenario [1ee933c5-1f68-43dc-b26a-0e76da1e17cd]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal [null]

Scenario: value in array replace not flattened [a73d7685-71d1-431c-91f8-98209107189a]
	Given the document ["foo","sil"]
	And the patch [{"op":"replace","path":"/1","value":["bar","baz"]}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo",["bar","baz"]]

Scenario: replace whole document [87a62a59-7262-4f88-b488-220c5739a437]
	Given the document {"foo":"bar"}
	And the patch [{"op":"replace","path":"","value":{"baz":"qux"}}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux"}

Scenario: test replace with missing parent key should fail [154f3711-4af3-43c1-a27a-1cc934b0cd71]
	Given the document {"bar":"baz"}
	And the patch [{"op":"replace","path":"/foo/bar","value":false}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: spurious patch properties [9f014c76-a20d-4d08-8b6f-8b80ee54fb6d]
	Given the document {"foo":1}
	And the patch [{"op":"test","path":"/foo","value":1,"spurious":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: null value should be valid obj property [3d42ad57-502d-4840-99de-ecffd29e7172]
	Given the document {"foo":null}
	And the patch [{"op":"test","path":"/foo","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null}

Scenario: null value should be valid obj property to be replaced with something truthy [81d0bd70-d965-42c5-9cba-a170d6112c7b]
	Given the document {"foo":null}
	And the patch [{"op":"replace","path":"/foo","value":"truthy"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"truthy"}

Scenario: null value should be valid obj property to be moved [73122530-cea9-43d4-9938-44a79b4d1e4d]
	Given the document {"foo":null}
	And the patch [{"op":"move","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"bar":null}

Scenario: null value should be valid obj property to be copied [45a24e21-8131-41f2-8693-b4f0dda061c8]
	Given the document {"foo":null}
	And the patch [{"op":"copy","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null,"bar":null}

Scenario: null value should be valid obj property to be removed [ca6e1be6-0d6a-4b89-92fe-d349185fd9af]
	Given the document {"foo":null}
	And the patch [{"op":"remove","path":"/foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: null value should still be valid obj property replace other value [b61208e8-0dbe-40d0-ba10-c9006e8d786d]
	Given the document {"foo":"bar"}
	And the patch [{"op":"replace","path":"/foo","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null}

Scenario: test should pass despite rearrangement [92c1f62a-69cf-45c0-aac6-699e8b3a7e06]
	Given the document {"foo":{"foo":1,"bar":2}}
	And the patch [{"op":"test","path":"/foo","value":{"bar":2,"foo":1}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"foo":1,"bar":2}}

Scenario: test should pass despite (nested) rearrangement [164477fb-294f-47a3-9fd2-0cb4f9c7a496]
	Given the document {"foo":[{"foo":1,"bar":2}]}
	And the patch [{"op":"test","path":"/foo","value":[{"bar":2,"foo":1}]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[{"foo":1,"bar":2}]}

Scenario: test should pass - no error [859c903d-351a-4d13-980f-ba8271aedab9]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	And the patch [{"op":"test","path":"/foo","value":{"bar":[1,2,5,4]}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":[1,2,5,4]}}

Scenario: Undescribed scenario [cbcc2934-29f9-460e-a9dc-2a6207a9117b]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	And the patch [{"op":"test","path":"/foo","value":[1,2]}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Empty-string element [37fa6a1a-ac9a-4607-bdc9-80fe69c0d1f0]
	Given the document {"":1}
	And the patch [{"op":"test","path":"/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"":1}

Scenario: Undescribed scenario [977e7eb4-e8d2-41a1-b59e-24eb0e4477b6]
	Given the document {"foo":["bar","baz"],"":0,"a/b":1,"c%d":2,"e^f":3,"g|h":4,"i\\j":5,"k\u0022l":6," ":7,"m~n":8}
	And the patch [{"op":"test","path":"/foo","value":["bar","baz"]},{"op":"test","path":"/foo/0","value":"bar"},{"op":"test","path":"/","value":0},{"op":"test","path":"/a~1b","value":1},{"op":"test","path":"/c%d","value":2},{"op":"test","path":"/e^f","value":3},{"op":"test","path":"/g|h","value":4},{"op":"test","path":"/i\\j","value":5},{"op":"test","path":"/k\u0022l","value":6},{"op":"test","path":"/ ","value":7},{"op":"test","path":"/m~0n","value":8}]
	When I apply the patch to the document
	Then the transformed document should equal {"":0," ":7,"a/b":1,"c%d":2,"e^f":3,"foo":["bar","baz"],"g|h":4,"i\\j":5,"k\u0022l":6,"m~n":8}

Scenario: Move to same location has no effect [6a4b5b83-68a1-448f-851b-5ff795565b01]
	Given the document {"foo":1}
	And the patch [{"op":"move","from":"/foo","path":"/foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: Undescribed scenario [246d2bd9-5168-426d-994c-4764433d0c1e]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"move","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{"qux":"hello"}],"bar":1}

Scenario: Undescribed scenario [e994136d-31e9-403a-85d6-085d6ff71b33]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	And the patch [{"op":"move","from":"/baz/0/qux","path":"/baz/1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{},"hello"],"bar":1}

Scenario: Undescribed scenario [5143e15e-d402-4762-bb5c-bd96e4ff08a5]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	And the patch [{"op":"copy","from":"/baz/0","path":"/boo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{"qux":"hello"}],"bar":1,"boo":{"qux":"hello"}}

Scenario: replacing the root of the document is possible with add [8c22b611-0b4d-47e5-94df-5a77d14f7a32]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"","value":{"baz":"qux"}}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux"}

Scenario: Adding to "/-" adds to the end of the array [7fa8e93a-88d0-49a6-af78-015fb3974577]
	Given the document [1,2]
	And the patch [{"op":"add","path":"/-","value":{"foo":["bar","baz"]}}]
	When I apply the patch to the document
	Then the transformed document should equal [1,2,{"foo":["bar","baz"]}]

Scenario: Adding to "/-" adds to the end of the array, even n levels down [a9ba53c4-0aac-43a4-a7f1-3c1c5d668085]
	Given the document [1,2,[3,[4,5]]]
	And the patch [{"op":"add","path":"/2/1/-","value":{"foo":["bar","baz"]}}]
	When I apply the patch to the document
	Then the transformed document should equal [1,2,[3,[4,5,{"foo":["bar","baz"]}]]]

Scenario: test remove with bad number should fail [b76f5f1d-3cd9-4707-ac75-c9cceb1e45d8]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"remove","path":"/baz/1e0/qux"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test remove on array [3960368d-eb72-4ad0-a3b3-9a4862707d66]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/0"}]
	When I apply the patch to the document
	Then the transformed document should equal [2,3,4]

Scenario: test repeated removes [1efb1102-2a8d-4b47-a05a-a631f8d271fb]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/1"},{"op":"remove","path":"/2"}]
	When I apply the patch to the document
	Then the transformed document should equal [1,3]

Scenario: test remove with bad index should fail [fe02ea1b-7151-4498-af3f-f98c55e3fc84]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/1e0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test replace with bad number should fail [e733103e-5bb8-42f4-8c12-0fcd737a7622]
	Given the document [""]
	And the patch [{"op":"replace","path":"/1e0","value":false}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test copy with bad number should fail [f73a7b82-3522-42d5-821a-e35a8b494c60]
	Given the document {"baz":[1,2,3],"bar":1}
	And the patch [{"op":"copy","from":"/baz/1e0","path":"/boo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test move with bad number should fail [26efffe3-8629-4bee-80b3-a65ca498555f]
	Given the document {"foo":1,"baz":[1,2,3,4]}
	And the patch [{"op":"move","from":"/baz/1e0","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test add with bad number should fail [c5e8e082-5b57-4a91-bc9c-f899c18d4253]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1e0","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'path' parameter [89d90967-8bad-4285-8ceb-4ac93acd451c]
	Given the document {}
	And the patch [{"op":"add","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: 'path' parameter with null value [dd12add4-e0c1-4325-a5fa-b055b04ad8fc]
	Given the document {}
	And the patch [{"op":"add","path":null,"value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: invalid JSON Pointer token [2ff0ecba-6aeb-44bb-8807-e1e80290540c]
	Given the document {}
	And the patch [{"op":"add","path":"foo","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to add [91a0fb7f-abe0-4078-adcc-0b49ac8a99be]
	Given the document [1]
	And the patch [{"op":"add","path":"/-"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to replace [66ca4e17-7f42-469f-b0ef-7f2704de5851]
	Given the document [1]
	And the patch [{"op":"replace","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to test [a67c0af5-58f4-4d5a-b874-706c647ad553]
	Given the document [null]
	And the patch [{"op":"test","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing value parameter to test - where undef is falsy [7fd48c28-834d-4b83-ad56-f70cf4bf19a0]
	Given the document [false]
	And the patch [{"op":"test","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from parameter to copy [0618cc43-db35-4607-8924-98fdf6cdd1dd]
	Given the document [1]
	And the patch [{"op":"copy","path":"/-"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from location to copy [ffab4fb7-8492-4f8e-933f-8d82662fb179]
	Given the document {"foo":1}
	And the patch [{"op":"copy","from":"/bar","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from parameter to move [bf721e67-3dbc-4f21-9585-0ad198a7bac9]
	Given the document {"foo":1}
	And the patch [{"op":"move","path":""}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from location to move [e62e5b0b-7203-4726-8ce7-2518da2872ab]
	Given the document {"foo":1}
	And the patch [{"op":"move","from":"/bar","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: unrecognized op should fail [cd6f0ae6-942a-42ff-8f24-d581f0f15653]
	Given the document {"foo":1}
	And the patch [{"op":"spam","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test with bad array number that has leading zeros [b3f07c0e-3ee2-40c2-8c24-d61cb281d2d4]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/00","value":"foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test with bad array number that has leading zeros [e4669067-de77-4699-a177-d5275e5aef7f]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/01","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing nonexistent field [2376282f-328f-431e-9d03-bbcb6cf97e03]
	Given the document {"foo":"bar"}
	And the patch [{"op":"remove","path":"/baz"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing deep nonexistent path [acf2040b-a8aa-4eb0-8bd6-fa4f4eefddb2]
	Given the document {"foo":"bar"}
	And the patch [{"op":"remove","path":"/missing1/missing2"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing nonexistent index [f46b5c72-39c1-463e-aae0-f68613a711cf]
	Given the document ["foo","bar"]
	And the patch [{"op":"remove","path":"/2"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Patch with different capitalisation than doc [15925c9d-5fde-407a-8578-4064280ba66c]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/FOO","value":"BAR"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","FOO":"BAR"}
