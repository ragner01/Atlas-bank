import { useState, useEffect } from 'react';
import { isOnline } from '@/lib/api';

// Network status hook - Matching Phase 24 mobile app patterns
export const useNetworkStatus = () => {
  const [isOnlineStatus, setIsOnlineStatus] = useState(isOnline());

  useEffect(() => {
    const handleOnline = () => setIsOnlineStatus(true);
    const handleOffline = () => setIsOnlineStatus(false);

    window.addEventListener('online', handleOnline);
    window.addEventListener('offline', handleOffline);

    return () => {
      window.removeEventListener('online', handleOnline);
      window.removeEventListener('offline', handleOffline);
    };
  }, []);

  return isOnlineStatus;
};

// Offline indicator component - Matching Phase 24 mobile app
export const OfflineIndicator = () => {
  const isOnlineStatus = useNetworkStatus();

  if (isOnlineStatus) return null;

  return (
    <div className="bg-orange-500 text-white px-4 py-2 text-center font-semibold">
      📡 Offline Mode - Operations will be queued
    </div>
  );
};

