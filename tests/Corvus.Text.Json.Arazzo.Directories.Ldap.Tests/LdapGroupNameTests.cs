// <copyright file="LdapGroupNameTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;
using Shouldly;

namespace Corvus.Text.Json.Arazzo.Directories.Ldap.Tests;

/// <summary>
/// Unit coverage (no directory) of the LDAP adapter's membership-name extraction (design §16.5.4): a person's inline
/// <c>memberOf</c> values are group DNs, but the identity value the mapper keys on is the group's naming value (the RDN),
/// so <see cref="LdapPrincipalDirectory.GroupName"/> extracts it. The full inline expansion is exercised by the container
/// conformance suite; this locks the DN-to-name reduction in the fast gate.
/// </summary>
[TestClass]
public sealed class LdapGroupNameTests
{
    [TestMethod]
    public void A_group_DN_reduces_to_its_RDN_value()
        => LdapPrincipalDirectory.GroupName("cn=payments,ou=groups,dc=corp,dc=example").ShouldBe("payments");

    [TestMethod]
    public void An_AD_style_DN_reduces_to_its_RDN_value()
        => LdapPrincipalDirectory.GroupName("CN=arazzo-admins,OU=Security,DC=corp,DC=example").ShouldBe("arazzo-admins");

    [TestMethod]
    public void A_bare_name_is_passed_through_unchanged()
        => LdapPrincipalDirectory.GroupName("payments").ShouldBe("payments");

    [TestMethod]
    public void An_escaped_comma_in_the_RDN_value_is_kept_whole()
        => LdapPrincipalDirectory.GroupName("cn=payments\\, eu,ou=groups,dc=corp").ShouldBe("payments\\, eu");

    [TestMethod]
    public void A_single_rdn_dn_reduces_to_its_value()
        => LdapPrincipalDirectory.GroupName("cn=solo").ShouldBe("solo");
}
