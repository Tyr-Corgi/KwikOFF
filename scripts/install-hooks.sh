#!/bin/bash
# Install git hooks for the project

set -e

echo "🔧 Installing git hooks..."

# Create hooks directory if it doesn't exist
mkdir -p .git/hooks

# Copy pre-push hook
cp .githooks/pre-push .git/hooks/pre-push
chmod +x .git/hooks/pre-push

# Make analysis script executable
chmod +x scripts/check-code-length.sh

echo "✅ Git hooks installed successfully!"
echo ""
echo "The following checks will run before each push:"
echo "  - File length validation (max 1000 lines)"
echo "  - Class length validation (max 500 lines)"
echo ""
echo "To bypass these checks use: git push --no-verify"
echo "(Not recommended unless fixing critical issues)"


