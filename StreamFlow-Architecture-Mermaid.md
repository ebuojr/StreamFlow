# StreamFlow EIP Architecture - Mermaid Diagram

## Main Flow Diagram

```mermaid
graph LR
    %% Styling
    classDef endpoint fill:#d5e8d4,stroke:#82b366,stroke-width:2px
    classDef queue fill:#fff2cc,stroke:#d6b656,stroke-width:2px
    classDef priorityQueue fill:#ffe6cc,stroke:#d79b00,stroke-width:3px
    classDef enricher fill:#e1d5e7,stroke:#9673a6,stroke-width:2px
    classDef router fill:#dae8fc,stroke:#6c8ebf,stroke-width:2px
    classDef dlc fill:#f8cecc,stroke:#b85450,stroke-width:2px
    classDef broker fill:#fff2cc,stroke:#d6b656,stroke-width:4px
    classDef complete fill:#d5e8d4,stroke:#82b366,stroke-width:3px

    %% Main Flow
    Client[External Client]
    OrderApi[OrderApi<br/>Message Endpoint]:::endpoint
    ERPApi[ERPApi<br/>Content Enricher<br/>+ Transactional Outbox]:::enricher
    ERPGateway[ERPGateway<br/>Polling Consumer<br/>Outbox Relay]:::enricher
    RabbitMQ[RabbitMQ<br/>Message Broker]:::broker
    
    InventoryQueue[inventory-check<br/>Point-to-Point Channel]:::queue
    InventoryService[InventoryService<br/>Content-Based Router]:::router
    
    PickingQueue[picking-stock-reserved<br/>Priority Queue<br/>x-max-priority: 10]:::priorityQueue
    PickingService[PickingService<br/>Message Translator]:::endpoint
    
    PackingQueue[packing-order-picked<br/>Point-to-Point Channel]:::queue
    PackingService[PackingService<br/>Message Endpoint]:::endpoint
    
    Complete[✅ Order Complete<br/>Ready for Shipment]:::complete
    
    %% Connections
    Client -->|HTTP POST<br/>CreateOrderRequest| OrderApi
    OrderApi -->|Forward| ERPApi
    ERPApi -->|Store in Outbox| ERPGateway
    ERPGateway -->|Poll & Publish<br/>OrderCreated| RabbitMQ
    
    RabbitMQ -->|Route| InventoryQueue
    InventoryQueue -->|Consume| InventoryService
    InventoryService -->|Publish<br/>StockReserved| RabbitMQ
    
    RabbitMQ -->|Route with Priority| PickingQueue
    PickingQueue -->|Consume<br/>Priority Order| PickingService
    PickingService -->|Publish<br/>OrderPicked| RabbitMQ
    
    RabbitMQ -->|Route| PackingQueue
    PackingQueue -->|Consume| PackingService
    PackingService -->|Publish<br/>OrderPacked| Complete
```

## Content Enricher Pattern (ERPApi)

```mermaid
graph TB
    classDef input fill:#dae8fc,stroke:#6c8ebf,stroke-width:2px
    classDef process fill:#e1d5e7,stroke:#9673a6,stroke-width:2px
    classDef output fill:#d5e8d4,stroke:#82b366,stroke-width:2px

    Input[Minimal Order<br/>OrderNo: 1001<br/>CustomerId: 123]:::input
    
    Enrich[ERPApi Content Enricher]:::process
    
    Input --> Enrich
    
    Enrich --> DB1[Fetch Customer Details]
    Enrich --> DB2[Fetch Order Items]
    Enrich --> DB3[Calculate Shipping Address]
    Enrich --> DB4[Determine Priority]
    
    DB1 --> Output
    DB2 --> Output
    DB3 --> Output
    DB4 --> Output
    
    Output[Enriched OrderCreated Event<br/>✓ Customer Name, Email<br/>✓ Full Item Details<br/>✓ Shipping Address<br/>✓ Priority Level<br/>✓ Payment Info]:::output
```

## Content-Based Router Pattern (InventoryService)

```mermaid
graph TB
    classDef input fill:#fff2cc,stroke:#d6b656,stroke-width:2px
    classDef router fill:#dae8fc,stroke:#6c8ebf,stroke-width:2px
    classDef priority fill:#ffe6cc,stroke:#d79b00,stroke-width:3px
    classDef standard fill:#fff2cc,stroke:#d6b656,stroke-width:2px

    Queue[inventory-check Queue]:::input
    Router{Content-Based Router<br/>Check Priority & OrderType}:::router
    
    Queue --> Router
    
    Router -->|Priority = 9<br/>AND<br/>OrderType = DK| FastTrack[Fast Track Path<br/>Priority: 10]:::priority
    Router -->|Standard Order| Standard[Standard Path<br/>Priority: 5]:::standard
    
    FastTrack --> PickingPriority[picking-stock-reserved<br/>Priority Queue<br/>Handled First]:::priority
    Standard --> PickingStandard[picking-stock-reserved<br/>Priority Queue<br/>Normal Processing]:::standard
```

## Dead Letter Channel Pattern

```mermaid
graph TB
    classDef normal fill:#d5e8d4,stroke:#82b366,stroke-width:2px
    classDef retry fill:#fff2cc,stroke:#d6b656,stroke-width:2px
    classDef dlc fill:#f8cecc,stroke:#b85450,stroke-width:2px
    classDef fault fill:#f8cecc,stroke:#b85450,stroke-width:3px

    Consumer[Message Consumer<br/>InventoryService/Picking/Packing]:::normal
    
    Consumer -->|Success| Process[Process Message]:::normal
    Consumer -->|Failure| Retry{Retry Policy<br/>Attempt < 3?}:::retry
    
    Retry -->|Yes| Wait[Wait 5 seconds]:::retry
    Wait --> Consumer
    
    Retry -->|No<br/>Max Retries Exceeded| DLC[Dead Letter Channel<br/>inventory-dead-letter<br/>picking-dead-letter<br/>packing-dead-letter]:::dlc
    
    DLC --> FaultConsumer[FaultConsumer<br/>Generic Handler]:::fault
    FaultConsumer --> Log[Log Error Details<br/>Manual Review Required]:::fault
```

## Transactional Outbox Pattern (ERPApi)

```mermaid
graph TB
    classDef service fill:#e1d5e7,stroke:#9673a6,stroke-width:2px
    classDef db fill:#dae8fc,stroke:#6c8ebf,stroke-width:2px
    classDef outbox fill:#fff2cc,stroke:#d6b656,stroke-width:2px
    classDef relay fill:#d5e8d4,stroke:#82b366,stroke-width:2px

    subgraph "Single Database Transaction"
        OrderService[OrderService.CreateOrder]:::service
        OrderService --> SaveOrder[Save Order Entity]:::db
        OrderService --> SaveOutbox[Save OutboxMessage]:::db
        SaveOrder --> Commit[Commit Transaction]:::db
        SaveOutbox --> Commit
    end
    
    Commit --> OutboxTable[(Outbox Table<br/>Messages waiting)]:::outbox
    
    OutboxTable --> Gateway[ERPGateway<br/>Polling Consumer]:::relay
    Gateway -->|Poll every 5s| OutboxTable
    Gateway -->|Publish to RabbitMQ| RabbitMQ[Message Broker]:::relay
    Gateway -->|Mark as Sent| OutboxTable
```

## Correlation Identifier Pattern

```mermaid
graph LR
    classDef trace fill:#dae8fc,stroke:#6c8ebf,stroke-width:2px

    Request[HTTP Request<br/>CorrelationId: abc-123]:::trace
    
    Request --> OrderApi[OrderApi<br/>ID: abc-123]:::trace
    OrderApi --> ERPApi[ERPApi<br/>ID: abc-123]:::trace
    ERPApi --> Queue1[Queue<br/>ID: abc-123]:::trace
    Queue1 --> Inventory[InventoryService<br/>ID: abc-123]:::trace
    Inventory --> Queue2[Queue<br/>ID: abc-123]:::trace
    Queue2 --> Picking[PickingService<br/>ID: abc-123]:::trace
    Picking --> Queue3[Queue<br/>ID: abc-123]:::trace
    Queue3 --> Packing[PackingService<br/>ID: abc-123]:::trace
    Packing --> Complete[Complete<br/>ID: abc-123]:::trace
```

## Complete System Overview with Error Handling

```mermaid
graph TB
    classDef endpoint fill:#d5e8d4,stroke:#82b366,stroke-width:2px
    classDef queue fill:#fff2cc,stroke:#d6b656,stroke-width:2px
    classDef priorityQueue fill:#ffe6cc,stroke:#d79b00,stroke-width:3px
    classDef enricher fill:#e1d5e7,stroke:#9673a6,stroke-width:2px
    classDef router fill:#dae8fc,stroke:#6c8ebf,stroke-width:2px
    classDef dlc fill:#f8cecc,stroke:#b85450,stroke-width:2px
    classDef broker fill:#fff2cc,stroke:#d6b656,stroke-width:4px

    Client[External Client]
    OrderApi[OrderApi]:::endpoint
    ERPApi[ERPApi<br/>Content Enricher]:::enricher
    ERPGateway[ERPGateway<br/>Outbox Relay]:::enricher
    RabbitMQ[RabbitMQ Broker]:::broker
    
    %% Inventory Branch
    InventoryQueue[inventory-check]:::queue
    InventoryService[InventoryService<br/>Router]:::router
    InventoryDLC[inventory-DLC]:::dlc
    
    %% Picking Branch
    PickingQueue[picking-stock-reserved<br/>Priority Queue]:::priorityQueue
    PickingService[PickingService<br/>Translator]:::endpoint
    PickingDLC[picking-DLC]:::dlc
    
    %% Packing Branch
    PackingQueue[packing-order-picked]:::queue
    PackingService[PackingService]:::endpoint
    PackingDLC[packing-DLC]:::dlc
    
    Complete[✅ Complete]:::endpoint
    
    %% Main Flow
    Client --> OrderApi
    OrderApi --> ERPApi
    ERPApi --> ERPGateway
    ERPGateway --> RabbitMQ
    
    %% Inventory Flow
    RabbitMQ --> InventoryQueue
    InventoryQueue --> InventoryService
    InventoryService -->|Success| RabbitMQ
    InventoryService -.->|3 Retries Failed| InventoryDLC
    
    %% Picking Flow
    RabbitMQ --> PickingQueue
    PickingQueue --> PickingService
    PickingService -->|Success| RabbitMQ
    PickingService -.->|3 Retries Failed| PickingDLC
    
    %% Packing Flow
    RabbitMQ --> PackingQueue
    PackingQueue --> PackingService
    PackingService -->|Success| Complete
    PackingService -.->|3 Retries Failed| PackingDLC
```

## Priority Queue Visualization

```mermaid
graph TB
    classDef high fill:#ff6b6b,stroke:#c92a2a,stroke-width:3px
    classDef medium fill:#ffa94d,stroke:#fd7e14,stroke-width:2px
    classDef low fill:#51cf66,stroke:#2f9e44,stroke-width:2px

    subgraph "picking-stock-reserved Priority Queue"
        direction TB
        High[Priority 10<br/>🔥 DK Orders<br/>OrderType = DK<br/>Priority = 9]:::high
        Medium[Priority 5<br/>⚡ Standard Express]:::medium
        Low[Priority 1<br/>📦 Regular Orders]:::low
        
        High --> |Processed First| PickingService[PickingService Consumer]
        Medium --> |Processed Second| PickingService
        Low --> |Processed Last| PickingService
    end
```

## Message Flow Sequence

```mermaid
sequenceDiagram
    participant Client
    participant OrderApi
    participant ERPApi
    participant OutboxTable
    participant ERPGateway
    participant RabbitMQ
    participant InventoryService
    participant PickingService
    participant PackingService
    
    Client->>OrderApi: POST /orders (CreateOrderRequest)
    OrderApi->>ERPApi: Forward Order
    
    activate ERPApi
    ERPApi->>ERPApi: Enrich Order (Customer, Items, Address)
    ERPApi->>OutboxTable: Save Order + OutboxMessage (Transaction)
    deactivate ERPApi
    
    ERPApi-->>OrderApi: Order Created (OrderNo: 1001)
    OrderApi-->>Client: 201 Created
    
    loop Poll every 5s
        ERPGateway->>OutboxTable: Query unsent messages
        OutboxTable-->>ERPGateway: OrderCreated event
        ERPGateway->>RabbitMQ: Publish OrderCreated
        ERPGateway->>OutboxTable: Mark as Sent
    end
    
    RabbitMQ->>InventoryService: OrderCreated
    activate InventoryService
    InventoryService->>InventoryService: Check Stock & Route by Priority
    InventoryService->>RabbitMQ: Publish StockReserved
    deactivate InventoryService
    
    RabbitMQ->>PickingService: StockReserved (with Priority)
    activate PickingService
    PickingService->>PickingService: Pick Items
    PickingService->>RabbitMQ: Publish OrderPicked
    deactivate PickingService
    
    RabbitMQ->>PackingService: OrderPicked
    activate PackingService
    PackingService->>PackingService: Pack Order
    PackingService->>PackingService: OrderPacked ✅
    deactivate PackingService
```

## Health Checks Architecture

```mermaid
graph TB
    classDef service fill:#d5e8d4,stroke:#82b366,stroke-width:2px
    classDef check fill:#dae8fc,stroke:#6c8ebf,stroke-width:2px
    classDef healthy fill:#51cf66,stroke:#2f9e44,stroke-width:2px
    classDef unhealthy fill:#ff6b6b,stroke:#c92a2a,stroke-width:2px

    subgraph "InventoryService Health Checks"
        InventoryService[InventoryService]:::service
        InventoryService --> DBCheck1[Database Health]:::check
        InventoryService --> RabbitCheck1[RabbitMQ Health]:::check
        
        DBCheck1 --> |Connected| Healthy1[✅ Healthy]:::healthy
        DBCheck1 --> |Failed| Unhealthy1[❌ Unhealthy]:::unhealthy
        
        RabbitCheck1 --> |Connected| Healthy2[✅ Healthy]:::healthy
        RabbitCheck1 --> |Failed| Unhealthy2[❌ Unhealthy]:::unhealthy
    end
    
    subgraph "PickingService Health Checks"
        PickingService[PickingService]:::service
        PickingService --> RabbitCheck2[RabbitMQ Health]:::check
        
        RabbitCheck2 --> |Connected| Healthy3[✅ Healthy]:::healthy
        RabbitCheck2 --> |Failed| Unhealthy3[❌ Unhealthy]:::unhealthy
    end
    
    subgraph "PackingService Health Checks"
        PackingService[PackingService]:::service
        PackingService --> RabbitCheck3[RabbitMQ Health]:::check
        
        RabbitCheck3 --> |Connected| Healthy4[✅ Healthy]:::healthy
        RabbitCheck3 --> |Failed| Unhealthy4[❌ Unhealthy]:::unhealthy
    end
```

---

## How to Use These Diagrams

### In GitHub/GitLab
Mermaid diagrams render automatically in markdown files on GitHub and GitLab. Just view this file!

### In VS Code
1. Install extension: **Markdown Preview Mermaid Support**
2. Open this file
3. Press `Ctrl+Shift+V` to preview

### Export to Image
1. Copy diagram code
2. Go to: https://mermaid.live/
3. Paste code
4. Click "Export" → PNG/SVG/PDF

### In Documentation Sites
Most documentation generators (Docusaurus, MkDocs, VuePress) support Mermaid natively.

### In Draw.io
1. Go to: https://mermaid.live/
2. Paste diagram code
3. Export as SVG
4. Import SVG into Draw.io

---

## EIP Patterns Legend

| Pattern | Service | Implementation |
|---------|---------|----------------|
| 📨 Content Enricher | ERPApi | Enriches OrderCreated with customer, items, address, priority |
| 🔀 Content-Based Router | InventoryService | Routes by Priority (9) and OrderType (DK) for fast-tracking |
| 🔄 Message Translator | PickingService | Transforms StockReserved → OrderPicked |
| 💾 Transactional Outbox | ERPApi + ERPGateway | Guarantees message delivery with DB transaction |
| ☠️ Dead Letter Channel | All Services | 3 retries with 5s delay, then DLC for manual review |
| 🔢 Priority Queue | picking-stock-reserved | x-max-priority: 10, DK orders get priority 10 |
| 🔗 Correlation Identifier | All Services | End-to-end tracing with CorrelationId |
| 🏢 Message Broker | RabbitMQ | Central messaging hub |
| ➡️ Point-to-Point Channel | All Queues | Direct queue-to-consumer communication |
| 🔄 Polling Consumer | ERPGateway | Polls Outbox table every 5s |
| 📍 Message Endpoint | OrderApi, Services | Message entry/exit points |

---

## Technology Stack

- **.NET 10.0** - Framework
- **MassTransit 8.5.4** - Messaging abstraction
- **RabbitMQ** - Message broker
- **Entity Framework Core** - ORM
- **SQLite** - Database
- **Serilog** - Structured logging
- **ASP.NET Core Health Checks** - Monitoring

---

## Quick Stats

- **Services**: 5 (OrderApi, ERPApi, InventoryService, PickingService, PackingService)
- **Worker Services**: 3 (Inventory, Picking, Packing)
- **Message Queues**: 6 (3 main + 3 DLC)
- **EIP Patterns**: 11 implemented
- **Events**: 4 (OrderCreated, StockReserved, OrderPicked, OrderPacked)
- **Consumers**: 6 (3 main + 3 fault)
- **Retry Attempts**: 3 per message
- **Retry Delay**: 5 seconds
- **Priority Levels**: 1-10 (Priority Queue)
- **Health Checks**: DB + RabbitMQ per service
