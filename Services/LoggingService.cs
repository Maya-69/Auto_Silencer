using System.Collections.ObjectModel;
using Silencer.Models;

namespace Silencer.Services
{
    public static class LoggingService
    {
        public static ObservableCollection<LogEntry> Logs { get; } = new();
        
        public static void LogInfo(string message)
        {
            AddLog("INFO", message);
        }
        
        public static void LogWarning(string message)
        {
            AddLog("WARN", message);
        }
        
        public static void LogError(string message)
        {
            AddLog("ERROR", message);
        }
        
        private static void AddLog(string level, string message)
        {
            var entry = new LogEntry
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };
            
            // Add to beginning for newest-first display
            Logs.Insert(0, entry);
        }
    }
}