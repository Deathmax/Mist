﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Net;
using System.IO;
using MetroFramework.Forms;

namespace MistClient
{
    public class Util
    {
        public static void LoadTheme(MetroFramework.Components.MetroStyleManager MetroStyleManager)
        {
            Friends.globalThemeManager.Add(MetroStyleManager);
            MetroStyleManager.Theme = Friends.globalStyleManager.Theme;
            MetroStyleManager.Style = Friends.globalStyleManager.Style;
        }

        public static string HTTPRequest(string url)
        {
            var result = "";
            try
            {
                using (var webClient = new WebClient())
                {
                    using (var stream = webClient.OpenRead(url))
                    {
                        using (var streamReader = new StreamReader(stream))
                        {
                            result = streamReader.ReadToEnd();
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                var wtf = ex.Message;
            }

            return result;
        }

        public static HttpWebResponse Fetch(string url)
        {
            HttpWebRequest request = WebRequest.Create(url) as HttpWebRequest;
            request.Method = "POST";
            HttpWebResponse response;
            for (int count = 0; count < 10; count++)
            {
                try
                {
                    response = request.GetResponse() as HttpWebResponse;
                    return response;
                }
                catch
                {
                    System.Threading.Thread.Sleep(100);
                    Console.WriteLine("retry");
                }
            }
            return null;
        }

        public static string ParseBetween(string Subject, string Start, string End)
        {
            return Regex.Match(Subject, Regex.Replace(Start, @"[][{}()*+?.\\^$|]", @"\$0") + @"\s*(((?!" + Regex.Replace(Start, @"[][{}()*+?.\\^$|]", @"\$0") + @"|" + Regex.Replace(End, @"[][{}()*+?.\\^$|]", @"\$0") + @").)+)\s*" + Regex.Replace(End, @"[][{}()*+?.\\^$|]", @"\$0"), RegexOptions.IgnoreCase).Value.Replace(Start, "").Replace(End, "");
        }

        public static string[] GetStringInBetween(string strBegin,
                                                  string strEnd, string strSource,
                                                  bool includeBegin, bool includeEnd)
        {
            string[] result = { "", "" };
            int iIndexOfBegin = strSource.IndexOf(strBegin);
            if (iIndexOfBegin != -1)
            {
                // include the Begin string if desired
                if (includeBegin)
                    iIndexOfBegin -= strBegin.Length;
                strSource = strSource.Substring(iIndexOfBegin
                    + strBegin.Length);
                int iEnd = strSource.IndexOf(strEnd);
                if (iEnd != -1)
                {
                    // include the End string if desired
                    if (includeEnd)
                        iEnd += strEnd.Length;
                    result[0] = strSource.Substring(0, iEnd);
                    // advance beyond this segment
                    if (iEnd + strEnd.Length < strSource.Length)
                        result[1] = strSource.Substring(iEnd
                            + strEnd.Length);
                }
            }
            else
                // stay where we are
                result[1] = strSource;
            return result;
        }

        public static string GetPrice(int defindex, int quality, SteamTrade.Inventory.Item inventoryItem, bool gifted = false, int attribute = 0)
        {
            try
            {
                double value = BackpackTF.CurrentSchema.Response.Prices[defindex][quality][attribute].Value;
                double keyValue = BackpackTF.CurrentSchema.Response.Prices[5021][6][0].Value;
                double billsValue = BackpackTF.CurrentSchema.Response.Prices[126][6][0].Value;
                double budValue = BackpackTF.CurrentSchema.Response.Prices[143][6][0].Value;

                var item = SteamTrade.Trade.CurrentSchema.GetItem(defindex);
                string result = "";

                if (inventoryItem.IsNotCraftable)
                {
                    value = value / 2.0;
                }
                if (inventoryItem.IsNotTradeable)
                {
                    value = value / 2.0;
                }
                if (gifted)
                {
                    value = value * 0.75;
                }
                if (quality == 3)
                {
                    if (item.CraftMaterialType == "weapon")
                    {
                        int level = inventoryItem.Level;
                        switch (level)
                        {
                            case 0:
                                value = billsValue + 5.11;
                                break;
                            case 1:
                                value = billsValue;
                                break;
                            case 42:
                                value = value * 10.0;
                                break;
                            case 69:
                                value = billsValue;
                                break;
                            case 99:
                                value = billsValue;
                                break;
                            case 100:
                                value = billsValue;
                                break;
                            default:
                                break;
                        }
                    }
                    else if (item.CraftMaterialType == "hat")
                    {
                        int level = inventoryItem.Level;
                        switch (level)
                        {
                            case 0:
                                value = value * 10.0;
                                break;
                            case 1:
                                value = value * 5.0;
                                break;
                            case 42:
                                value = value * 3.0;
                                break;
                            case 69:
                                value = value * 4.0;
                                break;
                            case 99:
                                value = value * 4.0;
                                break;
                            case 100:
                                value = value * 6.0;
                                break;
                            default:
                                break;
                        }
                    }
                }

                if (value >= budValue * 1.33)
                {
                    value = value / budValue;
                    result = value.ToString("0.00") + " buds";
                }
                else if (value >= keyValue && !item.ItemName.EndsWith("Key"))
                {
                    value = value / keyValue;
                    result = value.ToString("0.00") + " keys";
                }
                else
                {
                    result = value.ToString("0.00") + " ref";
                }

                return result;
            }
            catch
            {
                return "Unknown";
            }
        }

        public static string QualityToName(int quality)
        {
            switch (quality)
            {
                case 1:
                    return "Genuine";
                case 2:
                    return "Vintage";
                case 3:
                    return "Unusual";
                case 4:
                    return "Unique";
                case 5:
                    return "Community";
                case 6:
                    return "Valve";
                case 7:
                    return "Self-Made";
                case 8:
                    return "Customized";
                case 9:
                    return "Strange";
                case 10:
                    return "Completed";
                case 11:
                    return "Haunted";
                case 12:
                    return "Tournament";
                case 13:
                    return "Favored";
                default:
                    return "";
            }
        }

        public static string GetQualityColor(string quality)
        {
            return GetQualityColor(int.Parse(quality));
        }

        public static string GetQualityColor(int quality)
        {
            switch (quality)
            {
                case 1:
                    return "#4D7455";
                case 2:
                    return "#476291";
                case 3:
                    return "#8650AC";
                case 4:
                    return "#D2D2D2";
                case 5:
                    return "#70B04A";
                case 6:
                    return "#A50F79";
                case 7:
                    return "#70B04A";
                case 8:
                    return "#00FF00";
                case 9:
                    return "#CF6A32";
                case 10:
                case 11:
                case 12:
                    return "#8650AC";
                case 13:
                    return "#FFFF00";
                default:
                    return "#FFFFFF";
            }
        }
    }
}
