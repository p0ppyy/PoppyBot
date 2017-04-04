using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Web;
using System.Net;

namespace Steambot
{
    class Program
    {
        static string user, pass;
        static string authCode, twoFactorCode;

        static SteamClient steamClient;
        static CallbackManager manager;

        static SteamUser steamUser;
        static SteamFriends steamFriends;

        static String[] admins;

        static bool isRunning = false;

        static void Main(string[] args)
        {
            Console.WriteLine("CTRL+D quits");

            Console.Write("Enter steam username: ");

            user = Console.ReadLine();

            ConsoleKeyInfo key;

            Console.Write("Enter steam password: ");

            do {
                key = Console.ReadKey(true);

                if (key.Key != ConsoleKey.Backspace && key.Key != ConsoleKey.Enter) {
                    pass += key.KeyChar;
                    Console.Write("*");
                } else {
                    if (key.Key == ConsoleKey.Backspace && pass.Length > 0) {
                        pass = pass.Substring(0, (pass.Length - 1));
                        Console.Write("\b \b");
                    }
                }

            } while (key.Key != ConsoleKey.Enter);

            Console.WriteLine("");

            loginSteam();
        }

        static void loginSteam() {

            steamClient = new SteamClient();

            manager = new CallbackManager(steamClient);

            steamUser = steamClient.GetHandler<SteamUser>();

            steamFriends = steamClient.GetHandler<SteamFriends>();

            manager.Subscribe<SteamClient.ConnectedCallback>(onConnected);
            manager.Subscribe<SteamClient.DisconnectedCallback>(onDisconnected);


            manager.Subscribe<SteamUser.LoggedOnCallback>(onLoggedOn);

            manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(onMachineAuth);

            manager.Subscribe<SteamUser.AccountInfoCallback>(onAccountInfo);
            manager.Subscribe<SteamFriends.FriendsListCallback>(onFriendList);
            manager.Subscribe<SteamFriends.PersonaStateCallback>(onPersonaState);
            manager.Subscribe<SteamFriends.FriendAddedCallback>(onFriendAdded);
            manager.Subscribe<SteamFriends.FriendMsgCallback>(OnMessageReceived);

            steamClient.Connect();

            isRunning = true;

            while (isRunning) {
                manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
            }

            Console.ReadKey();

        }
        static void onConnected(SteamClient.ConnectedCallback callback) {
            if (callback.Result != EResult.OK) {
                Console.WriteLine("Unable to connect to Steam: {0}", callback.Result);
                isRunning = false;
                return;
            }
            Console.WriteLine("Connected \nLogging in... \n");

            byte[] sentryHash = null;
            if (File.Exists("sentry.bin"))
            {
                
                byte[] sentryFile = File.ReadAllBytes("sentry.bin");
                sentryHash = CryptoHelper.SHAHash(sentryFile);
            }

            steamUser.LogOn(new SteamUser.LogOnDetails
            {
                Username = user,
                Password = pass,
                AuthCode = authCode,
                TwoFactorCode = twoFactorCode,
                SentryFileHash = sentryHash,

            });

        }

        static void onDisconnected(SteamClient.DisconnectedCallback callback) {
            Console.WriteLine("Disconnected from Steam, reconnecting in 5...");

            Thread.Sleep(TimeSpan.FromSeconds(5));

            steamClient.Connect();

        }

        static void onLoggedOn(SteamUser.LoggedOnCallback callback) {

            bool isSteamGuard = callback.Result == EResult.AccountLogonDenied;
            bool is2FA = callback.Result == EResult.AccountLoginDeniedNeedTwoFactor;

            if (isSteamGuard || is2FA)
            {
                Console.WriteLine("This account is SteamGuard protected!");

                if (is2FA) {
                    Console.Write("Please enter 2 factor auth code form phone app: ");
                    twoFactorCode = Console.ReadLine();
                }
                else
                {
                    Console.Write("Please enter auth code sent to {0} here:", callback.EmailDomain);
                    authCode = Console.ReadLine();

                }

                return;

            }

            if (callback.Result != EResult.OK)
            {
                Console.WriteLine("Unable to logon to Steam: {0} / {1}", callback.Result, callback.ExtendedResult);

                isRunning = false;
                return;
            }


            Console.WriteLine("{0}, succesfully logged in!", user);
            
            
        }

        static void onPersonaState(SteamFriends.PersonaStateCallback callback) {

            Console.WriteLine(callback.Name + " is now " + callback.State);

        }

        static void onLoggedOut(SteamUser.LoggedOffCallback callback) {
            Console.WriteLine("Logging of: {0}", callback.Result);
        }

        static void onAccountInfo(SteamUser.AccountInfoCallback callback)
        {
            steamFriends.SetPersonaState(EPersonaState.Online);
            Console.WriteLine("State: {0}", steamFriends.GetPersonaState());
        }


        static void onFriendList(SteamFriends.FriendsListCallback callback) {

            int friendCount = steamFriends.GetFriendCount();

            Console.WriteLine("You have {0} friends", friendCount);

            foreach (var friend in callback.FriendList) {
                if(friend.Relationship == EFriendRelationship.RequestRecipient)
                {
                    steamFriends.AddFriend(friend.SteamID);

                }
            }
        }

        static void onFriendAdded(SteamFriends.FriendAddedCallback callback) {

            Console.WriteLine("{0} was added as a friend", callback.PersonaName);

        }


        static void onMachineAuth(SteamUser.UpdateMachineAuthCallback callback) {
            Console.WriteLine("Updating sentryfile...");

            int filesize;
            byte[] sentryHash;
            using (var fs = File.Open("sentry.bin", FileMode.OpenOrCreate, FileAccess.ReadWrite)) {
                fs.Seek(callback.Offset, SeekOrigin.Begin);
                fs.Write(callback.Data, 0, callback.BytesToWrite);
                filesize = (int)fs.Length;

                fs.Seek(0, SeekOrigin.Begin);
                using (var sha = new SHA1CryptoServiceProvider()) {
                    sentryHash = sha.ComputeHash(fs);
                }
            }

            steamUser.SendMachineAuthResponse(new SteamUser.MachineAuthDetails
            {
                JobID = callback.JobID,

                FileName = callback.FileName,

                BytesWritten = callback.BytesToWrite,
                FileSize = filesize,
                Offset = callback.Offset,

                Result = EResult.OK,
                LastError = 0,

                OneTimePassword = callback.OneTimePassword,

                SentryFileHash = sentryHash,


            });

            Console.WriteLine("Done! "); 

        }

        static void OnMessageReceived(SteamFriends.FriendMsgCallback callback){
         
            if (callback.Message.Length > 1) {

                bool isAdmin = false;
                bool isOwner = false;

                if (File.ReadAllText("admins.txt").Contains(callback.Sender.ToString())) {
                    isAdmin = true;
                }

                if (File.ReadAllText("owners.txt").Contains(callback.Sender.ToString())) {
                    isOwner = true;
                }

                Console.WriteLine(steamFriends.GetFriendPersonaName(callback.Sender) + ":" + callback.Sender + ": " + callback.Message);
                String[] message = callback.Message.Split();

                if (message[0].Substring(0, 1).Equals("!"))
                {
                    string command = message[0].Substring(1, message[0].Length - 1);

                    String temp;

                    switch (command)
                    {
                        case "hi":
                            SendMessage(callback.Sender, "Hi " + steamFriends.GetFriendPersonaName(callback.Sender));
                            break;
                        case "name":
                            if (isOwner) {
                                temp = String.Join(" ", message);
                                temp = temp.Substring(command.Length + 2);

                                steamFriends.SetPersonaName(temp);
                                SendMessage(callback.Sender, "Username changed to " + temp);
                            }

                            break;
                        case "admins":
                            foreach (string s in File.ReadAllText("admins.txt").Split(new string[] { Environment.NewLine }, StringSplitOptions.None)) {
                                SendMessage(callback.Sender, steamFriends.GetFriendPersonaName(new SteamID(s)));
                            }
                                

                            break;

                        case "addadmin":
                            if (isOwner) {
                                if (message.Length > 1)
                                {
                                    string admin = message[1];
                                    if (admin.StartsWith("STEAM_"))
                                    {
                                        File.AppendAllText("admins.txt", Environment.NewLine + admin);
                                        SendMessage(callback.Sender, steamFriends.GetFriendPersonaName(new SteamID(admin)) + " is now admin");

                                    }
                                    else {
                                        SendMessage(callback.Sender, "Wrong SteamID, use https://steamid.io/lookup to get SteamID ");
                                    }
                                } else {
                                    SendMessage(callback.Sender, "Usage !addadmin SteamID");
                                }

                            }
                            else {
                                SendMessage(callback.Sender, "You're not an admin");
                            }
                            break;

                        case "removeadmin":
                            if (isOwner) {
                                if (message.Length > 1) {
                                    string admin = message[1];
                                    if (admin.StartsWith("STEAM_")) {

                                        if (File.ReadAllText("admins.txt").Contains(admin)) {
                                            string[] tempText = File.ReadAllText("admins.txt").Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
                                            List<string> tempList = new List<string>();                                       
                                            for (int i = 0; i < tempText.Length; i++) {
                                                if (!tempText[i].ToLower().Equals(admin.ToLower())) {
                                                    tempList.Add(tempText[i]);
                                                }
                                                                                                   
                                            }
                                            File.WriteAllText("admins.txt", String.Join(Environment.NewLine, tempList));
                                            SendMessage(callback.Sender, steamFriends.GetFriendPersonaName(new SteamID(admin)) + " has no powers anymore");
                                        }

                                    } else {
                                        SendMessage(callback.Sender, "Wrong SteamID, use https://steamid.io/lookup to get SteamID ");
                                    }

                                } else {
                                    SendMessage(callback.Sender, "Usage !addadmin SteamID");
                                }

                            }

                            break;

                        case "addlink":
                            if (message.Length > 1) {
                                if (Uri.IsWellFormedUriString(message[1], UriKind.RelativeOrAbsolute)) {
                                    if (!File.Exists("links.txt")) {
                                        File.AppendAllText("links.txt", message[1]);
                                        SendMessage(callback.Sender, message[1] + " added to random links.");
                                    } else {
                                        File.AppendAllText("links.txt", Environment.NewLine + message[1]);
                                        SendMessage(callback.Sender, message[1] + " added to random links.");
                                    }
                                    
                                }

                            } else {
                                SendMessage(callback.Sender, "Usage !addlink url");
                            }
                            break;

                        case "randomlink":
                            Random rand = new Random();
                            string[] urls = File.ReadAllLines("links.txt");
                            int index = rand.Next(urls.Length);
                            SendMessage(callback.Sender, urls[index]);
                            break;

                        default:
                            SendMessage(callback.Sender, "Command not recognized.");
                            break;
                    }
                }             
            }
        }

        static void SendMessage(SteamID id, string message) {
            steamFriends.SendChatMessage(id , EChatEntryType.ChatMsg, message);
            Console.WriteLine(steamFriends.GetPersonaName() + ": " + message);
        
        }

    }
}
