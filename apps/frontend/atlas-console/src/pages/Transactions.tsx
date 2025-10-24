import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ArrowLeft, Send, ArrowDownToLine, Filter } from "lucide-react";
import { Link } from "react-router-dom";

export default function Transactions() {
  const transactions = [
    {
      id: 1,
      name: "Salary Payment",
      type: "credit",
      amount: 250000,
      date: "Dec 22, 2024",
      time: "9:00 AM",
      status: "completed",
    },
    {
      id: 2,
      name: "Transfer to Ada Nnamdi",
      type: "debit",
      amount: 15000,
      date: "Dec 21, 2024",
      time: "4:30 PM",
      status: "completed",
    },
    {
      id: 3,
      name: "Jumia Nigeria",
      type: "debit",
      amount: 25500,
      date: "Dec 21, 2024",
      time: "2:15 PM",
      status: "completed",
    },
    {
      id: 4,
      name: "DSTV Subscription",
      type: "debit",
      amount: 8500,
      date: "Dec 20, 2024",
      time: "10:20 AM",
      status: "completed",
    },
    {
      id: 5,
      name: "MTN Airtime",
      type: "debit",
      amount: 500,
      date: "Dec 20, 2024",
      time: "8:45 AM",
      status: "completed",
    },
    {
      id: 6,
      name: "Refund from Shop XYZ",
      type: "credit",
      amount: 12000,
      date: "Dec 19, 2024",
      time: "3:00 PM",
      status: "completed",
    },
    {
      id: 7,
      name: "Electricity - EKEDC",
      type: "debit",
      amount: 5000,
      date: "Dec 18, 2024",
      time: "11:30 AM",
      status: "completed",
    },
    {
      id: 8,
      name: "Transfer from Emeka James",
      type: "credit",
      amount: 30000,
      date: "Dec 17, 2024",
      time: "5:45 PM",
      status: "completed",
    },
  ];

  const formatAmount = (amount: number) => {
    return new Intl.NumberFormat("en-NG", {
      style: "currency",
      currency: "NGN",
      minimumFractionDigits: 0,
    }).format(amount);
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
            <h1 className="text-2xl font-bold">Transactions</h1>
          </div>
          <Button variant="ghost" size="icon" className="text-white hover:bg-white/20">
            <Filter size={20} />
          </Button>
        </div>

        <Card className="bg-white">
          <CardContent className="p-4">
            <div className="flex justify-around text-center">
              <div>
                <p className="text-xs text-muted-foreground mb-1">Money In</p>
                <p className="text-lg font-bold text-success">+₦292,000</p>
              </div>
              <div className="h-12 w-px bg-border" />
              <div>
                <p className="text-xs text-muted-foreground mb-1">Money Out</p>
                <p className="text-lg font-bold text-destructive">-₦54,500</p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="p-4">
        {/* Transaction List */}
        <div className="space-y-2">
          {transactions.map((transaction) => (
            <Card key={transaction.id}>
              <CardContent className="p-4">
                <div className="flex items-center gap-3">
                  <div
                    className={`w-10 h-10 rounded-full flex items-center justify-center ${
                      transaction.type === "credit" ? "bg-success/10" : "bg-muted"
                    }`}
                  >
                    {transaction.type === "credit" ? (
                      <ArrowDownToLine size={18} className="text-success" />
                    ) : (
                      <Send size={18} className="text-muted-foreground" />
                    )}
                  </div>

                  <div className="flex-1">
                    <p className="font-medium">{transaction.name}</p>
                    <p className="text-xs text-muted-foreground">
                      {transaction.date} • {transaction.time}
                    </p>
                  </div>

                  <div className="text-right">
                    <p
                      className={`font-semibold ${
                        transaction.type === "credit" ? "text-success" : "text-foreground"
                      }`}
                    >
                      {transaction.type === "credit" ? "+" : "-"}
                      {formatAmount(transaction.amount)}
                    </p>
                    <p className="text-xs text-muted-foreground capitalize">{transaction.status}</p>
                  </div>
                </div>
              </CardContent>
            </Card>
          ))}
        </div>
      </div>
    </div>
  );
}
