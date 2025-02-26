// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

using System;

namespace Evaluator;

public class BlobRef
{
    public BlobRef(string uri)
    {
        var parts = uri.Split("/", 2);
        if (parts.Length != 2)
        {
            throw new Exception($"expected a container and blob name separated by a /");
        }

        this.Container = parts[0];
        this.BlobName = parts[1];
    }

    public string Container { get; set; }
    public string BlobName { get; set; }
}