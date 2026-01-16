# Contributing to Experiment Catalog

Welcome, and thank you for your interest in contributing to the Experiment Catalog!

There are several ways in which you can contribute, beyond writing code. The goal of this document is to provide a high-level overview of how you can get involved.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Asking Questions](#asking-questions)
- [Reporting Issues](#reporting-issues)
- [Contributing Code](#contributing-code)
- [Development Setup](#development-setup)
- [Project Structure](#project-structure)
- [Coding Guidelines](#coding-guidelines)
- [Pull Request Guidelines](#pull-request-guidelines)
- [Thank You](#thank-you)

## Code of Conduct

This project has adopted the [Microsoft Open Source Code of Conduct](https://opensource.microsoft.com/codeofconduct/). For more information, see the [Code of Conduct FAQ](https://opensource.microsoft.com/codeofconduct/faq/) or contact [opencode@microsoft.com](mailto:opencode@microsoft.com) with any additional questions or comments.

## Asking Questions

Have a question? Please open a GitHub issue with the `question` label rather than opening a support ticket. Your well-worded question will serve as a resource to others searching for help.

## Reporting Issues

Have you identified a reproducible problem? Do you have a feature request? We want to hear about it! Here's how you can report your issue as effectively as possible.

### Look For an Existing Issue

Before you create a new issue, please search the [open issues](../../issues) to see if the issue or feature request has already been filed.

If you find your issue already exists, make relevant comments and add your [reaction](https://github.blog/2016-03-10-add-reactions-to-pull-requests-issues-and-comments/). Use a reaction in place of a "+1" comment:

- 👍 - upvote
- 👎 - downvote

### Writing Good Bug Reports

File a single issue per problem and feature request. Do not enumerate multiple bugs or feature requests in the same issue.

Please include the following with each issue:

- A clear and descriptive title
- Steps to reproduce the issue (1... 2... 3...)
- What you expected to see versus what you actually saw
- Version of .NET, Node.js, Python (as applicable)
- Your operating system
- Relevant environment variables (redact sensitive values)
- Any error messages or logs

## Contributing Code

If you are interested in writing code to fix issues or add features, please read this section carefully.

### Prerequisites

Before you begin, ensure you have the following installed:

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0) - for the backend APIs (catalog and evaluator)
- [Node.js 20+](https://nodejs.org/) - for the frontend UI
- [Python 3.10+](https://www.python.org/) - for the evaluation script
- [Docker](https://www.docker.com/) - for containerized deployment (optional)
- An Azure Storage Account for local development
- An Azure OpenAI deployment (for the evaluation service)

### Development Setup

#### Backend API

1. Navigate to the API directory:

   ```bash
   cd api
   ```

2. Create a `.env` file with required configuration (see [api/README.md](./api/README.md) for all options):

   ```env
   INCLUDE_CREDENTIAL_TYPES=azcli
   AZURE_STORAGE_ACCOUNT_NAME=<your-storage-account>
   ```

3. Run the API:

   ```bash
   dotnet run
   ```

   The API will be available at `http://localhost:6010` with Swagger documentation at `/swagger`.

#### Frontend UI

1. Navigate to the UI directory:

   ```bash
   cd ui
   ```

2. Install dependencies:

   ```bash
   npm install
   ```

3. Start the development server:

   ```bash
   npm run dev
   ```

   The UI will be available at `http://localhost:6020`.

#### Evaluator Service

1. Navigate to the evaluator directory:

   ```bash
   cd evaluator
   ```

2. Create a `.env` file with required configuration (see [evaluator/README.md](./evaluator/README.md) for all options).

3. Run the evaluator:

   ```bash
   dotnet run
   ```

   The evaluator service will be available at `http://localhost:6030`.

#### Evaluation Script

1. Navigate to the evaluation directory:

   ```bash
   cd evaluation
   ```

2. Install dependencies:

   ```bash
   pip install -r requirements.txt
   ```

3. Create a `.env` file with required configuration (see [evaluation/README.md](./evaluation/README.md) for all options):

   ```env
   AZURE_STORAGE_CONNECTION_STRING=<your-connection-string>
   QUEUE_NAME=<your-queue-name>
   AZURE_OPENAI_API_KEY=<your-api-key>
   AZURE_OPENAI_ENDPOINT=<your-endpoint>
   AZURE_OPENAI_DEPLOYMENT=<your-deployment>
   CATALOG_API_ENDPOINT=http://localhost:6010
   ```

4. Run the evaluation script:

   ```bash
   python eval.py
   ```

   This will start listening to the configured queue for evaluation jobs.

## Project Structure

The repository is organized as follows:

```text
experiment-catalog/
├── catalog/                    # C# .NET 8 backend API for experiment storage
│   ├── config/             # Configuration classes
│   ├── controllers/        # API endpoint controllers
│   ├── models/             # Data models and DTOs
│   ├── policies/           # Business logic policies
│   ├── services/           # Core services
│   └── wwwroot/            # Static files (built UI)
├── evaluator/                  # C# .NET 8 service for orchestrating evaluations
│   ├── config/             # Configuration classes
│   ├── controllers/        # API endpoint controllers
│   ├── exceptions/         # Custom exception classes
│   ├── models/             # Data models and DTOs
│   ├── samples/            # Sample ground truth files
│   ├── services/           # Core services
│   └── templates/          # Template files
├── evaluation/                 # Python evaluation script
│   ├── eval.py             # Main evaluation script
│   ├── requirements.txt    # Python dependencies
│   └── *.txt               # Evaluation prompt templates
├── ui/                         # Svelte frontend application
│   ├── public/             # Static assets
│   └── src/
│       └── lib/            # Svelte components and TypeScript models
├── catalog.Dockerfile          # Docker build configuration
└── experimentation-process.md  # Documentation on the experimentation workflow
```

### Catalog (`/catalog`)

The backend is a C# .NET 8 Web API that stores experiment data in Azure Blob Storage.

| Folder         | Description                                                                                         |
| -------------- | --------------------------------------------------------------------------------------------------- |
| `config/`      | Configuration interfaces (`IConfig.cs`) and implementations (`Config.cs`) for environment variables |
| `controllers/` | REST API controllers handling HTTP requests                                                         |
| `models/`      | Data models including `Experiment`, `Project`, `Result`, `Metric`, etc.                             |
| `policies/`    | Business logic implementations (e.g., `PercentImprovement.cs`)                                      |
| `services/`    | Core services for storage, maintenance, and statistics calculation                                  |

#### Key Controllers

- **`ProjectsController.cs`** - CRUD operations for projects
- **`ExperimentsController.cs`** - CRUD operations for experiments within projects
- **`ResultsController.cs`** - Recording and retrieving experiment results
- **`AnalysisController.cs`** - Statistical analysis and comparison endpoints
- **`DownloadController.cs`** - Support document downloads

#### Key Services

- **`AzureBlobStorageService.cs`** - Primary storage service for experiments and results
- **`AzureBlobStorageMaintenanceService.cs`** - Background service for storage optimization
- **`CalculateStatisticsService.cs`** - Statistical calculations including p-values
- **`AzureBlobSupportDocsService.cs`** - Support document management

### Evaluator (`/evaluator`)

The evaluator is a C# .NET 8 service that orchestrates the evaluation pipeline. It enqueues inference and evaluation jobs to Azure Storage Queues.

| Folder         | Description                                                     |
| -------------- | --------------------------------------------------------------- |
| `config/`      | Configuration interfaces and implementations                    |
| `controllers/` | API endpoint controllers for enqueueing evaluations             |
| `exceptions/`  | Custom exception classes (e.g., `DeadletterException`)          |
| `models/`      | Data models including `EnqueueRequest`, `PipelineRequest`, etc. |
| `samples/`     | Sample ground truth files in JSON and YAML formats              |
| `services/`    | Core services for queue management and pipeline orchestration   |
| `templates/`   | Template files for evaluation configuration                     |

#### Key Controllers

- **`EvaluationsController.cs`** - Endpoints for enqueueing and managing evaluation jobs

### Evaluation (`/evaluation`)

The evaluation service is a Python script that processes evaluation jobs from an Azure Storage Queue and reports results to the catalog.

| File               | Description                                      |
| ------------------ | ------------------------------------------------ |
| `eval.py`          | Main evaluation script that listens to the queue |
| `requirements.txt` | Python package dependencies                      |
| `coherence.txt`    | Prompt template for coherence evaluation         |
| `groundedness.txt` | Prompt template for groundedness evaluation      |
| `relevance.txt`    | Prompt template for relevance evaluation         |

### UI (`/ui`)

The frontend is a Svelte application with TypeScript for type safety.

| Folder/File      | Description                               |
| ---------------- | ----------------------------------------- |
| `src/App.svelte` | Main application component                |
| `src/lib/`       | Reusable components and TypeScript models |

#### Key Components

- **`ProjectsList.svelte`** / **`ProjectCard.svelte`** - Project listing and display
- **`ExperimentsList.svelte`** / **`ExperimentCard.svelte`** - Experiment listing and display
- **`ExperimentPage.svelte`** - Detailed experiment view
- **`ComparisonTable.svelte`** - Result comparison tables
- **`FreeFilter.svelte`** - Custom filter expression input
- **`MetricsFilter.svelte`** / **`TagsFilter.svelte`** - Filtering controls

#### Key Models (TypeScript)

- **`Project.ts`** - Project data model
- **`Experiment.ts`** - Experiment data model
- **`Result.ts`** - Result data model
- **`Metric.ts`** / **`MetricDefinition.ts`** - Metric-related models
- **`Comparison.ts`** / **`ComparisonByRef.ts`** - Comparison data structures

## Coding Guidelines

### C# (API)

- Follow [Microsoft C# Coding Conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful names for classes, methods, and variables
- Add XML documentation comments for public APIs
- Use `async`/`await` for asynchronous operations
- Handle exceptions appropriately and use `HttpException` for API errors

### TypeScript/Svelte (UI)

- Follow existing code style in the project
- Use TypeScript types and interfaces for type safety
- Keep components focused and single-purpose
- Use Svelte stores for shared state management

### Python (Evaluation)

- Follow [PEP 8](https://peps.python.org/pep-0008/) style guidelines
- Use type hints where applicable
- Document functions with docstrings
- Use environment variables for configuration (via `.env` files)

## Pull Request Guidelines

### Before Submitting

1. **Search existing PRs** - Ensure your contribution isn't a duplicate
2. **Create an issue first** - For significant changes, discuss the approach in an issue
3. **Branch from `main`** - Create a feature branch for your changes
4. **Test your changes** - Ensure the API, UI, and evaluator projects work correctly
5. **Update documentation** - Update README files if your changes affect usage

### Submitting a Pull Request

1. Fork the repository and create your branch from `main`
2. Make your changes following the coding guidelines
3. Test your changes locally
4. Commit your changes with clear, descriptive commit messages
5. Push to your fork and submit a pull request
6. Fill out the PR template completely

### PR Checklist

- [ ] I have read the [CONTRIBUTING](CONTRIBUTING.md) document
- [ ] My code follows the code style of this project
- [ ] I have tested my changes locally
- [ ] I have updated documentation as needed
- [ ] My changes generate no new warnings or errors

### After Submitting

- Be responsive to feedback from reviewers
- Make requested changes promptly
- Once approved, your PR will be merged by a maintainer

## Thank You

Your contributions to open source, large or small, make great projects like this possible. Thank you for taking the time to contribute!
