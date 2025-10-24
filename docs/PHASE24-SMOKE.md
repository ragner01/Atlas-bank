# Phase 24 Smoke Tests

## 🧪 **Testing Checklist**

### **Prerequisites**
- [ ] AtlasBank services running (Payments, Ledger, Trust Portal, Offline Queue, Agent Network)
- [ ] TypeScript SDK built (`cd sdks/typescript && npm i && npm run build`)
- [ ] C# SDK built (`cd sdks/csharp && dotnet build`)
- [ ] Mobile app dependencies installed (`cd apps/mobile/expo && npm i`)

## 📱 **Mobile App Testing**

### **1. Build and Start Mobile App**
```bash
cd apps/mobile/expo
cp env.example .env
npm run start
```

### **2. Login Flow**
- [ ] **Valid Login**: Use MSISDN `2348100000001` and any 4-6 digit PIN
- [ ] **Biometric Setup**: Enable Face ID/Touch ID if available
- [ ] **Device Registration**: Verify device ID is generated and stored
- [ ] **Secure Storage**: Confirm credentials are stored securely

### **3. Send Money Flow**
- [ ] **Online Transfer**: Send money with valid account ID
- [ ] **Amount Validation**: Test with invalid amounts (negative, zero, non-numeric)
- [ ] **Account Validation**: Test with invalid account IDs
- [ ] **Offline Fallback**: Disable network and verify offline queuing
- [ ] **Sync Process**: Re-enable network and sync offline operations

### **4. Agent Operations**
- [ ] **Cash-in Intent**: Create cash-in request with agent code
- [ ] **Cash-out Intent**: Create cash-out request with agent code
- [ ] **Agent Validation**: Test with invalid agent codes
- [ ] **Amount Limits**: Test with amounts exceeding limits

### **5. Trust Badge**
- [ ] **Badge Display**: Verify trust badge renders in home screen
- [ ] **Badge URL**: Confirm correct entity ID in badge URL
- [ ] **Network Error**: Test badge loading with network issues

### **6. Settings and Security**
- [ ] **PIN Change**: Update PIN and verify secure storage
- [ ] **USSD Info**: Display USSD fallback information
- [ ] **Biometric Re-auth**: Test biometric re-authentication

## 🔧 **TypeScript SDK Testing**

### **1. Basic Operations**
```typescript
import { AtlasClient, validateMsisdn, validateAccountId } from '@atlasbank/sdk';

const client = new AtlasClient({
  baseUrl: 'http://localhost:5191',
  tenantId: 'tnt_demo',
  timeout: 30000
});

// Test validation functions
console.log(validateMsisdn('+2348100000001')); // true
console.log(validateAccountId('msisdn::2348100000001')); // true
```

### **2. Transfer Operations**
```typescript
// Test transfer with risk assessment
const transferResult = await client.transferWithRisk({
  SourceAccountId: 'msisdn::2348100000001',
  DestinationAccountId: 'msisdn::2348100000002',
  Minor: 25000,
  Currency: 'NGN',
  Narration: 'Test transfer'
});
console.log('Transfer result:', transferResult);
```

### **3. Card Charge Operations**
```typescript
// Test card charge with limits enforcement
const chargeResult = await client.chargeCardEnforced({
  amountMinor: 10000,
  currency: 'NGN',
  cardToken: 'card_token_123',
  merchantId: 'merchant_001',
  mcc: '5411',
  deviceId: 'device_123',
  ip: '192.168.1.1'
});
console.log('Charge result:', chargeResult);
```

### **4. Balance Queries**
```typescript
// Test balance retrieval
const balanceResult = await client.getBalance('msisdn::2348100000001', 'NGN');
console.log('Balance:', balanceResult);
```

### **5. Offline Operations**
```typescript
// Test offline operation queuing
const offlineOp = await client.offlineEnqueue({
  tenantId: 'tnt_demo',
  deviceId: 'device_123',
  kind: 'transfer',
  payload: { source: 'msisdn::2348100000001', dest: 'msisdn::2348100000002', minor: 1000 },
  nonce: 'nonce_123',
  signature: 'signature_123'
});
console.log('Offline operation:', offlineOp);

// Test offline sync
const syncResult = await client.offlineSync('device_123', 20);
console.log('Sync result:', syncResult);
```

### **6. Error Handling**
```typescript
try {
  await client.transferWithRisk({
    SourceAccountId: 'invalid_account',
    DestinationAccountId: 'msisdn::2348100000002',
    Minor: -100, // Invalid amount
    Currency: 'NGN'
  });
} catch (error) {
  if (error instanceof AtlasApiError) {
    console.log('API Error:', error.status, error.code, error.message);
  }
}
```

## 🔧 **C# SDK Testing**

### **1. Basic Operations**
```csharp
using AtlasBank.Sdk;

var client = new AtlasClient("http://localhost:5191", "tnt_demo");

// Test transfer
var transferResult = await client.TransferWithRisk<ApiResponse<object>>(new TransferRequest
{
    SourceAccountId = "msisdn::2348100000001",
    DestinationAccountId = "msisdn::2348100000002",
    Minor = 25000,
    Currency = "NGN",
    Narration = "Test transfer"
});
Console.WriteLine($"Transfer result: {transferResult}");
```

### **2. Card Charge Operations**
```csharp
var chargeResult = await client.ChargeCardEnforced<ApiResponse<object>>(new CardChargeRequest
{
    AmountMinor = 10000,
    Currency = "NGN",
    CardToken = "card_token_123",
    MerchantId = "merchant_001",
    Mcc = "5411",
    DeviceId = "device_123",
    Ip = "192.168.1.1"
});
Console.WriteLine($"Charge result: {chargeResult}");
```

### **3. Balance Queries**
```csharp
var balanceResult = await client.GetBalance<ApiResponse<object>>("msisdn::2348100000001", "NGN");
Console.WriteLine($"Balance: {balanceResult}");
```

### **4. Error Handling**
```csharp
try
{
    await client.TransferWithRisk<ApiResponse<object>>(new TransferRequest
    {
        SourceAccountId = "invalid_account",
        DestinationAccountId = "msisdn::2348100000002",
        Minor = -100, // Invalid amount
        Currency = "NGN"
    });
}
catch (AtlasApiException ex)
{
    Console.WriteLine($"API Error: {ex.StatusCode} {ex.Code} {ex.Message}");
}
```

## 🌐 **Integration Testing**

### **1. End-to-End Transfer Flow**
1. **Mobile App**: Login with test credentials
2. **Send Money**: Transfer from `msisdn::2348100000001` to `msisdn::2348100000002`
3. **Verify**: Check balance changes in both accounts
4. **Offline Test**: Disable network and queue another transfer
5. **Sync**: Re-enable network and sync offline operations

### **2. Agent Network Integration**
1. **Cash-in**: Create cash-in intent with agent `AG001`
2. **Agent Confirmation**: Confirm cash-in in agent service
3. **Balance Check**: Verify account balance increased
4. **Cash-out**: Create cash-out intent
5. **Agent Confirmation**: Confirm cash-out in agent service

### **3. Trust Badge Integration**
1. **Badge Display**: Verify trust badge loads in mobile app
2. **Entity ID**: Confirm correct entity ID in badge URL
3. **Trust Score**: Verify trust score updates reflect in badge
4. **Network Error**: Test badge loading with network issues

### **4. Offline Queue Integration**
1. **Queue Operations**: Queue multiple operations while offline
2. **Sync Process**: Sync all queued operations when online
3. **Order Preservation**: Verify operations process in correct order
4. **Error Handling**: Test with invalid operations in queue

## 🔍 **Security Testing**

### **1. Input Validation**
- [ ] **MSISDN Validation**: Test with invalid phone numbers
- [ ] **Account ID Validation**: Test with invalid account formats
- [ ] **Amount Validation**: Test with negative, zero, non-numeric amounts
- [ ] **PIN Validation**: Test with invalid PIN formats

### **2. Authentication**
- [ ] **Biometric Auth**: Test Face ID/Touch ID authentication
- [ ] **PIN Fallback**: Test PIN fallback when biometrics fail
- [ ] **Session Management**: Test session timeout and renewal
- [ ] **Device Binding**: Verify device ID binding

### **3. Network Security**
- [ ] **HTTPS**: Verify all API calls use HTTPS in production
- [ ] **Certificate Pinning**: Test certificate pinning (if implemented)
- [ ] **Request Signing**: Verify HMAC signatures for offline operations
- [ ] **Rate Limiting**: Test rate limiting on API endpoints

### **4. Data Protection**
- [ ] **Secure Storage**: Verify credentials stored securely
- [ ] **Data Encryption**: Test data encryption in transit and at rest
- [ ] **PII Masking**: Verify PII masking in logs
- [ ] **Data Retention**: Test data retention policies

## 📊 **Performance Testing**

### **1. SDK Performance**
- [ ] **Response Times**: Measure API response times
- [ ] **Concurrent Requests**: Test multiple concurrent requests
- [ ] **Memory Usage**: Monitor memory usage during operations
- [ ] **Retry Logic**: Test retry behavior under load

### **2. Mobile App Performance**
- [ ] **App Launch**: Measure app launch time
- [ ] **Screen Transitions**: Test smooth screen transitions
- [ ] **Memory Management**: Monitor memory usage
- [ ] **Battery Usage**: Test battery impact

### **3. Network Performance**
- [ ] **Slow Network**: Test behavior on slow networks
- [ ] **Intermittent Network**: Test with intermittent connectivity
- [ ] **Offline Mode**: Test offline operation queuing
- [ ] **Sync Performance**: Measure sync operation performance

## 🐛 **Error Scenarios**

### **1. Network Errors**
- [ ] **Connection Timeout**: Test request timeout handling
- [ ] **Network Unavailable**: Test offline fallback
- [ ] **Server Error**: Test 5xx error handling
- [ ] **Rate Limiting**: Test 429 error handling

### **2. Validation Errors**
- [ ] **Invalid Input**: Test invalid input handling
- [ ] **Missing Fields**: Test missing required fields
- [ ] **Format Errors**: Test format validation errors
- [ ] **Business Rules**: Test business rule violations

### **3. Authentication Errors**
- [ ] **Invalid Credentials**: Test invalid login
- [ ] **Session Expired**: Test session expiration
- [ ] **Biometric Failure**: Test biometric authentication failure
- [ ] **Device Binding**: Test device binding errors

## ✅ **Success Criteria**

### **Mobile App**
- [ ] All screens load without errors
- [ ] Login flow works with biometrics and PIN
- [ ] Transfer operations work online and offline
- [ ] Agent operations create proper intents
- [ ] Trust badges display correctly
- [ ] Offline sync processes queued operations
- [ ] Error messages are user-friendly
- [ ] App handles network failures gracefully

### **TypeScript SDK**
- [ ] All API methods work correctly
- [ ] Input validation prevents invalid requests
- [ ] Error handling provides meaningful messages
- [ ] Retry logic handles transient failures
- [ ] Offline operations queue correctly
- [ ] HMAC signing works for offline operations
- [ ] Type safety prevents runtime errors

### **C# SDK**
- [ ] All API methods work correctly
- [ ] Resilience policies handle failures
- [ ] Configuration validation works
- [ ] Logging provides useful information
- [ ] Error handling provides detailed information
- [ ] Performance meets requirements
- [ ] Memory usage is reasonable

## 🚀 **Deployment Checklist**

### **SDK Deployment**
- [ ] **TypeScript SDK**: Publish to npm registry
- [ ] **C# SDK**: Publish to NuGet registry
- [ ] **Documentation**: Update API documentation
- [ ] **Versioning**: Follow semantic versioning
- [ ] **Changelog**: Update changelog with new features

### **Mobile App Deployment**
- [ ] **App Store**: Submit to iOS App Store
- [ ] **Play Store**: Submit to Google Play Store
- [ ] **Code Signing**: Configure code signing
- [ ] **App Icons**: Add proper app icons
- [ ] **Splash Screen**: Configure splash screen
- [ ] **Permissions**: Configure required permissions

### **Production Configuration**
- [ ] **API Endpoints**: Update to production URLs
- [ ] **Security Keys**: Use production security keys
- [ ] **Monitoring**: Configure error monitoring
- [ ] **Analytics**: Configure usage analytics
- [ ] **Crash Reporting**: Configure crash reporting

This comprehensive smoke test ensures all Phase 24 components work correctly together and meet production requirements.

