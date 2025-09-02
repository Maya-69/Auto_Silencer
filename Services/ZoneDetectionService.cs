using System.Timers;
using Silencer.Models;
#if ANDROID
using Android.Media;
using Android.Content;
using Android.App;
#endif

namespace Silencer.Services
{
    public class ZoneDetectionService
    {
        private readonly LocationService _locationService;
        private readonly SilentZoneService _zoneService;
        private readonly System.Timers.Timer _locationTimer;
        
        private bool _isInZone = false;
        private DateTime _zoneEnterTime = DateTime.MinValue;
        private DateTime _zoneExitTime = DateTime.MinValue;
        private bool _phoneIsSilenced = false;
        private List<SilentZone> _currentActiveZones = new List<SilentZone>();
        
                // Store original states to restore later
        #if ANDROID
                private RingerMode _originalRingerMode = RingerMode.Normal;
        #else
                private int _originalRingerMode = 0;
        #endif
                private bool _originalDndState = false;
        
        private readonly int _silentDelaySeconds = 10;
        private readonly int _normalDelaySeconds = 10;

        public ZoneDetectionService(LocationService locationService, SilentZoneService zoneService)
        {
            _locationService = locationService ?? throw new ArgumentNullException(nameof(locationService));
            _zoneService = zoneService ?? throw new ArgumentNullException(nameof(zoneService));
            
            _locationTimer = new System.Timers.Timer(5000);
            _locationTimer.Elapsed += OnLocationTimerElapsed;
            
            StoreOriginalPhoneState();
            LoggingService.LogInfo("ZoneDetectionService initialized");
        }

        #region Public Properties

        public bool IsMonitoring => _locationTimer.Enabled;
        public bool IsCurrentlyInZone => _isInZone;
        public bool IsPhoneSilenced => _phoneIsSilenced;
        public List<SilentZone> CurrentActiveZones => new List<SilentZone>(_currentActiveZones);
        public int SilentDelaySeconds => _silentDelaySeconds;
        public int NormalDelaySeconds => _normalDelaySeconds;

        #endregion

        #region Monitoring Control

        public void StartMonitoring()
        {
            if (!_locationTimer.Enabled)
            {
                _locationTimer.Start();
                LoggingService.LogInfo("Zone detection monitoring started");
            }
        }

        public void StopMonitoring()
        {
            if (_locationTimer.Enabled)
            {
                _locationTimer.Stop();
                LoggingService.LogInfo("Zone detection monitoring stopped");
                ResetDetectionState();
            }
        }

        private void ResetDetectionState()
        {
            _isInZone = false;
            _zoneEnterTime = DateTime.MinValue;
            _zoneExitTime = DateTime.MinValue;
            _currentActiveZones.Clear();
            LoggingService.LogInfo("Zone detection state reset");
        }

        #endregion

        #region Main Detection Logic

        private async void OnLocationTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                await ProcessLocationCheck();
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Zone detection error: {ex.Message}");
            }
        }

        private async Task ProcessLocationCheck()
        {
            var location = await _locationService.GetCurrentLocationAsync();
            if (location == null) 
            {
                LoggingService.LogWarning("Location unavailable");
                return;
            }

            LoggingService.LogInfo($"Location: {location.Latitude:F6}, {location.Longitude:F6}");

            var activeZones = _zoneService.GetActiveZonesAtLocation(location.Latitude, location.Longitude);
            bool currentlyInZone = activeZones.Count > 0;

            _currentActiveZones = new List<SilentZone>(activeZones);

            LogZoneDetectionDetails(activeZones, location);
            await HandleZoneStateChange(currentlyInZone, activeZones);
            await HandleTimingBasedActions();
        }

        private void LogZoneDetectionDetails(List<SilentZone> activeZones, Location location)
        {
            if (activeZones.Count > 0)
            {
                foreach (var zone in activeZones)
                {
                    var distance = zone.DistanceFromLocation(location.Latitude, location.Longitude);
                    LoggingService.LogInfo($"In zone '{zone.Name}' - Distance: {distance:F1}m");
                }
            }
            else
            {
                LoggingService.LogInfo("Not in any zones");
            }
        }

        private async Task HandleZoneStateChange(bool currentlyInZone, List<SilentZone> activeZones)
        {
            if (currentlyInZone && !_isInZone)
            {
                _isInZone = true;
                _zoneEnterTime = DateTime.Now;
                _zoneExitTime = DateTime.MinValue;
                
                var zoneNames = string.Join(", ", activeZones.Select(z => z.Name));
                LoggingService.LogInfo($"ENTERED zone(s): {zoneNames}");
            }
            else if (!currentlyInZone && _isInZone)
            {
                _isInZone = false;
                _zoneExitTime = DateTime.Now;
                _zoneEnterTime = DateTime.MinValue;
                
                LoggingService.LogInfo($"EXITED all zones");
            }

            LogTimingInformation();
        }

        private void LogTimingInformation()
        {
            if (_isInZone && _zoneEnterTime != DateTime.MinValue)
            {
                var timeInZone = DateTime.Now - _zoneEnterTime;
                LoggingService.LogInfo($"Time in zone: {timeInZone.TotalSeconds:F1}s");
            }
            else if (!_isInZone && _zoneExitTime != DateTime.MinValue)
            {
                var timeOutOfZone = DateTime.Now - _zoneExitTime;
                LoggingService.LogInfo($"Time out of zone: {timeOutOfZone.TotalSeconds:F1}s");
            }
        }

        #endregion

        #region Timing-Based Actions

        private async Task HandleTimingBasedActions()
        {
            if (_isInZone && !_phoneIsSilenced && _zoneEnterTime != DateTime.MinValue)
            {
                var timeInZone = DateTime.Now - _zoneEnterTime;
                if (timeInZone.TotalSeconds >= _silentDelaySeconds)
                {
                    LoggingService.LogInfo($"Activating silent mode after {timeInZone.TotalSeconds:F1}s");
                    await SetPhoneSilentMode(true);
                }
            }
            else if (!_isInZone && _phoneIsSilenced && _zoneExitTime != DateTime.MinValue)
            {
                var timeOutOfZone = DateTime.Now - _zoneExitTime;
                if (timeOutOfZone.TotalSeconds >= _normalDelaySeconds)
                {
                    LoggingService.LogInfo($"Restoring normal mode after {timeOutOfZone.TotalSeconds:F1}s");
                    await SetPhoneSilentMode(false);
                }
            }
        }

        #endregion

        #region Phone Control

        private void StoreOriginalPhoneState()
        {
#if ANDROID
            try
            {
                var audioManager = Platform.CurrentActivity?.GetSystemService(Android.Content.Context.AudioService) as AudioManager;
                var notificationManager = Platform.CurrentActivity?.GetSystemService(Android.Content.Context.NotificationService) as NotificationManager;
                
                if (audioManager != null)
                {
                    _originalRingerMode = audioManager.RingerMode;
                    LoggingService.LogInfo($"Stored original ringer mode: {_originalRingerMode}");
                }
                
                if (notificationManager != null && Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
                {
                    _originalDndState = notificationManager.CurrentInterruptionFilter != InterruptionFilter.All;
                    LoggingService.LogInfo($"Stored original DND state: {_originalDndState}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to store original phone state: {ex.Message}");
            }
#endif
        }

        private async Task SetPhoneSilentMode(bool enableSilent)
        {
#if ANDROID
            try
            {
                if (!await CheckDndPermission())
                {
                    LoggingService.LogError("DND permission not granted");
                    return;
                }

                var audioManager = Platform.CurrentActivity?.GetSystemService(Android.Content.Context.AudioService) as AudioManager;
                var notificationManager = Platform.CurrentActivity?.GetSystemService(Android.Content.Context.NotificationService) as NotificationManager;
                
                if (audioManager == null || notificationManager == null)
                {
                    LoggingService.LogError("Cannot access system services");
                    return;
                }

                if (enableSilent)
                {
                    // Enable silent mode + DND
                    audioManager.RingerMode = RingerMode.Silent;
                    
                    if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
                    {
                        notificationManager.SetInterruptionFilter(InterruptionFilter.Alarms);
                    }
                    
                    _phoneIsSilenced = true;
                    LoggingService.LogInfo("Phone set to SILENT + DND mode");
                    
                    if (_currentActiveZones.Any())
                    {
                        var zoneNames = string.Join(", ", _currentActiveZones.Select(z => z.Name));
                        LoggingService.LogInfo($"Triggered by zones: {zoneNames}");
                    }
                }
                else
                {
                    // Restore original states
                    audioManager.RingerMode = _originalRingerMode;
                    
                    if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
                    {
                        if (_originalDndState)
                        {
                            notificationManager.SetInterruptionFilter(InterruptionFilter.Priority);
                        }
                        else
                        {
                            notificationManager.SetInterruptionFilter(InterruptionFilter.All);
                        }
                    }
                    
                    _phoneIsSilenced = false;
                    LoggingService.LogInfo("Phone restored to original state");
                }

                LoggingService.LogInfo($"Ringer mode: {audioManager.RingerMode}");
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
                {
                    LoggingService.LogInfo($"DND filter: {notificationManager.CurrentInterruptionFilter}");
                }
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to control phone: {ex.Message}");
                
                // Reset internal state on failure
                if (enableSilent && _phoneIsSilenced)
                {
                    _phoneIsSilenced = false;
                }
                else if (!enableSilent && !_phoneIsSilenced)
                {
                    _phoneIsSilenced = true;
                }
            }
#else
            LoggingService.LogWarning("Silent mode control only available on Android");
            await Task.CompletedTask;
#endif
        }

        #endregion

        #region Permission Handling

        private async Task<bool> CheckDndPermission()
        {
#if ANDROID
            try
            {
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
                {
                    var notificationManager = Platform.CurrentActivity?.GetSystemService(Android.Content.Context.NotificationService) as NotificationManager;
                    return notificationManager?.IsNotificationPolicyAccessGranted ?? false;
                }
                return true;
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Error checking DND permission: {ex.Message}");
                return false;
            }
#else
            await Task.CompletedTask;
            return true;
#endif
        }

        #endregion

        #region Status and Diagnostics

        public string GetDetailedStatus()
        {
            var status = new System.Text.StringBuilder();
            status.AppendLine($"Monitoring Active: {IsMonitoring}");
            status.AppendLine($"Currently In Zone: {IsCurrentlyInZone}");
            status.AppendLine($"Phone Silenced: {IsPhoneSilenced}");
            status.AppendLine($"Active Zones Count: {_currentActiveZones.Count}");
            
            if (_currentActiveZones.Any())
            {
                status.AppendLine("Active Zones:");
                foreach (var zone in _currentActiveZones)
                {
                    status.AppendLine($"  - {zone.Name} ({zone.Radius}m radius)");
                }
            }
            
            if (_isInZone && _zoneEnterTime != DateTime.MinValue)
            {
                var timeInZone = DateTime.Now - _zoneEnterTime;
                status.AppendLine($"Time in zone: {timeInZone.TotalSeconds:F1}s");
            }
            
            if (!_isInZone && _zoneExitTime != DateTime.MinValue)
            {
                var timeOutOfZone = DateTime.Now - _zoneExitTime;
                status.AppendLine($"Time out of zone: {timeOutOfZone.TotalSeconds:F1}s");
            }
            
            return status.ToString();
        }

        public void ForceStateReset()
        {
            LoggingService.LogWarning("Manual state reset requested");
            ResetDetectionState();
        }

        #endregion

        #region Cleanup

        public void Dispose()
        {
            StopMonitoring();
            _locationTimer?.Dispose();
            LoggingService.LogInfo("ZoneDetectionService disposed");
        }

        #endregion
    }
}