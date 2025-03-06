# M365 Exchange demo using Azure Graph API

## Using Distributed SQL Server Cache for storing refresh token across invocations

- [Microsoft Article - Distributed token caches](https://learn.microsoft.com/en-us/aspnet/core/performance/caching/distributed?view=aspnetcore-9.0#distributed-sql-server-cachehttps://learn.microsoft.com/en-us/entra/msal/dotnet/how-to/token-cache-serialization?tabs=aspnetcore#distributed-token-caches)
- Create distributed cache table
    ```powershell
    dotnet tool install --global dotnet-sql-cache
    dotnet sql-cache create "Data Source=hpv-fcdbdev1-1.co.ihc.com; Database=SHWK;Integrated Security=True;Max Pool Size=250; Connection Timeout=90;Command Timeout=90;TrustServerCertificate=True" dbo TestCache
    ```
