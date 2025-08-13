#!/bin/bash

# Run NoLock.Social.Web Blazor WebAssembly application
echo "Starting NoLock.Social.Web..."
echo "Application will be available at http://localhost:5002"
echo ""

cd NoLock.Social.Web
dotnet run --launch-profile http