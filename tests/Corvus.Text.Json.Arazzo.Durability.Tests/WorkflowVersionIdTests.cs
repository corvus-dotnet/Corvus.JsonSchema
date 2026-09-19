// <copyright file="WorkflowVersionIdTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Durability.Tests;

/// <summary>The one reading of a versioned workflow id, <c>{base}-v{n}</c>.</summary>
[TestClass]
public sealed class WorkflowVersionIdTests
{
    [TestMethod]
    [DataRow("flow-v1", "flow", 1)]
    [DataRow("flow-v42", "flow", 42)]
    [DataRow("my-flow-v2", "my-flow", 2)]
    [DataRow("pre-v1-hotfix-v3", "pre-v1-hotfix", 3)]
    [DataRow("flow-v0", "flow", 0)]
    public void A_versioned_id_splits_at_the_last_version_suffix(string workflowId, string expectedBase, int expectedVersion)
    {
        WorkflowVersionId.TryParse(workflowId, out string baseWorkflowId, out int versionNumber).ShouldBeTrue();

        baseWorkflowId.ShouldBe(expectedBase);
        versionNumber.ShouldBe(expectedVersion);
    }

    [TestMethod]
    [DataRow("flow")]
    [DataRow("-v1")]
    [DataRow("flow-v")]
    [DataRow("flow-vx")]
    [DataRow("flow-v-3")]
    [DataRow("flow-v+3")]
    [DataRow("flow-v 3")]
    [DataRow("flow-v3 ")]
    [DataRow("flow-v1.0")]
    [DataRow("flow-v99999999999")]
    [DataRow("$schedule")]
    [DataRow("")]
    [DataRow(null)]
    public void What_is_not_a_version_is_not_read_as_one(string? workflowId)
    {
        // A sign or whitespace is not part of a version. Three of the four copies this replaced read "flow-v-3" as
        // version minus three.
        WorkflowVersionId.TryParse(workflowId, out string baseWorkflowId, out int versionNumber).ShouldBeFalse();

        baseWorkflowId.ShouldBeEmpty();
        versionNumber.ShouldBe(0);
    }

    [TestMethod]
    public void The_reading_does_not_depend_on_the_hosts_culture()
    {
        CultureInfo before = CultureInfo.CurrentCulture;
        try
        {
            // A culture whose digits and signs differ from the invariant one.
            CultureInfo.CurrentCulture = new CultureInfo("ar-SA");
            WorkflowVersionId.TryParse("flow-v12", out string baseWorkflowId, out int versionNumber).ShouldBeTrue();
            baseWorkflowId.ShouldBe("flow");
            versionNumber.ShouldBe(12);
        }
        finally
        {
            CultureInfo.CurrentCulture = before;
        }
    }
}