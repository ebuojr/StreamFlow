# StreamFlow WMS

A microservices-based Warehouse Management System (WMS) built with .NET 10, MassTransit, and RabbitMQ. The system demonstrates enterprise integration patterns for processing orders through multiple stages: order creation, inventory checking, picking, and packing.

## Architecture Overview

| Project | Description |
|---------|-------------|
| **OrderApi** | REST API gateway for order submission |
| **ERPApi** | Core ERP system managing order persistence and orchestration |
| **InventoryService** | Worker service for inventory availability checks |
| **PickingService** | Worker service for order picking operations |
| **PackingService** | Worker service for order packing operations |
| **BlazorUI** | WebAssembly front-end application |
| **Contracts** | Shared message contracts and DTOs |
| **Entities** | Shared domain models |

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop) (for RabbitMQ and Seq)
- [k6](https://k6.io/docs/getting-started/installation/) (for performance tests)

## Getting Started

### 1. Start Infrastructure (RabbitMQ & Seq)

```bash
docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:management
docker run -d --name seq -p 5341:80 datalust/seq:latest
```

- **RabbitMQ Management UI**: http://localhost:15672 (guest/guest)
- **Seq Logging UI**: http://localhost:5341

### 2. Run All Services

Open multiple terminals and run each service:

```bash
# Terminal 1 - ERPApi
cd ERPApi
dotnet run

# Terminal 2 - OrderApi
cd OrderApi
dotnet run

# Terminal 3 - InventoryService
cd InventoryService
dotnet run

# Terminal 4 - PickingService
cd PickingService
dotnet run

# Terminal 5 - PackingService
cd PackingService
dotnet run

# Terminal 6 - BlazorUI (optional)
cd BlazorUI
dotnet run
```

Or run the entire solution from the root:

```bash
dotnet build StreamFlow.slnx
```

## Running Tests

### Unit Tests

Run all unit tests using the .NET CLI:

```bash
dotnet test StreamFlow.Tests/StreamFlow.Tests.csproj
```

With detailed output:

```bash
dotnet test StreamFlow.Tests/StreamFlow.Tests.csproj --logger "console;verbosity=detailed"
```

### Performance Tests (k6)

Ensure all services are running, then execute the smoke test:

```bash
cd PerformanceTests
k6 run smoke-test.js
```

To run with custom base URL:

```bash
k6 run -e BASE_URL=https://localhost:7033 smoke-test.js
```

The smoke test runs 5 virtual users for 30 seconds and validates:
- 95th percentile response time < 500ms
- Error rate < 1%

## Technology Stack

- **.NET 10** - All projects
- **MassTransit** - Service bus abstraction
- **RabbitMQ** - Message broker
- **Entity Framework Core** - Data persistence (SQLite)
- **Serilog & Seq** - Structured logging
- **FluentValidation** - Request validation
- **xUnit** - Unit testing framework
- **k6** - Performance testing

## License

This project is for educational purposes.
