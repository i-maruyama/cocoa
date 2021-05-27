/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Linq;
using Acr.UserDialogs;
using Covid19Radar.Services;
using Covid19Radar.Services.Logs;
using Prism.Navigation;
using Xamarin.Forms;
using Xamarin.Essentials;

namespace Covid19Radar.ViewModels
{
    public class DebugPageViewModel : ViewModelBase
    {
        private readonly IUserDataService userDataService;
        private readonly ITermsUpdateService termsUpdateService;
        private readonly IExposureNotificationService exposureNotificationService;
        private readonly IEssentialsService essentialsService;
        private readonly ILogViewService logViewService;


        private string _debugInfo;

        public string TimeString4s(string time)
        {
            if (string.IsNullOrEmpty(time))
            {
                return "";
            }
            else
            {
                return TimeString(Convert.ToInt64(time));
            }
        }
        public string TimeString(long ticks)
        {
            var dtShifted = DateTimeOffset.FromUnixTimeMilliseconds(ticks).ToOffset(new TimeSpan(9, 0, 0));
            return dtShifted.ToLocalTime().ToString("F");
        }
        public string DebugInfo
        {
            get { return _debugInfo; }
            set { SetProperty(ref _debugInfo, value); }
        }
        public string[] BatteryInfo()
        {
            var s = new[]
            {
                "BatteryLevel: " + (Battery.ChargeLevel*100.0).ToString(),
                "BatteryState: " + Battery.State.ToString(),
                "BatterySource: " + Battery.PowerSource.ToString(),
                "BatteryEnergySaver: " + Battery.EnergySaverStatus.ToString(),
            };
            return (s);
        }
        public string[] EssentialInfo()
        {
            var s = new[]
            {
                "platform: " + essentialsService.Platform,
                "platform_version: " + essentialsService.PlatformVersion,
                "model: " + essentialsService.Model,
                "device_type: " + essentialsService.DeviceType ,
                "app_version: " + essentialsService.AppVersion,
                "build_number:" +  essentialsService.BuildNumber,
            };
            return (s);
        }
        public string[] LogInfo()
        {
            var s = logViewService.LogViewLs();
            var full = logViewService.LogViewTimeInitialize();
            var tek = logViewService.LogViewTimeLastProcessTek();
            return s.Concat(full).Concat(new string[] { "---tek---" }).Concat(tek).ToArray();
        }
        public async void Info(string ex = "")
        {
            string os;
            switch (Device.RuntimePlatform)
            {
                case Device.Android:
                    os = "Android";
                    break;
                case Device.iOS:
                    os = "iOS";
                    break;
                default:
                    os = "unknown";
                    break;
            }
#if DEBUG
            os += ",DEBUG";
#endif
#if USE_MOCK
            os += ",USE_MOCK";
#endif

            // debug info for ./SplashPageViewModel.cs
            string agree;
            if (termsUpdateService.IsAllAgreed())
            {
                agree = "exists";// (mainly) navigate from SplashPage to HomePage
                var termsUpdateInfo = await termsUpdateService.GetTermsUpdateInfo();
                if (termsUpdateService.IsReAgree(TermsType.TermsOfService, termsUpdateInfo))
                {
                    agree += "-TermsOfService";
                }
                else if (termsUpdateService.IsReAgree(TermsType.PrivacyPolicy, termsUpdateInfo))
                {
                    agree += "-PrivacyPolicy";
                }
            }
            else
            {
                agree = "not exists"; // navigate from SplashPage to TutorialPage1
            }

            var region = AppSettings.Instance.SupportedRegions[0];
            var ticks = exposureNotificationService.GetLastProcessTekTimestamp(region);
            var dt = DateTimeOffset.FromUnixTimeMilliseconds(ticks).ToOffset(new TimeSpan(9, 0, 0));
            //please check : offset is correct or not
            //cf: ../../../Covid19Radar.Android/Services/Logs/LogPeriodicDeleteServiceAndroid.cs
            var lastProcessTekTimestamp = dt.ToLocalTime().ToString("F");

            //var ticksBg = exposureNotificationService.GetLastProcessTekTimestampBg(region);
            //var dtBg = DateTimeOffset.FromUnixTimeMilliseconds(ticksBg).ToOffset(new TimeSpan(9, 0, 0));
            //var lastProcessTekTimestampBg = dtBg.ToLocalTime().ToString("F");
            var strng = exposureNotificationService.GetLastProcessTekTimestampBg(region).TrimEnd(',');
            var etag = exposureNotificationService.GetETag(region);
            var stlist = strng.Split(",").ToList().Select(x => TimeString4s(x));
            var lastProcessTekTimestampBg = string.Join(", ", stlist);
            var exposureNotificationStatus = await Xamarin.ExposureNotifications.ExposureNotification.IsEnabledAsync();
            var exposureNotificationMessage = await exposureNotificationService.UpdateStatusMessageAsync();
            // ../../settings.json
            //unnn var xamarinDebug = Xamarin.ExposureNotifications.DebugXamarin.debugString;
            var str = new[] { "Build: " + os, "Ver: " + AppSettings.Instance.AppVersion,
                "Region: " + string.Join(",", AppSettings.Instance.SupportedRegions), "CdnUrl: " + AppSettings.Instance.CdnUrlBase,
                "ApiUrl: " + AppSettings.Instance.ApiUrlBase, "Agree: " + agree, "StartDate: " + userDataService.GetStartDate().ToLocalTime().ToString("F"),
                "DaysOfUse: " + userDataService.GetDaysOfUse(), "ExposureCount: " + exposureNotificationService.GetExposureCount(),
                "LastProcessTek: " + lastProcessTekTimestamp, " (long): " + ticks, "ETag: " + etag,"ENstatus: " + exposureNotificationStatus,
                "ENmessage: " + exposureNotificationMessage, "Now: " + DateTime.Now.ToLocalTime().ToString("F"), ex,
                "---new---",
                "fg: " + Xamarin.Essentials.Preferences.Get("fore_ground",false).ToString(),
                "bg: " + Xamarin.Essentials.Preferences.Get("back_ground",false).ToString() + Xamarin.Essentials.Preferences.Get("back_ground_counter",0).ToString(),
                "bgInterval: " +  Xamarin.Essentials.Preferences.Get("bgInterval", "").ToString(),
                "LastProcessTekBg: " + lastProcessTekTimestampBg,
                "LastDownload: " + exposureNotificationService.GetLastDownloadDateTime(region).ToLocalTime().ToString("F"),
                "TekListCount: " + exposureNotificationService.GetLastProcessTekListCount(region).ToString(),
                "DownloadCount: " + exposureNotificationService.GetLastDownloadCount(region).ToString(),
                "-------"
            }.Concat(EssentialInfo()).Concat(BatteryInfo()).Concat(LogInfo()).ToArray();
            DebugInfo = string.Join(Environment.NewLine, str);
        }
        public DebugPageViewModel(INavigationService navigationService, IUserDataService userDataService, ITermsUpdateService termsUpdateService, IExposureNotificationService exposureNotificationService, IEssentialsService essentialsService
             , ILogViewService logViewService
            ) : base(navigationService)
        {
            Title = "Title:DebugPage";
            this.userDataService = userDataService;
            this.termsUpdateService = termsUpdateService;
            this.exposureNotificationService = exposureNotificationService;
            this.essentialsService = essentialsService;
            this.logViewService = logViewService;
        }
        public override void Initialize(INavigationParameters parameters)
        {
            base.Initialize(parameters);
            Info("Initialize");
        }
        public Command OnClickReload => new Command(async () =>
        {
            Info("Reload");
        });

        public Command OnClickStartExposureNotification => new Command(async () =>
        {
            UserDialogs.Instance.ShowLoading("Starting ExposureNotification...");
            var result = await exposureNotificationService.StartExposureNotification();
            var str = $"StartExposureNotification: {result}";
            UserDialogs.Instance.HideLoading();
            await UserDialogs.Instance.AlertAsync(str, str, Resources.AppResources.ButtonOk);
            Info("StartExposureNotification");
        });
        public Command OnClickFetchExposureKeyAsync => new Command(async () =>
        {
            var exLog = "FetchExposureKeyAsync";
            try { await exposureNotificationService.FetchExposureKeyAsync(); }
            catch (Exception ex) { exLog += $":Exception: {ex}"; }
            Info(exLog);
        });

        // see ../Settings/SettingsPageViewModel.cs
        public Command OnClickStopExposureNotification => new Command(async () =>
        {
            UserDialogs.Instance.ShowLoading("Stopping ExposureNotification...");
            var result = await exposureNotificationService.StopExposureNotification();
            string str = "StopExposureNotification: " + result.ToString();
            UserDialogs.Instance.HideLoading();
            await UserDialogs.Instance.AlertAsync(str, str, Resources.AppResources.ButtonOk);
            Info("StopExposureNotification");
        });

        public Command OnClickRemoveStartDate => new Command(async () =>
        {
            userDataService.RemoveStartDate();
            Info("RemoveStartDate");
        });
        public Command OnClickRemoveExposureInformation => new Command(async () =>
        {
            exposureNotificationService.RemoveExposureInformation();
            Info("RemoveExposureInformation");
        });
        public Command OnClickRemoveConfiguration => new Command(async () =>
        {
            exposureNotificationService.RemoveConfiguration();
            Info("RemoveConfiguration");
        });
        public Command OnClickRemoveLastProcessTekTimestamp => new Command(async () =>
        {
            exposureNotificationService.RemoveLastProcessTekTimestamp();
            Info("RemoveLastProcessTekTimestamp");
        });
        public Command OnClickRemoveAllUpdateDate => new Command(async () =>
        {
            termsUpdateService.RemoveAllUpdateDate();
            Info("RemoveAllUpdateDate");
        });
        public Command OnClickQuit => new Command(async () =>
        {
            Application.Current.Quit();
            DependencyService.Get<ICloseApplication>().closeApplication();
        });
    }
}
