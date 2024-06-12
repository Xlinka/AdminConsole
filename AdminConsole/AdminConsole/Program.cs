using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using SkyFrost.Base;

namespace AdminConsole
{
    internal class Program
    {
        private static Queue<Message> _messageQueue = new Queue<Message>();
        private static readonly string LogFilePath = $"console_log_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        private static string currentUser = "U-Resonite";

        private static async Task Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            var config = SkyFrostConfig.SKYFROST_PRODUCTION.WithUserAgent("AdminConsole", "1.0.0");

            Log($"Program started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            string machineId = GenerateRandomMachineId();
            Log($"Generated machine ID: {machineId}");

            string uid = GenerateUID(machineId);
            Log($"Generated UID: {uid}");

            var skyFrost = new SkyFrostInterface(uid, machineId, config);

            bool loggedIn = false;
            do
            {
                Console.ForegroundColor = ConsoleColor.White;
                Log("Prompting for login...");

                await Task.Delay(1000);  // Adding delay before login prompt

                Log("Login: ", false);
                string login = Console.ReadLine();
                Log(login, false);

                Log("Password: ", false);
                string pass = ReadPassword();
                Log("Attempting to log in...");

                var loginResult = await skyFrost.Session.Login(login, new PasswordLogin(pass), machineId, rememberMe: false, null);

                if (loginResult.Content == "TOTP")
                {
                    Log("2FA Code required...");
                    Log("2FA Code: ", false);
                    string code = Console.ReadLine();
                    Log("Attempting 2FA login...");
                    loginResult = await skyFrost.Session.Login(login, new PasswordLogin(pass), machineId, rememberMe: false, code);
                }

                if (loginResult.IsError)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Log("Error logging in!\t" + loginResult.Content);
                }
                else
                {
                    loggedIn = true;
                    Log("Login successful. Updating user status to Online...");
                    // Update user status to Online
                    await UpdateUserStatus(skyFrost);
                }
            } while (!loggedIn);

            Console.Clear();
            var cancelToken = new CancellationTokenSource();
            var skyFrostMessages = skyFrost.Messages.GetUserMessages(currentUser);
            skyFrost.Messages.OnMessageReceived += Messages_OnMessageReceived;

            Log("Setting up message listener...");
            skyFrost.Update();

            while (!cancelToken.IsCancellationRequested)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Log($"{skyFrost.CurrentUsername}> ", false);
                Console.ForegroundColor = ConsoleColor.Yellow;

                string command = Console.ReadLine().Trim();
                Log($"Received command: {command}");

                if (string.IsNullOrEmpty(command))
                {
                    Log("Empty command. Skipping...");
                    continue;
                }

                if (command[0] == '/')
                {
                    command = command[1..];
                }

                if (command.ToLower() == "exit")
                {
                    Log("Exit command received. Canceling...");
                    cancelToken.Cancel();
                    break;
                }

                if (command.StartsWith("changeuser "))
                {
                    currentUser = command.Substring(11).Trim();
                    Log($"Changed current user to: {currentUser}");
                    skyFrostMessages = skyFrost.Messages.GetUserMessages(currentUser);
                    continue;
                }

                Log($"Sending command: /{command}");
                if (!(await skyFrostMessages.SendTextMessage("/" + command)))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Log("Error sending message!");
                    continue;
                }

                Log("Waiting for response...");
                var s = Stopwatch.StartNew();
                while (_messageQueue.Count == 0 && s.ElapsedMilliseconds < 5000)
                {
                    await Task.Delay(250);
                    skyFrost.Update();
                }

                while (_messageQueue.Count > 0)
                {
                    var msg = _messageQueue.Dequeue();
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Log($"{currentUser}: ", false);
                    Log(msg.Content);
                }

                skyFrostMessages.MarkAllRead();
            }

            Console.ForegroundColor = ConsoleColor.White;
            Log("Logging out...");
            await skyFrost.Session.FinalizeSession();
            Log("Program ended.");
        }

        private static void Messages_OnMessageReceived(Message obj)
        {
            Log($"Message received from {obj.SenderId}: {obj.Content}");
            if (obj.SenderId == currentUser)
            {
                _messageQueue.Enqueue(obj);
            }
        }

        private static string GenerateRandomMachineId()
        {
            const string characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789_";
            var random = new Random();
            var result = new char[128];
            for (int i = 0; i < result.Length; i++)
            {
                result[i] = characters[random.Next(characters.Length)];
            }
            return new string(result);
        }

        private static string GenerateUID(string machineId)
        {
            using (var sha256 = SHA256.Create())
            {
                var data = Encoding.UTF8.GetBytes("AdminConsole-" + machineId);
                var hash = sha256.ComputeHash(data);
                return BitConverter.ToString(hash).Replace("-", "").ToUpper();
            }
        }

        private static async Task UpdateUserStatus(SkyFrostInterface skyFrost)
        {
            var status = new UserStatus
            {
                UserId = skyFrost.CurrentUserID,
                OnlineStatus = OnlineStatus.Online,
                OutputDevice = OutputDevice.Unknown,
                SessionType = UserSessionType.GraphicalClient,
                UserSessionId = skyFrost.CurrentUserID,
                IsPresent = true,
                LastPresenceTimestamp = DateTime.UtcNow,
                LastStatusChange = DateTime.UtcNow,
                CompatibilityHash = "adminconsole",
                AppVersion = "AdminConsole 1.0.2",
                IsMobile = false
            };
            Log($"Broadcasting user status: {status}");
            await skyFrost.HubClient.BroadcastStatus(status, BroadcastTarget.ALL_CONTACTS);
        }

        private static void Log(string message, bool newLine = true)
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

        private static string ReadPassword()
        {
            StringBuilder password = new StringBuilder();
            while (true)
            {
                ConsoleKeyInfo key = Console.ReadKey(intercept: true);
                if (key.Key == ConsoleKey.Enter)
                {
                    Console.WriteLine();
                    break;
                }
                else if (key.Key == ConsoleKey.Backspace)
                {
                    if (password.Length > 0)
                    {
                        password.Length--;
                        Console.Write("\b \b");
                    }
                }
                else
                {
                    password.Append(key.KeyChar);
                    Console.Write("*");
                }
            }
            return password.ToString();
        }
    }
}
