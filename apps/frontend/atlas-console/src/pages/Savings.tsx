import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ArrowLeft, Target, TrendingUp, Lock, Plus } from "lucide-react";
import { Link } from "react-router-dom";
import { Progress } from "@/components/ui/progress";

export default function Savings() {
  const savingsPlans = [
    {
      name: "Emergency Fund",
      target: 500000,
      current: 325000,
      icon: Target,
      color: "bg-blue-500",
    },
    {
      name: "Vacation 2025",
      target: 200000,
      current: 85000,
      icon: TrendingUp,
      color: "bg-purple-500",
    },
    {
      name: "New Laptop",
      target: 350000,
      current: 280000,
      icon: Lock,
      color: "bg-orange-500",
    },
  ];

  const calculateProgress = (current: number, target: number) => {
    return (current / target) * 100;
  };

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
        <div className="flex items-center gap-4 mb-6">
          <Link to="/">
            <Button variant="ghost" size="icon" className="text-white hover:bg-white/20">
              <ArrowLeft size={24} />
            </Button>
          </Link>
          <h1 className="text-2xl font-bold">Savings</h1>
        </div>

        <Card className="bg-white">
          <CardContent className="p-6">
            <div className="text-center">
              <p className="text-sm text-muted-foreground mb-1">Total Savings</p>
              <h2 className="text-4xl font-bold text-foreground mb-2">₦690,000</h2>
              <div className="flex items-center justify-center gap-2 text-success text-sm">
                <TrendingUp size={16} />
                <span>+12% this month</span>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      <div className="p-4 space-y-4">
        {/* Active Savings Plans */}
        <div>
          <h3 className="text-lg font-semibold mb-3">Active Plans</h3>
          <div className="space-y-3">
            {savingsPlans.map((plan, index) => {
              const Icon = plan.icon;
              const progress = calculateProgress(plan.current, plan.target);

              return (
                <Card key={index}>
                  <CardContent className="p-4">
                    <div className="flex items-start gap-3 mb-3">
                      <div className={`${plan.color} w-12 h-12 rounded-xl flex items-center justify-center`}>
                        <Icon size={24} className="text-white" />
                      </div>
                      <div className="flex-1">
                        <h4 className="font-semibold">{plan.name}</h4>
                        <p className="text-sm text-muted-foreground">
                          {formatAmount(plan.current)} of {formatAmount(plan.target)}
                        </p>
                      </div>
                      <Button size="sm" variant="outline">
                        Add
                      </Button>
                    </div>
                    <Progress value={progress} className="h-2" />
                    <p className="text-xs text-muted-foreground mt-2">
                      {progress.toFixed(0)}% completed
                    </p>
                  </CardContent>
                </Card>
              );
            })}
          </div>
        </div>

        {/* Create New Plan */}
        <Button className="w-full" size="lg" variant="outline">
          <Plus size={20} className="mr-2" />
          Create New Savings Plan
        </Button>

        {/* Savings Features */}
        <Card className="bg-primary/5 border-primary/20">
          <CardContent className="p-4 space-y-3">
            <h3 className="font-semibold">Savings Features</h3>
            <div className="space-y-2 text-sm">
              <div className="flex items-start gap-2">
                <div className="w-1.5 h-1.5 rounded-full bg-primary mt-1.5" />
                <p>Earn up to 15% interest per annum</p>
              </div>
              <div className="flex items-start gap-2">
                <div className="w-1.5 h-1.5 rounded-full bg-primary mt-1.5" />
                <p>Auto-save daily, weekly, or monthly</p>
              </div>
              <div className="flex items-start gap-2">
                <div className="w-1.5 h-1.5 rounded-full bg-primary mt-1.5" />
                <p>Lock funds to prevent early withdrawal</p>
              </div>
              <div className="flex items-start gap-2">
                <div className="w-1.5 h-1.5 rounded-full bg-primary mt-1.5" />
                <p>No minimum balance required</p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
