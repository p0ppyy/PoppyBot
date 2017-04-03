using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SteamKit2;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Steambot
{
    class Program
    {
        static string user, pass;
        static string authCode, twoFactorCode;

        static StreamReader sr;

        static SteamClient steamClient;
        static CallbackManager manager;

        static SteamUser steamUser;
        static SteamFriends steamFriends;

        static String[] admins;











        static bool isRunning = false;

        static void Main(string[] args)
        {
            //Console.WriteLine("CTRL+D quits");


            user = "poppiesbot";

            pass = "brorerik99";

            loginSteam();
        }

        static void loginSteam() {

            steamClient = new SteamClient();

            manager = new CallbackManager(steamClient);

            sr = new StreamReader("//intra.lund.se/studentdata/052314/Programmering/SteamBot-master/Steambot/Steambot/Resources/admins.txt");

            admins = sr.ReadToEnd().Split();

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

        static void OnMessageReceived(SteamFriends.FriendMsgCallback callback)
        {
            Console.WriteLine(steamFriends.GetFriendPersonaName(callback.Sender) + ": " + callback.Message);
            if (callback.Message.Length > 1) {
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
                                temp = String.Join(" ", message);
                                temp = temp.Substring(command.Length + 2);

                                steamFriends.SetPersonaName(temp);
                                SendMessage(callback.Sender, "Username changed to " + temp);
                                break;
                            case "admins":
                                for (int i = 0; i < admins.Length; i++){
                                    SendMessage(callback.Sender, admins[i]);
                                
                                }
                                break;

                            default:
                                SendMessage(callback.Sender, "Command not recognized.");
                                break;
                        }
                }             
            }
        }

        public static void SendMessage(SteamID id, string message) {
            steamFriends.SendChatMessage(id , EChatEntryType.ChatMsg, message);
            Console.WriteLine(steamFriends.GetPersonaName() + ": " + message);
        
        }


    }
}
