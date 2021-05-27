/* This Source Code Form is subject to the terms of the Mozilla Public
 * License, v. 2.0. If a copy of the MPL was not distributed with this
 * file, You can obtain one at https://mozilla.org/MPL/2.0/. */

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Runtime.CompilerServices;
using System.Text;
using Covid19Radar.Common;
using Xamarin.Essentials;

namespace Covid19Radar.Services.Logs
{
    public class LogViewService : ILogViewService
    {

        //  private readonly IHttpDataService httpDataService;
        //  private readonly ILoggerService loggerService;
        private readonly ILogPathService logPathService;
        // private readonly IStorageService storageService;
        public LogViewService(
     //   IHttpDataService httpDataService,
     //    ILoggerService loggerService,
     ILogPathService logPathService
            //   IStorageService storageService
            )
        {
            //          this.httpDataService = httpDataService;
            //        this.loggerService = loggerService;
            this.logPathService = logPathService;
            //      this.storageService = storageService;
        }

        public int LogViewFileCount()
        {
            return LogViewFiles().Length;
        }
        public string FileInfoString(string fileName)
        {
            var fileInfo = new FileInfo(fileName);
            var filename = fileInfo.Name.Replace(fileInfo.Extension, "");
            var fileSize_kB = fileInfo.Length / 1024;
            return filename + "(" + fileSize_kB.ToString() + "kB)";
        }
        public string FileRead(string fileName)
        {
            string output = null;
            try
            {
                using (FileStream fileStream = new FileStream(fileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    // To handle a file in writting, use FileShare.ReadWrite 
                    using (var sr = new StreamReader(fileStream, Encoding.UTF8))// see ./LoggerService.cs
                    {
                        String line;
                        while ((line = sr.ReadLine()) != null)
                        {
                            output += line;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
            return output;
        }

        public string[] LogViewLs()
        {
            var logFiles = LogViewFiles();
            return logFiles.ToList().Select(x => FileInfoString(x)).ToArray();
        }
        public string[] LogViewFull()
        {
            var logFiles = LogViewFiles();
            return logFiles.ToList().Select(x => FileRead(x)).ToArray();
        }
        public string[] LogViewGrep(string pattern, int max, bool reverse, int filter = 0, string[] keys = null)
        {
            var logFiles = LogViewFiles();
            if (reverse) Array.Reverse(logFiles);
            var output = new List<string>();
            int counter = 0;
            try
            {
                for (var fileCounter = 0; fileCounter < logFiles.Length; fileCounter++)
                {
                    using (FileStream fileStream = new FileStream(logFiles[fileCounter], FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var sr = new StreamReader(fileStream, Encoding.UTF8))// see ./LoggerService.cs
                        {
                            String line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                var match = Regex.Match(line, pattern);

                                if (match.Success)
                                {
                                    string add;
                                    if (filter > 0)
                                    {
                                        add = CsvFilter(line, filter);
                                    }
                                    else
                                    {
                                        add = line;
                                    }
                                    if (keys != null)
                                    {
                                        for (int i = 0; i < keys.Length; i++)
                                        {
                                            var key = keys[i];
                                            if (!string.IsNullOrEmpty(key))
                                            {
                                                add += "," + match.Groups[key].Value;
                                            }
                                        }
                                    }
                                    output.Add(add);
                                    counter++;
                                }
                            }
                            if (max > 0 && counter >= max)
                            {
                                fileCounter = logFiles.Length;
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(ex.ToString());
            }
            output.Sort();
            if (reverse) output.Reverse();
            return output.ToArray();
        }
        public string CsvFilter(string input, int i) // i > 0
        {
            return input.Split(',')[i - 1].Trim('"');
        }
        public string[] CsvFilter(string[] input, int i) // i > 0
        {
            return input.ToList().Select(x => CsvFilter(x, i)).ToArray();
        }
        public string[] LogViewTimeInitialize()
        {
            // When App starts, log Start OnInitialized() in ../../App.xaml.cs
            var outputs = LogViewGrep("Start\",\"OnInitialized", 10, true, 1);
            return outputs;
        }
        public string[] LogViewTimeHomePage()
        {
            // When App starts, log Start OnInitialized() in ../../App.xaml.cs
            var outputs = LogViewGrep("Start\",\"Initialize.*HomePageViewModel", 10, true, 1);
            return outputs;
        }

        public string[] LogViewTimeLastProcessTek()
        {
            // When LastProcessTekTimestamp is rewrited, log region ,newCreated in ../ExposureNotificationHandler.cs
            var outputs = LogViewGrep(@"Info(?<f>b?g?).*region: \d+, newCreated: (?<d>\d+)", 10, true, 0, new[] { "d", "f" });
            return outputs;
        }

        public string[] LogViewFiles()
        {
            var logsDirPath = logPathService.LogsDirPath;
            var logFiles = Directory.GetFiles(logsDirPath, logPathService.LogFileWildcardName);
            return logFiles;
        }
    }
}
