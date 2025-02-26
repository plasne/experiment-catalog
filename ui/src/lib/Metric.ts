// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

interface Metric {
    count: number;
    value: number;
    normalized: number;
    std_dev: number;
    tags: string[];
}