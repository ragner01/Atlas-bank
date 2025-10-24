import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Eye, EyeOff, Send, Receipt, Smartphone, Lightbulb, Tv, Wifi, Plus, QrCode, HandCoins, Bell, CreditCard as CreditCardIcon, LogOut } from "lucide-react";
import { useState, useEffect } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "@/hooks/useAuth";
import { useQuery } from "@tanstack/react-query";
import { LedgerService, formatCurrency, formatDate, AtlasClient, TrustService } from "@/lib/api";
import { toast } from "sonner";
import { OfflineIndicator } from "@/hooks/useNetworkStatus";

export default function Home() {
  const [showBalance, setShowBalance] = useState(true);
  const { user, logout } = useAuth();
  
  // Initialize AtlasClient - Matching Phase 24 mobile app
  const client = new AtlasClient();
  
  // Get account ID from user's MSISDN
  const accountId = user ? `msisdn::${user.msisdn}` : '';

  // Fetch account balance
  const { data: balance, isLoading: balanceLoading, error: balanceError } = useQuery({
    queryKey: ['balance', accountId],
    queryFn: () => LedgerService.getAccountBalance(accountId),
    enabled: !!accountId,
    refetchInterval: 30000, // Refetch every 30 seconds
  });

  // Fetch recent transactions
  const { data: transactions, isLoading: transactionsLoading } = useQuery({
    queryKey: ['transactions', accountId],
    queryFn: () => LedgerService.getAccountTransactions(accountId, 5),
    enabled: !!accountId,
    refetchInterval: 60000, // Refetch every minute
  });

  // Get trust badge URL - Matching Phase 24 mobile app
  const trustBadgeUrl = accountId ? client.trustBadgeUrl(accountId) : '';

  const quickActions = [
    { icon: Send, label: "Transfer", path: "/transfer", color: "bg-primary" },
    { icon: Receipt, label: "Pay Bills", path: "/bills", color: "bg-blue-500" },
    { icon: QrCode, label: "QR Pay", path: "/qr", color: "bg-purple-500" },
    { icon: HandCoins, label: "Request", path: "/request", color: "bg-orange-500" },
  ];

  const handleLogout = async () => {
    try {
      await logout();
      toast.success("Logged out successfully");
    } catch (error) {
      toast.error("Logout failed");
    }
  };

  const getDisplayName = () => {
    if (!user) return "User";
    return `${user.firstName} ${user.lastName}`;
  };

  const getInitials = () => {
    if (!user) return "U";
    return `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`;
  };

  return (
    <div className="min-h-screen bg-secondary">
      {/* Offline Indicator - Matching Phase 24 mobile app */}
      <OfflineIndicator />
      
      {/* Header */}
      <div className="bg-primary text-primary-foreground px-4 pt-8 pb-20 rounded-b-[2rem]">
        <div className="flex justify-between items-center mb-6">
          <div>
            <p className="text-sm opacity-90">Good {new Date().getHours() < 12 ? 'morning' : new Date().getHours() < 18 ? 'afternoon' : 'evening'}</p>
            <h1 className="text-2xl font-bold">{getDisplayName()}</h1>
          </div>
          <div className="flex gap-2">
            <Link to="/notifications">
              <div className="w-10 h-10 bg-white/20 rounded-full flex items-center justify-center relative">
                <Bell size={20} />
                <div className="absolute -top-1 -right-1 w-5 h-5 bg-destructive rounded-full flex items-center justify-center">
                  <span className="text-xs font-bold">3</span>
                </div>
              </div>
            </Link>
            <div className="w-10 h-10 bg-white/20 rounded-full flex items-center justify-center">
              <span className="text-lg font-bold">{getInitials()}</span>
            </div>
            <Button
              variant="ghost"
              size="icon"
              onClick={handleLogout}
              className="text-white hover:bg-white/20"
            >
              <LogOut size={20} />
            </Button>
          </div>
        </div>

        {/* Balance Card */}
        <Card className="bg-white shadow-xl">
          <CardContent className="p-6">
            <div className="flex justify-between items-start mb-4">
              <div>
                <p className="text-sm text-muted-foreground mb-1">Total Balance</p>
                <h2 className="text-3xl font-bold text-foreground">
                  {balanceLoading ? (
                    <div className="animate-pulse bg-gray-200 h-8 w-32 rounded"></div>
                  ) : balanceError ? (
                    "Error loading balance"
                  ) : showBalance ? (
                    formatCurrency(balance || 0)
                  ) : (
                    "₦•••••••"
                  )}
                </h2>
              </div>
              <button
                onClick={() => setShowBalance(!showBalance)}
                className="p-2 hover:bg-secondary rounded-full transition-colors"
              >
                {showBalance ? <Eye size={20} /> : <EyeOff size={20} />}
              </button>
            </div>
            <div className="flex gap-2">
              <span className="text-xs bg-success/10 text-success px-3 py-1 rounded-full font-medium">
                Account: {user?.msisdn || 'N/A'}
              </span>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Trust Badge - Matching Phase 24 mobile app */}
      {trustBadgeUrl && (
        <div className="px-4 -mt-12 mb-6">
          <div className="flex justify-center">
            <img 
              src={trustBadgeUrl} 
              alt="Trust Badge" 
              className="h-10 w-auto"
              onError={(e) => {
                // Hide badge if it fails to load
                e.currentTarget.style.display = 'none';
              }}
            />
          </div>
        </div>
      )}

      {/* Quick Actions */}
      <div className="px-4 mb-6">
        <div className="grid grid-cols-4 gap-3">
          {quickActions.map((action) => {
            const Icon = action.icon;
            return (
              <Link key={action.label} to={action.path}>
                <div className="flex flex-col items-center gap-2">
                  <div className={`${action.color} w-14 h-14 rounded-2xl flex items-center justify-center shadow-lg`}>
                    <Icon size={24} className="text-white" />
                  </div>
                  <span className="text-xs font-medium text-center">{action.label}</span>
                </div>
              </Link>
            );
          })}
        </div>
      </div>

      {/* Services */}
      <div className="px-4 mb-6">
        <h3 className="text-lg font-semibold mb-3">Services</h3>
        <div className="grid grid-cols-4 gap-4">
          {[
            { icon: Smartphone, label: "Airtime", path: "/bills", color: "text-blue-600" },
            { icon: Lightbulb, label: "Electricity", path: "/bills", color: "text-yellow-600" },
            { icon: HandCoins, label: "Loans", path: "/loans", color: "text-purple-600" },
            { icon: CreditCardIcon, label: "Cards", path: "/cards", color: "text-green-600" },
          ].map((service) => {
            const Icon = service.icon;
            return (
              <Link key={service.label} to={service.path}>
                <div className="flex flex-col items-center gap-2 p-3 bg-card rounded-xl hover:shadow-md transition-shadow">
                  <Icon size={28} className={service.color} />
                  <span className="text-xs text-center">{service.label}</span>
                </div>
              </Link>
            );
          })}
        </div>
      </div>

      {/* Recent Transactions */}
      <div className="px-4">
        <div className="flex justify-between items-center mb-3">
          <h3 className="text-lg font-semibold">Recent Transactions</h3>
          <Link to="/transactions" className="text-sm text-primary font-medium">
            See all
          </Link>
        </div>
        <Card>
          <CardContent className="p-0">
            {transactionsLoading ? (
              <div className="p-4 space-y-3">
                {[1, 2, 3].map((i) => (
                  <div key={i} className="flex items-center gap-3">
                    <div className="w-10 h-10 bg-gray-200 rounded-full animate-pulse"></div>
                    <div className="flex-1 space-y-2">
                      <div className="h-4 bg-gray-200 rounded animate-pulse"></div>
                      <div className="h-3 bg-gray-200 rounded w-1/2 animate-pulse"></div>
                    </div>
                    <div className="h-4 bg-gray-200 rounded w-16 animate-pulse"></div>
                  </div>
                ))}
              </div>
            ) : transactions && transactions.length > 0 ? (
              transactions.map((transaction, index) => (
                <div
                  key={transaction.id}
                  className={`flex items-center justify-between p-4 ${
                    index !== transactions.length - 1 ? "border-b border-border" : ""
                  }`}
                >
                  <div className="flex items-center gap-3">
                    <div
                      className={`w-10 h-10 rounded-full flex items-center justify-center ${
                        transaction.type === "credit" ? "bg-success/10" : "bg-destructive/10"
                      }`}
                    >
                      <Send
                        size={18}
                        className={transaction.type === "credit" ? "text-success rotate-180" : "text-destructive"}
                      />
                    </div>
                    <div>
                      <p className="font-medium">{transaction.narration || 'Transaction'}</p>
                      <p className="text-xs text-muted-foreground">{formatDate(transaction.createdAt)}</p>
                    </div>
                  </div>
                  <span
                    className={`font-semibold ${
                      transaction.type === "credit" ? "text-success" : "text-destructive"
                    }`}
                  >
                    {transaction.type === "credit" ? "+" : "-"}{formatCurrency(transaction.amountMinor)}
                  </span>
                </div>
              ))
            ) : (
              <div className="p-8 text-center text-muted-foreground">
                <p>No recent transactions</p>
                <p className="text-sm">Your transaction history will appear here</p>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </div>
  );
}