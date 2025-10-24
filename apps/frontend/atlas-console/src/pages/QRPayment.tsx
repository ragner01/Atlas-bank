import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ArrowLeft, QrCode, ScanLine, Download, Share2, Copy } from "lucide-react";
import { Link } from "react-router-dom";
import { useState } from "react";
import { toast } from "sonner";

export default function QRPayment() {
  const [amount, setAmount] = useState("");
  const [description, setDescription] = useState("");

  const userQRCode = "QR-USER-2348100000001";
  const userName = "Chukwudi Okafor";
  const userPhone = "08012345678";

  const handleCopy = () => {
    navigator.clipboard.writeText(userQRCode);
    toast.success("QR code copied to clipboard!");
  };

  const handleShare = () => {
    toast.success("Sharing QR code...");
  };

  const handleScan = () => {
    toast.success("Camera opening for QR scan...");
  };

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
          <h1 className="text-2xl font-bold">QR Payments</h1>
        </div>
      </div>

      <div className="p-4">
        <Tabs defaultValue="receive" className="w-full">
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="receive" className="gap-2">
              <QrCode size={18} />
              Receive
            </TabsTrigger>
            <TabsTrigger value="scan" className="gap-2">
              <ScanLine size={18} />
              Scan & Pay
            </TabsTrigger>
          </TabsList>

          <TabsContent value="receive" className="space-y-4 mt-4">
            {/* Generate QR */}
            <Card>
              <CardContent className="pt-6 space-y-4">
                <div className="text-center">
                  <h3 className="font-semibold mb-2">Your Payment QR Code</h3>
                  <p className="text-sm text-muted-foreground">
                    Show this code to receive payments
                  </p>
                </div>

                {/* QR Code Display */}
                <div className="bg-white p-6 rounded-xl flex flex-col items-center">
                  <div className="w-48 h-48 bg-gradient-to-br from-primary/20 to-primary/5 rounded-xl flex items-center justify-center mb-4">
                    <QrCode size={120} className="text-primary" />
                  </div>
                  <p className="font-semibold text-lg">{userName}</p>
                  <p className="text-sm text-muted-foreground">{userPhone}</p>
                  <p className="text-xs text-muted-foreground mt-2 font-mono">{userQRCode}</p>
                </div>

                {/* Custom Amount */}
                <div className="space-y-3">
                  <div className="space-y-2">
                    <Label htmlFor="qrAmount">Request Specific Amount (Optional)</Label>
                    <Input
                      id="qrAmount"
                      type="number"
                      value={amount}
                      onChange={(e) => setAmount(e.target.value)}
                      placeholder="Enter amount"
                    />
                  </div>

                  <div className="space-y-2">
                    <Label htmlFor="description">Description (Optional)</Label>
                    <Input
                      id="description"
                      value={description}
                      onChange={(e) => setDescription(e.target.value)}
                      placeholder="What's this for?"
                    />
                  </div>
                </div>

                {/* Actions */}
                <div className="grid grid-cols-3 gap-2">
                  <Button variant="outline" size="sm" onClick={handleCopy}>
                    <Copy size={16} className="mr-1" />
                    Copy
                  </Button>
                  <Button variant="outline" size="sm" onClick={handleShare}>
                    <Share2 size={16} className="mr-1" />
                    Share
                  </Button>
                  <Button variant="outline" size="sm">
                    <Download size={16} className="mr-1" />
                    Save
                  </Button>
                </div>
              </CardContent>
            </Card>

            {/* Recent QR Payments */}
            <div>
              <h3 className="text-sm font-semibold mb-3 text-muted-foreground">RECENT QR PAYMENTS</h3>
              <Card>
                <CardContent className="p-0">
                  {[
                    { name: "Emeka James", amount: 5000, time: "2 hours ago" },
                    { name: "Ada Nnamdi", amount: 12000, time: "Yesterday" },
                    { name: "Ngozi Okoro", amount: 3500, time: "2 days ago" },
                  ].map((payment, index) => (
                    <div
                      key={index}
                      className={`flex items-center justify-between p-4 ${
                        index !== 2 ? "border-b border-border" : ""
                      }`}
                    >
                      <div className="flex items-center gap-3">
                        <div className="w-10 h-10 bg-success/10 rounded-full flex items-center justify-center">
                          <QrCode size={18} className="text-success" />
                        </div>
                        <div>
                          <p className="font-medium">{payment.name}</p>
                          <p className="text-xs text-muted-foreground">{payment.time}</p>
                        </div>
                      </div>
                      <span className="font-semibold text-success">
                        +₦{payment.amount.toLocaleString()}
                      </span>
                    </div>
                  ))}
                </CardContent>
              </Card>
            </div>
          </TabsContent>

          <TabsContent value="scan" className="space-y-4 mt-4">
            <Card>
              <CardContent className="pt-6 space-y-4">
                <div className="text-center">
                  <h3 className="font-semibold mb-2">Scan QR to Pay</h3>
                  <p className="text-sm text-muted-foreground">
                    Point your camera at a QR code to make payment
                  </p>
                </div>

                {/* Camera View Placeholder */}
                <div className="bg-gradient-to-br from-muted to-secondary rounded-xl h-72 flex flex-col items-center justify-center relative overflow-hidden">
                  <div className="absolute inset-8 border-4 border-primary rounded-xl animate-pulse" />
                  <ScanLine size={64} className="text-primary mb-4" />
                  <p className="text-sm text-muted-foreground">Position QR code within frame</p>
                </div>

                <Button onClick={handleScan} className="w-full" size="lg">
                  Open Camera
                </Button>

                <div className="relative">
                  <div className="absolute inset-0 flex items-center">
                    <span className="w-full border-t border-border" />
                  </div>
                  <div className="relative flex justify-center text-xs uppercase">
                    <span className="bg-card px-2 text-muted-foreground">Or enter code manually</span>
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="manualCode">QR Code</Label>
                  <Input id="manualCode" placeholder="Enter QR code" />
                </div>

                <Button variant="outline" className="w-full">
                  Continue
                </Button>
              </CardContent>
            </Card>

            {/* Info */}
            <Card className="bg-blue-50 border-blue-200">
              <CardContent className="p-4">
                <p className="text-sm text-blue-900">
                  💡 <strong>Tip:</strong> You can scan QR codes from merchants, friends, or any payment request to pay instantly.
                </p>
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
}
