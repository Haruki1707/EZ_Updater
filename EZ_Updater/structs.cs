using Newtonsoft.Json;
using System.Collections.Generic;

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
        [JsonProperty("size")]
        public string Size { get; set; }
        [JsonProperty("browser_download_url")]
        public string BrowserDownloadUrl { get; set; }
    }

    public enum UpdaterState
    {
        CannotWriteOnDir,
        Idle,
        Fetching,
        RepoNotFound,
        RepoError,
        AssetNotFound,
        CheckingUpdate,
        NoUpdateAvailable,
        UpdateAvailable,
        Downloading,
        Retrying,
        Canceled,
        Downloaded,
        Installing,
        InstallFailed,
        Installed
    }

    public enum UpdaterShortState
    {
        Idle,
        Updating,
        Canceled,
        Installed
    }
}