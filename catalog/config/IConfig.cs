// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

namespace Catalog;

public interface IConfig
{
    int PORT { get; }
    string OPEN_TELEMETRY_CONNECTION_STRING { get; }
    string AZURE_STORAGE_ACCOUNT_NAME { get; }
    string AZURE_STORAGE_ACCOUNT_CONNSTRING { get; }
    int CONCURRENCY { get; }
    int REQUIRED_BLOCK_SIZE_IN_KB_FOR_OPTIMIZE { get; }
    int REQUIRED_MIN_OF_IDLE_BEFORE_OPTIMIZE { get; }
    int OPTIMIZE_EVERY_X_MINUTES { get; }
    string PATH_TEMPLATE { get; }

    void Validate();
}