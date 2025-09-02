namespace Silencer.Models
{
    public class LogEntry
    {
        public DateTime Timestamp { get; set; }
        public string Level { get; set; }
        public string Message { get; set; }
        public string FormattedTime => Timestamp.ToString("HH:mm:ss.fff");

        public string LevelColor => Level switch
        {
            "INFO" => "#00FF00",    // Green
            "WARN" => "#FFA500",    // Orange  
            "ERROR" => "#FF0000",   // Red
            _ => "#FFFFFF"          // White
        };
        
    }
}