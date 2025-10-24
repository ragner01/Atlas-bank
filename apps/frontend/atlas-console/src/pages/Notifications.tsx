import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { ArrowLeft, Bell, CheckCheck, Trash2, Gift, TrendingUp, AlertCircle, CreditCard } from "lucide-react";
import { Link } from "react-router-dom";
import { useState } from "react";

export default function Notifications() {
  const [notifications, setNotifications] = useState([
    {
      id: 1,
      type: "transaction",
      icon: TrendingUp,
      title: "Payment Received",
      message: "You received ₦30,000 from Emeka James",
      time: "5 minutes ago",
      read: false,
      color: "text-success",
      bgColor: "bg-success/10",
    },
    {
      id: 2,
      type: "promotion",
      icon: Gift,
      title: "Cashback Reward!",
      message: "You earned ₦500 cashback on your last transaction",
      time: "1 hour ago",
      read: false,
      color: "text-warning",
      bgColor: "bg-warning/10",
    },
    {
      id: 3,
      type: "alert",
      icon: AlertCircle,
      title: "Bill Payment Reminder",
      message: "Your DSTV subscription is due in 3 days",
      time: "3 hours ago",
      read: false,
      color: "text-destructive",
      bgColor: "bg-destructive/10",
    },
    {
      id: 4,
      type: "transaction",
      icon: CreditCard,
      title: "Card Transaction",
      message: "₦25,500 was debited from your virtual card at Jumia",
      time: "Yesterday",
      read: true,
      color: "text-primary",
      bgColor: "bg-primary/10",
    },
    {
      id: 5,
      type: "transaction",
      icon: TrendingUp,
      title: "Transfer Successful",
      message: "₦15,000 was sent to Ada Nnamdi",
      time: "Yesterday",
      read: true,
      color: "text-success",
      bgColor: "bg-success/10",
    },
    {
      id: 6,
      type: "promotion",
      icon: Gift,
      title: "New Feature Available",
      message: "Check out our new investment plans with up to 20% returns!",
      time: "2 days ago",
      read: true,
      color: "text-info",
      bgColor: "bg-info/10",
    },
  ]);

  const unreadCount = notifications.filter((n) => !n.read).length;

  const markAllAsRead = () => {
    setNotifications(notifications.map((n) => ({ ...n, read: true })));
  };

  const deleteNotification = (id: number) => {
    setNotifications(notifications.filter((n) => n.id !== id));
  };

  return (
    <div className="min-h-screen bg-secondary">
      {/* Header */}
      <div className="bg-primary text-primary-foreground px-4 py-6">
        <div className="flex items-center justify-between mb-4">
          <div className="flex items-center gap-4">
            <Link to="/">
              <Button variant="ghost" size="icon" className="text-white hover:bg-white/20">
                <ArrowLeft size={24} />
              </Button>
            </Link>
            <div>
              <h1 className="text-2xl font-bold">Notifications</h1>
              {unreadCount > 0 && (
                <p className="text-sm opacity-90">{unreadCount} unread</p>
              )}
            </div>
          </div>
          {unreadCount > 0 && (
            <Button
              variant="ghost"
              size="sm"
              className="text-white hover:bg-white/20"
              onClick={markAllAsRead}
            >
              <CheckCheck size={18} className="mr-1" />
              Mark all read
            </Button>
          )}
        </div>
      </div>

      <div className="p-4">
        {notifications.length === 0 ? (
          <Card>
            <CardContent className="p-12 text-center">
              <Bell size={48} className="text-muted-foreground mx-auto mb-4" />
              <h3 className="font-semibold mb-2">No notifications</h3>
              <p className="text-sm text-muted-foreground">
                You're all caught up! New notifications will appear here.
              </p>
            </CardContent>
          </Card>
        ) : (
          <div className="space-y-2">
            {notifications.map((notification) => {
              const Icon = notification.icon;
              return (
                <Card
                  key={notification.id}
                  className={notification.read ? "opacity-60" : ""}
                >
                  <CardContent className="p-4">
                    <div className="flex gap-3">
                      <div
                        className={`w-10 h-10 rounded-full flex items-center justify-center flex-shrink-0 ${notification.bgColor}`}
                      >
                        <Icon size={20} className={notification.color} />
                      </div>

                      <div className="flex-1 min-w-0">
                        <div className="flex items-start justify-between gap-2 mb-1">
                          <h4 className="font-semibold text-sm">{notification.title}</h4>
                          {!notification.read && (
                            <div className="w-2 h-2 bg-primary rounded-full flex-shrink-0 mt-1" />
                          )}
                        </div>
                        <p className="text-sm text-muted-foreground mb-2">
                          {notification.message}
                        </p>
                        <div className="flex items-center justify-between">
                          <span className="text-xs text-muted-foreground">
                            {notification.time}
                          </span>
                          <Button
                            variant="ghost"
                            size="sm"
                            className="h-8 px-2 text-muted-foreground hover:text-destructive"
                            onClick={() => deleteNotification(notification.id)}
                          >
                            <Trash2 size={14} />
                          </Button>
                        </div>
                      </div>
                    </div>
                  </CardContent>
                </Card>
              );
            })}
          </div>
        )}
      </div>
    </div>
  );
}
