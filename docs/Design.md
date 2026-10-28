## Architectural Diagram

```mermaid
graph TD
    subgraph Client ["Client Layer"]
        Customer["Customer / Legacy WCMS"]
    end

    subgraph ApiService ["Import API Service (.NET Minimal API)"]
        API["Import API Endpoint"]
    end

    subgraph WorkerService ["Background Import Worker Service"]
        Worker["Import Job Consumer"]
    end

    subgraph OutboxService ["Outbox Publisher Service"]
        OutboxProcessor["Outbox Publisher"]
    end

    subgraph DataLayer ["Data & Storage Layer"]
        DB[("PostgreSQL")]
        Storage[("Object Storage: SeaweedFS")]
    end

    subgraph MessagingLayer ["Messaging & Upstream Layer"]
        RabbitMQ{{"RabbitMQ Message Broker"}}
        UpstreamA["Upstream System A"]
        UpstreamB["Upstream System B"]
    end

    %% Flow connections
    Customer -->|"1. Submit Import Job"| API
    API -->|"2. Stage ZIP"| Storage
    API -->|"3. Create Job Record & ImportRequested Outbox Entry"| DB
    OutboxProcessor -->|"4. Read Pending Events"| DB
    OutboxProcessor -->|"5. Publish ImportRequested to import.jobs"| RabbitMQ
    RabbitMQ -->|"6. Deliver Job"| Worker
    Worker -->|"7. Read Staged ZIP & Store Media Files"| Storage
    Worker -->|"8. Save Content & ContentImported Outbox Entry"| DB
    OutboxProcessor -->|"9. Publish ContentImported to content.imported"| RabbitMQ

    RabbitMQ -->|"10. Consume Event"| UpstreamA
    RabbitMQ -->|"10. Consume Event"| UpstreamB
```

## Data Flow

1. A client submits an import request to the API. The API stages the uploaded ZIP file in object storage and records the import job in PostgreSQL.
2. The API records an `ImportRequested` outbox entry alongside the job record, so the job is durably recorded before asynchronous processing begins. The API can then return an accepted response (202) with the job identifier.
3. The separately hosted worker receives the job through RabbitMQ, reads the staged ZIP from object storage, validates and processes its content, stores media files in object storage, and saves content metadata and media references in PostgreSQL.
4. On successful processing, the worker records a `ContentImported` outbox entry in PostgreSQL. The binary files remain in object storage; messages carry event and job details rather than the files themselves.

The API handles request validation, staging, and job creation; it does not perform the heavy import processing. The worker runs as a separate service so it can process jobs independently of API request handling and be scaled separately.

# 