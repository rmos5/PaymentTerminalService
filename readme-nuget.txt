/* Sample nuget CLI */
nuget update -Self
.\nuget pack src\PaymentTerminalService.Model\PaymentTerminalService.Model.csproj -Build -Symbols -Properties Configuration=Release
.\nuget push PaymentTerminalService.Model.1.0.0.nupkg -Source "MyPackages" -ApiKey apikey
.\nuget pack src\PaymentTerminalService.Client\PaymentTerminalService.Client.csproj -Build -Symbols -Properties Configuration=Release
.\nuget push PaymentTerminalService.Client.1.0.0.nupkg -Source "MyPackages" -ApiKey apikey