// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using Newtonsoft.Json;

namespace Catalog;

public class Project
{
    [JsonProperty("name", Required = Required.Always)]
    public required string Name { get; set; }
}