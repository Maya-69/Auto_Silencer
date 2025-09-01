using Android.Media;


namespace Silencer;

public partial class MainPage : ContentPage
{
    private bool isSilenced = false;
    private int stepCount = 0;
    private Location lastLocation;

    public MainPage()
    {
        InitializeComponent();
        StartLocationTracking();
        StartStepCounting();
    }

    private async void OnSilentButtonClicked(object sender, EventArgs e)
    {
#if ANDROID
        if (!await CheckDndPermission())
        {
            await RequestDndPermission();
            return;
        }

        var audioManager = Platform.CurrentActivity?.GetSystemService(Android.Content.Context.AudioService) as AudioManager;
        if (audioManager != null)
        {
            if (!isSilenced)
            {
                audioManager.RingerMode = RingerMode.Silent;
                isSilenced = true;
                await DisplayAlert("Silencer", "Phone silenced!", "OK");
            }
            else
            {
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
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Location Error", $"Could not get location: {ex.Message}", "OK");
        }
    }

    private async void StartStepCounting()
    {
        try
        {
            bool isSupported = Accelerometer.IsSupported;
            if (isSupported)
            {
                Accelerometer.ReadingChanged += OnAccelerometerReadingChanged;
                Accelerometer.Start(SensorSpeed.Default);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Step Counter Error", $"Could not start step counter: {ex.Message}", "OK");
        }
    }

    private void OnAccelerometerReadingChanged(object sender, AccelerometerChangedEventArgs e)
    {
        var acceleration = Math.Sqrt(
            Math.Pow(e.Reading.Acceleration.X, 2) +
            Math.Pow(e.Reading.Acceleration.Y, 2) +
            Math.Pow(e.Reading.Acceleration.Z, 2));

        if (acceleration > 12.0)
        {
            stepCount++;
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Update UI with step count if needed
            });
        }
    }

#if ANDROID
    private async Task<bool> CheckDndPermission()
    {
        if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.M)
        {
            var notificationManager = Platform.CurrentActivity?.GetSystemService(Android.Content.Context.NotificationService) as Android.App.NotificationManager;
            return notificationManager?.IsNotificationPolicyAccessGranted ?? false;
        }
        return true;
    }

    private async Task RequestDndPermission()
    {
        var intent = new Android.Content.Intent(Android.Provider.Settings.ActionNotificationPolicyAccessSettings);
        Platform.CurrentActivity?.StartActivity(intent);
        await DisplayAlert("Permission Required",
            "To control silent mode, please:\n\n1. Find your app in the list\n2. Turn ON 'Allow Do Not Disturb access'\n3. Return to the app and try again",
            "OK");
    }
#endif
}