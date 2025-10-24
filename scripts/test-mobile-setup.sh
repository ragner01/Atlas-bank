#!/bin/bash

# AtlasBank Mobile App - Quick Test Script

set -e

echo "🧪 AtlasBank Mobile App - Quick Test"
echo "===================================="

# Check if we're in the right directory
if [ ! -f "apps/mobile/expo/package.json" ]; then
    echo "❌ Error: Please run this script from the AtlasBank root directory"
    exit 1
fi

# Check if AtlasBank services are running
echo "🔍 Checking AtlasBank services..."

SERVICES_RUNNING=true

if ! curl -s http://localhost:5191/health > /dev/null 2>&1; then
    echo "❌ Payments API is not running"
    SERVICES_RUNNING=false
else
    echo "✅ Payments API is running"
fi

if ! curl -s http://localhost:5802/health > /dev/null 2>&1; then
    echo "❌ Trust Portal is not running"
    SERVICES_RUNNING=false
else
    echo "✅ Trust Portal is running"
fi

if ! curl -s http://localhost:5622/health > /dev/null 2>&1; then
    echo "❌ Offline Queue is not running"
    SERVICES_RUNNING=false
else
    echo "✅ Offline Queue is running"
fi

if ! curl -s http://localhost:5621/health > /dev/null 2>&1; then
    echo "❌ Agent Network is not running"
    SERVICES_RUNNING=false
else
    echo "✅ Agent Network is running"
fi

if [ "$SERVICES_RUNNING" = false ]; then
    echo ""
    echo "⚠️ Some AtlasBank services are not running."
    echo "Please run 'make up' to start all services."
    echo ""
    read -p "Do you want to start the services now? (y/n): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "Starting AtlasBank services..."
        make up
        echo "Waiting for services to be ready..."
        sleep 15
    else
        echo "Please start services manually and run this script again."
        exit 1
    fi
fi

# Check mobile app setup
echo ""
echo "📱 Checking mobile app setup..."

cd apps/mobile/expo

# Check if dependencies are installed
if [ ! -d "node_modules" ]; then
    echo "❌ Dependencies not installed. Installing..."
    npm install
else
    echo "✅ Dependencies are installed"
fi

# Check if .env file exists
if [ ! -f ".env" ]; then
    echo "❌ .env file not found. Creating from template..."
    cp env.example .env
    echo "✅ Created .env file"
else
    echo "✅ .env file exists"
fi

# Check if Expo CLI is installed
if ! command -v expo &> /dev/null; then
    echo "❌ Expo CLI not installed. Installing..."
    npm install -g @expo/cli
else
    echo "✅ Expo CLI is installed"
fi

# Get local IP address
LOCAL_IP=$(ifconfig | grep "inet " | grep -v 127.0.0.1 | head -1 | awk '{print $2}')
if [ -z "$LOCAL_IP" ]; then
    echo "❌ Error: Could not determine local IP address"
    exit 1
fi
echo "✅ Local IP address: $LOCAL_IP"

# Update .env file with local IP
sed -i.bak "s/localhost/$LOCAL_IP/g" .env
echo "✅ Updated .env with local IP"

# Test TypeScript SDK build
echo ""
echo "🔧 Testing TypeScript SDK build..."
cd ../../../sdks/typescript
if npm run build > /dev/null 2>&1; then
    echo "✅ TypeScript SDK builds successfully"
else
    echo "❌ TypeScript SDK build failed"
    exit 1
fi

# Test C# SDK build
echo "🔧 Testing C# SDK build..."
cd ../csharp
if dotnet build > /dev/null 2>&1; then
    echo "✅ C# SDK builds successfully"
else
    echo "❌ C# SDK build failed"
    exit 1
fi

# Return to mobile app directory
cd ../../apps/mobile/expo

echo ""
echo "🎉 Mobile app setup test completed successfully!"
echo "=============================================="
echo ""
echo "📱 Next Steps:"
echo "1. Install Expo Go on your mobile device:"
echo "   - iOS: App Store → Search 'Expo Go'"
echo "   - Android: Google Play → Search 'Expo Go'"
echo ""
echo "2. Start the development server:"
echo "   npm run start"
echo ""
echo "3. Connect your device:"
echo "   - Scan QR code with Expo Go app"
echo "   - Or enter URL manually: exp://$LOCAL_IP:8081"
echo ""
echo "4. Test the app:"
echo "   - Login with MSISDN: 2348100000001"
echo "   - Use any 4-6 digit PIN"
echo "   - Test transfer functionality"
echo "   - Test offline mode"
echo ""
echo "📚 For detailed testing instructions, see:"
echo "   docs/PHYSICAL-DEVICE-TESTING.md"
echo "   docs/MOBILE-TESTING-CHECKLIST.md"
echo ""
echo "🚀 Ready to test! Run 'npm run start' to begin."

