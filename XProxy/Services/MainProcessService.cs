﻿using Microsoft.Extensions.Hosting;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using XProxy.Services;
using XProxy.Shared;
using XProxy.Shared.Services;

namespace XProxy.Patcher.Services
{
    public class MainProcessService : BackgroundService
    {
        public static string CoreFolder => "Core";

        static string Executable => Path.Combine(CoreFolder, OperatingSystem.IsWindows() ? "XProxy.Core.exe" : "XProxy.Core");
        static string AssemblyFile => Path.Combine(CoreFolder, "XProxy.Core.dll");

        public static Version AssemblyVersion { get; set; } = new Version(0, 0, 0);
        public static bool AssemblyUpdated { get; set; }

        public static bool IsWaitingForProcessExit;

        public static bool IsUpdating;
        public static bool DoUpdate;

        Process _mainProcess;

        private ConfigService _config;

        public MainProcessService(ConfigService config)
        {
            _config = config;
        }

        void GetAssemblyVersion()
        {
            if (File.Exists(AssemblyFile))
            {
                AssemblyName name = AssemblyName.GetAssemblyName(AssemblyFile);
                if (name != null)
                    AssemblyVersion = name.Version;
            }
            else
            {
                AssemblyVersion = new Version(0, 0, 0);
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            if (_mainProcess != null)
                _mainProcess.Kill(true);

            Environment.Exit(0);

            return base.StopAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            GetAssemblyVersion();

            Logger.Info("Check for updates...", "XProxy");

            while (UpdaterService.CheckForUpdates)
            {
                await Task.Delay(10);
            }

            if (AssemblyUpdated) GetAssemblyVersion();

            while (!stoppingToken.IsCancellationRequested)
            {
                if (File.Exists(Executable))
                {
                    StartProcess();

                    if (_mainProcess == null)
                    {
                        Logger.Info("Failed to start main process, retrying in 5 seconds...", "XProxy");
                        await Task.Delay(5000);
                        continue;
                    }

                    IsWaitingForProcessExit = true;
                    await _mainProcess.WaitForExitAsync(stoppingToken);
                    IsWaitingForProcessExit = false;

                    DoUpdate = File.Exists("./update");
                    if (DoUpdate) IsUpdating = true;
                }

                while (IsUpdating)
                {
                    await Task.Delay(10);
                }

                if (AssemblyUpdated) 
                    GetAssemblyVersion();

                await Task.Delay(5000);
            }

            _mainProcess.Kill();
        }

        void StartProcess()
        {
            ProcessStartInfo info = new ProcessStartInfo(Executable);

            info.ArgumentList.Add($"-p {Environment.CurrentDirectory}");

            if (Logger.AnsiDisabled)
                info.ArgumentList.Add("--ansidisable");

            try
            {
                _mainProcess = Process.Start(info);
            }
            catch(Win32Exception winEx)
            {
                Logger.Error(winEx.Message, "XProxy");
            }
            catch (Exception ex)
            {
                Logger.Error("Error " + ex, "XProxy");
            }
        }

        public override void Dispose()
        {
            if (_mainProcess != null)
                _mainProcess.Dispose();

            base.Dispose();
        }
    }
}
