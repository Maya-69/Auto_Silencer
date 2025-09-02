namespace Silencer.Models
{
    public class SilentZone
    {
        public string Id { get; set; } = Guid.NewGuid().ToString(); // Unique identifier
        public string Name { get; set; } = String.Empty; // Name of the silent zone
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Radius { get; set; } // in meters
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public bool IsActive { get; set; } // can disable 

        public double DistanceFromLocation(double currentLat, double currentLng)
        {
            const double EarthRadius = 6371000; // Earth's Radius meters
            double lat1Rad = DegreesToRadians(Latitude);
            double lat2Rad = DegreesToRadians(currentLat);
            double deltaLatRad = DegreesToRadians(currentLat - Latitude);
            double deltaLngRad = DegreesToRadians(currentLng - Longitude);

            double a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                      Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                      Math.Sin(deltaLngRad / 2) * Math.Sin(deltaLngRad / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

            return EarthRadius * c; // Distance in meters
        }

        // Check if inside the zone
        public bool IsLocationInZone(double currentLat, double currentLng)
        {
            double distance = DistanceFromLocation(currentLat, currentLng);
            return distance <= Radius;
        }

        // Convert Degrees to Radians
        private double DegreesToRadians(double degrees)
        {
            return degrees * (Math.PI / 180);
        }

        public string DisplayText => $"{Name} ({Latitude:F4}, {Longitude:F4}) ±{Radius}m";
       
    }
}