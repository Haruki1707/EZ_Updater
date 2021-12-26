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
}