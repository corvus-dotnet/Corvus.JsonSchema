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
t4 -o ./GeneratedFormatTypes/JsonByte.Core.cs -p"=TypeName=JsonByte" -p"=JsonTypeName= byte" -p"=ConversionOperatorToByte=implicit" -p="ValidateMethodName=TypeByte" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonByte.Basics.cs -p"=TypeName=JsonByte" -p"=JsonTypeName=byte" -p"=ConversionOperatorToByte=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonSByte.Core.cs -p"=TypeName=JsonSByte" -p"=JsonTypeName=sbyte" -p"=ConversionOperatorToSByte=implicit" -p="ValidateMethodName=TypeSByte" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonSByte.Basics.cs -p"=TypeName=JsonSByte" -p"=JsonTypeName=sbyte" -p"=ConversionOperatorToSByte=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonInt16.Core.cs -p"=TypeName=JsonInt16" -p"=JsonTypeName=int16" -p"=ConversionOperatorToInt16=implicit"  -p="ValidateMethodName=TypeInt16" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonInt16.Basics.cs -p"=TypeName=JsonInt16" -p"=JsonTypeName=int16" -p"=ConversionOperatorToInt16=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonUInt16.Core.cs -p"=TypeName=JsonUInt16" -p"=JsonTypeName=uint16" -p"=ConversionOperatorToUInt16=implicit"  -p="ValidateMethodName=TypeUInt16" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonUInt16.Basics.cs -p"=TypeName=JsonUInt16" -p"=JsonTypeName=uint16" -p"=ConversionOperatorToUInt16=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonInt32.Core.cs -p"=TypeName=JsonInt32" -p"=JsonTypeName=int32" -p"=ConversionOperatorToInt32=implicit"  -p="ValidateMethodName=TypeInt32" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonInt32.Basics.cs -p"=TypeName=JsonInt32" -p"=JsonTypeName=int32" -p"=ConversionOperatorToInt32=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonUInt32.Core.cs -p"=TypeName=JsonUInt32" -p"=JsonTypeName=uint32" -p"=ConversionOperatorToUInt32=implicit"  -p="ValidateMethodName=TypeUInt32" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonUInt32.Basics.cs -p"=TypeName=JsonUInt32" -p"=JsonTypeName=uint32" -p"=ConversionOperatorToUInt32=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonInt64.Core.cs -p"=TypeName=JsonInt64" -p"=JsonTypeName=int64" -p"=ConversionOperatorToInt64=implicit"  -p="ValidateMethodName=TypeInt64" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonInt64.Basics.cs -p"=TypeName=JsonInt64" -p"=JsonTypeName=int64" -p"=ConversionOperatorToInt64=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonUInt64.Core.cs -p"=TypeName=JsonUInt64" -p"=JsonTypeName=uint64" -p"=ConversionOperatorToUInt64=implicit"  -p="ValidateMethodName=TypeUInt64" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonUInt64.Basics.cs -p"=TypeName=JsonUInt64" -p"=JsonTypeName=uint64" -p"=ConversionOperatorToUInt64=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonInt128.Core.cs -p"=TypeName=JsonInt128" -p"=JsonTypeName=int128" -p"=ConversionOperatorToInt128=implicit"  -p="ValidateMethodName=TypeInt128" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonInt128.Basics.cs -p"=TypeName=JsonInt128" -p"=JsonTypeName=int128" -p"=ConversionOperatorToInt128=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonUInt128.Core.cs -p"=TypeName=JsonUInt128" -p"=JsonTypeName=uint128" -p"=ConversionOperatorToUInt128=implicit"  -p="ValidateMethodName=TypeUInt128" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonUInt128.Basics.cs -p"=TypeName=JsonUInt128" -p"=JsonTypeName=uint128" -p"=ConversionOperatorToUInt128=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonHalf.Core.cs -p"=TypeName=JsonHalf" -p"=JsonTypeName=half" -p"=ConversionOperatorToHalf=implicit"  -p="ValidateMethodName=TypeHalf" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonHalf.Basics.cs -p"=TypeName=JsonHalf" -p"=JsonTypeName=half" -p"=ConversionOperatorToHalf=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonSingle.Core.cs -p"=TypeName=JsonSingle" -p"=JsonTypeName=single" -p"=ConversionOperatorToSingle=implicit"  -p="ValidateMethodName=TypeSingle" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonSingle.Basics.cs -p"=TypeName=JsonSingle" -p"=JsonTypeName=single" -p"=ConversionOperatorToSingle=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonDouble.Core.cs -p"=TypeName=JsonDouble" -p"=JsonTypeName=double" -p"=ConversionOperatorToDouble=implicit"  -p="ValidateMethodName=TypeDouble" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonDouble.Basics.cs -p"=TypeName=JsonDouble" -p"=JsonTypeName=double" -p"=ConversionOperatorToDouble=implicit" ./Templates/FormattedNumberType.Basics.tt
t4 -o ./GeneratedFormatTypes/JsonDecimal.Core.cs -p"=TypeName=JsonDecimal" -p"=JsonTypeName=decimal" -p"=ConversionOperatorToDecimal=implicit"  -p="ValidateMethodName=TypeDecimal" ./Templates/FormattedNumberType.Core.tt
t4 -o ./GeneratedFormatTypes/JsonDecimal.Basics.cs -p"=TypeName=JsonDecimal" -p"=JsonTypeName=decimal" -p"=ConversionOperatorToDecimal=implicit" ./Templates/FormattedNumberType.Basics.tt

REM See ./Templates/CustomAny.Core.tt for details
t4 -o ./GeneratedFormatTypes/JsonAny.Core.cs -p"=TypeName=JsonAny" -p"=Summary=Represents any JSON value." -p"=HasConversionsToJsonAny=false" ./Templates/CustomAny.Core.tt
t4 -o ./GeneratedFormatTypes/JsonNotAny.Core.cs -p"=TypeName=JsonNotAny" -p"=Summary=Represents any JSON value, validating false." -p"=HasConversionsToJsonAny=true" ./Templates/CustomAny.Core.tt
