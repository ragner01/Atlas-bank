import React, { useEffect, useState, useCallback, useRef } from "react";
import { NavigationContainer } from "@react-navigation/native";
import { createBottomTabNavigator } from "@react-navigation/bottom-tabs";
import { createNativeStackNavigator } from "@react-navigation/native-stack";
import { 
  View, 
  Text, 
  TextInput, 
  TouchableOpacity, 
  Alert, 
  ScrollView, 
  Image, 
  ActivityIndicator,
  StyleSheet,
  StatusBar,
  KeyboardAvoidingView,
  Platform,
  Dimensions,
  SafeAreaView,
  RefreshControl,
  Animated,
  Easing
} from "react-native";
import * as LocalAuth from "expo-local-authentication";
import * as SecureStore from "expo-secure-store";
import * as Crypto from "expo-crypto";
import * as Network from "expo-network";
import { v4 as uuidv4 } from "uuid";
import { LinearGradient } from 'expo-linear-gradient';

const { width, height } = Dimensions.get('window');
const Tab = createBottomTabNavigator();
const Stack = createNativeStackNavigator();

// Design System Colors
const Colors = {
  primary: '#007BFF',
  primaryDark: '#0056CC',
  secondary: '#00C853',
  accent: '#FF6B35',
  success: '#4CAF50',
  warning: '#FF9800',
  error: '#F44336',
  background: '#F8F9FA',
  backgroundDark: '#121212',
  surface: '#FFFFFF',
  surfaceDark: '#1E1E1E',
  text: '#212529',
  textDark: '#FFFFFF',
  textSecondary: '#6C757D',
  textSecondaryDark: '#B0B0B0',
  border: '#E9ECEF',
  borderDark: '#333333',
  shadow: 'rgba(0, 0, 0, 0.1)',
  shadowDark: 'rgba(0, 0, 0, 0.3)',
};

// Typography
const Typography = {
  h1: { fontSize: 28, fontWeight: '700' as const, lineHeight: 34 },
  h2: { fontSize: 24, fontWeight: '600' as const, lineHeight: 30 },
  h3: { fontSize: 20, fontWeight: '600' as const, lineHeight: 26 },
  h4: { fontSize: 18, fontWeight: '600' as const, lineHeight: 24 },
  body1: { fontSize: 16, fontWeight: '400' as const, lineHeight: 22 },
  body2: { fontSize: 14, fontWeight: '400' as const, lineHeight: 20 },
  caption: { fontSize: 12, fontWeight: '400' as const, lineHeight: 16 },
  button: { fontSize: 16, fontWeight: '600' as const, lineHeight: 22 },
};

// Spacing
const Spacing = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
  xxl: 48,
};

// Border Radius
const BorderRadius = {
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  xxl: 24,
  round: 50,
};

// Theme Context
const ThemeContext = React.createContext({
  isDark: false,
  colors: Colors,
  typography: Typography,
  spacing: Spacing,
  borderRadius: BorderRadius,
});

// Custom Hooks
const useTheme = () => React.useContext(ThemeContext);
const useRealtimeBalance = (msisdn: string | null) => {
  const [balanceJson, setBalanceJson] = useState<any>(null);
  const [isConnected, setIsConnected] = useState(false);
  const wsRef = useRef<WebSocket | null>(null);

  useEffect(() => {
    if (!msisdn) return;

    const connectWebSocket = () => {
      try {
        const wsBase = process.env.EXPO_PUBLIC_REALTIME_BASE || "http://localhost:5851";
        const wsUrl = wsBase.replace("http", "ws") + "/ws";
        
        const ws = new WebSocket(wsUrl);
        wsRef.current = ws;

        ws.onopen = () => {
          console.log("WebSocket connected");
          setIsConnected(true);
          
          const subscribeMessage = {
            type: "invoke",
            method: "SubscribeBalance",
            args: [`msisdn::${msisdn}`]
          };
          ws.send(JSON.stringify(subscribeMessage));
        };

        ws.onmessage = (event) => {
          try {
            const message = JSON.parse(event.data);
            
            if (message?.type === "message" && message?.target === "balanceUpdate") {
              const payload = JSON.parse(message.arguments[0]);
              console.log("Received balance update:", payload);
              
              setBalanceJson((prev: any) => ({
                ...(prev || {}),
                availableMinor: payload.minor,
                currency: payload.currency,
                pendingMinor: payload.pendingMinor || 0
              }));
            }
          } catch (error) {
            console.error("Error parsing WebSocket message:", error);
          }
        };

        ws.onclose = () => {
          console.log("WebSocket disconnected");
          setIsConnected(false);
          setTimeout(connectWebSocket, 3000);
        };

        ws.onerror = (error) => {
          console.error("WebSocket error:", error);
          setIsConnected(false);
        };
      } catch (error) {
        console.error("Error connecting WebSocket:", error);
        setIsConnected(false);
      }
    };

    const fetchInitialBalance = async () => {
      try {
        const ledgerBase = process.env.EXPO_PUBLIC_LEDGER_BASE || "http://localhost:6181";
        const response = await fetch(
          `${ledgerBase}/ledger/accounts/${encodeURIComponent(`msisdn::${msisdn}`)}/balance/global?currency=NGN`
        );
        
        if (response.ok) {
          const balance = await response.json();
          setBalanceJson(balance);
        }
      } catch (error) {
        console.error("Error fetching initial balance:", error);
      }
    };

    fetchInitialBalance();
    connectWebSocket();

    return () => {
      if (wsRef.current) {
        wsRef.current.close();
        wsRef.current = null;
      }
    };
  }, [msisdn]);

  return { balanceJson, isConnected };
};

// Animation Hook
const useAnimatedValue = (initialValue: number = 0) => {
  const animatedValue = useRef(new Animated.Value(initialValue)).current;
  
  const animateTo = useCallback((toValue: number, duration: number = 300) => {
    Animated.timing(animatedValue, {
      toValue,
      duration,
      easing: Easing.out(Easing.cubic),
      useNativeDriver: true,
    }).start();
  }, [animatedValue]);

  return { animatedValue, animateTo };
};

// Shimmer Loading Component
const ShimmerLoading = ({ width, height, borderRadius = 8 }: { width: number, height: number, borderRadius?: number }) => {
  const shimmerAnimation = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    const animation = Animated.loop(
      Animated.timing(shimmerAnimation, {
        toValue: 1,
        duration: 1000,
        easing: Easing.linear,
        useNativeDriver: true,
      })
    );
    animation.start();
    return () => animation.stop();
  }, []);

  const translateX = shimmerAnimation.interpolate({
    inputRange: [0, 1],
    outputRange: [-width, width],
  });

  return (
    <View style={[styles.shimmerContainer, { width, height, borderRadius }]}>
      <Animated.View
        style={[
          styles.shimmer,
          {
            transform: [{ translateX }],
          },
        ]}
      />
    </View>
  );
};

// Balance Card Component
const BalanceCard = ({ balance, showBalance, onToggleBalance, isConnected, theme }: any) => {
  const { colors, typography, spacing, borderRadius } = theme;
  const balanceAnimation = useAnimatedValue();
  const [displayBalance, setDisplayBalance] = useState(0);

  useEffect(() => {
    if (balance !== undefined) {
      const targetBalance = balance / 100;
      const duration = Math.min(2000, Math.abs(targetBalance - displayBalance) * 10);
      
      Animated.timing(balanceAnimation.animatedValue, {
        toValue: targetBalance,
        duration,
        easing: Easing.out(Easing.cubic),
        useNativeDriver: false,
      }).start();

      const listener = balanceAnimation.animatedValue.addListener(({ value }) => {
        setDisplayBalance(value);
      });

      return () => balanceAnimation.animatedValue.removeListener(listener);
    }
  }, [balance]);

  const formatCurrency = (amount: number): string => {
    return new Intl.NumberFormat("en-NG", {
      style: "currency",
      currency: "NGN",
    }).format(amount);
  };

  return (
    <View style={[styles.balanceCard, { backgroundColor: colors.surface }]}>
      <LinearGradient
        colors={[colors.primary, colors.secondary]}
        start={{ x: 0, y: 0 }}
        end={{ x: 1, y: 1 }}
        style={[styles.balanceGradient, { borderRadius: borderRadius.xl }]}
      >
        <View style={styles.balanceContent}>
          <View style={styles.balanceHeader}>
            <Text style={[styles.balanceLabel, { color: colors.textDark }]}>
              Total Balance
            </Text>
            <TouchableOpacity 
              style={styles.eyeButton}
              onPress={onToggleBalance}
            >
              <Text style={styles.eyeIcon}>
                {showBalance ? '👁️' : '🙈'}
              </Text>
            </TouchableOpacity>
          </View>
          
          <Text style={[styles.balanceAmount, { color: colors.textDark }]}>
            {showBalance ? formatCurrency(displayBalance) : "₦•••••••"}
          </Text>
          
          <View style={styles.balanceFooter}>
            <View style={styles.connectionIndicator}>
              <View style={[
                styles.connectionDot, 
                { backgroundColor: isConnected ? colors.success : colors.error }
              ]} />
              <Text style={[styles.connectionText, { color: colors.textDark }]}>
                {isConnected ? 'Live' : 'Offline'}
              </Text>
            </View>
          </View>
        </View>
      </LinearGradient>
    </View>
  );
};

// Service Grid Component
const ServiceGrid = ({ services, onServicePress, theme }: any) => {
  const { colors, spacing, borderRadius } = theme;

  return (
    <View style={styles.serviceGrid}>
      {services.map((service: any, index: number) => (
        <TouchableOpacity
          key={service.id}
          style={[styles.serviceItem, { backgroundColor: colors.surface }]}
          onPress={() => onServicePress(service)}
          activeOpacity={0.7}
        >
          <View style={[styles.serviceIcon, { backgroundColor: service.color }]}>
            <Text style={styles.serviceEmoji}>{service.icon}</Text>
          </View>
          <Text style={[styles.serviceLabel, { color: colors.text }]}>
            {service.label}
          </Text>
        </TouchableOpacity>
      ))}
    </View>
  );
};

// Transaction Item Component
const TransactionItem = ({ transaction, theme }: any) => {
  const { colors, typography } = theme;

  return (
    <View style={[styles.transactionItem, { backgroundColor: colors.surface }]}>
      <View style={styles.transactionIcon}>
        <Text style={styles.transactionEmoji}>
          {transaction.type === 'credit' ? '📈' : '📉'}
        </Text>
      </View>
      <View style={styles.transactionDetails}>
        <Text style={[styles.transactionTitle, { color: colors.text }]}>
          {transaction.title}
        </Text>
        <Text style={[styles.transactionSubtitle, { color: colors.textSecondary }]}>
          {transaction.subtitle}
        </Text>
      </View>
      <Text style={[
        styles.transactionAmount,
        { color: transaction.type === 'credit' ? colors.success : colors.error }
      ]}>
        {transaction.type === 'credit' ? '+' : '-'}{transaction.amount}
      </Text>
    </View>
  );
};

// Quick Actions Component
const QuickActions = ({ actions, onActionPress, theme }: any) => {
  const { colors, spacing, borderRadius } = theme;

  return (
    <View style={styles.quickActions}>
      {actions.map((action: any, index: number) => (
        <TouchableOpacity
          key={action.id}
          style={[styles.quickActionItem, { backgroundColor: colors.surface }]}
          onPress={() => onActionPress(action)}
          activeOpacity={0.7}
        >
          <View style={[styles.quickActionIcon, { backgroundColor: action.color }]}>
            <Text style={styles.quickActionEmoji}>{action.icon}</Text>
          </View>
          <Text style={[styles.quickActionLabel, { color: colors.text }]}>
            {action.label}
          </Text>
        </TouchableOpacity>
      ))}
    </View>
  );
};

// Main Home Screen
function HomeScreen({ navigation }: any) {
  const [msisdn, setMsisdn] = useState<string | null>(null);
  const [showBalance, setShowBalance] = useState(true);
  const [user, setUser] = useState<any>(null);
  const [refreshing, setRefreshing] = useState(false);
  const [isDark, setIsDark] = useState(false);
  
  const theme = {
    isDark,
    colors: isDark ? Colors : Colors,
    typography: Typography,
    spacing: Spacing,
    borderRadius: BorderRadius,
  };
  
  const { balanceJson, isConnected } = useRealtimeBalance(msisdn);

  useEffect(() => {
    const loadUser = async () => {
      try {
        const storedMsisdn = await SecureStore.getItemAsync("msisdn");
        const storedPin = await SecureStore.getItemAsync("pin");
        const storedDeviceId = await SecureStore.getItemAsync("deviceId");
        
        if (storedMsisdn && storedPin && storedDeviceId) {
          setMsisdn(storedMsisdn);
          setUser({
            msisdn: storedMsisdn,
            pin: storedPin,
            deviceId: storedDeviceId,
            firstName: 'John',
            lastName: 'Doe',
            email: 'john.doe@example.com'
          });
        }
      } catch (error) {
        console.error("Failed to load user:", error);
      }
    };

    loadUser();
  }, []);

  const onRefresh = useCallback(async () => {
    setRefreshing(true);
    // Simulate refresh delay
    setTimeout(() => setRefreshing(false), 1000);
  }, []);

  const services = [
    { id: 'airtime', icon: '📱', label: 'Airtime', color: '#007BFF' },
    { id: 'electricity', icon: '⚡', label: 'Electricity', color: '#FF9800' },
    { id: 'bills', icon: '📄', label: 'Bills', color: '#4CAF50' },
    { id: 'loans', icon: '💰', label: 'Loans', color: '#9C27B0' },
    { id: 'cards', icon: '💳', label: 'Cards', color: '#FF5722' },
    { id: 'savings', icon: '🏦', label: 'Savings', color: '#00BCD4' },
  ];

  const quickActions = [
    { id: 'send', icon: '💸', label: 'Send', color: '#007BFF' },
    { id: 'cashin', icon: '📥', label: 'Cash In', color: '#4CAF50' },
    { id: 'cashout', icon: '📤', label: 'Cash Out', color: '#FF9800' },
    { id: 'request', icon: '🤝', label: 'Request', color: '#9C27B0' },
  ];

  const recentTransactions = [
    { id: '1', type: 'credit', title: 'Salary Payment', subtitle: 'Today, 2:30 PM', amount: '₦50,000' },
    { id: '2', type: 'debit', title: 'Transfer to Jane', subtitle: 'Yesterday, 4:15 PM', amount: '₦5,000' },
    { id: '3', type: 'debit', title: 'Airtime Purchase', subtitle: 'Yesterday, 1:20 PM', amount: '₦1,000' },
    { id: '4', type: 'credit', title: 'Cash In', subtitle: '2 days ago, 10:30 AM', amount: '₦10,000' },
  ];

  const handleServicePress = (service: any) => {
    navigation.navigate('Services', { service });
  };

  const handleQuickActionPress = (action: any) => {
    if (action.id === 'send') {
      navigation.navigate('Transfer');
    } else {
      Alert.alert('Coming Soon', `${action.label} feature will be available soon!`);
    }
  };

  const getDisplayName = () => {
    if (!user) return "User";
    return `${user.firstName} ${user.lastName}`;
  };

  const getInitials = () => {
    if (!user) return "U";
    return `${user.firstName.charAt(0)}${user.lastName.charAt(0)}`;
  };

  return (
    <ThemeContext.Provider value={theme}>
      <SafeAreaView style={[styles.container, { backgroundColor: theme.colors.background }]}>
        <StatusBar barStyle={isDark ? "light-content" : "dark-content"} />
        
        <ScrollView
          style={styles.scrollView}
          refreshControl={
            <RefreshControl refreshing={refreshing} onRefresh={onRefresh} />
          }
          showsVerticalScrollIndicator={false}
        >
          {/* Header */}
          <View style={styles.header}>
            <View style={styles.headerContent}>
              <View>
                <Text style={[styles.greeting, { color: theme.colors.textSecondary }]}>
                  Good {new Date().getHours() < 12 ? 'morning' : new Date().getHours() < 18 ? 'afternoon' : 'evening'}
                </Text>
                <Text style={[styles.userName, { color: theme.colors.text }]}>
                  {getDisplayName()}
                </Text>
              </View>
              <View style={styles.headerActions}>
                <TouchableOpacity style={styles.notificationButton}>
                  <Text style={styles.notificationIcon}>🔔</Text>
                  <View style={styles.notificationBadge}>
                    <Text style={styles.notificationBadgeText}>3</Text>
                  </View>
                </TouchableOpacity>
                <TouchableOpacity 
                  style={styles.themeToggle}
                  onPress={() => setIsDark(!isDark)}
                >
                  <Text style={styles.themeIcon}>{isDark ? '☀️' : '🌙'}</Text>
                </TouchableOpacity>
                <View style={[styles.avatar, { backgroundColor: theme.colors.primary }]}>
                  <Text style={styles.avatarText}>{getInitials()}</Text>
                </View>
              </View>
            </View>
          </View>

          {/* Balance Card */}
          <BalanceCard
            balance={balanceJson?.availableMinor}
            showBalance={showBalance}
            onToggleBalance={() => setShowBalance(!showBalance)}
            isConnected={isConnected}
            theme={theme}
          />

          {/* Quick Actions */}
          <View style={styles.section}>
            <Text style={[styles.sectionTitle, { color: theme.colors.text }]}>
              Quick Actions
            </Text>
            <QuickActions
              actions={quickActions}
              onActionPress={handleQuickActionPress}
              theme={theme}
            />
          </View>

          {/* Services Grid */}
          <View style={styles.section}>
            <Text style={[styles.sectionTitle, { color: theme.colors.text }]}>
              Services
            </Text>
            <ServiceGrid
              services={services}
              onServicePress={handleServicePress}
              theme={theme}
            />
          </View>

          {/* Recent Transactions */}
          <View style={styles.section}>
            <View style={styles.sectionHeader}>
              <Text style={[styles.sectionTitle, { color: theme.colors.text }]}>
                Recent Transactions
              </Text>
              <TouchableOpacity onPress={() => navigation.navigate('Transactions')}>
                <Text style={[styles.seeAllText, { color: theme.colors.primary }]}>
                  See All
                </Text>
              </TouchableOpacity>
            </View>
            <View style={[styles.transactionsList, { backgroundColor: theme.colors.surface }]}>
              {recentTransactions.map((transaction) => (
                <TransactionItem
                  key={transaction.id}
                  transaction={transaction}
                  theme={theme}
                />
              ))}
            </View>
          </View>
        </ScrollView>
      </SafeAreaView>
    </ThemeContext.Provider>
  );
}

// Styles
const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  scrollView: {
    flex: 1,
  },
  header: {
    paddingHorizontal: Spacing.lg,
    paddingTop: Spacing.md,
    paddingBottom: Spacing.lg,
  },
  headerContent: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
  },
  greeting: {
    ...Typography.body2,
    opacity: 0.8,
  },
  userName: {
    ...Typography.h3,
    marginTop: Spacing.xs,
  },
  headerActions: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
  },
  notificationButton: {
    position: 'relative',
    padding: Spacing.sm,
  },
  notificationIcon: {
    fontSize: 20,
  },
  notificationBadge: {
    position: 'absolute',
    top: 4,
    right: 4,
    backgroundColor: Colors.error,
    borderRadius: BorderRadius.round,
    width: 16,
    height: 16,
    justifyContent: 'center',
    alignItems: 'center',
  },
  notificationBadgeText: {
    color: Colors.textDark,
    fontSize: 10,
    fontWeight: '600' as const,
  },
  themeToggle: {
    padding: Spacing.sm,
  },
  themeIcon: {
    fontSize: 20,
  },
  avatar: {
    width: 40,
    height: 40,
    borderRadius: BorderRadius.round,
    justifyContent: 'center',
    alignItems: 'center',
  },
  avatarText: {
    color: Colors.textDark,
    ...Typography.button,
  },
  balanceCard: {
    marginHorizontal: Spacing.lg,
    marginBottom: Spacing.lg,
    borderRadius: BorderRadius.xl,
    shadowColor: Colors.shadow,
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.1,
    shadowRadius: 12,
    elevation: 8,
  },
  balanceGradient: {
    padding: Spacing.lg,
  },
  balanceContent: {
    alignItems: 'center',
  },
  balanceHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    width: '100%',
    marginBottom: Spacing.md,
  },
  balanceLabel: {
    ...Typography.body2,
    opacity: 0.9,
  },
  eyeButton: {
    padding: Spacing.sm,
  },
  eyeIcon: {
    fontSize: 20,
  },
  balanceAmount: {
    ...Typography.h1,
    fontWeight: '700',
    marginBottom: Spacing.md,
  },
  balanceFooter: {
    width: '100%',
    alignItems: 'center',
  },
  connectionIndicator: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: Spacing.sm,
  },
  connectionDot: {
    width: 8,
    height: 8,
    borderRadius: BorderRadius.round,
  },
  connectionText: {
    ...Typography.caption,
    opacity: 0.9,
  },
  section: {
    marginHorizontal: Spacing.lg,
    marginBottom: Spacing.lg,
  },
  sectionHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.md,
  },
  sectionTitle: {
    ...Typography.h4,
  },
  seeAllText: {
    ...Typography.body2,
    fontWeight: '500' as const,
  },
  quickActions: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    gap: Spacing.sm,
  },
  quickActionItem: {
    flex: 1,
    alignItems: 'center',
    padding: Spacing.md,
    borderRadius: BorderRadius.lg,
    shadowColor: Colors.shadow,
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
  },
  quickActionIcon: {
    width: 48,
    height: 48,
    borderRadius: BorderRadius.lg,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: Spacing.sm,
  },
  quickActionEmoji: {
    fontSize: 24,
  },
  quickActionLabel: {
    ...Typography.caption,
    fontWeight: '500',
    textAlign: 'center',
  },
  serviceGrid: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: Spacing.md,
  },
  serviceItem: {
    width: (width - Spacing.lg * 2 - Spacing.md * 2) / 3,
    alignItems: 'center',
    padding: Spacing.md,
    borderRadius: BorderRadius.lg,
    shadowColor: Colors.shadow,
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
  },
  serviceIcon: {
    width: 56,
    height: 56,
    borderRadius: BorderRadius.lg,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: Spacing.sm,
  },
  serviceEmoji: {
    fontSize: 28,
  },
  serviceLabel: {
    ...Typography.caption,
    fontWeight: '500',
    textAlign: 'center',
  },
  transactionsList: {
    borderRadius: BorderRadius.lg,
    shadowColor: Colors.shadow,
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
  },
  transactionItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
  },
  transactionIcon: {
    width: 40,
    height: 40,
    borderRadius: BorderRadius.md,
    backgroundColor: Colors.background,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: Spacing.md,
  },
  transactionEmoji: {
    fontSize: 20,
  },
  transactionDetails: {
    flex: 1,
  },
  transactionTitle: {
    ...Typography.body2,
    fontWeight: '500',
    marginBottom: Spacing.xs,
  },
  transactionSubtitle: {
    ...Typography.caption,
  },
  transactionAmount: {
    ...Typography.body2,
    fontWeight: '600' as const,
  },
  shimmerContainer: {
    backgroundColor: Colors.border,
    overflow: 'hidden',
  },
  shimmer: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: 'rgba(255, 255, 255, 0.6)',
  },
});

export default HomeScreen;
