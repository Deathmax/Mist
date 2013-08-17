using System;
using System.Web;
using System.Net;
using System.Text;
using System.IO;
using System.Threading;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Windows.Forms;
using MistClient;
using BrightIdeasSoftware;
using SteamKit2;
using SteamTrade;
using System.Media;
using ToastNotifications;
using SteamKit2.Internal;

namespace SteamBot
{
    public class Bot
    {
        public string BotControlClass;
        // If the bot is logged in fully or not.  This is only set
        // when it is.
        public bool IsLoggedIn = false;

        // The bot's display name.  Changing this does not mean that
        // the bot's name will change.
        public string DisplayName { get; private set; }

        // The response to all chat messages sent to it.
        public string ChatResponse;

        // A list of SteamIDs that this bot recognizes as admins.
        public ulong[] Admins;
        public SteamFriends SteamFriends;
        public SteamClient SteamClient;
        public SteamTrading SteamTrade;
        public SteamUser SteamUser;

        // The current trade; if the bot is not in a trade, this is
        // null.
        public Trade CurrentTrade;

        public bool IsDebugMode = false;

        // The log for the bot.  This logs with the bot's display name.
        public Log log;
        
        public delegate UserHandler UserHandlerCreator(Bot bot, SteamID id);
        public UserHandlerCreator CreateHandler;
        Dictionary<ulong, UserHandler> userHandlers = new Dictionary<ulong, UserHandler>();

        public List<SteamID> friends = new List<SteamID>();

        // The maximum amount of time the bot will trade for.
        public int MaximumTradeTime { get; private set; }

        // The maximum amount of time the bot will wait in between
        // trade actions.
        public int MaximiumActionGap { get; private set; }

        // The Steam Web API key.
        string apiKey;

        // The prefix put in the front of the bot's display name.
        //string DisplayNamePrefix;

        // Log level to use for this bot
        Log.LogLevel LogLevel;

        // The number, in milliseconds, between polls for the trade.
        int TradePollingInterval;

        string sessionId;
        string token;

        SteamUser.LogOnDetails logOnDetails;

        TradeManager tradeManager;

        bool hasrun = false;
        public bool otherAccepted = false;
        public static string displayName = "[unknown]";
        public Login main;

        public Inventory MyInventory;
        public Inventory OtherInventory;

        Friends showFriends;

        public static string MachineAuthData;

        public Bot(Configuration.BotInfo config, Log log, string apiKey, UserHandlerCreator handlerCreator, Login _login, bool debug = false)
        {
            this.main = _login;
            logOnDetails = new SteamUser.LogOnDetails
            {
                Username = _login.Username,
                Password = _login.Password
            };
            ChatResponse = "";
            TradePollingInterval = 50;
            Admins = new ulong[1];
            Admins[0] = 123456789;
            this.apiKey = apiKey;
            try
            {
                LogLevel = (Log.LogLevel)Enum.Parse(typeof(Log.LogLevel), "Debug", true);
            }
            catch (ArgumentException)
            {
                Console.WriteLine("Invalid LogLevel provided in configuration. Defaulting to 'INFO'");
                LogLevel = Log.LogLevel.Info;
            }
            this.log = log;
            CreateHandler = handlerCreator;
            BotControlClass = "SteamBot.SimpleUserHandler";

            // Hacking around https
            ServicePointManager.ServerCertificateValidationCallback += SteamWeb.ValidateRemoteCertificate;

            log.Debug ("Initializing Steam account...");
            main.Invoke((Action)(() =>
            {
                main.label_status.Text = "Initializing Steam account...";
            }));
            SteamClient = new SteamClient();
            SteamTrade = SteamClient.GetHandler<SteamTrading>();
            SteamUser = SteamClient.GetHandler<SteamUser>();
            SteamFriends = SteamClient.GetHandler<SteamFriends>();
            log.Info ("Connecting...");
            main.Invoke((Action)(() =>
            {
                main.label_status.Text = "Connecting to Steam...";
            }));
            SteamClient.Connect();
            
            Thread CallbackThread = new Thread(() => // Callback Handling
            {
                while (true)
                {
                    CallbackMsg msg = SteamClient.WaitForCallback (true);

                    HandleSteamMessage (msg);
                }
            }); 
            
            CallbackThread.Start();
            CallbackThread.Join();
            log.Success("Done loading account!");
            main.Invoke((Action)(() =>
            {
                main.label_status.Text = "Done loading account!";
            }));
        }

        /// <summary>
        /// Creates a new trade with the given partner.
        /// </summary>
        /// <returns>
        /// <c>true</c>, if trade was opened,
        /// <c>false</c> if there is another trade that must be closed first.
        /// </returns>
        public bool OpenTrade (SteamID other)
        {
            if (CurrentTrade != null)
                return false;

            SteamTrade.Trade(other);

            return true;
        }

        /// <summary>
        /// Closes the current active trade.
        /// </summary>
        public void CloseTrade() 
        {
            if (CurrentTrade == null)
                return;

            UnsubscribeTrade (GetUserHandler (CurrentTrade.OtherSID), CurrentTrade);

            tradeManager.StopTrade ();

            CurrentTrade = null;
        }

        void OnTradeTimeout(object sender, EventArgs args) 
        {
            // ignore event params and just null out the trade.
            GetUserHandler (CurrentTrade.OtherSID).OnTradeTimeout();
        }

        void OnTradeEnded (object sender, EventArgs e)
        {
            CloseTrade();
        }        

        bool HandleTradeSessionStart (SteamID other)
        {
            if (CurrentTrade != null)
                return false;

            try
            {
                tradeManager.InitializeTrade(SteamUser.SteamID, other);
                CurrentTrade = tradeManager.StartTrade (SteamUser.SteamID, other);
            }
            catch (SteamTrade.Exceptions.InventoryFetchException ie)
            {
                // we shouldn't get here because the inv checks are also
                // done in the TradeProposedCallback handler.
                string response = String.Empty;
                
                if (ie.FailingSteamId.ConvertToUInt64() == other.ConvertToUInt64())
                {
                    response = "Trade failed. Could not correctly fetch your backpack. Either the inventory is inaccessable or your backpack is private.";
                }
                else 
                {
                    response = "Trade failed. Could not correctly fetch my backpack.";
                }
                
                SteamFriends.SendChatMessage(other, 
                                             EChatEntryType.ChatMsg,
                                             response);

                log.Info ("Bot sent other: " + response);
                
                CurrentTrade = null;
                return false;
            }
            
            CurrentTrade.OnClose += CloseTrade;
            SubscribeTrade (CurrentTrade, GetUserHandler (other));

            return true;
        }

        void HandleSteamMessage (CallbackMsg msg)
        {
            log.Debug(msg.ToString());

            #region Login
            msg.Handle<SteamClient.ConnectedCallback> (callback =>
            {
                log.Debug ("Connection Callback: " + callback.Result);

                if (callback.Result == EResult.OK)
                {
                    UserLogOn();
                }
                else
                {
                    log.Error ("Failed to connect to Steam Community, trying again...");
                    main.Invoke((Action)(() =>
                    {
                        main.label_status.Text = "Failed to connect to Steam Community, trying again...";
                    }));
                    SteamClient.Connect ();
                }

            });

            msg.Handle<SteamUser.LoggedOnCallback> (callback =>
            {
                log.Debug ("Logged On Callback: " + callback.Result);

                if (callback.Result == EResult.OK)
                {
                    main.Invoke((Action)(() =>
                    {
                        main.label_status.Text = "Logging in to Steam...";
                        log.Info("Logging in to Steam...");
                    }));
                }

                if (callback.Result != EResult.OK)
                {
                    log.Error ("Login Error: " + callback.Result);
                    main.Invoke((Action)(() =>
                    {
                        main.label_status.Text = "Login Error: " + callback.Result;
                    }));
                }
                
                if (callback.Result == EResult.InvalidPassword)
                {
                    MessageBox.Show("Your password is incorrect. Please try again.",
                                    "Invalid Password",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error,
                                    MessageBoxDefaultButton.Button1);
                    main.wrongAPI = true;
                    main.Invoke((Action)(main.Close));
                    return;
                }

                if (callback.Result == EResult.AccountLogonDenied)
                {
                    log.Interface ("This account is protected by Steam Guard.  Enter the authentication code sent to the proper email: ");
                    SteamGuard SteamGuard = new SteamGuard();
                    SteamGuard.ShowDialog();
                    logOnDetails.AuthCode = SteamGuard.AuthCode;
                    main.Invoke((Action)(() =>
                    {
                        main.label_status.Text = "Logging in...";
                    }));
                }

                if (callback.Result == EResult.InvalidLoginAuthCode)
                {
                    log.Interface("An Invalid Authorization Code was provided.  Enter the authentication code sent to the proper email: ");
                    SteamGuard SteamGuard = new SteamGuard("An Invalid Authorization Code was provided.\nEnter the authentication code sent to the proper email: ");
                    SteamGuard.ShowDialog();
                    logOnDetails.AuthCode = SteamGuard.AuthCode;
                    main.Invoke((Action)(() =>
                    {
                        main.label_status.Text = "Logging in...";
                    }));
                }
            });

            msg.Handle<SteamUser.LoginKeyCallback> (callback =>
            {
                log.Debug("Handling LoginKeyCallback...");

                while (true)
                {
                    try
                    {
                        log.Info("About to authenticate...");
                        bool authd = false;
                        try
                        {
                            authd = SteamWeb.Authenticate(callback, SteamClient, out sessionId, out token);
                        }
                        catch (Exception ex)
                        {
                            log.Error("Error on authentication:\n" + ex);
                        }
                        if (authd)
                        {
                            log.Success("User Authenticated!");
                            main.Invoke((Action)(() =>
                            {
                                main.label_status.Text = "User authenticated!";
                            }));
                            tradeManager = new TradeManager(apiKey, sessionId, token);
                            tradeManager.SetTradeTimeLimits(MaximumTradeTime, MaximiumActionGap, TradePollingInterval);
                            tradeManager.OnTimeout += OnTradeTimeout;
                            tradeManager.OnTradeEnded += OnTradeEnded;
                            break;
                        }
                        else
                        {
                            log.Warn("Authentication failed, retrying in 2s...");
                            main.Invoke((Action)(() =>
                            {
                                main.label_status.Text = "Authentication failed, retrying in 2s...";
                            }));
                            Thread.Sleep(2000);
                        }
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.ToString());
                    }
                }

                if (Trade.CurrentSchema == null)
                {
                    log.Info ("Downloading Schema...");
                    main.Invoke((Action)(() =>
                    {
                        main.label_status.Text = "Downloading schema...";
                    }));
                    try
                    {
                        Trade.CurrentSchema = Schema.FetchSchema(apiKey);
                    }
                    catch (Exception ex)
                    {
                        log.Error(ex.ToString());
                        MessageBox.Show("I can't fetch the schema! Your API key may be invalid or there may be a problem connecting to Steam. Please make sure you have obtained a proper API key at http://steamcommunity.com/dev/apikey",
                                    "Schema Error",
                                    MessageBoxButtons.OK,
                                    MessageBoxIcon.Error,
                                    MessageBoxDefaultButton.Button1);
                        main.wrongAPI = true;
                        main.Invoke((Action)(main.Dispose));
                        return;
                    }
                    log.Success ("Schema Downloaded!");
                    main.Invoke((Action)(() =>
                    {
                        main.label_status.Text = "Schema downloaded!";
                    }));
                }

                SteamFriends.SetPersonaName (SteamFriends.GetFriendPersonaName(SteamUser.SteamID));
                SteamFriends.SetPersonaState (EPersonaState.Online);

                log.Success ("Account Logged In Completely!");
                main.Invoke((Action)(() =>
                {
                    main.label_status.Text = "Logged in completely!";
                }));

                IsLoggedIn = true;
                displayName = SteamFriends.GetPersonaName();
                ConnectToGC(13540830642081628378);
                Thread.Sleep(500);
                DisconnectFromGC();
                try
                {
                    main.Invoke((Action)(main.Hide));
                }
                catch (Exception)
                {
                    Environment.Exit(1);
                }
                Thread.Sleep(2500);
                CDNCache.Initialize();
            });

            // handle a special JobCallback differently than the others
            if (msg.IsType<SteamClient.JobCallback<SteamUser.UpdateMachineAuthCallback>>())
            {
                msg.Handle<SteamClient.JobCallback<SteamUser.UpdateMachineAuthCallback>>(
                    jobCallback => OnUpdateMachineAuthCallback(jobCallback.Callback, jobCallback.JobID)
                );
            }
            #endregion

            #region Friends
            msg.Handle<SteamFriends.FriendsListCallback>(callback =>
            {
                bool newFriend = false;
                foreach (SteamFriends.FriendsListCallback.Friend friend in callback.FriendList)
                {
                    if (!friends.Contains(friend.SteamID) && !friend.SteamID.ToString().StartsWith("1"))
                    {
                        new Thread(() =>
                        {
                            main.Invoke((Action)(() =>
                            {
                                if (showFriends == null && friend.Relationship == EFriendRelationship.RequestRecipient)
                                {
                                    log.Info(SteamFriends.GetFriendPersonaName(friend.SteamID) + " has added you.");
                                    friends.Add(friend.SteamID);
                                    newFriend = true;
                                    string name = SteamFriends.GetFriendPersonaName(friend.SteamID);
                                    string status = SteamFriends.GetFriendPersonaState(friend.SteamID).ToString();
                                    if (!ListFriendRequests.Find(friend.SteamID))
                                    {
                                        ListFriendRequests.Add(name, friend.SteamID, status);
                                    }
                                }
                                if (showFriends != null && friend.Relationship == EFriendRelationship.RequestRecipient)
                                {
                                    log.Info(SteamFriends.GetFriendPersonaName(friend.SteamID) + " has added you.");
                                    friends.Add(friend.SteamID);
                                    /*if (friend.Relationship == EFriendRelationship.RequestRecipient &&
                                        GetUserHandler(friend.SteamID).OnFriendAdd())
                                    {
                                        SteamFriends.AddFriend(friend.SteamID);
                                    }*/
                                    newFriend = true;
                                    string name = SteamFriends.GetFriendPersonaName(friend.SteamID);
                                    string status = SteamFriends.GetFriendPersonaState(friend.SteamID).ToString();
                                    if (!ListFriendRequests.Find(friend.SteamID))
                                    {
                                        try
                                        {
                                            showFriends.NotifyFriendRequest();
                                            ListFriendRequests.Add(name, friend.SteamID, status);
                                            log.Info("Notifying you that " + SteamFriends.GetFriendPersonaName(friend.SteamID) + " has added you.");
                                            int duration = 5;
                                            FormAnimator.AnimationMethod animationMethod = FormAnimator.AnimationMethod.Slide;
                                            FormAnimator.AnimationDirection animationDirection = FormAnimator.AnimationDirection.Up;
                                            Notification toastNotification = new Notification(name, "has sent you a friend request.", duration, animationMethod, animationDirection);
                                            toastNotification.Show();
                                            try
                                            {
                                                string soundsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory);
                                                string soundFile = Path.Combine(soundsFolder + "trade_message.wav");
                                                using (System.Media.SoundPlayer player = new System.Media.SoundPlayer(soundFile))
                                                {
                                                    player.Play();
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                Console.WriteLine(e.Message);
                                            }
                                            showFriends.list_friendreq.SetObjects(ListFriendRequests.Get());
                                        }
                                        catch
                                        {
                                            Console.WriteLine("Friends list hasn't loaded yet...");
                                        }
                                    }
                                }
                            }));
                        }).Start();
                    }   
                    else
                    {
                        if (friend.Relationship == EFriendRelationship.None)
                        {
                            friends.Remove(friend.SteamID);
                            GetUserHandler(friend.SteamID).OnFriendRemove();
                        }
                    }
                }
                if (!newFriend && ListFriendRequests.Get().Count == 0)
                {
                    if (showFriends != null)
                    {
                        showFriends.HideFriendRequests();
                    }
                }
            });

            msg.Handle<SteamFriends.PersonaStateCallback>(callback =>
            {
                var status = callback.State;
                var sid = callback.FriendID;
                GetUserHandler(sid).SetStatus(status);
                ListFriends.UpdateStatus(sid, status.ToString());
            });


            msg.Handle<SteamFriends.FriendMsgCallback>(callback =>
            {
                EChatEntryType type = callback.EntryType;

                if (callback.EntryType == EChatEntryType.Typing)
                {
                    var name = SteamFriends.GetFriendPersonaName(callback.Sender);
                    GetUserHandler(callback.Sender).SetChatStatus(name + " is typing...");
                }
                else
                {
                    GetUserHandler(callback.Sender).SetChatStatus("");
                }

                if (callback.EntryType == EChatEntryType.ChatMsg ||
                    callback.EntryType == EChatEntryType.Emote)
                {
                    //log.Info (String.Format ("Chat Message from {0}: {1}",
                    //                     SteamFriends.GetFriendPersonaName (callback.Sender),
                    //                     callback.Message
                    //));
                    GetUserHandler(callback.Sender).OnMessage(callback.Message, type);
                }
            });

            msg.Handle<SteamFriends.ChatMsgCallback>(callback =>
            {
                Console.WriteLine(SteamFriends.GetFriendPersonaName(callback.ChatterID) + ": " + callback.Message);
            });
            #endregion

            #region Trading
            msg.Handle<SteamTrading.SessionStartCallback>(callback =>
            {
                bool started = HandleTradeSessionStart(callback.OtherClient);

                //if (!started)
                //log.Info ("Could not start the trade session.");
                //else
                //log.Debug ("SteamTrading.SessionStartCallback handled successfully. Trade Opened.");
            });

            msg.Handle<SteamTrading.TradeProposedCallback>(callback =>
            {
                try
                {
                    tradeManager.InitializeTrade(SteamUser.SteamID, callback.OtherClient);
                }
                catch
                {
                    SteamFriends.SendChatMessage(callback.OtherClient,
                                                 EChatEntryType.ChatMsg,
                                                 "Trade declined. Could not correctly fetch your backpack.");

                    SteamTrade.RespondToTrade(callback.TradeID, false);
                    return;
                }

                if (tradeManager.OtherInventory.IsPrivate)
                {
                    SteamFriends.SendChatMessage(callback.OtherClient,
                                                 EChatEntryType.ChatMsg,
                                                 "Trade declined. Your backpack cannot be private.");

                    SteamTrade.RespondToTrade(callback.TradeID, false);
                    return;
                }

                //if (CurrentTrade == null && GetUserHandler (callback.OtherClient).OnTradeRequest ())
                if (CurrentTrade == null)
                    GetUserHandler(callback.OtherClient).SendTradeState(callback.TradeID);
                else
                    SteamTrade.RespondToTrade(callback.TradeID, false);
            });

            msg.Handle<SteamTrading.TradeResultCallback>(callback =>
            {
                //log.Debug ("Trade Status: " + callback.Response);

                if (callback.Response == EEconTradeResponse.Accepted)
                {
                    //log.Info ("Trade Accepted!");
                }
                if (callback.Response == EEconTradeResponse.Cancel ||
                    callback.Response == EEconTradeResponse.ConnectionFailed ||
                    callback.Response == EEconTradeResponse.Declined ||
                    callback.Response == EEconTradeResponse.Error ||
                    callback.Response == EEconTradeResponse.InitiatorAlreadyTrading ||
                    callback.Response == EEconTradeResponse.TargetAlreadyTrading ||
                    callback.Response == EEconTradeResponse.Timeout ||
                    callback.Response == EEconTradeResponse.TooSoon ||
                    callback.Response == EEconTradeResponse.TradeBannedInitiator ||
                    callback.Response == EEconTradeResponse.TradeBannedTarget ||
                    callback.Response == EEconTradeResponse.NotLoggedIn) // uh...
                {
                    if (callback.Response == EEconTradeResponse.Cancel)
                        TradeResponse(callback.OtherClient, "had asked to trade with you, but has cancelled their request.");
                    if (callback.Response == EEconTradeResponse.ConnectionFailed)
                        TradeResponse(callback.OtherClient, "Lost connection to Steam. Reconnecting as soon as possible...");
                    if (callback.Response == EEconTradeResponse.Declined)
                        TradeResponse(callback.OtherClient, "has declined your trade request.");
                    if (callback.Response == EEconTradeResponse.Error)
                        TradeResponse(callback.OtherClient, "An error has occurred in sending the trade request.");
                    if (callback.Response == EEconTradeResponse.InitiatorAlreadyTrading)
                        TradeResponse(callback.OtherClient, "You are already in a trade so you cannot trade someone else.");
                    if (callback.Response == EEconTradeResponse.TargetAlreadyTrading)
                        TradeResponse(callback.OtherClient, "You cannot trade the other user because they are already in trade with someone else.");
                    if (callback.Response == EEconTradeResponse.Timeout)
                        TradeResponse(callback.OtherClient, "did not respond to the trade request.");
                    if (callback.Response == EEconTradeResponse.TooSoon)
                        TradeResponse(callback.OtherClient, "It is too soon to send a new trade request. Try again later.");
                    if (callback.Response == EEconTradeResponse.TradeBannedInitiator)
                        TradeResponse(callback.OtherClient, "You are trade-banned and cannot trade.");
                    if (callback.Response == EEconTradeResponse.TradeBannedTarget)
                        TradeResponse(callback.OtherClient, "You cannot trade with this person because they are trade-banned.");
                    if (callback.Response == EEconTradeResponse.NotLoggedIn)
                        TradeResponse(callback.OtherClient, "Trade failed to initialize because you are not logged in.");
                    CloseTrade();
                }

            });
            #endregion

            #region Disconnect
            msg.Handle<SteamUser.LoggedOffCallback> (callback =>
            {
                IsLoggedIn = false;
                log.Warn ("Logged Off: " + callback.Result);
            });

            msg.Handle<SteamClient.DisconnectedCallback> (callback =>
            {
                IsLoggedIn = false;
                CloseTrade ();
                log.Warn ("Disconnected from Steam Network!");
                main.Invoke((Action)(() =>
                {
                    main.label_status.Text = "Disconnected from Steam Network! Retrying...";
                }));
                SteamClient.Connect ();
                main.Invoke((Action)(() =>
                {
                    main.label_status.Text = "Connecting to Steam...";
                }));
            });
            #endregion

            if (!hasrun && IsLoggedIn)
            {
                Thread main = new Thread(GUI);
                main.Start();
                hasrun = true;
            }
        }

        void TradeResponse(SteamID _sid, string message)
        {
            GetUserHandler(_sid).SendTradeError(message);
        }

        void GUI()
        {
            main.Invoke(new MethodInvoker(delegate()
            {
                showFriends = new Friends(this, Bot.displayName);
                showFriends.Show();
                showFriends.Activate();
                LoadFriends();
                showFriends.friends_list.SetObjects(ListFriends.Get(MistClient.Properties.Settings.Default.OnlineOnly));                
            }));            
        }

        public void LoadFriends()
        {
            ListFriends.Clear();
            Console.WriteLine("Loading all friends...");
            for (int count = 0; count < SteamFriends.GetFriendCount(); count++)
            {
                var friendID = SteamFriends.GetFriendByIndex(count);
                var friendName = SteamFriends.GetFriendPersonaName(friendID);
                var friendState = SteamFriends.GetFriendPersonaState(friendID).ToString();
                if (friendState.ToString() != "Offline" && SteamFriends.GetFriendRelationship(friendID) == EFriendRelationship.Friend)
                {
                    string friend_name = friendName + " (" + friendID + ")" + Environment.NewLine + friendState;
                    ListFriends.Add(friendName, friendID, friendState);
                }
                Thread.Sleep(25);
            }
            for (int count = 0; count < SteamFriends.GetFriendCount(); count++)
            {
                var friendID = SteamFriends.GetFriendByIndex(count);
                var friendName = SteamFriends.GetFriendPersonaName(friendID);
                var friendState = SteamFriends.GetFriendPersonaState(friendID).ToString();
                if (friendState.ToString() == "Offline" && SteamFriends.GetFriendRelationship(friendID) == EFriendRelationship.Friend)
                {
                    ListFriends.Add(friendName, friendID, friendState);
                }
                Thread.Sleep(25);
            }
            bool newFriend = true;
            foreach (var item in ListFriends.Get())
            {
                if (ListFriendRequests.Find(item.SID))
                {
                    Console.WriteLine("Found friend {0} in list of friend requests, so let's remove the user.", item.Name);
                    // Not a friend request, so let's remove it
                    ListFriendRequests.Remove(item.SID);
                    newFriend = false;
                }
            }
            foreach (var item in ListFriendRequests.Get())
            {
                if (item.Name == "[unknown]")
                {
                    string name = SteamFriends.GetFriendPersonaName(item.SteamID);
                    ListFriendRequests.Remove(item.SteamID);
                    ListFriendRequests.Add(name, item.SteamID);
                }
                if (item.Name == "")
                {
                    string name = SteamFriends.GetFriendPersonaName(item.SteamID);
                    ListFriendRequests.Remove(item.SteamID);
                    ListFriendRequests.Add(name, item.SteamID);
                }
            }
            if (newFriend && ListFriendRequests.Get().Count != 0)
            {
                Console.WriteLine("Notifying about new friend request.");
                showFriends.NotifyFriendRequest();
                showFriends.list_friendreq.SetObjects(ListFriendRequests.Get());
            }
            Console.WriteLine("Done!");
        }

        public void NewChat(SteamID sid)
        {
            Console.WriteLine("Opening chat.");
            GetUserHandler(sid).OpenChat(sid);
        }

        public static void Print(object sender)
        {
            var old_color = Console.ForegroundColor;
            Console.ForegroundColor = ConsoleColor.Magenta;
            Console.WriteLine(sender);
            Console.ForegroundColor = old_color;
        }

        public void ConnectToGC(ulong appId)
        {
            var playMsg = new ClientMsgProtobuf<CMsgClientGamesPlayed>(
                EMsg.ClientGamesPlayedWithDataBlob);
            var game = new CMsgClientGamesPlayed.GamePlayed
            {
                game_id = new GameID(appId),
                game_extra_info = "Mist - Portable Steam Client",
            };

            playMsg.Body.games_played.Add(game);
            SteamClient.Send(playMsg);
        }

        public void DisconnectFromGC()
        {
            var deregMsg = new ClientMsgProtobuf<CMsgClientDeregisterWithServer>(
                EMsg.ClientDeregisterWithServer);

            deregMsg.Body.eservertype = 42;
            deregMsg.Body.app_id = 0;

            SteamClient.Send(deregMsg);

            ConnectToGC(0);
        }

        void UserLogOn()
        {
            // get sentry file which has the machine hw info saved 
            // from when a steam guard code was entered
            FileInfo fi = new FileInfo(String.Format("{0}.sentryfile", logOnDetails.Username));

            if (fi.Exists && fi.Length > 0)
                logOnDetails.SentryFileHash = SHAHash(File.ReadAllBytes(fi.FullName));
            else
                logOnDetails.SentryFileHash = null;

            SteamUser.LogOn(logOnDetails);
        }

        UserHandler GetUserHandler (SteamID sid)
        {
            if (!userHandlers.ContainsKey (sid))
            {
                userHandlers [sid.ConvertToUInt64 ()] = CreateHandler (this, sid);
            }
            return userHandlers [sid.ConvertToUInt64 ()];
        }

        static byte [] SHAHash (byte[] input)
        {
            SHA1Managed sha = new SHA1Managed();
            
            byte[] output = sha.ComputeHash( input );
            
            sha.Clear();
            
            return output;
        }

        void OnUpdateMachineAuthCallback (SteamUser.UpdateMachineAuthCallback machineAuth, JobID jobId)
        {
            byte[] hash = SHAHash (machineAuth.Data);

            StringBuilder sb = new StringBuilder();
            for (int count = 0; count < hash.Length; count++)
            {
                sb.Append(hash[count]);
            }

            MachineAuthData = sb.ToString();

            File.WriteAllBytes (String.Format ("{0}.sentryfile", logOnDetails.Username), machineAuth.Data);
            
            var authResponse = new SteamUser.MachineAuthDetails
            {
                BytesWritten = machineAuth.BytesToWrite,
                FileName = machineAuth.FileName,
                FileSize = machineAuth.BytesToWrite,
                Offset = machineAuth.Offset,
                
                SentryFileHash = hash, // should be the sha1 hash of the sentry file we just wrote
                
                OneTimePassword = machineAuth.OneTimePassword, // not sure on this one yet, since we've had no examples of steam using OTPs
                
                LastError = 0, // result from win32 GetLastError
                Result = EResult.OK, // if everything went okay, otherwise ~who knows~
                
                JobID = jobId, // so we respond to the correct server job
            };
            
            // send off our response
            SteamUser.SendMachineAuthResponse (authResponse);
        }

        /// <summary>
        /// Gets the bot's inventory and stores it in MyInventory.
        /// </summary>
        /// <example> This sample shows how to find items in the bot's inventory from a user handler.
        /// <code>
        /// Bot.GetInventory(); // Get the inventory first
        /// foreach (var item in Bot.MyInventory.Items)
        /// {
        ///     if (item.Defindex == 5021)
        ///     {
        ///         // Bot has a key in its inventory
        ///     }
        /// }
        /// </code>
        /// </example>
        public void GetInventory()
        {
            MyInventory = Inventory.FetchInventory(SteamUser.SteamID, apiKey);
        }

        /// <summary>
        /// Gets the other user's inventory and stores it in OtherInventory.
        /// </summary>
        /// <param name="OtherSID">The SteamID of the other user</param>
        /// <example> This sample shows how to find items in the other user's inventory from a user handler.
        /// <code>
        /// Bot.GetOtherInventory(OtherSID); // Get the inventory first
        /// foreach (var item in Bot.OtherInventory.Items)
        /// {
        ///     if (item.Defindex == 5021)
        ///     {
        ///         // User has a key in its inventory
        ///     }
        /// }
        /// </code>
        /// </example>
        public void GetOtherInventory(SteamID OtherSID)
        {
            OtherInventory = Inventory.FetchInventory(OtherSID, apiKey);
        }

        /// <summary>
        /// Subscribes all listeners of this to the trade.
        /// </summary>
        public void SubscribeTrade (Trade trade, UserHandler handler)
        {
            trade.OnClose += handler.OnTradeClose;
            trade.OnError += handler.OnTradeError;
            //trade.OnTimeout += OnTradeTimeout;
            trade.OnAfterInit += handler.OnTradeInit;
            trade.OnUserAddItem += handler.OnTradeAddItem;
            trade.OnUserRemoveItem += handler.OnTradeRemoveItem;
            trade.OnMessage += handler.OnTradeMessage;
            trade.OnUserSetReady += handler.OnTradeReady;
            trade.OnUserAccept += handler.OnTradeAccept;
        }
        
        /// <summary>
        /// Unsubscribes all listeners of this from the current trade.
        /// </summary>
        public void UnsubscribeTrade (UserHandler handler, Trade trade)
        {
            trade.OnClose -= handler.OnTradeClose;
            trade.OnError -= handler.OnTradeError;
            //Trade.OnTimeout -= OnTradeTimeout;
            trade.OnAfterInit -= handler.OnTradeInit;
            trade.OnUserAddItem -= handler.OnTradeAddItem;
            trade.OnUserRemoveItem -= handler.OnTradeRemoveItem;
            trade.OnMessage -= handler.OnTradeMessage;
            trade.OnUserSetReady -= handler.OnTradeReady;
            trade.OnUserAccept -= handler.OnTradeAccept;
        }
    }
}
