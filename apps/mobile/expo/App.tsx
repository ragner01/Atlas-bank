import React, { useEffect, useState, useRef } from "react";
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
  Animated,
  Easing,
} from "react-native";
import * as LocalAuth from "expo-local-authentication";
import * as SecureStore from "expo-secure-store";
import * as Crypto from "expo-crypto";
import * as Network from "expo-network";
import { v4 as uuidv4 } from "uuid";
import { LinearGradient } from 'expo-linear-gradient';

// Import our redesigned screens
import HomeScreen from './HomeScreen';
import TransferScreen from './TransferScreen';
import ServicesScreen from './ServicesScreen';
import BottomTabNavigator from './BottomTabNavigator';

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

const Spacing = {
  xs: 4,
  sm: 8,
  md: 16,
  lg: 24,
  xl: 32,
  xxl: 48,
};

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

// Splash Screen Component
const SplashScreen = ({ onFinish }: any) => {
  const fadeAnimation = useRef(new Animated.Value(0)).current;
  const scaleAnimation = useRef(new Animated.Value(0.8)).current;
  const logoAnimation = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    const startAnimations = () => {
      Animated.parallel([
        Animated.timing(fadeAnimation, {
          toValue: 1,
          duration: 1000,
          easing: Easing.out(Easing.cubic),
          useNativeDriver: true,
        }),
        Animated.spring(scaleAnimation, {
          toValue: 1,
          useNativeDriver: true,
          tension: 100,
          friction: 8,
        }),
        Animated.timing(logoAnimation, {
          toValue: 1,
          duration: 1500,
          easing: Easing.out(Easing.cubic),
          useNativeDriver: true,
        }),
      ]).start();
    };

    startAnimations();

    const timer = setTimeout(() => {
      onFinish();
    }, 3000);

    return () => clearTimeout(timer);
  }, []);

  return (
    <View style={styles.splashContainer}>
      <LinearGradient
        colors={[Colors.primary, Colors.secondary]}
        style={styles.splashGradient}
      >
        <Animated.View
          style={[
            styles.splashContent,
            {
              opacity: fadeAnimation,
              transform: [{ scale: scaleAnimation }],
            },
          ]}
        >
          <Animated.View
            style={[
              styles.logoContainer,
              {
                transform: [
                  {
                    translateY: logoAnimation.interpolate({
                      inputRange: [0, 1],
                      outputRange: [50, 0],
                    }),
                  },
                ],
              },
            ]}
          >
            <Text style={styles.logoEmoji}>🏦</Text>
            <Text style={styles.logoText}>AtlasBank</Text>
            <Text style={styles.logoSubtext}>Digital Banking</Text>
          </Animated.View>
          
          <Animated.View
            style={[
              styles.loadingContainer,
              {
                opacity: logoAnimation,
              },
            ]}
          >
            <ActivityIndicator size="large" color={Colors.textDark} />
            <Text style={styles.loadingText}>Loading...</Text>
          </Animated.View>
        </Animated.View>
      </LinearGradient>
    </View>
  );
};

// Login Screen Component
const LoginScreen = ({ onLogin }: any) => {
  const [msisdn, setMsisdn] = useState("2348100000001");
  const [pin, setPin] = useState("1234");
  const [loading, setLoading] = useState(false);
  const [isDark, setIsDark] = useState(false);
  const [showBiometric, setShowBiometric] = useState(false);

  const theme = {
    isDark,
    colors: isDark ? Colors : Colors,
    typography: Typography,
    spacing: Spacing,
    borderRadius: BorderRadius,
  };

  const slideAnimation = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    Animated.timing(slideAnimation, {
      toValue: 1,
      duration: 800,
      easing: Easing.out(Easing.cubic),
      useNativeDriver: true,
    }).start();

    checkBiometricAvailability();
  }, []);

  const checkBiometricAvailability = async () => {
    try {
      const hasHardware = await LocalAuth.hasHardwareAsync();
      const isEnrolled = await LocalAuth.isEnrolledAsync();
      setShowBiometric(hasHardware && isEnrolled);
    } catch (error) {
      console.error('Biometric check failed:', error);
    }
  };

  const handleLogin = async () => {
    setLoading(true);
    try {
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 1500));
      
      // Store auth details
      await SecureStore.setItemAsync("msisdn", msisdn);
      await SecureStore.setItemAsync("pin", pin);
      await SecureStore.setItemAsync("deviceId", uuidv4());
      
      onLogin();
    } catch (error) {
      Alert.alert('Error', 'Login failed. Please try again.');
    } finally {
      setLoading(false);
    }
  };

  const handleBiometricLogin = async () => {
    try {
      const result = await LocalAuth.authenticateAsync({
        promptMessage: 'Authenticate to login',
        fallbackLabel: 'Use PIN',
      });

      if (result.success) {
        handleLogin();
      }
    } catch (error) {
      console.error('Biometric authentication failed:', error);
    }
  };

  const translateY = slideAnimation.interpolate({
    inputRange: [0, 1],
    outputRange: [100, 0],
  });

  const opacity = slideAnimation.interpolate({
    inputRange: [0, 1],
    outputRange: [0, 1],
  });

  return (
    <SafeAreaView style={[styles.loginContainer, { backgroundColor: theme.colors.background }]}>
      <StatusBar barStyle={isDark ? "light-content" : "dark-content"} />
      
      <View style={styles.loginHeader}>
        <TouchableOpacity onPress={() => setIsDark(!isDark)} style={styles.themeToggle}>
          <Text style={styles.themeIcon}>{isDark ? '☀️' : '🌙'}</Text>
        </TouchableOpacity>
      </View>

      <Animated.View
        style={[
          styles.loginContent,
          {
            opacity,
            transform: [{ translateY }],
          },
        ]}
      >
        <View style={styles.loginLogo}>
          <Text style={styles.loginEmoji}>🏦</Text>
          <Text style={[styles.loginTitle, { color: theme.colors.text }]}>
            Welcome to AtlasBank
          </Text>
          <Text style={[styles.loginSubtitle, { color: theme.colors.textSecondary }]}>
            Your digital banking partner
          </Text>
        </View>

        <View style={styles.loginForm}>
          <View style={styles.inputGroup}>
            <Text style={[styles.inputLabel, { color: theme.colors.text }]}>
              Phone Number
            </Text>
            <TextInput
              style={[
                styles.input,
                {
                  backgroundColor: theme.colors.surface,
                  borderColor: theme.colors.border,
                  color: theme.colors.text,
                }
              ]}
              value={msisdn}
              onChangeText={setMsisdn}
              placeholder="2348100000001"
              placeholderTextColor={theme.colors.textSecondary}
              keyboardType="phone-pad"
            />
          </View>

          <View style={styles.inputGroup}>
            <Text style={[styles.inputLabel, { color: theme.colors.text }]}>
              PIN
            </Text>
            <TextInput
              style={[
                styles.input,
                {
                  backgroundColor: theme.colors.surface,
                  borderColor: theme.colors.border,
                  color: theme.colors.text,
                }
              ]}
              value={pin}
              onChangeText={setPin}
              placeholder="Enter your PIN"
              placeholderTextColor={theme.colors.textSecondary}
              secureTextEntry
              keyboardType="numeric"
            />
          </View>

          <TouchableOpacity
            style={styles.loginButton}
            onPress={handleLogin}
            disabled={loading}
          >
            <LinearGradient
              colors={[Colors.primary, Colors.secondary]}
              style={styles.loginButtonGradient}
            >
              {loading ? (
                <ActivityIndicator color={Colors.textDark} />
              ) : (
                <Text style={[styles.loginButtonText, { color: Colors.textDark }]}>
                  Login
                </Text>
              )}
            </LinearGradient>
          </TouchableOpacity>

          {showBiometric && (
            <TouchableOpacity
              style={styles.biometricButton}
              onPress={handleBiometricLogin}
            >
              <Text style={styles.biometricIcon}>👆</Text>
              <Text style={[styles.biometricText, { color: theme.colors.textSecondary }]}>
                Use Biometric
              </Text>
            </TouchableOpacity>
          )}
        </View>
      </Animated.View>
    </SafeAreaView>
  );
};

// Savings Screen Component
const SavingsScreen = ({ navigation }: any) => {
  const [isDark, setIsDark] = useState(false);

  const theme = {
    isDark,
    colors: isDark ? Colors : Colors,
    typography: Typography,
    spacing: Spacing,
    borderRadius: BorderRadius,
  };

  return (
    <SafeAreaView style={[styles.container, { backgroundColor: theme.colors.background }]}>
      <StatusBar barStyle={isDark ? "light-content" : "dark-content"} />
      
      <View style={styles.header}>
        <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
          <Text style={styles.backIcon}>←</Text>
        </TouchableOpacity>
        <Text style={[styles.headerTitle, { color: theme.colors.text }]}>
          Savings
        </Text>
        <TouchableOpacity onPress={() => setIsDark(!isDark)} style={styles.themeButton}>
          <Text style={styles.themeIcon}>{isDark ? '☀️' : '🌙'}</Text>
        </TouchableOpacity>
      </View>

      <ScrollView style={styles.content}>
        <View style={[styles.card, { backgroundColor: theme.colors.surface }]}>
          <Text style={[styles.cardTitle, { color: theme.colors.text }]}>
            Savings Account
          </Text>
          <Text style={[styles.cardSubtitle, { color: theme.colors.textSecondary }]}>
            Grow your money with competitive interest rates
          </Text>
          <TouchableOpacity style={styles.cardButton}>
            <LinearGradient
              colors={[Colors.primary, Colors.secondary]}
              style={styles.cardButtonGradient}
            >
              <Text style={[styles.cardButtonText, { color: Colors.textDark }]}>
                Open Account
              </Text>
            </LinearGradient>
          </TouchableOpacity>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
};

// Profile Screen Component
const ProfileScreen = ({ navigation }: any) => {
  const [isDark, setIsDark] = useState(false);

  const theme = {
    isDark,
    colors: isDark ? Colors : Colors,
    typography: Typography,
    spacing: Spacing,
    borderRadius: BorderRadius,
  };

  const handleLogout = async () => {
    Alert.alert(
      'Logout',
      'Are you sure you want to logout?',
      [
        { text: 'Cancel', style: 'cancel' },
        {
          text: 'Logout',
          style: 'destructive',
          onPress: async () => {
            try {
              await SecureStore.deleteItemAsync("msisdn");
              await SecureStore.deleteItemAsync("pin");
              await SecureStore.deleteItemAsync("deviceId");
              // Navigate back to login
            } catch (error) {
              console.error("Logout error:", error);
            }
          }
        }
      ]
    );
  };

  return (
    <SafeAreaView style={[styles.container, { backgroundColor: theme.colors.background }]}>
      <StatusBar barStyle={isDark ? "light-content" : "dark-content"} />
      
      <View style={styles.header}>
        <Text style={[styles.headerTitle, { color: theme.colors.text }]}>
          Profile
        </Text>
        <TouchableOpacity onPress={() => setIsDark(!isDark)} style={styles.themeButton}>
          <Text style={styles.themeIcon}>{isDark ? '☀️' : '🌙'}</Text>
        </TouchableOpacity>
      </View>

      <ScrollView style={styles.content}>
        <View style={[styles.profileCard, { backgroundColor: theme.colors.surface }]}>
          <View style={styles.profileAvatar}>
            <Text style={styles.profileAvatarText}>JD</Text>
          </View>
          <Text style={[styles.profileName, { color: theme.colors.text }]}>
            John Doe
          </Text>
          <Text style={[styles.profileEmail, { color: theme.colors.textSecondary }]}>
            john.doe@example.com
          </Text>
        </View>

        <View style={styles.menuSection}>
          <Text style={[styles.menuTitle, { color: theme.colors.text }]}>
            Account Settings
          </Text>
          
          {[
            { icon: '👤', title: 'Personal Information', onPress: () => Alert.alert('Coming Soon', 'Personal Information feature') },
            { icon: '🔒', title: 'Security Settings', onPress: () => Alert.alert('Coming Soon', 'Security Settings feature') },
            { icon: '🔔', title: 'Notifications', onPress: () => Alert.alert('Coming Soon', 'Notifications feature') },
            { icon: '❓', title: 'Help & Support', onPress: () => Alert.alert('Coming Soon', 'Help & Support feature') },
          ].map((item, index) => (
            <TouchableOpacity
              key={index}
              style={[styles.menuItem, { backgroundColor: theme.colors.surface }]}
              onPress={item.onPress}
            >
              <Text style={styles.menuIcon}>{item.icon}</Text>
              <Text style={[styles.menuText, { color: theme.colors.text }]}>
                {item.title}
              </Text>
              <Text style={[styles.menuArrow, { color: theme.colors.textSecondary }]}>
                →
              </Text>
            </TouchableOpacity>
          ))}
        </View>

        <TouchableOpacity
          style={[styles.logoutButton, { backgroundColor: Colors.error }]}
          onPress={handleLogout}
        >
          <Text style={[styles.logoutText, { color: Colors.textDark }]}>
            Logout
          </Text>
        </TouchableOpacity>
      </ScrollView>
    </SafeAreaView>
  );
};

// Main App Component
export default function App() {
  const [isLoading, setIsLoading] = useState(true);
  const [isLoggedIn, setIsLoggedIn] = useState(false);
  const [isDark, setIsDark] = useState(false);

  const theme = {
    isDark,
    colors: isDark ? Colors : Colors,
    typography: Typography,
    spacing: Spacing,
    borderRadius: BorderRadius,
  };

  useEffect(() => {
    const checkAuth = async () => {
      try {
        const msisdn = await SecureStore.getItemAsync("msisdn");
        const pin = await SecureStore.getItemAsync("pin");
        
        if (msisdn && pin) {
          setIsLoggedIn(true);
        }
      } catch (error) {
        console.error("Auth check failed:", error);
      } finally {
        setIsLoading(false);
      }
    };

    checkAuth();
  }, []);

  if (isLoading) {
    return <SplashScreen onFinish={() => setIsLoading(false)} />;
  }

  if (!isLoggedIn) {
    return (
      <ThemeContext.Provider value={theme}>
        <LoginScreen onLogin={() => setIsLoggedIn(true)} />
      </ThemeContext.Provider>
    );
  }

  return (
    <ThemeContext.Provider value={theme}>
      <NavigationContainer>
        <Tab.Navigator
          tabBar={(props) => <BottomTabNavigator {...props} theme={theme} />}
          screenOptions={{
            headerShown: false,
          }}
        >
          <Tab.Screen name="Home" component={HomeScreen} />
          <Tab.Screen name="Transfer" component={TransferScreen} />
          <Tab.Screen name="Services" component={ServicesScreen} />
          <Tab.Screen name="Savings" component={SavingsScreen} />
          <Tab.Screen name="Profile" component={ProfileScreen} />
        </Tab.Navigator>
      </NavigationContainer>
    </ThemeContext.Provider>
  );
}

// Styles
const styles = StyleSheet.create({
  container: {
    flex: 1,
  },
  splashContainer: {
    flex: 1,
  },
  splashGradient: {
    flex: 1,
    justifyContent: 'center',
    alignItems: 'center',
  },
  splashContent: {
    alignItems: 'center',
  },
  logoContainer: {
    alignItems: 'center',
    marginBottom: Spacing.xxl,
  },
  logoEmoji: {
    fontSize: 80,
    marginBottom: Spacing.md,
  },
  logoText: {
    ...Typography.h1,
    color: Colors.textDark,
    fontWeight: '700',
    marginBottom: Spacing.sm,
  },
  logoSubtext: {
    ...Typography.body1,
    color: Colors.textDark,
    opacity: 0.8,
  },
  loadingContainer: {
    alignItems: 'center',
  },
  loadingText: {
    ...Typography.body2,
    color: Colors.textDark,
    marginTop: Spacing.md,
    opacity: 0.8,
  },
  loginContainer: {
    flex: 1,
  },
  loginHeader: {
    flexDirection: 'row',
    justifyContent: 'flex-end',
    paddingHorizontal: Spacing.lg,
    paddingTop: Spacing.md,
  },
  themeToggle: {
    padding: Spacing.sm,
  },
  loginContent: {
    flex: 1,
    justifyContent: 'center',
    paddingHorizontal: Spacing.lg,
  },
  loginLogo: {
    alignItems: 'center',
    marginBottom: Spacing.xxl,
  },
  loginEmoji: {
    fontSize: 60,
    marginBottom: Spacing.md,
  },
  loginTitle: {
    ...Typography.h2,
    marginBottom: Spacing.sm,
    textAlign: 'center',
  },
  loginSubtitle: {
    ...Typography.body1,
    textAlign: 'center',
  },
  loginForm: {
    gap: Spacing.lg,
  },
  inputGroup: {
    gap: Spacing.sm,
  },
  inputLabel: {
    ...Typography.body2,
    fontWeight: '500',
  },
  input: {
    borderWidth: 1,
    borderRadius: BorderRadius.lg,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.md,
    fontSize: 16,
    minHeight: 56,
  },
  loginButton: {
    borderRadius: BorderRadius.lg,
    overflow: 'hidden',
    marginTop: Spacing.md,
  },
  loginButtonGradient: {
    paddingVertical: Spacing.md,
    alignItems: 'center',
  },
  loginButtonText: {
    ...Typography.button,
    fontWeight: '600',
  },
  biometricButton: {
    alignItems: 'center',
    paddingVertical: Spacing.md,
  },
  biometricIcon: {
    fontSize: 24,
    marginBottom: Spacing.sm,
  },
  biometricText: {
    ...Typography.body2,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: Spacing.lg,
    paddingVertical: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
  },
  backButton: {
    padding: Spacing.sm,
  },
  backIcon: {
    fontSize: 24,
    color: Colors.primary,
  },
  headerTitle: {
    ...Typography.h3,
  },
  themeButton: {
    padding: Spacing.sm,
  },
  themeIcon: {
    fontSize: 20,
  },
  content: {
    flex: 1,
    paddingHorizontal: Spacing.lg,
    paddingTop: Spacing.lg,
  },
  card: {
    padding: Spacing.lg,
    borderRadius: BorderRadius.lg,
    shadowColor: Colors.shadow,
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
    marginBottom: Spacing.lg,
  },
  cardTitle: {
    ...Typography.h4,
    marginBottom: Spacing.sm,
  },
  cardSubtitle: {
    ...Typography.body2,
    marginBottom: Spacing.lg,
  },
  cardButton: {
    borderRadius: BorderRadius.lg,
    overflow: 'hidden',
  },
  cardButtonGradient: {
    paddingVertical: Spacing.md,
    alignItems: 'center',
  },
  cardButtonText: {
    ...Typography.button,
    fontWeight: '600',
  },
  profileCard: {
    alignItems: 'center',
    padding: Spacing.lg,
    borderRadius: BorderRadius.lg,
    shadowColor: Colors.shadow,
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
    marginBottom: Spacing.lg,
  },
  profileAvatar: {
    width: 80,
    height: 80,
    borderRadius: BorderRadius.round,
    backgroundColor: Colors.primary,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: Spacing.md,
  },
  profileAvatarText: {
    ...Typography.h2,
    color: Colors.textDark,
    fontWeight: '600',
  },
  profileName: {
    ...Typography.h3,
    marginBottom: Spacing.sm,
  },
  profileEmail: {
    ...Typography.body2,
  },
  menuSection: {
    marginBottom: Spacing.lg,
  },
  menuTitle: {
    ...Typography.h4,
    marginBottom: Spacing.md,
  },
  menuItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    borderRadius: BorderRadius.lg,
    marginBottom: Spacing.sm,
    shadowColor: Colors.shadow,
    shadowOffset: { width: 0, height: 1 },
    shadowOpacity: 0.05,
    shadowRadius: 4,
    elevation: 1,
  },
  menuIcon: {
    fontSize: 20,
    marginRight: Spacing.md,
  },
  menuText: {
    flex: 1,
    ...Typography.body2,
  },
  menuArrow: {
    fontSize: 16,
  },
  logoutButton: {
    paddingVertical: Spacing.md,
    borderRadius: BorderRadius.lg,
    alignItems: 'center',
    marginBottom: Spacing.xl,
  },
  logoutText: {
    ...Typography.button,
  },
});