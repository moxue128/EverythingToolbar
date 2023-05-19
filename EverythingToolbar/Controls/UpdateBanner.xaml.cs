﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using System.Windows;
using EverythingToolbar.Behaviors;
using EverythingToolbar.Helpers;
using EverythingToolbar.Properties;
using NLog;

namespace EverythingToolbar.Controls
{
    public partial class UpdateBanner
    {
        private Version _latestVersion;
        private static readonly ILogger Logger = ToolbarLogger.GetLogger<ThemeAwareness>();
        private static readonly string ApiUrl = "https://api.github.com/repos/srwi/EverythingToolbar/releases";
        private static readonly string LatestReleaseUrl = "https://github.com/srwi/EverythingToolbar/releases/latest";
        
        public UpdateBanner()
        {
            InitializeComponent();
        }

        private static async Task<Version> GetLatestStableReleaseVersion()
        {
            try
            {
                using (var client = new HttpClient())
                {
                    client.DefaultRequestHeaders.UserAgent.ParseAdd("EverythingToolbar");

                    var response = await client.GetAsync(ApiUrl);
                    if (response.IsSuccessStatusCode)
                    {
                        var jsonStream = await response.Content.ReadAsStreamAsync();
                        var serializer = new DataContractJsonSerializer(typeof(List<Release>));
                        var releases = (List<Release>)serializer.ReadObject(jsonStream);
                        var stableReleases = releases.Where(r => !r.Prerelease).ToList();
                        var latestStableRelease = stableReleases.FirstOrDefault();
                        if (latestStableRelease != null)
                        {
                            return new Version(latestStableRelease.TagName);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logger.Error(e, "Failed to get latest release version.");
            }
            
            return null;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (!Settings.Default.isUpdateNotificationsEnabled)
                return;
            
            var assemblyVersion = Assembly.GetExecutingAssembly().GetName().Version;
            _latestVersion = await GetLatestStableReleaseVersion();
            
            if (_latestVersion == null || _latestVersion == TryGetSkippedUpdate())
                return;
            if (assemblyVersion == null || assemblyVersion.CompareTo(_latestVersion) >= 0)
                return;
            
            LatestVersionRun.Text = _latestVersion.ToString();
            Visibility = Visibility.Visible;
        }

        private static Version TryGetSkippedUpdate()
        {
            try
            {
                return new Version(Settings.Default.skippedUpdate);
            }
            catch
            {
                return null;
            }
        }

        private void OnDownloadClicked(object sender, RoutedEventArgs e)
        {
            Process.Start(LatestReleaseUrl);
        }

        private void OnSkipUpdateClicked(object sender, RoutedEventArgs e)
        {
            Settings.Default.skippedUpdate = _latestVersion.ToString();
            Visibility = Visibility.Collapsed;
        }

        [DataContract]
        private class Release
        {
            [DataMember(Name = "tag_name")]
            public string TagName { get; set; }

            [DataMember(Name = "prerelease")]
            public bool Prerelease { get; set; }
        }
    }
}