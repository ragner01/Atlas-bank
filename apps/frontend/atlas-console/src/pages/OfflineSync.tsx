import { useState } from "react";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ArrowLeft, Loader2, RefreshCw } from "lucide-react";
import { Link } from "react-router-dom";
import { toast } from "sonner";
import { useAuth } from "@/hooks/useAuth";
import { AtlasClient, AtlasApiError, generateDeviceId } from "@/lib/api";
import { useMutation, useQuery } from "@tanstack/react-query";
import { OfflineIndicator } from "@/hooks/useNetworkStatus";

export default function OfflineSync() {
  const [result, setResult] = useState<any>(null);
  
  const { user } = useAuth();
  const client = new AtlasClient();

  // Fetch queued operations
  const { data: queuedOps, isLoading: loadingOps } = useQuery({
    queryKey: ['offline-ops', user?.msisdn],
    queryFn: async () => {
      const deviceId = generateDeviceId();
      return client.offlineSync(deviceId, 50);
    },
    enabled: !!user,
    refetchInterval: 30000, // Refetch every 30 seconds
  });

  // Sync mutation
  const syncMutation = useMutation({
    mutationFn: async () => {
      const deviceId = generateDeviceId();
      return client.offlineSync(deviceId, 50);
    },
    onSuccess: (data) => {
      setResult(data);
      toast.success(`Synced ${data.synced} operations successfully`);
    },
    onError: (error: any) => {
      if (error instanceof AtlasApiError) {
        toast.error(`${error.message} (${error.code})`);
      } else {
        toast.error("Sync failed. Please try again.");
      }
    },
  });

  const handleSync = () => {
    syncMutation.mutate();
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
          <h1 className="text-2xl font-bold">Offline Sync</h1>
        </div>
      </div>

      <div className="p-4 space-y-4">
        <Card>
          <CardContent className="pt-6 space-y-4">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="font-semibold">Queued Operations</h3>
                <p className="text-sm text-muted-foreground">
                  {loadingOps ? "Loading..." : `${queuedOps?.results?.length || 0} operations pending`}
                </p>
              </div>
              <Button 
                onClick={handleSync} 
                disabled={syncMutation.isPending}
                size="sm"
              >
                {syncMutation.isPending ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <RefreshCw className="h-4 w-4" />
                )}
              </Button>
            </div>

            <Button 
              onClick={handleSync} 
              className="w-full" 
              size="lg"
              disabled={syncMutation.isPending}
            >
              {syncMutation.isPending ? (
                <>
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                  Syncing...
                </>
              ) : (
                "Sync Now"
              )}
            </Button>
          </CardContent>
        </Card>

        {/* Queued Operations List */}
        {queuedOps?.results && queuedOps.results.length > 0 && (
          <Card>
            <CardContent className="pt-6">
              <h3 className="font-semibold mb-4">Pending Operations</h3>
              <div className="space-y-2">
                {queuedOps.results.map((op: any, index: number) => (
                  <div key={index} className="flex items-center justify-between p-3 bg-secondary rounded-lg">
                    <div>
                      <p className="font-medium">{op.kind}</p>
                      <p className="text-sm text-muted-foreground">
                        {op.payload?.narration || 'No description'}
                      </p>
                    </div>
                    <div className="text-sm text-muted-foreground">
                      {op.payload?.Minor ? formatCurrency(op.payload.Minor) : ''}
                    </div>
                  </div>
                ))}
              </div>
            </CardContent>
          </Card>
        )}

        {/* Sync Results */}
        {result && (
          <Card>
            <CardContent className="pt-6">
              <h3 className="font-semibold mb-4">Sync Results</h3>
              <div className="bg-secondary p-4 rounded-lg">
                <pre className="text-sm overflow-auto">
                  {JSON.stringify(result, null, 2)}
                </pre>
              </div>
            </CardContent>
          </Card>
        )}

        <Card>
          <CardContent className="pt-6">
            <h3 className="font-semibold mb-2">How Offline Sync Works:</h3>
            <ol className="text-sm text-muted-foreground space-y-1">
              <li>1. Operations are queued when offline</li>
              <li>2. Each operation is cryptographically signed</li>
              <li>3. When online, sync processes all queued operations</li>
              <li>4. Operations are processed in order</li>
              <li>5. Results are returned for verification</li>
            </ol>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
