Stateless.Workflow
==================

Stateless.Workflow is a basic .NET workflow engine based on the awesome [stateless](https://code.google.com/p/stateless/) State Machine. It works in the same fashion as Stateless, but provides a wrapper for moving between states with the extra features expected of a workflow engine, such a retry attempts, exception handling, delays beteen workflow steps, etc.

# Persistence

The engine supports multiple data stores: 

 1. MemoryStore - this is an in-memory store: not particularly useful for most scenarios but it's good for testing, and could theoretically be used in an application wanting to take advantage of non-critical, simple workflowed steps in a background process.
 2. MongoDbStore - a store using a MongoDb database.
 3. RavenDb - a store using a RavenDb database.
 
All stores will use two collections: Workflows and CompletedWorkflows.  When a workflow is registered via the server or client, it is persisted in the Workflows collection.  The workflow will then move through various states until completion, in which case it will be moved to the CompletedWorkflows collection.  This store is never read by the workflow engine, it is purely for archiving purposes.

# Workflow Configuration

Workflows are configured in similar fashion to Stateless.  You should make an effort to understand how Stateless works before attempting to use this workflow engine.  All configuration should go into the Initialise override on the Workflow itself.  Extra properties can be added to your custom workflows, and these will be serialized with the workflow when it is persisted to the store. The Stateless concepts of State and Triggers remain the same, and are used by the workflow engine to move between actions in the workflow.

# Example  

There is a full example application in the source code, in the Test.Stateless.WorkflowEngine.Example folder.  This contains a windows service that creates a sample workflow when it runs, which writes 10 files to the "C:\Temp" folder, then deletes all the files.  You can look at this example to see how to set up a windows service that runs the workflow engine.  The WorkerThreadFunc method in the WorkflowEngineExampleService class contains an instantiation of each store type (Memory, MongoDb and RavenDb), if you'd like to run the example just configure what you want.  You can set up your connection properties in the Bootstrapper.

## Looking at the code

TO DO - explaining the code of the workflow will be useful.
