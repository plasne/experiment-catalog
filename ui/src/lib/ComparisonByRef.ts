// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.

interface ComparisonByRef {
    last_results_for_baseline_experiment: Record<string, Result>,
    baseline_results_for_chosen_experiment: Record<string, Result>,
    chosen_results_for_chosen_experiment: Record<string, Result>,
}