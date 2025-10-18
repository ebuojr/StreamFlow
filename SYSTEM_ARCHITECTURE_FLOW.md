# StreamFlow - E-Commerce WMS System Architecture

## 📋 Overview

This document provides comprehensive Mermaid diagrams illustrating the **StreamFlow** warehouse management system architecture, message flows, and order processing pipeline.

---

## 🏗️ High-Level System Architecture

```mermaid
graph TB
    subgraph "External Client"
        CLIENT[Client Application]
    end
    
    subgraph "API Gateway"
        ORDERAPI[OrderApi<br/>Request/Reply Gateway]
    end
    
    subgraph "Core Services"
        ERPAPI[ERPApi<br/>Order Management + Database]
        INVENTORY[InventoryService<br/>Stock Checker]
        PICKING[PickingService<br/>Warehouse Picking]
        PACKING[PackingService<br/>Warehouse Packing]
    end
    
    subgraph "Message Broker"
        RABBITMQ[RabbitMQ<br/>Event Bus + Priority Queues]
    end
    
    subgraph "Database"
        DB[(SQLite<br/>Orders + OrderItems<br/>+ Outbox)]
    end
    
    CLIENT -->|POST /api/order| ORDERAPI
    ORDERAPI <-->|Request/Reply<br/>CreateOrderRequest| RABBITMQ
    RABBITMQ <--> ERPAPI
    ERPAPI <--> DB
    ERPAPI -->|OrderCreated Event| RABBITMQ
    RABBITMQ --> INVENTORY
    INVENTORY -->|StockReserved<br/>StockUnavailable<br/>PartialStockReserved| RABBITMQ
    RABBITMQ --> PICKING
    PICKING -->|OrderPicked| RABBITMQ
    RABBITMQ --> PACKING
    PACKING -->|OrderPacked| RABBITMQ
    RABBITMQ --> ERPAPI
    
    style CLIENT fill:#e1f5ff
    style ORDERAPI fill:#fff3cd
    style ERPAPI fill:#d4edda
    style INVENTORY fill:#d1ecf1
    style PICKING fill:#d1ecf1
    style PACKING fill:#d1ecf1
    style RABBITMQ fill:#f8d7da
    style DB fill:#e2e3e5
```

---

## 🔄 Complete Order Processing Flow

### Scenario 1: Full Stock Availability (Happy Path)

```mermaid
sequenceDiagram
    participant Client
    participant OrderApi
    participant RabbitMQ
    participant ERPApi
    participant Database
    participant Inventory
    participant Picking
    participant Packing

    Client->>OrderApi: POST /api/order (Order Data)
    activate OrderApi
    
    OrderApi->>RabbitMQ: CreateOrderRequest (Request/Reply)
    activate RabbitMQ
    
    RabbitMQ->>ERPApi: CreateOrderRequest
    activate ERPApi
    
    ERPApi->>Database: BEGIN TRANSACTION
    ERPApi->>Database: INSERT Order + OrderItems
    Note over ERPApi,Database: Status: "Created"<br/>OrderItems.Status: "Pending"
    
    ERPApi->>Database: INSERT Outbox (OrderCreated Event)
    ERPApi->>Database: COMMIT TRANSACTION
    
    ERPApi->>RabbitMQ: CreateOrderResponse (OrderNo, Success)
    deactivate ERPApi
    
    RabbitMQ->>OrderApi: CreateOrderResponse
    deactivate RabbitMQ
    
    OrderApi->>Client: 200 OK (OrderNo)
    deactivate OrderApi
    
    Note over ERPApi: Background Worker<br/>polls Outbox table
    
    ERPApi->>RabbitMQ: Publish OrderCreated Event<br/>(from Outbox)
    ERPApi->>Database: Mark Outbox message processed
    
    RabbitMQ->>Inventory: OrderCreated Event
    activate Inventory
    
    Note over Inventory: Random Check (80%)<br/>All items Available ✅
    Inventory->>Inventory: Simulate Check (100-500ms)
    
    Inventory->>RabbitMQ: Publish StockReserved Event
    deactivate Inventory
    
    RabbitMQ->>ERPApi: StockReserved Event
    activate ERPApi
    ERPApi->>Database: UPDATE Order.OrderState = "StockReserved"
    ERPApi->>Database: UPDATE OrderItems.Status = "Available"
    deactivate ERPApi
    
    RabbitMQ->>Picking: StockReserved Event<br/>(Priority Queue: 9 or 1)
    activate Picking
    
    Note over Picking: Pick items from warehouse
    Picking->>Picking: Simulate Picking (2-5s)
    
    Picking->>RabbitMQ: Publish OrderPicked Event<br/>(Priority Header)
    deactivate Picking
    
    RabbitMQ->>ERPApi: OrderPicked Event
    activate ERPApi
    ERPApi->>Database: UPDATE Order.OrderState = "Picked"
    ERPApi->>Database: UPDATE OrderItems.Status = "Picked"
    deactivate ERPApi
    
    RabbitMQ->>Packing: OrderPicked Event
    activate Packing
    
    Note over Packing: Pack items for shipping
    Packing->>Packing: Calculate Box Size & Weight
    Packing->>Packing: Simulate Packing (1.5-3s)
    
    Packing->>RabbitMQ: Publish OrderPacked Event<br/>(FINAL STATE)
    deactivate Packing
    
    RabbitMQ->>ERPApi: OrderPacked Event
    activate ERPApi
    ERPApi->>Database: UPDATE Order.OrderState = "Packed"
    ERPApi->>Database: UPDATE OrderItems.Status = "Packed"
    deactivate ERPApi
    
    Note over Database: Order Complete! ✅<br/>Ready for Shipping
```

---

### Scenario 2: No Stock Availability

```mermaid
sequenceDiagram
    participant Client
    participant OrderApi
    participant RabbitMQ
    participant ERPApi
    participant Database
    participant Inventory

    Client->>OrderApi: POST /api/order (Order Data)
    OrderApi->>RabbitMQ: CreateOrderRequest
    RabbitMQ->>ERPApi: CreateOrderRequest
    
    ERPApi->>Database: INSERT Order + OrderItems<br/>(Status: "Created", Items: "Pending")
    ERPApi->>Database: INSERT Outbox (OrderCreated)
    ERPApi->>RabbitMQ: CreateOrderResponse (Success)
    RabbitMQ->>OrderApi: CreateOrderResponse
    OrderApi->>Client: 200 OK (OrderNo)
    
    Note over ERPApi: Outbox Worker publishes
    ERPApi->>RabbitMQ: Publish OrderCreated Event
    
    RabbitMQ->>Inventory: OrderCreated Event
    activate Inventory
    
    Note over Inventory: Random Check (80%)<br/>NO items Available ❌
    Inventory->>Inventory: Simulate Check (100-500ms)
    
    Inventory->>RabbitMQ: Publish StockUnavailable Event<br/>(UnavailableSkus, Reason)
    deactivate Inventory
    
    RabbitMQ->>ERPApi: StockUnavailable Event
    activate ERPApi
    ERPApi->>Database: UPDATE Order.OrderState = "StockUnavailable"
    ERPApi->>Database: UPDATE All OrderItems.Status = "Unavailable"
    deactivate ERPApi
    
    Note over Database: Order STOPS ❌<br/>No Picking/Packing
    Note over Client: Customer can query:<br/>GET /api/order/{id}/tracking<br/>Status: "StockUnavailable"
```

---

### Scenario 3: Partial Stock Availability

```mermaid
sequenceDiagram
    participant Client
    participant OrderApi
    participant RabbitMQ
    participant ERPApi
    participant Database
    participant Inventory
    participant Picking
    participant Packing

    Client->>OrderApi: POST /api/order (Order Data)
    OrderApi->>RabbitMQ: CreateOrderRequest
    RabbitMQ->>ERPApi: CreateOrderRequest
    
    ERPApi->>Database: INSERT Order + OrderItems<br/>(Status: "Created", Items: "Pending")
    ERPApi->>Database: INSERT Outbox (OrderCreated)
    ERPApi->>RabbitMQ: CreateOrderResponse (Success)
    RabbitMQ->>OrderApi: CreateOrderResponse
    OrderApi->>Client: 200 OK (OrderNo)
    
    ERPApi->>RabbitMQ: Publish OrderCreated Event
    
    RabbitMQ->>Inventory: OrderCreated Event
    activate Inventory
    
    Note over Inventory: Random Check (80% per item)<br/>Item A: Available ✅<br/>Item B: Unavailable ❌<br/>Item C: Available ✅
    Inventory->>Inventory: Simulate Check (100-500ms)
    
    Inventory->>RabbitMQ: Publish PartialStockReserved Event<br/>(AvailableItems: [A, C]<br/>UnavailableItems: [B])
    deactivate Inventory
    
    RabbitMQ->>ERPApi: PartialStockReserved Event
    activate ERPApi
    ERPApi->>Database: UPDATE Order.OrderState = "PartialDelivered"
    ERPApi->>Database: UPDATE Item A.Status = "Available"
    ERPApi->>Database: UPDATE Item B.Status = "Unavailable"
    ERPApi->>Database: UPDATE Item C.Status = "Available"
    deactivate ERPApi
    
    RabbitMQ->>Picking: PartialStockReserved Event<br/>(Priority Queue)
    activate Picking
    
    Note over Picking: Pick ONLY Available Items [A, C]
    Picking->>Picking: Simulate Picking (2-5s)
    
    Picking->>RabbitMQ: Publish OrderPicked Event<br/>(Items: [A, C] only)
    deactivate Picking
    
    RabbitMQ->>ERPApi: OrderPicked Event
    activate ERPApi
    ERPApi->>Database: UPDATE Order.OrderState = "Picked"
    ERPApi->>Database: UPDATE Item A.Status = "Picked"
    ERPApi->>Database: UPDATE Item B.Status = "Unavailable" (unchanged)
    ERPApi->>Database: UPDATE Item C.Status = "Picked"
    deactivate ERPApi
    
    RabbitMQ->>Packing: OrderPicked Event
    activate Packing
    
    Note over Packing: Pack ONLY Available Items [A, C]
    Packing->>Packing: Simulate Packing (1.5-3s)
    
    Packing->>RabbitMQ: Publish OrderPacked Event<br/>(Items: [A, C] only)
    deactivate Packing
    
    RabbitMQ->>ERPApi: OrderPacked Event
    activate ERPApi
    ERPApi->>Database: UPDATE Order.OrderState = "Packed"
    ERPApi->>Database: UPDATE Item A.Status = "Packed"
    ERPApi->>Database: UPDATE Item B.Status = "Unavailable" (unchanged)
    ERPApi->>Database: UPDATE Item C.Status = "Packed"
    deactivate ERPApi
    
    Note over Database: Partial Order Complete! ⚠️<br/>Items A, C: Packed ✅<br/>Item B: Unavailable ❌
```

---

## 🎯 Service Responsibilities

```mermaid
graph LR
    subgraph "OrderApi - API Gateway"
        OA1[Request/Reply Pattern]
        OA2[No Business Logic]
        OA3[Synchronous Response]
    end
    
    subgraph "ERPApi - Order Management"
        EA1[Order CRUD Operations]
        EA2[Database Transactions]
        EA3[Outbox Pattern]
        EA4[Event Consumers]
        EA5[State Management]
        EA6[Order Tracking API]
    end
    
    subgraph "InventoryService - Stock Management"
        IS1[Random Stock Check 80%]
        IS2[Content-Based Router]
        IS3[Publish 3 Event Types]
        IS4[Priority Fast-Track]
    end
    
    subgraph "PickingService - Warehouse Picking"
        PS1[Pick Items from Warehouse]
        PS2[Priority Queue Consumer]
        PS3[Full & Partial Orders]
        PS4[Content Enricher]
    end
    
    subgraph "PackingService - Warehouse Packing"
        PAS1[Pack Items for Shipping]
        PAS2[Calculate Box Size]
        PAS3[Calculate Weight]
        PAS4[Final State Publisher]
    end
    
    style OA1 fill:#fff3cd
    style EA1 fill:#d4edda
    style IS1 fill:#d1ecf1
    style PS1 fill:#d1ecf1
    style PAS1 fill:#d1ecf1
```

---

## 📊 Database Schema

```mermaid
erDiagram
    Order ||--o{ OrderItem : contains
    Order ||--|| Customer : has
    Order ||--|| Payment : has
    Order ||--|| ShippingAddress : has
    Order ||--o{ Outbox : "generates events"
    
    Order {
        Guid Id PK
        int OrderNo UK "Auto-increment from 1000"
        DateTime CreatedAt
        string OrderState "Created|StockReserved|PartialDelivered|Picked|Packed|StockUnavailable|Failed"
        string CountryCode
        decimal TotalAmount
        Guid CustomerId
    }
    
    OrderItem {
        Guid Id PK
        Guid OrderId FK
        string Sku
        string Name
        int Quantity
        decimal UnitPrice
        decimal TotalPrice
        string Status "Pending|Available|Unavailable|Picked|Packed"
    }
    
    Customer {
        Guid Id
        string FirstName
        string LastName
        string Email
        string Phone
    }
    
    Payment {
        string PaymentMethod
        string PaymentStatus
        DateTime PaidAt
        string Currency
        decimal Amount
    }
    
    ShippingAddress {
        string Street
        string City
        string State
        string PostalCode
        string Country "DK=Priority, Others=Standard"
    }
    
    Outbox {
        Guid Id PK
        string MessageType
        string Payload "JSON"
        DateTime CreatedAt
        DateTime ProcessedAt "NULL=Pending"
        int RetryCount
    }
```

---

## 🔀 Event Flow by Type

```mermaid
graph TB
    subgraph "Event Types"
        OC[OrderCreated]
        SR[StockReserved]
        PSR[PartialStockReserved]
        SU[StockUnavailable]
        OP[OrderPicked]
        OPA[OrderPacked]
    end
    
    subgraph "Publishers"
        ERPOUT[ERPApi - Outbox Worker]
        INV[InventoryService]
        PICK[PickingService]
        PACK[PackingService]
    end
    
    subgraph "Consumers"
        INVC[Inventory Consumer]
        ERPSC[ERPApi - State Consumers]
        PICKC[Picking Consumers]
        PACKC[Packing Consumer]
    end
    
    ERPOUT -->|Publishes| OC
    OC --> INVC
    
    INVC -->|80% Orders| SR
    INVC -->|~20% Orders| PSR
    INVC -->|<1% Orders| SU
    
    SR --> ERPSC
    PSR --> ERPSC
    SU --> ERPSC
    
    SR --> PICKC
    PSR --> PICKC
    
    PICK -->|Publishes| OP
    OP --> ERPSC
    OP --> PACKC
    
    PACK -->|Publishes| OPA
    OPA --> ERPSC
    
    style OC fill:#cce5ff
    style SR fill:#d4edda
    style PSR fill:#fff3cd
    style SU fill:#f8d7da
    style OP fill:#d1ecf1
    style OPA fill:#d4edda
```

---

## 🏃 Priority Queue System

```mermaid
graph TB
    subgraph "Order Classification"
        ODK[Order from Denmark<br/>Country = 'DK']
        OOTHER[Order from Other Countries<br/>Country != 'DK']
    end
    
    subgraph "Priority Assignment"
        PRI9[Priority = 9<br/>OrderType = 'Priority']
        PRI1[Priority = 1<br/>OrderType = 'Standard']
    end
    
    subgraph "Processing Speed"
        FAST[Fast Check: 100ms<br/>Fast Picking<br/>High Priority Queue]
        NORMAL[Normal Check: 500ms<br/>Normal Picking<br/>Standard Priority Queue]
    end
    
    ODK --> PRI9
    OOTHER --> PRI1
    
    PRI9 --> FAST
    PRI1 --> NORMAL
    
    style ODK fill:#d4edda
    style PRI9 fill:#ffc107
    style FAST fill:#28a745
```

---

## 🎨 Enterprise Integration Patterns Used

```mermaid
mindmap
  root((StreamFlow EIP))
    Request Reply
      OrderApi to ERPApi
      Synchronous Communication
      CreateOrderRequest Response
    
    Outbox Pattern
      Transactional Messaging
      Reliable Event Publishing
      Prevent Message Loss
      Eventual Consistency
    
    Content Enricher
      ERPApi enriches OrderCreated
      All downstream services have full context
      No HTTP calls needed
      Customer + Address + Items
    
    Content Based Router
      InventoryService routes by availability
      3 Routes: Full Partial None
      Dynamic routing logic
      80% availability rate
    
    Priority Queue
      RabbitMQ x-max-priority 10
      DK Orders get priority 9
      Other Orders get priority 1
      Fast track critical orders
    
    Guaranteed Delivery
      Outbox ensures publish
      RabbitMQ persistence
      Consumer retries
      Dead Letter Queue
    
    Event Driven
      Loose Coupling
      Asynchronous Processing
      Scalable Architecture
      State Management via Events
```

---

## 📈 Order State Transitions

```mermaid
stateDiagram-v2
    [*] --> Created: Order Received
    
    Created --> StockReserved: All Items Available (80%)
    Created --> PartialDelivered: Some Items Available (20%)
    Created --> StockUnavailable: No Items Available (<1%)
    
    StockReserved --> Picked: Picking Complete
    PartialDelivered --> Picked: Picking Complete (Available Items)
    
    Picked --> Packed: Packing Complete
    
    Packed --> [*]: Order Complete ✅
    StockUnavailable --> [*]: Order Failed ❌
    
    Created --> Failed: System Error
    StockReserved --> Failed: System Error
    Picked --> Failed: System Error
    
    Failed --> [*]: Order Failed ❌
    
    note right of StockReserved
        All items marked "Available"
        Full order fulfillment
    end note
    
    note right of PartialDelivered
        Some items "Available"
        Some items "Unavailable"
        Partial fulfillment
    end note
    
    note right of StockUnavailable
        All items marked "Unavailable"
        Order cannot be fulfilled
    end note
    
    note right of Packed
        All available items "Packed"
        Ready for shipping
        Terminal success state
    end note
```

---

## 🔍 OrderItem Status Lifecycle

```mermaid
stateDiagram-v2
    [*] --> Pending: Order Created
    
    Pending --> Available: Stock Check (Item In Stock)
    Pending --> Unavailable: Stock Check (Item Out of Stock)
    
    Available --> Picked: Picking Complete
    Available --> Unavailable: Stock Check Failed
    
    Picked --> Packed: Packing Complete
    
    Packed --> [*]: Item Shipped ✅
    Unavailable --> [*]: Item Not Fulfilled ❌
    
    note right of Pending
        Default state
        Awaiting inventory check
    end note
    
    note right of Available
        Item reserved
        Ready for picking
    end note
    
    note right of Unavailable
        Out of stock
        Not picked/packed
        Remains in this state
    end note
    
    note right of Picked
        Item picked from warehouse
        Ready for packing
    end note
    
    note right of Packed
        Item packed
        Ready for shipping
        Terminal success state
    end note
```

---

## 🛠️ Technology Stack

```mermaid
graph TB
    subgraph "Backend Framework"
        DOTNET[.NET 9<br/>ASP.NET Core]
    end
    
    subgraph "Message Broker"
        RABBIT[RabbitMQ<br/>Event Bus + Priority Queues]
        MASSTRANSIT[MassTransit<br/>Messaging Framework]
    end
    
    subgraph "Database"
        SQLITE[SQLite<br/>Lightweight SQL Database]
        EF[Entity Framework Core 9.0.10<br/>ORM + Migrations]
    end
    
    subgraph "Services"
        ORDERAPI[OrderApi - Gateway]
        ERPAPI[ERPApi - Core]
        INV[InventoryService]
        PICK[PickingService]
        PACK[PackingService]
    end
    
    DOTNET --> ORDERAPI
    DOTNET --> ERPAPI
    DOTNET --> INV
    DOTNET --> PICK
    DOTNET --> PACK
    
    MASSTRANSIT --> RABBIT
    RABBIT <--> ORDERAPI
    RABBIT <--> ERPAPI
    RABBIT <--> INV
    RABBIT <--> PICK
    RABBIT <--> PACK
    
    EF --> SQLITE
    ERPAPI --> EF
    
    style DOTNET fill:#512bd4
    style RABBIT fill:#ff6600
    style MASSTRANSIT fill:#1e90ff
    style SQLITE fill:#003b57
    style EF fill:#512bd4
```

---

## 📋 API Endpoints

### OrderApi (API Gateway)
```http
POST /api/order
- Body: Order JSON
- Response: CreateOrderResponse (OrderNo, Success)
- Pattern: Request/Reply to ERPApi
```

### ERPApi (Order Management)
```http
POST /api/order
- Body: Order JSON
- Response: CreateOrderResponse (OrderNo, Success)
- Action: Create order + Outbox message

GET /api/order
- Response: List of all orders

GET /api/order/{id}
- Response: Order details by ID

GET /api/order/{state}
- Response: Orders by state (Created, Picked, Packed, etc.)

GET /api/order/{id}/tracking
- Response: OrderTrackingResponse with status history

PUT /api/order/{id}/state
- Body: New state (string)
- Response: 204 No Content
```

---

## 🔢 Configuration Values

### Priority Settings
- **Denmark Orders**: Priority = 9, OrderType = "Priority"
- **Other Countries**: Priority = 1, OrderType = "Standard"

### Processing Times
- **Inventory Check (Priority)**: 100ms
- **Inventory Check (Standard)**: 500ms
- **Picking Time**: 2000-5000ms (random)
- **Packing Time**: 1500-3000ms (random)

### Inventory Availability
- **Item Availability Rate**: 80% per item
- **Full Stock**: ~80% of orders (all items available)
- **Partial Stock**: ~20% of orders (some items unavailable)
- **No Stock**: <1% of orders (all items unavailable)

### Queue Settings
- **Max Priority**: 10
- **Priority Queue Prefetch**: 4
- **Retry Attempts**: 3
- **Retry Interval**: 5 seconds

### Database
- **OrderNo Starting Value**: 1000
- **Auto-Increment**: Yes
- **Unique Index**: OrderNo

---

## 🎯 Key Features

1. ✅ **Transactional Outbox Pattern**: Ensures reliable event publishing
2. ✅ **Content Enricher Pattern**: Eliminates HTTP calls between services
3. ✅ **Priority Queue System**: Fast-tracks Denmark orders
4. ✅ **Random Inventory Logic**: 80% availability simulation
5. ✅ **Partial Order Fulfillment**: Processes available items separately
6. ✅ **Item-Level Status Tracking**: Granular fulfillment visibility
7. ✅ **Event-Driven Architecture**: Loose coupling, high scalability
8. ✅ **Request/Reply Gateway**: Synchronous client response
9. ✅ **Dead Letter Queue**: Handles failed messages
10. ✅ **Retry Mechanism**: Automatic retry on failures

---

## 🚀 Message Flow Summary

```mermaid
graph LR
    A[Client] -->|1. POST Order| B[OrderApi]
    B -->|2. Request/Reply| C[ERPApi]
    C -->|3. Save to DB + Outbox| D[Database]
    C -->|4. Reply Success| B
    B -->|5. Return OrderNo| A
    C -->|6. Publish via Outbox| E[RabbitMQ]
    E -->|7. OrderCreated| F[Inventory]
    F -->|8. StockReserved/Partial/Unavailable| E
    E -->|9. Update State| C
    E -->|10. Pick Items| G[Picking]
    G -->|11. OrderPicked| E
    E -->|12. Update State| C
    E -->|13. Pack Items| H[Packing]
    H -->|14. OrderPacked| E
    E -->|15. Update State FINAL| C
    C -->|16. Store Final State| D
    
    style A fill:#e1f5ff
    style B fill:#fff3cd
    style C fill:#d4edda
    style D fill:#e2e3e5
    style E fill:#f8d7da
    style F fill:#d1ecf1
    style G fill:#d1ecf1
    style H fill:#d1ecf1
```

---

## 📚 Related Documentation

- **Item Status Tracking**: `ORDERITEM_STATUS_TRACKING.md`
- **Random Inventory Logic**: `INVENTORY_RANDOM_STOCK_IMPLEMENTATION.md`
- **Database Migrations**: `DATABASE_MIGRATION_STATUS.md`
- **NET 9 Downgrade**: `NET9_DOWNGRADE_FIXES.md`
- **Code Cleanup Report**: `UNUSED_CODE_CLEANUP_REPORT.md`

---

## 🎉 Summary

**StreamFlow** is a modern, event-driven warehouse management system built with:
- **.NET 9** for high performance
- **RabbitMQ + MassTransit** for reliable messaging
- **SQLite + EF Core** for data persistence
- **Enterprise Integration Patterns** for scalable architecture
- **Microservices** for modularity and maintainability

The system handles **full, partial, and no-stock scenarios** with **priority queue processing** for critical orders, **transactional messaging** for reliability, and **comprehensive tracking** for complete visibility.

**Ready for production deployment!** 🚀
