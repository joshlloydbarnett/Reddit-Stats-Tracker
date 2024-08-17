namespace RedditStatsTracker.Models
{
    /// <summary>
    /// Helper class for deserializing the token response from the Reddit API.
    /// </summary>
    public class TokenResponse
    {
        [Newtonsoft.Json.JsonProperty("access_token")]
        public string AccessToken { get; set; }

        [Newtonsoft.Json.JsonProperty("expires_in")]
        public int ExpiresIn { get; set; }
    }
}
