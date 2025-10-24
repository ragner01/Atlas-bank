# Physical Device Testing Guide for AtlasBank Mobile App

## 📱 **Prerequisites**

### **Development Environment**
- **Node.js**: v18+ (check with `node --version`)
- **npm**: Latest version (check with `npm --version`)
- **Expo CLI**: Install with `npm install -g @expo/cli`
- **EAS CLI**: Install with `npm install -g eas-cli`

### **iOS Testing Requirements**
- **macOS**: Required for iOS development
- **Xcode**: Latest version from App Store
- **iOS Simulator**: Included with Xcode
- **Apple Developer Account**: For device testing (free or paid)
- **Physical iOS Device**: iPhone/iPad with iOS 13+

### **Android Testing Requirements**
- **Android Studio**: Latest version
- **Android SDK**: API level 33+ (Android 13+)
- **Physical Android Device**: Android 8.0+ (API level 26+)
- **USB Debugging**: Enabled on device

## 🔧 **Setup Instructions**

### **1. Install Development Tools**

```bash
# Install Expo CLI globally
npm install -g @expo/cli

# Install EAS CLI for builds
npm install -g eas-cli

# Verify installations
expo --version
eas --version
```

### **2. Configure Mobile App**

```bash
cd apps/mobile/expo

# Install dependencies
npm install

# Copy environment configuration
cp env.example .env

# Edit .env file with your local IP address
# Replace localhost with your computer's IP address
```

### **3. Update Environment Configuration**

Edit `.env` file to use your computer's IP address instead of localhost:

```bash
# Get your computer's IP address
ifconfig | grep "inet " | grep -v 127.0.0.1

# Update .env file with your IP
EXPO_PUBLIC_PAYMENTS_BASE=http://YOUR_IP:5191
EXPO_PUBLIC_TRUST_PORTAL=http://YOUR_IP:5802
EXPO_PUBLIC_OFFLINE_BASE=http://YOUR_IP:5622
EXPO_PUBLIC_AGENT_BASE=http://YOUR_IP:5621
```

## 📱 **iOS Device Testing**

### **Method 1: Expo Go App (Easiest)**

1. **Install Expo Go**:
   - Download Expo Go from App Store
   - Install on your iOS device

2. **Start Development Server**:
   ```bash
   cd apps/mobile/expo
   npm run start
   ```

3. **Connect Device**:
   - Scan QR code with Expo Go app
   - Or use same network and enter URL manually

4. **Test Features**:
   - Login with test credentials
   - Test transfer functionality
   - Test offline mode
   - Test biometric authentication

### **Method 2: Development Build (More Control)**

1. **Configure EAS**:
   ```bash
   cd apps/mobile/expo
   eas build:configure
   ```

2. **Create Development Build**:
   ```bash
   # For iOS simulator
   eas build --platform ios --profile development

   # For physical device
   eas build --platform ios --profile development --local
   ```

3. **Install on Device**:
   - Download build from EAS dashboard
   - Install via Xcode or TestFlight

### **Method 3: Direct Xcode Build**

1. **Generate iOS Project**:
   ```bash
   cd apps/mobile/expo
   npx expo run:ios
   ```

2. **Open in Xcode**:
   ```bash
   open ios/atlasmobile.xcworkspace
   ```

3. **Configure Signing**:
   - Select your development team
   - Choose your device
   - Build and run

## 🤖 **Android Device Testing**

### **Method 1: Expo Go App (Easiest)**

1. **Install Expo Go**:
   - Download Expo Go from Google Play Store
   - Install on your Android device

2. **Enable USB Debugging**:
   - Go to Settings > About Phone
   - Tap "Build Number" 7 times
   - Go to Settings > Developer Options
   - Enable "USB Debugging"

3. **Start Development Server**:
   ```bash
   cd apps/mobile/expo
   npm run start
   ```

4. **Connect Device**:
   - Scan QR code with Expo Go app
   - Or use same network and enter URL manually

### **Method 2: Development Build**

1. **Create Development Build**:
   ```bash
   cd apps/mobile/expo
   eas build --platform android --profile development
   ```

2. **Install APK**:
   - Download APK from EAS dashboard
   - Install on device via ADB or file manager

### **Method 3: Direct Android Build**

1. **Generate Android Project**:
   ```bash
   cd apps/mobile/expo
   npx expo run:android
   ```

2. **Connect Device**:
   ```bash
   # Check connected devices
   adb devices

   # Install and run
   npx expo run:android --device
   ```

## 🌐 **Network Configuration**

### **Local Network Setup**

1. **Ensure Services Running**:
   ```bash
   # Start AtlasBank services
   make up

   # Verify services are accessible
   curl http://localhost:5191/health
   curl http://localhost:5802/health
   ```

2. **Configure Firewall**:
   ```bash
   # macOS - Allow incoming connections
   sudo pfctl -f /etc/pf.conf

   # Or temporarily disable firewall for testing
   sudo pfctl -d
   ```

3. **Test Network Connectivity**:
   ```bash
   # From your mobile device, test connectivity
   # Use browser or network testing app
   http://YOUR_IP:5191/health
   ```

## 🧪 **Testing Scenarios**

### **1. Basic Functionality Tests**

```bash
# Test login flow
- Enter MSISDN: 2348100000001
- Enter PIN: 1234
- Verify biometric setup (if available)
- Check device ID generation

# Test transfer flow
- Send money to: msisdn::2348100000002
- Amount: 25000 (₦250.00)
- Verify online transfer
- Test offline fallback
```

### **2. Network Condition Tests**

```bash
# Test with different network conditions
- WiFi connection
- Cellular data
- Slow network (throttle connection)
- No network (airplane mode)
- Intermittent network
```

### **3. Device-Specific Tests**

```bash
# iOS specific
- Face ID authentication
- Touch ID authentication
- iOS keyboard behavior
- iOS-specific UI elements

# Android specific
- Fingerprint authentication
- Android keyboard behavior
- Android-specific UI elements
- Back button behavior
```

### **4. Security Tests**

```bash
# Test secure storage
- Verify PIN storage in keychain
- Test credential persistence
- Test secure data clearing

# Test input validation
- Invalid MSISDN formats
- Invalid PIN formats
- Invalid amounts
- SQL injection attempts
```

## 🔍 **Debugging and Troubleshooting**

### **Common Issues and Solutions**

1. **Network Connection Issues**:
   ```bash
   # Check if services are running
   docker ps | grep atlas

   # Check network connectivity
   ping YOUR_IP

   # Check firewall settings
   sudo pfctl -sr
   ```

2. **Expo Go Connection Issues**:
   ```bash
   # Clear Expo cache
   expo r -c

   # Restart development server
   npm run start -- --clear
   ```

3. **Build Issues**:
   ```bash
   # Clear node modules
   rm -rf node_modules
   npm install

   # Clear Expo cache
   expo r -c
   ```

4. **Device Not Recognized**:
   ```bash
   # iOS - Check Xcode device list
   xcrun devicectl list devices

   # Android - Check ADB devices
   adb devices
   adb kill-server
   adb start-server
   ```

### **Debug Tools**

1. **Expo DevTools**:
   - Open browser to `http://localhost:19002`
   - View logs and errors
   - Monitor network requests

2. **React Native Debugger**:
   ```bash
   # Install React Native Debugger
   brew install --cask react-native-debugger
   ```

3. **Flipper**:
   - Install Flipper desktop app
   - Connect to device for advanced debugging

## 📊 **Performance Testing**

### **1. Load Testing**

```bash
# Test with multiple concurrent users
- Simulate multiple login attempts
- Test concurrent transfers
- Monitor memory usage
- Check battery impact
```

### **2. Memory Testing**

```bash
# Monitor memory usage
- Use Xcode Instruments (iOS)
- Use Android Studio Profiler (Android)
- Test memory leaks
- Monitor garbage collection
```

### **3. Battery Testing**

```bash
# Test battery impact
- Monitor battery usage over time
- Test background operations
- Check for battery-draining operations
- Optimize for battery life
```

## 🚀 **Production Testing**

### **1. TestFlight (iOS)**

```bash
# Create production build
eas build --platform ios --profile production

# Submit to TestFlight
eas submit --platform ios
```

### **2. Google Play Internal Testing (Android)**

```bash
# Create production build
eas build --platform android --profile production

# Submit to Google Play
eas submit --platform android
```

### **3. Beta Testing**

```bash
# Create beta build
eas build --platform all --profile preview

# Distribute to beta testers
eas update --branch beta
```

## 📋 **Testing Checklist**

### **Pre-Testing Setup**
- [ ] Development environment configured
- [ ] Physical device prepared
- [ ] Network connectivity verified
- [ ] AtlasBank services running
- [ ] Environment variables configured

### **Basic Functionality**
- [ ] App launches successfully
- [ ] Login flow works
- [ ] Biometric authentication works
- [ ] Transfer functionality works
- [ ] Offline mode works
- [ ] Sync functionality works

### **Network Testing**
- [ ] WiFi connectivity
- [ ] Cellular data connectivity
- [ ] Slow network handling
- [ ] No network handling
- [ ] Network recovery

### **Device-Specific Testing**
- [ ] iOS-specific features
- [ ] Android-specific features
- [ ] Different screen sizes
- [ ] Different orientations
- [ ] Hardware features (camera, biometrics)

### **Security Testing**
- [ ] Secure storage
- [ ] Input validation
- [ ] Authentication flow
- [ ] Data encryption
- [ ] Network security

### **Performance Testing**
- [ ] App launch time
- [ ] Screen transition speed
- [ ] Memory usage
- [ ] Battery impact
- [ ] Network efficiency

This comprehensive guide will help you test the AtlasBank mobile app on physical devices with confidence!

