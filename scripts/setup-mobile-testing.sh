#!/bin/bash

# AtlasBank Mobile App - Physical Device Testing Setup Script

set -e

echo "🚀 AtlasBank Mobile App - Physical Device Testing Setup"
echo "======================================================"

# Check if we're in the right directory
if [ ! -f "apps/mobile/expo/package.json" ]; then
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

# Install global dependencies
echo "📦 Installing global dependencies..."
if ! command -v expo &> /dev/null; then
    echo "Installing Expo CLI..."
    npm install -g @expo/cli
else
    echo "✅ Expo CLI already installed: $(expo --version)"
fi

if ! command -v eas &> /dev/null; then
    echo "Installing EAS CLI..."
    npm install -g eas-cli
else
    echo "✅ EAS CLI already installed: $(eas --version)"
fi

# Get local IP address
echo "🌐 Getting local IP address..."
LOCAL_IP=$(ifconfig | grep "inet " | grep -v 127.0.0.1 | head -1 | awk '{print $2}')
if [ -z "$LOCAL_IP" ]; then
    echo "❌ Error: Could not determine local IP address"
    exit 1
fi
echo "✅ Local IP address: $LOCAL_IP"

# Navigate to mobile app directory
cd apps/mobile/expo

# Install dependencies
echo "📦 Installing mobile app dependencies..."
npm install

# Create environment file
echo "⚙️ Creating environment configuration..."
if [ ! -f ".env" ]; then
    cp env.example .env
    echo "✅ Created .env file from template"
else
    echo "✅ .env file already exists"
fi

# Update environment file with local IP
echo "🔧 Updating environment configuration with local IP..."
sed -i.bak "s/localhost/$LOCAL_IP/g" .env
echo "✅ Updated .env with local IP: $LOCAL_IP"

# Check if AtlasBank services are running
echo "🔍 Checking AtlasBank services..."
if curl -s http://localhost:5191/health > /dev/null 2>&1; then
    echo "✅ Payments API is running"
else
    echo "⚠️ Payments API is not running. Please run 'make up' to start services."
fi

if curl -s http://localhost:5802/health > /dev/null 2>&1; then
    echo "✅ Trust Portal is running"
else
    echo "⚠️ Trust Portal is not running. Please run 'make up' to start services."
fi

# Display testing instructions
echo ""
echo "🎯 Physical Device Testing Setup Complete!"
echo "=========================================="
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
echo "🔧 Troubleshooting:"
echo "- If connection fails, check firewall settings"
echo "- Ensure device and computer are on same network"
echo "- Try disabling VPN if active"
echo ""
echo "📚 For detailed testing instructions, see:"
echo "   docs/PHYSICAL-DEVICE-TESTING.md"
echo ""
echo "🚀 Ready to test! Run 'npm run start' to begin."

