# Physical Device Testing Checklist

## 📱 **Quick Start Guide**

### **1. Setup (One-time)**
```bash
# Run the setup script
./scripts/setup-mobile-testing.sh

# Or manually:
cd apps/mobile/expo
npm install
cp env.example .env
# Edit .env with your local IP address
```

### **2. Start Testing**
```bash
# Start AtlasBank services
make up

# Start mobile development server
cd apps/mobile/expo
npm run start
```

### **3. Connect Device**
- **iOS**: Install Expo Go from App Store
- **Android**: Install Expo Go from Google Play
- Scan QR code or enter URL manually

## 🧪 **Testing Scenarios**

### **Basic Functionality**
- [ ] **App Launch**: App opens without crashes
- [ ] **Login Flow**: 
  - MSISDN: `2348100000001`
  - PIN: `1234` (or any 4-6 digits)
  - Biometric setup (if available)
- [ ] **Home Screen**: Trust badge displays
- [ ] **Navigation**: All screens accessible

### **Transfer Testing**
- [ ] **Online Transfer**:
  - Destination: `msisdn::2348100000002`
  - Amount: `25000` (₦250.00)
  - Verify success message
- [ ] **Offline Transfer**:
  - Disable WiFi/cellular
  - Attempt transfer
  - Verify offline queue message
  - Re-enable network
  - Test sync functionality

### **Agent Operations**
- [ ] **Cash-in Intent**:
  - Agent Code: `AG001`
  - Amount: `50000`
  - Verify success message
- [ ] **Cash-out Intent**:
  - Agent Code: `AG001`
  - Amount: `30000`
  - Verify success message

### **Network Testing**
- [ ] **WiFi Connection**: App works on WiFi
- [ ] **Cellular Data**: App works on mobile data
- [ ] **Slow Network**: App handles slow connections
- [ ] **No Network**: Offline mode works
- [ ] **Network Recovery**: Sync works when reconnected

### **Device-Specific Testing**
- [ ] **iOS Features**:
  - Face ID/Touch ID authentication
  - iOS keyboard behavior
  - iOS-specific UI elements
- [ ] **Android Features**:
  - Fingerprint authentication
  - Android keyboard behavior
  - Back button behavior

### **Security Testing**
- [ ] **Input Validation**:
  - Invalid MSISDN formats
  - Invalid PIN formats
  - Invalid amounts
- [ ] **Secure Storage**:
  - PIN stored securely
  - Device ID generated
  - Credentials persist across app restarts

## 🔍 **Common Issues & Solutions**

### **Connection Issues**
```bash
# Check if services are running
curl http://localhost:5191/health

# Check local IP
ifconfig | grep "inet " | grep -v 127.0.0.1

# Restart development server
npm run start -- --clear
```

### **Build Issues**
```bash
# Clear cache and reinstall
rm -rf node_modules
npm install
expo r -c
```

### **Device Not Found**
```bash
# iOS
xcrun devicectl list devices

# Android
adb devices
adb kill-server && adb start-server
```

## 📊 **Performance Testing**

### **Memory Usage**
- Monitor memory usage during testing
- Check for memory leaks
- Test with multiple screen transitions

### **Battery Impact**
- Monitor battery usage
- Test background operations
- Check for battery-draining operations

### **Network Efficiency**
- Monitor data usage
- Check request/response sizes
- Test with slow networks

## 🚀 **Production Testing**

### **TestFlight (iOS)**
```bash
# Create production build
eas build --platform ios --profile production

# Submit to TestFlight
eas submit --platform ios
```

### **Google Play Internal Testing (Android)**
```bash
# Create production build
eas build --platform android --profile production

# Submit to Google Play
eas submit --platform android
```

## 📋 **Success Criteria**

### **Functional Requirements**
- [ ] All screens load without errors
- [ ] Login flow works with biometrics and PIN
- [ ] Transfer operations work online and offline
- [ ] Agent operations create proper intents
- [ ] Trust badges display correctly
- [ ] Offline sync processes queued operations

### **Performance Requirements**
- [ ] App launch time < 3 seconds
- [ ] Screen transitions < 1 second
- [ ] Memory usage reasonable
- [ ] Battery impact minimal
- [ ] Network efficiency good

### **Security Requirements**
- [ ] Input validation prevents invalid requests
- [ ] Secure storage protects credentials
- [ ] Network requests use HTTPS
- [ ] Error messages don't expose sensitive data
- [ ] Authentication flow is secure

This checklist ensures comprehensive testing of the AtlasBank mobile app on physical devices!

