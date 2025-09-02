using System.Collections.ObjectModel;
using Silencer.Models;
using System.Text.Json;

namespace Silencer.Services
{
    public class SilentZoneService
    {
        private readonly string _dataFilePath;
        private readonly ObservableCollection<SilentZone> _zones;
        private readonly object _zoneLock = new object();
        private int _nextZoneNumber = 1;

        public SilentZoneService()
        {
            _dataFilePath = Path.Combine(FileSystem.AppDataDirectory, "silent_zones.json");
            _zones = new ObservableCollection<SilentZone>();
            _ = LoadZonesAsync();
        }

        #region Properties

        public ObservableCollection<SilentZone> GetAllZones 
        { 
            get { return _zones; } 
        }

        public List<SilentZone> GetZonesCopy()
        {
            lock (_zoneLock)
            {
                return new List<SilentZone>(_zones);
            }
        }

        public int GetZoneCount() 
        {
            lock (_zoneLock)
            {
                return _zones.Count;
            }
        }

        public int GetActiveZoneCount() 
        {
            lock (_zoneLock)
            {
                return _zones.Count(z => z.IsActive);
            }
        }

        #endregion

        #region Zone Creation

        public async Task<SilentZone> CreateZoneAsync(double latitude, double longitude, double radius = 5.0, string customName = null)
        {
            LoggingService.LogInfo($"Creating zone at {latitude:F6}, {longitude:F6}, radius {radius:F1}m");

            var zone = new SilentZone
            {
                Name = customName ?? GenerateZoneName(),
                Latitude = latitude,
                Longitude = longitude,
                Radius = radius,
                IsActive = true
            };

            // ALWAYS modify ObservableCollection on main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                lock (_zoneLock)
                {
                    _zones.Add(zone);
                }
            });

            await SaveZonesAsync();
            LoggingService.LogInfo($"Created zone '{zone.Name}'");
            return zone;
        }

        public async Task<SilentZone> CreateZoneAtCurrentLocationAsync(LocationService locationService, double radius = 5.0, string customName = null)
        {
            var location = await locationService.GetCurrentLocationAsync();
            if (location == null)
            {
                LoggingService.LogError("Cannot create zone: Current location unavailable");
                throw new InvalidOperationException("Could not get current location");
            }

            return await CreateZoneAsync(location.Latitude, location.Longitude, radius, customName);
        }

        #endregion

        #region Zone Management

        public async Task<bool> UpdateZoneAsync(SilentZone zone)
        {
            SilentZone existingZone = null;
            
            lock (_zoneLock)
            {
                existingZone = _zones.FirstOrDefault(z => z.Id == zone.Id);
            }

            if (existingZone == null)
            {
                LoggingService.LogError($"Zone {zone.Name} not found for update");
                return false;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                lock (_zoneLock)
                {
                    existingZone.Name = zone.Name;
                    existingZone.Radius = zone.Radius;
                    existingZone.IsActive = zone.IsActive;
                    existingZone.UpdatedAt = DateTime.Now;
                }
            });

            await SaveZonesAsync();
            LoggingService.LogInfo($"Updated zone '{zone.Name}'");
            return true;
        }

        public async Task<bool> DeleteZoneAsync(string zoneId)
        {
            if (string.IsNullOrEmpty(zoneId))
            {
                LoggingService.LogError("Cannot delete zone: Zone ID is null or empty");
                return false;
            }

            SilentZone zoneToRemove = null;
            string zoneName = "";

            lock (_zoneLock)
            {
                zoneToRemove = _zones.FirstOrDefault(z => z.Id == zoneId);
                if (zoneToRemove != null)
                {
                    zoneName = zoneToRemove.Name;
                }
            }

            if (zoneToRemove == null)
            {
                LoggingService.LogWarning($"Zone with ID '{zoneId}' not found for deletion");
                return false;
            }

            bool removed = false;
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                lock (_zoneLock)
                {
                    removed = _zones.Remove(zoneToRemove);
                }
            });
            
            if (removed)
            {
                await SaveZonesAsync();
                LoggingService.LogInfo($"Deleted zone '{zoneName}'");
                return true;
            }
            else
            {
                LoggingService.LogError($"Failed to remove zone '{zoneName}'");
                return false;
            }
        }

        public async Task<bool> DeleteZoneByNameAsync(string zoneName)
        {
            if (string.IsNullOrEmpty(zoneName))
            {
                LoggingService.LogError("Cannot delete zone: Zone name is null or empty");
                return false;
            }

            SilentZone zone = null;
            lock (_zoneLock)
            {
                zone = _zones.FirstOrDefault(z => z.Name.Equals(zoneName, StringComparison.OrdinalIgnoreCase));
            }

            if (zone == null)
            {
                LoggingService.LogWarning($"Zone '{zoneName}' not found for deletion");
                return false;
            }

            return await DeleteZoneAsync(zone.Id);
        }

        public async Task<bool> ToggleZoneActiveStateAsync(string zoneId)
        {
            var zone = GetZoneById(zoneId);
            if (zone == null)
            {
                LoggingService.LogError($"Zone with ID '{zoneId}' not found for toggle");
                return false;
            }

            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                lock (_zoneLock)
                {
                    zone.IsActive = !zone.IsActive;
                    zone.UpdatedAt = DateTime.Now;
                }
            });
            
            await SaveZonesAsync();
            LoggingService.LogInfo($"Zone '{zone.Name}' is now {(zone.IsActive ? "ACTIVE" : "INACTIVE")}");
            return true;
        }

        #endregion

        #region Zone Queries

        public SilentZone GetZoneById(string zoneId)
        {
            lock (_zoneLock)
            {
                return _zones.FirstOrDefault(z => z.Id == zoneId);
            }
        }

        public SilentZone GetZoneByName(string zoneName)
        {
            lock (_zoneLock)
            {
                return _zones.FirstOrDefault(z => z.Name.Equals(zoneName, StringComparison.OrdinalIgnoreCase));
            }
        }

        public List<SilentZone> GetActiveZonesAtLocation(double latitude, double longitude)
        {
            List<SilentZone> activeZones;
            
            lock (_zoneLock)
            {
                activeZones = _zones.Where(z => z.IsActive && z.IsLocationInZone(latitude, longitude)).ToList();
            }
            
            if (activeZones.Any())
            {
                LoggingService.LogInfo($"Found {activeZones.Count} active zone(s) at location");
            }
            
            return activeZones;
        }

        public List<SilentZone> GetZonesNearLocation(double latitude, double longitude, double maxDistanceMeters = 100.0)
        {
            lock (_zoneLock)
            {
                return _zones.Where(z => z.DistanceFromLocation(latitude, longitude) <= maxDistanceMeters).ToList();
            }
        }

        #endregion

        #region Utility Methods

        private string GenerateZoneName()
        {
            string name;
            int attempts = 0;
            
            lock (_zoneLock)
            {
                do
                {
                    name = $"Zone {_nextZoneNumber++}";
                    attempts++;
                    
                    if (attempts > 1000)
                    {
                        name = $"Zone {Guid.NewGuid().ToString()[..8]}";
                        break;
                    }
                }
                while (_zones.Any(z => z.Name.Equals(name, StringComparison.OrdinalIgnoreCase)));
            }
            
            return name;
        }

        private void UpdateNextZoneNumber()
        {
            var maxNumber = 0;
            
            lock (_zoneLock)
            {
                foreach (var zone in _zones)
                {
                    if (zone.Name.StartsWith("Zone ", StringComparison.OrdinalIgnoreCase) && 
                        int.TryParse(zone.Name.Substring(5), out int number) && 
                        number > maxNumber)
                    {
                        maxNumber = number;
                    }
                }
            }
            
            _nextZoneNumber = maxNumber + 1;
            LoggingService.LogInfo($"Next zone number updated to: {_nextZoneNumber}");
        }

        #endregion

        #region Data Persistence

        private async Task SaveZonesAsync()
        {
            try
            {
                var directory = Path.GetDirectoryName(_dataFilePath);
                if (!Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                List<SilentZone> zonesToSave;
                lock (_zoneLock)
                {
                    zonesToSave = new List<SilentZone>(_zones);
                }

                var json = JsonSerializer.Serialize(zonesToSave, new JsonSerializerOptions
                {
                    WriteIndented = true
                });

                await File.WriteAllTextAsync(_dataFilePath, json);
                LoggingService.LogInfo($"Saved {zonesToSave.Count} zones to storage");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to save zones: {ex.Message}");
            }
        }

        private async Task LoadZonesAsync()
        {
            try
            {
                if (!File.Exists(_dataFilePath))
                {
                    LoggingService.LogInfo("No existing zone file found");
                    return;
                }

                var json = await File.ReadAllTextAsync(_dataFilePath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    LoggingService.LogWarning("Zone file is empty");
                    return;
                }

                var loadedZones = JsonSerializer.Deserialize<List<SilentZone>>(json);

                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    lock (_zoneLock)
                    {
                        _zones.Clear();
                        if (loadedZones != null && loadedZones.Any())
                        {
                            foreach (var zone in loadedZones)
                            {
                                if (IsValidZone(zone))
                                {
                                    _zones.Add(zone);
                                }
                                else
                                {
                                    LoggingService.LogWarning($"Skipped invalid zone: {zone?.Name ?? "Unknown"}");
                                }
                            }

                            UpdateNextZoneNumber();
                            LoggingService.LogInfo($"Loaded {_zones.Count} valid zones");
                        }
                        else
                        {
                            LoggingService.LogInfo("No valid zones found in file");
                        }
                    }
                });
            }
            catch (JsonException jsonEx)
            {
                LoggingService.LogError($"JSON parsing error: {jsonEx.Message}");
                await BackupCorruptedFile();
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to load zones: {ex.Message}");
            }
        }

        private bool IsValidZone(SilentZone zone)
        {
            return zone != null &&
                   !string.IsNullOrWhiteSpace(zone.Id) &&
                   !string.IsNullOrWhiteSpace(zone.Name) &&
                   zone.Latitude >= -90 && zone.Latitude <= 90 &&
                   zone.Longitude >= -180 && zone.Longitude <= 180 &&
                   zone.Radius > 0 && zone.Radius <= 10000;
        }

        private async Task BackupCorruptedFile()
        {
            try
            {
                var backupPath = $"{_dataFilePath}.corrupted.{DateTime.Now:yyyyMMdd_HHmmss}.bak";
                File.Copy(_dataFilePath, backupPath);
                LoggingService.LogInfo($"Corrupted file backed up to: {backupPath}");
            }
            catch (Exception ex)
            {
                LoggingService.LogError($"Failed to backup corrupted file: {ex.Message}");
            }
        }

        #endregion
    }
}