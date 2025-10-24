import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { Label } from '@/components/ui/label';
import { Eye, EyeOff, Smartphone, Lock } from 'lucide-react';
import { useAuth } from '@/hooks/useAuth';
import { toast } from 'sonner';
import { useNavigate } from 'react-router-dom';

export default function Login() {
  const [msisdn, setMsisdn] = useState('');
  const [pin, setPin] = useState('');
  const [showPin, setShowPin] = useState(false);
  const [isLoading, setIsLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    
    if (!msisdn || !pin) {
      toast.error('Please fill in all fields');
      return;
    }

    if (msisdn.length < 10) {
      toast.error('Please enter a valid phone number');
      return;
    }

    if (pin.length < 4) {
      toast.error('PIN must be at least 4 digits');
      return;
    }

    try {
      setIsLoading(true);
      await login(msisdn, pin);
      toast.success('Login successful!');
      navigate('/');
    } catch (error: any) {
      toast.error(error.response?.data?.message || 'Login failed. Please try again.');
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-primary/20 via-secondary to-primary/10 flex items-center justify-center p-4">
      <Card className="w-full max-w-md">
        <CardHeader className="text-center">
          <div className="mx-auto w-16 h-16 bg-primary rounded-full flex items-center justify-center mb-4">
            <span className="text-2xl font-bold text-primary-foreground">A</span>
          </div>
          <CardTitle className="text-2xl font-bold">AtlasBank Console</CardTitle>
          <p className="text-muted-foreground">Sign in to your account</p>
        </CardHeader>
        <CardContent>
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-2">
              <Label htmlFor="msisdn">Phone Number</Label>
              <div className="relative">
                <Smartphone className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                <Input
                  id="msisdn"
                  type="tel"
                  placeholder="08012345678"
                  value={msisdn}
                  onChange={(e) => setMsisdn(e.target.value)}
                  className="pl-10"
                  maxLength={15}
                />
              </div>
            </div>

            <div className="space-y-2">
              <Label htmlFor="pin">PIN</Label>
              <div className="relative">
                <Lock className="absolute left-3 top-3 h-4 w-4 text-muted-foreground" />
                <Input
                  id="pin"
                  type={showPin ? 'text' : 'password'}
                  placeholder="Enter your PIN"
                  value={pin}
                  onChange={(e) => setPin(e.target.value)}
                  className="pl-10 pr-10"
                  maxLength={6}
                />
                <button
                  type="button"
                  onClick={() => setShowPin(!showPin)}
                  className="absolute right-3 top-3 h-4 w-4 text-muted-foreground hover:text-foreground"
                >
                  {showPin ? <EyeOff size={16} /> : <Eye size={16} />}
                </button>
              </div>
            </div>

            <Button type="submit" className="w-full" size="lg" disabled={isLoading}>
              {isLoading ? 'Signing in...' : 'Sign In'}
            </Button>
          </form>

          <div className="mt-6 text-center">
            <p className="text-sm text-muted-foreground">
              Demo Credentials:
            </p>
            <p className="text-xs text-muted-foreground mt-1">
              Phone: 2348100000001 | PIN: 1234
            </p>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
