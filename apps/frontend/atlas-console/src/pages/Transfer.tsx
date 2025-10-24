import { useState } from "react";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Button } from "@/components/ui/button";
import { Label } from "@/components/ui/label";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { ArrowLeft, Users, Building2, Loader2 } from "lucide-react";
import { Link } from "react-router-dom";
import { toast } from "sonner";
import { useAuth } from "@/hooks/useAuth";
import { PaymentsService, formatCurrency, AtlasClient, AtlasApiError, validateAccountId, validateAmount, hmacHex, generateDeviceId, isOnline } from "@/lib/api";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { OfflineIndicator } from "@/hooks/useNetworkStatus";

export default function Transfer() {
  const [amount, setAmount] = useState("");
  const [accountNumber, setAccountNumber] = useState("");
  const [selectedBank, setSelectedBank] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [narration, setNarration] = useState("");
  const [isValidatingAccount, setIsValidatingAccount] = useState(false);
  const [accountName, setAccountName] = useState("");
  
  const { user } = useAuth();
  const queryClient = useQueryClient();
  
  // Initialize AtlasClient - Matching Phase 24 mobile app
  const client = new AtlasClient();

  // Transfer mutation with offline fallback - Matching Phase 24 mobile app
  const transferMutation = useMutation({
    mutationFn: async (transferData: any) => {
      // Try online first
      try {
        return await PaymentsService.transferWithRisk(transferData);
      } catch (error) {
        // If online fails and we're offline, queue the operation
        if (!isOnline()) {
          console.log("Online transfer failed, queuing offline:", error);
          
          const deviceId = generateDeviceId();
          const nonce = `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
          const signature = await hmacHex('dev-secret', deviceId, transferData, nonce, 'tnt_demo');
          
          await client.offlineEnqueue({
            tenantId: 'tnt_demo',
            deviceId: deviceId,
            kind: 'transfer',
            payload: transferData,
            nonce,
            signature,
          });
          
          return { reference: 'offline-queued', status: 'queued' };
        }
        throw error;
      }
    },
    onSuccess: (data) => {
      if (data.status === 'queued') {
        toast.success("Transfer queued for sync when online");
      } else {
        toast.success(`Transfer successful! Reference: ${data.reference}`);
      }
      
      // Reset form
      setAmount("");
      setAccountNumber("");
      setPhoneNumber("");
      setNarration("");
      setAccountName("");
      
      // Invalidate balance and transactions queries
      queryClient.invalidateQueries({ queryKey: ['balance'] });
      queryClient.invalidateQueries({ queryKey: ['transactions'] });
    },
    onError: (error: any) => {
      if (error instanceof AtlasApiError) {
        toast.error(`${error.message} (${error.code})`);
      } else {
        toast.error(error.response?.data?.message || "Transfer failed. Please try again.");
      }
    },
  });

  const handleAccountValidation = async () => {
    if (accountNumber.length !== 10) {
      toast.error("Please enter a valid 10-digit account number");
      return;
    }

    try {
      setIsValidatingAccount(true);
      const result = await PaymentsService.validateAccount(accountNumber, selectedBank);
      setAccountName(result.name);
      toast.success(`Account validated: ${result.name}`);
    } catch (error: any) {
      toast.error(error.response?.data?.message || "Account validation failed");
      setAccountName("");
    } finally {
      setIsValidatingAccount(false);
    }
  };

  const handleBankTransfer = async () => {
    // Validation - Matching Phase 24 mobile app patterns
    if (!amount || !accountNumber || !selectedBank) {
      toast.error("Please fill in all required fields");
      return;
    }

    if (!accountName) {
      toast.error("Please validate the account number first");
      return;
    }

    const amountMinor = parseFloat(amount) * 100;
    if (!validateAmount(amountMinor)) {
      toast.error("Please enter a valid amount (1 NGN - 1,000,000 NGN)");
      return;
    }

    const sourceAccountId = user ? `msisdn::${user.msisdn}` : '';
    const destinationAccountId = `account::${accountNumber}::${selectedBank}`;

    if (!validateAccountId(sourceAccountId) || !validateAccountId(destinationAccountId)) {
      toast.error("Invalid account ID format");
      return;
    }

    transferMutation.mutate({
      SourceAccountId: sourceAccountId,
      DestinationAccountId: destinationAccountId,
      Minor: amountMinor,
      Currency: "NGN",
      Narration: narration || `Transfer to ${accountName}`,
    });
  };

  const handleWalletTransfer = async () => {
    // Validation - Matching Phase 24 mobile app patterns
    if (!amount || !phoneNumber) {
      toast.error("Please fill in all required fields");
      return;
    }

    const amountMinor = parseFloat(amount) * 100;
    if (!validateAmount(amountMinor)) {
      toast.error("Please enter a valid amount (1 NGN - 1,000,000 NGN)");
      return;
    }

    const sourceAccountId = user ? `msisdn::${user.msisdn}` : '';
    const destinationAccountId = `msisdn::${phoneNumber}`;

    if (!validateAccountId(sourceAccountId) || !validateAccountId(destinationAccountId)) {
      toast.error("Invalid account ID format");
      return;
    }

    transferMutation.mutate({
      SourceAccountId: sourceAccountId,
      DestinationAccountId: destinationAccountId,
      Minor: amountMinor,
      Currency: "NGN",
      Narration: narration || `Transfer to ${phoneNumber}`,
    });
  };

  const recentRecipients = [
    { name: "Ada Nnamdi", account: "0123456789", bank: "GTBank" },
    { name: "Emeka James", account: "9876543210", bank: "Access Bank" },
    { name: "Ngozi Okoro", account: "5555666677", bank: "First Bank" },
  ];

  return (
    <div className="min-h-screen bg-secondary">
      {/* Offline Indicator - Matching Phase 24 mobile app */}
      <OfflineIndicator />
      
      {/* Header */}
      <div className="bg-primary text-primary-foreground px-4 py-6">
        <div className="flex items-center gap-4">
          <Link to="/">
            <Button variant="ghost" size="icon" className="text-white hover:bg-white/20">
              <ArrowLeft size={24} />
            </Button>
          </Link>
          <h1 className="text-2xl font-bold">Transfer Money</h1>
        </div>
      </div>

      <div className="p-4 space-y-4">
        <Tabs defaultValue="bank" className="w-full">
          <TabsList className="grid w-full grid-cols-2">
            <TabsTrigger value="bank" className="gap-2">
              <Building2 size={18} />
              Bank Transfer
            </TabsTrigger>
            <TabsTrigger value="wallet" className="gap-2">
              <Users size={18} />
              To Wallet
            </TabsTrigger>
          </TabsList>

          <TabsContent value="bank" className="space-y-4 mt-4">
            <Card>
              <CardContent className="pt-6 space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="bank">Select Bank</Label>
                  <Select value={selectedBank} onValueChange={setSelectedBank}>
                    <SelectTrigger>
                      <SelectValue placeholder="Choose bank" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="gtbank">GTBank</SelectItem>
                      <SelectItem value="access">Access Bank</SelectItem>
                      <SelectItem value="firstbank">First Bank</SelectItem>
                      <SelectItem value="zenith">Zenith Bank</SelectItem>
                      <SelectItem value="uba">UBA</SelectItem>
                      <SelectItem value="providus">Providus Bank</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="account">Account Number</Label>
                  <div className="flex gap-2">
                    <Input
                      id="account"
                      type="tel"
                      maxLength={10}
                      value={accountNumber}
                      onChange={(e) => setAccountNumber(e.target.value)}
                      placeholder="Enter 10-digit account number"
                      className="flex-1"
                    />
                    <Button
                      onClick={handleAccountValidation}
                      disabled={isValidatingAccount || accountNumber.length !== 10}
                      size="sm"
                    >
                      {isValidatingAccount ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : (
                        "Validate"
                      )}
                    </Button>
                  </div>
                  {accountName && (
                    <p className="text-sm text-success">✓ {accountName}</p>
                  )}
                </div>

                <div className="space-y-2">
                  <Label htmlFor="amount">Amount (₦)</Label>
                  <Input
                    id="amount"
                    type="number"
                    value={amount}
                    onChange={(e) => setAmount(e.target.value)}
                    placeholder="0.00"
                    className="text-2xl font-bold"
                    min="1"
                    step="0.01"
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="narration">Narration (Optional)</Label>
                  <Input 
                    id="narration" 
                    placeholder="What's this for?" 
                    value={narration}
                    onChange={(e) => setNarration(e.target.value)}
                  />
                </div>

                <Button 
                  onClick={handleBankTransfer} 
                  className="w-full" 
                  size="lg"
                  disabled={transferMutation.isPending || !amount || !accountNumber || !selectedBank || !accountName}
                >
                  {transferMutation.isPending ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Processing...
                    </>
                  ) : (
                    `Transfer ${amount ? formatCurrency(parseFloat(amount) * 100) : "₦0.00"}`
                  )}
                </Button>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="wallet" className="space-y-4 mt-4">
            <Card>
              <CardContent className="pt-6 space-y-4">
                <div className="space-y-2">
                  <Label htmlFor="phone">Phone Number</Label>
                  <Input
                    id="phone"
                    placeholder="08012345678"
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                    type="tel"
                    maxLength={15}
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="walletAmount">Amount (₦)</Label>
                  <Input
                    id="walletAmount"
                    type="number"
                    placeholder="0.00"
                    className="text-2xl font-bold"
                    value={amount}
                    onChange={(e) => setAmount(e.target.value)}
                    min="1"
                    step="0.01"
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="walletNarration">Narration (Optional)</Label>
                  <Input 
                    id="walletNarration" 
                    placeholder="What's this for?" 
                    value={narration}
                    onChange={(e) => setNarration(e.target.value)}
                  />
                </div>

                <Button 
                  onClick={handleWalletTransfer} 
                  className="w-full" 
                  size="lg"
                  disabled={transferMutation.isPending || !amount || !phoneNumber}
                >
                  {transferMutation.isPending ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Processing...
                    </>
                  ) : (
                    `Send to Wallet ${amount ? formatCurrency(parseFloat(amount) * 100) : "₦0.00"}`
                  )}
                </Button>
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>

        {/* Recent Recipients */}
        <div>
          <h3 className="text-sm font-semibold mb-3 text-muted-foreground">RECENT RECIPIENTS</h3>
          <Card>
            <CardContent className="p-0">
              {recentRecipients.map((recipient, index) => (
                <button
                  key={index}
                  className={`w-full flex items-center gap-3 p-4 text-left hover:bg-secondary transition-colors ${
                    index !== recentRecipients.length - 1 ? "border-b border-border" : ""
                  }`}
                  onClick={() => {
                    setAccountNumber(recipient.account);
                    setSelectedBank(recipient.bank.toLowerCase().replace(' ', ''));
                    setAccountName(recipient.name);
                  }}
                >
                  <div className="w-10 h-10 bg-primary/10 rounded-full flex items-center justify-center">
                    <span className="text-primary font-bold">{recipient.name.charAt(0)}</span>
                  </div>
                  <div className="flex-1">
                    <p className="font-medium">{recipient.name}</p>
                    <p className="text-xs text-muted-foreground">
                      {recipient.account} • {recipient.bank}
                    </p>
                  </div>
                </button>
              ))}
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
