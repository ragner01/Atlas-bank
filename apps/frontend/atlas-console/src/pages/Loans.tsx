import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Progress } from "@/components/ui/progress";
import { ArrowLeft, TrendingUp, CheckCircle2, Clock } from "lucide-react";
import { Link } from "react-router-dom";
import { useState } from "react";
import { toast } from "sonner";

export default function Loans() {
  const [loanAmount, setLoanAmount] = useState("50000");
  const [duration, setDuration] = useState("3");

  const calculateInterest = () => {
    const amount = Number(loanAmount);
    const months = Number(duration);
    const rate = 0.05; // 5% per month
    return Math.round(amount * rate * months);
  };

  const handleApply = () => {
    toast.success("Loan application submitted! We'll review shortly.");
  };

  const creditScore = 720;
  const loanLimit = 500000;

  const activeLoans = [
    {
      id: 1,
      amount: 100000,
      remaining: 45000,
      dueDate: "Jan 15, 2025",
      status: "active",
    },
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
          <h1 className="text-2xl font-bold">Loans & Credit</h1>
        </div>

        {/* Credit Score Card */}
        <Card className="bg-white">
          <CardContent className="p-6">
            <div className="flex items-center justify-between mb-4">
              <div>
                <p className="text-sm text-muted-foreground mb-1">Credit Score</p>
                <h2 className="text-4xl font-bold text-foreground">{creditScore}</h2>
                <p className="text-sm text-success mt-1">Excellent</p>
              </div>
              <div className="text-right">
                <p className="text-sm text-muted-foreground mb-1">Available Credit</p>
                <p className="text-2xl font-bold text-foreground">₦{loanLimit.toLocaleString()}</p>
              </div>
            </div>
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <TrendingUp size={16} className="text-success" />
              <span>+15 points this month</span>
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="p-4 space-y-4">
        {/* Active Loans */}
        {activeLoans.length > 0 && (
          <div>
            <h3 className="text-sm font-semibold mb-3 text-muted-foreground">ACTIVE LOANS</h3>
            {activeLoans.map((loan) => {
              const progress = ((loan.amount - loan.remaining) / loan.amount) * 100;
              return (
                <Card key={loan.id} className="mb-3">
                  <CardContent className="p-4">
                    <div className="flex justify-between items-start mb-3">
                      <div>
                        <p className="font-semibold">Loan #{loan.id}</p>
                        <p className="text-sm text-muted-foreground">Due: {loan.dueDate}</p>
                      </div>
                      <span className="bg-warning/10 text-warning px-3 py-1 rounded-full text-xs font-medium">
                        Active
                      </span>
                    </div>
                    <div className="space-y-2">
                      <div className="flex justify-between text-sm">
                        <span className="text-muted-foreground">Amount Borrowed</span>
                        <span className="font-semibold">₦{loan.amount.toLocaleString()}</span>
                      </div>
                      <div className="flex justify-between text-sm">
                        <span className="text-muted-foreground">Remaining</span>
                        <span className="font-semibold text-destructive">₦{loan.remaining.toLocaleString()}</span>
                      </div>
                      <Progress value={progress} className="h-2" />
                    </div>
                    <Button className="w-full mt-3" size="sm">
                      Make Repayment
                    </Button>
                  </CardContent>
                </Card>
              );
            })}
          </div>
        )}

        {/* Loan Calculator */}
        <Card>
          <CardContent className="pt-6 space-y-4">
            <h3 className="font-semibold text-lg">Apply for Quick Loan</h3>

            <div className="space-y-2">
              <Label htmlFor="amount">Loan Amount (₦)</Label>
              <Input
                id="amount"
                type="number"
                value={loanAmount}
                onChange={(e) => setLoanAmount(e.target.value)}
                className="text-2xl font-bold"
              />
              <p className="text-xs text-muted-foreground">Maximum: ₦{loanLimit.toLocaleString()}</p>
            </div>

            <div className="space-y-2">
              <Label htmlFor="duration">Duration (Months)</Label>
              <div className="grid grid-cols-4 gap-2">
                {[1, 3, 6, 12].map((months) => (
                  <Button
                    key={months}
                    variant={duration === String(months) ? "default" : "outline"}
                    onClick={() => setDuration(String(months))}
                    size="sm"
                  >
                    {months}M
                  </Button>
                ))}
              </div>
            </div>

            <div className="bg-secondary p-4 rounded-lg space-y-2">
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Loan Amount</span>
                <span className="font-semibold">₦{Number(loanAmount).toLocaleString()}</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Interest (5%/month)</span>
                <span className="font-semibold">₦{calculateInterest().toLocaleString()}</span>
              </div>
              <div className="flex justify-between text-sm">
                <span className="text-muted-foreground">Duration</span>
                <span className="font-semibold">{duration} months</span>
              </div>
              <div className="pt-2 border-t border-border flex justify-between">
                <span className="font-semibold">Total Repayment</span>
                <span className="font-bold text-lg">
                  ₦{(Number(loanAmount) + calculateInterest()).toLocaleString()}
                </span>
              </div>
              <p className="text-xs text-muted-foreground">
                Monthly: ₦{Math.round((Number(loanAmount) + calculateInterest()) / Number(duration)).toLocaleString()}
              </p>
            </div>

            <Button onClick={handleApply} className="w-full" size="lg">
              Apply for Loan
            </Button>
          </CardContent>
        </Card>

        {/* Loan Benefits */}
        <Card className="bg-primary/5 border-primary/20">
          <CardContent className="p-4 space-y-3">
            <h3 className="font-semibold">Why Choose Our Loans?</h3>
            <div className="space-y-2">
              {[
                "Instant approval in 5 minutes",
                "Flexible repayment plans",
                "No collateral required",
                "Competitive interest rates",
                "Build your credit score",
              ].map((benefit, index) => (
                <div key={index} className="flex items-start gap-2">
                  <CheckCircle2 size={16} className="text-success mt-0.5 flex-shrink-0" />
                  <span className="text-sm">{benefit}</span>
                </div>
              ))}
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
