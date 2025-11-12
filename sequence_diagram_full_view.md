sequenceDiagram
    autonumber
    
    actor Client
    participant OrderApi
    participant RabbitMQ as RabbitMQ<br/>(create-order-request)
    participant ERPApi
    participant ERPDb as ERP Database<br/>(SQLite + Outbox)
    participant OutboxWorker as MassTransit<br/>Outbox Publisher
    participant OrderCreatedTopic as RabbitMQ Topic<br/>(OrderCreated)
    participant InventoryService
    participant StockReservedTopic as RabbitMQ Topic<br/>(StockReserved)
    participant PickingService
    participant OrderPickedTopic as RabbitMQ Topic<br/>(OrderPicked)
    participant PackingService
    participant OrderPackedTopic as RabbitMQ Topic<br/>(OrderPacked)
    
    Note over Client,OrderPackedTopic: Phase 1: Order Creation (Request/Response Pattern)
    
    Client->>+OrderApi: POST /api/order
    OrderApi->>OrderApi: Generate CorrelationId = OrderId
    OrderApi->>+RabbitMQ: SendRequest(CreateOrderRequest)
    
    RabbitMQ->>+ERPApi: CreateOrderRequest
    ERPApi->>ERPApi: Validate Order (FluentValidation)
    
    ERPApi->>+ERPDb: BEGIN TRANSACTION
    ERPDb-->>-ERPApi: Transaction Started
    
    ERPApi->>ERPDb: INSERT Order + Items
    ERPDb-->>ERPApi: OrderNo Generated
    
    Note over ERPApi,ERPDb: Content Enricher Pattern:<br/>Add Items, Customer, Shipping Address
    
    ERPApi->>ERPDb: INSERT to Outbox Table<br/>(OrderCreated event + enriched data)
    ERPDb-->>ERPApi: Event Stored in Outbox
    
    ERPApi->>ERPDb: COMMIT TRANSACTION
    ERPDb-->>ERPApi: Committed
    
    ERPApi-->>RabbitMQ: CreateOrderResponse<br/>(OrderNo, Success=true)
    RabbitMQ-->>-OrderApi: Response Received
    OrderApi-->>-Client: 200 OK (OrderNo)
    
    Note over Client,OrderPackedTopic: Phase 2: Outbox Publishing & Inventory Check
    
    OutboxWorker->>ERPDb: Poll Outbox Table
    ERPDb-->>OutboxWorker: Unpublished Messages
    
    OutboxWorker->>+OrderCreatedTopic: Publish(OrderCreated)<br/>with enriched data
    OutboxWorker->>ERPDb: Mark as Published
    
    OrderCreatedTopic->>+InventoryService: OrderCreated Event
        
    InventoryService->>InventoryService: Check Stock Availability<br/>(80% success rate simulation)
    
    alt All Items Available
        InventoryService->>+StockReservedTopic: Publish(StockReserved)<br/>IsPartial=false, Items=All
        InventoryService->>InventoryService: Log: "All items available"
        
        Note over InventoryService,ERPApi: Phase 3: State Updates (Event-Driven)
        
        StockReservedTopic->>ERPApi: StockReserved Event
        ERPApi->>ERPDb: UPDATE Order<br/>State="StockReserved"<br/>All Items Status="Available"
        ERPApi->>ERPApi: Log with CorrelationId
        
        Note over StockReservedTopic,PickingService: Phase 4: Picking Process
        
        StockReservedTopic->>+PickingService: StockReserved Event<br/>(Priority Queue x-max-priority=10)
        
        Note over PickingService: Processing Time:<br/>Random 2000-5000ms
        
        PickingService->>PickingService: Simulate Picking Process<br/>Task.Delay(2000-5000ms)
        
        PickingService->>+OrderPickedTopic: Publish(OrderPicked)<br/>Set Priority Header (9 or 1)
        PickingService->>PickingService: Log: "Picking completed"<br/>with CorrelationId
        
        OrderPickedTopic->>ERPApi: OrderPicked Event
        ERPApi->>ERPDb: UPDATE Order<br/>State="Picked"<br/>Items Status="Picked"
        ERPApi->>ERPApi: Log with CorrelationId
        
        Note over OrderPickedTopic,PackingService: Phase 5: Packing Process
        
        OrderPickedTopic->>+PackingService: OrderPicked Event<br/>(PrefetchCount=16)
        
        Note over PackingService: Processing Time:<br/>Random 1500-3000ms
        
        PackingService->>PackingService: Simulate Packing Process<br/>Task.Delay(1500-3000ms)
        PackingService->>PackingService: Calculate Box Size & Weight
        
        PackingService->>+OrderPackedTopic: Publish(OrderPacked)<br/>BoxSize, Weight, Items
        PackingService->>PackingService: Log: "Packing completed"<br/>with CorrelationId
        
        OrderPackedTopic->>ERPApi: OrderPacked Event
        ERPApi->>ERPDb: UPDATE Order<br/>State="Packed" (FINAL)<br/>Items Status="Packed"
        ERPApi->>ERPApi: Log: "Order workflow complete"
        
        Note over Client,OrderPackedTopic: ✅ Order Complete: Created → StockReserved → Picked → Packed
    end