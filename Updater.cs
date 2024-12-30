using System.Net.Http.Headers;
using System.Text.Json;

namespace GttrcrGist
{
    public class Updater
    {
        public struct Result
        {
            public Version LocalVersion;
            public Version RemoteVersion;
            public Uri BrowserDownloadUrl;
        }

        public struct Release
        {
            public struct Asset
            {
                public string url { get; set; }
                public DateTime created_at { get; set; }
                public DateTime updated_at { get; set; }
                public string browser_download_url { get; set; }
            }

            public string url { get; set; }
            public string tag_name { get; set; }
            public string name { get; set; }
            public bool draft { get; set; }
            public bool prerelease { get; set; }
            public DateTime created_at { get; set; }
            public DateTime published_at { get; set; }
            public List<Asset> assets { get; set; }
        }

        private static Updater? _instance;
        private Thread? _updateCheckerThread;
        private static readonly object _lock = new();

        //Hide the NewVersionAvailableCallback
        public bool Hide { get; set; }

        //Execute just once
        public bool Once { get; set; }

        //LocalVersion is the version to check
        public string? LocalVersion { get; set; }

        //github repo name
        public string? RepoName { get; set; }

        //github organization name (RepoName's owner)
        public string? OrganizationName { get; set; }

        //github user name to identify the request
        public string? UserName { get; set; }

        //github token
        public string? Token { get; set; }

        //refresh time in minutes
        public int UpdateRefreshMinutes { get; set; }

        //allow prereleases
        public bool AllowPrerelease { get; set; }

        //callback to execute in case of an incoming new version
        public delegate void UpdateCheckerDelegate(Result checkResult);
        public UpdateCheckerDelegate? NewVersionAvailableCallback;

        //callback to for any error
        public delegate void UpdateCheckerErrorDelegate(string error);
        public UpdateCheckerErrorDelegate? ErrorCallback;

        private Updater()
        {
            Hide = false;
            Once = false;
            RepoName = null;
            OrganizationName = null;
            UserName = null;
            Token = null;
            UpdateRefreshMinutes = 1;
            AllowPrerelease = false;
        }

        public static Updater Get()
        {
            _instance ??= new();

            return _instance;
        }

        public static void Reset()
        {
            lock (_lock)
            {
                _instance = null;
            }
        }

        public Result? GetLatest()
        {
            UpdateCheckerDelegate? tmp = NewVersionAvailableCallback;
            NewVersionAvailableCallback = null;
            Result? result = Run();
            NewVersionAvailableCallback = tmp;
            return result;
        }

        public Result? Run()
        {
            try
            {
                HttpClient client = new() { BaseAddress = new Uri("https://api.github.com") };

                client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Updater.Check", "1.0"));
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Token", Token);
                HttpResponseMessage response = client.GetAsync("/repos/" + OrganizationName + "/" + RepoName + "/releases").Result;

                if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                {
                    try
                    {
                        // We might have hit the rate limit : we check the headers
                        var rateLimitReset = response.Headers.Contains("X-RateLimit-Reset")
                            ? Convert.ToInt64(response.Headers.GetValues("X-RateLimit-Reset").FirstOrDefault())
                            : (long?)null;

                        if (rateLimitReset.HasValue)
                            ErrorCallback?.Invoke($"GitHub Rate limit exceeded at {DateTime.Now:HH:mm}. Try again at {DateTimeOffset.FromUnixTimeSeconds(rateLimitReset.Value).LocalDateTime}.");
                        else
                            ErrorCallback?.Invoke("GitHub Access Forbidden");
                    }
                    catch (Exception ex)
                    {
                        ErrorCallback?.Invoke("GitHub API : " + ex.Message);
                    }
                }

                response.EnsureSuccessStatusCode();
                string content = response.Content.ReadAsStringAsync().Result;
                List<Release>? releases = JsonSerializer.Deserialize<List<Release>>(content);
                releases = releases?.OrderByDescending(x => x.published_at).ToList();
                releases = releases?.Where(x =>
                {
                    if (AllowPrerelease)
                        return true;
                    else if (!AllowPrerelease)
                        return !x.prerelease;

                    return false;
                }).ToList();
                Release latestRemote;
                if (releases?.Count > 0)
                    latestRemote = releases.FirstOrDefault();
                else
                    return null;

                Version remoteVersion = new(latestRemote.tag_name);
                Result result = new() { RemoteVersion = remoteVersion };

                //local
                if (!Version.TryParse(LocalVersion, out Version? localVersion))
                    throw new InvalidCastException(LocalVersion + " is not a valid Version string");
                result.LocalVersion = localVersion;

                //comparison
                int versionComparison = localVersion.CompareTo(remoteVersion);
                if (versionComparison < 0)
                {
                    //The version on GitHub is more up to date than this local release
                    string browserDownloadUrl = string.Empty;
                    if (latestRemote.assets.Count > 0)
                        browserDownloadUrl = latestRemote.assets[0].browser_download_url;

                    result.BrowserDownloadUrl = new(browserDownloadUrl);
                    NewVersionAvailableCallback?.Invoke(result);
                }
                else if (versionComparison > 0)
                {
                    //This local version is greater than the release version on GitHub.
                }
                else
                {
                    //This local Version and the Version on GitHub are equal.
                }

                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }

            return null;
        }

        public void Start()
        {
            if (_updateCheckerThread == null)
            {
                _updateCheckerThread = new(new ThreadStart(() =>
                {
                    try
                    {
                        do
                        {
                            if (!Hide && LocalVersion != null && RepoName != null && OrganizationName != null && UserName != null && NewVersionAvailableCallback != null)
                            {
                                Run();
                                Thread.Sleep(UpdateRefreshMinutes * 60 * 1000);
                            }
                        }
                        while (!Once);
                    }
                    catch { }
                }))
                { IsBackground = true };
                _updateCheckerThread.Start();
            }
        }
    }
}