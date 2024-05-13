using Corvus.Json;
using JsonSchemaSample.CrmApi;
using JsonSchemaSample.CustomerApi;
using JsonSchemaSample.DatabaseApi;

// The Customer payload incoming from our CRM
string incomingCrmPayload =
    """
    {
        "id": 1234,
        "firstName": "Henry",
        "lastName": "James"
    }
    """;

// The customer as stored in our DB
string dbPayload =
    """
    {
        "id": 56789,
        "familyName": "Henri",
        "givenName": "James",
        "idDescriptors": [
            {"source": "crm", "id": 1234 },
            {"source": "api", "id": "169edea5-dbe5-4bf4-80a8-a97a596cb85c" }
        ]
    }
    """;

// Parse the incoming CRM payload
using var parsedCrmPayload = ParsedValue<CrmCustomer>.Parse(incomingCrmPayload);
CrmCustomer crmCustomer = parsedCrmPayload.Instance;

// Query the corresponding customer in the database
using var parsedDbCustomer = QueryDatabase(DbCustomer.CrmIdDescriptor.Create(crmCustomer.Id));
DbCustomer dbCustomer = parsedDbCustomer.Instance;

Console.WriteLine("Original customer details in DB:");
Console.WriteLine(dbCustomer);

Console.WriteLine("CRM update:");
Console.WriteLine(crmCustomer);

// Update the db customer from the CRM payload
dbCustomer = UpdateDbCustomerFromCrm(dbCustomer, crmCustomer);

WriteDatabase(dbCustomer);

Console.WriteLine("Updated database customer:");
Console.WriteLine(dbCustomer);

// Now, map the db customer back to an api customer
ApiCustomer apiCustomer = MapToApiCustomer(dbCustomer);

if (apiCustomer.IsValid())
{
    Console.WriteLine("API customer: ");
    Console.WriteLine(apiCustomer);
}
else
{
    Console.WriteLine("The updated customer was not valid for the API.");
}

ApiCustomer MapToApiCustomer(in DbCustomer dbCustomer)
{
    // We know that ApiCustomer and DbCustomer are nearly identical.
    // So we can efficiently convert the DbCustomer directly into an ApiCustomer
    // We remove the ID properties that we don't require,
    // and add the CustomerId property.
    // We have avoided copying and allocating string values - they remain as pointers
    // to the underlying UTF8 data backing the original DbCustomer instance.
    return dbCustomer
        .As<ApiCustomer>()
        .RemoveProperty(DbCustomer.JsonPropertyNames.IdDescriptorsUtf8)
        .RemoveProperty(DbCustomer.JsonPropertyNames.IdUtf8)
        .WithCustomerId(GetApiId(dbCustomer));
}

// Update the customer from the CRM value
DbCustomer UpdateDbCustomerFromCrm(in DbCustomer dbCustomer, in CrmCustomer crmCustomer)
{
    return dbCustomer
        .WithFamilyName(crmCustomer.LastName)
        .WithGivenName(crmCustomer.FirstName);
}

// Generic database query mechanism for an ID
ParsedValue<DbCustomer> QueryDatabase(in DbCustomer.IdDescriptor idDescriptor)
{
    // We imagine that this is a public entry point for our API, so we will validate
    // the type passed in to us.
    if (!idDescriptor.IsValid())
    {
        throw new InvalidOperationException("The id descriptor is not valid");
    }

    // Use pattern matching to dispatch to the appropriate handler for the id descriptor in the query
    return idDescriptor.Match(
        (in DbCustomer.ApiIdDescriptor d) => QueryWithApiId(d),
        (in DbCustomer.CrmIdDescriptor d) => QueryWithCrmId(d),
        (in DbCustomer.GenericIdDescriptor d) => throw new InvalidOperationException("Unsupported generic id"),
        (in DbCustomer.IdDescriptor d) => throw new InvalidOperationException($"Unknown ID descriptor: {d}"));
}

// Find the appropriate ID for the API in the customer object
JsonUuid GetApiId(in DbCustomer dbCustomer)
{
    foreach (DbCustomer.IdDescriptor id in dbCustomer.IdDescriptors.EnumerateArray())
    {
        // TryGetAs performs a pattern match for a single type
        if (id.TryGetAsApiIdDescriptor(out DbCustomer.ApiIdDescriptor apiId))
        {
            return apiId.Id;
        }
    }

    return JsonUuid.Undefined;
}

void WriteDatabase(in DbCustomer customer)
{
    if (!customer.IsValid())
    {
        throw new InvalidOperationException("The customer is not valid");
    }

    // Write the customer back to the database...
}

// Execute the query with the CRM ID
ParsedValue<DbCustomer> QueryWithCrmId(in DbCustomer.CrmIdDescriptor idDescriptor)
{
    // (Fake a database query for the payload)
    if (idDescriptor.Id == 1234)
    {
        return ParsedValue<DbCustomer>.Parse(dbPayload);
    }

    return DbCustomer.Undefined;
}

// Execute the query with the API ID
ParsedValue<DbCustomer> QueryWithApiId(in DbCustomer.ApiIdDescriptor idDescriptor)
{
    // (Fake a database query for the payload)
    if (idDescriptor.Id == Guid.Parse("169edea5-dbe5-4bf4-80a8-a97a596cb85c"))
    {
        return ParsedValue<DbCustomer>.Parse(dbPayload);
    }

    return DbCustomer.Undefined;
}