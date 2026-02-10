# MCP Services

This document describes the MCP capabilities for experiment comparison and analysis, and how the integration was implemented.

## Tests

The following queries have been tested:

- how many projects do I have?
- what projects do I have?
- list the projects I have
- what experiments do I have in amltest?
- list experiments under sprint-02
- what permutations of the test_aml_run experiment exist?
- how good was the 20250805220419 permuation?
- how did the 20250807071317 permutation compare to the baseline?
- what were the top 5 ground truths that saw improvement in the recall?
- create me a new project called "sprint-02"
- create me an experiment under sprint-02
- set the experiment known as "baseline" as the baseline for this project
- what tags are used in this project?
- what metrics are defined?
- what 3 tags would have the greatest impact on my recall metric?

## Implementation Guide

Follow these steps to add MCP (Model Context Protocol) support to an existing ASP.NET Core web API. This pattern exposes the same business logic through both REST endpoints and MCP tool calls.

### 1. Add the NuGet package

Add the `ModelContextProtocol.AspNetCore` package to the project file:

```xml
<PackageReference Include="ModelContextProtocol.AspNetCore" Version="0.7.0-preview.1" />
```

### 2. Extract business logic into service classes

Move logic out of controllers into dedicated service classes so both controllers and MCP tools can share the same code. Two services were created in the `services/` folder:

| Service             | Purpose                                                          |
| ------------------- | ---------------------------------------------------------------- |
| `AnalysisService`   | Tag impact analysis (meaningful tags)                            |
| `ExperimentService` | Comparison, per-ref comparison, named set retrieval, set listing |

Controllers become thin wrappers that delegate to these services. MCP tool classes do the same.

### 3. Create MCP tool classes

Create classes in a `mcp/` folder. Each class groups related tools and follows this pattern:

- Annotate the class with `[McpServerToolType]`
- Use constructor injection to receive the shared services
- Annotate each public method with `[McpServerTool(Name = "...")]` and `[Description("...")]`
- Annotate each parameter with `[Description("...")]`
- Return domain objects directly (the MCP SDK serializes them for you)

Three tool classes were created:

| Class              | Tools                                                                                                                                                                 |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `ProjectsTools`    | ListProjects, AddProject, ListTags, GetMetricDefinitions                                                                                                              |
| `ExperimentsTools` | ListExperiments, GetExperiment, AddExperiment, ListSetsForExperiment, SetExperimentAsBaseline, SetBaselineForExperiment, CompareExperiment, CompareByRef, GetNamedSet |
| `AnalysisTools`    | CalculateStatistics, MeaningfulTags                                                                                                                                   |

### 4. Create an exception filter for MCP

MCP tool calls do not pass through ASP.NET middleware, so `HttpExceptionMiddleware` does not catch exceptions thrown during tool execution. Create an `McpToolExceptionFilter` that mirrors the same error-handling behavior:

- Catch `HttpWithResponseException`, `HttpException`, and generic `Exception`
- Return a `CallToolResult` with `IsError = true` and a text message
- Register the filter via `.AddCallToolFilter(McpToolExceptionFilter.Create())`

### 5. Register services and MCP in Program.cs

Add the following registrations:

```csharp
// register the shared services
builder.Services.AddSingleton<AnalysisService>();
builder.Services.AddSingleton<ExperimentService>();

// register the MCP server
builder.Services
    .AddMcpServer()
    .WithHttpTransport()
    .WithToolsFromAssembly()
    .AddCallToolFilter(McpToolExceptionFilter.Create());
```

`WithToolsFromAssembly()` discovers all classes annotated with `[McpServerToolType]` automatically.

### 6. Map the MCP endpoint

After `app.MapControllers()`, add:

```csharp
app.MapMcp("/mcp");
```

This exposes the MCP Streamable HTTP endpoint at `/mcp`.

### 7. Update CORS for MCP Inspector

If you use the MCP Inspector for testing, add its origin to the CORS policy:

```csharp
corsBuilder.WithOrigins(
    "http://localhost:6020",
    "http://localhost:6274"  // MCP Inspector
)
```

### 8. Handle enum serialization for MCP

The MCP SDK uses `System.Text.Json` rather than `Newtonsoft.Json`. If any tool parameters use enums, add the `System.Text.Json` converter attribute alongside any existing Newtonsoft attributes:

```csharp
[System.Text.Json.Serialization.JsonConverter(typeof(JsonStringEnumConverter))]
public enum MeaningfulTagsComparisonMode { Baseline, Zero, Average }
```

### 9. Authentication

This branch did not change the authentication configuration, but it is worth noting how it applies to MCP.

The project uses a `FallbackPolicy` configured in `AuthorizationConfigurator`. When `IsAuthenticationEnabled` is true, the fallback policy calls `RequireAuthenticatedUser()` (and optionally `RequireRole()`). Because a fallback policy applies to **all endpoints that do not have an explicit authorization attribute**, the `/mcp` endpoint inherits the same JWT Bearer authentication requirement as the REST controllers. No `[Authorize]` or `[AllowAnonymous]` attributes exist on the MCP tool classes — the fallback policy covers them automatically.

In practice this means:

- **Auth enabled**: MCP clients must send a valid JWT Bearer token in the `Authorization` header, same as any REST call. The MCP Inspector or any programmatic client needs the token.
- **Auth disabled**: The fallback policy is not set, so the MCP endpoint is open — useful during local development.
- **No per-tool granularity**: Currently all MCP tools share the same authentication and authorization policy. If you need different access levels per tool, you would need to add custom authorization checks inside the tool methods or use a filter.

### 10. Add Copilot agent and skill files (optional)

To enable GitHub Copilot Chat to use the MCP tools via an agent:

- Create `.github/agents/ask-catalog.agent.md` with the agent definition, tool references, and tool selection guidance
- Create `.github/skills/experiment-catalog/SKILL.md` with domain context (hierarchy, terminology, workflows) so the agent can reason about the data model

### Summary of files changed or created

| File                                         | Action   | Purpose                                         |
| -------------------------------------------- | -------- | ----------------------------------------------- |
| `exp-catalog.csproj`                         | Modified | Added `ModelContextProtocol.AspNetCore` package |
| `Program.cs`                                 | Modified | Registered services, MCP server, endpoint, CORS |
| `services/AnalysisService.cs`                | Created  | Extracted meaningful-tags logic from controller |
| `services/ExperimentService.cs`              | Created  | Extracted compare/set logic from controller     |
| `controllers/AnalysisController.cs`          | Modified | Thinned to delegate to `AnalysisService`        |
| `controllers/ExperimentsController.cs`       | Modified | Thinned to delegate to `ExperimentService`      |
| `controllers/HttpException.cs`               | Moved    | Relocated from project root into `controllers/` |
| `mcp/ProjectsTools.cs`                       | Created  | MCP tools for project operations                |
| `mcp/ExperimentsTools.cs`                    | Created  | MCP tools for experiment operations             |
| `mcp/AnalysisTools.cs`                       | Created  | MCP tools for analysis operations               |
| `mcp/McpToolExceptionFilter.cs`              | Created  | Exception handling filter for MCP tool calls    |
| `models/MeaningfulTagsRequest.cs`            | Modified | Added `System.Text.Json` enum converter         |
| `.github/agents/ask-catalog.agent.md`        | Created  | Copilot agent definition                        |
| `.github/skills/experiment-catalog/SKILL.md` | Created  | Domain skill context for the agent              |
