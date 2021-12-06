using System;
using System.IO;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using System.Threading;
using System.Diagnostics;
using System.ComponentModel;
using System.IO.Compression;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;

[assembly: CLSCompliant(true)]
namespace EZ_Updater
{
    struct GithubResponse
    {
        [JsonProperty("message")]
        public string Message { get; set; }
        [JsonProperty("tag_name")]
        public string TagName { get; set; }
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("assets")]
        public List<Asset> Assets { get; set; }
        [JsonProperty("body")]
        public string Body { get; set; }
    }
    struct Asset
    {
        [JsonProperty("name")]
        public string Name { get; set; }
        [JsonProperty("browser_download_url")]
        public string BrowserDownloadUrl { get; set; }
    }

    public enum UpdaterState
    {
        Idle = -1,
        Fetching = 0,
        CheckingUpdate = 1,
        NoUpdateAvailable = 2,
        UpdateAvailable = 3,
        Downloading = 4,
        Retrying = 5,
        Canceled = 6,
        Downloaded = 7,
        Installing = 8,
        InstallFailed = 9,
        Installed = 10,
    }

    public class Updater
    {
        /// <summary>
        /// GitHub User owner of the repository where update is located
        /// </summary>
        public static string GitHub_User { get; set; }
        
        /// <summary>
        /// GitHub Repositoy where update is located
        /// </summary>
        public static string GitHub_Repository { get; set; }
        
        /// <summary>
        /// GitHub Asset to download
        /// </summary>
        /// <value>ProgramName.exe</value>
        public static string GitHub_Asset { get; set; }
        
        /// <returns>
        /// Count of how many retry of downloading are done (MAX: 4)
        /// </returns>
        public static int DownloadRetryCount { get; private set; }
        
        /// <returns>
        /// GitHub latest release name (or tittle)
        /// </returns>
        public static string ReleaseName { get; private set; }

        /// <summary>
        /// GitHub latest release body text
        /// </summary>
        public static string ReleaseBody { get; private set; }
        
        /// <returns>
        /// GitHub latest release tag version (without letters, just: Major.Minor.Patch.Build)
        /// </returns>
        public static Version ReleaseVersion { get; private set; }
        
        /// <returns>
        /// Updater state
        /// </returns>
        public static UpdaterState State { get; private set; }
        
        /// <returns>
        /// Updater update download percentage
        /// </returns>
        public static int ProgressPercentage { get; private set; }

        /// <returns>
        /// Updater message of UpdaterState
        /// </returns>
        public static string Message { get; private set; }


        // Program File Attributes
        readonly private static string ProgramFile = Process.GetCurrentProcess().MainModule.FileName;
        readonly private static string ProgramFilePath = Path.GetDirectoryName(ProgramFile);
        /// <returns>
        /// Main program file name
        /// </returns>
        readonly public static string ProgramFileName = Path.GetFileName(ProgramFile);
        /// <returns>
        /// Main program file version
        /// </returns>
        readonly public static Version ProgramFileVersion = new Version(FileVersionInfo.GetVersionInfo(ProgramFile).FileVersion);


        // GitHub API Attributes
        private static Uri GitHub_ApiUrl => new Uri($"https://api.github.com/repos/{GitHub_User}/{GitHub_Repository}/releases/latest");
        private static GithubResponse APIResponse;
        private static Asset AssetSelected = new Asset();


        // Updater Attributes
        private static WebClient WebClient;
        private static int MovedFiles = 0;
        private static int UpdateNFiles = 0;
        private static List<string> MovedFileDir = new List<string>();
        private static Timer Timer = new Timer(RetryToDownload, null, -1, -1);
        private static string EZTempPath => Path.GetTempPath() + $"EZ_Updater{DownloadRetryCount}";

        // Updater Events things
        /// <summary>
        /// GUI context for calling Events that modifies a GUI
        /// <code>
        /// 
        /// // Pass current sync context for GUI events
        /// Updater.GUI_Context = SynchronizationContext.Current;
        /// </code>
        /// </summary>
        public static SynchronizationContext GUI_Context = SynchronizationContext.Current;

        /// <summary>
        /// Calls your custom logger to log EZ_Updater logs
        /// <code>
        /// Example:
        /// 
        /// Updater.CustomLogger = MyLogger;
        /// 
        /// public void MyLogger(string LogMessage){
        ///     //Your code for logging...
        /// }
        /// </code>
        /// </summary>
        public static Action<string> CustomLogger = null;

        /// <summary>
        /// Occurs when download is canceled after some attempts
        /// </summary>
        public static event DownloadCanceledEvent DownloadCanceled;
        public delegate void DownloadCanceledEvent(object sender, EventArgs args);
        protected static void OnDowloadCanceled()
        {
            State = UpdaterState.Canceled;
            Message = "Download canceled";
            Log("Download canceled");
            CallOnUIThread(() => { DownloadCanceled?.Invoke(null, EventArgs.Empty); });
            OnUpdaterChange();
        }

        /// <summary>
        /// Occurs when download is being retried
        /// </summary>
        public static event RetryDownloadEvent RetryDownload;
        public delegate void RetryDownloadEvent(object sender, EventArgs args);
        protected static void OnRetryDownload()
        {
            State = UpdaterState.Retrying;
            Message = $"Retrying download... {DownloadRetryCount}/4";
            Log($"Retrying download {DownloadRetryCount}/4");
            CallOnUIThread(() => { RetryDownload?.Invoke(null, EventArgs.Empty); });
            OnUpdaterChange();
        }

        /// <summary>
        /// Occurs when download make a progress
        /// </summary>
        public static event DownloadProgressEvent DownloadProgress;
        public delegate void DownloadProgressEvent(object sender, DownloadProgressChangedEventArgs args);
        protected static void OnDownloadProgress(DownloadProgressChangedEventArgs e)
        {
            Log(State != UpdaterState.Downloading ,$"Downloading: {AssetSelected.Name} | {AssetSelected.BrowserDownloadUrl}");
            State = UpdaterState.Downloading;
            Message = "Downloading update...";
            CallOnUIThread(() => { DownloadProgress?.Invoke(null, e); });
            OnUpdaterChange();
        }

        /// <summary>
        /// Occurs when installation make a progress
        /// </summary>
        public static event UpdateProgressEvent UpdateProgress;
        public delegate void UpdateProgressEvent(object sender, EventArgs args);
        protected static void OnUpdateProgress()
        {
            CallOnUIThread(() => { UpdateProgress?.Invoke(null, EventArgs.Empty); });
            OnUpdaterChange();
        }

        /// <summary>
        /// Occurs when installation of the update fails
        /// </summary>
        public static event UpdateFailedEvent UpdateFailed;
        public delegate void UpdateFailedEvent(object sender, EventArgs args);
        protected static void OnUpdateFailed()
        {
            Log("Update Failed! restoring backup files");
            State = UpdaterState.InstallFailed;
            Message = "Installation failed";
            CallOnUIThread(() => { UpdateFailed?.Invoke(null, EventArgs.Empty); });
            OnUpdaterChange();
        }

        /// <summary>
        /// Occurs when updating process is finished. You should restart your application.
        /// </summary>
        public static event UpdateFinishedEvent UpdateFinished;
        public delegate void UpdateFinishedEvent(object sender, EventArgs args);
        protected static void OnUpdateFinished()
        {
            State = UpdaterState.Installed;
            Message = "Update installed!";
            Log("Update installed!");
            OnUpdaterChange();
            Thread.Sleep(250);
            CallOnUIThread(() => { UpdateFinished?.Invoke(null, EventArgs.Empty); }, false);
            TryToCleanEvents();
        }

        /// <summary>
        /// Occurs when Updater state changes
        /// </summary>
        public static event UpdaterChangeEvent UpdaterChange;
        public delegate void UpdaterChangeEvent(object sender, EventArgs args);
        protected static void OnUpdaterChange()
        {
            CallOnUIThread(() => { UpdaterChange?.Invoke(null, EventArgs.Empty); }, false);
        }

        public static void TryToCleanEvents()
        {
            DownloadCanceled = null;
            RetryDownload = null;
            DownloadProgress = null;
            UpdateProgress = null;
            UpdateFailed = null;
            UpdateFinished = null;
            UpdaterChange = null;
        }

        static Updater()
        {
            GitHub_Asset = ProgramFileName;
            State = UpdaterState.Idle;
            Message = "Idle...";

            CleanFolder(ProgramFilePath, "*.EZold");

            for(int i = 0; i < 4; i++)
            {
                DownloadRetryCount = i;
                if (Directory.Exists(EZTempPath))
                    CleanFolder(EZTempPath);
            }
            DownloadRetryCount = 0;
        }

        /// <summary>
        /// Sync CheckUpdate
        /// </summary>
        /// <returns>True or False depending if Update is available</returns>
        public static bool CheckUpdate()
        {
            return Task.Run(CheckVersion).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sync CheckUpdate
        /// </summary>
        /// <param name="GitHubUser">GitHub User owner of the repository where update is located</param>
        /// <param name="GitHubRepository">GitHub Repositoy where update is located</param>
        /// <returns>True or False depending if Update is available</returns>
        public static bool CheckUpdate(string GitHubUser, string GitHubRepository)
        {
            GitHub_User = GitHubUser;
            GitHub_Repository = GitHubRepository;
            return Task.Run(CheckVersion).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Async CheckUpdate
        /// </summary>
        /// <returns>True or False depending if Update is available</returns>
        public static async Task<bool> CheckUpdateAsync()
        {
            return await CheckVersion();
        }

        /// <summary>
        /// Async CheckUpdate
        /// </summary>
        /// <param name="GitHubUser">GitHub User owner of the repository where update is located</param>
        /// <param name="GitHubRepository">GitHub Repositoy where update is located</param>
        /// <returns>True or False depending if Update is available</returns>
        public static async Task<bool> CheckUpdateAsync(string GitHubUser, string GitHubRepository)
        {
            GitHub_User = GitHubUser;
            GitHub_Repository = GitHubRepository;
            return await CheckVersion();
        }

        private static async Task<bool> CheckVersion()
        {
            _ = GitHub_User ?? throw new NullReferenceException(nameof(GitHub_User));
            _ = GitHub_Repository ?? throw new NullReferenceException(nameof(GitHub_Repository));

            bool API_Result = await API_Call();
            if (!API_Result) return false;

            State = UpdaterState.CheckingUpdate;
            Message = "Checking Update...";
            Log($"Release version: {ReleaseVersion} | Program version: {ProgramFileVersion}");

            bool UpdateAvailable = ReleaseVersion > ProgramFileVersion;
            if (UpdateAvailable)
            {
                State = UpdaterState.UpdateAvailable;
                Message = "Update available!";
            }
            else
            {
                State = UpdaterState.NoUpdateAvailable;
                Message = "No update available";
            }

            return UpdateAvailable;
        }

        private static async Task<bool> API_Call()
        {
            State = UpdaterState.Fetching;
            Message = "Fetching GitHub API";
            try
            {
                WebClient = new WebClient();
                WebClient.Headers.Add(HttpRequestHeader.UserAgent, GitHub_Repository);
                WebClient.Headers.Add(HttpRequestHeader.Accept, "application/vnd.github.v3+json");
                try
                {
                    var Result = await WebClient.DownloadStringTaskAsync(GitHub_ApiUrl);
                    APIResponse = JsonConvert.DeserializeObject<GithubResponse>(Result);
                }
                catch (WebException wex)
                {
                    using (var s = wex.Response.GetResponseStream())
                    {
                        var buffer = new byte[wex.Response.ContentLength];
                        var contentBytes = s.Read(buffer, 0, buffer.Length);
                        var content = Encoding.UTF8.GetString(buffer);
                        APIResponse = JsonConvert.DeserializeObject<GithubResponse>(content);
                    }
                }

                Log(APIResponse.Message == null, $"{GitHub_User}/{GitHub_Repository}: {APIResponse.Name}");
                Log(APIResponse.Message != null, $"{GitHub_User}/{GitHub_Repository}: {APIResponse.Message}");

                string TAG = "";
                MatchCollection MC = new Regex(@"(?<digit>\d+)").Matches(APIResponse.TagName);

                foreach (Match m in MC)
                {
                    CaptureCollection cc = m.Groups["digit"].Captures;
                    foreach (Capture c in cc)
                    {
                        TAG += c.Value;

                        if (m != MC[MC.Count - 1])
                            TAG += ".";
                    }
                }

                ReleaseName = APIResponse.Name;
                ReleaseBody = APIResponse.Body;
                ReleaseVersion = new Version(TAG);

                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Updates the application wheter or not there new update available
        /// </summary>
        /// <param name="OneActionForAll">Event you want to be called when anything in the Updater changes</param>
        public static void Update(Action<object, EventArgs> OneActionForAll)
        {
            if (OneActionForAll != null) UpdaterChange += new UpdaterChangeEvent(OneActionForAll);
            Update(null, null);
        }

        /// <summary>
        /// Updates the application wheter or not there new update available
        /// </summary>
        /// <param name="CanceledDownloadR">Event you want to be called when download is canceled</param>
        /// <param name="RetryDownloadR">Event you want to be called retrying download</param>
        /// <param name="DownloadProgressEventR">Event you want to be called when download is progressing
        ///     <code>
        ///     Example:
        ///     
        ///         DownloadProgress(object sender, EventArgs e){
        ///             //Example
        ///             progressBar.Value = Updater.DownloadPercentage;
        ///         }
        ///     </code>
        /// </param>
        /// <param name="UpdateFailedR">Event you want to be called when update had failed</param>
        /// <param name="UpdateFinishedR">Event you want to be called when update had finished (like a application restart to apply update)</param>
        public static void Update(
            Action<object, DownloadProgressChangedEventArgs> DownloadProgressEventR = null,
            Action<object, EventArgs> CanceledDownloadR = null,
            Action<object, EventArgs> RetryDownloadR = null,
            Action<object, EventArgs> UpdateProgressR = null,
            Action<object, EventArgs> UpdateFailedR = null,
            Action<object, EventArgs> UpdateFinishedR = null)
        {
            if(DownloadRetryCount >= 4)
            { 
                State = UpdaterState.Canceled;
                Message = "Download canceled";
                return; 
            }

            if(DownloadProgressEventR != null) DownloadProgress += new DownloadProgressEvent(DownloadProgressEventR);
            if(CanceledDownloadR != null) DownloadCanceled += new DownloadCanceledEvent(CanceledDownloadR);
            if(RetryDownloadR != null) RetryDownload += new RetryDownloadEvent(RetryDownloadR);
            if(UpdateProgressR != null) UpdateProgress += new UpdateProgressEvent(UpdateProgressR);
            if(UpdateFailedR != null) UpdateFailed += new UpdateFailedEvent(UpdateFailedR);
            if (UpdateFinishedR != null) UpdateFinished += new UpdateFinishedEvent(UpdateFinishedR);

            ProgressPercentage = 0;

            foreach (var Asset in APIResponse.Assets)
                if (Asset.Name == GitHub_Asset)
                    AssetSelected = Asset;

            if (AssetSelected.Name == null)
            {
                Log($"{GitHub_Asset}: not founded in GitHub release assets");
                OnDowloadCanceled();
                return;
            }

            Update();
        }

        private static void Update()
        {
            if (Directory.Exists(EZTempPath))
                CleanFolder(EZTempPath);
            else
                Directory.CreateDirectory(EZTempPath);

            WebClient.DownloadProgressChanged += new DownloadProgressChangedEventHandler(RestartTimer);
            WebClient.DownloadFileCompleted += new AsyncCompletedEventHandler(CompletedFile);

            try
            {
                WebClient.DownloadFileAsync(new Uri(AssetSelected.BrowserDownloadUrl), $"{EZTempPath}\\{AssetSelected.Name}");
                Timer.Change(5000, -1);
            }
            catch { }
        }

        private static void RestartTimer(object sender, DownloadProgressChangedEventArgs e)
        {
            ProgressPercentage = e.ProgressPercentage;
            Timer.Change(-1, -1);
            Timer.Change(5000, -1);

            OnDownloadProgress(e);
        }

        private static void RetryToDownload(object sender)
        {
            Timer.Change(-1, -1);
            WebClient.CancelAsync();
            WebClient.Dispose();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            WebClient = new WebClient();
            if (DownloadRetryCount >= 4)
            {
                Timer.Dispose();
                Timer = new Timer(Canceled, null, 2500, 0);
                return;
            }
            Timer.Change(5000, -1);

            DownloadRetryCount++;
            Update();

            OnRetryDownload();
        }

        private static void Canceled(object sender)
        {
            Timer.Change(-1, -1);
            WebClient.CancelAsync();
            WebClient.Dispose();

            OnDowloadCanceled();
        }

        private static void CompletedFile(object sender, AsyncCompletedEventArgs e)
        {
            if (!e.Cancelled && e.Error == null)
            {
                State = UpdaterState.Downloaded;
                Message = "Update downloaded";
                Timer.Change(-1, -1);
                Timer.Dispose();
                WebClient.CancelAsync();
                WebClient.Dispose();
                Log("Download completed");
                Timer = new Timer(InstallUpdate, null, 1000, -1);
            }
        }

        private static void InstallUpdate(object sender)
        {
            State = UpdaterState.Installing;
            Message = "Installing Update...";
            string AssetDownloaded = $"{EZTempPath}\\{AssetSelected.Name}";
            switch (Path.GetExtension(AssetSelected.Name))
            {
                case ".zip":
                    string AssetMoved = $"{EZTempPath}\\EZ-{AssetSelected.Name}";

                    File.Move(AssetDownloaded, AssetMoved);

                    ZipFile.ExtractToDirectory(AssetMoved, EZTempPath);
                    Log($"ZIP: {AssetDownloaded} extracted");
                    File.Delete(AssetMoved);
                    break;
            }

            OnUpdateProgress();

            try
            {
                Log("Installing files");
                MoveFilesInstall(EZTempPath, Path.GetDirectoryName(ProgramFile));
                OnUpdateFinished();
            }
            catch
            {
                try
                {
                    foreach (var item in MovedFileDir)
                    {
                        var FileDir = File.GetAttributes(item);
                        if (FileDir.HasFlag(FileAttributes.Directory))
                            Directory.Delete(item, true);
                        else
                            File.Delete(item);
                    }
                    MovedFileDir.Clear();

                    MoveFilesRevert(ProgramFilePath);

                    ProgressPercentage = UpdateNFiles = MovedFiles = 0;

                    CleanFolder(EZTempPath);

                    OnUpdateFailed();
                }
                catch { }
            }

        }

        private static void CleanFolder(string directory, string filepattern = "*")
        {
            DirectoryInfo Adir = new DirectoryInfo(directory);
            foreach (FileInfo file in Adir.GetFiles(filepattern))
                file.Delete();
            foreach (DirectoryInfo dir in Adir.GetDirectories())
            {
                if (filepattern != "*")
                    CleanFolder(dir.FullName, filepattern);
                else
                    dir.Delete(true);
            }
        }

        private static void MoveFilesInstall(string directory, string destDir)
        {
            DirectoryInfo Adir = new DirectoryInfo(directory);
            foreach (FileInfo file in Adir.GetFiles())
            {
                var newfile = $"{destDir}\\{file.Name}";
                if (File.Exists(newfile))
                    File.Move($"{newfile}", $"{newfile}.EZold");
                Log($"File: {file.FullName} -> {newfile}");
                file.MoveTo($"{newfile}");
                UpdateProgressCalc(directory);
                MovedFileDir.Add($"{newfile}");
            }
            foreach (DirectoryInfo dir in Adir.GetDirectories())
            {
                Log(Adir.FullName);
                var newdestdir = $"{destDir}\\{dir.Name}";
                if (Directory.Exists(newdestdir))
                {
                    Log($"Folder: Accessing {newdestdir}");
                    MoveFilesInstall(dir.FullName, newdestdir);
                }
                else
                {
                    Log($"Folder: Creating {newdestdir}");
                    Directory.CreateDirectory(newdestdir);
                    MoveFilesInstall(dir.FullName, newdestdir);
                    MovedFileDir.Add(newdestdir);
                }
            }
        }

        private static void MoveFilesRevert(string directory)
        {
            foreach (FileInfo file in new DirectoryInfo(directory).GetFiles("*.EZold"))
                file.MoveTo(file.FullName.Replace(".EZold", ""));
            foreach (DirectoryInfo dir in new DirectoryInfo(directory).GetDirectories())
                MoveFilesRevert(dir.FullName);
        }

        private static void UpdateProgressCalc(string directory)
        {
            if(UpdateNFiles == 0)
                UpdateNFiles = Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length + 1;
            MovedFiles++;
            ProgressPercentage = int.Parse(Math.Truncate((double)MovedFiles / (double)UpdateNFiles * 100).ToString());
            OnUpdateProgress();
        }

        private static void CallOnUIThread(Action action, bool async = true)
        {
            try
            {
                if (!async && GUI_Context != null)
                    GUI_Context.Send((o) =>
                    {
                        action();
                    }, null);
                else if (GUI_Context != null)
                    GUI_Context.Post((o) =>
                    {
                        action();
                    }, null);
                else
                    action();
            }
            catch (InvalidOperationException)
            {
                throw new Exception("For GUI events first establish Updater.GUI_Context to SynchronizationContext.Current");
            }
            catch (Exception)
            {
                throw;
            }
        }

        private static void Log(bool condition, string message)
        {
            if (condition)
                Log(message);
        }
        private static void Log(string message)
        {
            message = "EZ_Updater\t" + message;
            Trace.WriteLine(message);
            CallOnUIThread(() => { CustomLogger?.Invoke(message); });
        }
    }
}
