REM This transforms the templates in" ./Templates and produces the code in" ./FormatType

REM See ./Templates/FormattedStringType.Core.tt for details
t4 -o ./GeneratedFormatTypes/JsonBase64Content.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonBase64Content" -p"=JsonTypeName=base64content" -p"=ValidateMethodName=TypeBase64Content" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonBase64Content.Basics.cs -p"=TypeName=JsonBase64Content" -p"=JsonTypeName=base64content" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonBase64String.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonBase64String" -p"=JsonTypeName=base64string" -p"=ValidateMethodName=TypeBase64String" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonBase64String.Basics.cs -p"=TypeName=JsonBase64String" -p"=JsonTypeName=base64string" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonContent.Core.cs -p"=OverrideEquals=true" -p"=TypeName=JsonContent" -p"=JsonTypeName=content" -p"=ValidateMethodName=TypeContent" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonContent.Basics.cs -p"=TypeName=JsonContent" -p"=JsonTypeName=content" ./Templates/FormattedStringType.Basics.tt

t4 -o ./GeneratedFormatTypes/JsonContentPre201909.Core.cs -p"=OverrideEquals=true" -p"=TypeName=JsonContentPre201909" -p"=JsonTypeName=content" -p"=ValidateMethodName=TypeContentPre201909" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonContentPre201909.Basics.cs -p"=TypeName=JsonContentPre201909" -p"=JsonTypeName=content" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonBase64ContentPre201909.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonBase64ContentPre201909" -p"=JsonTypeName=base64content" -p"=ValidateMethodName=TypeBase64ContentPre201909" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonBase64ContentPre201909.Basics.cs -p"=TypeName=JsonBase64ContentPre201909" -p"=JsonTypeName=base64content" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonBase64StringPre201909.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonBase64StringPre201909" -p"=JsonTypeName=base64string" -p"=ValidateMethodName=TypeBase64StringPre201909" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonBase64StringPre201909.Basics.cs -p"=TypeName=JsonBase64StringPre201909" -p"=JsonTypeName=base64string" ./Templates/FormattedStringType.Basics.tt

t4 -o ./GeneratedFormatTypes/JsonDate.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonDate" -p"=JsonTypeName=date" -p"=ValidateMethodName=TypeDate" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonDate.Basics.cs -p"=TypeName=JsonDate" -p"=JsonTypeName=date" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonDateTime.Core.cs -p"=OverrideEquals=true" -p"=TypeName=JsonDateTime" -p"=JsonTypeName=dateTime" -p"=ValidateMethodName=TypeDateTime" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonDateTime.Basics.cs -p"=TypeName=JsonDateTime" -p"=JsonTypeName=dateTime" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonDuration.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonDuration" -p"=JsonTypeName=duration" -p"=ValidateMethodName=TypeDuration" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonDuration.Basics.cs -p"=TypeName=JsonDuration" -p"=JsonTypeName=duration" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonEmail.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonEmail" -p"=JsonTypeName=email" -p"=ValidateMethodName=TypeEmail" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonEmail.Basics.cs -p"=TypeName=JsonEmail" -p"=JsonTypeName=email" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonHostname.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonHostname" -p"=JsonTypeName=hostname" -p"=ValidateMethodName=TypeHostname" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonHostname.Basics.cs -p"=TypeName=JsonHostname" -p"=JsonTypeName=hostname" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonIdnEmail.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonIdnEmail" -p"=JsonTypeName=idnEmail" -p"=ValidateMethodName=TypeIdnEmail" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonIdnEmail.Basics.cs -p"=TypeName=JsonIdnEmail" -p"=JsonTypeName=idnEmail" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonIdnHostname.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonIdnHostname" -p"=JsonTypeName=idnHostname" -p"=ValidateMethodName=TypeIdnHostName" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonIdnHostname.Basics.cs -p"=TypeName=JsonIdnHostname" -p"=JsonTypeName=idnHostname" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonIpV4.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonIpV4" -p"=JsonTypeName=ipv4" -p"=ValidateMethodName=TypeIpV4" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonIpV4.Basics.cs -p"=TypeName=JsonIpV4" -p"=JsonTypeName=ipv4" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonIpV6.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonIpV6" -p"=JsonTypeName=ipv6" -p"=ValidateMethodName=TypeIpV6" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonIpV6.Basics.cs -p"=TypeName=JsonIpV6" -p"=JsonTypeName=ipv6" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonIri.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonIri" -p"=JsonTypeName=iri" -p"=ValidateMethodName=TypeIri" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonIri.Basics.cs -p"=TypeName=JsonIri" -p"=JsonTypeName=iri" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonIriReference.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonIriReference" -p"=JsonTypeName=iriReference" -p"=ValidateMethodName=TypeIriReference" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonIriReference.Basics.cs -p"=TypeName=JsonIriReference" -p"=JsonTypeName=iriReference" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonPointer.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonPointer" -p"=JsonTypeName=pointer" -p"=ValidateMethodName=TypePointer" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonPointer.Basics.cs -p"=TypeName=JsonPointer" -p"=JsonTypeName=pointer" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonRegex.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonRegex" -p"=JsonTypeName=regex" -p"=ValidateMethodName=TypeRegex" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonRegex.Basics.cs -p"=TypeName=JsonRegex" -p"=JsonTypeName=regex" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonRelativePointer.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonRelativePointer" -p"=JsonTypeName=relativePointer" -p"=ValidateMethodName=TypeRelativePointer" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonRelativePointer.Basics.cs -p"=TypeName=JsonRelativePointer" -p"=JsonTypeName=relativePointer" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonTime.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonTime" -p"=JsonTypeName=time" -p"=ValidateMethodName=TypeTime" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonTime.Basics.cs -p"=TypeName=JsonTime" -p"=JsonTypeName=time" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonUri.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonUri" -p"=JsonTypeName=uri" -p"=ValidateMethodName=TypeUri" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonUri.Basics.cs -p"=TypeName=JsonUri" -p"=JsonTypeName=uri" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonUriReference.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonUriReference" -p"=JsonTypeName=uriReference" -p"=ValidateMethodName=TypeUriReference" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonUriReference.Basics.cs -p"=TypeName=JsonUriReference" -p"=JsonTypeName=uriReference" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonUriTemplate.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonUriTemplate" -p"=JsonTypeName=uriTemplate" -p"=ValidateMethodName=TypeUriTemplate" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonUriTemplate.Basics.cs -p"=TypeName=JsonUriTemplate" -p"=JsonTypeName=uriTemplate" ./Templates/FormattedStringType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonUuid.Core.cs -p"=OverrideEquals=false" -p"=TypeName=JsonUuid" -p"=JsonTypeName=uuid" -p"=ValidateMethodName=TypeUuid" ./Templates/FormattedStringType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonUuid.Basics.cs -p"=TypeName=JsonUuid" -p"=JsonTypeName=uuid" ./Templates/FormattedStringType.Basics.tt

REM See ./Templates/FormattedNumberType.Core.tt for details
t4 -o ./GeneratedFormatTypes/JsonInteger.Core.cs -p"=TypeName=JsonInteger" -p"=JsonTypeName=integer" -p"=ValidateMethodName=TypeInteger" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonInteger.Basics.cs -p"=TypeName=JsonInteger" -p"=JsonTypeName=integer" ./Templates/FormattedNumberType.Basics.tt

REM See ./Templates/CustomAny.Core.tt for details
t4 -o ./GeneratedFormatTypes/JsonAny.Core.cs -p"=TypeName=JsonAny" -p"=Summary=Represents any JSON value." -p"=HasConversionsToJsonAny=false" ./Templates/CustomAny.Core.tt
t4 -o ./GeneratedFormatTypes/JsonAny.Array.Add.cs -p"=TypeName=JsonAny" -p"=Summary=Represents any JSON value." ./Templates/CustomAny.Array.Add.tt
t4 -o ./GeneratedFormatTypes/JsonAny.Array.Remove.cs -p"=TypeName=JsonAny" -p"=Summary=Represents any JSON value." ./Templates/CustomAny.Array.Remove.tt
t4 -o ./GeneratedFormatTypes/JsonAny.Array.cs -p"=TypeName=JsonAny" -p"=Summary=Represents any JSON value." ./Templates/CustomAny.Array.tt
t4 -o ./GeneratedFormatTypes/JsonAny.Boolean.cs -p"=TypeName=JsonAny" -p"=Summary=Represents any JSON value." ./Templates/CustomAny.Boolean.tt
t4 -o ./GeneratedFormatTypes/JsonAny.Number.cs -p"=TypeName=JsonAny" -p"=Summary=Represents any JSON value." ./Templates/CustomAny.Number.tt
t4 -o ./GeneratedFormatTypes/JsonAny.Object.cs -p"=TypeName=JsonAny" -p"=Summary=Represents any JSON value." ./Templates/CustomAny.Object.tt
t4 -o ./GeneratedFormatTypes/JsonAny.String.cs -p"=TypeName=JsonAny" -p"=Summary=Represents any JSON value." ./Templates/CustomAny.String.tt

t4 -o ./GeneratedFormatTypes/JsonNotAny.Core.cs -p"=TypeName=JsonNotAny" -p"=Summary=Represents any JSON value, validating false." -p"=HasConversionsToJsonAny=true" ./Templates/CustomAny.Core.tt
t4 -o ./GeneratedFormatTypes/JsonNotAny.Array.Add.cs -p"=TypeName=JsonNotAny" -p"=Summary=Represents any JSON value, validating false." ./Templates/CustomAny.Array.Add.tt
t4 -o ./GeneratedFormatTypes/JsonNotAny.Array.Remove.cs -p"=TypeName=JsonNotAny" -p"=Summary=Represents any JSON value, validating false." ./Templates/CustomAny.Array.Remove.tt
t4 -o ./GeneratedFormatTypes/JsonNotAny.Array.cs -p"=TypeName=JsonNotAny" -p"=Summary=Represents any JSON value, validating false." ./Templates/CustomAny.Array.tt
t4 -o ./GeneratedFormatTypes/JsonNotAny.Boolean.cs -p"=TypeName=JsonNotAny" -p"=Summary=Represents any JSON value, validating false." ./Templates/CustomAny.Boolean.tt
t4 -o ./GeneratedFormatTypes/JsonNotAny.Number.cs -p"=TypeName=JsonNotAny" -p"=Summary=Represents any JSON value, validating false." ./Templates/CustomAny.Number.tt
t4 -o ./GeneratedFormatTypes/JsonNotAny.Object.cs -p"=TypeName=JsonNotAny" -p"=Summary=Represents any JSON value, validating false." ./Templates/CustomAny.Object.tt
t4 -o ./GeneratedFormatTypes/JsonNotAny.String.cs -p"=TypeName=JsonNotAny" -p"=Summary=Represents any JSON value, validating false." ./Templates/CustomAny.String.tt
