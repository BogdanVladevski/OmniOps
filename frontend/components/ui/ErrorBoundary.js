import React from 'react';
import { View, Text, StyleSheet } from 'react-native';
import { useTheme } from '../../contexts/ThemeContext';

export default class ErrorBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { error: null };
  }

  static getDerivedStateFromError(error) {
    return { error };
  }

  componentDidCatch(error, info) {
    console.error('UI error boundary:', error, info);
  }

  render() {
    if (this.state.error) {
      return <Fallback error={this.state.error} onReset={() => this.setState({ error: null })} />;
    }
    return this.props.children;
  }
}

function Fallback({ error, onReset }) {
  const { colors } = useTheme();
  return (
    <View style={[styles.container, { backgroundColor: colors.background }]}>
      <Text style={[styles.title, { color: colors.text }]}>Something went wrong</Text>
      <Text style={[styles.body, { color: colors.textSecondary }]}>
        {error?.message ?? 'An unexpected error occurred in this screen.'}
      </Text>
      <Text style={[styles.retry, { color: colors.accent }]} onPress={onReset}>
        Try again
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  container: { flex: 1, justifyContent: 'center', alignItems: 'center', padding: 24 },
  title: { fontSize: 20, fontWeight: '700', marginBottom: 8 },
  body: { textAlign: 'center', lineHeight: 20, marginBottom: 16 },
  retry: { fontSize: 16, fontWeight: '700' },
});
