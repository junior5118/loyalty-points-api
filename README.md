# Loyalty Points API

A small **ASP.NET Core 8** web API that models a loyalty program: customers earn points on purchases, activate their account, and redeem points for discount vouchers.

> **Demo project.** It uses fictional data and was built to show the patterns I work with professionally (business rules, API security, importing files, tests). It contains **no client code, no client data and no secrets**.
> Developed with AI assistance (Claude).

## What it demonstrates

| Topic | Where |
|---|---|
| **Business rules in the domain, not in controllers** | `Account`, `PointsCalculator` in `LoyaltyPoints.Core` |
| **Activation rule**: points earned before activation stay *pending* and are only released when the account is activated | `Account.RegisterPurchase` / `Account.Activate` |
| **Redemption with limits**: minimum, maximum per request, balance check, discount capped at **99.99%** (never 100%) | `Account.Redeem` |
| **Ledger** of every movement (pending, credited, redeemed) | `LedgerEntry` |
| **Bulk import** of a plain-text export (`cpf;amount` per line) with line-level errors that never echo the CPF | `PurchaseFileParser`, `POST /imports/purchases` |
| **Personal data protection**: CPF validated, stored only as an HMAC hash plus a masked version | `Cpf.Hash`, `Cpf.Mask` |
| **API key** compared in constant time; secrets come from the environment | `Program.cs` |
| **Rate limiting per client IP**, stricter on sensitive routes (activate, redeem, import) | `Program.cs` |
| **Unit tests (xUnit)** for rules, CPF and the file parser | `tests/LoyaltyPoints.Tests` |

## Run it

Requirements: .NET 8 SDK.

```bash
# Secrets are NOT in the repository. Create your own random values:
export Api__Key="<a long random string>"
export Security__CpfHashKey="<another long random string>"

dotnet run --project src/LoyaltyPoints.Api
```

Or, for local development, use user-secrets:

```bash
dotnet user-secrets set "Api:Key" "<random>" --project src/LoyaltyPoints.Api
dotnet user-secrets set "Security:CpfHashKey" "<random>" --project src/LoyaltyPoints.Api
```

The API refuses to start if these values are missing. See `.env.example`.

## Endpoints

All routes (except `/health`) require the header `X-Api-Key`.

| Method | Route | Purpose |
|---|---|---|
| `POST` | `/accounts` | Create an account `{ "cpf": "529.982.247-25" }` |
| `GET` | `/accounts/{id}` | Account summary (CPF is always masked) |
| `POST` | `/accounts/{id}/purchases` | Register a purchase `{ "amount": 300 }` |
| `POST` | `/accounts/{id}/activate` | Activate and release pending points |
| `POST` | `/accounts/{id}/redeem` | Redeem points `{ "points": 20 }` |
| `POST` | `/imports/purchases` | Import `cpf;amount` lines (`text/plain`) |
| `GET` | `/health` | Health check |

Points: 1 point per R$ 10 spent; purchases of R$ 200 or more earn double. 10 points = 1% discount. All values are configurable in `appsettings.json`.

## Tests

```bash
dotnet test
```

## Design notes

- **Why pending points?** A common real-world bug is points that appear in reports but not for the customer, because the account was never activated. Modeling it explicitly makes the behavior visible and testable.
- **Why hash the CPF?** The API only needs to *find* an account, not to read the CPF back. A keyed hash (HMAC-SHA256) lets it look accounts up without storing the raw number.
- **What would come next:** integration tests with `WebApplicationFactory`, a real SQL Server provider, idempotency keys for imports, and structured logging with the [safe-log-queue](https://github.com/junior5118/safe-log-queue) library.

## License

MIT
