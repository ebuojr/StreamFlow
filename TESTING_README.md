# StreamFlow WMS - Testing

Simple testing setup for StreamFlow WMS.

# StreamFlow WMS - Testing

Testing setup for StreamFlow WMS with focus on real functionality.

## Unit Tests

### Setup
Test project: `StreamFlow.Tests/` with **10 meaningful tests** in `BasicTests.cs`

### Run Tests
```powershell
# Run all tests
dotnet test StreamFlow.Tests/StreamFlow.Tests.csproj

# Run with detailed output
dotnet test StreamFlow.Tests/StreamFlow.Tests.csproj --logger "console;verbosity=detailed"
```

### What's Tested

**Order Business Logic (4 tests)**
- `Order_FindOrderType_DanishOrderIsPriority` - Tests DK orders are Priority
- `Order_FindOrderType_NonDanishOrderIsStandard` - Tests non-DK orders are Standard
- `Order_GetPriority_DanishOrderHasPriority9` - Tests Priority orders get priority 9
- `Order_GetPriority_StandardOrderHasPriority1` - Tests Standard orders get priority 1

**Order Validation (3 tests)**
- `OrderService_ValidateOrder_RejectsNullOrder` - Tests null order rejection
- `OrderService_ValidateOrder_RejectsOrderWithNoItems` - Tests empty items validation
- `OrderService_ValidateOrder_RejectsOrderWithNoCustomer` - Tests customer requirement

**Database & Outbox (3 tests)**
- `OrderDbContext_HasMassTransitOutboxTables` - **Verifies MassTransit Outbox pattern** (OutboxMessage, OutboxState tables)
- `OrderDbContext_OrderHasRowVersionProperty` - **Tests optimistic concurrency** (RowVersion as concurrency token)
- `Order_IsStoredWithOwnedEntities` - Tests EF Core owned entities (Customer, ShippingAddress, Payment flattened in Order table)

### Why These Tests Matter

✅ **Business Logic**: Priority routing (DK=9, others=1) is critical for queue priority  
✅ **Data Integrity**: Validation prevents bad orders reaching the system  
✅ **Outbox Pattern**: Ensures transactional event publishing with MassTransit  
✅ **Concurrency Control**: RowVersion prevents lost updates in concurrent scenarios  
✅ **EF Mapping**: Owned entities reduce joins and improve performance

---

## Performance Tests (k6)

### Prerequisites
Install k6:
```powershell
choco install k6
```

Verify installation:
```powershell
k6 version
```

### Run Smoke Test

1. Start ERPApi:
```powershell
cd ERPApi
dotnet run
```

2. In a new terminal, run k6 test:
```powershell
cd PerformanceTests
k6 run smoke-test.js
```

### Test Details
- **Duration:** 30 seconds
- **Virtual Users:** 1
- **Endpoint:** POST /api/order
- **Test Data:** Fixed Danish order (Mette Hansen, 1x Plus Size Maxi Dress)
- **Thresholds:**
  - P95 response time < 500ms
  - Error rate < 1%

### Custom URL
```powershell
k6 run -e BASE_URL=https://your-url:port smoke-test.js
```

---

## Expected Results

### Unit Tests
- ✅ All tests pass
- ✅ Order validation works correctly
- ✅ Events are published from consumers
- ✅ Logging is verified

### Smoke Test
```
✓ status is 200
✓ has orderNo
✓ isSuccessfullyCreated

checks.........................: 100.00%
http_req_duration..............: avg=XX ms p(95)=XX ms
http_req_failed................: 0.00%
```

---

## Quick Reference

```powershell
# Unit tests
dotnet test

# Smoke test (start ERPApi first!)
cd PerformanceTests
k6 run smoke-test.js
```
