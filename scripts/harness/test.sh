#!/usr/bin/env bash
set -euo pipefail

root_dir=$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)
cd "$root_dir"

echo "==> Test: running .NET tests..."
dotnet test experiment-catalog.sln --nologo --verbosity quiet

echo "==> Test: running UI Playwright tests..."
if [ -d "ui" ] && [ -f "ui/playwright.config.ts" ]; then
  cd ui
  npx playwright test
  cd "$root_dir"
fi

echo "Tests passed."
