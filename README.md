[![Build Status](https://dev.azure.com/johnwatson484/John%20D%20Watson/_apis/build/status/Azure%20Relay%20Reverse%20Proxy?branchName=main)](https://dev.azure.com/johnwatson484/John%20D%20Watson/_build/latest?definitionId=51&branchName=main)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=johnwatson484_azure-relay-reverse-proxy&metric=alert_status)](https://sonarcloud.io/dashboard?id=johnwatson484_azure-relay-reverse-proxy)

# Azure Relay Reverse Proxy

Reverse proxy for integration with Azure Relay.

## Prerequisites
- .NET 5

## Setup

Add proxy configurations to `appsettings.json`.

For example:

```
{
  "Proxies": [
    {
      "ConnectionString": "relay1ConnectionString",
      "TargetUri": "http://website.test.relay:8087"
    },
    {
      "ConnectionString": "relay2ConnectionString",
      "TargetUri": "http://website.test.relay:8088"
    }
  ]
}
```

## Start the reverse proxy

```
cd AzureRelayReverseProxy
dotnet run
```
