# WCMS Import System Technical Specification

## 1. Overview
This specification defines the technical approach for the WCMS content import platform described in the requirements. The platform is designed to accept content imports from legacy WCMS systems, process them asynchronously, store metadata and media assets securely, and publish downstream integration events using RabbitMQ.

## 2. Technology Stack

### 2.1 Application Framework
- .NET Core MVC / Minimal API
- ASP.NET Core for REST endpoints
- Entity Framework Core for relational data access and persistence
- Background workers for asynchronous import processing

### 2.2 Message Broker
- RabbitMQ
- Used for reliable event-driven notification of upstream systems
- Supports asynchronous propagation of `ContentImported` integration events

### 2.3 Data Layer
- PostgreSQL for relational metadata storage (via the Npgsql EF Core provider)
- Entity Framework Core ORM for entity mapping, migrations, and repository/data access
- SeaweedFS for binary media object storage (accessed through its S3-compatible API)
- JSONB / JSON support for flexible metadata payloads

### 2.4 Testing
- xUnit for unit testing
- Integration tests for API and worker workflows
- Optional testcontainers for database and RabbitMQ validation

### 2.5 Observability
- Structured logging
- Correlation IDs for request tracing
- Metrics and tracing integration (OpenTelemetry recommended)

## 3. System Architecture

### 3.1 Components
- Import API endpoint
- Background import worker
- Entity Framework Core data access layer
- Import job persistence layer
- Object storage client
- RabbitMQ publisher / outbox processor
- Upstream subscribers

### 3.2 Execution Flow
1. Client submits import content via HTTP endpoint.
2. API validates the request and stages the uploaded file in object storage.
3. In one transaction, API creates an `import_jobs` record (`queued`) and an `ImportRequested` outbox event.
4. API responds immediately with `202 Accepted` and job identifier.
5. Outbox publisher sends `ImportRequested` to the durable `import.jobs` RabbitMQ queue.
6. Background worker consumes the message, skips it if the job is already `processing` or `completed`, and marks the job `processing`.
7. Worker reads the staged file, persists metadata in the database, and uploads media files to object storage.
8. Worker inserts a `ContentImported` event into the outbox table and acknowledges the `ImportRequested` message only after the job is completed or recorded as failed.
9. Outbox publisher sends `ContentImported` to the `content.imported` RabbitMQ queue.
10. Upstream systems consume and process the message.

## 4. Solution Design

### 4.1 API Layer
The API layer will be implemented using ASP.NET Core Minimal API patterns.

Responsibilities:
- accept upload requests
- validate input payloads
- stage uploaded files in object storage
- create job records and `ImportRequested` outbox events in one transaction
- return job status and tracking references

### 4.2 Background Worker
A background processing service will handle the heavy import workload. It runs as a RabbitMQ consumer of the `import.jobs` queue, so multiple worker instances can compete for jobs and queued jobs survive restarts.

Responsibilities:
- consume `ImportRequested` messages with a low prefetch count (1 or a small number) because each job is heavy
- enforce idempotency, since RabbitMQ delivers at least once
- acknowledge a message only after the job is completed or recorded as failed
- unzip and parse payloads
- validate metadata and media files
- store metadata in relational DB
- save media asset pointers in object storage
- update job status and logging
- produce outbox events for successful imports

### 4.3 Database Responsibilities
The database stores:
- tenant and import metadata
- content item records
- validation errors
- media asset references
- job logs
- outbox events for reliable event delivery

### 4.4 Message Handling
RabbitMQ is used for two purposes, each with its own durable queue:

| Queue | Producer | Consumer | Message |
| --- | --- | --- | --- |
| `import.jobs` | API (via outbox publisher) | Import worker | `ImportRequested` |
| `content.imported` | Import worker (via outbox publisher) | Upstream systems | `ContentImported` |

Operational rules for `import.jobs`:
- Messages carry only identifiers (`ImportJobId`, `TenantId`, staged storage path, `CorrelationId`), never the file itself.
- Configure a retry policy and a dead-letter queue so a failing job does not block others.
- Set the consumer acknowledgement timeout longer than the longest expected import.

The `ContentImported` message model should include:
- `EventId`
- `ContentId`
- `ImportJobId`
- `TenantId`
- `SourceSystem`
- `PublishedAt`
- `PayloadVersion`

## 5. Design Principles
- Decouple HTTP requests from processing-heavy operations
- Use the transactional outbox pattern for reliability
- Ensure each import job is independently trackable
- Separate metadata from binary media storage
- Log all critical processing and validation data
- Avoid single import failures blocking unrelated jobs

## 6. Validation and Error Handling
The system shall:
- validate uploaded file type and structure
- record malformed records in the validation error table
- continue processing other records even when some items fail
- mark the import job as `partial_failed` or `failed` depending on outcome
- emit structured logs with `ClientIP`, `ContentId`, and `CorrelationId`

## 7. Testing Strategy

### 7.1 Unit Testing with xUnit
Use xUnit for:
- request validation logic
- file format parsing
- content transformation logic
- status calculation logic
- outbox payload generation
- retry logic for event publication

### 7.2 Integration Testing
- validate API upload endpoints
- verify background worker behavior
- test database insert/update flows
- simulate RabbitMQ publisher and downstream consumption

### 7.3 Recommended Testing Tools
- xUnit
- FluentAssertions
- Moq for service mocking
- Testcontainers for PostgreSQL / RabbitMQ integration tests

## 8. Example Technology Mapping

| Concern | Selected Technology |
| --- | --- |
| API Layer | .NET Core MVC / Minimal API |
| Data Access / ORM | Entity Framework Core |
| Background Processing | Hosted Service / Background Worker |
| Queue / Messaging | RabbitMQ |
| Relational DB | PostgreSQL |
| Object Storage | SeaweedFS (S3-compatible API) |
| Unit Testing | xUnit |
| Observability | Structured logging + correlation IDs |

## 9. Deployment Considerations
- Run the API and worker as separate services or hosted background services
- Keep the worker decoupled from user-facing HTTP timing
- Use environment-specific configuration for database, storage, and RabbitMQ connection strings
- Ensure durable queues and retry policies are configured for RabbitMQ

## 10. Summary
The solution will use .NET Core MVC / Minimal API for the import API, Entity Framework Core for database persistence, RabbitMQ for reliable event-driven communication, and xUnit for automated testing. The system architecture separates request handling from heavy processing, ensures reliable downstream notifications, and provides traceability across the entire import lifecycle.
