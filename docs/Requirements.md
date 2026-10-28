# WCMS Content Import Requirements

## Problem Statement

One of our Web Content Management Systems (WCMS) needs a way for customers to import content from their current WCMS. After the content is imported, upstream systems must be notified that the new content is available.

## Functional Requirements

1. The system shall accept content imports in multiple formats, including:
   - ZIP/JSON/XML/CSV/…
2. For ZIP files, the system shall extract the archive and store:
   - metadata in a relational database
   - media files in object storage
   - content relationships using a `ContentId`
3. The system shall validate uploaded files and log all validation errors to a database table.
   - Requests must be decoupled so a failed request does not impact other imports.
4. Media files shall be stored in object storage and linked back to the parent content item by `ContentId`.
5. Customers shall be able to view real-time status for import jobs.
6. On successful import, the system shall publish an even to notify subscribed upstream systems.

## Non-Functional Requirements

1. Quick HTTP requests must be decoupled from heavy content import processing through separate background workers.
2. The system must reliably prevent message loss, even if the message broker is temporarily unavailable.
3. Logging must be comprehensive and include key identifiers such as `EventId`, `ContentId`, and `ClientIP` so a request can be traced from upload start to upstream notification completion.

## Scope and Assumptions

### 1. Estimated Requests per Second

- Assuming one user represents one company/domain, a maximum of approximately 10 import requests per second is a reasonable target.

### 2. Response Time

- The client should receive a quick `202 Accepted` response while the background worker handles the heavy media processing.
- A delay of approximately 30 seconds in upstream notification is acceptable.

### 3. Pub/Sub Pattern

- Assuming the upstream/downstream services/system implemented Message Broker such as RabbitMQ.
- Subscribe to Message queue where import jobs completed, and carry on with whatever handling the service supposed to do.

### 4. Database and Storage Scale

- Assumption: the maximum upload size is 100 MB per ZIP request.
- 10% of the payload is metadata (JSON/XML/HTML/schemas).
- 90% of the payload is binary media files (images, videos, audio, documents).

```text
100 MB Compressed ZIP Payload
┌───────────────────────────────┬───────────────────────────────┐
│        10 MB Metadata        │       90 MB Media Assets      │
│ (JSON, XML, HTML, Schemas)   │ (Images, Documents, PDFs)    │
└──────────────┬────────────────┴──────────────┬──────────────┘
               │                                      │
               v                                      v
       Database (Postgres)                  Object Storage (SeaweedFS)
    [~1,500 - 3,000 Rich Records]         [~90 - 250 Binary Files]
```

### WCMS Import Pipeline: Scale Projection Matrix

| Number of Imports | Total Payload (ZIPs) | Metadata Volume (10%) | Media Volume (90%) | Est. Content Records | Est. Object Storage (SeaweedFS) | Est. Database Size (Postgres w/ Indexes) |
| --- | --- | --- | --- | --- | --- | --- |
| 1 Import | 100 MB | 10 MB | 90 MB | \~2,000 rows | 90 MB | \~25 MB |
| 10 Imports | 1 GB | 100 MB | 900 MB | \~20,000 rows | 900 MB | \~250 MB |
| 100 Imports | 10 GB | 1 GB | 9 GB | \~200,000 rows | 9 GB | \~2.5 GB |
| 1,000 Imports | 100 GB | 10 GB | 90 GB | \~2,000,000 rows | 90 GB | \~25 GB – 30 GB |
| 10,000 Imports | 1 TB | 100 GB | 900 GB | \~20,000,000 rows | 900 GB | \~250 GB – 300 GB |

### 5. Storage Growth Forecast (Month and Year)

Based on the projection above, the storage footprint can be estimated as:

- 1 import = approximately 100 MB total payload
- 90 MB goes to object storage
- 25 MB goes to relational database metadata
- Total estimated storage per import = 115 MB (approximate, including database overhead)

Monthly projection formula:

- Monthly payload growth = `imports_per_month × 100 MB`
- Monthly object storage growth = `imports_per_month × 90 MB`
- Monthly database growth = `imports_per_month × 25 MB`
- Annual growth = Monthly growth × 12

#### Example A: 1,000 imports per month

- Total payload: 100 GB/month
- Object storage: 90 GB/month
- Database: 25 GB/month
- Annual total payload: 1.2 TB/year
- Annual object storage: 1.08 TB/year
- Annual database: 300 GB/year

#### Example B: 10,000 imports per month

- Total payload: 1 TB/month
- Object storage: 900 GB/month
- Database: 250 GB/month
- Annual total payload: 12 TB/year
- Annual object storage: 10.8 TB/year
- Annual database: 3 TB/year

#### Example C: 100,000 imports per month

- Total payload: 10 TB/month
- Object storage: 9 TB/month
- Database: 2.5 TB/month
- Annual total payload: 120 TB/year
- Annual object storage: 108 TB/year
- Annual database: 30 TB/year
