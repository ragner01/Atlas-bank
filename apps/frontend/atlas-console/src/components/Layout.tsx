import { ReactNode } from "react";
import { Link, useLocation } from "react-router-dom";
import { Home, Send, Receipt, CreditCard, PiggyBank, Clock, User, HandCoins, WifiOff } from "lucide-react";
import { cn } from "@/lib/utils";

interface LayoutProps {
  children: ReactNode;
}

const navItems = [
  { path: "/", icon: Home, label: "Home" },
  { path: "/transfer", icon: Send, label: "Transfer" },
  { path: "/bills", icon: Receipt, label: "Bills" },
  { path: "/cards", icon: CreditCard, label: "Cards" },
  { path: "/savings", icon: PiggyBank, label: "Savings" },
  { path: "/transactions", icon: Clock, label: "History" },
  { path: "/profile", icon: User, label: "Profile" },
];

const additionalNavItems = [
  { path: "/agent/cashin", icon: HandCoins, label: "Cash In" },
  { path: "/agent/cashout", icon: HandCoins, label: "Cash Out" },
  { path: "/offline/sync", icon: WifiOff, label: "Sync" },
];

export const Layout = ({ children }: LayoutProps) => {
  const location = useLocation();

  return (
    <div className="min-h-screen bg-secondary pb-20">
      <main className="pb-4">{children}</main>
      
      <nav className="fixed bottom-0 left-0 right-0 bg-card border-t border-border">
        <div className="max-w-7xl mx-auto px-2">
          <div className="flex justify-around items-center py-2">
            {navItems.map((item) => {
              const Icon = item.icon;
              const isActive = location.pathname === item.path;
              
              return (
                <Link
                  key={item.path}
                  to={item.path}
                  className={cn(
                    "flex flex-col items-center gap-1 px-3 py-2 rounded-lg transition-colors",
                    isActive
                      ? "text-primary"
                      : "text-muted-foreground hover:text-foreground"
                  )}
                >
                  <Icon size={20} />
                  <span className="text-xs font-medium">{item.label}</span>
                </Link>
              );
            })}
          </div>
        </div>
      </nav>
      
      {/* Additional navigation for Phase 24 features */}
      <div className="fixed top-4 right-4 flex gap-2">
        {additionalNavItems.map((item) => {
          const Icon = item.icon;
          const isActive = location.pathname === item.path;
          
          return (
            <Link
              key={item.path}
              to={item.path}
              className={cn(
                "flex items-center gap-2 px-3 py-2 rounded-lg transition-colors text-sm",
                isActive
                  ? "bg-primary text-primary-foreground"
                  : "bg-card text-muted-foreground hover:text-foreground border border-border"
              )}
            >
              <Icon size={16} />
              <span className="font-medium">{item.label}</span>
            </Link>
          );
        })}
      </div>
    </div>
  );
};
