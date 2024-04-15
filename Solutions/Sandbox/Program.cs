using Corvus.Json;
using Corvus.Json.Benchmarking.Models;

using var parsedPerson = ParsedValue<Person>.Parse(
    """
    {
        "name": {
            "familyName": "Oldroyd",
            "givenName": "Michael",
            "otherNames": [],
            "email": "michael.oldryoyd@contoso.com"
        },
        "dateOfBirth": "1944-07-14",
        "netWorth": 1234567890.1234567891,
        "height": 1.8
    }
    """);


Person person = parsedPerson.Instance;

for(int i = 0; i < 100; ++i)
{
    bool _ = person.IsValid();
}

Thread.Sleep(2000);

for (int i = 0; i < 10000; ++i)
{
    bool _ = person.IsValid();
}