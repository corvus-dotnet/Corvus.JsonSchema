$env:BUILDVAR_SkipBuildModuleVersionCheck = "true"
$env:BUILDVAR_SkipPrAutoflowEnrollmentCheck = "true"
& ./build.ps1 -Preview
