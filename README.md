# aspire

A .NET Aspire distributed e-shop sample: a Blazor **WebApp** frontend backed by **Catalog** and **Basket**
microservices, with a dedicated **Identity** service issuing JWTs for authentication and role-based
(**User** / **Admin**) authorization.

## Architecture

| Project | Description |
|---|---|
| `AppHost` | Orchestrates all resources and services via .NET Aspire |
| `Identity` | Issues JWT access/refresh tokens; owns the user/role store (ASP.NET Core Identity + PostgreSQL) |
| `Catalog` | Product catalog API (PostgreSQL). Reads are public; create/update/delete require the `Admin` role |
| `Basket` | Shopping basket API (Redis). Requires a valid JWT; users may only access their own basket |
| `WebApp` | Blazor Server frontend — product browsing/search, login/register, basket, and an admin product page |
| `ServiceDefaults` | Shared Aspire service defaults (telemetry, health checks, service discovery, JWT validation, messaging) |

Backing services: PostgreSQL, Redis, RabbitMQ (all provisioned automatically by AppHost via Docker).

## Prerequisites

- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/) (running) — AppHost provisions PostgreSQL, Redis, and RabbitMQ as containers

## Running the project

```
cd eshop-distributed/src
dotnet run --project AppHost
```

This starts the Aspire dashboard (URL printed in the console) where you can see every resource and
open the WebApp's live URL, e.g.:

```
https://<webapp-host>:<port>/login
```

### Default seeded credentials

| Role | Email | Password |
|---|---|---|
| Admin | `admin@eshop.com` | `Admin@123` |

New accounts created via **Register** are assigned the `User` role automatically. Only the seeded
account (or one manually promoted) has `Admin` access — visible via the Admin nav link and the
`/admin/products` page.

## Solution layout

```
eshop-distributed/
  src/
    AppHost/        # Aspire orchestration
    Identity/       # Auth service (JWT issuance, users/roles)
    Catalog/        # Product catalog API
    Basket/         # Shopping basket API
    WebApp/         # Blazor Server frontend
    ServiceDefaults/# Shared cross-cutting concerns
```
