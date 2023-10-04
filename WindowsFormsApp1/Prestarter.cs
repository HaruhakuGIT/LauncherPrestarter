﻿using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Windows.Forms;

namespace WindowsFormsApp1
{
    internal class Prestarter
    {
        public string projectName;
        public static HttpClient sharedClient = new HttpClient();
        public IProgress<float> progress;
        public IStatusPeporter reporter;
        public string launcherUrl;

        public Prestarter(IProgress<float> progress, IStatusPeporter reporter)
        {
            this.projectName = Config.PROJECT;
            this.progress = progress;
            this.launcherUrl = Config.LAUNCHER_URL;
            this.reporter = reporter;
        }

        public JavaStatus checkDate(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return JavaStatus.NOT_INSTALLED;
                }
                string text = File.ReadAllText(path);
                DateTime parsed = DateTime.Parse(text);
                DateTime now = DateTime.Now;
                if (parsed.AddDays(30) < now)
                {
                    return JavaStatus.NEED_UPDATE;
                }
                return JavaStatus.OK;
            }
            catch (Exception e)
            {
                return JavaStatus.NOT_INSTALLED;
            }
        }

        public enum JavaStatus
        {
            NOT_INSTALLED, NEED_UPDATE, OK
        }

        public async void run()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            string basePath = Environment.GetEnvironmentVariable("APPDATA") + "\\" + projectName;
            Directory.CreateDirectory(basePath);
            string javaPath = null;
            if (Config.USAGE_GLOBAL_JAVA)
            {
                string globalBasePath = Environment.GetEnvironmentVariable("APPDATA") + "\\GravitLauncherStore";
                Directory.CreateDirectory(globalBasePath);
                string globalJavaPath = globalBasePath + "\\Java";
                Directory.CreateDirectory(globalJavaPath);
                javaPath = globalJavaPath + "\\" + Config.javaDownloader.getPrefix();
            }
            else
            {
                javaPath = basePath + "\\" + "jre-full";
            }
            string dateFilePath = javaPath + "\\" + "date-updated";
            var javaStatus = checkDate(dateFilePath);
            if (javaStatus != JavaStatus.OK)
            {
                if (Config.ENABLE_DOWNLOAD_QUESTION)
                {
                    if (javaStatus == JavaStatus.NEED_UPDATE)
                    {
                        var dialog = MessageBox.Show(string.Format("Доступно обновление Java. Обновить?", projectName), "Prestarter", MessageBoxButtons.YesNoCancel);
                        if (dialog == DialogResult.No)
                        {
                            goto launcher_start;
                        }
                        else if (dialog == DialogResult.Yes)
                        {
                            // Yes
                        }
                        else
                        {
                            Application.Exit();
                            return;
                        }
                    }
                    else
                    {
                        var dialog = MessageBox.Show(string.Format("Для запуска лаунчера {0} необходимо программное обеспечение Java. Скачать {1}?", projectName, Config.javaDownloader.GetName()), "Prestarter", MessageBoxButtons.OKCancel);
                        if (dialog != DialogResult.OK)
                        {
                            Application.Exit();
                            return;
                        }
                    }
                }
                var result = await Config.javaDownloader.Download(javaPath, this);
                if(result)
                {
                    var openjfxResult = await Config.openjfxDownloader.Download(javaPath, this);
                }
                File.WriteAllText(dateFilePath, DateTime.Now.ToString());
            }
        launcher_start:
            reporter.updateStatus("Поиск лаунчера");
            string launcherPath = basePath + "\\Launcher.jar";
            if (launcherUrl == null)
            {
                launcherPath = System.Reflection.Assembly.GetEntryAssembly().Location;
            }
            else if (!File.Exists(launcherPath))
            {
                reporter.updateStatus("Скачивание лаунчера");
                reporter.requestNormalProgressbar();
                using (var file = new FileStream(launcherPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await sharedClient.DownloadAsync(launcherUrl, file, progress);
                }
                reporter.requestWaitProgressbar();
            }
            reporter.updateStatus("Запуск");
            Process process = new Process();
            // Configure the process using the StartInfo properties.
            process.StartInfo.FileName = javaPath + "\\bin\\java.exe";
            process.StartInfo.Arguments = "-Dlauncher.noJavaCheck=true -jar \"" + launcherPath + "\"";
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;
            process.Start();
            Thread startThread = new Thread(() =>
            {
                if (process.WaitForExit(500))
                {
                    ReportErrorAndExit("Процесс лаунчера завершился слишком быстро");
                }
                Application.Exit();
            });
            startThread.Start();
        }

        public void ReportErrorAndExit(string message)
        {
            MessageBox.Show(message, "GravitLauncher Prestarter", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Application.Exit();
        }
    }
}
