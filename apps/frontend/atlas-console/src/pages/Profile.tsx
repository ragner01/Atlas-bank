import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { 
  ArrowLeft, 
  User, 
  Bell, 
  Shield, 
  HelpCircle, 
  FileText, 
  LogOut,
  ChevronRight,
  Star,
  Gift
} from "lucide-react";
import { Link } from "react-router-dom";

export default function Profile() {
  const menuItems = [
    { icon: User, label: "Personal Information", path: "#" },
    { icon: Bell, label: "Notifications", path: "#", badge: "3" },
    { icon: Shield, label: "Security & Privacy", path: "#" },
    { icon: Star, label: "Referral Program", path: "#", highlight: true },
    { icon: Gift, label: "Rewards & Cashback", path: "#" },
    { icon: HelpCircle, label: "Help & Support", path: "#" },
    { icon: FileText, label: "Terms & Conditions", path: "#" },
  ];

  return (
    <div className="min-h-screen bg-secondary">
      {/* Header */}
      <div className="bg-primary text-primary-foreground px-4 py-6">
        <div className="flex items-center gap-4 mb-6">
          <Link to="/">
            <Button variant="ghost" size="icon" className="text-white hover:bg-white/20">
              <ArrowLeft size={24} />
            </Button>
          </Link>
          <h1 className="text-2xl font-bold">Profile</h1>
        </div>

        {/* Profile Card */}
        <Card className="bg-white">
          <CardContent className="p-6">
            <div className="flex items-center gap-4">
              <div className="w-16 h-16 bg-primary rounded-full flex items-center justify-center">
                <span className="text-2xl font-bold text-white">CO</span>
              </div>
              <div className="flex-1">
                <h2 className="text-xl font-bold">Chukwudi Okafor</h2>
                <p className="text-sm text-muted-foreground">chukwudi@email.com</p>
                <p className="text-xs text-muted-foreground">+234 801 234 5678</p>
              </div>
              <Button size="sm" variant="outline">
                Edit
              </Button>
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="p-4 space-y-4">
        {/* Account Tier */}
        <Card>
          <CardContent className="p-4">
            <div className="flex items-center justify-between">
              <div>
                <p className="text-sm text-muted-foreground mb-1">Account Tier</p>
                <p className="text-lg font-semibold">Tier 3 (KYC Verified)</p>
              </div>
              <div className="bg-success/10 text-success px-3 py-1 rounded-full text-sm font-medium">
                Verified ✓
              </div>
            </div>
            <div className="mt-4 space-y-2">
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Daily limit</span>
                <span className="font-medium">₦5,000,000</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Balance limit</span>
                <span className="font-medium">Unlimited</span>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Menu Items */}
        <Card>
          <CardContent className="p-0">
            {menuItems.map((item, index) => {
              const Icon = item.icon;
              return (
                <Link
                  key={index}
                  to={item.path}
                  className={`flex items-center gap-3 p-4 hover:bg-secondary transition-colors ${
                    index !== menuItems.length - 1 ? "border-b border-border" : ""
                  } ${item.highlight ? "bg-primary/5" : ""}`}
                >
                  <Icon size={20} className={item.highlight ? "text-primary" : "text-muted-foreground"} />
                  <span className={`flex-1 font-medium ${item.highlight ? "text-primary" : ""}`}>
                    {item.label}
                  </span>
                  {item.badge && (
                    <span className="bg-destructive text-white text-xs px-2 py-0.5 rounded-full">
                      {item.badge}
                    </span>
                  )}
                  <ChevronRight size={18} className="text-muted-foreground" />
                </Link>
              );
            })}
          </CardContent>
        </Card>

        {/* App Version */}
        <div className="text-center text-sm text-muted-foreground">
          <p>Version 1.0.0</p>
        </div>

        {/* Logout */}
        <Button variant="destructive" className="w-full" size="lg">
          <LogOut size={20} className="mr-2" />
          Log Out
        </Button>
      </div>
    </div>
  );
}
