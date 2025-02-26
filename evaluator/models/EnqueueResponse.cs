// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;
using Newtonsoft.Json;

namespace Evaluator;

public class EnqueueResponse
{
    [JsonProperty("run_id", Required = Required.Always)]
    public required Guid RunId { get; set; }
}