using System;
using System.IO;
using System.Text;

namespace AdminConsole
{
    public static class Logger
    {
        private static readonly string LogDirectory = "ConsoleLogs";
        private static readonly string LogFilePath = Path.Combine(LogDirectory, $"console_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");

        static Logger()
        {
            if (!Directory.Exists(LogDirectory))
            {
                Directory.CreateDirectory(LogDirectory);
            }
        }

        public static void Log(string message, bool newLine = true)
        {
            string logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] : {message}";
            if (newLine)
            {
                Console.WriteLine(logMessage);
            }
            else
            {
                Console.Write(logMessage);
            }
            File.AppendAllText(LogFilePath, logMessage + Environment.NewLine);
        }
    }
}
