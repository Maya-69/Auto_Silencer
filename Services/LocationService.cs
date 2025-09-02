using Silencer.Models;

namespace Silencer.Services;

public class LocationService
{
    private DateTime _lastLogTime = DateTime.MinValue;
    private Location _lastLocation;
    
    public async Task<Location> GetCurrentLocationAsync()
    {
        // Only log every 5 seconds to avoid spam
        bool shouldLog = DateTime.Now - _lastLogTime > TimeSpan.FromSeconds(5);
        
        if (shouldLog)
        {
            LoggingService.LogInfo("Requesting current location...");
        }
        
        try
        {
            var request = new GeolocationRequest
            {
                DesiredAccuracy = GeolocationAccuracy.High,
                Timeout = TimeSpan.FromSeconds(10)
            };
            
            var location = await Geolocation.GetLocationAsync(request);
            
            if (location != null && shouldLog)
            {
                LoggingService.LogInfo($"Lat: {location.Latitude:F6}, Lng: {location.Longitude:F6}, Alt: {location.Altitude:F1}m");
                LoggingService.LogInfo($"Accuracy: {location.Accuracy:F1}m");
                _lastLogTime = DateTime.Now;
            }
            
            _lastLocation = location;
            return location;
        }
        catch (Exception ex)
        {
            if (shouldLog)
            {
                LoggingService.LogError($"Location failed: {ex.Message}");
                _lastLogTime = DateTime.Now;
            }
            return null;
        }
    }

    public async Task<bool> CheckLocationPermissionsAsync()
    {
        LoggingService.LogInfo("Checking location permissions...");
        
        var status = await Permissions.CheckStatusAsync<Permissions.LocationWhenInUse>();

        if (status != PermissionStatus.Granted)
        {
            LoggingService.LogWarning("Requesting location permission...");
            status = await Permissions.RequestAsync<Permissions.LocationWhenInUse>();
        }

        bool granted = status == PermissionStatus.Granted;
        
        if (granted)
        {
            LoggingService.LogInfo("Location permission granted");
        }
        else
        {
            LoggingService.LogError("Location permission denied");
        }

        return granted;
    }
}