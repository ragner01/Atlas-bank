# 🏦 AtlasBank Mobile App - Modern Fintech UI

A **Tier-1 African fintech mobile banking experience** built with React Native and Expo, featuring modern UI/UX design inspired by Opay, Moniepoint, and PalmPay.

## ✨ Features

### 🎨 **Modern Design System**
- **Glassmorphism & Gradients**: Beautiful balance cards with gradient backgrounds
- **Curved Navigation**: Floating bottom tabs with smooth animations
- **Dark Mode**: Automatic theme switching with smooth transitions
- **Typography**: Consistent SF Pro Display/Poppins font system
- **Shadows & Elevation**: Subtle depth with proper shadow hierarchy

### ⚡ **Performance Optimized**
- **60fps Animations**: Smooth micro-interactions and transitions
- **Real-time Updates**: WebSocket integration for live balance updates
- **Local Caching**: AsyncStorage/SecureStore for offline functionality
- **Shimmer Loading**: Skeleton screens for better perceived performance
- **Optimized for Low-end Devices**: Efficient rendering and memory usage

### 🔐 **Security & Authentication**
- **Biometric Login**: Face ID/Touch ID integration
- **Secure Storage**: Encrypted local storage for sensitive data
- **PIN Protection**: 4-6 digit PIN validation
- **Session Management**: Automatic token refresh and logout

### 💸 **Banking Features**
- **Real-time Balance**: Live updates via SignalR WebSocket
- **Send Money**: Modern transfer interface with validation
- **Services Hub**: Organized bill payments and utilities
- **Quick Actions**: One-tap access to common functions
- **Transaction History**: Clean, categorized transaction list

## 🚀 Quick Start

### Prerequisites
- Node.js 18+
- Expo CLI (`npm install -g @expo/cli`)
- iOS Simulator (macOS) or Android Studio
- Physical device with Expo Go app (optional)

### Installation

```bash
# Clone the repository
cd apps/mobile/expo

# Install dependencies
npm install

# Start the development server
npm start

# Run on iOS simulator
npm run ios

# Run on Android emulator
npm run android
```

### Demo Credentials
- **Phone**: `2348100000001`
- **PIN**: `1234`

## 📱 Screens Overview

### 🏠 **Home Screen**
- **Balance Card**: Gradient card with real-time balance updates
- **Quick Actions**: Send, Cash In, Cash Out, Request Money
- **Services Grid**: 3-column layout with colorful icons
- **Recent Transactions**: Scrollable list with smooth animations
- **Connection Status**: Live/Offline indicator

### 💸 **Transfer Screen**
- **Floating Labels**: Modern input design with smooth animations
- **Amount Formatting**: Automatic currency formatting (₦)
- **Contact Selection**: Quick access to recent contacts
- **Confirmation Modal**: Bottom sheet with slide-up animation
- **Validation**: Real-time form validation with error states

### ⚙️ **Services Screen**
- **Featured Services**: Quick access to Airtime and Electricity
- **Expandable Categories**: Bills, Financial Services, Lifestyle
- **Service Cards**: Detailed service information with icons
- **Help Section**: Integrated support and contact options

### 🏦 **Savings Screen**
- **Account Overview**: Savings account information
- **Interest Rates**: Competitive rates display
- **Quick Actions**: Open account, view details

### 👤 **Profile Screen**
- **User Avatar**: Personalized profile with initials
- **Menu Sections**: Organized settings and preferences
- **Theme Toggle**: Dark/Light mode switching
- **Logout**: Secure session termination

## 🎨 Design System

### Colors
```typescript
const Colors = {
  primary: '#007BFF',      // Blue
  secondary: '#00C853',    // Green
  accent: '#FF6B35',       // Orange
  success: '#4CAF50',      // Success Green
  warning: '#FF9800',      // Warning Orange
  error: '#F44336',        // Error Red
  background: '#F8F9FA',   // Light Background
  surface: '#FFFFFF',      // Card Background
  text: '#212529',         // Primary Text
  textSecondary: '#6C757D', // Secondary Text
}
```

### Typography
- **H1**: 28px, Bold (Balance amounts)
- **H2**: 24px, Semi-bold (Screen titles)
- **H3**: 20px, Semi-bold (Section titles)
- **H4**: 18px, Semi-bold (Card titles)
- **Body1**: 16px, Regular (Main content)
- **Body2**: 14px, Regular (Secondary content)
- **Caption**: 12px, Regular (Labels, hints)

### Spacing
- **XS**: 4px (Tight spacing)
- **SM**: 8px (Small gaps)
- **MD**: 16px (Standard spacing)
- **LG**: 24px (Large spacing)
- **XL**: 32px (Extra large spacing)
- **XXL**: 48px (Section spacing)

### Border Radius
- **SM**: 8px (Small elements)
- **MD**: 12px (Buttons, inputs)
- **LG**: 16px (Cards, containers)
- **XL**: 20px (Large cards)
- **XXL**: 24px (Balance card)
- **Round**: 50px (Circular elements)

## 🔧 Technical Architecture

### State Management
- **React Context**: Theme and user state
- **Local State**: Component-level state with hooks
- **Secure Storage**: Encrypted local persistence

### Navigation
- **React Navigation**: Bottom tabs + stack navigation
- **Custom Tab Bar**: Animated floating tabs
- **Deep Linking**: URL-based navigation support

### Animations
- **Animated API**: Native driver for 60fps performance
- **Spring Animations**: Natural, bouncy interactions
- **Timing Animations**: Smooth transitions and easing
- **Interpolation**: Complex animation curves

### Real-time Features
- **WebSocket**: SignalR integration for live updates
- **Connection Management**: Auto-reconnection and error handling
- **State Synchronization**: Real-time balance updates

## 📦 Dependencies

### Core
- `react-native`: Mobile framework
- `expo`: Development platform
- `@react-navigation/*`: Navigation system

### UI & Animations
- `expo-linear-gradient`: Gradient backgrounds
- `expo-blur`: Glassmorphism effects
- `react-native-animated`: Smooth animations

### Security & Storage
- `expo-secure-store`: Encrypted storage
- `expo-local-authentication`: Biometric auth
- `expo-crypto`: Cryptographic functions

### Utilities
- `uuid`: Unique identifiers
- `expo-network`: Network status detection

## 🎯 Performance Optimizations

### Rendering
- **FlatList**: Efficient list rendering
- **Memoization**: React.memo for expensive components
- **Lazy Loading**: On-demand component loading
- **Image Optimization**: Compressed assets and caching

### Memory Management
- **Cleanup**: Proper useEffect cleanup
- **Ref Management**: Efficient ref usage
- **State Optimization**: Minimal re-renders

### Network
- **Request Batching**: Grouped API calls
- **Caching Strategy**: Smart cache invalidation
- **Offline Support**: Local data persistence

## 🧪 Testing

### Unit Tests
```bash
npm test
```

### E2E Tests
```bash
# Install Detox (iOS)
npm install -g detox-cli
detox build
detox test
```

### Manual Testing
- **iOS Simulator**: Test on various device sizes
- **Android Emulator**: Test on different Android versions
- **Physical Devices**: Real device testing with Expo Go

## 🚀 Deployment

### Development Build
```bash
expo build:ios
expo build:android
```

### Production Build
```bash
expo build:ios --release-channel production
expo build:android --release-channel production
```

### App Store Submission
```bash
expo upload:ios
expo upload:android
```

## 🔒 Security Considerations

### Data Protection
- **Encryption**: All sensitive data encrypted at rest
- **Secure Transmission**: HTTPS/WSS for all network calls
- **PIN Security**: Secure PIN storage and validation
- **Session Management**: Automatic logout and token refresh

### Authentication
- **Biometric Integration**: Face ID/Touch ID support
- **Multi-factor**: PIN + biometric authentication
- **Session Timeout**: Automatic session expiration
- **Secure Storage**: Keychain/Keystore integration

## 📊 Analytics & Monitoring

### Performance Metrics
- **App Launch Time**: Cold and warm start times
- **Screen Load Times**: Individual screen performance
- **Animation FPS**: Smooth 60fps animations
- **Memory Usage**: Efficient memory management

### User Analytics
- **Screen Views**: User navigation patterns
- **Feature Usage**: Most used banking features
- **Error Tracking**: Crash and error monitoring
- **Performance Monitoring**: Real-time performance data

## 🤝 Contributing

### Code Style
- **ESLint**: Enforced code quality
- **Prettier**: Consistent code formatting
- **TypeScript**: Type safety and documentation
- **Conventional Commits**: Standardized commit messages

### Development Workflow
1. **Feature Branch**: Create feature branch from main
2. **Development**: Implement feature with tests
3. **Code Review**: Peer review and approval
4. **Testing**: Comprehensive testing on devices
5. **Merge**: Merge to main branch

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **Design Inspiration**: Opay, Moniepoint, PalmPay, Kuda
- **UI Libraries**: React Navigation, Expo, Linear Gradient
- **Icons**: Emoji-based icons for universal compatibility
- **Community**: React Native and Expo communities

---

**Built with ❤️ for modern African fintech experiences**
