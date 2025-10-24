#!/bin/bash

# AtlasBank Console Frontend Setup Script

set -e

echo "🎨 AtlasBank Console Frontend Setup"
echo "==================================="

# Check if we're in the right directory
if [ ! -f "apps/frontend/atlas-console/package.json" ]; then
    echo "❌ Error: Please run this script from the AtlasBank root directory"
    exit 1
fi

# Check Node.js version
echo "📋 Checking Node.js version..."
NODE_VERSION=$(node --version | cut -d'v' -f2 | cut -d'.' -f1)
if [ "$NODE_VERSION" -lt 18 ]; then
    echo "❌ Error: Node.js 18+ required. Current version: $(node --version)"
    exit 1
fi
echo "✅ Node.js version: $(node --version)"

# Check npm version
echo "📋 Checking npm version..."
echo "✅ npm version: $(npm --version)"

# Navigate to frontend directory
cd apps/frontend/atlas-console

# Install dependencies
echo "📦 Installing frontend dependencies..."
npm install

# Create environment file
echo "⚙️ Creating environment configuration..."
if [ ! -f ".env" ]; then
    cp env.example .env
    echo "✅ Created .env file from template"
else
    echo "✅ .env file already exists"
fi

# Check if AtlasBank services are running
echo "🔍 Checking AtlasBank services..."
if curl -s http://localhost:5191/health > /dev/null 2>&1; then
    echo "✅ Payments API is running"
else
    echo "⚠️ Payments API is not running. Please run 'make up' to start services."
fi

if curl -s http://localhost:5190/health > /dev/null 2>&1; then
    echo "✅ Ledger API is running"
else
    echo "⚠️ Ledger API is not running. Please run 'make up' to start services."
fi

if curl -s http://localhost:5802/health > /dev/null 2>&1; then
    echo "✅ Trust Portal is running"
else
    echo "⚠️ Trust Portal is not running. Please run 'make up' to start services."
fi

# Run type checking
echo "🔧 Running type checking..."
if npm run type-check > /dev/null 2>&1; then
    echo "✅ TypeScript compilation successful"
else
    echo "❌ TypeScript compilation failed"
    exit 1
fi

# Run linting
echo "🔧 Running linting..."
if npm run lint > /dev/null 2>&1; then
    echo "✅ Linting passed"
else
    echo "⚠️ Linting issues found (non-blocking)"
fi

# Test build
echo "🔧 Testing production build..."
if npm run build > /dev/null 2>&1; then
    echo "✅ Production build successful"
    rm -rf dist
else
    echo "❌ Production build failed"
    exit 1
fi

echo ""
echo "🎉 AtlasBank Console Frontend Setup Complete!"
echo "============================================="
echo ""
echo "📱 Next Steps:"
echo "1. Start the development server:"
echo "   make console-dev"
echo "   or"
echo "   cd apps/frontend/atlas-console && npm run dev"
echo ""
echo "2. Access the application:"
echo "   http://localhost:5173"
echo ""
echo "3. Login with demo credentials:"
echo "   Phone: 2348100000001"
echo "   PIN: 1234"
echo ""
echo "🔧 Available Commands:"
echo "  make console-dev    - Start development server"
echo "  make console-build - Build for production"
echo "  make console-test  - Run tests and linting"
echo ""
echo "📚 For detailed documentation, see:"
echo "   apps/frontend/atlas-console/README.md"
echo ""
echo "🚀 Ready to develop! Run 'make console-dev' to begin."
