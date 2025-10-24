import { Toaster } from "@/components/ui/toaster";
import { Toaster as Sonner } from "@/components/ui/sonner";
import { TooltipProvider } from "@/components/ui/tooltip";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { BrowserRouter, Routes, Route, Navigate } from "react-router-dom";
import { AuthProvider, useAuth } from "@/hooks/useAuth";
import { ErrorBoundary } from "@/components/ErrorBoundary";
import { Layout } from "./components/Layout";
import Home from "./pages/Home";
import Login from "./pages/Login";
import Transfer from "./pages/Transfer";
import Bills from "./pages/Bills";
import Cards from "./pages/Cards";
import Savings from "./pages/Savings";
import Transactions from "./pages/Transactions";
import Profile from "./pages/Profile";
import Loans from "./pages/Loans";
import QRPayment from "./pages/QRPayment";
import Notifications from "./pages/Notifications";
import RequestMoney from "./pages/RequestMoney";
import AgentCashIn from "./pages/AgentCashIn";
import AgentCashOut from "./pages/AgentCashOut";
import OfflineSync from "./pages/OfflineSync";
import NotFound from "./pages/NotFound";

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      retry: (failureCount, error: any) => {
        // Don't retry on 4xx errors (client errors)
        if (error?.statusCode && error.statusCode >= 400 && error.statusCode < 500) {
          return false;
        }
        // Retry up to 3 times for other errors
        return failureCount < 3;
      },
      refetchOnWindowFocus: false,
      staleTime: 5 * 60 * 1000, // 5 minutes
    },
    mutations: {
      retry: (failureCount, error: any) => {
        // Don't retry mutations on client errors
        if (error?.statusCode && error.statusCode >= 400 && error.statusCode < 500) {
          return false;
        }
        // Retry once for network errors
        return failureCount < 1;
      },
    },
  },
});

// Protected Route Component
const ProtectedRoute = ({ children }: { children: React.ReactNode }) => {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
      </div>
    );
  }

  return isAuthenticated ? <>{children}</> : <Navigate to="/login" replace />;
};

// Public Route Component (redirects to home if authenticated)
const PublicRoute = ({ children }: { children: React.ReactNode }) => {
  const { isAuthenticated, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
      </div>
    );
  }

  return isAuthenticated ? <Navigate to="/" replace /> : <>{children}</>;
};

const AppRoutes = () => (
  <Routes>
    <Route path="/login" element={
      <PublicRoute>
        <Login />
      </PublicRoute>
    } />
    <Route path="/" element={
      <ProtectedRoute>
        <Layout>
          <Home />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/transfer" element={
      <ProtectedRoute>
        <Layout>
          <Transfer />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/bills" element={
      <ProtectedRoute>
        <Layout>
          <Bills />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/cards" element={
      <ProtectedRoute>
        <Layout>
          <Cards />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/savings" element={
      <ProtectedRoute>
        <Layout>
          <Savings />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/transactions" element={
      <ProtectedRoute>
        <Layout>
          <Transactions />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/profile" element={
      <ProtectedRoute>
        <Layout>
          <Profile />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/loans" element={
      <ProtectedRoute>
        <Layout>
          <Loans />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/qr" element={
      <ProtectedRoute>
        <Layout>
          <QRPayment />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/notifications" element={
      <ProtectedRoute>
        <Layout>
          <Notifications />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/request" element={
      <ProtectedRoute>
        <Layout>
          <RequestMoney />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/agent/cashin" element={
      <ProtectedRoute>
        <Layout>
          <AgentCashIn />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/agent/cashout" element={
      <ProtectedRoute>
        <Layout>
          <AgentCashOut />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="/offline/sync" element={
      <ProtectedRoute>
        <Layout>
          <OfflineSync />
        </Layout>
      </ProtectedRoute>
    } />
    <Route path="*" element={<NotFound />} />
  </Routes>
);

const App = () => (
  <ErrorBoundary>
    <QueryClientProvider client={queryClient}>
      <AuthProvider>
        <TooltipProvider>
          <Toaster />
          <Sonner />
          <BrowserRouter>
            <AppRoutes />
          </BrowserRouter>
        </TooltipProvider>
      </AuthProvider>
    </QueryClientProvider>
  </ErrorBoundary>
);

export default App;
