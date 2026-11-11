# ADR-001: Modular Monolith Over Microservices

## Status
Accepted

## Context
OptiFlow requires separate HTTP API ingestion and asynchronous background LLM processing without the operational overhead of managing many distributed services, brokers, and schemas early in delivery.

## Decision
Structure OptiFlow as a Clean Architecture modular monolith with two runtime processes (`OptiFlow.Api` and `OptiFlow.Worker`) plus isolated assemblies (`Domain`, `Application`, `Infrastructure`, `Opal`). Enforce boundaries with `OptiFlow.ArchitectureTests`.

## Consequences
- Single transactional database and unified deployment model with zero distributed-transaction complexity.
- Clear `IContentSource` and `ILanguageModelClient` boundaries allow extracting hot components (e.g., diffing or AI gateway) into separate services later if benchmarks justify it.
