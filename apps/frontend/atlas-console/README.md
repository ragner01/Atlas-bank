# AtlasBank Console Frontend

A modern, responsive React-based banking console built with TypeScript, Vite, and shadcn/ui components. This frontend provides a beautiful user interface for all AtlasBank services including transfers, cards, loans, and account management.

## 🚀 Features

### ✨ **Modern UI/UX**
- **Responsive Design**: Works seamlessly on desktop, tablet, and mobile devices
- **Dark/Light Theme**: Built-in theme switching with system preference detection
- **Accessibility**: WCAG compliant with keyboard navigation and screen reader support
- **Animations**: Smooth transitions and micro-interactions for better user experience

### 🔐 **Authentication & Security**
- **JWT-based Authentication**: Secure token-based authentication
- **Protected Routes**: Automatic redirection for unauthenticated users
- **Input Validation**: Client-side validation with Zod schemas
- **Secure Storage**: Encrypted token storage with automatic expiration

### 💰 **Banking Features**
- **Real-time Balance**: Live balance updates from Ledger API
- **Transfer Money**: Bank transfers and wallet-to-wallet transfers
- **Transaction History**: Complete transaction history with filtering
- **Account Management**: Profile management and settings
- **Cards Management**: Virtual and physical card management
- **Loans**: Apply for and manage loans
- **Bills Payment**: Pay utilities and other bills

### 🔧 **Technical Features**
- **TypeScript**: Full type safety throughout the application
- **React Query**: Efficient data fetching and caching
- **Form Handling**: React Hook Form with validation
- **Error Handling**: Comprehensive error boundaries and user feedback
- **Loading States**: Skeleton loaders and progress indicators
- **Offline Support**: Graceful degradation when offline

## 🛠️ Technology Stack

- **Frontend Framework**: React 18 with TypeScript
- **Build Tool**: Vite for fast development and optimized builds
- **UI Components**: shadcn/ui with Radix UI primitives
- **Styling**: Tailwind CSS with custom design system
- **State Management**: React Query for server state, React hooks for local state
- **Routing**: React Router v6 with protected routes
- **Forms**: React Hook Form with Zod validation
- **HTTP Client**: Axios with interceptors for authentication
- **Icons**: Lucide React for consistent iconography
- **Notifications**: Sonner for toast notifications

## 📁 Project Structure

```
apps/frontend/atlas-console/
├── src/
│   ├── components/          # Reusable UI components
│   │   ├── ui/             # shadcn/ui components
│   │   └── Layout.tsx      # Main layout component
│   ├── hooks/              # Custom React hooks
│   │   └── useAuth.tsx     # Authentication hook
│   ├── lib/                # Utility libraries
│   │   └── api.ts          # API service layer
│   ├── pages/              # Page components
│   │   ├── Home.tsx        # Dashboard/home page
│   │   ├── Login.tsx       # Authentication page
│   │   ├── Transfer.tsx    # Money transfer page
│   │   ├── Cards.tsx       # Card management
│   │   ├── Loans.tsx       # Loan management
│   │   └── ...             # Other pages
│   ├── App.tsx             # Main app component
│   └── main.tsx            # App entry point
├── public/                 # Static assets
├── Dockerfile              # Production Docker image
├── nginx.conf              # Nginx configuration
├── package.json            # Dependencies and scripts
└── vite.config.ts          # Vite configuration
```

## 🚀 Getting Started

### Prerequisites

- **Node.js**: v18 or higher
- **npm**: Latest version
- **AtlasBank Services**: Backend services must be running

### Installation

1. **Install Dependencies**:
   ```bash
   cd apps/frontend/atlas-console
   npm install
   ```

2. **Environment Configuration**:
   ```bash
   cp env.example .env
   # Edit .env with your API endpoints
   ```

3. **Start Development Server**:
   ```bash
npm run dev
```

4. **Access the Application**:
   - Open http://localhost:5173 in your browser
   - Use demo credentials: Phone: `2348100000001`, PIN: `1234`

### Using Make Commands

```bash
# Start frontend in development mode
make console-dev

# Build for production
make console-build

# Run tests and linting
make console-test
```

## 🔌 API Integration

The frontend integrates with multiple AtlasBank backend services:

### **Services Connected**
- **Payments API** (`:5191`): Transfer money, validate accounts
- **Ledger API** (`:5190`): Account balances, transaction history
- **Trust Portal** (`:5802`): Trust scores and badges
- **Cards Vault** (`:5192`): Card management
- **Loans API** (`:5193`): Loan applications and management
- **Agent Network** (`:5621`): Agent cash-in/cash-out
- **Offline Queue** (`:5622`): Offline transaction queuing

### **API Service Layer**

The `src/lib/api.ts` file provides a comprehensive service layer:

```typescript
// Authentication
AuthService.login(msisdn, pin)
AuthService.logout()

// Ledger operations
LedgerService.getAccountBalance(accountId)
LedgerService.getAccountTransactions(accountId)

// Payment operations
PaymentsService.transferMoney(transferRequest)
PaymentsService.validateAccount(accountNumber, bankCode)

// Card operations
CardsService.getCards()
CardsService.createCard()

// Loan operations
LoansService.getLoans()
LoansService.applyForLoan(productId, amount)
```

## 🎨 Design System

### **Color Palette**
- **Primary**: Blue (#3B82F6) - Main brand color
- **Secondary**: Gray (#F8FAFC) - Background colors
- **Success**: Green (#10B981) - Success states
- **Destructive**: Red (#EF4444) - Error states
- **Warning**: Yellow (#F59E0B) - Warning states

### **Typography**
- **Font Family**: Inter (system font fallback)
- **Headings**: Font weights 600-700
- **Body**: Font weight 400
- **Captions**: Font weight 500

### **Spacing**
- **Base Unit**: 4px
- **Common Spacing**: 4px, 8px, 12px, 16px, 24px, 32px, 48px, 64px

## 🔒 Security Features

### **Authentication**
- JWT tokens stored in secure HTTP-only cookies
- Automatic token refresh
- Session timeout handling
- Logout on token expiration

### **Input Validation**
- Client-side validation with Zod schemas
- Server-side validation feedback
- XSS protection
- CSRF protection

### **Data Protection**
- Sensitive data masking (account numbers, card numbers)
- Secure API communication (HTTPS)
- No sensitive data in localStorage
- Automatic session cleanup

## 📱 Responsive Design

### **Breakpoints**
- **Mobile**: < 768px
- **Tablet**: 768px - 1024px
- **Desktop**: > 1024px

### **Mobile-First Approach**
- Touch-friendly interface
- Optimized for thumb navigation
- Swipe gestures support
- Mobile-specific layouts

## 🧪 Testing

### **Available Tests**
```bash
# Type checking
npm run type-check

# Linting
npm run lint

# Build verification
npm run build
```

### **Testing Strategy**
- **Unit Tests**: Component testing with React Testing Library
- **Integration Tests**: API integration testing
- **E2E Tests**: Full user journey testing
- **Accessibility Tests**: WCAG compliance testing

## 🚀 Deployment

### **Production Build**
```bash
npm run build
```

### **Docker Deployment**
```bash
# Build Docker image
docker build -t atlasbank-console .

# Run container
docker run -p 3000:3000 atlasbank-console
```

### **Environment Variables**
```bash
# API Endpoints
VITE_PAYMENTS_API_URL=https://api.atlasbank.com/payments
VITE_LEDGER_API_URL=https://api.atlasbank.com/ledger
VITE_TRUST_API_URL=https://api.atlasbank.com/trust

# App Configuration
VITE_APP_NAME=AtlasBank Console
VITE_DEFAULT_CURRENCY=NGN
VITE_MAX_TRANSFER_AMOUNT=1000000
```

## 🔧 Development

### **Code Style**
- **ESLint**: Configured with React and TypeScript rules
- **Prettier**: Code formatting
- **Husky**: Pre-commit hooks
- **Conventional Commits**: Standardized commit messages

### **Performance Optimization**
- **Code Splitting**: Route-based code splitting
- **Lazy Loading**: Component lazy loading
- **Image Optimization**: WebP format with fallbacks
- **Bundle Analysis**: Webpack bundle analyzer

### **Accessibility**
- **ARIA Labels**: Proper ARIA attributes
- **Keyboard Navigation**: Full keyboard support
- **Screen Reader**: Optimized for screen readers
- **Color Contrast**: WCAG AA compliance

## 📊 Monitoring & Analytics

### **Error Tracking**
- Error boundaries for graceful error handling
- Console error logging
- User feedback collection

### **Performance Monitoring**
- Core Web Vitals tracking
- Bundle size monitoring
- API response time tracking

## 🤝 Contributing

### **Development Workflow**
1. Create feature branch from `main`
2. Make changes with proper TypeScript types
3. Add tests for new functionality
4. Run linting and type checking
5. Submit pull request

### **Code Standards**
- Use TypeScript for all new code
- Follow React best practices
- Write meaningful commit messages
- Add JSDoc comments for complex functions

## 📚 Documentation

### **Component Documentation**
- Storybook integration for component documentation
- Props documentation with TypeScript
- Usage examples and best practices

### **API Documentation**
- OpenAPI/Swagger integration
- Request/response examples
- Error code documentation

## 🆘 Troubleshooting

### **Common Issues**

1. **Build Errors**:
   ```bash
   # Clear node_modules and reinstall
   rm -rf node_modules package-lock.json
   npm install
   ```

2. **API Connection Issues**:
   - Check if backend services are running
   - Verify API URLs in `.env` file
   - Check network connectivity

3. **Authentication Issues**:
   - Clear browser cookies
   - Check JWT token expiration
   - Verify authentication endpoints

### **Debug Mode**
```bash
# Enable debug logging
VITE_DEBUG_MODE=true npm run dev
```

## 📈 Roadmap

### **Upcoming Features**
- [ ] **Biometric Authentication**: Fingerprint/Face ID support
- [ ] **Push Notifications**: Real-time transaction alerts
- [ ] **Multi-language Support**: Internationalization
- [ ] **Advanced Analytics**: Spending insights and reports
- [ ] **Social Features**: Split bills and group payments
- [ ] **Investment Tools**: Portfolio management
- [ ] **Insurance**: Policy management and claims

### **Technical Improvements**
- [ ] **PWA Support**: Progressive Web App capabilities
- [ ] **Offline Mode**: Enhanced offline functionality
- [ ] **Performance**: Further optimization and caching
- [ ] **Testing**: Comprehensive test coverage
- [ ] **Documentation**: Enhanced developer documentation

---

**AtlasBank Console Frontend** - Modern banking made simple, secure, and beautiful. 🏦✨