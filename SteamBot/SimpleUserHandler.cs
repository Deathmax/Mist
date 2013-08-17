using SteamKit2;
using System.Collections.Generic;
using SteamTrade;
using System.Threading;
using System;
using MistClient;
using System.Windows.Forms;
using System.IO;
using ToastNotifications;

namespace SteamBot
{
    public class SimpleUserHandler : UserHandler
    {
        ShowTrade ShowTrade;
        
        public SimpleUserHandler(Bot bot, SteamID sid) : base(bot, sid) { }

        public void SendMessage(string message)
        {
            if (message != "")
            {
                Bot.SteamFriends.SendChatMessage(OtherSID, EChatEntryType.ChatMsg, message);
            }
        }

        public override void SetChatStatus(string message)
        {
            if (Friends.chat_opened)
            {
                Bot.main.Invoke((Action)(() =>
                {
                    foreach (TabPage tab in Friends.chat.ChatTabControl.TabPages)
                    {
                        if (tab.Text == Bot.SteamFriends.GetFriendPersonaName(OtherSID))
                        {
                            foreach (var item in tab.Controls)
                            {
                                Friends.chat.chatTab = (ChatTab)item;
                            }
                            tab.Invoke((Action)(() =>
                            {
                                Friends.chat.chatTab.chat_status.Text = message;
                            }));
                            return;
                        }
                    }

                }));
            }
        }

        public override void SetStatus(EPersonaState state)
        {
            if (Friends.chat_opened)
            {
                Bot.main.Invoke((Action)(() =>
                {
                    foreach (TabPage tab in Friends.chat.ChatTabControl.TabPages)
                    {
                        if (tab.Text == Bot.SteamFriends.GetFriendPersonaName(OtherSID))
                        {
                            foreach (var item in tab.Controls)
                            {
                                Friends.chat.chatTab = (ChatTab)item;
                            }
                            tab.Invoke((Action)(() =>
                            {
                                Friends.chat.chatTab.steam_status.Text = state.ToString();
                            }));
                            return;
                        }
                    }
                    
                }));
            }
        }

        public override void SendTradeError(string message)
        {
            string selected = Bot.SteamFriends.GetFriendPersonaName(OtherSID);
            ulong sid = OtherSID;
            Bot.main.Invoke((Action)(() =>
            {
                if (!Friends.chat_opened)
                {
                    Friends.chat = new Chat(Bot);
                    Friends.chat.AddChat(selected, sid);
                    Friends.chat.Show();
                    Friends.chat_opened = true;
                    Friends.chat.Flash();
                    DisplayChatNotify(message);
                }
                else
                {
                    bool found = false;
                    try
                    {
                        foreach (TabPage tab in Friends.chat.ChatTabControl.TabPages)
                        {
                            if (tab.Text == selected)
                            {
                                foreach (var item in tab.Controls)
                                {
                                    Friends.chat.chatTab = (ChatTab)item;
                                }
                                Friends.chat.ChatTabControl.SelectedTab = tab;
                                Friends.chat.Show();
                                Friends.chat.Flash();
                                found = true;
                                tab.Invoke((Action)(() =>
                                {
                                    DisplayChatNotify(message);
                                }));
                                break;
                            }
                        }
                        if (!found)
                        {
                            Friends.chat.AddChat(selected, sid);
                            Friends.chat.Show();
                            Friends.chat.Flash();
                            DisplayChatNotify(message);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }));
            
        }

        void DisplayChatNotify(string message)
        {
            Bot.main.Invoke((Action)(() =>
            {
                foreach (TabPage tab in Friends.chat.ChatTabControl.TabPages)
                {
                    if (tab.Text == Bot.SteamFriends.GetFriendPersonaName(OtherSID))
                    {
                        foreach (var item in tab.Controls)
                        {
                            Friends.chat.chatTab = (ChatTab)item;
                        }
                        tab.Invoke((Action)(() =>
                        {
                            if (message == "had asked to trade with you, but has cancelled their request.")
                            {                                
                                Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " " + message + "\r\n", true);
                            }
                            if (message == "Lost connection to Steam. Reconnecting as soon as possible...")
                                Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + message + "\r\n", false);
                            if (message == "has declined your trade request.")
                                Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " " + message + "\r\n", true);
                            if (message == "An error has occurred in sending the trade request.")
                                Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + message + "\r\n", false);
                            if (message == "You are already in a trade so you cannot trade someone else.")
                                Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + message + "\r\n", false);
                            if (message == "You cannot trade the other user because they are already in trade with someone else.")
                                Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + message + "\r\n", false);
                            if (message == "did not respond to the trade request.")
                            {
                                if (Friends.chat.chatTab.otherSentTrade)
                                {
                                    Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " had asked to trade with you, but you did not respond in time." + "\r\n", true);
                                    Friends.chat.chatTab.otherSentTrade = false;
                                }
                                else
                                {
                                    Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " " + message + "\r\n", true);
                                }
                            }
                            if (message == "It is too soon to send a new trade request. Try again later.")
                                Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + message + "\r\n", false);
                            if (message == "You are trade-banned and cannot trade.")
                                Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + message + "\r\n", false);
                            if (message == "You cannot trade with this person because they are trade-banned.")
                                Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + message + "\r\n", false);
                            if (message == "Trade failed to initialize because either you or the user are not logged in.")
                                Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + message + "\r\n", false);
                            if (Friends.chat_opened)
                                Friends.chat.chatTab.TradeButtonMode(1);
                        }));
                        return;
                    }
                }

            }));
            
        }

        public override void OpenChat(SteamID SID)
        {
            string selected = Bot.SteamFriends.GetFriendPersonaName(SID);
            ulong sid = SID;
            if (!Friends.chat_opened)
            {
                Friends.chat = new Chat(Bot);
                Friends.chat.AddChat(selected, sid);
                Friends.chat.Show();
                Friends.chat.Flash();
                Friends.chat_opened = true;
            }
            else
            {
                bool found = false;
                try
                {
                    foreach (TabPage tab in Friends.chat.ChatTabControl.TabPages)
                    {
                        if (tab.Text == selected)
                        {                            
                            Friends.chat.Show();
                            Friends.chat.Flash();
                            found = true;
                        }
                    }
                    if (!found)
                    {
                        Friends.chat.AddChat(selected, sid);
                        Friends.chat.Show();
                        Friends.chat.Flash();
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            }
        }

        public override bool OnFriendAdd()
        {
            return false;
        }

        public override void OnFriendRemove() { }

        public override void OnMessage(string message, EChatEntryType type)
        {
            if (Bot.main.InvokeRequired)
            {
                Bot.main.Invoke((Action)(() =>
                {
                    var other = Bot.SteamFriends.GetFriendPersonaName(OtherSID);
                    OpenChat(OtherSID);
                    string date = "[" + DateTime.Now + "] ";
                    string name = other + ": ";
                    foreach (TabPage tab in Friends.chat.ChatTabControl.TabPages)
                    {
                        if (tab.Text == other)
                        {
                            foreach (var item in tab.Controls)
                            {
                                Friends.chat.chatTab = (ChatTab)item;
                            }
                        }
                    }
                    int islink;
                    islink = 0;
                    if (message.Contains("http://") || (message.Contains("https://")) || (message.Contains("www.")) || (message.Contains("ftp."))){
                        string[] stan = message.Split(' ');
                        foreach (string word in stan)
                        {
                            if (word.Contains("http://") || (word.Contains("https://")) || (word.Contains("www.")) || (word.Contains("ftp.")))
                            {
                                if (word.Contains("."))
                                {
                                    islink = 1;
                                }
                            }
                        }
                    }
                    if (islink == 1)
                    {
                        Friends.chat.chatTab.UpdateChat("[INFO] ", "WARNING: ", "Do not click on links that you feel that maybe unsafe. Make sure the link is what it should be by looking at it.");
                    }
                    Friends.chat.chatTab.UpdateChat(date, name, message);
                    new Thread(() =>
                    {
                        if (!Chat.hasFocus)
                        {
                            int duration = 3;
                            FormAnimator.AnimationMethod animationMethod = FormAnimator.AnimationMethod.Slide;
                            FormAnimator.AnimationDirection animationDirection = FormAnimator.AnimationDirection.Up;
                            string title = Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " says:";
                            Notification toastNotification = new Notification(title, message, duration, animationMethod, animationDirection, Friends.chat.chatTab.avatarBox);
                            Bot.main.Invoke((Action)(() =>
                            {
                                toastNotification.Show();
                            }));
                        }
                    }).Start();
                }));
            }
            else
            {
                var other = Bot.SteamFriends.GetFriendPersonaName(OtherSID);
                OpenChat(OtherSID);
                string date = "[" + DateTime.Now + "] ";
                string name = other + ": ";
                foreach (TabPage tab in Friends.chat.ChatTabControl.TabPages)
                {
                    if (tab.Text == other)
                    {
                        foreach (var item in tab.Controls)
                        {
                            Friends.chat.chatTab = (ChatTab)item;
                        }
                    }
                }
                Friends.chat.chatTab.UpdateChat(date, name, message);
            }
        }

        public override void SendTradeState(uint tradeID)
        {
            string name = Bot.SteamFriends.GetFriendPersonaName(OtherSID);
            Bot.main.Invoke((Action)(() =>
            {
                if (!Friends.chat_opened)
                {
                    Friends.chat = new Chat(Bot);
                    Friends.chat.AddChat(name, OtherSID);
                    Friends.chat.Show();
                    Friends.chat_opened = true;
                    Friends.chat.Flash();
                    Friends.chat.chatTab.TradeButtonMode(3, tradeID);
                    Friends.chat.chatTab.otherSentTrade = true;
                }
                else
                {
                    bool found = false;
                    try
                    {
                        foreach (TabPage tab in Friends.chat.ChatTabControl.TabPages)
                        {
                            Console.WriteLine("Looking at " + tab.Text);
                            if (tab.Text == name)
                            {
                                foreach (var item in tab.Controls)
                                {
                                    Friends.chat.chatTab = (ChatTab)item;
                                }
                                Friends.chat.ChatTabControl.SelectedTab = tab;
                                Friends.chat.Show();
                                Friends.chat.Flash();
                                Friends.chat.chatTab.TradeButtonMode(3, tradeID);
                                Friends.chat.chatTab.otherSentTrade = true;
                                found = true;
                            }
                        }
                        if (!found)
                        {
                            Console.WriteLine("Not found");
                            Friends.chat.AddChat(name, OtherSID);
                            Friends.chat.Show();
                            Friends.chat.Flash();
                            Friends.chat.chatTab.TradeButtonMode(3, tradeID);
                            Friends.chat.chatTab.otherSentTrade = true;
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
            }));
        }

        public override bool OnTradeRequest()
        {
            return false;
        }

        public override void OnTradeError(string error)
        {
            string name = Bot.SteamFriends.GetFriendPersonaName(OtherSID);
            Bot.main.Invoke((Action)(() =>
            {
                try
                {
                    base.OnTradeClose();
                    Bot.main.Invoke((Action)(() =>
                    {
                        ShowTrade.Close();
                    }));
                }
                catch (Exception ex)
                {
                    Bot.Print(ex);
                }
                if (!Friends.chat_opened)
                {
                    Friends.chat = new Chat(Bot);
                    Friends.chat.AddChat(name, OtherSID);
                    Friends.chat.Show();
                    Friends.chat_opened = true;
                    Friends.chat.Flash();
                    Friends.chat.chatTab.TradeButtonMode(1);
                    if (error.Contains("cancelled"))
                    {
                        Friends.chat.Invoke((Action)(() =>
                        {
                            Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] The trade has been cancelled.\r\n", true);
                        }));
                    }
                    else
                    {
                        Friends.chat.Invoke((Action)(() =>
                        {
                            Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] Error: " + error + "\r\n", true);
                        }));
                    }
                }
                else
                {
                    bool found = false;
                    try
                    {
                        Console.WriteLine("Trying");
                        foreach (TabPage tab in Friends.chat.ChatTabControl.TabPages)
                        {
                            Console.WriteLine("Looking at " + tab.Text);
                            if (tab.Text == name)
                            {
                                foreach (var item in tab.Controls)
                                {
                                    Friends.chat.chatTab = (ChatTab)item;
                                }
                                Friends.chat.ChatTabControl.SelectedTab = tab;
                                Friends.chat.Show();
                                Friends.chat.Flash();
                                Friends.chat.chatTab.TradeButtonMode(1);
                                if (error.Contains("cancelled"))
                                {
                                    Friends.chat.Invoke((Action)(() =>
                                    {
                                        Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] The trade has been cancelled.\r\n", true);
                                    }));
                                }
                                else
                                {
                                    Friends.chat.Invoke((Action)(() =>
                                    {
                                        Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] Error: " + error, true);
                                    }));
                                }
                                found = true;
                            }
                        }
                        if (!found)
                        {
                            Console.WriteLine("Not found");
                            Friends.chat.AddChat(name, OtherSID);
                            Friends.chat.Show();
                            Friends.chat.Flash();
                            Friends.chat.chatTab.TradeButtonMode(1);
                            if (error.Contains("cancelled"))
                            {
                                Friends.chat.Invoke((Action)(() =>
                                {
                                    Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] The trade has been cancelled.\r\n", true);
                                }));
                            }
                            else
                            {
                                Friends.chat.Invoke((Action)(() =>
                                {
                                    Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] Error: " + error + "\r\n", true);
                                }));
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                    }
                }
                
            }));
        }

        public override void OnTradeTimeout()
        {
            Bot.main.Invoke((Action)(() =>
            {
                try
                {
                    base.OnTradeClose();
                    ShowTrade.Close();
                }
                catch (Exception ex)
                {
                    Bot.Print(ex);
                }
                foreach (TabPage tab in Friends.chat.ChatTabControl.TabPages)
                {
                    if (tab.Text == Bot.SteamFriends.GetFriendPersonaName(OtherSID))
                    {
                        foreach (var item in tab.Controls)
                        {
                            Friends.chat.chatTab = (ChatTab)item;
                        }
                    }
                }
                Friends.chat.Invoke((Action)(() =>
                {
                    if (Friends.chat_opened)
                    {
                        Friends.chat.chatTab.TradeButtonMode(1);
                        Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] The trade has expired.\r\n", false);
                    }
                }));
            }));
        }

        public override void OnTradeInit()
        {
            ShowTrade.itemsAdded = 0;
            ChatTab.AppendLog(OtherSID, "==========[TRADE STARTED]==========\r\n");            
            Bot.log.Success("Trade successfully initialized.");
            foreach (TabPage tab in Friends.chat.ChatTabControl.TabPages)
            {
                if (tab.Text == Bot.SteamFriends.GetFriendPersonaName(OtherSID))
                {
                    foreach (var item in tab.Controls)
                    {
                        Friends.chat.chatTab = (ChatTab)item;
                    }
                }
            }
            Friends.chat.Invoke((Action)(() =>
            {
                Friends.chat.chatTab.UpdateButton("Currently in Trade");
                Friends.chat.chatTab.UpdateChat("[" + DateTime.Now + "] " + Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " has accepted your trade request.\r\n", false);
            }));
            ShowTrade.loading = true;
            Bot.main.Invoke((Action)(() =>
            {
                ShowTrade.ClearAll();
            }));
            TradeCountInventory();
        }

        public override void OnTradeClose()
        {
            try
            {
                Friends.chat.Invoke((Action)(() =>
                {
                    ShowTrade.Close();
                }));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
            foreach (TabPage tab in Friends.chat.ChatTabControl.TabPages)
            {
                if (tab.Text == Bot.SteamFriends.GetFriendPersonaName(OtherSID))
                {
                    foreach (var item in tab.Controls)
                    {
                        Friends.chat.chatTab = (ChatTab)item;
                    }
                }
            }
            Bot.main.Invoke((Action)(() =>
            {
                if (Friends.chat_opened)
                {
                    Friends.chat.chatTab.TradeButtonMode(1);
                }
            }));
            base.OnTradeClose();
        }

        public void TradeCountInventory()
        {
            Bot.main.Invoke((Action)(() =>
            {
                ShowTrade = new ShowTrade(Bot, Bot.SteamFriends.GetFriendPersonaName(OtherSID));
                ShowTrade.Show();
                ShowTrade.Activate();
            }));
            BackpackTF.CurrentSchema = BackpackTF.FetchSchema();
            // Let's count our inventory
            Thread loadInventory = new Thread(() =>
            {   
                Console.WriteLine("Trade window opened.");
                Console.WriteLine("Loading all inventory items.");
                if (Bot.CurrentTrade == null)
                    return;
                Inventory.Item[] inventory = Trade.MyInventory.Items;
                foreach (Inventory.Item item in inventory)
                {
                    if (!item.IsNotTradeable)
                    {
                        var currentItem = Trade.CurrentSchema.GetItem(item.Defindex);
                        string name = "";
                        string itemValue = "";
                        var type = Convert.ToInt32(item.Quality.ToString());
                        if (QualityToName(type) != "Unique")
                            name += QualityToName(type) + " ";                        
                        name += currentItem.ItemName;
                        if (QualityToName(type) == "Unusual")
                        {
                            try
                            {
                                for (int count = 0; count < item.Attributes.Length; count++)
                                {
                                    if (item.Attributes[count].Defindex == 134)
                                    {
                                        name += " (Effect: " + EffectToName(item.Attributes[count].FloatValue) + ")";
                                    }
                                }
                            }
                            catch (Exception)
                            {

                            }
                        }
                        if (currentItem.CraftMaterialType == "supply_crate")
                        {
                            for (int count = 0; count < item.Attributes.Length; count++)
                            {
                                name += " #" + (item.Attributes[count].FloatValue);
                                if (item.Attributes[count].Defindex == 186)
                                {
                                    name += " (Gifted)";
                                }
                            }
                        }
                        try
                        {
                            int size = item.Attributes.Length;
                            for (int count = 0; count < size; count++)
                            {
                                if (item.Attributes[count].Defindex == 261)
                                {
                                    string paint = ShowBackpack.PaintToName(item.Attributes[count].FloatValue);
                                    name += " (Painted: " + paint + ")";
                                }
                                if (item.Attributes[count].Defindex == 186)
                                {
                                    name += " (Gifted)";
                                }
                            }
                        }
                        catch
                        {
                            // Item has no attributes... or something.
                        }
                        if (item.IsNotCraftable)
                            name += " (Uncraftable)";
                        if (currentItem.Name == "Wrapped Gift")
                        {
                            // Untested!
                            try
                            {                           
                                var containedItem = Trade.CurrentSchema.GetItem(item.ContainedItem.Defindex);
                                var containedName = GetItemName(containedItem, item.ContainedItem, out itemValue);
                                name += " (Contains: " + containedName + ")";
                            }
                            catch (Exception ex)
                            {
                                Bot.Print(ex);
                                // Guess this doesn't work :P.
                            }
                        }
                        string price = Util.GetPrice(item.Defindex, currentItem.ItemQuality, item);
                        ListInventory.Add(name, item.Id, currentItem.ImageURL, price);
                    }
                }
                try
                {
                    ShowTrade.loading = false;
                    Bot.main.Invoke((Action)(() => ShowTrade.list_inventory.SetObjects(ListInventory.Get())));
                }
                catch
                {

                }
            });
            loadInventory.Start();
        }

        string QualityToName(int quality)
        {
            if (quality == 1)
                return "Genuine";
            if (quality == 3)
                return "Vintage";
            if (quality == 5)
                return "Unusual";
            if (quality == 6)
                return "Unique";
            if (quality == 7)
                return "Community";
            if (quality == 9)
                return "Self-Made";
            if (quality == 11)
                return "Strange";
            if (quality == 13)
                return "Haunted";
            return "";
        }

        string EffectToName(float effect)
        {
            if (effect == 6)
                return "Green Confetti";
            if (effect == 7)
                return "Purple Confetti";
            if (effect == 8)
                return "Haunted Ghosts";
            if (effect == 9)
                return "Green Energy";
            if (effect == 10)
                return "Purple Energy";
            if (effect == 11)
                return "Circling TF Logo";
            if (effect == 12)
                return "Massed Flies";
            if (effect == 13)
                return "Burning Flames";
            if (effect == 14)
                return "Scorching Flames";
            if (effect == 15)
                return "Searing Plasma";
            if (effect == 16)
                return "Vivid Plasma";
            if (effect == 17)
                return "Sunbeams";
            if (effect == 18)
                return "Circling Peace Sign";
            if (effect == 19)
                return "Circling Heart";
            if (effect == 29)
                return "Stormy Storm";
            if (effect == 30)
                return "Blizzardy Storm";
            if (effect == 31)
                return "Nuts n' Bolts";
            if (effect == 32)
                return "Orbiting Planets";
            if (effect == 33)
                return "Orbiting Fire";
            if (effect == 34)
                return "Bubbling";
            if (effect == 35)
                return "Smoking";
            if (effect == 36)
                return "Steaming";
            if (effect == 37)
                return "Flaming Lantern";
            if (effect == 38)
                return "Cloudy Moon";
            if (effect == 39)
                return "Cauldron Bubbles";
            if (effect == 40)
                return "Eerie Orbiting Fire";
            if (effect == 43)
                return "Knifestorm";
            if (effect == 44)
                return "Misty Skull";
            if (effect == 45)
                return "Harvest Moon";
            if (effect == 46)
                return "It's a Secret to Everybody";
            if (effect == 47)
                return "Stormy 13th Hour";
            return "";
        }

        public override void OnTradeAddItem(Schema.Item schemaItem, Inventory.Item inventoryItem)
        {
            Bot.main.Invoke((Action)(() =>
            {
                string itemValue = "";
                string completeName = GetItemName(schemaItem, inventoryItem, out itemValue, false);
                ulong itemID = inventoryItem.Id;
                //string itemValue = Util.GetPrice(schemaItem.Defindex, schemaItem.ItemQuality, inventoryItem);
                double value = 0;
                if (itemValue.Contains("ref"))
                {
                    string newValue = ShowTrade.ReplaceLastOccurrence(itemValue, "ref", "");
                    value = Convert.ToDouble(newValue);
                }
                else if (itemValue.Contains("key"))
                {
                    string newValue = ShowTrade.ReplaceLastOccurrence(itemValue, "keys", "");
                    value = Convert.ToDouble(newValue);
                    value = value * BackpackTF.KeyPrice;
                }
                else if (itemValue.Contains("bud"))
                {
                    string newValue = ShowTrade.ReplaceLastOccurrence(itemValue, "buds", "");
                    value = Convert.ToDouble(newValue);
                    value = value * BackpackTF.BudPrice;
                }
                ShowTrade.OtherTotalValue += value;
                if (ShowTrade.OtherTotalValue >= BackpackTF.BudPrice * 1.33)
                {
                    double formatPrice = ShowTrade.OtherTotalValue / BackpackTF.BudPrice;
                    string label = "Total Value: " + formatPrice.ToString("0.00") + " buds";
                    ShowTrade.UpdateLabel(label);
                }
                else if (ShowTrade.OtherTotalValue >= BackpackTF.KeyPrice)
                {
                    double formatPrice = ShowTrade.OtherTotalValue / BackpackTF.KeyPrice;
                    string label = "Total Value: " + formatPrice.ToString("0.00") + " keys";
                    ShowTrade.UpdateLabel(label);
                }
                else
                {
                    double formatPrice = ShowTrade.OtherTotalValue;
                    string label = "Total Value: " + formatPrice.ToString("0.00") + " ref";
                    ShowTrade.UpdateLabel(label);
                }
                ListOtherOfferings.Add(completeName, itemID, itemValue);
                ShowTrade.list_otherofferings.SetObjects(ListOtherOfferings.Get());
                ShowTrade.itemsAdded++;
                if (ShowTrade.itemsAdded > 0)
                {
                    ShowTrade.check_userready.Enabled = true;
                }
                string itemName = GetItemName(schemaItem, inventoryItem, out itemValue, false);
                ShowTrade.AppendText(Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " added: ", itemName);
                ChatTab.AppendLog(OtherSID, "[Trade Chat] " + Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " added: " + itemName + "\r\n");
                ShowTrade.ResetTradeStatus();
            }));
        }

        public override void OnTradeRemoveItem(Schema.Item schemaItem, Inventory.Item inventoryItem)
        {
            Bot.main.Invoke((Action)(() =>
            {
                string itemValue = "";
                string completeName = GetItemName(schemaItem, inventoryItem, out itemValue, false);
                ulong itemID = inventoryItem.Id;
                double value = 0;
                if (itemValue.Contains("ref"))
                {
                    string newValue = ShowTrade.ReplaceLastOccurrence(itemValue, "ref", "");
                    value = Convert.ToDouble(newValue);
                }
                else if (itemValue.Contains("key"))
                {
                    string newValue = ShowTrade.ReplaceLastOccurrence(itemValue, "keys", "");
                    value = Convert.ToDouble(newValue);
                    value = value * BackpackTF.KeyPrice;
                }
                else if (itemValue.Contains("bud"))
                {
                    string newValue = ShowTrade.ReplaceLastOccurrence(itemValue, "buds", "");
                    value = Convert.ToDouble(newValue);
                    value = value * BackpackTF.BudPrice;
                }
                ShowTrade.OtherTotalValue -= value;
                if (ShowTrade.OtherTotalValue >= BackpackTF.BudPrice * 1.33)
                {
                    double formatPrice = ShowTrade.OtherTotalValue / BackpackTF.BudPrice;
                    string label = "Total Value: " + formatPrice.ToString("0.00") + " buds";
                    ShowTrade.UpdateLabel(label);
                }
                else if (ShowTrade.OtherTotalValue >= BackpackTF.KeyPrice)
                {
                    double formatPrice = ShowTrade.OtherTotalValue / BackpackTF.KeyPrice;
                    string label = "Total Value: " + formatPrice.ToString("0.00") + " keys";
                    ShowTrade.UpdateLabel(label);
                }
                else
                {
                    double formatPrice = ShowTrade.OtherTotalValue;
                    string label = "Total Value: " + formatPrice.ToString("0.00") + " ref";
                    ShowTrade.UpdateLabel(label);
                }
                ListOtherOfferings.Remove(completeName, itemID);
                ShowTrade.list_otherofferings.SetObjects(ListOtherOfferings.Get());
                ShowTrade.itemsAdded--;
                if (ShowTrade.itemsAdded <= 0)
                {
                    ShowTrade.check_userready.Enabled = false;                    
                }
                string itemName = GetItemName(schemaItem, inventoryItem, out itemValue, false);
                ShowTrade.AppendText(Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " removed: ", itemName);
                ChatTab.AppendLog(OtherSID, "[Trade Chat] " + Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " removed: " + itemName + "\r\n");
                ShowTrade.ResetTradeStatus();
            }));
        }

        string GetItemName(Schema.Item schemaItem, Inventory.Item inventoryItem, out string price, bool id = false)
        {
            price = "Unknown";
            bool isGifted = false;
            bool isUnusual = false;
            var currentItem = Trade.CurrentSchema.GetItem(schemaItem.Defindex);
            string name = "";
            var type = Convert.ToInt32(inventoryItem.Quality.ToString());
            if (QualityToName(type) != "Unique")
                name += QualityToName(type) + " ";            
            name += currentItem.ItemName;
            if (QualityToName(type) == "Unusual")
            {
                isUnusual = true;
                try
                {
                    for (int count = 0; count < inventoryItem.Attributes.Length; count++)
                    {
                        if (inventoryItem.Attributes[count].Defindex == 134)
                        {
                            name += " (Effect: " + EffectToName(inventoryItem.Attributes[count].FloatValue) + ")";
                            price = Util.GetPrice(schemaItem.Defindex, type, inventoryItem, false, (int)inventoryItem.Attributes[count].FloatValue);
                        }
                    }
                }
                catch (Exception)
                {
                    
                }
            }
            if (currentItem.CraftMaterialType == "supply_crate")
            {
                for (int count = 0; count < inventoryItem.Attributes.Length; count++)
                {
                    name += " #" + (inventoryItem.Attributes[count].FloatValue);
                }
            }
            try
            {
                int size = inventoryItem.Attributes.Length;
                for (int count = 0; count < size; count++)
                {
                    if (inventoryItem.Attributes[count].Defindex == 261)
                    {
                        string paint = ShowBackpack.PaintToName(inventoryItem.Attributes[count].FloatValue);
                        name += " (Painted: " + paint + ")";
                    }
                    if (inventoryItem.Attributes[count].Defindex == 186)
                    {
                        isGifted = true;
                        name += " (Gifted)";
                    }
                }
            }
            catch
            {
                // Item has no attributes... or something.
            }
            if (inventoryItem.IsNotCraftable)
                name += " (Uncraftable)";
            if (currentItem.Name == "Wrapped Gift")
            {
                isGifted = true;
                // Untested!
                try
                {
                    int size = inventoryItem.Attributes.Length;
                    for (int count = 0; count < size; count++)
                    {
                        var containedItem = Trade.CurrentSchema.GetItem(inventoryItem.ContainedItem.Defindex);
                        var containedName = GetItemName(containedItem, inventoryItem.ContainedItem, out price, false);
                        price = Util.GetPrice(inventoryItem.ContainedItem.Defindex, Convert.ToInt32(inventoryItem.ContainedItem.Quality.ToString()), inventoryItem, true);
                        name += " (Contains: " + containedName + ")";
                    }
                }
                catch
                {
                    // Item has no attributes... or something.
                }
            }
            if (id)
                name += " :" + inventoryItem.Id;
            if (!isGifted && !isUnusual)
            {
                price = Util.GetPrice(currentItem.Defindex, type, inventoryItem);
                ListBackpack.Add(name, inventoryItem.Defindex, currentItem.ImageURL, price);
            }
            else
            {
                ListBackpack.Add(name, inventoryItem.Defindex, currentItem.ImageURL, price);
            }
            return name;
        }

        public override void OnTradeMessage(string message)
        {
            Bot.main.Invoke((Action)(() =>
            {
                string send = Bot.SteamFriends.GetFriendPersonaName(OtherSID) + ": " + message + " [" + DateTime.Now.ToLongTimeString() + "]\r\n";
                ShowTrade.UpdateChat(send);
                ChatTab.AppendLog(OtherSID, "[Trade Chat] " + send);
                if (!ShowTrade.focused)
                {
                    int duration = 3;
                    FormAnimator.AnimationMethod animationMethod = FormAnimator.AnimationMethod.Slide;
                    FormAnimator.AnimationDirection animationDirection = FormAnimator.AnimationDirection.Up;
                    string title = "[Trade Chat] " + Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " says:";
                    Notification toastNotification = new Notification(title, message, duration, animationMethod, animationDirection, Friends.chat.chatTab.avatarBox);
                    toastNotification.Show();
                }
            }));
        }

        public override void OnTradeReady(bool ready)
        {
            Bot.main.Invoke((Action)(() =>
            {
                ShowTrade.check_otherready.Checked = ready;
                if (ready)
                {
                    ShowTrade.AppendText(Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " is ready.");
                    ChatTab.AppendLog(OtherSID, "[Trade Chat] " + Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " is ready. [" + DateTime.Now.ToLongTimeString() + "]\r\n");
                }
                else
                {
                    ShowTrade.AppendText(Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " is not ready.");
                    ChatTab.AppendLog(OtherSID, "[Trade Chat] " + Bot.SteamFriends.GetFriendPersonaName(OtherSID) + " is not ready. [" + DateTime.Now.ToLongTimeString() + "]\r\n");
                    ShowTrade.ResetTradeStatus();
                }
                if (ready && ShowTrade.check_userready.Checked)
                    ShowTrade.button_accept.Enabled = true;
                else
                    ShowTrade.button_accept.Enabled = false;
            }));
        }

        public override void OnTradeAccept()
        {
            Bot.otherAccepted = true;
            while (!ShowTrade.accepted)
            {
                // wait
            }
            OnTradeClose();
        }
    }
}
