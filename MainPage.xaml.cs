#if ANDROID
using Android.Media;
#endif
using Silencer.Services;
using Silencer.Models;

namespace Silencer;

public partial class MainPage : ContentPage
{
	private bool isSilenced = false;
	private int stepCount = 0;
	private Location lastLocation;
	private readonly LocationService _locationService; // Location service for GPS functionality
	private SilentZoneService _zoneService; // Silent zone management service
	private ZoneDetectionService _zoneDetectionService; // Automatic zone detection service
	private bool _masterSwitchEnabled = false;

	public MainPage()
	{
		InitializeComponent();

		// Initialize services
		_locationService = new LocationService();
		_zoneService = new SilentZoneService();
		_zoneDetectionService = new ZoneDetectionService(_locationService, _zoneService);

		// Start background tracking services
		StartLocationTracking();
		StartStepCounting();

		// Test location service on app startup
		_ = TestLocationAsync();
	}

	#region Master Switch & Status Management

	private async void OnMasterSwitchClicked(object sender, EventArgs e)
	{
		if (!_masterSwitchEnabled)
		{
			// Check all required permissions before enabling
			var hasLocationPermission = await _locationService.CheckLocationPermissionsAsync();
			if (!hasLocationPermission)
			{
				await DisplayAlert("Permission Required", "Location permission required for automatic silent zones", "OK");
				return;
			}

			var hasDndPermission = await CheckDndPermission();
			if (!hasDndPermission)
			{
				#if ANDROID
								await RequestDndPermission();
								return;
				#else
								await DisplayAlert("Permission Required", "Do Not Disturb permission is only applicable on Android.", "OK");
								return;
				#endif
			}

			// All permissions granted - enable automatic mode
			_masterSwitchEnabled = true;
			_zoneDetectionService.StartMonitoring();
			await DisplayAlert("Automatic Silent Zones", "Automatic silent zone detection is now ACTIVE", "OK");
			LoggingService.LogInfo("Master switch enabled - automatic silent zones active");
		}
		else
		{
			// Disable automatic mode
			_masterSwitchEnabled = false;
			_zoneDetectionService.StopMonitoring();
			await DisplayAlert("Automatic Silent Zones", "Automatic silent zone detection is now OFF", "OK");
			LoggingService.LogInfo("Master switch disabled - automatic silent zones inactive");
		}
	}

	private async void OnCheckStatusClicked(object sender, EventArgs e)
	{
		string status = _masterSwitchEnabled
			? "Automatic Silent Zones are ACTIVE"
			: "Automatic Silent Zones are OFF";

		string additionalInfo = "";
		if (_masterSwitchEnabled)
		{
			additionalInfo += $"\n• Currently monitoring: {(_zoneDetectionService.IsMonitoring ? "Yes" : "No")}";
			additionalInfo += $"\n• In zone: {(_zoneDetectionService.IsCurrentlyInZone ? "Yes" : "No")}";
			additionalInfo += $"\n• Phone silenced: {(_zoneDetectionService.IsPhoneSilenced ? "Yes" : "No")}";
			additionalInfo += $"\n• Total zones: {_zoneService.GetZoneCount()}";
			additionalInfo += $"\n• Active zones: {_zoneService.GetActiveZoneCount()}";
		}

		await DisplayAlert("Status", status + additionalInfo, "OK");
	}

	#endregion

	#region Zone Management

	// Test creating a zone at current location
	private async void OnCreateZoneClicked(object sender, EventArgs e)
	{
		try
		{
			var zone = await _zoneService.CreateZoneAtCurrentLocationAsync(_locationService);
			await DisplayAlert("Success", $"Created {zone.Name} at current location", "OK");
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Failed to create zone: {ex.Message}", "OK");
		}
	}

	// Show all existing zones
	private async void OnShowZonesClicked(object sender, EventArgs e)
	{
		var zones = _zoneService.GetAllZones; // This is the property, not method
		var zoneList = string.Join("\n", zones.Select(z => z.DisplayText));
		
		await DisplayAlert("Silent Zones", 
			zones.Count > 0 ? zoneList : "No zones created yet", 
			"OK");
	}

	// Delete existing zones with selection dialog
	private async void OnDeleteZoneClicked(object sender, EventArgs e)
	{
		try
		{
			var zones = _zoneService.GetAllZones;
			if (zones.Count == 0)
			{
				await DisplayAlert("No Zones", "No zones available to delete", "OK");
				return;
			}

			// Create array of zone names for selection
			var zoneNames = zones.Select(z => z.Name).ToArray();
			
			// Show action sheet to select zone for deletion
			var selectedZone = await DisplayActionSheet("Select zone to delete:", "Cancel", null, zoneNames);
			
			if (selectedZone != null && selectedZone != "Cancel")
			{
				// Find the selected zone
				var zoneToDelete = zones.FirstOrDefault(z => z.Name == selectedZone);
				if (zoneToDelete != null)
				{
					// Confirm deletion
					bool confirm = await DisplayAlert("Confirm Delete", 
						$"Are you sure you want to delete '{zoneToDelete.Name}'?", 
						"Delete", "Cancel");
					
					if (confirm)
					{
						bool success = await _zoneService.DeleteZoneAsync(zoneToDelete.Id);
						if (success)
						{
							await DisplayAlert("Success", $"Zone '{zoneToDelete.Name}' deleted successfully", "OK");
							LoggingService.LogInfo($"User deleted zone: {zoneToDelete.Name}");
						}
						else
						{
							await DisplayAlert("Error", "Failed to delete zone", "OK");
						}
					}
				}
			}
		}
		catch (Exception ex)
		{
			await DisplayAlert("Error", $"Failed to delete zone: {ex.Message}", "OK");
			LoggingService.LogError($"Zone deletion error: {ex.Message}");
		}
	}

	#endregion

	#region Manual Silent Control

	// Toggles phone between silent and normal mode when button is clicked
	private async void OnSilentButtonClicked(object sender, EventArgs e)
	{
#if ANDROID
		// Check if app has permission to modify Do Not Disturb settings
		if (!await CheckDndPermission())
		{
			await RequestDndPermission();
			return;
		}

		// Get Android's audio manager to control ringer mode
		var audioManager = Platform.CurrentActivity?.GetSystemService(Android.Content.Context.AudioService) as AudioManager;
		if (audioManager != null)
		{
			if (!isSilenced)
			{
				// Turn phone to silent mode
				audioManager.RingerMode = RingerMode.Silent;
				isSilenced = true;
				await DisplayAlert("Silencer", "Phone silenced!", "OK");
			}
			else
			{
				// Turn phone back to normal mode
				audioManager.RingerMode = RingerMode.Normal;
				isSilenced = false;
				await DisplayAlert("Silencer", "Silent mode turned off!", "OK");
			}
		}
		else
		{
			await DisplayAlert("Error", "Could not access audio manager", "OK");
		}
#else
        await DisplayAlert("Not Supported", "Silent mode control is only available on Android", "OK");
#endif
	}

	#endregion

	#region Location & Step Tracking

	// Gets current GPS location and stores it for future use
	private async void StartLocationTracking()
	{
		try
		{
			var request = new GeolocationRequest
			{
				DesiredAccuracy = GeolocationAccuracy.Medium,
				Timeout = TimeSpan.FromSeconds(10)
			};

			var location = await Geolocation.GetLocationAsync(request);
			if (location != null)
			{
				lastLocation = location;
				LoggingService.LogInfo($"Location tracking started: {location.Latitude:F6}, {location.Longitude:F6}");
			}
		}
		catch (Exception ex)
		{
			LoggingService.LogError($"Location tracking failed: {ex.Message}");
			await DisplayAlert("Location Error", $"Could not get location: {ex.Message}", "OK");
		}
	}

	// Initializes accelerometer for step counting functionality
	private async void StartStepCounting()
	{
		try
		{
			bool isSupported = Accelerometer.IsSupported;
			if (isSupported)
			{
				Accelerometer.ReadingChanged += OnAccelerometerReadingChanged;
				Accelerometer.Start(SensorSpeed.Default);
				LoggingService.LogInfo("Step counting started");
			}
			else
			{
				LoggingService.LogWarning("Accelerometer not supported on this device");
			}
		}
		catch (Exception ex)
		{
			LoggingService.LogError($"Step counter initialization failed: {ex.Message}");
			await DisplayAlert("Step Counter Error", $"Could not start step counter: {ex.Message}", "OK");
		}
	}

	// Processes accelerometer data to detect steps based on movement threshold
	private void OnAccelerometerReadingChanged(object sender, AccelerometerChangedEventArgs e)
	{
		// Calculate total acceleration magnitude from X, Y, Z components
		var acceleration = Math.Sqrt(
			Math.Pow(e.Reading.Acceleration.X, 2) +
			Math.Pow(e.Reading.Acceleration.Y, 2) +
			Math.Pow(e.Reading.Acceleration.Z, 2));

		// If acceleration exceeds threshold, count as a step
		if (acceleration > 12.0)
		{
			stepCount++;
			MainThread.BeginInvokeOnMainThread(() =>
			{
				// Update UI with step count if needed in future
			});
		}
	}

	// Manual test button to trigger location service
	private async void OnTestLocationClicked(object sender, EventArgs e)
	{
		await _locationService.GetCurrentLocationAsync();
	}

	// Tests location service functionality when app starts
	private async Task TestLocationAsync()
	{
		LoggingService.LogInfo("App started - testing location service");

		// Verify location permissions are granted
		var hasPermission = await _locationService.CheckLocationPermissionsAsync();
		if (!hasPermission)
		{
			LoggingService.LogError("Location permission denied");
			return;
		}

		// Get initial location reading for testing
		var location = await _locationService.GetCurrentLocationAsync();
		if (location != null)
		{
			LoggingService.LogInfo("Initial location test completed successfully");
		}
	}

	#endregion

	#region Android Permissions

#if ANDROID
	// Checks if app has permission to modify Do Not Disturb settings on Android
	private async Task<bool> CheckDndPermission()
	{
		if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
		{
			var notificationManager = Platform.CurrentActivity?.GetSystemService(Android.Content.Context.NotificationService) as Android.App.NotificationManager;
			return notificationManager?.IsNotificationPolicyAccessGranted ?? false;
		}
		return true; // Older Android versions don't need this permission
	}

	// Prompts user to grant Do Not Disturb access permission in Android settings
	private async Task RequestDndPermission()
	{
		var intent = new Android.Content.Intent(Android.Provider.Settings.ActionNotificationPolicyAccessSettings);
		Platform.CurrentActivity?.StartActivity(intent);
		await DisplayAlert("Permission Required",
			"To control silent mode, please:\n\n1. Find your app in the list\n2. Turn ON 'Allow Do Not Disturb access'\n3. Return to the app and try again",
			"OK");
	}
#else
	// Stub for non-Android platforms
	private async Task<bool> CheckDndPermission()
	{
		await Task.CompletedTask;
		return true;
	}

	private async Task RequestDndPermission()
	{
		await DisplayAlert("Permission Required", "Do Not Disturb permission is only applicable on Android.", "OK");
	}
#endif

	#endregion
}