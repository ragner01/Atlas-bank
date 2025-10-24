# Phase 24 — SDKs & Mobile App (Enhanced Security & Resilience)

## 🎯 **What You Got**

### **TypeScript SDK (`@atlasbank/sdk`)**
- **✅ Enhanced Security**: Input validation, HMAC signing, request timeouts
- **✅ Resilience Patterns**: Retry logic with exponential backoff, circuit breaker support
- **✅ Error Handling**: Comprehensive error types with proper HTTP status mapping
- **✅ Type Safety**: Full TypeScript support with strict type checking
- **✅ Offline Support**: Queue operations for offline-first mobile experience
- **✅ Trust Integration**: Dynamic trust badge URL generation

### **C# SDK (`AtlasBank.Sdk`)**
- **✅ Production Ready**: Full .NET 9 support with async/await patterns
- **✅ Resilience**: Polly integration with retry and circuit breaker policies
- **✅ Configuration**: Flexible options pattern with validation
- **✅ Logging**: Structured logging with correlation IDs
- **✅ Error Handling**: Custom exception types with detailed error information
- **✅ Performance**: Connection pooling and efficient HTTP client usage

### **React Native Mobile App**
- **✅ Biometric Authentication**: Face ID/Touch ID integration with fallback
- **✅ Offline-First**: Automatic fallback to offline queue when network unavailable
- **✅ Secure Storage**: PIN and credentials stored using Expo SecureStore
- **✅ Input Validation**: Client-side validation with error display
- **✅ Network Awareness**: Real-time network status indicator
- **✅ Trust Badges**: Live SVG trust score display
- **✅ Agent Integration**: Cash-in/cash-out with agent network

## 🔒 **Security Features Implemented**

### **SDK Security**
```typescript
// Input validation
if (!validateMsisdn(msisdn)) {
  throw new Error("Invalid MSISDN format");
}

// HMAC signing for offline operations
const signature = await hmacHex(secret, deviceId, payload, nonce, tenantId);

// Request timeouts and retry logic
const client = new AtlasClient({
  timeout: 30000,
  retry: { attempts: 3, delay: 1000, backoff: 2 }
});
```

### **Mobile App Security**
```typescript
// Biometric authentication
const result = await LocalAuth.authenticateAsync({
  promptMessage: "Authenticate with biometrics",
  fallbackLabel: "Use PIN",
});

// Secure credential storage
await SecureStore.setItemAsync("pin", pin);
await SecureStore.setItemAsync("deviceId", deviceId);

// Input validation
if (!validatePin(pin)) {
  setError("PIN must be 4-6 digits");
}
```

## 🏗️ **Architecture Highlights**

### **Resilience Patterns**
- **Circuit Breaker**: Prevents cascade failures (3 failures → 30s break)
- **Retry Logic**: Exponential backoff for transient failures
- **Timeout Handling**: Configurable request timeouts
- **Fallback Mechanisms**: Offline queue when online fails

### **Error Handling**
- **Structured Errors**: Custom error types with codes and details
- **HTTP Status Mapping**: Proper status code to error mapping
- **User-Friendly Messages**: Localized error messages for mobile
- **Logging Integration**: Structured logging with correlation IDs

### **Offline-First Design**
- **Queue Operations**: Store operations when offline
- **Sync Mechanism**: Batch sync when online
- **HMAC Signatures**: Cryptographic integrity verification
- **Network Awareness**: Real-time connectivity status

## 📱 **Mobile App Features**

### **Authentication Flow**
1. **PIN Entry**: 4-6 digit PIN validation
2. **Biometric Setup**: Optional Face ID/Touch ID enrollment
3. **Device Registration**: Unique device ID generation
4. **Secure Storage**: Credentials encrypted in device keychain

### **Transfer Flow**
1. **Online Attempt**: Try real-time transfer first
2. **Offline Fallback**: Queue operation if network fails
3. **Sync Process**: Batch process queued operations
4. **Status Updates**: Real-time operation status

### **Agent Integration**
- **Cash-in Intent**: Generate agent deposit codes
- **Cash-out Intent**: Generate agent withdrawal codes
- **Status Tracking**: Monitor agent operation status

## 🔧 **Configuration**

### **TypeScript SDK**
```typescript
const client = new AtlasClient({
  baseUrl: "http://localhost:5191",
  tenantId: "tnt_demo",
  trustBadgeBase: "http://localhost:5802",
  offlineBase: "http://localhost:5622",
  timeout: 30000,
  retry: {
    attempts: 3,
    delay: 1000,
    backoff: 2,
  },
});
```

### **C# SDK**
```csharp
var client = new AtlasClient(new AtlasClientOptions
{
    BaseUrl = "http://localhost:5191",
    TenantId = "tnt_demo",
    TimeoutMs = 30000,
    Retry = new RetryOptions
    {
        Attempts = 3,
        DelayMs = 1000,
        BackoffMultiplier = 2.0
    }
});
```

### **Mobile App Environment**
```bash
EXPO_PUBLIC_PAYMENTS_BASE=http://localhost:5191
EXPO_PUBLIC_TRUST_PORTAL=http://localhost:5802
EXPO_PUBLIC_OFFLINE_BASE=http://localhost:5622
EXPO_PUBLIC_AGENT_BASE=http://localhost:5621
```

## 🚀 **Integration Points**

### **With Existing Phases**
- **Phase 18**: Trust Portal for badge generation
- **Phase 19**: Limits enforcement for card charges
- **Phase 20**: KYC/AML integration for compliance
- **Phase 22**: Offline queue and agent network
- **Phase 23**: Feature store for risk scoring

### **API Endpoints Used**
- `/payments/transfers/with-risk` - Risk-assessed transfers
- `/payments/cnp/charge/enforced` - Limits-enforced card charges
- `/ledger/accounts/{id}/balance/global` - Account balance queries
- `/offline/ops` - Offline operation queuing
- `/offline/sync` - Offline operation synchronization
- `/agent/cashin/intent` - Agent cash-in requests
- `/agent/withdraw/intent` - Agent cash-out requests

## 🔐 **Security Considerations**

### **Production Readiness**
- **Replace Demo Secrets**: Use proper secret management (Azure Key Vault, AWS Secrets Manager)
- **Device Attestation**: Implement Android SafetyNet/Play Integrity, iOS DeviceCheck
- **mTLS**: Enable mutual TLS for service-to-service communication
- **PIN Hashing**: Move PIN handling to server-side with Argon2id + HSM
- **Ed25519 Keys**: Replace HMAC with device-bound Ed25519 signatures

### **Compliance**
- **PCI DSS**: Card token handling follows PCI guidelines
- **GDPR**: Data minimization and user consent
- **Local Regulations**: KYC/AML compliance integration
- **Audit Logging**: Comprehensive operation logging

## 📊 **Performance Optimizations**

### **SDK Performance**
- **Connection Pooling**: HTTP client connection reuse
- **Request Batching**: Batch multiple operations
- **Caching**: Response caching where appropriate
- **Compression**: Gzip compression for large payloads

### **Mobile Performance**
- **Lazy Loading**: Load screens on demand
- **Image Optimization**: Efficient trust badge rendering
- **Memory Management**: Proper cleanup of resources
- **Background Sync**: Efficient offline operation processing

## 🧪 **Testing Strategy**

### **SDK Testing**
- **Unit Tests**: Individual method testing
- **Integration Tests**: API endpoint testing
- **Error Scenarios**: Network failures, timeouts, invalid responses
- **Performance Tests**: Load testing with multiple concurrent requests

### **Mobile Testing**
- **Device Testing**: Multiple device and OS versions
- **Network Conditions**: Offline, slow, intermittent connectivity
- **Biometric Testing**: Various biometric authentication scenarios
- **Security Testing**: Penetration testing and vulnerability scanning

## 🎯 **Next Steps**

### **Immediate**
1. **Deploy SDKs**: Publish to npm and NuGet
2. **Mobile Testing**: Test on physical devices
3. **Security Audit**: Comprehensive security review
4. **Documentation**: API documentation and user guides

### **Short-term**
1. **Push Notifications**: Real-time status updates
2. **QR Payments**: Merchant-presented QR codes
3. **Deep Links**: Invoice and payment deep linking
4. **State Management**: XState for complex flows

### **Long-term**
1. **Web Portal**: Next.js operator dashboard
2. **Advanced Analytics**: Real-time dashboards
3. **Multi-language**: Internationalization support
4. **Accessibility**: WCAG compliance

## 📋 **Dependencies**

### **TypeScript SDK**
- `cross-fetch`: Universal fetch implementation
- `uuid`: UUID generation
- `typescript`: Type checking and compilation

### **C# SDK**
- `System.Net.Http.Json`: JSON HTTP client
- `Polly`: Resilience patterns
- `Microsoft.Extensions.Logging`: Structured logging

### **Mobile App**
- `expo`: React Native framework
- `expo-local-authentication`: Biometric authentication
- `expo-secure-store`: Secure credential storage
- `@react-navigation/native`: Navigation
- `@atlasbank/sdk`: AtlasBank TypeScript SDK

This implementation provides a production-ready foundation for AtlasBank's mobile and SDK ecosystem with comprehensive security, resilience, and offline-first capabilities.

