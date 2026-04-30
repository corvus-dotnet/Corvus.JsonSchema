using JsonSchemaSample.Api;
using NodaTime;

var personCommonSchema = PersonCommonSchema.Create(
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    // Invalid string length
    otherNames: string.Empty,
    height: 1.57);

// Custom types can be converted in the same way as the primitive types on which they are based.
PersonCommonSchema.ConstrainedString constrainedGivenName = personCommonSchema.GivenName;
PersonCommonSchema.ConstrainedString constrainedFamilyName = personCommonSchema.FamilyName;
string cgn = (string)constrainedGivenName;
string cfn = (string)constrainedFamilyName;

// Low-allocation comparisons are available in the usual way (as are all the other features of JsonString).
constrainedGivenName.Equals(constrainedFamilyName);
constrainedGivenName.EqualsString("Hello");
constrainedGivenName.EqualsUtf8Bytes("Anne"u8);