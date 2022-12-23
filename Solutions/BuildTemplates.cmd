REM Runs all the template build commands
cd Corvus.Json.CodeGeneration.6\Corvus.Json.CodeGeneration.Draft6
start cmd.exe /C BuildTemplates.cmd
cd ..\..\Corvus.Json.CodeGeneration.7\Corvus.Json.CodeGeneration.Draft7
start cmd.exe /C BuildTemplates.cmd
cd ..\..\Corvus.Json.CodeGeneration.201909\Corvus.Json.CodeGeneration.Draft201909
start cmd.exe /C BuildTemplates.cmd
cd ..\..\Corvus.Json.CodeGeneration.202012\Corvus.Json.CodeGeneration.Draft202012
start cmd.exe /C BuildTemplates.cmd
cd ..\..\Corvus.Json.ExtendedTypes\Corvus.Json
start cmd.exe /C BuildTemplates.cmd
cd ..\..