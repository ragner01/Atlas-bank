import React, { useRef, useEffect } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  StyleSheet,
  Animated,
  Easing,
  Dimensions,
} from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';

const { width } = Dimensions.get('window');

// Design System
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

// Tab Item Component
const TabItem = ({ 
  tab, 
  isActive, 
  onPress, 
  index, 
  theme,
  animatedValue 
}: any) => {
  const scaleAnimation = useRef(new Animated.Value(1)).current;
  const translateYAnimation = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    if (isActive) {
      Animated.parallel([
        Animated.spring(scaleAnimation, {
          toValue: 1.1,
          useNativeDriver: true,
          tension: 100,
          friction: 8,
        }),
        Animated.timing(translateYAnimation, {
          toValue: -4,
          duration: 200,
          easing: Easing.out(Easing.cubic),
          useNativeDriver: true,
        }),
      ]).start();
    } else {
      Animated.parallel([
        Animated.spring(scaleAnimation, {
          toValue: 1,
          useNativeDriver: true,
          tension: 100,
          friction: 8,
        }),
        Animated.timing(translateYAnimation, {
          toValue: 0,
          duration: 200,
          easing: Easing.out(Easing.cubic),
          useNativeDriver: true,
        }),
      ]).start();
    }
  }, [isActive]);

  return (
    <TouchableOpacity
      style={styles.tabItem}
      onPress={onPress}
      activeOpacity={0.7}
    >
      <Animated.View
        style={[
          styles.tabContent,
          {
            transform: [
              { scale: scaleAnimation },
              { translateY: translateYAnimation },
            ],
          },
        ]}
      >
        {isActive ? (
          <View style={[styles.activeTabBackground, { backgroundColor: theme.colors.primary }]}>
            <LinearGradient
              colors={[theme.colors.primary, theme.colors.secondary]}
              style={styles.activeTabGradient}
            >
              <Text style={styles.tabIcon}>{tab.icon}</Text>
            </LinearGradient>
          </View>
        ) : (
          <View style={styles.inactiveTabBackground}>
            <Text style={[styles.tabIcon, { color: theme.colors.textSecondary }]}>
              {tab.icon}
            </Text>
          </View>
        )}
        
        <Text
          style={[
            styles.tabLabel,
            {
              color: isActive ? theme.colors.primary : theme.colors.textSecondary,
              fontWeight: isActive ? '600' as const : '400' as const,
            },
          ]}
        >
          {tab.label}
        </Text>
      </Animated.View>
    </TouchableOpacity>
  );
};

// Main Bottom Tab Navigator
const BottomTabNavigator = ({ state, descriptors, navigation, theme }: any) => {
  const tabs = [
    { key: 'Home', icon: '🏠', label: 'Home' },
    { key: 'Transfer', icon: '💸', label: 'Send' },
    { key: 'Services', icon: '⚙️', label: 'Services' },
    { key: 'Savings', icon: '🏦', label: 'Savings' },
    { key: 'Profile', icon: '👤', label: 'Profile' },
  ];

  const animatedValue = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    Animated.timing(animatedValue, {
      toValue: 1,
      duration: 300,
      easing: Easing.out(Easing.cubic),
      useNativeDriver: true,
    }).start();
  }, []);

  const translateY = animatedValue.interpolate({
    inputRange: [0, 1],
    outputRange: [100, 0],
  });

  const opacity = animatedValue.interpolate({
    inputRange: [0, 1],
    outputRange: [0, 1],
  });

  return (
    <Animated.View
      style={[
        styles.container,
        {
          backgroundColor: theme.colors.surface,
          transform: [{ translateY }],
          opacity,
        },
      ]}
    >
      {/* Top Border with Gradient */}
      <LinearGradient
        colors={[theme.colors.primary + '20', 'transparent']}
        style={styles.topBorder}
      />
      
      {/* Tab Items */}
      <View style={styles.tabsContainer}>
        {tabs.map((tab, index) => {
          const isFocused = state.index === index;
          const onPress = () => {
            const event = navigation.emit({
              type: 'tabPress',
              target: state.routes[index].key,
              canPreventDefault: true,
            });

            if (!isFocused && !event.defaultPrevented) {
              navigation.navigate(state.routes[index].name);
            }
          };

          return (
            <TabItem
              key={tab.key}
              tab={tab}
              isActive={isFocused}
              onPress={onPress}
              index={index}
              theme={theme}
              animatedValue={animatedValue}
            />
          );
        })}
      </View>
      
      {/* Bottom Safe Area */}
      <View style={styles.bottomSafeArea} />
    </Animated.View>
  );
};

// Styles
const styles = StyleSheet.create({
  container: {
    position: 'absolute',
    bottom: 0,
    left: 0,
    right: 0,
    shadowColor: Colors.shadow,
    shadowOffset: { width: 0, height: -4 },
    shadowOpacity: 0.1,
    shadowRadius: 12,
    elevation: 8,
  },
  topBorder: {
    height: 2,
    width: '100%',
  },
  tabsContainer: {
    flexDirection: 'row',
    paddingHorizontal: Spacing.sm,
    paddingTop: Spacing.sm,
    paddingBottom: Spacing.xs,
  },
  tabItem: {
    flex: 1,
    alignItems: 'center',
    paddingVertical: Spacing.sm,
  },
  tabContent: {
    alignItems: 'center',
    justifyContent: 'center',
  },
  activeTabBackground: {
    width: 48,
    height: 48,
    borderRadius: BorderRadius.round,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: Spacing.xs,
    shadowColor: Colors.primary,
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.3,
    shadowRadius: 8,
    elevation: 8,
  },
  activeTabGradient: {
    width: 48,
    height: 48,
    borderRadius: BorderRadius.round,
    justifyContent: 'center',
    alignItems: 'center',
  },
  inactiveTabBackground: {
    width: 48,
    height: 48,
    borderRadius: BorderRadius.round,
    justifyContent: 'center',
    alignItems: 'center',
    marginBottom: Spacing.xs,
  },
  tabIcon: {
    fontSize: 20,
    color: Colors.textDark,
  },
  tabLabel: {
    ...Typography.caption,
    textAlign: 'center',
  },
  bottomSafeArea: {
    height: 34, // iPhone safe area
    backgroundColor: Colors.surface,
  },
});

export default BottomTabNavigator;
