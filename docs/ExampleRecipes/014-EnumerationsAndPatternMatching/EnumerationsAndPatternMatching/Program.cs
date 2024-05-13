using JsonSchemaSample.Api;

// Create currency values, using the permitted values in the enum
CurrencyValue valueInGbp =
    CurrencyValue.Create(Currencies.EnumValues.Gbp, 123.45m);
CurrencyValue valueInEur =
    CurrencyValue.Create(Currencies.EnumValues.Eur, 1234.56m);

// Get an enum value as a UTF8 byte array
ReadOnlySpan<byte> utf8Value = Currencies.EnumValues.GbpUtf8;

// Pass to a processing function. No polymorphism required.
Console.WriteLine(
    ConvertToUsd(valueInGbp));
Console.WriteLine(
    ConvertToUsd(valueInEur));

CurrencyValue ConvertToUsd(in CurrencyValue currencyValue)
{
    // Use the match function on the enum
    return currencyValue.Currency.Match(
        // Pass in an (optional) state value. All pattern matching functions allow a state to be
        // passed in addition to the value.
        currencyValue,
        // These match the currency type and return a new value in USD
        // I like to use the optional parameter names to help with readability
        matchGbp: cv => CurrencyValue.Create(Currencies.EnumValues.Usd, cv.Value / 1.25m),
        matchUsd: cv => cv,
        matchEur: cv => CurrencyValue.Create(Currencies.EnumValues.Usd, cv.Value / 1.08m),
        defaultMatch: cv => throw new InvalidOperationException($"Unknown currency type: {cv.Currency}"));
}