import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ArrowLeft, CreditCard, Eye, EyeOff, Copy, Lock, Settings } from "lucide-react";
import { Link } from "react-router-dom";
import { useState } from "react";
import { toast } from "sonner";

export default function Cards() {
  const [showDetails, setShowDetails] = useState(false);

  const cardDetails = {
    number: "5399 8345 7721 6543",
    cvv: "123",
    expiry: "12/26",
  };

  const copyToClipboard = (text: string) => {
    navigator.clipboard.writeText(text);
    toast.success("Copied to clipboard!");
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
          <h1 className="text-2xl font-bold">My Cards</h1>
        </div>
      </div>

      <div className="p-4 space-y-4">
        {/* Virtual Card */}
        <div className="relative">
          <div className="bg-gradient-to-br from-primary via-primary to-green-700 rounded-2xl p-6 text-white shadow-xl">
            <div className="flex justify-between items-start mb-8">
              <div>
                <p className="text-xs opacity-80 mb-1">Virtual Card</p>
                <p className="text-sm font-semibold">Naira Mastercard</p>
              </div>
              <CreditCard size={32} className="opacity-80" />
            </div>

            <div className="space-y-4">
              <div>
                <p className="text-xs opacity-80 mb-1">Card Number</p>
                <p className="text-lg font-mono tracking-wider">
                  {showDetails ? cardDetails.number : "•••• •••• •••• 6543"}
                </p>
              </div>

              <div className="flex gap-8">
                <div>
                  <p className="text-xs opacity-80 mb-1">Expiry</p>
                  <p className="font-mono">{showDetails ? cardDetails.expiry : "••/••"}</p>
                </div>
                <div>
                  <p className="text-xs opacity-80 mb-1">CVV</p>
                  <p className="font-mono">{showDetails ? cardDetails.cvv : "•••"}</p>
                </div>
              </div>

              <div>
                <p className="text-xs opacity-80 mb-1">Cardholder Name</p>
                <p className="font-semibold">CHUKWUDI OKAFOR</p>
              </div>
            </div>
          </div>

          <button
            onClick={() => setShowDetails(!showDetails)}
            className="absolute top-10 right-10 p-2 bg-white/20 rounded-full hover:bg-white/30 transition-colors"
          >
            {showDetails ? <EyeOff size={20} /> : <Eye size={20} />}
          </button>
        </div>

        {/* Card Details */}
        <Card>
          <CardContent className="p-4 space-y-3">
            <div className="flex items-center justify-between">
              <span className="text-sm text-muted-foreground">Balance</span>
              <span className="text-2xl font-bold">₦50,000.00</span>
            </div>
            <div className="flex gap-2">
              <Button className="flex-1" variant="outline" size="sm">
                <Lock size={16} className="mr-2" />
                Freeze Card
              </Button>
              <Button className="flex-1" variant="outline" size="sm">
                <Settings size={16} className="mr-2" />
                Settings
              </Button>
            </div>
          </CardContent>
        </Card>

        {/* Card Actions */}
        {showDetails && (
          <Card>
            <CardContent className="p-4 space-y-3">
              <h3 className="font-semibold mb-2">Quick Actions</h3>
              <Button
                variant="outline"
                className="w-full justify-start"
                onClick={() => copyToClipboard(cardDetails.number)}
              >
                <Copy size={16} className="mr-2" />
                Copy Card Number
              </Button>
              <Button
                variant="outline"
                className="w-full justify-start"
                onClick={() => copyToClipboard(cardDetails.cvv)}
              >
                <Copy size={16} className="mr-2" />
                Copy CVV
              </Button>
            </CardContent>
          </Card>
        )}

        {/* Create New Card */}
        <Button className="w-full" size="lg" variant="outline">
          <CreditCard size={20} className="mr-2" />
          Create New Virtual Card
        </Button>

        {/* Card Info */}
        <Card className="bg-blue-50 border-blue-200">
          <CardContent className="p-4">
            <p className="text-sm text-blue-900">
              💡 <strong>Tip:</strong> Use virtual cards for online payments. You can create multiple cards and set spending limits for each one.
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
