import { View, Text, StyleSheet, Animated, Easing } from 'react-native'
import React, { useEffect, useRef, useState } from 'react'
import { check, request, PERMISSIONS, RESULTS } from 'react-native-permissions';
import Geolocation from 'react-native-geolocation-service';

interface LoadingScreenProps {
  onLoadingComplete: () => void
}

const LoadingScreen = ({ onLoadingComplete }: LoadingScreenProps) => {
  const fadeAnim = useRef(new Animated.Value(0)).current
  const scaleAnim = useRef(new Animated.Value(0.8)).current
  const rotateAnim = useRef(new Animated.Value(0)).current
  const pulseAnim = useRef(new Animated.Value(1)).current
  const progressAnim = useRef(new Animated.Value(0)).current

  useEffect(() => {
    // Main fade and scale animation
    Animated.parallel([
      Animated.timing(fadeAnim, {
        toValue: 1,
        duration: 1500,
        easing: Easing.out(Easing.quad),
        useNativeDriver: true,
      }),
      Animated.spring(scaleAnim, {
        toValue: 1,
        tension: 100,
        friction: 8,
        useNativeDriver: true,
      }),
    ]).start()

    // Gentle rotation animation for the emoji
    Animated.loop(
      Animated.timing(rotateAnim, {
        toValue: 1,
        duration: 4000,
        easing: Easing.linear,
        useNativeDriver: true,
      })
    ).start()

    // Pulse animation for the text
    Animated.loop(
      Animated.sequence([
        Animated.timing(pulseAnim, {
          toValue: 1.05,
          duration: 2000,
          easing: Easing.inOut(Easing.sin),
          useNativeDriver: true,
        }),
        Animated.timing(pulseAnim, {
          toValue: 1,
          duration: 2000,
          easing: Easing.inOut(Easing.sin),
          useNativeDriver: true,
        }),
      ])
    ).start()

    // Progress bar animation
    Animated.timing(progressAnim, {
      toValue: 1,
      duration: 3000,
      easing: Easing.out(Easing.quad),
      useNativeDriver: false,
    }).start(() => {
      // Navigate to home screen after loading completes
      setTimeout(() => {
        onLoadingComplete()
      }, 500)
    })
  }, [])

  const spin = rotateAnim.interpolate({
    inputRange: [0, 1],
    outputRange: ['0deg', '360deg'],
  })

  const progressWidth = progressAnim.interpolate({
    inputRange: [0, 1],
    outputRange: ['0%', '100%'],
  })

  return (
    <View style={styles.container}>
      <Animated.View
        style={[
          styles.content,
          {
            opacity: fadeAnim,
            transform: [{ scale: scaleAnim }],
          },
        ]}
      >
        <Animated.Text
          style={[
            styles.emoji,
            {
              transform: [{ rotate: spin }],
            },
          ]}
        >
          ☘️
        </Animated.Text>
        
        <Animated.Text
          style={[
            styles.welcomeText,
            {
              transform: [{ scale: pulseAnim }],
            },
          ]}
        >
          Welcome To
        </Animated.Text>
        
        <Animated.Text
          style={[
            styles.appName,
            {
              transform: [{ scale: pulseAnim }],
            },
          ]}
        >
          Silencer
        </Animated.Text>
        
        <View style={styles.loadingContainer}>
          <View style={styles.loadingBar}>
            <Animated.View
              style={[
                styles.loadingProgress,
                {
                  width: progressWidth,
                },
              ]}
            />
          </View>
        </View>
      </Animated.View>
    </View>
  )
}

const HomeScreen = () => {
  const fadeAnim = useRef(new Animated.Value(0)).current

  useEffect(() => {
    Animated.timing(fadeAnim, {
      toValue: 1,
      duration: 800,
      easing: Easing.out(Easing.quad),
      useNativeDriver: true,
    }).start()
  }, [])

  return (
    <Animated.View style={[styles.homeContainer, { opacity: fadeAnim }]}>
      <View style={styles.homeHeader}>
        <Text style={styles.homeTitle}>Silencer</Text>
        <Text style={styles.homeSubtitle}>Find your peace</Text>
      </View>
    </Animated.View>
  )
}

const App = () => {
  const [isLoading, setIsLoading] = useState(true)

  const handleLoadingComplete = () => {
    setIsLoading(false)
  }

  return (
    <>
      {isLoading ? (
        <LoadingScreen onLoadingComplete={handleLoadingComplete} />
      ) : (
        <HomeScreen />
      )}
    </>
  )
}

export default App

const styles = StyleSheet.create({
  // Loading Screen Styles
  container: {
    flex: 1,
    backgroundColor: '#1a2332', // Deep navy blue
    justifyContent: 'center',
    alignItems: 'center',
  },
  content: {
    alignItems: 'center',
    justifyContent: 'center',
  },
  emoji: {
    fontSize: 80,
    marginBottom: 30,
  },
  welcomeText: {
    fontSize: 24,
    color: '#b8d4e3', // Soft blue-gray
    fontWeight: '300',
    marginBottom: 8,
    letterSpacing: 1,
  },
  appName: {
    fontSize: 36,
    color: '#ffffff',
    fontWeight: '700',
    marginBottom: 50,
    letterSpacing: 2,
    textShadowColor: 'rgba(184, 212, 227, 0.3)',
    textShadowOffset: { width: 0, height: 2 },
    textShadowRadius: 8,
  },
  loadingContainer: {
    width: 200,
    alignItems: 'center',
  },
  loadingBar: {
    width: '100%',
    height: 4,
    backgroundColor: 'rgba(184, 212, 227, 0.2)',
    borderRadius: 2,
    overflow: 'hidden',
  },
  loadingProgress: {
    height: '100%',
    backgroundColor: '#7dd3fc', // Soft sky blue
    borderRadius: 2,
    shadowColor: '#7dd3fc',
    shadowOffset: { width: 0, height: 0 },
    shadowOpacity: 0.8,
    shadowRadius: 8,
  },
  
  // Home Screen Styles
  homeContainer: {
    flex: 1,
    backgroundColor: '#0f172a', // Deep dark blue
    justifyContent: 'center',
    alignItems: 'center',
  },
  homeHeader: {
    alignItems: 'center',
    paddingHorizontal: 40,
  },
  homeTitle: {
    fontSize: 38,
    color: '#ffffff',
    fontWeight: '700',
    marginBottom: 16,
    letterSpacing: 2,
    textShadowColor: 'rgba(125, 211, 252, 0.4)',
    textShadowOffset: { width: 0, height: 2 },
    textShadowRadius: 10,
  },
  homeSubtitle: {
    fontSize: 20,
    color: '#94a3b8', // Soft blue-gray
    fontWeight: '300',
    textAlign: 'center',
    letterSpacing: 1,
    opacity: 0.9,
  },
})