// <copyright file="JsonPatchApplyTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.Patch;
using Corvus.Json.Patch.Model;
using Xunit;

namespace Corvus.Json.Specs.Tests.JsonPatch
{
    public class JsonPatchApplyTests
    {
        [Fact]
        public void EmptyListEmptyDocs_a93b876d_a689_4509_9399_d95297ecc1da()
        {
            JsonAny doc = JsonAny.Parse("""{}""");
            JsonPatchDocument patch = JsonAny.Parse("""[]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{}"""), output);
        }

        [Fact]
        public void EmptyPatchList_5fa809a2_c35c_4626_8cd4_46d4c5eb3b8a()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), output);
        }

        [Fact]
        public void RearrangementsOK_4a4dcebf_5931_4c90_82c6_55ecd61c7252()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1,"bar":2}""");
            JsonPatchDocument patch = JsonAny.Parse("""[]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"bar":2,"foo":1}"""), output);
        }

        [Fact]
        public void RearrangementsOKHowAboutOneLevelDownArray_c9d4e4d7_442e_4ed1_99dc_dcf660b07818()
        {
            JsonAny doc = JsonAny.Parse("""[{"foo":1,"bar":2}]""");
            JsonPatchDocument patch = JsonAny.Parse("""[]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""[{"bar":2,"foo":1}]"""), output);
        }

        [Fact]
        public void RearrangementsOKHowAboutOneLevelDown_87dd0009_396f_40f6_9818_c84e9d671ece()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":{"foo":1,"bar":2}}""");
            JsonPatchDocument patch = JsonAny.Parse("""[]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":{"bar":2,"foo":1}}"""), output);
        }

        [Fact]
        public void AddReplacesAnyExistingField_a05dccb6_5c3b_42ce_b3a2_7ef8a395a35b()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":null}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/foo","value":1}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), output);
        }

        [Fact]
        public void ToplevelArray_80193eea_6adb_4090_8b65_c22ea2a99c36()
        {
            JsonAny doc = JsonAny.Parse("""[]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/0","value":"foo"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""["foo"]"""), output);
        }

        [Fact]
        public void ToplevelArrayNoChange_9d0d6e85_f100_493b_befa_8e11969213ab()
        {
            JsonAny doc = JsonAny.Parse("""["foo"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""["foo"]"""), output);
        }

        [Fact]
        public void ToplevelObjectNumericString_c5f13054_53b3_4aae_bb23_1ce006972e8f()
        {
            JsonAny doc = JsonAny.Parse("""{}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/foo","value":"1"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":"1"}"""), output);
        }

        [Fact]
        public void ToplevelObjectInteger_ca209dfd_cfb0_4a33_81ca_4230a1d0e42a()
        {
            JsonAny doc = JsonAny.Parse("""{}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/foo","value":1}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), output);
        }

        [Fact]
        public void ReplaceObjectDocumentWithArrayDocument_3c5b40e8_e1ca_47a0_a64b_0a58be9d5c22()
        {
            JsonAny doc = JsonAny.Parse("""{}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"","value":[]}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""[]"""), output);
        }

        [Fact]
        public void ReplaceArrayDocumentWithObjectDocument_8a6cf38c_6395_4c51_a155_8f62a2bd6742()
        {
            JsonAny doc = JsonAny.Parse("""[]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"","value":{}}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{}"""), output);
        }

        [Fact]
        public void AppendToRootArrayDocument_0c9c89e3_e271_4924_b3e2_279a4acd29ad()
        {
            JsonAny doc = JsonAny.Parse("""[]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/-","value":"hi"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""["hi"]"""), output);
        }

        [Fact]
        public void AddTarget_2fa6b2da_4e13_4696_8eca_87bb72cf5dda()
        {
            JsonAny doc = JsonAny.Parse("""{}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/","value":1}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"":1}"""), output);
        }

        [Fact]
        public void AddFooDeepTargetTrailingSlash_36fff972_aca8_48e0_9761_168f7a87841f()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":{}}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/foo/","value":1}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":{"":1}}"""), output);
        }

        [Fact]
        public void AddCompositeValueAtTopLevel_4abb5172_b070_4095_8222_b965d41f7972()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/bar","value":[1,2]}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1,"bar":[1,2]}"""), output);
        }

        [Fact]
        public void AddIntoCompositeValue_7c9b8475_cdfd_492b_a7f4_eeecda86a845()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1,"baz":[{"qux":"hello"}]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/baz/0/foo","value":"world"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1,"baz":[{"qux":"hello","foo":"world"}]}"""), output);
        }

        [Fact]
        public void Scenario_398cd589_4c74_4dab_9ad4_e816d3dcace4()
        {
            JsonAny doc = JsonAny.Parse("""{"bar":[1,2]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/bar/8","value":"5"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void Scenario_0453efa9_e201_46ce_a24a_61f9c0c17100()
        {
            JsonAny doc = JsonAny.Parse("""{"bar":[1,2]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/bar/-1","value":"5"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void Scenario_d9a7ad47_8576_405b_9112_ab1aaf64a8fe()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/bar","value":true}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1,"bar":true}"""), output);
        }

        [Fact]
        public void Scenario_952f0780_2feb_44e6_8e22_bf8d1083c27d()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/bar","value":false}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1,"bar":false}"""), output);
        }

        [Fact]
        public void Scenario_8f76a263_24ee_4e17_8f0f_bbc8f0bcfce2()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/bar","value":null}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1,"bar":null}"""), output);
        }

        [Fact]
        public void ZeroCanBeAnArrayIndexOrObjectElementName_cdda4f9c_4084_49a5_b9b4_4c586c07129d()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/0","value":"bar"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1,"0":"bar"}"""), output);
        }

        [Fact]
        public void Scenario_ec6b9545_cee8_4031_84be_8559afd88ec5()
        {
            JsonAny doc = JsonAny.Parse("""["foo"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/1","value":"bar"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""["foo","bar"]"""), output);
        }

        [Fact]
        public void Scenario_eeb9b237_74ce_4309_96c1_098c1b0e2864()
        {
            JsonAny doc = JsonAny.Parse("""["foo","sil"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/1","value":"bar"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""["foo","bar","sil"]"""), output);
        }

        [Fact]
        public void Scenario_7f199b20_b341_45a5_aa03_c5a44d00ff8d()
        {
            JsonAny doc = JsonAny.Parse("""["foo","sil"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/0","value":"bar"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""["bar","foo","sil"]"""), output);
        }

        [Fact]
        public void PushItemToArrayViaLastIndexPlus1_a61d0766_cbf1_46d8_97bd_b1e3d320b2a1()
        {
            JsonAny doc = JsonAny.Parse("""["foo","sil"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/2","value":"bar"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""["foo","sil","bar"]"""), output);
        }

        [Fact]
        public void AddItemToArrayAtIndexGreaterThanLengthShouldFail_8bcc8ab0_1086_4cf3_974d_eba45538c40b()
        {
            JsonAny doc = JsonAny.Parse("""["foo","sil"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/3","value":"bar"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void TestAgainstImplementationSpecificNumericParsing_d73c94f5_f4b1_4b10_aebd_9e434e3c4c4a()
        {
            JsonAny doc = JsonAny.Parse("""{"1e0":"foo"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/1e0","value":"foo"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"1e0":"foo"}"""), output);
        }

        [Fact]
        public void TestWithBadNumberShouldFail_1b524118_e6ac_4da6_bfd5_e3ef0901c3c9()
        {
            JsonAny doc = JsonAny.Parse("""["foo","bar"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/1e0","value":"bar"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void Scenario_0ea55319_9b0a_443a_9a34_dd54ff9615ed()
        {
            JsonAny doc = JsonAny.Parse("""["foo","sil"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/bar","value":42}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void ValueInArrayAddNotFlattened_1eed4a25_fa33_4a79_941f_64f8abd38de6()
        {
            JsonAny doc = JsonAny.Parse("""["foo","sil"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/1","value":["bar","baz"]}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""["foo",["bar","baz"],"sil"]"""), output);
        }

        [Fact]
        public void Scenario_93f1e141_d360_42c4_9551_9d17b2cc6f21()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1,"bar":[1,2,3,4]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"remove","path":"/bar"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), output);
        }

        [Fact]
        public void Scenario_0441b4c1_fd3f_4889_a8e4_25746689e59a()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1,"baz":[{"qux":"hello"}]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"remove","path":"/baz/0/qux"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1,"baz":[{}]}"""), output);
        }

        [Fact]
        public void Scenario_4494c932_0e77_49f2_9fd0_a172da108586()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1,"baz":[{"qux":"hello"}]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/foo","value":[1,2,3,4]}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}"""), output);
        }

        [Fact]
        public void Scenario_9d576afc_5dfc_4230_aa86_b821853416a3()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":[1,2,3,4],"baz":[{"qux":"hello"}]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/baz/0/qux","value":"world"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":[1,2,3,4],"baz":[{"qux":"world"}]}"""), output);
        }

        [Fact]
        public void Scenario_7f45700a_9571_4511_8685_b35a866ef48c()
        {
            JsonAny doc = JsonAny.Parse("""["foo"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/0","value":"bar"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""["bar"]"""), output);
        }

        [Fact]
        public void Scenario_64bfc017_729f_4289_b3fb_3eca1b847486()
        {
            JsonAny doc = JsonAny.Parse("""[""]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/0","value":0}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""[0]"""), output);
        }

        [Fact]
        public void Scenario_9006d68f_3e1f_4e34_a20f_bfc4d0951e7b()
        {
            JsonAny doc = JsonAny.Parse("""[""]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/0","value":true}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""[true]"""), output);
        }

        [Fact]
        public void Scenario_4e9405c2_4cfd_4ab6_a03e_be61f5be5d98()
        {
            JsonAny doc = JsonAny.Parse("""[""]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/0","value":false}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""[false]"""), output);
        }

        [Fact]
        public void Scenario_1ee933c5_1f68_43dc_b26a_0e76da1e17cd()
        {
            JsonAny doc = JsonAny.Parse("""[""]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/0","value":null}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""[null]"""), output);
        }

        [Fact]
        public void ValueInArrayReplaceNotFlattened_a73d7685_71d1_431c_91f8_98209107189a()
        {
            JsonAny doc = JsonAny.Parse("""["foo","sil"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/1","value":["bar","baz"]}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""["foo",["bar","baz"]]"""), output);
        }

        [Fact]
        public void ReplaceWholeDocument_87a62a59_7262_4f88_b488_220c5739a437()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":"bar"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"","value":{"baz":"qux"}}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"baz":"qux"}"""), output);
        }

        [Fact]
        public void TestReplaceWithMissingParentKeyShouldFail_154f3711_4af3_43c1_a27a_1cc934b0cd71()
        {
            JsonAny doc = JsonAny.Parse("""{"bar":"baz"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/foo/bar","value":false}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void SpuriousPatchProperties_9f014c76_a20d_4d08_8b6f_8b80ee54fb6d()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/foo","value":1,"spurious":1}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), output);
        }

        [Fact]
        public void NullValueShouldBeValidObjProperty_3d42ad57_502d_4840_99de_ecffd29e7172()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":null}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/foo","value":null}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":null}"""), output);
        }

        [Fact]
        public void NullValueShouldBeValidObjPropertyToBeReplacedWithSomethingTruthy_81d0bd70_d965_42c5_9cba_a170d6112c7b()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":null}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/foo","value":"truthy"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":"truthy"}"""), output);
        }

        [Fact]
        public void NullValueShouldBeValidObjPropertyToBeMoved_73122530_cea9_43d4_9938_44a79b4d1e4d()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":null}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"move","from":"/foo","path":"/bar"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"bar":null}"""), output);
        }

        [Fact]
        public void NullValueShouldBeValidObjPropertyToBeCopied_45a24e21_8131_41f2_8693_b4f0dda061c8()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":null}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"copy","from":"/foo","path":"/bar"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":null,"bar":null}"""), output);
        }

        [Fact]
        public void NullValueShouldBeValidObjPropertyToBeRemoved_ca6e1be6_0d6a_4b89_92fe_d349185fd9af()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":null}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"remove","path":"/foo"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{}"""), output);
        }

        [Fact]
        public void NullValueShouldStillBeValidObjPropertyReplaceOtherValue_b61208e8_0dbe_40d0_ba10_c9006e8d786d()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":"bar"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/foo","value":null}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":null}"""), output);
        }

        [Fact]
        public void TestShouldPassDespiteRearrangement_92c1f62a_69cf_45c0_aac6_699e8b3a7e06()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":{"foo":1,"bar":2}}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/foo","value":{"bar":2,"foo":1}}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":{"foo":1,"bar":2}}"""), output);
        }

        [Fact]
        public void TestShouldPassDespiteNestedRearrangement_164477fb_294f_47a3_9fd2_0cb4f9c7a496()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":[{"foo":1,"bar":2}]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/foo","value":[{"bar":2,"foo":1}]}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":[{"foo":1,"bar":2}]}"""), output);
        }

        [Fact]
        public void TestShouldPassNoError_859c903d_351a_4d13_980f_ba8271aedab9()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":{"bar":[1,2,5,4]}}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/foo","value":{"bar":[1,2,5,4]}}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":{"bar":[1,2,5,4]}}"""), output);
        }

        [Fact]
        public void Scenario_cbcc2934_29f9_460e_a9dc_2a6207a9117b()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":{"bar":[1,2,5,4]}}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/foo","value":[1,2]}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void EmptyStringElement_37fa6a1a_ac9a_4607_bdc9_80fe69c0d1f0()
        {
            JsonAny doc = JsonAny.Parse("""{"":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/","value":1}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"":1}"""), output);
        }

        [Fact]
        public void Scenario_977e7eb4_e8d2_41a1_b59e_24eb0e4477b6()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":["bar","baz"],"":0,"a/b":1,"c%d":2,"e^f":3,"g|h":4,"i\\j":5,"k\u0022l":6," ":7,"m~n":8}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/foo","value":["bar","baz"]},{"op":"test","path":"/foo/0","value":"bar"},{"op":"test","path":"/","value":0},{"op":"test","path":"/a~1b","value":1},{"op":"test","path":"/c%d","value":2},{"op":"test","path":"/e^f","value":3},{"op":"test","path":"/g|h","value":4},{"op":"test","path":"/i\\j","value":5},{"op":"test","path":"/k\u0022l","value":6},{"op":"test","path":"/ ","value":7},{"op":"test","path":"/m~0n","value":8}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"":0," ":7,"a/b":1,"c%d":2,"e^f":3,"foo":["bar","baz"],"g|h":4,"i\\j":5,"k\u0022l":6,"m~n":8}"""), output);
        }

        [Fact]
        public void MoveToSameLocationHasNoEffect_6a4b5b83_68a1_448f_851b_5ff795565b01()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"move","from":"/foo","path":"/foo"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), output);
        }

        [Fact]
        public void Scenario_246d2bd9_5168_426d_994c_4764433d0c1e()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1,"baz":[{"qux":"hello"}]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"move","from":"/foo","path":"/bar"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"baz":[{"qux":"hello"}],"bar":1}"""), output);
        }

        [Fact]
        public void Scenario_e994136d_31e9_403a_85d6_085d6ff71b33()
        {
            JsonAny doc = JsonAny.Parse("""{"baz":[{"qux":"hello"}],"bar":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"move","from":"/baz/0/qux","path":"/baz/1"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"baz":[{},"hello"],"bar":1}"""), output);
        }

        [Fact]
        public void Scenario_5143e15e_d402_4762_bb5c_bd96e4ff08a5()
        {
            JsonAny doc = JsonAny.Parse("""{"baz":[{"qux":"hello"}],"bar":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"copy","from":"/baz/0","path":"/boo"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"baz":[{"qux":"hello"}],"bar":1,"boo":{"qux":"hello"}}"""), output);
        }

        [Fact]
        public void ReplacingTheRootOfTheDocumentIsPossibleWithAdd_8c22b611_0b4d_47e5_94df_5a77d14f7a32()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":"bar"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"","value":{"baz":"qux"}}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"baz":"qux"}"""), output);
        }

        [Fact]
        public void AddingToAddsToTheEndOfTheArray_7fa8e93a_88d0_49a6_af78_015fb3974577()
        {
            JsonAny doc = JsonAny.Parse("""[1,2]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/-","value":{"foo":["bar","baz"]}}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""[1,2,{"foo":["bar","baz"]}]"""), output);
        }

        [Fact]
        public void AddingToAddsToTheEndOfTheArrayEvenNLevelsDown_a9ba53c4_0aac_43a4_a7f1_3c1c5d668085()
        {
            JsonAny doc = JsonAny.Parse("""[1,2,[3,[4,5]]]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/2/1/-","value":{"foo":["bar","baz"]}}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""[1,2,[3,[4,5,{"foo":["bar","baz"]}]]]"""), output);
        }

        [Fact]
        public void TestRemoveWithBadNumberShouldFail_b76f5f1d_3cd9_4707_ac75_c9cceb1e45d8()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1,"baz":[{"qux":"hello"}]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"remove","path":"/baz/1e0/qux"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void TestRemoveOnArray_3960368d_eb72_4ad0_a3b3_9a4862707d66()
        {
            JsonAny doc = JsonAny.Parse("""[1,2,3,4]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"remove","path":"/0"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""[2,3,4]"""), output);
        }

        [Fact]
        public void TestRepeatedRemoves_1efb1102_2a8d_4b47_a05a_a631f8d271fb()
        {
            JsonAny doc = JsonAny.Parse("""[1,2,3,4]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"remove","path":"/1"},{"op":"remove","path":"/2"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""[1,3]"""), output);
        }

        [Fact]
        public void TestRemoveWithBadIndexShouldFail_fe02ea1b_7151_4498_af3f_f98c55e3fc84()
        {
            JsonAny doc = JsonAny.Parse("""[1,2,3,4]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"remove","path":"/1e0"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void TestReplaceWithBadNumberShouldFail_e733103e_5bb8_42f4_8c12_0fcd737a7622()
        {
            JsonAny doc = JsonAny.Parse("""[""]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/1e0","value":false}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void TestCopyWithBadNumberShouldFail_f73a7b82_3522_42d5_821a_e35a8b494c60()
        {
            JsonAny doc = JsonAny.Parse("""{"baz":[1,2,3],"bar":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"copy","from":"/baz/1e0","path":"/boo"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void TestMoveWithBadNumberShouldFail_26efffe3_8629_4bee_80b3_a65ca498555f()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1,"baz":[1,2,3,4]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"move","from":"/baz/1e0","path":"/foo"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void TestAddWithBadNumberShouldFail_c5e8e082_5b57_4a91_bc9c_f899c18d4253()
        {
            JsonAny doc = JsonAny.Parse("""["foo","sil"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/1e0","value":"bar"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void MissingPathParameter_89d90967_8bad_4285_8ceb_4ac93acd451c()
        {
            JsonAny doc = JsonAny.Parse("""{}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","value":"bar"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void PathParameterWithNullValue_dd12add4_e0c1_4325_a5fa_b055b04ad8fc()
        {
            JsonAny doc = JsonAny.Parse("""{}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":null,"value":"bar"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void InvalidJSONPointerToken_2ff0ecba_6aeb_44bb_8807_e1e80290540c()
        {
            JsonAny doc = JsonAny.Parse("""{}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"foo","value":"bar"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void MissingValueParameterToAdd_91a0fb7f_abe0_4078_adcc_0b49ac8a99be()
        {
            JsonAny doc = JsonAny.Parse("""[1]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/-"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void MissingValueParameterToReplace_66ca4e17_7f42_469f_b0ef_7f2704de5851()
        {
            JsonAny doc = JsonAny.Parse("""[1]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/0"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void MissingValueParameterToTest_a67c0af5_58f4_4d5a_b874_706c647ad553()
        {
            JsonAny doc = JsonAny.Parse("""[null]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/0"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void MissingValueParameterToTestWhereUndefIsFalsy_7fd48c28_834d_4b83_ad56_f70cf4bf19a0()
        {
            JsonAny doc = JsonAny.Parse("""[false]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/0"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void MissingFromParameterToCopy_0618cc43_db35_4607_8924_98fdf6cdd1dd()
        {
            JsonAny doc = JsonAny.Parse("""[1]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"copy","path":"/-"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void MissingFromLocationToCopy_ffab4fb7_8492_4f8e_933f_8d82662fb179()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"copy","from":"/bar","path":"/foo"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void MissingFromParameterToMove_bf721e67_3dbc_4f21_9585_0ad198a7bac9()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"move","path":""}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void MissingFromLocationToMove_e62e5b0b_7203_4726_8ce7_2518da2872ab()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"move","from":"/bar","path":"/foo"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void UnrecognizedOpShouldFail_cd6f0ae6_942a_42ff_8f24_d581f0f15653()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"spam","path":"/foo","value":1}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void TestWithBadArrayNumberThatHasLeadingZeros_b3f07c0e_3ee2_40c2_8c24_d61cb281d2d4()
        {
            JsonAny doc = JsonAny.Parse("""["foo","bar"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/00","value":"foo"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void TestWithBadArrayNumberThatHasLeadingZeros_e4669067_de77_4699_a177_d5275e5aef7f()
        {
            JsonAny doc = JsonAny.Parse("""["foo","bar"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/01","value":"bar"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void RemovingNonexistentField_2376282f_328f_431e_9d03_bbcb6cf97e03()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":"bar"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"remove","path":"/baz"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void RemovingDeepNonexistentPath_acf2040b_a8aa_4eb0_8bd6_fa4f4eefddb2()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":"bar"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"remove","path":"/missing1/missing2"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void RemovingNonexistentIndex_f46b5c72_39c1_463e_aae0_f68613a711cf()
        {
            JsonAny doc = JsonAny.Parse("""["foo","bar"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"remove","path":"/2"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void PatchWithDifferentCapitalisationThanDoc_15925c9d_5fde_407a_8578_4064280ba66c()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":"bar"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/FOO","value":"BAR"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":"bar","FOO":"BAR"}"""), output);
        }

        [Fact]
        public void AddWithMissingObject_c65e41a1_4353_40b2_810c_d48abbab3204()
        {
            JsonAny doc = JsonAny.Parse("""{"q":{"bar":2}}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/a/b","value":1}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void A1AddingAnObjectMember_cc1ffcde_5e7d_4e4d_9923_d64742324ac5()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":"bar"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/baz","value":"qux"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"baz":"qux","foo":"bar"}"""), output);
        }

        [Fact]
        public void A2AddingAnArrayElement_7828f56a_98a7_44f4_a211_e1e4c20f7a00()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":["bar","baz"]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/foo/1","value":"qux"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":["bar","qux","baz"]}"""), output);
        }

        [Fact]
        public void A3RemovingAnObjectMember_2993017e_97a8_45cb_b8cb_eb214233518b()
        {
            JsonAny doc = JsonAny.Parse("""{"baz":"qux","foo":"bar"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"remove","path":"/baz"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":"bar"}"""), output);
        }

        [Fact]
        public void A4RemovingAnArrayElement_a67f508e_f991_435c_81fe_ed329a7312ec()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":["bar","qux","baz"]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"remove","path":"/foo/1"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":["bar","baz"]}"""), output);
        }

        [Fact]
        public void A5ReplacingAValue_0a8da2af_d6ab_4329_a35d_676e533e1f2c()
        {
            JsonAny doc = JsonAny.Parse("""{"baz":"qux","foo":"bar"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"replace","path":"/baz","value":"boo"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"baz":"boo","foo":"bar"}"""), output);
        }

        [Fact]
        public void A6MovingAValue_9d7c86e5_eade_42ba_b97c_88a76e46fc21()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":{"bar":"baz","waldo":"fred"},"qux":{"corge":"grault"}}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"move","from":"/foo/waldo","path":"/qux/thud"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":{"bar":"baz"},"qux":{"corge":"grault","thud":"fred"}}"""), output);
        }

        [Fact]
        public void A7MovingAnArrayElement_dc95c5b5_10fa_481c_9346_78b83099691e()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":["all","grass","cows","eat"]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"move","from":"/foo/1","path":"/foo/3"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":["all","cows","eat","grass"]}"""), output);
        }

        [Fact]
        public void A8TestingAValueSuccess_08f80592_ee11_4b56_b1d8_2f44a3eec732()
        {
            JsonAny doc = JsonAny.Parse("""{"baz":"qux","foo":["a",2,"c"]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/baz","value":"qux"},{"op":"test","path":"/foo/1","value":2}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"baz":"qux","foo":["a",2,"c"]}"""), output);
        }

        [Fact]
        public void A9TestingAValueError_edda6c6b_68bd_4f13_af1a_65f300870e9a()
        {
            JsonAny doc = JsonAny.Parse("""{"baz":"qux"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/baz","value":"bar"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void A10AddingANestedMemberObject_b7d68493_6dcf_4aad_86af_a561814e2aee()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":"bar"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/child","value":{"grandchild":{}}}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":"bar","child":{"grandchild":{}}}"""), output);
        }

        [Fact]
        public void A11IgnoringUnrecognizedElements_7dc7f9ea_bf55_4864_95b0_5bb40e7764c6()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":"bar"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/baz","value":"qux","xyz":123}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":"bar","baz":"qux"}"""), output);
        }

        [Fact]
        public void A12AddingToANonExistentTarget_22c8a16b_3893_4f29_be92_84957cb644fe()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":"bar"}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/baz/bat","value":"qux"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void A14EscapeOrdering_61ce3ee1_3757_4094_9a59_6e5096edf4f8()
        {
            JsonAny doc = JsonAny.Parse("""{"/":9,"~1":10}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/~01","value":10}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"/":9,"~1":10}"""), output);
        }

        [Fact]
        public void A15ComparingStringsAndNumbers_14ffd972_53a1_4c3e_b624_bd9736df3061()
        {
            JsonAny doc = JsonAny.Parse("""{"/":9,"~1":10}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"test","path":"/~01","value":"10"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void A16AddingAnArrayValue_3e4ec449_a50b_4819_ac69_ebbb0d1b0367()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":["bar"]}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/foo/-","value":["abc","def"]}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":["bar",["abc","def"]]}"""), output);
        }

        [Fact]
        public void AddItemToArrayWithBadIndexLeadingZeroesShouldFail_672c1926_eb11_40be_8d10_3eff831a29b2()
        {
            JsonAny doc = JsonAny.Parse("""["foo","bar"]""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"add","path":"/00","value":"foo"}]""").As<JsonPatchDocument>();
            bool result;
            JsonAny output;
            if (patch.IsValid())
            {
                result = doc.TryApplyPatch(patch, out output);
            }
            else
            {
                result = false;
                output = doc;
            }

            Assert.False(result);
        }

        [Fact]
        public void CopyToSameLocationHasNoEffect_153f6d29_ddb7_4c56_9caf_967a75a88494()
        {
            JsonAny doc = JsonAny.Parse("""{"foo":1}""");
            JsonPatchDocument patch = JsonAny.Parse("""[{"op":"copy","from":"/foo","path":"/foo"}]""").As<JsonPatchDocument>();
            bool result = doc.TryApplyPatch(patch, out JsonAny output);
            Assert.Equal(JsonAny.Parse("""{"foo":1}"""), output);
        }
    }
}