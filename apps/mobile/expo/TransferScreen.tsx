import React, { useState, useRef, useEffect } from 'react';
import {
  View,
  Text,
  TextInput,
  TouchableOpacity,
  Alert,
  StyleSheet,
  StatusBar,
  SafeAreaView,
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  Animated,
  Easing,
} from 'react-native';
import { LinearGradient } from 'expo-linear-gradient';
import { BlurView } from 'expo-blur';

// Design System (same as HomeScreen)
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

// Input Component with Floating Label
const FloatingInput = ({ 
  label, 
  value, 
  onChangeText, 
  placeholder, 
  keyboardType = 'default',
  secureTextEntry = false,
  error,
  theme,
  ...props 
}: any) => {
  const [isFocused, setIsFocused] = useState(false);
  const labelAnimation = useRef(new Animated.Value(value ? 1 : 0)).current;

  useEffect(() => {
    Animated.timing(labelAnimation, {
      toValue: isFocused || value ? 1 : 0,
      duration: 200,
      easing: Easing.out(Easing.cubic),
      useNativeDriver: false,
    }).start();
  }, [isFocused, value]);

  const labelStyle = {
    position: 'absolute' as const,
    left: Spacing.md,
    top: labelAnimation.interpolate({
      inputRange: [0, 1],
      outputRange: [20, -8],
    }),
    fontSize: labelAnimation.interpolate({
      inputRange: [0, 1],
      outputRange: [16, 12],
    }),
    color: labelAnimation.interpolate({
      inputRange: [0, 1],
      outputRange: [Colors.textSecondary, Colors.primary],
    }),
    backgroundColor: theme.colors.surface,
    paddingHorizontal: Spacing.sm,
    zIndex: 1,
  };

  return (
    <View style={styles.inputContainer}>
      <Animated.Text style={labelStyle}>{label}</Animated.Text>
      <TextInput
        style={[
          styles.input,
          {
            borderColor: error ? Colors.error : isFocused ? Colors.primary : Colors.border,
            backgroundColor: theme.colors.surface,
            color: theme.colors.text,
          }
        ]}
        value={value}
        onChangeText={onChangeText}
        onFocus={() => setIsFocused(true)}
        onBlur={() => setIsFocused(false)}
        keyboardType={keyboardType}
        secureTextEntry={secureTextEntry}
        placeholder={isFocused ? placeholder : ''}
        placeholderTextColor={Colors.textSecondary}
        {...props}
      />
      {error && (
        <Text style={[styles.errorText, { color: Colors.error }]}>
          {error}
        </Text>
      )}
    </View>
  );
};

// Amount Input Component
const AmountInput = ({ value, onChangeText, theme }: any) => {
  const formatAmount = (text: string) => {
    const numericValue = text.replace(/[^0-9]/g, '');
    if (numericValue === '') return '';
    
    const amount = parseInt(numericValue);
    return new Intl.NumberFormat('en-NG', {
      style: 'currency',
      currency: 'NGN',
      minimumFractionDigits: 0,
    }).format(amount);
  };

  const handleAmountChange = (text: string) => {
    const numericValue = text.replace(/[^0-9]/g, '');
    onChangeText(numericValue);
  };

  return (
    <View style={styles.amountContainer}>
      <Text style={[styles.amountLabel, { color: theme.colors.text }]}>
        Amount
      </Text>
      <View style={[styles.amountInputContainer, { backgroundColor: theme.colors.surface }]}>
        <Text style={[styles.currencySymbol, { color: theme.colors.text }]}>
          ₦
        </Text>
        <TextInput
          style={[styles.amountInput, { color: theme.colors.text }]}
          value={value ? formatAmount(value) : ''}
          onChangeText={handleAmountChange}
          keyboardType="numeric"
          placeholder="0"
          placeholderTextColor={Colors.textSecondary}
        />
      </View>
    </View>
  );
};

// Contact Selection Component
const ContactSelector = ({ onSelectContact, theme }: any) => {
  const [showContacts, setShowContacts] = useState(false);
  
  const recentContacts = [
    { id: '1', name: 'Jane Doe', phone: '2348100000002', avatar: '👩' },
    { id: '2', name: 'John Smith', phone: '2348100000003', avatar: '👨' },
    { id: '3', name: 'Mary Johnson', phone: '2348100000004', avatar: '👩‍💼' },
  ];

  return (
    <View style={styles.contactSelector}>
      <TouchableOpacity
        style={[styles.contactButton, { backgroundColor: theme.colors.surface }]}
        onPress={() => setShowContacts(!showContacts)}
      >
        <Text style={[styles.contactButtonText, { color: theme.colors.text }]}>
          📞 Select from contacts
        </Text>
      </TouchableOpacity>
      
      {showContacts && (
        <View style={[styles.contactsList, { backgroundColor: theme.colors.surface }]}>
          {recentContacts.map((contact) => (
            <TouchableOpacity
              key={contact.id}
              style={styles.contactItem}
              onPress={() => {
                onSelectContact(contact);
                setShowContacts(false);
              }}
            >
              <Text style={styles.contactAvatar}>{contact.avatar}</Text>
              <View style={styles.contactInfo}>
                <Text style={[styles.contactName, { color: theme.colors.text }]}>
                  {contact.name}
                </Text>
                <Text style={[styles.contactPhone, { color: theme.colors.textSecondary }]}>
                  {contact.phone}
                </Text>
              </View>
            </TouchableOpacity>
          ))}
        </View>
      )}
    </View>
  );
};

// Confirmation Modal Component
const ConfirmationModal = ({ 
  visible, 
  onClose, 
  onConfirm, 
  recipient, 
  amount, 
  theme 
}: any) => {
  const slideAnimation = useRef(new Animated.Value(0)).current;

  useEffect(() => {
    if (visible) {
      Animated.spring(slideAnimation, {
        toValue: 1,
        useNativeDriver: true,
        tension: 100,
        friction: 8,
      }).start();
    } else {
      Animated.timing(slideAnimation, {
        toValue: 0,
        duration: 200,
        useNativeDriver: true,
      }).start();
    }
  }, [visible]);

  if (!visible) return null;

  const translateY = slideAnimation.interpolate({
    inputRange: [0, 1],
    outputRange: [300, 0],
  });

  return (
    <View style={styles.modalOverlay}>
      <Animated.View
        style={[
          styles.modalContent,
          {
            backgroundColor: theme.colors.surface,
            transform: [{ translateY }],
          },
        ]}
      >
        <View style={styles.modalHeader}>
          <Text style={[styles.modalTitle, { color: theme.colors.text }]}>
            Confirm Transfer
          </Text>
          <TouchableOpacity onPress={onClose} style={styles.closeButton}>
            <Text style={styles.closeIcon}>✕</Text>
          </TouchableOpacity>
        </View>

        <View style={styles.confirmationDetails}>
          <View style={styles.confirmationRow}>
            <Text style={[styles.confirmationLabel, { color: theme.colors.textSecondary }]}>
              To
            </Text>
            <Text style={[styles.confirmationValue, { color: theme.colors.text }]}>
              {recipient}
            </Text>
          </View>
          
          <View style={styles.confirmationRow}>
            <Text style={[styles.confirmationLabel, { color: theme.colors.textSecondary }]}>
              Amount
            </Text>
            <Text style={[styles.confirmationValue, { color: theme.colors.text }]}>
              {amount}
            </Text>
          </View>
          
          <View style={styles.confirmationRow}>
            <Text style={[styles.confirmationLabel, { color: theme.colors.textSecondary }]}>
              Fee
            </Text>
            <Text style={[styles.confirmationValue, { color: theme.colors.text }]}>
              ₦0.00
            </Text>
          </View>
        </View>

        <View style={styles.modalActions}>
          <TouchableOpacity
            style={[styles.cancelButton, { backgroundColor: Colors.border }]}
            onPress={onClose}
          >
            <Text style={[styles.cancelButtonText, { color: theme.colors.text }]}>
              Cancel
            </Text>
          </TouchableOpacity>
          
          <TouchableOpacity
            style={styles.confirmButton}
            onPress={onConfirm}
          >
            <LinearGradient
              colors={[Colors.primary, Colors.secondary]}
              style={styles.confirmButtonGradient}
            >
              <Text style={[styles.confirmButtonText, { color: Colors.textDark }]}>
                Send Money
              </Text>
            </LinearGradient>
          </TouchableOpacity>
        </View>
      </Animated.View>
    </View>
  );
};

// Main Transfer Screen
function TransferScreen({ navigation }: any) {
  const [recipient, setRecipient] = useState('');
  const [amount, setAmount] = useState('');
  const [note, setNote] = useState('');
  const [showConfirmation, setShowConfirmation] = useState(false);
  const [isDark, setIsDark] = useState(false);
  const [errors, setErrors] = useState<any>({});

  const theme = {
    isDark,
    colors: isDark ? Colors : Colors,
    typography: Typography,
    spacing: Spacing,
    borderRadius: BorderRadius,
  };

  const validateForm = () => {
    const newErrors: any = {};
    
    if (!recipient.trim()) {
      newErrors.recipient = 'Recipient is required';
    } else if (!/^234\d{10}$/.test(recipient.replace(/\s/g, ''))) {
      newErrors.recipient = 'Invalid phone number format';
    }
    
    if (!amount.trim()) {
      newErrors.amount = 'Amount is required';
    } else if (parseInt(amount) < 100) {
      newErrors.amount = 'Minimum amount is ₦100';
    } else if (parseInt(amount) > 1000000) {
      newErrors.amount = 'Maximum amount is ₦1,000,000';
    }
    
    setErrors(newErrors);
    return Object.keys(newErrors).length === 0;
  };

  const handleSendMoney = () => {
    if (validateForm()) {
      setShowConfirmation(true);
    }
  };

  const handleConfirmTransfer = async () => {
    try {
      // Simulate API call
      await new Promise(resolve => setTimeout(resolve, 2000));
      
      Alert.alert(
        'Success!',
        `₦${parseInt(amount).toLocaleString()} sent to ${recipient}`,
        [
          {
            text: 'OK',
            onPress: () => {
              setShowConfirmation(false);
              setRecipient('');
              setAmount('');
              setNote('');
              navigation.goBack();
            },
          },
        ]
      );
    } catch (error) {
      Alert.alert('Error', 'Transfer failed. Please try again.');
    }
  };

  const handleSelectContact = (contact: any) => {
    setRecipient(contact.phone);
  };

  return (
    <SafeAreaView style={[styles.container, { backgroundColor: theme.colors.background }]}>
      <StatusBar barStyle={isDark ? "light-content" : "dark-content"} />
      
      {/* Header */}
      <View style={styles.header}>
        <TouchableOpacity onPress={() => navigation.goBack()} style={styles.backButton}>
          <Text style={styles.backIcon}>←</Text>
        </TouchableOpacity>
        <Text style={[styles.headerTitle, { color: theme.colors.text }]}>
          Send Money
        </Text>
        <TouchableOpacity onPress={() => setIsDark(!isDark)} style={styles.themeButton}>
          <Text style={styles.themeIcon}>{isDark ? '☀️' : '🌙'}</Text>
        </TouchableOpacity>
      </View>

      <KeyboardAvoidingView
        style={styles.content}
        behavior={Platform.OS === 'ios' ? 'padding' : 'height'}
      >
        <ScrollView style={styles.scrollView} showsVerticalScrollIndicator={false}>
          {/* Recipient Section */}
          <View style={styles.section}>
            <Text style={[styles.sectionTitle, { color: theme.colors.text }]}>
              Recipient
            </Text>
            
            <FloatingInput
              label="Phone Number"
              value={recipient}
              onChangeText={setRecipient}
              placeholder="Enter phone number (e.g., 2348100000001)"
              keyboardType="phone-pad"
              error={errors.recipient}
              theme={theme}
            />
            
            <ContactSelector
              onSelectContact={handleSelectContact}
              theme={theme}
            />
          </View>

          {/* Amount Section */}
          <View style={styles.section}>
            <Text style={[styles.sectionTitle, { color: theme.colors.text }]}>
              Amount
            </Text>
            
            <AmountInput
              value={amount}
              onChangeText={setAmount}
              theme={theme}
            />
            
            {errors.amount && (
              <Text style={[styles.errorText, { color: Colors.error }]}>
                {errors.amount}
              </Text>
            )}
            
            {/* Quick Amount Buttons */}
            <View style={styles.quickAmounts}>
              {['1000', '5000', '10000', '50000'].map((quickAmount) => (
                <TouchableOpacity
                  key={quickAmount}
                  style={[styles.quickAmountButton, { backgroundColor: theme.colors.surface }]}
                  onPress={() => setAmount(quickAmount)}
                >
                  <Text style={[styles.quickAmountText, { color: theme.colors.text }]}>
                    ₦{parseInt(quickAmount).toLocaleString()}
                  </Text>
                </TouchableOpacity>
              ))}
            </View>
          </View>

          {/* Note Section */}
          <View style={styles.section}>
            <Text style={[styles.sectionTitle, { color: theme.colors.text }]}>
              Note (Optional)
            </Text>
            
            <FloatingInput
              label="Add a note"
              value={note}
              onChangeText={setNote}
              placeholder="What's this for?"
              theme={theme}
            />
          </View>

          {/* Send Button */}
          <TouchableOpacity
            style={styles.sendButton}
            onPress={handleSendMoney}
            disabled={!recipient.trim() || !amount.trim()}
          >
            <LinearGradient
              colors={[Colors.primary, Colors.secondary]}
              style={styles.sendButtonGradient}
            >
              <Text style={[styles.sendButtonText, { color: Colors.textDark }]}>
                Send Money
              </Text>
            </LinearGradient>
          </TouchableOpacity>
        </ScrollView>
      </KeyboardAvoidingView>

      {/* Confirmation Modal */}
      <ConfirmationModal
        visible={showConfirmation}
        onClose={() => setShowConfirmation(false)}
        onConfirm={handleConfirmTransfer}
        recipient={recipient}
        amount={`₦${parseInt(amount || '0').toLocaleString()}`}
        theme={theme}
      />
    </SafeAreaView>
  );
}

// Styles
const styles = StyleSheet.create({
  container: {
    flex: 1,
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
  },
  scrollView: {
    flex: 1,
    paddingHorizontal: Spacing.lg,
  },
  section: {
    marginTop: Spacing.lg,
  },
  sectionTitle: {
    ...Typography.h4,
    marginBottom: Spacing.md,
  },
  inputContainer: {
    marginBottom: Spacing.md,
  },
  input: {
    borderWidth: 1,
    borderRadius: BorderRadius.lg,
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.md,
    fontSize: 16,
    minHeight: 56,
  },
  errorText: {
    ...Typography.caption,
    marginTop: Spacing.xs,
    marginLeft: Spacing.sm,
  },
  amountContainer: {
    marginBottom: Spacing.md,
  },
  amountLabel: {
    ...Typography.body2,
    fontWeight: '500' as const,
    marginBottom: Spacing.sm,
  },
  amountInputContainer: {
    flexDirection: 'row',
    alignItems: 'center',
    borderWidth: 1,
    borderColor: Colors.border,
    borderRadius: BorderRadius.lg,
    paddingHorizontal: Spacing.md,
    minHeight: 56,
  },
  currencySymbol: {
    ...Typography.h4,
    marginRight: Spacing.sm,
  },
  amountInput: {
    flex: 1,
    fontSize: 20,
    fontWeight: '600' as const,
  },
  quickAmounts: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: Spacing.sm,
    marginTop: Spacing.md,
  },
  quickAmountButton: {
    paddingHorizontal: Spacing.md,
    paddingVertical: Spacing.sm,
    borderRadius: BorderRadius.md,
    borderWidth: 1,
    borderColor: Colors.border,
  },
  quickAmountText: {
    ...Typography.body2,
    fontWeight: '500' as const,
  },
  contactSelector: {
    marginTop: Spacing.sm,
  },
  contactButton: {
    padding: Spacing.md,
    borderRadius: BorderRadius.lg,
    borderWidth: 1,
    borderColor: Colors.border,
    alignItems: 'center',
  },
  contactButtonText: {
    ...Typography.body2,
    color: Colors.primary,
  },
  contactsList: {
    marginTop: Spacing.sm,
    borderRadius: BorderRadius.lg,
    borderWidth: 1,
    borderColor: Colors.border,
    overflow: 'hidden',
  },
  contactItem: {
    flexDirection: 'row',
    alignItems: 'center',
    padding: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
  },
  contactAvatar: {
    fontSize: 24,
    marginRight: Spacing.md,
  },
  contactInfo: {
    flex: 1,
  },
  contactName: {
    ...Typography.body2,
    fontWeight: '500' as const,
    marginBottom: Spacing.xs,
  },
  contactPhone: {
    ...Typography.caption,
  },
  sendButton: {
    marginTop: Spacing.xl,
    marginBottom: Spacing.xl,
    borderRadius: BorderRadius.lg,
    overflow: 'hidden',
  },
  sendButtonGradient: {
    paddingVertical: Spacing.md,
    alignItems: 'center',
  },
  sendButtonText: {
    ...Typography.button,
    fontWeight: '600',
  },
  modalOverlay: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    bottom: 0,
    backgroundColor: 'rgba(0, 0, 0, 0.5)',
    justifyContent: 'flex-end',
  },
  modalContent: {
    borderTopLeftRadius: BorderRadius.xl,
    borderTopRightRadius: BorderRadius.xl,
    padding: Spacing.lg,
    maxHeight: '50%',
  },
  modalHeader: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    marginBottom: Spacing.lg,
  },
  modalTitle: {
    ...Typography.h3,
  },
  closeButton: {
    padding: Spacing.sm,
  },
  closeIcon: {
    fontSize: 20,
    color: Colors.textSecondary,
  },
  confirmationDetails: {
    marginBottom: Spacing.lg,
  },
  confirmationRow: {
    flexDirection: 'row',
    justifyContent: 'space-between',
    alignItems: 'center',
    paddingVertical: Spacing.md,
    borderBottomWidth: 1,
    borderBottomColor: Colors.border,
  },
  confirmationLabel: {
    ...Typography.body2,
  },
  confirmationValue: {
    ...Typography.body2,
    fontWeight: '500' as const,
  },
  modalActions: {
    flexDirection: 'row',
    gap: Spacing.md,
  },
  cancelButton: {
    flex: 1,
    paddingVertical: Spacing.md,
    borderRadius: BorderRadius.lg,
    alignItems: 'center',
  },
  cancelButtonText: {
    ...Typography.button,
    fontWeight: '600',
  },
  confirmButton: {
    flex: 1,
    borderRadius: BorderRadius.lg,
    overflow: 'hidden',
  },
  confirmButtonGradient: {
    paddingVertical: Spacing.md,
    alignItems: 'center',
  },
  confirmButtonText: {
    ...Typography.button,
    fontWeight: '600',
  },
});

export default TransferScreen;
