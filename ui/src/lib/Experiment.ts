// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

interface Experiment {
    name?: string;
    hypothesis?: string;
    created?: Date;
    annotations?: Annotation[];
}