using Corvus.Json;

var secondsPeriod = NodaTime.Period.FromSeconds(14);
var secondsDuration = new JsonDuration(secondsPeriod); // PT14S
bool secondsDurationValid = secondsDuration.IsValid(); // true
var secondsDurationToPeriod = secondsDuration.GetPeriod(); // OK

var millisecondsPeriod = NodaTime.Period.FromMilliseconds(1400);
var millisecondsDuration = new JsonDuration(millisecondsPeriod); // PT1.4S
bool millisecondsDurationValid = millisecondsDuration.IsValid();  // true
var millisecondsDurationToPeriod = millisecondsDuration.GetPeriod(); // InvalidOperationException: Operation is not valid due to the current state of the object

// same occurs with JsonDuration that was constructed from a string, e.g. new JsonDuration("PT1.4S"), or was deserialized from a JSON document

var hoursDuration = new JsonDuration("PT1.5H");
var hoursDurationValid = hoursDuration.IsValid(); // false
if (hoursDurationValid)
{
    var hoursDurationToPeriod = hoursDuration.GetPeriod(); // InvalidOperationException
}
