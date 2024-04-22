#!/bin/bash

# Navigate to the ui directory
cd ui

# Install any dependencies
npm install

# Build the Svelte project
npm run build

# Navigate to the catalog
cd ../catalog

# Copy the built files to the wwwroot directory
mkdir -p wwwroot/
cp -r ../ui/dist/* wwwroot/