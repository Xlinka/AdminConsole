using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SkyFrost.Base;

namespace AdminConsole
{
    internal class Program
    {

        private static Queue<Message> _messageQueue = new Queue<Message>();
        private static string currentUser = "U-Resonite";

        private static async Task Main(string[] args)
        {
           


            Console.OutputEncoding = Encoding.UTF8;
            Console.InputEncoding = Encoding.UTF8;
            var config = SkyFrostConfig.SKYFROST_PRODUCTION.WithUserAgent("AdminConsole", "1.0.5");

            Logger.Log($"Program started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");

            string machineId = GenerateRandomMachineId();
            Logger.Log($"Generated machine ID: {machineId}");

            string uid = GenerateUID(machineId);
            Logger.Log($"Generated UID: {uid}");

            var skyFrost = new SkyFrostInterface(uid, machineId, config);

            bool loggedIn = false;
            do
            {
                Console.ForegroundColor = ConsoleColor.White;
                Logger.Log("Prompting for login...");

                await Task.Delay(1000);  // Adding delay before login prompt

                Logger.Log("Login: ", false);
                string login = Console.ReadLine();
                Logger.Log(login, false);

                Logger.Log("Password: ", false);
                string pass = ReadPassword();
                Logger.Log("Attempting to log in...");

                var loginResult = await skyFrost.Session.Login(login, new PasswordLogin(pass), machineId, rememberMe: false, null);

                if (loginResult.Content == "TOTP")
                {
                    Logger.Log("2FA Code required...");
                    Logger.Log("2FA Code: ", false);
                    string code = Console.ReadLine();
                    Logger.Log("Attempting 2FA login...");
                    loginResult = await skyFrost.Session.Login(login, new PasswordLogin(pass), machineId, rememberMe: false, code);
                }

                if (loginResult.IsError)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Logger.Log("Error logging in!\t" + loginResult.Content);
                }
                else
                {
                    loggedIn = true;
                    Logger.Log("Login successful. Updating user status to Online...");
                    // Update user status to Online
                    await UpdateUserStatus(skyFrost);
                }
            } while (!loggedIn);

            Console.Clear();
            var cancelToken = new CancellationTokenSource();
            var skyFrostMessages = skyFrost.Messages.GetUserMessages(currentUser);
            skyFrost.Messages.OnMessageReceived += Messages_OnMessageReceived;

            Logger.Log("Setting up message listener...");
            skyFrost.Update();

            while (!cancelToken.IsCancellationRequested)
            {
                Console.ForegroundColor = ConsoleColor.DarkYellow;
                Logger.Log($"{skyFrost.CurrentUsername}> ", false);
                Console.ForegroundColor = ConsoleColor.Yellow;

                string command = Console.ReadLine().Trim();
                Logger.Log($"Received command: {command}");

                if (string.IsNullOrEmpty(command))
                {
                    Logger.Log("Empty command. Skipping...");
                    continue;
                }

                if (command[0] == '/')
                {
                    command = command[1..];
                }

                if (command.ToLower() == "exit")
                {
                    Logger.Log("Exit command received. Canceling...");
                    cancelToken.Cancel();
                    break;
                }

                if (command.StartsWith("changeuser "))
                {
                    currentUser = "U-" + command.Substring(11).Trim();
                    Logger.Log($"Changed current user to: {currentUser}");
                    skyFrostMessages = skyFrost.Messages.GetUserMessages(currentUser);
                    continue;
                }

                if (currentUser == "U-Resonite")
                {
                    command = "/" + command;
                }

                Logger.Log($"Sending command: {command}");
                if (!(await skyFrostMessages.SendTextMessage(command)))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Logger.Log("Error sending message!");
                    continue;
                }

                Logger.Log("Waiting for response...");
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
                    Logger.Log($"{currentUser}: ", false);
                    Logger.Log(msg.Content);
                }

                skyFrostMessages.MarkAllRead();
            }

            Console.ForegroundColor = ConsoleColor.White;
            Logger.Log("Logging out...");
            await skyFrost.Session.FinalizeSession();
            Logger.Log("Program ended.");
        }

        private static void Messages_OnMessageReceived(Message obj)
        {
            Logger.Log($"Message received from {obj.SenderId}: {obj.Content}");
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
                AppVersion = "AdminConsole 1.0.5",
                IsMobile = false
            };
            Logger.Log($"Broadcasting user status: {status}");
            await skyFrost.HubClient.BroadcastStatus(status, BroadcastTarget.ALL_CONTACTS);
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
