import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { ArrowLeft, Users, Share2, Copy, Clock } from "lucide-react";
import { Link } from "react-router-dom";
import { useState } from "react";
import { toast } from "sonner";

export default function RequestMoney() {
  const [amount, setAmount] = useState("");
  const [recipient, setRecipient] = useState("");
  const [reason, setReason] = useState("");

  const handleRequest = () => {
    toast.success("Payment request sent!");
  };

  const handleShare = () => {
    toast.success("Sharing payment link...");
  };

  const pendingRequests = [
    {
      id: 1,
      from: "Emeka James",
      amount: 15000,
      reason: "Lunch at Chicken Republic",
      time: "2 hours ago",
      status: "pending",
    },
    {
      id: 2,
      from: "Ada Nnamdi",
      amount: 5000,
      reason: "Movie tickets",
      time: "Yesterday",
      status: "pending",
    },
  ];

  const sentRequests = [
    {
      id: 1,
      to: "Ngozi Okoro",
      amount: 8000,
      reason: "Dinner split",
      time: "3 hours ago",
      status: "pending",
    },
  ];

  return (
    <div className="min-h-screen bg-secondary">
      {/* Header */}
      <div className="bg-primary text-primary-foreground px-4 py-6">
        <div className="flex items-center gap-4">
          <Link to="/">
            <Button variant="ghost" size="icon" className="text-white hover:bg-white/20">
              <ArrowLeft size={24} />
            </Button>
          </Link>
          <h1 className="text-2xl font-bold">Request Money</h1>
        </div>
      </div>

      <div className="p-4 space-y-4">
        {/* Request Form */}
        <Card>
          <CardContent className="pt-6 space-y-4">
            <div className="text-center mb-4">
              <div className="w-16 h-16 bg-primary/10 rounded-full flex items-center justify-center mx-auto mb-3">
                <Users size={32} className="text-primary" />
              </div>
              <h3 className="font-semibold text-lg">Send Payment Request</h3>
              <p className="text-sm text-muted-foreground">
                Request money from friends or split bills
              </p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="recipient">Recipient (Phone or @username)</Label>
              <Input
                id="recipient"
                value={recipient}
                onChange={(e) => setRecipient(e.target.value)}
                placeholder="08012345678 or @username"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="requestAmount">Amount (₦)</Label>
              <Input
                id="requestAmount"
                type="number"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                placeholder="0.00"
                className="text-2xl font-bold"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="reason">What's it for?</Label>
              <Textarea
                id="reason"
                value={reason}
                onChange={(e) => setReason(e.target.value)}
                placeholder="e.g., Lunch at restaurant"
                rows={3}
              />
            </div>

            <div className="grid grid-cols-2 gap-2">
              <Button onClick={handleRequest} className="w-full">
                Send Request
              </Button>
              <Button variant="outline" onClick={handleShare} className="w-full">
                <Share2 size={18} className="mr-2" />
                Share Link
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* Quick Amounts */}
        <Card>
          <CardContent className="pt-4">
            <Label className="text-xs text-muted-foreground mb-2 block">QUICK AMOUNTS</Label>
            <div className="grid grid-cols-4 gap-2">
              {[1000, 5000, 10000, 20000].map((amt) => (
                <Button
                  key={amt}
                  variant="outline"
                  size="sm"
                  onClick={() => setAmount(String(amt))}
                >
                  ₦{amt.toLocaleString()}
                </Button>
              ))}
            </div>
          </CardContent>
        </Card>

        {/* Pending Requests Received */}
        {pendingRequests.length > 0 && (
          <div>
            <h3 className="text-sm font-semibold mb-3 text-muted-foreground">
              REQUESTS RECEIVED
            </h3>
            <Card>
              <CardContent className="p-0">
                {pendingRequests.map((request, index) => (
                  <div
                    key={request.id}
                    className={`p-4 ${
                      index !== pendingRequests.length - 1 ? "border-b border-border" : ""
                    }`}
                  >
                    <div className="flex items-start justify-between mb-2">
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center">
                          <span className="text-primary font-bold">
                            {request.from.charAt(0)}
                          </span>
                        </div>
                        <div>
                          <p className="font-medium">{request.from}</p>
                          <p className="text-xs text-muted-foreground">{request.time}</p>
                        </div>
                      </div>
                      <span className="font-bold text-lg">
                        ₦{request.amount.toLocaleString()}
                      </span>
                    </div>
                    <p className="text-sm text-muted-foreground mb-3 ml-13">
                      {request.reason}
                    </p>
                    <div className="grid grid-cols-2 gap-2">
                      <Button size="sm" className="w-full">
                        Pay Now
                      </Button>
                      <Button size="sm" variant="outline" className="w-full">
                        Decline
                      </Button>
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>
        )}

        {/* Sent Requests */}
        {sentRequests.length > 0 && (
          <div>
            <h3 className="text-sm font-semibold mb-3 text-muted-foreground">
              REQUESTS SENT
            </h3>
            <Card>
              <CardContent className="p-0">
                {sentRequests.map((request, index) => (
                  <div
                    key={request.id}
                    className={`flex items-center justify-between p-4 ${
                      index !== sentRequests.length - 1 ? "border-b border-border" : ""
                    }`}
                  >
                    <div className="flex items-center gap-3">
                      <div className="w-10 h-10 bg-warning/10 rounded-full flex items-center justify-center">
                        <Clock size={18} className="text-warning" />
                      </div>
                      <div>
                        <p className="font-medium">{request.to}</p>
                        <p className="text-xs text-muted-foreground">{request.reason}</p>
                        <p className="text-xs text-muted-foreground">{request.time}</p>
                      </div>
                    </div>
                    <div className="text-right">
                      <p className="font-bold">₦{request.amount.toLocaleString()}</p>
                      <span className="text-xs text-warning">Pending</span>
                    </div>
                  </div>
                ))}
              </CardContent>
            </Card>
          </div>
        )}

        {/* Info */}
        <Card className="bg-blue-50 border-blue-200">
          <CardContent className="p-4">
            <p className="text-sm text-blue-900">
              💡 <strong>Tip:</strong> Payment requests expire after 7 days. Recipients can pay directly from the request notification.
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
