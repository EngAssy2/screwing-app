
# 01  Run project go to folder 
cd D:\Project_Apps\Screwing_Hub_23_Apps\ScrewingHub

# 02 build and run project  for visual studio not using terminal 
dotnet build d:\Project_Apps\Screwing_Hub_23_Apps\ScrewingHub\ScrewingHub.sln
dotnet run --project src\ScrewingHub.App

# 03 build and release project static 64bit
dotnet publish src\ScrewingHub.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true

# 04 build and release project static 32bit
dotnet publish src\ScrewingHub.App -c Release -r win-x86 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true   

# 05 copy all file in publish folder to target folder
# source folder
D:\Project_Apps\Screwing_Hub_23_Apps\ScrewingHub\src\ScrewingHub.App\bin\Release\net8.0-windows\win-x64\publish
# target folder 
D:\Project_Apps\Screwing_Hub_23_Apps\Tool_Release\ScrewingHub_exe

# 06 copy all file in publish folder to target folder
# source folder
D:\Project_Apps\Screwing_Hub_23_Apps\ScrewingHub\src\ScrewingHub.App\bin\Release\net8.0-windows\win-x86\publish
# target folder 
D:\Project_Apps\Screwing_Hub_23_Apps\Tool_Release\ScrewingHub_exe_32
