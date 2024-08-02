# Change directory to the Corvus.Json directory
Set-Location -Path 'Corvus.Json.ExtendedTypes\Corvus.Json'

# Run the BuildTemplates.ps1 script
& '.\BuildTemplates.ps1'

# Change back to the original directory
Set-Location -Path '..\..'