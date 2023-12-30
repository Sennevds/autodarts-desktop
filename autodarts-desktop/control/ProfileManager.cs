using autodarts_desktop.model;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Octokit;
using File = System.IO.File;
using Path = System.IO.Path;


namespace autodarts_desktop.control
{

    /// <summary>
    /// Manages everything around apps-lifecycle.
    /// </summary>
    public class ProfileManager
    {

        // ATTRIBUTES

        private readonly string appsDownloadableFile = "apps-downloadable.json";
        private readonly string appsInstallableFile = "apps-installable.json";
        private readonly string appsLocalFile = "apps-local.json";
        private readonly string appsOpenFile = "apps-open.json";
        private readonly string profilesFile = "profiles.json";

        public event EventHandler<AppEventArgs>? AppDownloadStarted;
        public event EventHandler<AppEventArgs>? AppDownloadFinished;
        public event EventHandler<AppEventArgs>? AppDownloadFailed;
        public event EventHandler<DownloadProgressChangedEventArgs>? AppDownloadProgressed;

        public event EventHandler<AppEventArgs>? AppInstallStarted;
        public event EventHandler<AppEventArgs>? AppInstallFinished;
        public event EventHandler<AppEventArgs>? AppInstallFailed;
        public event EventHandler<AppEventArgs>? AppConfigurationRequired;

        private List<AppBase> AppsAll;
        private List<AppDownloadable> AppsDownloadable;
        private List<AppInstallable> AppsInstallable;
        private List<AppLocal> AppsLocal;
        private List<AppOpen> AppsOpen;
        private List<Profile> Profiles;





        // METHODS

        public ProfileManager()
        {
            var basePath = Helper.GetAppBasePath();
            appsDownloadableFile = Path.Combine(basePath, appsDownloadableFile);
            appsInstallableFile = Path.Combine(basePath, appsInstallableFile);
            appsLocalFile = Path.Combine(basePath, appsLocalFile);
            appsOpenFile = Path.Combine(basePath, appsOpenFile);
            profilesFile = Path.Combine(basePath, profilesFile);
        }



        public async Task LoadAppsAndProfiles()
        {
            AppsAll = new();
            AppsDownloadable = new();
            AppsInstallable = new();
            AppsLocal = new();
            AppsOpen = new();

            Profiles = new();

            if (File.Exists(appsDownloadableFile))
            {
                try
                {
                    var appsDownloadable = JsonConvert.DeserializeObject<List<AppDownloadable>>(File.ReadAllText(appsDownloadableFile));
                    AppsDownloadable.AddRange(appsDownloadable!);
                    AppsAll.AddRange(AppsDownloadable);
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException(appsDownloadableFile, ex.Message);
                }
            }
            else
            {
                await CreateDummyAppsDownloadable();
            }

            if (File.Exists(appsInstallableFile))
            {
                try
                {
                    var appsInstallable = JsonConvert.DeserializeObject<List<AppInstallable>>(File.ReadAllText(appsInstallableFile));
                    AppsInstallable.AddRange(appsInstallable);
                    AppsAll.AddRange(AppsInstallable);
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException(appsInstallableFile, ex.Message);
                }
            }
            else
            {
                CreateDummyAppsInstallable();
            }


            if (File.Exists(appsLocalFile))
            {
                try
                {
                    var appsLocal = JsonConvert.DeserializeObject<List<AppLocal>>(File.ReadAllText(appsLocalFile));
                    AppsLocal.AddRange(appsLocal);
                    MigrateAppsLocal();
                    AppsAll.AddRange(AppsLocal);
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException(appsLocalFile, ex.Message);
                }
            }
            else
            {
                CreateDummyAppsLocal();
            }

            if (File.Exists(appsOpenFile))
            {
                try
                {
                    var appsOpen = JsonConvert.DeserializeObject<List<AppOpen>>(File.ReadAllText(appsOpenFile));
                    AppsOpen.AddRange(appsOpen);
                    MigrateAppsOpen();
                    AppsAll.AddRange(AppsOpen);
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException(appsOpenFile, ex.Message);
                }
            }
            else
            {
                CreateDummyAppsOpen();
            }


            if (File.Exists(profilesFile))
            {
                try
                {
                    Profiles = JsonConvert.DeserializeObject<List<Profile>>(File.ReadAllText(profilesFile));
                    MigrateProfiles();
                }
                catch (Exception ex)
                {
                    throw new ConfigurationException(profilesFile, ex.Message);
                }
            }
            else
            {
                CreateDummyProfiles();
            }


            foreach (var profile in Profiles)
            {
                foreach(KeyValuePair<string, ProfileState> profileLink in profile.Apps)
                {
                    var appFound = false;
                    foreach(var app in AppsAll)
                    {
                        if(app.Name == profileLink.Key)
                        {
                            appFound = true;
                            profileLink.Value.SetApp(app);
                            break;
                        }
                    }
                    if (!appFound) throw new Exception($"Profile-App '{profileLink.Key}' not found");
                }
            }


            foreach (var appDownloadable in AppsDownloadable)
            {
                appDownloadable.DownloadStarted += AppDownloadable_DownloadStarted;
                appDownloadable.DownloadFinished += AppDownloadable_DownloadFinished;
                appDownloadable.DownloadFailed += AppDownloadable_DownloadFailed;
                appDownloadable.DownloadProgressed += AppDownloadable_DownloadProgressed;
                appDownloadable.AppConfigurationRequired += App_AppConfigurationRequired;
            }
            foreach (var appInstallable in AppsInstallable)
            {
                appInstallable.DownloadStarted += AppDownloadable_DownloadStarted;
                appInstallable.DownloadFinished += AppDownloadable_DownloadFinished;
                appInstallable.DownloadFailed += AppDownloadable_DownloadFailed;
                appInstallable.DownloadProgressed += AppDownloadable_DownloadProgressed;
                appInstallable.InstallStarted += AppInstallable_InstallStarted;
                appInstallable.InstallFinished += AppInstallable_InstallFinished;
                appInstallable.InstallFailed += AppInstallable_InstallFailed;
                appInstallable.AppConfigurationRequired += App_AppConfigurationRequired;
            }
            foreach (var appLocal in AppsLocal)
            {
                appLocal.AppConfigurationRequired += App_AppConfigurationRequired;
            }
            foreach (var appOpen in AppsOpen)
            {
                appOpen.AppConfigurationRequired += App_AppConfigurationRequired;
            }
        }

        public void StoreApps()
        {
            SerializeApps(AppsDownloadable, appsDownloadableFile);
            SerializeApps(AppsInstallable, appsInstallableFile);
            SerializeApps(AppsLocal, appsLocalFile);
            SerializeApps(AppsOpen, appsOpenFile);
            SerializeProfiles(Profiles, profilesFile);
        }

        public void DeleteConfigurationFile(string configurationFile)
        {
            File.Delete(configurationFile);
        }

        public static bool RunProfile(Profile? profile)
        {
            if (profile == null) return false;

            var allAppsRunning = true;
            var appsTaggedForStart = profile.Apps.Where(x => x.Value.TaggedForStart);
            foreach (KeyValuePair<string, ProfileState> app in appsTaggedForStart)
            {
                // as here is no catch, apps-run stops when there is an error
                if (!app.Value.App.Run(app.Value.RuntimeArguments)) allAppsRunning = false;
            }
            return allAppsRunning;
        }

        public void CloseApps()
        {
            foreach (var app in AppsAll)
            {
                try
                {
                    app.Close();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Closing failed for app: {app.Name} - {ex.Message}");
                }
            }
        }

        public List<Profile> GetProfiles()
        {
            return Profiles;
        }




        private void CreateDummyAppsLocal()
        {
            List<AppLocal> apps = new();

            AppLocal custom =
               new(
                   name: "custom",
                   descriptionShort: "Starts a program on your file-system"
                   );

            apps.Add(custom);

            AppsLocal.AddRange(apps);
            AppsAll.AddRange(apps);
            SerializeApps(apps, appsLocalFile);
        }

        private void MigrateAppsLocal()
        {

            // Add more migs..
        }


        private void CreateDummyAppsOpen()
        {
            List<AppOpen> apps = new();

            AppOpen autodartsWeb =
                new(
                    name: "autodarts.io",
                    descriptionShort: "Opens autodart`s web-platform",
                    defaultValue: "https://autodarts.io"
                    );
            AppOpen autodartsBoardManager =
                new(
                    name: "autodarts-boardmanager",
                    descriptionShort: "Opens autodart`s board-manager",
                    defaultValue: "http://127.0.0.1:3180"
                    );

            apps.Add(autodartsWeb);
            apps.Add(autodartsBoardManager);

            AppsOpen.AddRange(apps);
            AppsAll.AddRange(apps);
            SerializeApps(apps, appsOpenFile);
        }

        private void MigrateAppsOpen()
        {
            var autodartsBoardManager = AppsOpen.FindIndex(a => a.Name == "autodarts-boardmanager");
            if (autodartsBoardManager == -1)
            {
                AppOpen autodartsBoardManagerCreate =
                                               new(
                                                   name: "autodarts-boardmanager",
                                                   descriptionShort: "Opens autodart`s board-manager",
                                                   defaultValue: "http://127.0.0.1:3180"
                                                   );

                AppsOpen.Add(autodartsBoardManagerCreate);
            }


            // Add more migs..
        }


        private void CreateDummyAppsInstallable()
        {
            // Define Download-Maps for Apps with os
            var dartboardsClientDownloadMap = new DownloadMap();
            dartboardsClientDownloadMap.WindowsX64 = "https://dartboards.online/dboclient_***VERSION***.exe";
            //dartboardsClientDownloadMap.MacX64 = "https://dartboards.online/dboclient_***VERSION***.dmg";
            var dartboardsClientDownloadUrl = dartboardsClientDownloadMap.GetDownloadUrlByOs("0.9.2");

            var droidCamDownloadMap = new DownloadMap();
            droidCamDownloadMap.WindowsX64 = "https://github.com/dev47apps/windows-releases/releases/download/win-***VERSION***/DroidCam.Setup.***VERSION***.exe";
            var droidCamDownloadUrl = droidCamDownloadMap.GetDownloadUrlByOs("6.5.2");

            var epocCamDownloadMap = new DownloadMap();
            epocCamDownloadMap.WindowsX64 = "https://edge.elgato.com/egc/windows/epoccam/EpocCam_Installer64_***VERSION***.exe";
            //epocCamDownloadMap.MacX64 = "https://edge.elgato.com/egc/macos/epoccam/EpocCam_Installer_***VERSION***.pkg";
            var epocCamDownloadUrl = epocCamDownloadMap.GetDownloadUrlByOs("3_4_0");



            List <AppInstallable> apps = new();

            if(dartboardsClientDownloadUrl != null)
            {
                AppInstallable dartboardsClient =
                new(
                    downloadUrl: dartboardsClientDownloadUrl,
                    name: "dartboards-client",
                    helpUrl: "https://dartboards.online/client",
                    descriptionShort: "webcam connection client for dartboards.online",
                    executable: "dartboardsonlineclient.exe",
                    defaultPathExecutable: Path.Join(Helper.GetUserDirectoryPath(), @"AppData\Local\Programs\dartboardsonlineclient"),
                    startsAfterInstallation: true
                    );
                apps.Add(dartboardsClient);
            }

            if (droidCamDownloadUrl != null)
            {
                AppInstallable droidCam =
                new(
                    downloadUrl: droidCamDownloadUrl,
                    name: "droid-cam",
                    helpUrl: "https://www.dev47apps.com",
                    descriptionShort: "uses your android phone/tablet as local camera",
                    defaultPathExecutable: @"C:\Program Files (x86)\DroidCam",
                    executable: "DroidCamApp.exe",
                    runAsAdminInstall: true,
                    startsAfterInstallation: false
                    );
                apps.Add(droidCam);
            }

            if (epocCamDownloadUrl != null)
            {
                AppInstallable epocCam =
                new(
                    downloadUrl: epocCamDownloadUrl,
                    name: "epoc-cam",
                    helpUrl: "https://www.elgato.com/de/epoccam",
                    descriptionShort: "uses your iOS phone/tablet as local camera",
                    defaultPathExecutable: @"C:\Program Files (x86)\Elgato\EpocCam",
                    // epoccamtray.exe
                    executable: "EpocCamService.exe",
                    runAsAdminInstall: false,
                    startsAfterInstallation: false,
                    isService: true
                    );
                apps.Add(epocCam);
            }

            AppsInstallable.AddRange(apps);
            AppsAll.AddRange(apps);
            SerializeApps(apps, appsInstallableFile);
        }


        private async Task CreateDummyAppsDownloadable()
        {
            // Define os-specific download-Maps for each app
            var autodartsClientDownloadMap = new DownloadMap
            {
                MacX64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.darwin-amd64.opencv4.7.0.tar.gz",
                MacArm64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.darwin-arm64.opencv4.7.0.tar.gz",
                LinuxX64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.linux-amd64.tar.gz",
                LinuxArm64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.linux-arm64.tar.gz",
                WindowsX64 = "https://github.com/autodarts/releases/releases/download/v***VERSION***/autodarts***VERSION***.windows-amd64.zip"
            };
            var autodartsClientDownloadUrl = autodartsClientDownloadMap.GetDownloadUrlByOs("0.22.0");
            var tag = await GetLatestCallerVersion();
            var autodartsCallerDownloadMap = new DownloadMap
            {
                WindowsX64 = "https://github.com/Sennevds/autodarts-caller/releases/download/***VERSION***/autodarts-caller.exe",
                LinuxX64 = "https://github.com/Sennevds/autodarts-caller/releases/download/***VERSION***/autodarts-caller",
                MacX64 = "https://github.com/Sennevds/autodarts-caller/releases/download/***VERSION***/autodarts-caller-mac"
            };
            var autodartsCallerDownloadUrl = autodartsCallerDownloadMap.GetDownloadUrlByOs(tag);

            var autodartsExternDownloadMap = new DownloadMap
            {
                WindowsX64 = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern.exe",
                LinuxX64 = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern",
                MacX64 = "https://github.com/lbormann/autodarts-extern/releases/download/v***VERSION***/autodarts-extern-mac"
            };
            var autodartsExternDownloadUrl = autodartsExternDownloadMap.GetDownloadUrlByOs("1.5.4");

            var autodartsWledDownloadMap = new DownloadMap
            {
                WindowsX64 = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled.exe",
                LinuxX64 = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled",
                MacX64 = "https://github.com/lbormann/autodarts-wled/releases/download/v***VERSION***/autodarts-wled-mac"
            };
            var autodartsWledDownloadUrl = autodartsWledDownloadMap.GetDownloadUrlByOs("1.4.6");

            var virtualDartsZoomDownloadMap = new DownloadMap
            {
                WindowsX64 = "https://www.lehmann-bo.de/Downloads/VDZ/Virtual Darts Zoom.zip"
            };
            var virtualDartsZoomDownloadUrl = virtualDartsZoomDownloadMap.GetDownloadUrlByOs();

            var autodartsGifDownloadMap = new DownloadMap
            {
                WindowsX64 = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif.exe",
                LinuxX64 = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif",
                MacX64 = "https://github.com/lbormann/autodarts-gif/releases/download/v***VERSION***/autodarts-gif-mac"
            };
            var autodartsGifDownloadUrl = autodartsGifDownloadMap.GetDownloadUrlByOs("1.0.3");

            var autodartsVoiceDownloadMap = new DownloadMap
            {
                WindowsX64 = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice.exe",
                LinuxX64 = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice",
                MacX64 = "https://github.com/lbormann/autodarts-voice/releases/download/v***VERSION***/autodarts-voice-mac"
            };
            var autodartsVoiceDownloadUrl = autodartsVoiceDownloadMap.GetDownloadUrlByOs("1.0.5");

            var camLoaderDownloadMap = new DownloadMap
            {
                WindowsX86 = "https://github.com/lbormann/cam-loader/releases/download/v***VERSION***/cam-loader.zip",
                WindowsX64 = "https://github.com/lbormann/cam-loader/releases/download/v***VERSION***/cam-loader.zip"
            };
            var camLoaderDownloadUrl = camLoaderDownloadMap.GetDownloadUrlByOs("1.0.0");




            List<AppDownloadable> apps = new();

            if (!String.IsNullOrEmpty(autodartsClientDownloadUrl))
            {
                AppDownloadable autodarts =
                new(
                    downloadUrl: autodartsClientDownloadUrl,
                    name: "autodarts-client",
                    helpUrl: "https://docs.autodarts.io/",
                    descriptionShort: "Client for dart recognition with cameras"
                    );
                apps.Add(autodarts);
            }

            if (!String.IsNullOrEmpty(autodartsCallerDownloadUrl))
            {
                AppDownloadable autodartsCaller =
                    new(
                        downloadUrl: autodartsCallerDownloadUrl,
                        name: "autodarts-caller",
                        helpUrl: "https://github.com/Sennevds/autodarts-caller",
                        descriptionShort: "calls out thrown points",
                        configuration: new(
                            prefix: "-",
                            delimitter: " ",
                            arguments: new List<Argument> {
                            new(name: "U", type: "string", required: true, nameHuman: "autodarts-username", section: "Autodarts"),
                            new(name: "P", type: "password", required: true, nameHuman: "autodarts-password", section: "Autodarts"),
                            new(name: "B", type: "string", required: true, nameHuman: "autodarts-board-id", section: "Autodarts"),
                            new(name: "M", type: "path", required: true, nameHuman: "path-to-sound-files", section: "Media"),
                            new(name: "MS", type: "path", required: false, nameHuman: "path-to-shared-sound-files", section: "Media"),
                            new(name: "V", type: "float[0.0..1.0]", required: false, nameHuman: "caller-volume", section: "Media"),
                            new(name: "C", type: "string", required: false, nameHuman: "specific-caller", section: "Calls"),
                            new(name: "R", type: "bool", required: false, nameHuman: "random-caller", section: "Random", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "L", type: "bool", required: false, nameHuman: "random-caller-each-leg", section: "Random", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "RL", type: "int[0..6]", required: false, nameHuman: "random-caller-language", section: "Random"),
                            new(name: "RG", type: "int[0..2]", required: false, nameHuman: "random-caller-gender", section: "Random"),
                            new(name: "CCP", type: "bool", required: false, nameHuman: "call-current-player", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "E", type: "bool", required: false, nameHuman: "call-every-dart", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "ESF", type: "bool", required: false, nameHuman: "call-every-dart-single-files", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "PCC", type: "int", required: false, nameHuman: "possible-checkout-call", section: "Calls"),
                            new(name: "PCCSF", type: "bool", required: false, nameHuman: "possible-checkout-call-single-files", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "PCCYO", type: "bool", required: false, nameHuman: "possible-checkout-call-only-yourself", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "A", type: "float[0.0..1.0]", required: false, nameHuman: "ambient-sounds", section: "Calls"),
                            new(name: "AAC", type: "bool", required: false, nameHuman: "ambient-sounds-after-calls", section: "Calls", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "DL", type: "bool", required: false, nameHuman: "downloads", section: "Downloads", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "DLLA", type: "int[0..6]", required: false, nameHuman: "downloads-language", section: "Downloads"),
                            new(name: "DLL", type: "int", required: false, nameHuman: "downloads-limit", section: "Downloads"),
                            new(name: "BAV", type: "float[0.0..1.0]", required: false, nameHuman: "background-audio-volume", section: "Calls"),
                            new(name: "WEB", type: "int[0..2]", required: false, nameHuman: "web-caller", section: "Service"),
                            new(name: "WEBSB", type: "bool", required: false, nameHuman: "web-scoreboard", section: "Service", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "WEBP", type: "int", required: false, nameHuman: "web-caller-port", section: "Service"),
                            new(name: "HP", type: "int", required: false, nameHuman: "host-port", section: "Service"),
                            new(name: "DEB", type: "bool", required: false, nameHuman: "debug", section: "Service", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"}),
                            new(name: "CC", type: "bool", required: false, nameHuman: "cert-check", section: "Service", valueMapping: new Dictionary<string, string>{["True"] = "1",["False"] = "0"})
                            })
                        );
                apps.Add(autodartsCaller);
            }

            if (!String.IsNullOrEmpty(autodartsExternDownloadUrl)) {
                AppDownloadable autodartsExtern =
                new(
                    downloadUrl: autodartsExternDownloadUrl,
                    name: "autodarts-extern",
                    helpUrl: "https://github.com/lbormann/autodarts-extern",
                    descriptionShort: "automates dart web platforms with autodarts",
                    configuration: new(
                        prefix: "--",
                        delimitter: " ",
                        arguments: new List<Argument> {
                            new(name: "connection", type: "string", required: false, nameHuman: "Connection", section: "Service"),
                            new(name: "browser_path", type: "file", required: true, nameHuman: "Path to browser", section: "", description: "Path to browser. fav. Chrome"),
                            new(name: "autodarts_user", type: "string", required: true, nameHuman: "Autodarts-Email", section: "Autodarts"),
                            new(name: "autodarts_password", type: "password", required: true, nameHuman: "Autodarts-Password", section: "Autodarts"),
                            new(name: "autodarts_board_id", type: "string", required: true, nameHuman: "Autodarts-Board-ID", section: "Autodarts"),
                            new(name: "extern_platform", type: "selection[lidarts,nakka,dartboards]", required: true, nameHuman: "", isRuntimeArgument: true),
                            new(name: "time_before_exit", type: "int[0..150000]", required: false, nameHuman: "Dwel after match end (in milliseconds)", section: "Match"),
                            new(name: "lidarts_user", type: "string", required: false, nameHuman: "Lidarts-Email", section: "Lidarts", requiredOnArgument: "extern_platform=lidarts"),
                            new(name: "lidarts_password", type: "password", required: false, nameHuman: "Lidarts-Password", section: "Lidarts", requiredOnArgument: "extern_platform=lidarts"),
                            new(name: "lidarts_skip_dart_modals", type: "bool", required: false, nameHuman: "Skip dart-modals", section: "Lidarts"),
                            new(name: "lidarts_chat_message_start", type: "string", required: false, nameHuman: "Chat-message on match-start", section: "Lidarts", value: "Hi, GD! Automated darts-scoring - powered by autodarts.io - Enter the community: https://discord.gg/bY5JYKbmvM"),
                            new(name: "lidarts_chat_message_end", type: "string", required: false, nameHuman: "Chat-message on match-end", section: "Lidarts", value: "Thanks GG, WP!"),
                            new(name: "nakka_skip_dart_modals", type: "bool", required: false, nameHuman: "Skip dart-modals", section: "Nakka"),
                            new(name: "dartboards_user", type: "string", required: false, nameHuman: "Dartboards-Email", section: "Dartboards", requiredOnArgument: "extern_platform=dartboards"),
                            new(name: "dartboards_password", type: "password", required: false, nameHuman: "Dartboards-Password", section: "Dartboards", requiredOnArgument: "extern_platform=dartboards"),
                            new(name: "dartboards_skip_dart_modals", type: "bool", required: false, nameHuman: "Skip dart-modals", section: "Dartboards"),
                        })
                );
                apps.Add(autodartsExtern);
            }

            if (!String.IsNullOrEmpty(autodartsWledDownloadUrl))
            {
                var autodartsWledArguments = new List<Argument> {
                        new(name: "CON", type: "string", required: false, nameHuman: "Connection", section: "Service"),
                        new(name: "WEPS", type: "string", required: true, isMulti: true, nameHuman: "wled-endpoints", section: "WLED"),
                        new(name: "DU", type: "int[0..10]", required: false, nameHuman: "effects-duration", section: "WLED"),
                        new(name: "BSS", type: "float[0.0..10.0]", required: false, nameHuman: "board-start-stop", section: "Autodarts"),
                        new(name: "BRI", type: "int[1..255]", required: false, nameHuman: "effects-brightness", section: "WLED"),
                        new(name: "HFO", type: "int[2..170]", required: false, nameHuman: "highfinish-on", section: "Autodarts"),
                        new(name: "HF", type: "string", required: false, isMulti: true, nameHuman: "high-finish-effects", section: "WLED"),
                        new(name: "IDE", type: "string", required: false, nameHuman: "idle-effect", section: "WLED"),
                        new(name: "G", type: "string", required: false, isMulti: true, nameHuman: "game-won-effects", section: "WLED"),
                        new(name: "M", type: "string", required: false, isMulti : true, nameHuman: "match-won-effects", section: "WLED"),
                        new(name: "B", type: "string", required: false, isMulti : true, nameHuman: "busted-effects", section: "WLED"),
                        new(name: "DEB", type: "bool", required: false, nameHuman: "debug", section: "Service", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" })

                    };
                for (int i = 0; i <= 180; i++)
                {
                    var score = i.ToString();
                    Argument scoreArgument = new(name: "S" + score, type: "string", required: false, isMulti: true, nameHuman: "score " + score, section: "WLED");
                    autodartsWledArguments.Add(scoreArgument);
                }
                for (int i = 1; i <= 12; i++)
                {
                    var areaNumber = i.ToString();
                    Argument areaArgument = new(name: "A" + areaNumber, type: "string", required: false, isMulti: true, nameHuman: "area-" + areaNumber, section: "WLED");
                    autodartsWledArguments.Add(areaArgument);
                }

                AppDownloadable autodartsWled =
                new(
                    downloadUrl: autodartsWledDownloadUrl,
                    name: "autodarts-wled",
                    helpUrl: "https://github.com/lbormann/autodarts-wled",
                    descriptionShort: "control wled installations",
                    configuration: new(
                        prefix: "-",
                        delimitter: " ",
                        arguments: autodartsWledArguments)
                    );
                apps.Add(autodartsWled);
            }

            if (!String.IsNullOrEmpty(virtualDartsZoomDownloadUrl))
            {
                AppDownloadable virtualDartsZoom =
                new(
                    downloadUrl: virtualDartsZoomDownloadUrl,
                    name: "virtual-darts-zoom",
                    helpUrl: "https://lehmann-bo.de/?p=28",
                    descriptionShort: "zooms webcam image onto the thrown darts",
                    runAsAdmin: true
                    );
                apps.Add(virtualDartsZoom);
            }

            if (!String.IsNullOrEmpty(autodartsGifDownloadUrl))
            {
                var autodartsGifArguments = new List<Argument> {
                         new(name: "MP", type: "path", required: false, nameHuman: "path-to-image-files", section: "Media"),
                         new(name: "CON", type: "string", required: false, nameHuman: "Connection", section: "Service"),
                         new(name: "HFO", type: "int[2..170]", required: false, nameHuman: "highfinish-on", section: "Autodarts"),
                         new(name: "HF", type: "string", required: false, isMulti: true, nameHuman: "high-finish-images", section: "Images"),
                         new(name: "G", type: "string", required: false, isMulti: true, nameHuman: "game-won-images", section: "Images"),
                         new(name: "M", type: "string", required: false, isMulti : true, nameHuman: "match-won-images", section: "Images"),
                         new(name: "B", type: "string", required: false, isMulti : true, nameHuman: "busted-images", section: "Images"),
                         new(name: "WEB", type: "int[0..2]", required: false, nameHuman: "web-gifs", section: "Service"),
                         new(name: "DEB", type: "bool", required: false, nameHuman: "debug", section: "Service", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" })

                     };
                for (int i = 0; i <= 180; i++)
                {
                    var score = i.ToString();
                    Argument scoreArgument = new(name: "S" + score, type: "string", required: false, isMulti: true, nameHuman: "score " + score, section: "Images");
                    autodartsGifArguments.Add(scoreArgument);
                }
                for (int i = 1; i <= 12; i++)
                {
                    var areaNumber = i.ToString();
                    Argument areaArgument = new(name: "A" + areaNumber, type: "string", required: false, isMulti: true, nameHuman: "area-" + areaNumber, section: "Images");
                    autodartsGifArguments.Add(areaArgument);
                }

                AppDownloadable autodartsGif =
                new(
                    downloadUrl: autodartsGifDownloadUrl,
                    name: "autodarts-gif",
                    helpUrl: "https://github.com/lbormann/autodarts-gif",
                    descriptionShort: "displays your favorite gifs",
                    configuration: new(
                        prefix: "-",
                        delimitter: " ",
                        arguments: autodartsGifArguments)
                    );
                apps.Add(autodartsGif);
            }

            if (!String.IsNullOrEmpty(autodartsVoiceDownloadUrl))
            {
                var autodartsVoiceArguments = new List<Argument> {
                        new(name: "CON", type: "string", required: false, nameHuman: "Connection", section: "Service"),
                        new(name: "MP", type: "path", required: true, nameHuman: "path-to-speech-model", section: "Voice-Recognition"),
                        new(name: "L", type: "int[0..2]", required: false, nameHuman: "language", section: "Voice-Recognition"),
                        new(name: "KNG", type: "string", required: false, isMulti: true, nameHuman: "keywords-next-game", section: "Voice-Recognition"),
                        new(name: "KN", type: "string", required: false, isMulti: true, nameHuman: "keywords-next", section: "Voice-Recognition"),
                        new(name: "KU", type: "string", required: false, isMulti: true, nameHuman: "keywords-undo", section: "Voice-Recognition"),
                        new(name: "KBC", type: "string", required: false, isMulti: true, nameHuman: "keywords-ban-caller", section: "Voice-Recognition"),
                        new(name: "KCC", type: "string", required: false, isMulti: true, nameHuman: "keywords-change-caller", section: "Voice-Recognition"),
                        new(name: "KSB", type: "string", required: false, isMulti: true, nameHuman: "keywords-start-board", section: "Voice-Recognition"),
                        new(name: "KSPB", type: "string", required: false, isMulti: true, nameHuman: "keywords-stop-board", section: "Voice-Recognition"),
                        new(name: "KRB", type: "string", required: false, isMulti: true, nameHuman: "keywords-reset-board", section: "Voice-Recognition"),
                        new(name: "KCB", type: "string", required: false, isMulti: true, nameHuman: "keywords-calibrate-board", section: "Voice-Recognition"),
                        new(name: "KFD", type: "string", required: false, isMulti: true, nameHuman: "keywords-first-dart", section: "Voice-Recognition"),
                        new(name: "KSD", type: "string", required: false, isMulti: true, nameHuman: "keywords-second-dart", section: "Voice-Recognition"),
                        new(name: "KTD", type: "string", required: false, isMulti: true, nameHuman: "keywords-third-dart", section: "Voice-Recognition"),
                        new(name: "KS", type: "string", required: false, isMulti: true, nameHuman: "keywords-single", section: "Voice-Recognition"),
                        new(name: "KD", type: "string", required: false, isMulti: true, nameHuman: "keywords-double", section: "Voice-Recognition"),
                        new(name: "KT", type: "string", required: false, isMulti: true, nameHuman: "keywords-triple", section: "Voice-Recognition"),
                        new(name: "KZERO", type: "string", required: false, isMulti: true, nameHuman: "keywords-zero", section: "Voice-Recognition"),
                        new(name: "KONE", type: "string", required: false, isMulti: true, nameHuman: "keywords-one", section: "Voice-Recognition"),
                        new(name: "KTWO", type: "string", required: false, isMulti: true, nameHuman: "keywords-two", section: "Voice-Recognition"),
                        new(name: "KTHREE", type: "string", required: false, isMulti: true, nameHuman: "keywords-three", section: "Voice-Recognition"),
                        new(name: "KFOUR", type: "string", required: false, isMulti: true, nameHuman: "keywords-four", section: "Voice-Recognition"),
                        new(name: "KFIVE", type: "string", required: false, isMulti: true, nameHuman: "keywords-five", section: "Voice-Recognition"),
                        new(name: "KSIX", type: "string", required: false, isMulti: true, nameHuman: "keywords-six", section: "Voice-Recognition"),
                        new(name: "KSEVEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-seven", section: "Voice-Recognition"),
                        new(name: "KEIGHT", type: "string", required: false, isMulti: true, nameHuman: "keywords-eight", section: "Voice-Recognition"),
                        new(name: "KNINE", type: "string", required: false, isMulti: true, nameHuman: "keywords-nine", section: "Voice-Recognition"),
                        new(name: "KTEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-ten", section: "Voice-Recognition"),
                        new(name: "KELEVEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-eleven", section: "Voice-Recognition"),
                        new(name: "KTWELVE", type: "string", required: false, isMulti: true, nameHuman: "keywords-twelve", section: "Voice-Recognition"),
                        new(name: "KTHIRTEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-thirteen", section: "Voice-Recognition"),
                        new(name: "KFOURTEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-fourteen", section: "Voice-Recognition"),
                        new(name: "KFIFTEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-fifteen", section: "Voice-Recognition"),
                        new(name: "KSIXTEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-sixteen", section: "Voice-Recognition"),
                        new(name: "KSEVENTEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-seventeen", section: "Voice-Recognition"),
                        new(name: "KEIGHTEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-eighteen", section: "Voice-Recognition"),
                        new(name: "KNINETEEN", type: "string", required: false, isMulti: true, nameHuman: "keywords-nineteen", section: "Voice-Recognition"),
                        new(name: "KTWENTY", type: "string", required: false, isMulti: true, nameHuman: "keywords-twenty", section: "Voice-Recognition"),
                        new(name: "KTWENTYFIVE", type: "string", required: false, isMulti: true, nameHuman: "keywords-twenty-five", section: "Voice-Recognition"),
                        new(name: "KFIFTY", type: "string", required: false, isMulti: true, nameHuman: "keywords-fifty", section: "Voice-Recognition"),
                        new(name: "DEB", type: "bool", required: false, nameHuman: "debug", section: "Service", valueMapping: new Dictionary<string, string> { ["True"] = "1", ["False"] = "0" })
                    };


                AppDownloadable autodartsVoice =
                new(
                    downloadUrl: autodartsVoiceDownloadUrl,
                    name: "autodarts-voice",
                    helpUrl: "https://github.com/lbormann/autodarts-voice",
                    descriptionShort: "control autodarts by voice",
                    configuration: new(
                        prefix: "-",
                        delimitter: " ",
                        arguments: autodartsVoiceArguments)
                    );
                apps.Add(autodartsVoice);
            }

            if (!String.IsNullOrEmpty(camLoaderDownloadUrl))
            {
                AppDownloadable camLoader =
                new(
                    downloadUrl: camLoaderDownloadUrl,
                    name: "cam-loader",
                    helpUrl: "https://github.com/lbormann/cam-loader",
                    descriptionShort: "Saves and loads camera settings"
                    );
                apps.Add(camLoader);
            }



            AppsDownloadable.AddRange(apps);
            AppsAll.AddRange(apps);
            
            SerializeApps(apps, appsDownloadableFile);
        }

        private async Task<string?> GetLatestCallerVersion()
        {
            var client = new GitHubClient(new ProductHeaderValue("my-cool-app"));

            // var response = await new HttpClient().GetAsync("https://api.github.com/repos/Sennevds/autodarts-caller/tags");
            
            var releases = await client.Repository.Release.GetAll("Sennevds", "autodarts-caller");
            var latest = releases!.MinBy(x=> x.TagName);
            
            return latest!.TagName;

        }



        private void CreateDummyProfiles()
        {
            var autodartsClient = AppsDownloadable.Find(a => a.Name == "autodarts-client") != null;
            var autodartsCaller = AppsDownloadable.Find(a => a.Name == "autodarts-caller") != null;
            var autodartsExtern = AppsDownloadable.Find(a => a.Name == "autodarts-extern") != null;
            var autodartsWled = AppsDownloadable.Find(a => a.Name == "autodarts-wled") != null;
            var autodartsGif = AppsDownloadable.Find(a => a.Name == "autodarts-gif") != null;
            var autodartsVoice = AppsDownloadable.Find(a => a.Name == "autodarts-voice") != null;
            var virtualDartsZoom = AppsDownloadable.Find(a => a.Name == "virtual-darts-zoom") != null;
            var camLoader = AppsDownloadable.Find(a => a.Name == "cam-loader") != null;
            var droidCam = AppsInstallable.Find(a => a.Name == "droid-cam") != null;
            var epocCam = AppsInstallable.Find(a => a.Name == "epoc-cam") != null;
            var dartboardsClient = AppsInstallable.Find(a => a.Name == "dartboards-client") != null;
            var custom = AppsLocal.Find(a => a.Name == "custom") != null;
            
            

            if (autodartsCaller)
            {
                var p1Name = "autodarts-caller";
                var p1Apps = new Dictionary<string, ProfileState>();
                if (autodartsClient) p1Apps.Add("autodarts-client", new ProfileState());
                p1Apps.Add("autodarts.io", new ProfileState());
                p1Apps.Add("autodarts-boardmanager", new ProfileState());
                if (autodartsCaller) p1Apps.Add("autodarts-caller", new ProfileState(true));
                if (autodartsWled) p1Apps.Add("autodarts-wled", new ProfileState());
                if (autodartsGif) p1Apps.Add("autodarts-gif", new ProfileState());
                if (autodartsVoice) p1Apps.Add("autodarts-voice", new ProfileState());
                if (camLoader) p1Apps.Add("cam-loader", new ProfileState());
                if (custom) p1Apps.Add("custom", new ProfileState());
                Profiles.Add(new Profile(p1Name, p1Apps));
            }
            
            if (autodartsCaller && autodartsExtern)
            {
                var p2Name = "autodarts-extern: lidarts.org";
                var p2Args = new Dictionary<string, string> { { "extern_platform", "lidarts" } };
                var p2Apps = new Dictionary<string, ProfileState>();
                if (autodartsClient) p2Apps.Add("autodarts-client", new ProfileState());
                p2Apps.Add("autodarts.io", new ProfileState());
                p2Apps.Add("autodarts-boardmanager", new ProfileState());
                if (autodartsCaller) p2Apps.Add("autodarts-caller", new ProfileState(true));
                if (autodartsWled) p2Apps.Add("autodarts-wled", new ProfileState());
                if (autodartsGif) p2Apps.Add("autodarts-gif", new ProfileState());
                if (autodartsVoice) p2Apps.Add("autodarts-voice", new ProfileState());
                if (autodartsExtern) p2Apps.Add("autodarts-extern", new ProfileState(true, runtimeArguments: p2Args));
                if (virtualDartsZoom) p2Apps.Add("virtual-darts-zoom", new ProfileState());
                if (camLoader) p2Apps.Add("cam-loader", new ProfileState());
                if (droidCam) p2Apps.Add("droid-cam", new ProfileState());
                if (epocCam) p2Apps.Add("epoc-cam", new ProfileState());
                if (custom) p2Apps.Add("custom", new ProfileState());
                Profiles.Add(new Profile(p2Name, p2Apps));
            }

            if (autodartsCaller && autodartsExtern)
            {
                var p3Name = "autodarts-extern: nakka.com/n01/online";
                var p3Args = new Dictionary<string, string> { { "extern_platform", "nakka" } };
                var p3Apps = new Dictionary<string, ProfileState>();
                if (autodartsClient) p3Apps.Add("autodarts-client", new ProfileState());
                p3Apps.Add("autodarts.io", new ProfileState());
                p3Apps.Add("autodarts-boardmanager", new ProfileState());
                if (autodartsCaller) p3Apps.Add("autodarts-caller", new ProfileState(true));
                if (autodartsWled) p3Apps.Add("autodarts-wled", new ProfileState());
                if (autodartsGif) p3Apps.Add("autodarts-gif", new ProfileState());
                if (autodartsVoice) p3Apps.Add("autodarts-voice", new ProfileState());
                if (autodartsExtern) p3Apps.Add("autodarts-extern", new ProfileState(true, runtimeArguments: p3Args));
                if (virtualDartsZoom) p3Apps.Add("virtual-darts-zoom", new ProfileState());
                if (camLoader) p3Apps.Add("cam-loader", new ProfileState());
                if (droidCam) p3Apps.Add("droid-cam", new ProfileState());
                if (epocCam) p3Apps.Add("epoc-cam", new ProfileState());
                if (custom) p3Apps.Add("custom", new ProfileState());
                Profiles.Add(new Profile(p3Name, p3Apps));
            }

            if (autodartsCaller && autodartsExtern)
            {
                var p4Name = "autodarts-extern: dartboards.online";
                var p4Args = new Dictionary<string, string> { { "extern_platform", "dartboards" } };
                var p4Apps = new Dictionary<string, ProfileState>();
                if (autodartsClient) p4Apps.Add("autodarts-client", new ProfileState());
                p4Apps.Add("autodarts.io", new ProfileState());
                p4Apps.Add("autodarts-boardmanager", new ProfileState());
                if (autodartsCaller) p4Apps.Add("autodarts-caller", new ProfileState(true));
                if (autodartsWled) p4Apps.Add("autodarts-wled", new ProfileState());
                if (autodartsGif) p4Apps.Add("autodarts-gif", new ProfileState());
                if (autodartsVoice) p4Apps.Add("autodarts-voice", new ProfileState());
                if (autodartsExtern) p4Apps.Add("autodarts-extern", new ProfileState(true, runtimeArguments: p4Args));
                if (virtualDartsZoom) p4Apps.Add("virtual-darts-zoom", new ProfileState());
                if (camLoader) p4Apps.Add("cam-loader", new ProfileState());
                if (dartboardsClient) p4Apps.Add("dartboards-client", new ProfileState());
                if (droidCam) p4Apps.Add("droid-cam", new ProfileState());
                if (epocCam) p4Apps.Add("epoc-cam", new ProfileState());
                if (custom) p4Apps.Add("custom", new ProfileState());
                Profiles.Add(new Profile(p4Name, p4Apps));
            }

            if (autodartsClient)
            {
                var p5Name = "autodarts-client";
                var p5Apps = new Dictionary<string, ProfileState>();
                if (autodartsClient) p5Apps.Add("autodarts-client", new ProfileState(true));
                p5Apps.Add("autodarts.io", new ProfileState());
                p5Apps.Add("autodarts-boardmanager", new ProfileState());
                if (virtualDartsZoom) p5Apps.Add("virtual-darts-zoom", new ProfileState());
                if (camLoader) p5Apps.Add("cam-loader", new ProfileState());
                if (droidCam) p5Apps.Add("droid-cam", new ProfileState());
                if (epocCam) p5Apps.Add("epoc-cam", new ProfileState());
                if (custom) p5Apps.Add("custom", new ProfileState());
                Profiles.Add(new Profile(p5Name, p5Apps));
            }

            SerializeProfiles(Profiles, profilesFile);
        }

        private void MigrateProfiles()
        {
            // 9. Mig (Remove autodarts-bot)
            foreach (var p in Profiles)
            {
                p.Apps.Remove("autodarts-bot");
            }

            // 15. Mig (Add autodarts-wled)
            foreach (var p in Profiles)
            {
                if (p.Name == "autodarts-client") continue;

                if (!p.Apps.ContainsKey("autodarts-wled"))
                {
                    p.Apps.Add("autodarts-wled", new());
                }      
            }

            var autodartsClient = AppsDownloadable.Find(a => a.Name == "autodarts-client") != null;
            var autodartsCaller = AppsDownloadable.Find(a => a.Name == "autodarts-caller") != null;
            var autodartsExtern = AppsDownloadable.Find(a => a.Name == "autodarts-extern") != null;
            var autodartsWled = AppsDownloadable.Find(a => a.Name == "autodarts-wled") != null;
            var virtualDartsZoom = AppsDownloadable.Find(a => a.Name == "virtual-darts-zoom") != null;
            var droidCam = AppsInstallable.Find(a => a.Name == "droid-cam") != null;
            var epocCam = AppsInstallable.Find(a => a.Name == "epoc-cam") != null;
            var dartboardsClient = AppsInstallable.Find(a => a.Name == "dartboards-client") != null;
            var custom = AppsLocal.Find(a => a.Name == "custom") != null;


            if (!autodartsCaller)
            {
                Profiles.RemoveAll(p => p.Name == "autodarts-caller");
                Profiles.RemoveAll(p => p.Name == "autodarts-extern: lidarts.org");
                Profiles.RemoveAll(p => p.Name == "autodarts-extern: nakka.com/n01/online");
                Profiles.RemoveAll(p => p.Name == "autodarts-extern: dartboards.online");
            }
            if (!autodartsExtern)
            {
                Profiles.RemoveAll(p => p.Name == "autodarts-extern: lidarts.org");
                Profiles.RemoveAll(p => p.Name == "autodarts-extern: nakka.com/n01/online");
                Profiles.RemoveAll(p => p.Name == "autodarts-extern: dartboards.online");
            }

            foreach (var p in Profiles)
            {
                if (!autodartsClient) p.Apps.Remove("autodarts-client");
                if (!autodartsCaller) p.Apps.Remove("autodarts-caller");
                if (!autodartsWled) p.Apps.Remove("autodarts-wled");
                if (!autodartsExtern) p.Apps.Remove("autodarts-extern");
                if (!virtualDartsZoom) p.Apps.Remove("virtual-darts-zoom");
                if (!dartboardsClient) p.Apps.Remove("dartboards-client");
                if (!droidCam) p.Apps.Remove("droid-cam");
                if (!epocCam) p.Apps.Remove("epoc-cam");
                if (!custom) p.Apps.Remove("custom");
            }

            var p5 = Profiles.Find(p => p.Name == "autodarts-client") != null;

            if (autodartsClient)
            {
                if (!p5)
                {
                    var p5Name = "autodarts-client";
                    var p5Apps = new Dictionary<string, ProfileState>();
                    if (autodartsClient) p5Apps.Add("autodarts-client", new ProfileState(true));
                    p5Apps.Add("autodarts.io", new ProfileState());
                    p5Apps.Add("autodarts-boardmanager", new ProfileState());
                    if (virtualDartsZoom) p5Apps.Add("virtual-darts-zoom", new ProfileState());
                    if (droidCam) p5Apps.Add("droid-cam", new ProfileState());
                    if (epocCam) p5Apps.Add("epoc-cam", new ProfileState());
                    if (custom) p5Apps.Add("custom", new ProfileState());
                    Profiles.Add(new Profile(p5Name, p5Apps));
                }
            }

            // Adds boardmanager to all profiles except autodarts-client
            foreach (var p in Profiles)
            {
                if (p.Name == "autodarts-client") continue;

                if (!p.Apps.ContainsKey("autodarts-boardmanager"))
                {
                    p.Apps.Add("autodarts-boardmanager", new());
                }
            }

            // Adds autodarts-gif to all profiles except autodarts-client and removes pointless apps from autodarts-client
            foreach (var p in Profiles)
            {
                if (p.Name == "autodarts-client")
                {
                    p.Apps.Remove("virtual-darts-zoom");
                    p.Apps.Remove("droid-cam");
                    p.Apps.Remove("epoc-cam");
                    continue;
                }

                if (!p.Apps.ContainsKey("autodarts-gif"))
                {
                    p.Apps.Add("autodarts-gif", new());
                }
            }


            // Adds or removes cam-loader for all profiles
            var camLoader = AppsDownloadable.Find(a => a.Name == "cam-loader") != null;
            if (!camLoader)
            {
                foreach (var p in Profiles)
                {
                    p.Apps.Remove("cam-loader");
                }
            }
            else
            {
                foreach (var p in Profiles)
                {
                    if (!p.Apps.ContainsKey("cam-loader"))
                    {
                        p.Apps.Add("cam-loader", new());
                    }
                }
            }

            // Adds or removes autodarts-voice for all profiles except autodarts-client
            var autodartsVoice = AppsDownloadable.Find(a => a.Name == "autodarts-voice") != null;
            if (!autodartsVoice)
            {
                foreach (var p in Profiles)
                {
                    p.Apps.Remove("autodarts-voice");
                }
            }
            else
            {
                foreach (var p in Profiles)
                {
                    if (p.Name == "autodarts-client") continue;

                    if (!p.Apps.ContainsKey("autodarts-voice"))
                    {
                        p.Apps.Add("autodarts-voice", new());
                    }
                }
            }





            // Add more migs..
        }



        private void SerializeApps<AppBase>(List<AppBase> apps, string filename)
        {
            var settings = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore
            };
            var appsJsonStr = JsonConvert.SerializeObject(apps, Formatting.Indented, settings);
            File.WriteAllText(filename, appsJsonStr);
        }
        
        private void SerializeProfiles(List<Profile> profiles, string filename)
        {
            var settings = new JsonSerializerSettings
            {
                DefaultValueHandling = DefaultValueHandling.Ignore
            };
            var profilesJsonStr = JsonConvert.SerializeObject(profiles, Formatting.Indented, settings);
            File.WriteAllText(filename, profilesJsonStr);
        }




        private void AppDownloadable_DownloadStarted(object? sender, AppEventArgs e)
        {
            OnAppDownloadStarted(e);
        }

        private void AppDownloadable_DownloadFinished(object? sender, AppEventArgs e)
        {
            OnAppDownloadFinished(e);
        }

        private void AppDownloadable_DownloadFailed(object? sender, AppEventArgs e)
        {
            OnAppDownloadFailed(e);
        }

        private void AppDownloadable_DownloadProgressed(object? sender, DownloadProgressChangedEventArgs e)
        {
            OnAppDownloadProgressed(e);
        }



        private void AppInstallable_InstallStarted(object? sender, AppEventArgs e)
        {
            OnAppInstallStarted(e);
        }

        private void AppInstallable_InstallFinished(object? sender, AppEventArgs e)
        {
            OnAppInstallFinished(e);
        }

        private void AppInstallable_InstallFailed(object? sender, AppEventArgs e)
        {
            OnAppInstallFailed(e);
        }

        private void App_AppConfigurationRequired(object? sender, AppEventArgs e)
        {
            OnAppConfigurationRequired(e);
        }



        protected virtual void OnAppDownloadStarted(AppEventArgs e)
        {
            AppDownloadStarted?.Invoke(this, e);
        }

        protected virtual void OnAppDownloadFinished(AppEventArgs e)
        {
            AppDownloadFinished?.Invoke(this, e);
        }

        protected virtual void OnAppDownloadFailed(AppEventArgs e)
        {
            AppDownloadFailed?.Invoke(this, e);
        }

        protected virtual void OnAppDownloadProgressed(DownloadProgressChangedEventArgs e)
        {
            AppDownloadProgressed?.Invoke(this, e);
        }



        protected virtual void OnAppInstallStarted(AppEventArgs e)
        {
            AppInstallStarted?.Invoke(this, e);
        }

        protected virtual void OnAppInstallFinished(AppEventArgs e)
        {
            AppInstallFinished?.Invoke(this, e);
        }

        protected virtual void OnAppInstallFailed(AppEventArgs e)
        {
            AppInstallFailed?.Invoke(this, e);
        }

        protected virtual void OnAppConfigurationRequired(AppEventArgs e)
        {
            AppConfigurationRequired?.Invoke(this, e);
        }
        

    }
}
