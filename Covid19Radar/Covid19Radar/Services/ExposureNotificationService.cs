﻿/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using Acr.UserDialogs;
using Covid19Radar.Common;
using Covid19Radar.Model;
using Covid19Radar.Services.Logs;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Xamarin.ExposureNotifications;
using Xamarin.Forms;

namespace Covid19Radar.Services
{
    public interface IExposureNotificationService
    {
        Task MigrateFromUserData(UserDataModel userData);

        Configuration GetConfiguration();
        void RemoveConfiguration();

        long GetLastProcessTekTimestamp(string region);
        string GetETag(string region);
        string GetLastProcessTekTimestampBg(string region);
        long GetLastProcessTekListCount(string region);
        long GetLastDownloadCount(string region);
        DateTime GetLastDownloadDateTime(string region);
        void SetLastProcessTekTimestamp(string region, long created);
        void SetETag(string region, string created);
        void SetLastProcessTekTimestampBg(string region, long created);
        void SetLastProcessTekListCount(string region, long created);
        void SetLastDownloadCount(string region, long created);
        void SetLastDownloadDateTime(string region, DateTime created);
        void RemoveLastProcessTekTimestamp();

        Task FetchExposureKeyAsync();

        List<UserExposureInfo> GetExposureInformationList();
        void SetExposureInformation(UserExposureSummary summary, List<UserExposureInfo> informationList);
        void RemoveExposureInformation();

        List<UserExposureInfo> GetExposureInformationListToDisplay();
        int GetExposureCountToDisplay();

        Task<string> UpdateStatusMessageAsync();
        Task<bool> StartExposureNotification();
        Task<bool> StopExposureNotification();

        string PositiveDiagnosis { get; set; }
        DateTime? DiagnosisDate { get; set; }
        IEnumerable<TemporaryExposureKey> FliterTemporaryExposureKeys(IEnumerable<TemporaryExposureKey> temporaryExposureKeys);
    }

    public class ExposureNotificationService : IExposureNotificationService
    {
        private readonly ILoggerService loggerService;
        private readonly IHttpClientService httpClientService;
        private readonly ISecureStorageService secureStorageService;
        private readonly IPreferencesService preferencesService;
        private readonly IApplicationPropertyService applicationPropertyService;

        public string CurrentStatusMessage { get; set; } = "初期状態";
        public Status ExposureNotificationStatus { get; set; }

        public ExposureNotificationService(ILoggerService loggerService, IHttpClientService httpClientService, ISecureStorageService secureStorageService, IPreferencesService preferencesService, IApplicationPropertyService applicationPropertyService)
        {
            this.loggerService = loggerService;
            this.httpClientService = httpClientService;
            this.secureStorageService = secureStorageService;
            this.preferencesService = preferencesService;
            this.applicationPropertyService = applicationPropertyService;

            _ = GetExposureNotificationConfig();
        }

        public async Task MigrateFromUserData(UserDataModel userData)
        {
            loggerService.StartMethod();

            const string ConfigurationPropertyKey = "ExposureNotificationConfigration";

            if (userData.LastProcessTekTimestamp != null && userData.LastProcessTekTimestamp.Count > 0)
            {
                var stringValue = Utils.SerializeToJson(userData.LastProcessTekTimestamp);
                preferencesService.SetValue(PreferenceKey.LastProcessTekTimestamp, stringValue);
                userData.LastProcessTekTimestamp.Clear();
                loggerService.Info("Migrated LastProcessTekTimestamp");
            }

            if (applicationPropertyService.ContainsKey(ConfigurationPropertyKey))
            {
                var configuration = applicationPropertyService.GetProperties(ConfigurationPropertyKey) as string;
                if (!string.IsNullOrEmpty(configuration))
                {
                    preferencesService.SetValue(PreferenceKey.ExposureNotificationConfiguration, configuration);
                }
                await applicationPropertyService.Remove(ConfigurationPropertyKey);
                loggerService.Info("Migrated ExposureNotificationConfiguration");
            }

            if (userData.ExposureInformation != null)
            {
                secureStorageService.SetValue(PreferenceKey.ExposureInformation, JsonConvert.SerializeObject(userData.ExposureInformation));
                userData.ExposureInformation = null;
                loggerService.Info("Migrated ExposureInformation");
            }

            if (userData.ExposureSummary != null)
            {
                secureStorageService.SetValue(PreferenceKey.ExposureSummary, JsonConvert.SerializeObject(userData.ExposureSummary));
                userData.ExposureSummary = null;
                loggerService.Info("Migrated ExposureSummary");
            }

            loggerService.EndMethod();
        }

        private async Task GetExposureNotificationConfig()
        {
            loggerService.StartMethod();
            try
            {
                string container = AppSettings.Instance.BlobStorageContainerName;
                string url = AppSettings.Instance.CdnUrlBase + $"{container}/Configration.json";
                HttpClient httpClient = httpClientService.Create();
                Task<HttpResponseMessage> response = httpClient.GetAsync(url);
                HttpResponseMessage result = await response;
                if (result.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    loggerService.Info("Success to download configuration");
                    var content = await result.Content.ReadAsStringAsync();
                    preferencesService.SetValue(PreferenceKey.ExposureNotificationConfiguration, content);
                }
                else
                {
                    loggerService.Error("Fail to download configuration");
                }
            }
            catch (Exception ex)
            {
                loggerService.Exception("Failed download of exposure notification configuration.", ex);
            }
            finally
            {
                loggerService.EndMethod();
            }
        }

        public Configuration GetConfiguration()
        {
            loggerService.StartMethod();
            Configuration result = null;
            var configurationJson = preferencesService.GetValue<string>(PreferenceKey.ExposureNotificationConfiguration, null);
            if (!string.IsNullOrEmpty(configurationJson))
            {
                loggerService.Info($"configuration: {configurationJson}");
                result = JsonConvert.DeserializeObject<Configuration>(configurationJson);
            }
            loggerService.EndMethod();
            return result;
        }

        public void RemoveConfiguration()
        {
            loggerService.StartMethod();
            preferencesService.RemoveValue(PreferenceKey.ExposureNotificationConfiguration);
            loggerService.EndMethod();
        }

        public async Task FetchExposureKeyAsync()
        {
            loggerService.StartMethod();
            await ExposureNotification.UpdateKeysFromServer();
            loggerService.EndMethod();
        }

        public T GetKey<T>(string region, string key, T def)
        {
            loggerService.StartMethod();
            var result = def;
            var jsonString = preferencesService.GetValue<string>(key, null);
            if (!string.IsNullOrEmpty(jsonString))
            {
                var dict = JsonConvert.DeserializeObject<Dictionary<string, T>>(jsonString);
                if (dict.ContainsKey(region))
                {
                    result = dict[region];
                }
            }
            loggerService.EndMethod();
            return result;
        }
        public DateTime GetLastDownloadDateTime(string region)
        {
            return GetKey<DateTime>(region, PreferenceKey.LastDownloadDateTime, new DateTime());
        }

        public long GetLastProcessTekTimestamp(string region)
        {
            return GetKey<long>(region, PreferenceKey.LastProcessTekTimestamp, 0L);
        }
        public string GetETag(string region)
        {
            return GetKey<string>(region, PreferenceKey.ETag, "");
        }

        public string GetLastProcessTekTimestampBg(string region)
        {
            return GetKey<string>(region, PreferenceKey.LastProcessTekTimestampBg, "");
        }

        public long GetLastProcessTekListCount(string region)
        {
            return GetKey<long>(region, PreferenceKey.LastProcessTekListCount, 0L);
        }
        public long GetLastDownloadCount(string region)
        {
            return GetKey<long>(region, PreferenceKey.LastDownloadCount, 0L);
        }

        public void SetKey<T>(string region, string key, T created)
        {
            loggerService.StartMethod();
            var jsonString = preferencesService.GetValue<string>(key, null);
            Dictionary<string, T> newDict;
            if (!string.IsNullOrEmpty(jsonString))
            {
                newDict = JsonConvert.DeserializeObject<Dictionary<string, T>>(jsonString);
            }
            else
            {
                newDict = new Dictionary<string, T>();
            }
            newDict[region] = created;
            preferencesService.SetValue(key, JsonConvert.SerializeObject(newDict));
            loggerService.EndMethod();
        }
        public void SetLastProcessTekTimestamp(string region, long created)
        {
            SetKey<long>(region, PreferenceKey.LastProcessTekTimestamp, created);
        }
        public void SetETag(string region, string created)
        {
            SetKey<string>(region, PreferenceKey.ETag, created);
        }

        public void SetLastProcessTekTimestampBg(string region, long created)
        {
            string s = GetLastProcessTekTimestampBg(region).TrimEnd(',');
            var list = s.Split(",").ToList();
            int max = 15;
            if (list.Count > max)// list[0] ~ list[count-1]
            {
                list.RemoveRange(max, list.Count - 1 - max);
            }
            if (list.Count > 0)
            {
                s = created.ToString() + "," + string.Join(",", list);

            }
            else
            {
                s = created.ToString();
            }
            SetKey<string>(region, PreferenceKey.LastProcessTekTimestampBg, s);
        }
        public void SetLastDownloadDateTime(string region, DateTime created)
        {
            SetKey<DateTime>(region, PreferenceKey.LastDownloadDateTime, created);
        }
        public void SetLastProcessTekListCount(string region, long created)
        {
            SetKey<long>(region, PreferenceKey.LastProcessTekListCount, created);
        }
        public void SetLastDownloadCount(string region, long created)
        {
            SetKey<long>(region, PreferenceKey.LastDownloadCount, created);
        }

        public void RemoveLastProcessTekTimestamp()
        {
            loggerService.StartMethod();
            preferencesService.RemoveValue(PreferenceKey.LastProcessTekTimestamp);
            preferencesService.RemoveValue(PreferenceKey.ETag);
            preferencesService.RemoveValue(PreferenceKey.LastProcessTekTimestampBg);
            preferencesService.RemoveValue(PreferenceKey.LastProcessTekListCount);
            preferencesService.RemoveValue(PreferenceKey.LastDownloadCount);
            preferencesService.RemoveValue(PreferenceKey.LastDownloadDateTime);
            loggerService.EndMethod();
        }


        public List<UserExposureInfo> GetExposureInformationList()
        {
            loggerService.StartMethod();
            List<UserExposureInfo> result = null;
            var exposureInformationJson = secureStorageService.GetValue<string>(PreferenceKey.ExposureInformation);
            if (!string.IsNullOrEmpty(exposureInformationJson))
            {
                result = JsonConvert.DeserializeObject<List<UserExposureInfo>>(exposureInformationJson);
            }
            loggerService.EndMethod();
            return result;
        }

        public void SetExposureInformation(UserExposureSummary summary, List<UserExposureInfo> informationList)
        {
            loggerService.StartMethod();
            var summaryJson = JsonConvert.SerializeObject(summary);
            var informationListJson = JsonConvert.SerializeObject(informationList);
            secureStorageService.SetValue(PreferenceKey.ExposureSummary, summaryJson);
            secureStorageService.SetValue(PreferenceKey.ExposureInformation, informationListJson);
            loggerService.EndMethod();
        }

        public void RemoveExposureInformation()
        {
            loggerService.StartMethod();
            secureStorageService.RemoveValue(PreferenceKey.ExposureSummary);
            secureStorageService.RemoveValue(PreferenceKey.ExposureInformation);
            loggerService.EndMethod();
        }

        public List<UserExposureInfo> GetExposureInformationListToDisplay()
        {
            loggerService.StartMethod();
            var list = GetExposureInformationList()?
                .Where(x => x.Timestamp.CompareTo(DateTimeUtility.Instance.UtcNow.AddDays(AppConstants.DaysOfExposureInformationToDisplay)) >= 0)
                .ToList();
            loggerService.EndMethod();
            return list;
        }

        public int GetExposureCountToDisplay()
        {
            loggerService.StartMethod();
            int result = 0;
            var exposureInformationList = GetExposureInformationListToDisplay();
            if (exposureInformationList != null)
            {
                result = exposureInformationList.Count;
            }
            loggerService.EndMethod();
            return result;
        }

        public async Task<string> UpdateStatusMessageAsync()
        {
            loggerService.StartMethod();
            ExposureNotificationStatus = await ExposureNotification.GetStatusAsync();
            loggerService.EndMethod();
            return await GetStatusMessageAsync();
        }

        public async Task<bool> StartExposureNotification()
        {
            loggerService.StartMethod();
            try
            {
                var enabled = await ExposureNotification.IsEnabledAsync();
                if (!enabled)
                {
                    await ExposureNotification.StartAsync();
                }

                loggerService.EndMethod();
                return true;
            }
            catch (Exception ex)
            {
                loggerService.Exception("Error enabling notifications.", ex);
                loggerService.EndMethod();
                return false;
            }
            finally
            {

            }
        }

        public async Task<bool> StopExposureNotification()
        {
            loggerService.StartMethod();
            try
            {
                var enabled = await ExposureNotification.IsEnabledAsync();
                if (enabled)
                {
                    await ExposureNotification.StopAsync();
                }

                loggerService.EndMethod();
                return true;
            }
            catch (Exception ex)
            {
                loggerService.Exception("Error disabling notifications.", ex);
                loggerService.EndMethod();
                return false;
            }
        }

        private async Task<string> GetStatusMessageAsync()
        {
            var message = "";

            switch (ExposureNotificationStatus)
            {
                case Status.Unknown:
                    await UserDialogs.Instance.AlertAsync(Resources.AppResources.ExposureNotificationStatusMessageUnknown, "", Resources.AppResources.ButtonOk);
                    message = Resources.AppResources.ExposureNotificationStatusMessageUnknown;
                    break;
                case Status.Disabled:
                    await UserDialogs.Instance.AlertAsync(Resources.AppResources.ExposureNotificationStatusMessageDisabled, "", Resources.AppResources.ButtonOk);
                    message = Resources.AppResources.ExposureNotificationStatusMessageDisabled;
                    break;
                case Status.Active:
                    message = Resources.AppResources.ExposureNotificationStatusMessageActive;
                    break;
                case Status.BluetoothOff:
                    // call out settings in each os
                    await UserDialogs.Instance.AlertAsync(Resources.AppResources.ExposureNotificationStatusMessageBluetoothOff, "", Resources.AppResources.ButtonOk);
                    message = Resources.AppResources.ExposureNotificationStatusMessageBluetoothOff;
                    break;
                case Status.Restricted:
                    // call out settings in each os
                    await UserDialogs.Instance.AlertAsync(Resources.AppResources.ExposureNotificationStatusMessageRestricted, "", Resources.AppResources.ButtonOk);
                    message = Resources.AppResources.ExposureNotificationStatusMessageRestricted;
                    break;
                default:
                    break;
            }

            CurrentStatusMessage = message;
            return message;
        }

        /* Processing number issued when positive */
        public string PositiveDiagnosis { get; set; }

        /* Date of diagnosis or onset (Local time) */
        public DateTime? DiagnosisDate { get; set; }

        public IEnumerable<TemporaryExposureKey> FliterTemporaryExposureKeys(IEnumerable<TemporaryExposureKey> temporaryExposureKeys)
        {
            loggerService.StartMethod();

            IEnumerable<TemporaryExposureKey> newTemporaryExposureKeys = null;

            try
            {
                if (DiagnosisDate is DateTime diagnosisDate)
                {
                    var fromDateTime = diagnosisDate.AddDays(AppConstants.DaysToSendTek);
                    var fromDateTimeOffset = new DateTimeOffset(fromDateTime);
                    loggerService.Info($"Filter: After {fromDateTimeOffset}");
                    newTemporaryExposureKeys = temporaryExposureKeys.Where(x => x.RollingStart >= fromDateTimeOffset);
                    loggerService.Info($"Count: {newTemporaryExposureKeys.Count()}");
                }
                else
                {
                    throw new InvalidOperationException("No diagnosis date has been set");
                }
            }
            catch (Exception ex)
            {
                loggerService.Exception("Temporary exposure keys filtering failed", ex);
                throw ex;
            }
            finally
            {
                DiagnosisDate = null;
                loggerService.EndMethod();
            }

            return newTemporaryExposureKeys;
        }
    }
}
