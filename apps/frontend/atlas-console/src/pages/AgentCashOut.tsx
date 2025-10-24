import { useState } from "react";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { ArrowLeft, Loader2 } from "lucide-react";
import { Link } from "react-router-dom";
import { toast } from "sonner";
import { useAuth } from "@/hooks/useAuth";
import { AgentService, formatCurrency, AtlasClient, AtlasApiError, validateAmount } from "@/lib/api";
import { useMutation } from "@tanstack/react-query";
import { OfflineIndicator } from "@/hooks/useNetworkStatus";

export default function AgentCashOut() {
  const [agentCode, setAgentCode] = useState("AG001");
  const [amount, setAmount] = useState("");
  const [loading, setLoading] = useState(false);
  
  const { user } = useAuth();
  const client = new AtlasClient();

  const cashOutMutation = useMutation({
    mutationFn: async () => {
      if (!user?.msisdn) throw new Error("User not logged in");
      
      const amountMinor = parseInt(amount);
      if (!validateAmount(amountMinor)) {
        throw new Error("Invalid amount");
      }

      return AgentService.cashOutIntent(user.msisdn, agentCode, amountMinor, 'NGN');
    },
    onSuccess: (data) => {
      toast.success("Cash-out request sent to agent");
      setAmount("");
    },
    onError: (error: any) => {
      if (error instanceof AtlasApiError) {
        toast.error(`${error.message} (${error.code})`);
      } else {
        toast.error(error.message || "Failed to create cash-out intent");
      }
    },
  });

  const handleSubmit = () => {
    if (!agentCode || !amount) {
      toast.error("Please fill in all fields");
      return;
    }

    const amountMinor = parseInt(amount);
    if (!validateAmount(amountMinor)) {
      toast.error("Please enter a valid amount (1 NGN - 1,000,000 NGN)");
      return;
    }

    cashOutMutation.mutate();
  };

  return (
    <div className="min-h-screen bg-secondary">
      <OfflineIndicator />
      
      <div className="bg-primary text-primary-foreground px-4 py-6">
        <div className="flex items-center gap-4">
          <Link to="/">
            <Button variant="ghost" size="icon" className="text-white hover:bg-white/20">
              <ArrowLeft size={24} />
            </Button>
          </Link>
          <h1 className="text-2xl font-bold">Agent Cash-out</h1>
        </div>
      </div>

      <div className="p-4 space-y-4">
        <Card>
          <CardContent className="pt-6 space-y-4">
            <div className="space-y-2">
              <Label htmlFor="agent">Agent Code</Label>
              <Input
                id="agent"
                value={agentCode}
                onChange={(e) => setAgentCode(e.target.value)}
                placeholder="AG001"
              />
            </div>

            <div className="space-y-2">
              <Label htmlFor="amount">Amount (minor units)</Label>
              <Input
                id="amount"
                type="number"
                value={amount}
                onChange={(e) => setAmount(e.target.value)}
                placeholder="30000"
              />
            </div>

            <div className="text-sm text-muted-foreground">
              Amount: {amount ? formatCurrency(parseInt(amount) || 0) : "₦0.00"}
            </div>

            <Button 
              onClick={handleSubmit} 
              className="w-full" 
              size="lg"
              disabled={cashOutMutation.isPending || !agentCode || !amount}
            >
              {cashOutMutation.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Processing...
                </>
              ) : (
                "Create Cash-out Intent"
              )}
            </Button>
          </CardContent>
        </Card>

        <Card>
          <CardContent className="pt-6">
            <h3 className="font-semibold mb-2">How it works:</h3>
            <ol className="text-sm text-muted-foreground space-y-1">
              <li>1. Enter agent code and amount</li>
              <li>2. Agent receives withdrawal request</li>
              <li>3. Visit agent with valid ID</li>
              <li>4. Agent processes withdrawal</li>
              <li>5. Receive cash from agent</li>
            </ol>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
