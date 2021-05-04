# AsynchronJobServer
AsynchronJobServer is a request driven Server / Service for processing of custom operations with the intention to be quickly integrated in existing environments and easily expanded to meet custom requirements.

## Request Queue
The Request Queue is the technology independent storage for requests. Each Request is enqueued until it is accepted by a RequestHandler and processed as a Job. Since the AsynchronJobServer is intended as a Backend Service and the Request Queue is not tied to a specific technology, the frontend for creation and enqueuing requests needs to be implemented independently.

## RequestHandler
RequestHandler are the component, that manage and perform the execution of the request as Jobs. RequestHandler are evaluated in the order they are registered with a first-match policy.
What kind of requests the `RequestHandler` supports depends on the registered `plugins`

The following RequestHandler are implemented:

### InternalAsyncRequestHandler
This RequestHandler handles supported requests as internal `async` tasks

### DummyAsyncRequestHandler
The `DummyAsyncRequestHandler` simply rejects requests gracefully


## Plugins
Designed as a plugin-architecture, all business logic parts are implemented as plugins. The following two types of plugins are supported.
### Connectors
Connector Plugins handle the interaction with the request queue. Connectors are responsible to fetch requests from the queue and write responses back to the storage. 

### Jobs
Jobs perform the actual task to fullfil a request. Each job has a specified type, that needs to match the request type. Job plugins need to be registered with a `RequestHandler`. 
