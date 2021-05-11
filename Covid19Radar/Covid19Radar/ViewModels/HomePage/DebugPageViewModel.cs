/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.Diagnostics;
using Acr.UserDialogs;
using Covid19Radar.Common;
using Covid19Radar.Model;
using Covid19Radar.Resources;
using Covid19Radar.Services;
using Covid19Radar.Services.Logs;
using Covid19Radar.Views;
using Prism.Navigation;
using Xamarin.Forms;

namespace Covid19Radar.ViewModels
{
    public class DebugPageViewModel : ViewModelBase
    {
        private readonly ILoggerService loggerService;
        private readonly IUserDataService userDataService;
        private readonly ITermsUpdateService termsUpdateService;
        private readonly IExposureNotificationService exposureNotificationService;
        private readonly IHttpDataService httpDataService;

        private string _count;
        private string _downloadcount;
        private string _downloadDate;
        private string _lastProcessTekTimestamp;
	
        public string StartDate
        {
            get { return userDataService.GetStartDate().ToLocalTime().ToString("D"); }
            set {  }
        }
        public string StartDateTime
        {
            get { return userDataService.GetStartDate().ToLocalTime().ToString("F"); }
            set {  }
        }
        public string PastDate
        {
            get { return userDataService.GetDaysOfUse().ToString(); }
            set {  }
        }
        public string NowDate
        {
            get { return DateTime.Now.ToLocalTime().ToString("F");
	    }
            set {  }
        }
        public string Count
        {
            get { return _count; }
            set { SetProperty(ref _count, value); }
        }
        public string DownloadCount
        {
            get { return _downloadcount; }
            set { SetProperty(ref _downloadcount, value); }
        }
        public string DownloadDateTime
        {
            get { return _downloadDate; }
            set { SetProperty(ref _downloadDate, value); }
        }
        public string LastProcessTekTimestamp
        {
            get { return _lastProcessTekTimestamp; }
            set { SetProperty(ref _lastProcessTekTimestamp, value); }
        }
        public string StringAppSettings
	{
	    // [[snap:///~/mnt/owner/source/repos/cocoa/Covid19Radar/Covid19Radar/settings.json]]
	    get {
		string os = null;
		switch (Device.RuntimePlatform)
		{
		    case Device.Android:
			os = "Android";
			break;
		    case Device.iOS:
			os = "iOS";
			break;
		}
		long ticks =  exposureNotificationService.GetLastProcessTekTimestamp(AppSettings.Instance.SupportedRegions[0]);
		DateTimeOffset dt = DateTimeOffset.FromUnixTimeMilliseconds(ticks).ToOffset(new TimeSpan(9, 0, 0));
		//please check : offset is correct or not
		//cf: ../../../Covid19Radar.Android/Services/Logs/LogPeriodicDeleteServiceAndroid.cs
		string LastProcessTekTimestamp = dt.ToLocalTime().ToString("F");

		var str = new string[]
		{"build: "+os
#if DEBUG
		 +",DEBUG"
#endif
#if USE_MOCK
		 +",USE_MOCK"
#endif
		 ,"ver: "+AppSettings.Instance.AppVersion
		 ,"region: "+AppSettings.Instance.SupportedRegions[0]
		 ,"cdnurl: "+AppSettings.Instance.CdnUrlBase
		 ,"GetStart: "+userDataService.GetStartDate().ToLocalTime().ToString("F")
		 ,"Now: "+DateTime.Now.ToLocalTime().ToString("F")
		 ,"LastProcessTek: "+LastProcessTekTimestamp
		 ,"ExposureCount: "+exposureNotificationService.GetExposureCount().ToString()
		};
		return string.Join(Environment.NewLine,str);
	    }
	    set{}
	}
        public DebugPageViewModel(IHttpDataService httpDataService,INavigationService navigationService, ILoggerService loggerService, IUserDataService userDataService, ITermsUpdateService termsUpdateService,IExposureNotificationService exposureNotificationService) : base(navigationService)
        {
            Title = "Title:DebugPage";
            this.loggerService = loggerService;
            this.userDataService = userDataService;
            this.exposureNotificationService = exposureNotificationService;
        }
        public override async void Initialize(INavigationParameters parameters)
        {
            loggerService.StartMethod();
            try
            {
                await exposureNotificationService.StartExposureNotification();
                await exposureNotificationService.FetchExposureKeyAsync();
                var statusMessage = await exposureNotificationService.UpdateStatusMessageAsync();
                loggerService.Info($"Exposure notification status: {statusMessage}");
                base.Initialize(parameters);
                loggerService.EndMethod();
            }
            catch (Exception ex)
            {
                Debug.WriteLine(ex.ToString());
                loggerService.Exception("Failed to exposure notification status.", ex);
		// in DEBUG build, we get 39507 error from exposure notification api
                loggerService.EndMethod();
            }
	    
	    long ticks =  exposureNotificationService.GetLastProcessTekTimestamp(AppSettings.Instance.SupportedRegions[0]);
	    DateTimeOffset dt = DateTimeOffset.FromUnixTimeMilliseconds(ticks).ToOffset(new TimeSpan(9, 0, 0));
	    //long から時刻を生成する処理は正確ではない可能性があります。要確認。
	    //参考 ~/git/cocoa/Covid19Radar/Covid19Radar.Android/Services/Logs/LogPeriodicDeleteServiceAndroid.cs
	    LastProcessTekTimestamp = dt.ToLocalTime().ToString("F");

	    Count = exposureNotificationService.GetLastProcessTekListCount(AppSettings.Instance.SupportedRegions[0]).ToString();
	    DownloadCount = exposureNotificationService.GetLastDownloadCount(AppSettings.Instance.SupportedRegions[0]).ToString();
	    DownloadDateTime = exposureNotificationService.GetLastDownloadDateTime(AppSettings.Instance.SupportedRegions[0]).ToLocalTime().ToString("F");
        }
	public Command OnClickExposures => new Command(async () =>
        {
            loggerService.StartMethod();

            var count = exposureNotificationService.GetExposureCount();
            loggerService.Info($"Exposure count: {count}");
            if (count > 0)
            {
                await NavigationService.NavigateAsync(nameof(ContactedNotifyPage));
                loggerService.EndMethod();
                return;
            }
            else
            {
                await NavigationService.NavigateAsync(nameof(NotContactPage));
                loggerService.EndMethod();
                return;
            }
        });

        public Command OnClickShareApp => new Command(() =>
       {
           loggerService.StartMethod();

           AppUtils.PopUpShare();

           loggerService.EndMethod();
       });
	public Command OnClickRemove => new Command(async () =>
       {
           loggerService.StartMethod();

	   exposureNotificationService.RemoveLastProcessTekTimestamp();
	   await NavigationService.NavigateAsync(nameof(DebugPage));

           loggerService.EndMethod();
       });
    }
}
