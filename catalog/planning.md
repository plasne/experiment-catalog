RESEARCH
Research my existing code base.

Pay particular attention to how configuration management is done: config/Config.cs, config/IConfig.cs.

Pay particular attention to how storage services are used: services/IStorageService.cs, services/AzureBlobStorageService.cs.

This project uses C# and dotnet 10, so research anything you need to know about that.

The new work item will be related to implementing Cosmos DB in this C# project. Find out the proper ways to implement this using the SDK.

RESEARCHI want to add to my existing research. If we added support for a IStorageService that worked with a database that could filter records (for example, Cosmos), there should be some ways to streamline some of the loading. For example, a comparison only needs 1 set from the baseline, so that could be filtered during load. Something like this would not apply to Azure Blob Storage (the existing implementation). Please review the existing code and see what the proper way to introduce these optimized methods might be. Maybe they are optional parameters in the existing functions or maybe there are parallel optimized load methods that can be introduced.

PLANNING
Using the research, I now want to plan the implementation of a Cosmos storage service.1. Create a new Cosmos storage service (AzureCosmosStorageService.cs) compatible with IStorageService.cs.2. The Cosmos implementation should use all dotnet best practices and should support a connection string or DefaultAzureCredential (in a similar way to how it is done in AzureBlobStorageService).3. Ensure everything is implemented and the solution can build properly.4. Implement some new methods for IStorageServices.cs that can improve performance by only loading data we need.5. Change the use of IStorageServices.cs so these new methods are used where appropriate.6. Write a changes.md file that details where these new methods are used so I can review them.7. Implement the AzureBlobStorageService.cs version of these new methods such that they load the whole file and trim the results (more or less as they did previously). 8. Implement the new Cosmos version of these new methods using appropriate optimizations.9. Review the code to ensure that the functionality and performance of the existing AzureBlobStorageService is the same as it was before these changes.10. Review the code of AzureCosmosStorageService to ensure it is implemented properly and confirm it builds.11. Write a cosmos.md file that explains how to setup the new Cosmos instance.Ask me for anything that needs clarification. Please suggest to me any ideas that you have.The output of the planning step should be a clear list of all the steps to implement prefixed by `[ ]` so that they can be marked complete as they are done.PLANNINGI have the comprehensive research document. Before creating the implementation plan, I have a few clarifying questions and suggestions:
Clarifying Questions

1. Storage Backend Selection: How should the application choose between Blob Storage and Cosmos DB at runtime?
   - Option A: Environment variable (e.g., STORAGE_PROVIDER=cosmos|blob)
   - Option B: Auto-detect based on which config values are present (Cosmos takes priority if both configured)
   - Option C: Both registered, controller injection chooses based on operation type
2. Cosmos DB Container Strategy: The research proposes 3 containers (projects, experiments, results). Do you want:
   - Option A: All 3 containers (normalized, most flexible for queries)
   - Option B: 2 containers (projects, experiments with embedded results) - simpler but less scalable
   - Confirm the partition key strategy looks correct?
3. Data Migration: Should the plan include a migration utility to move existing Blob Storage data to Cosmos DB, or is this out of scope?
4. Maintenance Service: The project has AzureBlobStorageMaintenanceService.cs. Should we create AzureCosmosStorageMaintenanceService.cs as well, or defer that?
   Suggestions
5. Interface Extension Approach: The research recommends a hybrid approach with IFilterableStorageService. I suggest using capability checking in controllers (if (storageService is IFilterableStorageService filterable)) rather than adding optional parameters to the base interface. This keeps the existing interface clean and makes optimizations explicit.
6. Cosmos Document Models: Rather than creating separate CosmosProject.cs, CosmosExperiment.cs files, I suggest adding [JsonProperty("id")] attributes to existing models and using a CosmosDocument<T> wrapper for partition key handling. This reduces model duplication.
7. Testing Strategy: Should I include a phase for creating unit tests for the new service, or will you handle testing separately?
8. Configuration Validation: Add validation in Config.cs that ensures either Blob Storage OR Cosmos DB config is provided (not both, not neither) based on the selected provider.
9. Indexing Policy: Include Cosmos DB indexing policy recommendations in cosmos.md for the filterable properties (Set, Ref, BaselineExperiment, etc.).
   Please let me know your preferences on these items, and I'll create the detailed implementation plan with checkboxes.
   Clarifying Questions:1. See #4 in suggestions for details.2. 3 containers3. Out-of-scope4. I don’t think there is any maintenance needed for Cosmos, but if you think there is, tell me what and then if I agree, yes, we should have a maintenance service.
   Suggestions:
10. The research is just research that was done - you don't have to follow those recommendations. My advice on this is implement interface methods that do whatever high-level function you want to do, like loading the baseline set. But the implementations vary. For example, AzureBlob would load the whole experiment and then trim to the appropriate set (either last or specific baseline). AzureCosmos would do a read to see which set needs to be loaded (wherever that metadata is stored) and then loads just the appropriate set.
11. Yes, that seems fine. Just keep in mind that older files in AzureBlob won't have "id" so it has to be optional.
12. No unit tests.
13. You will need to modify the configuration such that the settings for Blob and Cosmos are all optional. Then in the Validate() method you would need to ensure that the right properties are set for only one of the options. If both are set, fail. If niether is set, fail. You will probably need a factory for create/get IStorageService because you will need to determine which to return based on the configuration and you cannot do that before you add them to the IServiceCollection since there is async startup.
14. yes

we are in agreement, remove FindStatisticsAsync, GetExperimentWithSetsAsync should load statistics
IMPLEMENTATIONimplement this plan please——— This approach in the ExperimentsController.cs cannot work…```
var experiment = await storageService.GetExperimentAsync(projectName, experimentName, false, cancellationToken);
var experimentBaselineSet = experiment.BaselineSet ?? experiment.FirstSet;

````
FirstSet is based on scanning the results to find the first set. If the results aren’t loaded, then you cannot get the first. Isn’t GetProjectBaselineWithBaselineSetAsync() built exactly for this purpose. Why not use that?——— In AzureCosmosStorageService.cs does it really make sense to have separate models for Cosmos. Couldn’t we inherit from the models that exist at least? Thoughts? Go with best practice, but this seems redundant and hard to maintain.——— In AzureCosmosStorageService.cs, the models don’t follow the existing models which use snake_case. They should be consistent.——— In AzureCosmosStorageService.cs, I would remove UpdateExperimentModifiedAsync(), the modified date really isn’t used for anything important and if you look at AzureBlobStorageService.cs, you can see it is always computed from the results anyway.
——— AzureCosmosStorageService.cs uses LoadResultsAsync() and LoadStatisticsAsync(), but as far as I know, both things are always loaded at the same time. I wouldn’t distinguish. That’s also why there are in the same container. You can reference AzureBlobStorageService.cs to make sure I am correct.———

Can we remove OptimizeExperimentAsync() from IStorageService.cs and remove the {experimentName}/optimize endpoint? The optimization path in AzureBlobStorageMaintenanceService.cs should just use AzureBlobStorageService.cs outside of the interface.——— As a coding practice, I don’t use _ in front of private fields. An example is `private IStorageService? _storageService;` in CalculateStatisticsService.cs.———
Do something like `IsAuthenticationEnabled` in Config.cs for whether Cosmos or Blob is used instead of checking the variables in Validate() and in StorageServiceFactory.cs.——— If any of these changes are standards for coding, please write them to a standards.md file so I can include them later.——— Update the plan document (2026-01-30-cosmos-storage-service-plan.instructions.md) with these changes in case the plan is implemented again.




———

This is used in ExperimentsController.cs…```
var experiment = await storageService.GetExperimentAsync(projectName, experimentName, cancellationToken: cancellationToken);
var experimentBaselineSet = experiment.BaselineSet ?? experiment.FirstSet;
````

…but couldn’t there be an implementation very similar to GetProjectBaselineWithBaselineSetAsync() for this?
