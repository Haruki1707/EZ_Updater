using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

[assembly: CLSCompliant(true)]
namespace EZ_Updater
{
    public class Updater
    {
        /// <summary>
        /// GitHub user owner of the repository where the release is located
        /// </summary>
        public static string GitHub_User { get; set; }

        /// <summary>
        /// GitHub repository where the release is located
        /// </summary>
        public static string GitHub_Repository { get; set; }

        /// <summary>
        /// GitHub asset to download
        /// </summary>
        /// <value>Default: OriginalFileName</value>
        public static string GitHub_Asset { get; set; }

        /// <returns>
        /// Count of how many retries of downloading are done (MAX: 4)
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

        ///<summary>
        ///Short State of updater, based on Idle, Updating, Canceled and Installed
        ///</summary>
        ///<returns>
        ///Updater shorted state
        ///</returns>
        public static UpdaterShortState ShortState { get; private set; }

        /// <returns>
        /// Update download percentage
        /// </returns>
        public static int ProgressPercentage { get; private set; }

        /// <returns>
        /// Updater message of UpdaterState
        /// </returns>
        public static string Message { get; private set; }

        /// <summary>
        /// Interfix that joins "EZ_Updater" with the message on logging.
        /// </summary>
        /// <value>\t</value>
        public static string LogInterfix = "\t";


        // Program File Attributes
        private static string ProgramFile = Process.GetCurrentProcess().MainModule.FileName;
        private static string ProgramFilePath => Path.GetDirectoryName(ProgramFile);
        /// <returns>
        /// Main program file name
        /// </returns>
        public static string ProgramFileName => Path.GetFileName(ProgramFile);
        /// <returns>
        /// Main program file version
        /// </returns>
        readonly public static Version ProgramFileVersion = new Version(FileVersionInfo.GetVersionInfo(ProgramFile).FileVersion);


        // GitHub API Attributes
        private static Uri GitHub_ApiUrl => new Uri($"https://api.github.com/repos/{GitHub_User}/{GitHub_Repository}/releases/latest");
        private static GithubResponse APIResponse;
        private static Asset AssetSelected = new Asset();


        // Updater Attributes
        /// <summary>
        /// Here goes the original file name of the program (without the extension). OBLIGATORY
        /// </summary>
        public static string OriginalFileName { get; set; }
        ///<summary>
        /// Indicates that current program directory cannot be written, because admin rights needed or folder writte access is forbiden
        ///</summary>
        public static bool CannotWriteOnDir { get; private set; }
        private static WebClient WebClient;
        private static int MovedFiles = 0;
        private static int UpdateNFiles = 0;
        private static List<string> MovedFileDir = new List<string>();
        private static Timer Timer = new Timer(RetryToDownload, null, -1, -1);
        private static string EZTempPath => Path.GetTempPath() + $"EZ_Updater{DownloadRetryCount}";
        private static bool API_Executed = false;
        private static bool keepfilename = false;

        /// <summary>
        /// Keeps Original File Name for the program
        /// </summary>
        /// <value>false</value>
        public static bool KeepOriginalFileName
        {
            get { return keepfilename; }
            set
            {
                keepfilename = value;

                if (keepfilename)
                {
                    _ = OriginalFileName ?? throw new NullReferenceException(nameof(OriginalFileName));
                    OriginalFileName = Path.GetFileNameWithoutExtension(OriginalFileName);
                    string OFN = OriginalFileName + Path.GetExtension(ProgramFileName);

                    if (OFN == ProgramFileName)
                        return;

                    if (File.Exists(OFN))
                        for (int i = 1; i < 100; i++)
                            if (!File.Exists(OriginalFileName + $" - old ({i})" + Path.GetExtension(ProgramFileName)))
                            {
                                File.Move(OFN, OriginalFileName + $" - old ({i})" + Path.GetExtension(ProgramFileName));
                                break;
                            }

                    File.Move(ProgramFileName, OFN);
                    Log($"File renamed: {ProgramFileName} -> {OFN}");
                    ProgramFile = $"{ProgramFilePath}\\{OFN}";
                }
            }
        }


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
        /// Occurs when the download is canceled after some attempts
        /// </summary>
        public static event DownloadCanceledEvent DownloadCanceled;
        public delegate void DownloadCanceledEvent(object sender, EventArgs args);
        protected static void OnDowloadCanceled()
        {
            State = UpdaterState.Canceled;
            ShortState = UpdaterShortState.Canceled;
            Message = "Download canceled";
            Log("Download canceled");
            CallOnUIThread(() => { DownloadCanceled?.Invoke(null, EventArgs.Empty); });
            OnUpdaterChange();
        }

        /// <summary>
        /// Occurs when the download is being retried
        /// </summary>
        public static event RetryDownloadEvent RetryDownload;
        public delegate void RetryDownloadEvent(object sender, EventArgs args);
        protected static void OnRetryDownload()
        {
            State = UpdaterState.Retrying;
            ShortState = UpdaterShortState.Updating;
            Message = $"Retrying download... {DownloadRetryCount}/4";
            Log($"Retrying download {DownloadRetryCount}/4");
            CallOnUIThread(() => { RetryDownload?.Invoke(null, EventArgs.Empty); });
            OnUpdaterChange();
        }

        /// <summary>
        /// Occurs when the download has progressed
        /// </summary>
        public static event DownloadProgressEvent DownloadProgress;
        public delegate void DownloadProgressEvent(object sender, DownloadProgressChangedEventArgs args);
        protected static void OnDownloadProgress(DownloadProgressChangedEventArgs e)
        {
            Log(State != UpdaterState.Downloading, $"Downloading: {AssetSelected.Name} | {AssetSelected.BrowserDownloadUrl}");
            State = UpdaterState.Downloading;
            ShortState = UpdaterShortState.Updating;
            Message = "Downloading update...";
            CallOnUIThread(() => { DownloadProgress?.Invoke(null, e); });
            OnUpdaterChange();
        }

        /// <summary>
        /// Occurs when the installation has progressed
        /// </summary>
        public static event UpdateProgressEvent UpdateProgress;
        public delegate void UpdateProgressEvent(object sender, EventArgs args);
        protected static void OnUpdateProgress()
        {
            State = UpdaterState.Installing;
            ShortState = UpdaterShortState.Updating;
            CallOnUIThread(() => { UpdateProgress?.Invoke(null, EventArgs.Empty); });
            OnUpdaterChange();
        }

        /// <summary>
        /// Occurs when the installation of the update fails
        /// </summary>
        public static event UpdateFailedEvent UpdateFailed;
        public delegate void UpdateFailedEvent(object sender, EventArgs args);
        protected static void OnUpdateFailed()
        {
            Log("Update Failed! restoring backup files");
            State = UpdaterState.InstallFailed;
            ShortState = UpdaterShortState.Canceled;
            Message = "Installation failed";
            CallOnUIThread(() => { UpdateFailed?.Invoke(null, EventArgs.Empty); });
            OnUpdaterChange();
        }

        /// <summary>
        /// Occurs when the updating process is finished. You should restart your application.
        /// </summary>
        public static event UpdateFinishedEvent UpdateFinished;
        public delegate void UpdateFinishedEvent(object sender, EventArgs args);
        protected static void OnUpdateFinished()
        {
            State = UpdaterState.Installed;
            ShortState = UpdaterShortState.Installed;
            Message = "Update installed!";
            Log("Update installed!");
            OnUpdaterChange();
            Thread.Sleep(250);
            CallOnUIThread(() => { UpdateFinished?.Invoke(null, EventArgs.Empty); }, false);
            TryToCleanEvents();
        }

        /// <summary>
        /// Occurs when the Updater state changes
        /// </summary>
        public static event UpdaterChangeEvent UpdaterChange;
        public delegate void UpdaterChangeEvent(object sender, EventArgs args);
        protected static void OnUpdaterChange()
        {
            CallOnUIThread(() => { UpdaterChange?.Invoke(null, EventArgs.Empty); }, false);
        }

        private static void CanceledSomething()
        {
            CallOnUIThread(() => { DownloadCanceled?.Invoke(null, EventArgs.Empty); });
            CallOnUIThread(() => { UpdateFailed?.Invoke(null, EventArgs.Empty); });
            OnUpdaterChange();
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
            CannotWriteOnDir = false;
            State = UpdaterState.Idle;
            ShortState = UpdaterShortState.Idle;
            Message = "Idle...";

            try
            {
                File.Create("UpdaterWriteTest.EZ").Close();
                File.Delete("UpdaterWriteTest.EZ");

                for (int i = 0; i < 4; i++)
                {
                    DownloadRetryCount = i;
                    if (Directory.Exists(EZTempPath))
                        CleanAfterUpdate(EZTempPath, ProgramFilePath, "*.EZold");
                }
            }
            catch (UnauthorizedAccessException)
            {
                CannotWriteOnDir = true;
                State = UpdaterState.CannotWriteOnDir;
                ShortState = UpdaterShortState.Canceled;
                Message = $"{OriginalFileName} has an update available, but cannot write on current folder. Please consider moving {OriginalFileName} to another location or starting it with Adminitrator Rights";
                Log("User has not write access on current directory: {ProgramFilePath}. Consider moving.");
            }

            DownloadRetryCount = 0;
        }

        /// <summary>
        /// Sync CheckUpdate
        /// </summary>
        /// <returns>True or False depending if an update is available</returns>
        public static bool CheckUpdate()
        {
            return Task.Run(CheckVersion).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Sync CheckUpdate
        /// </summary>
        /// <param name="GitHubUser">GitHub user owner of the repository where the update is located</param>
        /// <param name="GitHubRepository">GitHub repository where the update is located</param>
        /// <returns>True or False depending if an update is available</returns>
        public static bool CheckUpdate(string GitHubUser, string GitHubRepository)
        {
            GitHub_User = GitHubUser;
            GitHub_Repository = GitHubRepository;
            return Task.Run(CheckVersion).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Async CheckUpdate
        /// </summary>
        /// <returns>True or False depending if an update is available</returns>
        public static async Task<bool> CheckUpdateAsync()
        {
            return await CheckVersion();
        }

        /// <summary>
        /// Async CheckUpdate
        /// </summary>
        /// <param name="GitHubUser">GitHub user owner of the repository where the update is located</param>
        /// <param name="GitHubRepository">GitHub repositoy where the update is located</param>
        /// <returns>True or False depending if an update is available</returns>
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
            _ = OriginalFileName ?? throw new NullReferenceException(nameof(OriginalFileName));

            bool API_Executed = await API_Call();
            if (!API_Executed) return false;

            State = UpdaterState.CheckingUpdate;
            Message = "Checking Update...";
            Log($"Release version: {ReleaseVersion} | Program version: {ProgramFileVersion}");

            bool UpdateAvailable = ReleaseVersion > ProgramFileVersion;
            if (State == UpdaterState.RepoNotFound || State == UpdaterState.RepoError)
                return false;
            if (UpdateAvailable)
            {
                State = UpdaterState.UpdateAvailable;
                ShortState = UpdaterShortState.Idle;
                Message = "Update available!";
            }
            else
            {
                State = UpdaterState.NoUpdateAvailable;
                ShortState = UpdaterShortState.Idle;
                Message = "No update available";
            }

            Log(Message);

            return UpdateAvailable;
        }

        private static async Task<bool> API_Call()
        {
            State = UpdaterState.Fetching;
            Message = "Fetching GitHub API";
            try
            {
                using (HttpClient jsonClient = new HttpClient())
                {
                    jsonClient.DefaultRequestHeaders.Add("User-Agent", "EZ-Updater/0.5");
                    jsonClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");

                    var result = await jsonClient.GetAsync(GitHub_ApiUrl);
                    APIResponse = JsonConvert.DeserializeObject<GithubResponse>(await result.Content.ReadAsStringAsync());
                }

                Log(APIResponse.Message == null, $"{GitHub_User}/{GitHub_Repository}: {APIResponse.Name}");
                Log(APIResponse.Message != null, $"{GitHub_User}/{GitHub_Repository}: {APIResponse.Message}");

                if (APIResponse.Message == "Not Found")
                {
                    State = UpdaterState.RepoNotFound;
                    ShortState = UpdaterShortState.Canceled;
                    Message = "Repository not found";
                    return false;
                }
                else if (APIResponse.Message != null)
                {
                    State = UpdaterState.RepoError;
                    ShortState = UpdaterShortState.Canceled;
                    Message = "Repository error: " + APIResponse.Message;
                    return false;
                }

                if (APIResponse.Name == null)
                    APIResponse.Name = APIResponse.TagName;

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
        /// Updates the application whether or not there is a new update available
        /// </summary>
        /// <param name="OneActionForAll">Event you want to be called when UpdaterState changes</param>
        public static void Update(Action<object, EventArgs> OneActionForAll)
        {
            if (OneActionForAll != null) UpdaterChange += new UpdaterChangeEvent(OneActionForAll);
            Update();
        }

        /// <summary>
        /// Updates the application whether or not there is a new update available
        /// </summary>
        /// <param name="CanceledDownloadR">Event you want to be called when the download is canceled</param>
        /// <param name="RetryDownloadR">Event you want to be called when retrying download</param>
        /// <param name="DownloadProgressEventR">Event you want to be called when the download is progressing
        ///     <code>
        ///     Example:
        ///     
        ///         DownloadProgress(object sender, EventArgs e){
        ///             //Example
        ///             progressBar.Value = Updater.DownloadPercentage;
        ///         }
        ///     </code>
        /// </param>
        /// <param name="UpdateFailedR">Event you want to be called when the update had failed</param>
        /// <param name="UpdateFinishedR">Event you want to be called when the update had finished (like an application restart to apply the update)</param>
        public static async void Update(
            Action<object, DownloadProgressChangedEventArgs> DownloadProgressEventR = null,
            Action<object, EventArgs> CanceledDownloadR = null,
            Action<object, EventArgs> RetryDownloadR = null,
            Action<object, EventArgs> UpdateProgressR = null,
            Action<object, EventArgs> UpdateFailedR = null,
            Action<object, EventArgs> UpdateFinishedR = null)
        {
            if (!API_Executed)
                API_Executed = await API_Call();

            if (State == UpdaterState.RepoNotFound || State == UpdaterState.RepoError)
            {
                CanceledSomething();
                return;
            }

            if (CannotWriteOnDir)
            {
                State = UpdaterState.CannotWriteOnDir;
                ShortState = UpdaterShortState.Canceled;
                Message = $"{OriginalFileName} has an update available, but cannot write on current folder. Please consider moving {OriginalFileName} to another location or starting it with Adminitrator Rights";
                CanceledSomething();
                return;
            }

            if (DownloadRetryCount >= 4)
            {
                State = UpdaterState.Canceled;
                ShortState = UpdaterShortState.Canceled;
                Message = "Download canceled";
                CanceledSomething();
                return;
            }

            if (DownloadProgressEventR != null) DownloadProgress += new DownloadProgressEvent(DownloadProgressEventR);
            if (CanceledDownloadR != null) DownloadCanceled += new DownloadCanceledEvent(CanceledDownloadR);
            if (RetryDownloadR != null) RetryDownload += new RetryDownloadEvent(RetryDownloadR);
            if (UpdateProgressR != null) UpdateProgress += new UpdateProgressEvent(UpdateProgressR);
            if (UpdateFailedR != null) UpdateFailed += new UpdateFailedEvent(UpdateFailedR);
            if (UpdateFinishedR != null) UpdateFinished += new UpdateFinishedEvent(UpdateFinishedR);

            ProgressPercentage = 0;

            GitHub_Asset = GitHub_Asset ?? (OriginalFileName + Path.GetExtension(ProgramFileName));

            foreach (var Asset in APIResponse.Assets)
                if (Asset.Name == GitHub_Asset)
                    AssetSelected = Asset;

            if (AssetSelected.Name == null)
            {
                State = UpdaterState.AssetNotFound;
                ShortState = UpdaterShortState.Canceled;
                Message = $"{GitHub_User}/{GitHub_Repository}: {GitHub_Asset} not founded in GitHub releases assets";

                Log($"{GitHub_Asset}: not founded in GitHub release assets");
                CanceledSomething();
                return;
            }

            WebClient = new WebClient();
            DoUpdate();
        }

        private static void DoUpdate()
        {
            if (Directory.Exists(EZTempPath))
                CleanFolder(EZTempPath);

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
            DoUpdate();

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
                ShortState = UpdaterShortState.Updating;
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
            ShortState = UpdaterShortState.Updating;
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

            if (!keepfilename && File.Exists(EZTempPath + $"\\{OriginalFileName}{Path.GetExtension(ProgramFileName)}"))
                File.Move(EZTempPath + $"\\{OriginalFileName}{Path.GetExtension(ProgramFileName)}", EZTempPath + $"\\{ProgramFileName}");

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
                        {
                            File.Delete(item);
                            File.Move(item + ".EZold", item);
                        }
                    }
                    MovedFileDir.Clear();

                    ProgressPercentage = UpdateNFiles = MovedFiles = 0;

                    CleanFolder(EZTempPath);
                }
                catch { }

                OnUpdateFailed();
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

            if (Adir.GetFiles().Length < 1)
                Adir.Delete(true);
        }

        private static void CleanAfterUpdate(string EZdirectory, string directory, string filepattern = "*")
        {
            DirectoryInfo Adir = new DirectoryInfo(EZdirectory);
            foreach (DirectoryInfo dir in Adir.GetDirectories())
                if (Directory.Exists(directory + dir.FullName.Replace(EZdirectory, "")))
                    CleanFolder(directory + dir.FullName.Replace(EZdirectory, ""), filepattern);
            DirectoryInfo Bdir = new DirectoryInfo(directory);
            foreach (FileInfo file in Bdir.GetFiles(filepattern))
                file.Delete();

            CleanFolder(EZdirectory);
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

        private static void UpdateProgressCalc(string directory)
        {
            if (UpdateNFiles == 0)
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

        private static void Log(string message)
        {
            message = "EZ_Updater" + LogInterfix + message;
            Trace.WriteLine(message);
            CallOnUIThread(() => { CustomLogger?.Invoke(message); });
        }

        private static void Log(bool condition, string message)
        {
            if (condition)
                Log(message);
        }
    }
}
