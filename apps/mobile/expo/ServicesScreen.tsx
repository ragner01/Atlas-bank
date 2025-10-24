import React, { useState, useRef, useEffect } from 'react';
import {
  View,
  Text,
  TouchableOpacity,
  Alert,
  StyleSheet,
  StatusBar,
  SafeAreaView,
  ScrollView,
  Animated,
  Easing,
  Dimensions,
} from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';

const { width } = Dimensions.get('window');

// Design System (same as previous screens)
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

// Service Category Component
const ServiceCategory = ({ 
  title, 
  services, 
  onServicePress, 
  theme, 
  isExpanded, 
  onToggle 
}: any) => {
  const heightAnimation = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    Animated.timing(heightAnimation, {
      toValue: isExpanded ? 1 : 0,
      duration: 300,
      easing: Easing.out(Easing.cubic),
      useNativeDriver: false,
    }).start();
  }, [isExpanded]);

  const maxHeight = heightAnimation.interpolate({
    inputRange: [0, 1],
    outputRange: [0, services.length * 80],
  });

  return (
    <View style={[styles.categoryContainer, { backgroundColor: theme.colors.surface }]}>
      <TouchableOpacity
        style={styles.categoryHeader}
        onPress={onToggle}
        activeOpacity={0.7}
      >
        <Text style={[styles.categoryTitle, { color: theme.colors.text }]}>
          {title}
        </Text>
        <Text style={[styles.expandIcon, { color: theme.colors.textSecondary }]}>
          {isExpanded ? '−' : '+'}
        </Text>
      </TouchableOpacity>
      
      <Animated.View style={{ maxHeight, overflow: 'hidden' }}>
        {services.map((service: any, index: number) => (
          <TouchableOpacity
            key={service.id}
            style={[
              styles.serviceItem,
              { backgroundColor: theme.colors.background },
              index === services.length - 1 && styles.lastServiceItem,
            ]}
            onPress={() => onServicePress(service)}
            activeOpacity={0.7}
          >
            <View style={[styles.serviceIcon, { backgroundColor: service.color }]}>
              <Text style={styles.serviceEmoji}>{service.icon}</Text>
            </View>
            <View style={styles.serviceInfo}>
              <Text style={[styles.serviceName, { color: theme.colors.text }]}>
                {service.name}
              </Text>
              <Text style={[styles.serviceDescription, { color: theme.colors.textSecondary }]}>
                {service.description}
              </Text>
            </View>
            <Text style={[styles.serviceArrow, { color: theme.colors.textSecondary }]}>
              →
            </Text>
          </TouchableOpacity>
        ))}
      </Animated.View>
    </View>
  );
};

// Featured Service Component
const FeaturedService = ({ service, onPress, theme }: any) => {
  return (
    <TouchableOpacity
      style={[styles.featuredService, { backgroundColor: theme.colors.surface }]}
      onPress={() => onPress(service)}
      activeOpacity={0.7}
    >
      <LinearGradient
        colors={[service.color, service.color + '80']}
        style={styles.featuredServiceGradient}
      >
        <View style={styles.featuredServiceContent}>
          <Text style={styles.featuredServiceEmoji}>{service.icon}</Text>
          <Text style={[styles.featuredServiceName, { color: Colors.textDark }]}>
            {service.name}
          </Text>
          <Text style={[styles.featuredServiceDescription, { color: Colors.textDark }]}>
            {service.description}
          </Text>
        </View>
      </LinearGradient>
    </TouchableOpacity>
  );
};

// Main Services Screen
function ServicesScreen({ navigation }: any) {
  const [isDark, setIsDark] = useState(false);
  const [expandedCategories, setExpandedCategories] = useState<{[key: string]: boolean}>({
    bills: true,
    financial: false,
    lifestyle: false,
  });

  const theme = {
    isDark,
    colors: isDark ? Colors : Colors,
    typography: Typography,
    spacing: Spacing,
    borderRadius: BorderRadius,
  };

  const featuredServices = [
    {
      id: 'airtime',
      name: 'Airtime',
      description: 'Top up your phone',
      icon: '📱',
      color: '#007BFF',
    },
    {
      id: 'electricity',
      name: 'Electricity',
      description: 'Pay your bills',
      icon: '⚡',
      color: '#FF9800',
    },
  ];

  const serviceCategories = {
    bills: {
      title: 'Bills & Utilities',
      services: [
        {
          id: 'electricity',
          name: 'Electricity',
          description: 'Pay electricity bills',
          icon: '⚡',
          color: '#FF9800',
        },
        {
          id: 'water',
          name: 'Water',
          description: 'Pay water bills',
          icon: '💧',
          color: '#2196F3',
        },
        {
          id: 'internet',
          name: 'Internet',
          description: 'Pay internet bills',
          icon: '🌐',
          color: '#9C27B0',
        },
        {
          id: 'cable',
          name: 'Cable TV',
          description: 'Pay cable TV bills',
          icon: '📺',
          color: '#FF5722',
        },
      ],
    },
    financial: {
      title: 'Financial Services',
      services: [
        {
          id: 'loans',
          name: 'Quick Loans',
          description: 'Get instant loans',
          icon: '💰',
          color: '#4CAF50',
        },
        {
          id: 'savings',
          name: 'Savings',
          description: 'Save money securely',
          icon: '🏦',
          color: '#00BCD4',
        },
        {
          id: 'investments',
          name: 'Investments',
          description: 'Grow your money',
          icon: '📈',
          color: '#8BC34A',
        },
        {
          id: 'insurance',
          name: 'Insurance',
          description: 'Protect what matters',
          icon: '🛡️',
          color: '#FFC107',
        },
      ],
    },
    lifestyle: {
      title: 'Lifestyle',
      services: [
        {
          id: 'transport',
          name: 'Transport',
          description: 'Book rides & tickets',
          icon: '🚗',
          color: '#607D8B',
        },
        {
          id: 'food',
          name: 'Food Delivery',
          description: 'Order food online',
          icon: '🍕',
          color: '#E91E63',
        },
        {
          id: 'shopping',
          name: 'Shopping',
          description: 'Buy online',
          icon: '🛒',
          color: '#795548',
        },
        {
          id: 'entertainment',
          name: 'Entertainment',
          description: 'Movies & events',
          icon: '🎬',
          color: '#3F51B5',
        },
      ],
    },
  };

  const handleServicePress = (service: any) => {
    // Handle different service types
    switch (service.id) {
      case 'airtime':
        Alert.alert('Airtime', 'Airtime purchase feature coming soon!');
        break;
      case 'electricity':
        Alert.alert('Electricity', 'Electricity bill payment feature coming soon!');
        break;
      case 'loans':
        navigation.navigate('Loans');
        break;
      case 'savings':
        navigation.navigate('Savings');
        break;
      default:
        Alert.alert(service.name, `${service.name} feature coming soon!`);
    }
  };

  const toggleCategory = (categoryKey: string) => {
    setExpandedCategories(prev => ({
      ...prev,
      [categoryKey]: !prev[categoryKey],
    }));
  };

  return (
    <SafeAreaView style={[styles.container, { backgroundColor: theme.colors.background }]}>
      <StatusBar barStyle={isDark ? "light-content" : "dark-content"} />
      
      {/* Header */}
      <View style={styles.header}>
        <View style={styles.headerContent}>
          <Text style={[styles.headerTitle, { color: theme.colors.text }]}>
            Services
          </Text>
          <TouchableOpacity onPress={() => setIsDark(!isDark)} style={styles.themeButton}>
            <Text style={styles.themeIcon}>{isDark ? '☀️' : '🌙'}</Text>
          </TouchableOpacity>
        </View>
        <Text style={[styles.headerSubtitle, { color: theme.colors.textSecondary }]}>
          Manage your bills and payments
        </Text>
      </View>

      <ScrollView style={styles.content} showsVerticalScrollIndicator={false}>
        {/* Featured Services */}
        <View style={styles.section}>
          <Text style={[styles.sectionTitle, { color: theme.colors.text }]}>
            Quick Access
          </Text>
          <View style={styles.featuredServicesGrid}>
            {featuredServices.map((service) => (
              <FeaturedService
                key={service.id}
                service={service}
                onPress={handleServicePress}
                theme={theme}
              />
            ))}
          </View>
        </View>

        {/* Service Categories */}
        <View style={styles.section}>
          <Text style={[styles.sectionTitle, { color: theme.colors.text }]}>
            All Services
          </Text>
          
          {Object.entries(serviceCategories).map(([key, category]) => (
            <ServiceCategory
              key={key}
              title={category.title}
              services={category.services}
              onServicePress={handleServicePress}
              theme={theme}
              isExpanded={expandedCategories[key]}
              onToggle={() => toggleCategory(key)}
            />
          ))}
        </View>

        {/* Help Section */}
        <View style={styles.section}>
          <View style={[styles.helpCard, { backgroundColor: theme.colors.surface }]}>
            <Text style={styles.helpEmoji}>💬</Text>
            <View style={styles.helpContent}>
              <Text style={[styles.helpTitle, { color: theme.colors.text }]}>
                Need Help?
              </Text>
              <Text style={[styles.helpDescription, { color: theme.colors.textSecondary }]}>
                Contact our support team for assistance with any service
              </Text>
            </View>
            <TouchableOpacity
              style={[styles.helpButton, { backgroundColor: theme.colors.primary }]}
              onPress={() => Alert.alert('Support', 'Support feature coming soon!')}
            >
              <Text style={[styles.helpButtonText, { color: Colors.textDark }]}>
                Contact
              </Text>
            </TouchableOpacity>
          </View>
        </View>
      </ScrollView>
    </SafeAreaView>
  );
}

// Styles
const styles = StyleSheet.create({
  container: {
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
    marginBottom: Spacing.sm,
  },
  headerTitle: {
    ...Typography.h2,
  },
  themeButton: {
    padding: Spacing.sm,
  },
  themeIcon: {
    fontSize: 20,
  },
  headerSubtitle: {
    ...Typography.body2,
  },
  content: {
    flex: 1,
    paddingHorizontal: Spacing.lg,
  },
  section: {
    marginBottom: Spacing.lg,
  },
  sectionTitle: {
    ...Typography.h4,
    marginBottom: Spacing.md,
  },
  featuredServicesGrid: {
    flexDirection: 'row',
    gap: Spacing.md,
  },
  featuredService: {
    flex: 1,
    borderRadius: BorderRadius.lg,
    overflow: 'hidden',
    shadowColor: Colors.shadow,
    shadowOffset: { width: 0, height: 4 },
    shadowOpacity: 0.1,
    shadowRadius: 12,
    elevation: 8,
  },
  featuredServiceGradient: {
    padding: Spacing.lg,
  },
  featuredServiceContent: {
    alignItems: 'center',
  },
  featuredServiceEmoji: {
    fontSize: 32,
    marginBottom: Spacing.sm,
  },
  featuredServiceName: {
    ...Typography.h4,
    fontWeight: '600',
    marginBottom: Spacing.xs,
    textAlign: 'center',
  },
  featuredServiceDescription: {
    ...Typography.caption,
    textAlign: 'center',
    opacity: 0.9,
  },
  categoryContainer: {
    borderRadius: BorderRadius.lg,
    marginBottom: Spacing.md,
    shadowColor: Colors.shadow,
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
    overflow: 'hidden',
  },
  categoryHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
  },
  categoryTitle: {
    ...Typography.h4,
  },
  expandIcon: {
    fontSize: 20,
    fontWeight: '600' as const,
  },
  serviceItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
  },
  lastServiceItem: {
    borderBottomWidth: 0,
  },
  serviceIcon: {
    width: 48,
    height: 48,
    borderRadius: BorderRadius.lg,
    justifyContent: 'center',
    alignItems: 'center',
    marginRight: Spacing.md,
  },
  serviceEmoji: {
    fontSize: 24,
  },
  serviceInfo: {
    flex: 1,
  },
  serviceName: {
    ...Typography.body2,
    fontWeight: '500' as const,
    marginBottom: Spacing.xs,
  },
  serviceDescription: {
    ...Typography.caption,
  },
  serviceArrow: {
    fontSize: 18,
    fontWeight: '600' as const,
  },
  helpCard: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.lg,
    borderRadius: BorderRadius.lg,
    shadowColor: Colors.shadow,
    shadowOffset: { width: 0, height: 2 },
    shadowOpacity: 0.05,
    shadowRadius: 8,
    elevation: 2,
  },
  helpEmoji: {
    fontSize: 32,
    marginRight: Spacing.md,
  },
  helpContent: {
    flex: 1,
  },
  helpTitle: {
    ...Typography.body2,
    fontWeight: '600' as const,
    marginBottom: Spacing.xs,
  },
  helpDescription: {
    ...Typography.caption,
  },
  helpButton: {
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    borderRadius: BorderRadius.md,
  },
  helpButtonText: {
    ...Typography.caption,
    fontWeight: '600' as const,
  },
});

export default ServicesScreen;
