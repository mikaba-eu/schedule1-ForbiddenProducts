# ForbiddenProducts

A Schedule I mod that blocks product handovers per customer using a configurable JSON mapping.

Author: MiKiBa

## Features
- Config-based forbidden products per customer
- Runtime checks in customer/handover UI flows
- Optional debug files for diagnostics

## Build
```bash
dotnet build ForbiddenProducts.csproj -c Release
```

## Config
Default embedded config source:
- `customer_forbidden_products.proposal.json`

Runtime config path:
- `UserData/ForbiddenProducts/customer_forbidden_products.json`
