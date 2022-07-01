
Feature: tests

Scenario: empty list, empty docs [f0eb19ea-04b3-4ceb-8c01-a5e8e02fa4c1]
	Given the document {}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: empty patch list [48aeba26-ca81-4660-8ea0-58cdb8428221]
	Given the document {"foo":1}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: rearrangements OK? [5c1322d8-7217-40b0-8ec2-59ca8c79d8ad]
	Given the document {"foo":1,"bar":2}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"bar":2,"foo":1}

Scenario: rearrangements OK?  How about one level down ... array [020aad9c-63ba-4cc8-870a-fbbdf99b8990]
	Given the document [{"foo":1,"bar":2}]
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal [{"bar":2,"foo":1}]

Scenario: rearrangements OK?  How about one level down... [ea37e8df-abbb-4f94-807b-767dc150d859]
	Given the document {"foo":{"foo":1,"bar":2}}
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":2,"foo":1}}

Scenario: add replaces any existing field [f12b12b7-e24e-4976-b471-af8db93b7b56]
	Given the document {"foo":null}
	And the patch [{"op":"add","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: toplevel array [0a994399-046a-4386-a881-f5f5ed633a7c]
	Given the document []
	And the patch [{"op":"add","path":"/0","value":"foo"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo"]

Scenario: toplevel array, no change [c339cc7d-a4f9-4e71-a7e9-d77c155889f7]
	Given the document ["foo"]
	And the patch []
	When I apply the patch to the document
	Then the transformed document should equal ["foo"]

Scenario: toplevel object, numeric string [a55e0ab6-4c9c-4d3c-9dce-5519e7cebe05]
	Given the document {}
	And the patch [{"op":"add","path":"/foo","value":"1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"1"}

Scenario: toplevel object, integer [9cb78587-1ba0-4bdb-a150-dca88e47a0df]
	Given the document {}
	And the patch [{"op":"add","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: replace object document with array document? [dca4589f-cba5-4165-b1d7-195ac1195ee7]
	Given the document {}
	And the patch [{"op":"add","path":"","value":[]}]
	When I apply the patch to the document
	Then the transformed document should equal []

Scenario: replace array document with object document? [5a5966d2-9684-4107-8842-4b13438beece]
	Given the document []
	And the patch [{"op":"add","path":"","value":{}}]
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: append to root array document? [23d3cd03-5407-478a-adea-2fdf5fbab836]
	Given the document []
	And the patch [{"op":"add","path":"/-","value":"hi"}]
	When I apply the patch to the document
	Then the transformed document should equal ["hi"]

Scenario: Add, / target [309d3402-e09e-48c2-8d9b-d79153d0937b]
	Given the document {}
	And the patch [{"op":"add","path":"/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"":1}

Scenario: Add, /foo/ deep target (trailing slash) [3e5bc413-6885-443f-82a9-b44274bf8779]
	Given the document {"foo":{}}
	And the patch [{"op":"add","path":"/foo/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"":1}}

Scenario: Add composite value at top level [9189f212-2b0a-4b30-8ca6-79a576e323d3]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":[1,2]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":[1,2]}

Scenario: Add into composite value [9a80433a-72f6-4716-a6d4-49fb3863310c]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"add","path":"/baz/0/foo","value":"world"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"baz":[{"qux":"hello","foo":"world"}]}

Scenario: Undescribed scenario [f1de5857-a47f-415d-8bdf-3c84f0520186]
	Given the document {"bar":[1,2]}
	And the patch [{"op":"add","path":"/bar/8","value":"5"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [bf30fe75-144a-4d52-afa0-e27ad04ea752]
	Given the document {"bar":[1,2]}
	And the patch [{"op":"add","path":"/bar/-1","value":"5"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [70e96c6b-9aea-4942-8968-5f90ab208fee]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":true}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":true}

Scenario: Undescribed scenario [3c78de7b-061f-4665-bd34-4134d3438ca5]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":false}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":false}

Scenario: Undescribed scenario [7f1c8c33-1822-4514-974d-10ae36e3f2a5]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/bar","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"bar":null}

Scenario: 0 can be an array index or object element name [7ee57dee-fc9f-4452-95d4-0583952734b3]
	Given the document {"foo":1}
	And the patch [{"op":"add","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"0":"bar"}

Scenario: Undescribed scenario [b7a35026-2113-4c98-a50e-c27a2723debe]
	Given the document ["foo"]
	And the patch [{"op":"add","path":"/1","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","bar"]

Scenario: Undescribed scenario [4c800c2f-925f-48ea-9a69-0b2a330d4a4f]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","bar","sil"]

Scenario: Undescribed scenario [b330f4f7-ebf2-4ebd-972f-57aed965168c]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["bar","foo","sil"]

Scenario: push item to array via last index + 1 [8b1caa65-7a40-4748-a07a-afe0d414d553]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/2","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo","sil","bar"]

Scenario: add item to array at index > length should fail [7d997dbe-8ef1-4b90-beab-a68dcfe7d93b]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/3","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test against implementation-specific numeric parsing [f27c5741-1e3e-4665-aad7-28e419015fc4]
	Given the document {"1e0":"foo"}
	And the patch [{"op":"test","path":"/1e0","value":"foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"1e0":"foo"}

Scenario: test with bad number should fail [f1255270-dbaf-459e-a21e-cb8b79bc6fe4]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/1e0","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Undescribed scenario [b9318e23-51d7-4b47-9873-79978778ddb6]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/bar","value":42}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: value in array add not flattened [0398e8eb-d578-4b2c-a4e5-2f9f8bf6c9cc]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1","value":["bar","baz"]}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo",["bar","baz"],"sil"]

Scenario: Undescribed scenario [120a1a66-855f-4325-9d89-c3c43a6bb0ed]
	Given the document {"foo":1,"bar":[1,2,3,4]}
	And the patch [{"op":"remove","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: Undescribed scenario [9e9e8952-97ab-4369-817c-2487dce494b8]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"remove","path":"/baz/0/qux"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1,"baz":[{}]}

Scenario: Undescribed scenario [2792924d-184d-4652-8e5b-8f566e84a970]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"replace","path":"/foo","value":[1,2,3,4]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}

Scenario: Undescribed scenario [a08a7283-2e3b-4298-ab56-d51839fb9904]
	Given the document {"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}
	And the patch [{"op":"replace","path":"/baz/0/qux","value":"world"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[1,2,3,4],"baz":[{"qux":"world"}]}

Scenario: Undescribed scenario [b896e7e6-a43b-4058-98da-56933b6735fb]
	Given the document ["foo"]
	And the patch [{"op":"replace","path":"/0","value":"bar"}]
	When I apply the patch to the document
	Then the transformed document should equal ["bar"]

Scenario: Undescribed scenario [cc0e2978-f6c3-47fe-adf3-dc2b85880dfd]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":0}]
	When I apply the patch to the document
	Then the transformed document should equal [0]

Scenario: Undescribed scenario [f917fdd1-6f19-4b7d-b3cf-e4c37af0ef96]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":true}]
	When I apply the patch to the document
	Then the transformed document should equal [true]

Scenario: Undescribed scenario [de065511-87fe-4e48-9530-00d447c362cf]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":false}]
	When I apply the patch to the document
	Then the transformed document should equal [false]

Scenario: Undescribed scenario [b58945fd-c4cb-44a0-bac7-aca3d2188ef6]
	Given the document [""]
	And the patch [{"op":"replace","path":"/0","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal [null]

Scenario: value in array replace not flattened [53dfab89-5800-46c2-88c3-6e5e35fe8a9e]
	Given the document ["foo","sil"]
	And the patch [{"op":"replace","path":"/1","value":["bar","baz"]}]
	When I apply the patch to the document
	Then the transformed document should equal ["foo",["bar","baz"]]

Scenario: replace whole document [77d375d9-f965-48b5-b94d-13fd86a6e944]
	Given the document {"foo":"bar"}
	And the patch [{"op":"replace","path":"","value":{"baz":"qux"}}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux"}

Scenario: test replace with missing parent key should fail [94578516-a526-4101-952e-de41a53030bf]
	Given the document {"bar":"baz"}
	And the patch [{"op":"replace","path":"/foo/bar","value":false}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: spurious patch properties [ba6d83d4-1841-469e-81cf-b262735516a8]
	Given the document {"foo":1}
	And the patch [{"op":"test","path":"/foo","value":1,"spurious":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: null value should be valid obj property [707597af-5dd9-4498-8c1b-5d5994f97dac]
	Given the document {"foo":null}
	And the patch [{"op":"test","path":"/foo","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null}

Scenario: null value should be valid obj property to be replaced with something truthy [a1c3d49f-2757-4cb6-9ce5-ae7d51ce14f7]
	Given the document {"foo":null}
	And the patch [{"op":"replace","path":"/foo","value":"truthy"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"truthy"}

Scenario: null value should be valid obj property to be moved [ae6c8fe2-1775-466b-b24f-89bf290d8066]
	Given the document {"foo":null}
	And the patch [{"op":"move","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"bar":null}

Scenario: null value should be valid obj property to be copied [6b04c8c4-a1f6-40fa-8a14-0da5fd3bcc3f]
	Given the document {"foo":null}
	And the patch [{"op":"copy","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null,"bar":null}

Scenario: null value should be valid obj property to be removed [fb33dcd2-858a-4bd7-95b4-679b6d68518a]
	Given the document {"foo":null}
	And the patch [{"op":"remove","path":"/foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {}

Scenario: null value should still be valid obj property replace other value [b2816189-0c0d-47a3-825c-409d82e9b9f9]
	Given the document {"foo":"bar"}
	And the patch [{"op":"replace","path":"/foo","value":null}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":null}

Scenario: test should pass despite rearrangement [4d45156c-f596-4afe-b0b2-e3da0f1e9641]
	Given the document {"foo":{"foo":1,"bar":2}}
	And the patch [{"op":"test","path":"/foo","value":{"bar":2,"foo":1}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"foo":1,"bar":2}}

Scenario: test should pass despite (nested) rearrangement [d78fbf86-9a00-473d-93fb-28bda0c06df7]
	Given the document {"foo":[{"foo":1,"bar":2}]}
	And the patch [{"op":"test","path":"/foo","value":[{"bar":2,"foo":1}]}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":[{"foo":1,"bar":2}]}

Scenario: test should pass - no error [3150b7b1-064f-4ba7-a55e-881767eb1d75]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	And the patch [{"op":"test","path":"/foo","value":{"bar":[1,2,5,4]}}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":{"bar":[1,2,5,4]}}

Scenario: Undescribed scenario [0d8e8abd-6a1c-4fc5-bb33-b22d908b04cb]
	Given the document {"foo":{"bar":[1,2,5,4]}}
	And the patch [{"op":"test","path":"/foo","value":[1,2]}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Empty-string element [afdc0c00-d9b6-4f8f-b13d-77b2b4d77411]
	Given the document {"":1}
	And the patch [{"op":"test","path":"/","value":1}]
	When I apply the patch to the document
	Then the transformed document should equal {"":1}

Scenario: Undescribed scenario [ad60b7fc-99fc-4c67-b22d-8c7799046bca]
	Given the document {"foo":["bar","baz"],"":0,"a/b":1,"c%d":2,"e^f":3,"g|h":4,"i\\j":5,"k\u0022l":6," ":7,"m~n":8}
	And the patch [{"op":"test","path":"/foo","value":["bar","baz"]},{"op":"test","path":"/foo/0","value":"bar"},{"op":"test","path":"/","value":0},{"op":"test","path":"/a~1b","value":1},{"op":"test","path":"/c%d","value":2},{"op":"test","path":"/e^f","value":3},{"op":"test","path":"/g|h","value":4},{"op":"test","path":"/i\\j","value":5},{"op":"test","path":"/k\u0022l","value":6},{"op":"test","path":"/ ","value":7},{"op":"test","path":"/m~0n","value":8}]
	When I apply the patch to the document
	Then the transformed document should equal {"":0," ":7,"a/b":1,"c%d":2,"e^f":3,"foo":["bar","baz"],"g|h":4,"i\\j":5,"k\u0022l":6,"m~n":8}

Scenario: Move to same location has no effect [fcf3cac7-ee63-4937-9ee2-a820e675c40e]
	Given the document {"foo":1}
	And the patch [{"op":"move","from":"/foo","path":"/foo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":1}

Scenario: Undescribed scenario [7630fff0-a530-48aa-90d4-2ae39d92be6d]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"move","from":"/foo","path":"/bar"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{"qux":"hello"}],"bar":1}

Scenario: Undescribed scenario [db604aa1-4188-44a6-848f-bc09c9bda25c]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	And the patch [{"op":"move","from":"/baz/0/qux","path":"/baz/1"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{},"hello"],"bar":1}

Scenario: Undescribed scenario [f7a8454e-6575-4510-a8f5-76be4d9a6dd1]
	Given the document {"baz":[{"qux":"hello"}],"bar":1}
	And the patch [{"op":"copy","from":"/baz/0","path":"/boo"}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":[{"qux":"hello"}],"bar":1,"boo":{"qux":"hello"}}

Scenario: replacing the root of the document is possible with add [21b3a435-d7b5-4a26-a808-53cbe9419865]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"","value":{"baz":"qux"}}]
	When I apply the patch to the document
	Then the transformed document should equal {"baz":"qux"}

Scenario: Adding to "/-" adds to the end of the array [2f958b9a-0462-4174-a22e-48197a895938]
	Given the document [1,2]
	And the patch [{"op":"add","path":"/-","value":{"foo":["bar","baz"]}}]
	When I apply the patch to the document
	Then the transformed document should equal [1,2,{"foo":["bar","baz"]}]

Scenario: Adding to "/-" adds to the end of the array, even n levels down [b6df6048-4893-498d-9eb3-15c7f0bd12a0]
	Given the document [1,2,[3,[4,5]]]
	And the patch [{"op":"add","path":"/2/1/-","value":{"foo":["bar","baz"]}}]
	When I apply the patch to the document
	Then the transformed document should equal [1,2,[3,[4,5,{"foo":["bar","baz"]}]]]

Scenario: test remove with bad number should fail [df746b5b-ed70-4024-bb70-688f62074079]
	Given the document {"foo":1,"baz":[{"qux":"hello"}]}
	And the patch [{"op":"remove","path":"/baz/1e0/qux"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test remove on array [18900ae9-ab24-4063-96b2-c5e9f7ec29ac]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/0"}]
	When I apply the patch to the document
	Then the transformed document should equal [2,3,4]

Scenario: test repeated removes [30e3b2c7-be84-4ffb-bf4f-ca15dbcf694b]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/1"},{"op":"remove","path":"/2"}]
	When I apply the patch to the document
	Then the transformed document should equal [1,3]

Scenario: test remove with bad index should fail [a3391957-9fe9-4191-993e-aaf2efe0295e]
	Given the document [1,2,3,4]
	And the patch [{"op":"remove","path":"/1e0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test replace with bad number should fail [bc8ccbdb-f568-4843-91c9-0d6da6670f3a]
	Given the document [""]
	And the patch [{"op":"replace","path":"/1e0","value":false}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test copy with bad number should fail [c11e8371-f809-4c3e-9526-e54f3e57ef93]
	Given the document {"baz":[1,2,3],"bar":1}
	And the patch [{"op":"copy","from":"/baz/1e0","path":"/boo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test move with bad number should fail [62d4e48d-cc2b-4bbe-8fc2-0fd904863b26]
	Given the document {"foo":1,"baz":[1,2,3,4]}
	And the patch [{"op":"move","from":"/baz/1e0","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test add with bad number should fail [17446dab-0243-4ed8-8508-3c1601e64635]
	Given the document ["foo","sil"]
	And the patch [{"op":"add","path":"/1e0","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'path' parameter [ade94665-c59a-4a95-bded-634464273d46]
	Given the document {}
	And the patch [{"op":"add","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: 'path' parameter with null value [959889fa-c89e-453b-8be0-1546a5767342]
	Given the document {}
	And the patch [{"op":"add","path":null,"value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: invalid JSON Pointer token [a159ff73-67b7-487f-ab8a-d2752c10ad41]
	Given the document {}
	And the patch [{"op":"add","path":"foo","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to add [68a2b870-6b39-4aa8-b011-abfcd9cd22a3]
	Given the document [1]
	And the patch [{"op":"add","path":"/-"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to replace [64caf814-dd4e-445f-8bc4-a7c4a4908283]
	Given the document [1]
	And the patch [{"op":"replace","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing 'value' parameter to test [04de32d6-0174-400f-ab77-b1f551111242]
	Given the document [null]
	And the patch [{"op":"test","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing value parameter to test - where undef is falsy [d1bf2ee0-8f9d-43b8-b6a3-981cfc482eca]
	Given the document [false]
	And the patch [{"op":"test","path":"/0"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from parameter to copy [1af8e2b1-2a7b-481b-a353-61fb76630835]
	Given the document [1]
	And the patch [{"op":"copy","path":"/-"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from location to copy [c1499bea-d14e-4a2f-a395-248e8b624a0f]
	Given the document {"foo":1}
	And the patch [{"op":"copy","from":"/bar","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from parameter to move [c407ef91-8231-4e32-b543-c355a872fe9f]
	Given the document {"foo":1}
	And the patch [{"op":"move","path":""}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: missing from location to move [1741c3cf-1369-46d4-82a1-aab0c3ca8f09]
	Given the document {"foo":1}
	And the patch [{"op":"move","from":"/bar","path":"/foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: unrecognized op should fail [2ee426d5-0392-48c3-a527-5ecd61816499]
	Given the document {"foo":1}
	And the patch [{"op":"spam","path":"/foo","value":1}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test with bad array number that has leading zeros [93db8e1b-469e-4a3b-99f0-94b1738a7b19]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/00","value":"foo"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: test with bad array number that has leading zeros [402850b5-c06d-4625-a93d-98fd9ce4ecd5]
	Given the document ["foo","bar"]
	And the patch [{"op":"test","path":"/01","value":"bar"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing nonexistent field [51bc7151-2629-4a8b-9e41-6cdcdf7201e9]
	Given the document {"foo":"bar"}
	And the patch [{"op":"remove","path":"/baz"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing deep nonexistent path [6eeaab6f-a348-4ee8-844c-be87c60b674b]
	Given the document {"foo":"bar"}
	And the patch [{"op":"remove","path":"/missing1/missing2"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Removing nonexistent index [9f3592f3-2699-402f-a0f7-458c3924318a]
	Given the document ["foo","bar"]
	And the patch [{"op":"remove","path":"/2"}]
	When I apply the patch to the document
	Then the document should not be transformed.

Scenario: Patch with different capitalisation than doc [b0ecec6e-1314-4962-bceb-1b827683aade]
	Given the document {"foo":"bar"}
	And the patch [{"op":"add","path":"/FOO","value":"BAR"}]
	When I apply the patch to the document
	Then the transformed document should equal {"foo":"bar","FOO":"BAR"}
