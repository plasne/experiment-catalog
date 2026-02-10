using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;
using ModelContextProtocol.Server;

namespace Catalog;

/// <summary>
/// MCP tools for project management operations.
/// </summary>
[McpServerToolType]
public class ProjectsTools(IStorageService storageService)
{
    private void ValidateProjectName(string? project) => McpValidationHelper.ValidateProjectName(project, storageService);

    /// <summary>
    /// Lists all projects.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of all projects.</returns>
    [McpServerTool(Name = "ListProjects"), Description("List all projects.")]
    public async Task<IList<Project>> ListProjects(
        CancellationToken cancellationToken = default)
    {
        return await storageService.GetProjectsAsync(cancellationToken);
    }

    /// <summary>
    /// Adds a new project.
    /// </summary>
    /// <param name="name">The project name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A message indicating the project was added.</returns>
    [McpServerTool(Name = "AddProject"), Description("Add a new project.")]
    public async Task<string> AddProject(
        [Description("The project name")] string name,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectName(name);

        var project = new Project { Name = name };
        await storageService.AddProjectAsync(project, cancellationToken);
        return $"Project '{name}' added.";
    }

    /// <summary>
    /// Lists all tag names in a project.
    /// </summary>
    /// <param name="project">The project name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of tag names.</returns>
    [McpServerTool(Name = "ListTags"), Description("List all tag names in a project.")]
    public async Task<IList<string>> ListTags(
        [Description("The project name")] string project,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectName(project);

        return await storageService.ListTagsAsync(project, cancellationToken);
    }

    /// <summary>
    /// Gets the metric definitions for a project.
    /// </summary>
    /// <param name="project">The project name.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A list of metric definitions.</returns>
    [McpServerTool(Name = "GetMetricDefinitions"), Description("Get the metric definitions for a project.")]
    public async Task<IList<MetricDefinition>> GetMetricDefinitions(
        [Description("The project name")] string project,
        CancellationToken cancellationToken = default)
    {
        ValidateProjectName(project);

        return await storageService.GetMetricsAsync(project, cancellationToken);
    }
}
