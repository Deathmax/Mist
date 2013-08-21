﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Net;
using System.IO;
using SteamBot;
using MetroFramework.Forms;

namespace MistClient
{
    public partial class Updater : MetroForm
    {
        string newVer;
        bool updating = false;
        Log log;

        public Updater(string newVer, string changelog, Log log)
        {
            InitializeComponent();
            label_newver.Text = "Mist v" + newVer + " is available (you have v" + Friends.mist_ver + ").\nWould you like to download it now?";
            this.newVer = newVer;
            this.log = log;
            this.text_changelog.Text = changelog;
            Util.LoadTheme(metroStyleManager1);
        }

        private void button_install_Click(object sender, EventArgs e)
        {
            updating = true;
            Updater_Progress download = new Updater_Progress(this, log);
            download.ShowDialog();
        }

        private void Updater_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (updating)
            {
                var filename = System.Reflection.Assembly.GetExecutingAssembly().Location;
                System.Diagnostics.Process.Start(filename);
                Environment.Exit(0);
            }
        }

        public void CloseUpdater()
        {
            this.Close();
        }

        private void text_changelog_Enter(object sender, EventArgs e)
        {
            label3_Click(sender, e);
        }

        private void label3_Click(object sender, EventArgs e)
        {
            label3.Focus();
        }

        private void button_skip_Click(object sender, EventArgs e)
        {
            Properties.Settings.Default.SkipUpdate = true;
            Properties.Settings.Default.SkippedVersion = newVer;
            Properties.Settings.Default.Save();
            this.Close();
        }

        private void button_remind_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void linkLabel1_MouseHover(object sender, EventArgs e)
        {
            ToolTip linkTip = new ToolTip();
            linkTip.ToolTipIcon = ToolTipIcon.Info;
            linkTip.IsBalloon = true;
            linkTip.ShowAlways = true;
            linkTip.ToolTipTitle = "Manual Download";
            string link = "http://steamcommunity.com/groups/MistClient/discussions/0/810919057023360607/";
            linkTip.SetToolTip(metroLink1, link);
        }

        private void linkLabel1_LinkClicked(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("explorer.exe", "http://steamcommunity.com/groups/MistClient/discussions/0/810919057023360607/");
        }
    }
}
