import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from "@/components/ui/select";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { ArrowLeft, Smartphone, Wifi, Lightbulb, Tv, Loader2 } from "lucide-react";
import { Link } from "react-router-dom";
import { toast } from "sonner";
import { useState } from "react";
import { useAuth } from "@/hooks/useAuth";
import { PaymentsService, AtlasClient, AtlasApiError, validateAmount, hmacHex, generateDeviceId, isOnline } from "@/lib/api";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { OfflineIndicator } from "@/hooks/useNetworkStatus";

export default function Bills() {
  const [selectedNetwork, setSelectedNetwork] = useState("");
  const [phoneNumber, setPhoneNumber] = useState("");
  const [amount, setAmount] = useState("");
  const [selectedProvider, setSelectedProvider] = useState("");
  const [meterNumber, setMeterNumber] = useState("");
  const [smartcardNumber, setSmartcardNumber] = useState("");
  const [selectedPackage, setSelectedPackage] = useState("");
  
  const { user } = useAuth();
  const queryClient = useQueryClient();
  
  // Initialize AtlasClient - Matching Phase 24 mobile app
  const client = new AtlasClient();

  // Bill payment mutation with offline fallback - Matching Phase 24 mobile app
  const billPaymentMutation = useMutation({
    mutationFn: async (paymentData: any) => {
      // Try online first
      try {
        return await PaymentsService.payBill(paymentData);
      } catch (error) {
        // If online fails and we're offline, queue the operation
        if (!isOnline()) {
          console.log("Online bill payment failed, queuing offline:", error);
          
          const deviceId = generateDeviceId();
          const nonce = `${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
          const signature = await hmacHex('dev-secret', deviceId, paymentData, nonce, 'tnt_demo');
          
          await client.offlineEnqueue({
            tenantId: 'tnt_demo',
            deviceId: deviceId,
            kind: 'bill_payment',
            payload: paymentData,
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
        toast.success("Bill payment queued for sync when online");
      } else {
        toast.success(`Payment successful! Reference: ${data.reference}`);
      }
      
      // Reset form
      setPhoneNumber("");
      setAmount("");
      setMeterNumber("");
      setSmartcardNumber("");
      
      // Invalidate balance and transactions queries
      queryClient.invalidateQueries({ queryKey: ['balance'] });
      queryClient.invalidateQueries({ queryKey: ['transactions'] });
    },
    onError: (error: any) => {
      if (error instanceof AtlasApiError) {
        toast.error(`${error.message} (${error.code})`);
      } else {
        toast.error(error.response?.data?.message || "Payment failed. Please try again.");
      }
    },
  });

  const handleAirtimePayment = async () => {
    if (!selectedNetwork || !phoneNumber || !amount) {
      toast.error("Please fill in all required fields");
      return;
    }

    const amountMinor = parseFloat(amount) * 100;
    if (!validateAmount(amountMinor)) {
      toast.error("Please enter a valid amount (1 NGN - 1,000,000 NGN)");
      return;
    }

    const sourceAccountId = user ? `msisdn::${user.msisdn}` : '';

    billPaymentMutation.mutate({
      SourceAccountId: sourceAccountId,
      BillType: 'airtime',
      Provider: selectedNetwork,
      AccountNumber: phoneNumber,
      Amount: amountMinor,
      Currency: 'NGN',
      Narration: `Airtime purchase for ${phoneNumber}`,
    });
  };

  const handleDataPayment = async () => {
    if (!selectedProvider || !phoneNumber || !selectedPackage) {
      toast.error("Please fill in all required fields");
      return;
    }

    const packageAmounts: { [key: string]: number } = {
      '1gb': 30000, // 300 NGN in minor units
      '2gb': 50000,
      '5gb': 120000,
      '10gb': 200000,
      '20gb': 350000,
    };

    const amountMinor = packageAmounts[selectedPackage] || 0;
    const sourceAccountId = user ? `msisdn::${user.msisdn}` : '';

    billPaymentMutation.mutate({
      SourceAccountId: sourceAccountId,
      BillType: 'data',
      Provider: selectedProvider,
      AccountNumber: phoneNumber,
      Amount: amountMinor,
      Currency: 'NGN',
      Narration: `Data purchase for ${phoneNumber}`,
    });
  };

  const handleElectricityPayment = async () => {
    if (!selectedProvider || !meterNumber || !amount) {
      toast.error("Please fill in all required fields");
      return;
    }

    const amountMinor = parseFloat(amount) * 100;
    if (!validateAmount(amountMinor)) {
      toast.error("Please enter a valid amount (1 NGN - 1,000,000 NGN)");
      return;
    }

    const sourceAccountId = user ? `msisdn::${user.msisdn}` : '';

    billPaymentMutation.mutate({
      SourceAccountId: sourceAccountId,
      BillType: 'electricity',
      Provider: selectedProvider,
      AccountNumber: meterNumber,
      Amount: amountMinor,
      Currency: 'NGN',
      Narration: `Electricity payment for ${meterNumber}`,
    });
  };

  const handleCablePayment = async () => {
    if (!selectedProvider || !smartcardNumber || !selectedPackage) {
      toast.error("Please fill in all required fields");
      return;
    }

    const packageAmounts: { [key: string]: number } = {
      'compact': 1050000, // 10,500 NGN in minor units
      'premium': 2450000,
      'asia': 820000,
    };

    const amountMinor = packageAmounts[selectedPackage] || 0;
    const sourceAccountId = user ? `msisdn::${user.msisdn}` : '';

    billPaymentMutation.mutate({
      SourceAccountId: sourceAccountId,
      BillType: 'cable',
      Provider: selectedProvider,
      AccountNumber: smartcardNumber,
      Amount: amountMinor,
      Currency: 'NGN',
      Narration: `Cable subscription for ${smartcardNumber}`,
    });
  };

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
          <h1 className="text-2xl font-bold">Pay Bills</h1>
        </div>
      </div>

      <div className="p-4">
        <Tabs defaultValue="airtime" className="w-full">
          <TabsList className="grid w-full grid-cols-4">
            <TabsTrigger value="airtime" className="text-xs">
              <Smartphone size={16} />
            </TabsTrigger>
            <TabsTrigger value="data" className="text-xs">
              <Wifi size={16} />
            </TabsTrigger>
            <TabsTrigger value="electricity" className="text-xs">
              <Lightbulb size={16} />
            </TabsTrigger>
            <TabsTrigger value="cable" className="text-xs">
              <Tv size={16} />
            </TabsTrigger>
          </TabsList>

          <TabsContent value="airtime" className="space-y-4 mt-4">
            <Card>
              <CardContent className="pt-6 space-y-4">
                <div className="space-y-2">
                  <Label>Network Provider</Label>
                  <div className="grid grid-cols-4 gap-3">
                    {["MTN", "Airtel", "Glo", "9mobile"].map((network) => (
                      <button
                        key={network}
                        onClick={() => setSelectedNetwork(network.toLowerCase())}
                        className={`p-4 border-2 rounded-xl transition-all ${
                          selectedNetwork === network.toLowerCase()
                            ? "border-primary bg-primary/5"
                            : "border-border hover:border-primary hover:bg-primary/5"
                        }`}
                      >
                        <div className="text-sm font-semibold">{network}</div>
                      </button>
                    ))}
                  </div>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="phone">Phone Number</Label>
                  <Input 
                    id="phone" 
                    type="tel" 
                    placeholder="08012345678" 
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                  />
                </div>

                <div className="space-y-2">
                  <Label>Amount</Label>
                  <div className="grid grid-cols-3 gap-2">
                    {["100", "200", "500", "1000", "2000", "5000"].map((amountValue) => (
                      <Button 
                        key={amountValue} 
                        variant="outline"
                        onClick={() => setAmount(amountValue)}
                        className={amount === amountValue ? "border-primary bg-primary/5" : ""}
                      >
                        ₦{amountValue}
                      </Button>
                    ))}
                  </div>
                  <Input 
                    type="number" 
                    placeholder="Custom amount" 
                    value={amount}
                    onChange={(e) => setAmount(e.target.value)}
                    min="1"
                    step="0.01"
                  />
                </div>

                <Button 
                  onClick={handleAirtimePayment} 
                  className="w-full" 
                  size="lg"
                  disabled={billPaymentMutation.isPending || !selectedNetwork || !phoneNumber || !amount}
                >
                  {billPaymentMutation.isPending ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Processing...
                    </>
                  ) : (
                    "Buy Airtime"
                  )}
                </Button>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="data" className="space-y-4 mt-4">
            <Card>
              <CardContent className="pt-6 space-y-4">
                <div className="space-y-2">
                  <Label>Network Provider</Label>
                  <Select value={selectedProvider} onValueChange={setSelectedProvider}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select network" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="mtn">MTN</SelectItem>
                      <SelectItem value="airtel">Airtel</SelectItem>
                      <SelectItem value="glo">Glo</SelectItem>
                      <SelectItem value="9mobile">9mobile</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="dataPhone">Phone Number</Label>
                  <Input 
                    id="dataPhone" 
                    type="tel" 
                    placeholder="08012345678" 
                    value={phoneNumber}
                    onChange={(e) => setPhoneNumber(e.target.value)}
                  />
                </div>

                <div className="space-y-2">
                  <Label>Data Plan</Label>
                  <Select value={selectedPackage} onValueChange={setSelectedPackage}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select plan" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="1gb">1GB - ₦300 (30 days)</SelectItem>
                      <SelectItem value="2gb">2GB - ₦500 (30 days)</SelectItem>
                      <SelectItem value="5gb">5GB - ₦1,200 (30 days)</SelectItem>
                      <SelectItem value="10gb">10GB - ₦2,000 (30 days)</SelectItem>
                      <SelectItem value="20gb">20GB - ₦3,500 (30 days)</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                <Button 
                  onClick={handleDataPayment} 
                  className="w-full" 
                  size="lg"
                  disabled={billPaymentMutation.isPending || !selectedProvider || !phoneNumber || !selectedPackage}
                >
                  {billPaymentMutation.isPending ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Processing...
                    </>
                  ) : (
                    "Buy Data"
                  )}
                </Button>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="electricity" className="space-y-4 mt-4">
            <Card>
              <CardContent className="pt-6 space-y-4">
                <div className="space-y-2">
                  <Label>Disco Provider</Label>
                  <Select value={selectedProvider} onValueChange={setSelectedProvider}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select provider" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="ekedc">Eko Electric (EKEDC)</SelectItem>
                      <SelectItem value="ikedc">Ikeja Electric (IKEDC)</SelectItem>
                      <SelectItem value="aedc">Abuja Electric (AEDC)</SelectItem>
                      <SelectItem value="phed">Port Harcourt Electric (PHED)</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="meter">Meter Number</Label>
                  <Input 
                    id="meter" 
                    placeholder="Enter meter number" 
                    value={meterNumber}
                    onChange={(e) => setMeterNumber(e.target.value)}
                  />
                </div>

                <div className="space-y-2">
                  <Label htmlFor="electricAmount">Amount (₦)</Label>
                  <Input 
                    id="electricAmount" 
                    type="number" 
                    placeholder="0.00" 
                    value={amount}
                    onChange={(e) => setAmount(e.target.value)}
                    min="1"
                    step="0.01"
                  />
                </div>

                <Button 
                  onClick={handleElectricityPayment} 
                  className="w-full" 
                  size="lg"
                  disabled={billPaymentMutation.isPending || !selectedProvider || !meterNumber || !amount}
                >
                  {billPaymentMutation.isPending ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Processing...
                    </>
                  ) : (
                    "Pay Electricity Bill"
                  )}
                </Button>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="cable" className="space-y-4 mt-4">
            <Card>
              <CardContent className="pt-6 space-y-4">
                <div className="space-y-2">
                  <Label>Service Provider</Label>
                  <Select value={selectedProvider} onValueChange={setSelectedProvider}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select provider" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="dstv">DSTV</SelectItem>
                      <SelectItem value="gotv">GOTV</SelectItem>
                      <SelectItem value="startimes">Startimes</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                <div className="space-y-2">
                  <Label htmlFor="smartcard">Smartcard Number</Label>
                  <Input 
                    id="smartcard" 
                    placeholder="Enter smartcard number" 
                    value={smartcardNumber}
                    onChange={(e) => setSmartcardNumber(e.target.value)}
                  />
                </div>

                <div className="space-y-2">
                  <Label>Package</Label>
                  <Select value={selectedPackage} onValueChange={setSelectedPackage}>
                    <SelectTrigger>
                      <SelectValue placeholder="Select package" />
                    </SelectTrigger>
                    <SelectContent>
                      <SelectItem value="compact">Compact - ₦10,500</SelectItem>
                      <SelectItem value="premium">Premium - ₦24,500</SelectItem>
                      <SelectItem value="asia">Asia - ₦8,200</SelectItem>
                    </SelectContent>
                  </Select>
                </div>

                <Button 
                  onClick={handleCablePayment} 
                  className="w-full" 
                  size="lg"
                  disabled={billPaymentMutation.isPending || !selectedProvider || !smartcardNumber || !selectedPackage}
                >
                  {billPaymentMutation.isPending ? (
                    <>
                      <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      Processing...
                    </>
                  ) : (
                    "Subscribe"
                  )}
                </Button>
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      </div>
    </div>
  );
}
