using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Reddit;
using Reddit.Things;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using RedditStatsTracker.Models;

namespace RedditStatsTracker.Services
{
    /// <summary>
    /// RedditService class that handles fetching posts from Reddit and managing statistics.
    /// It manages Reddit API access tokens, tracks posts and users, and provides statistics.
    /// </summary>
    public class RedditService
    {
        private readonly IConfiguration _configuration;
        private readonly ILogger<RedditService> _logger;
        private readonly HttpClient _httpClient;
        private RedditClient _redditClient;
        private string _accessToken; // Current access token for Reddit API
        private DateTime _tokenExpiration; // Expiration time for the current access token

        // Dictionary to store the number of posts per user
        private readonly ConcurrentDictionary<string, int> _userPostCounts = new ConcurrentDictionary<string, int>();

        // Variable to store the post with the highest number of upvotes
        private RedditPost _postWithMostUpvotes;
        private readonly object _upvoteLock = new object(); // Lock for thread-safety when updating the top post

        /// <summary>
        /// Constructor that initializes the RedditService with configuration and logger.
        /// </summary>
        /// <param name="configuration">Application configuration, including Reddit API credentials.</param>
        /// <param name="logger">Logger for logging information and errors.</param>
        public RedditService(IConfiguration configuration, ILogger<RedditService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration;
            _httpClient = new HttpClient();
        }

        /// <summary>
        /// Fetches posts from a specific subreddit, updates statistics, and returns the posts as a list of RedditPost DTOs.
        /// </summary>
        /// <param name="subredditName">The name of the subreddit to fetch posts from.</param>
        /// <returns>A list of RedditPost DTOs representing the fetched posts.</returns>
        public async Task<List<RedditPost>> GetPostsFromSubredditAsync(string subredditName)
        {
            try
            {
                _logger.LogInformation("Attempting to fetch posts from subreddit: {subredditName}");

                // Ensure that a valid access token is available
                await EnsureAccessTokenAsync();

                // Initialize RedditClient with the access token
                _redditClient = new RedditClient(
                    accessToken: _accessToken,
                    appId: _configuration["Reddit:ClientId"],
                    appSecret: _configuration["Reddit:ClientSecret"],
                    userAgent: _configuration["Reddit:UserAgent"]
                );

                // Fetch the subreddit and its posts
                var subreddit = _redditClient.Subreddit(subredditName).About();
                var posts = subreddit?.Posts?.GetHot();

                // Check if any posts were found
                if (posts == null || posts.Count == 0)
                {
                    _logger.LogWarning("No posts found for subreddit: {subredditName}");
                    return new List<RedditPost>();
                }

                _logger.LogInformation("Successfully fetched posts from subreddit: {subredditName}");

                // Convert Reddit.Controllers.Post objects to RedditPost DTOs
                var postDtos = posts.Select(p => new RedditPost
                {
                    Title = p.Title,
                    Author = p.Author,
                    UpVotes = p.UpVotes
                }).ToList();

                // Update statistics based on the fetched posts
                UpdateStatistics(postDtos);

                return postDtos;
            }
            catch (Reddit.Exceptions.RedditNotFoundException ex)
            {
                _logger.LogError(ex, "Subreddit {subredditName} not found", subredditName);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching posts from subreddit: {subredditName}", subredditName);
                throw;
            }
        }

        /// <summary>
        /// Ensures that a valid access token is available. If the current token is expired or missing, it fetches a new one.
        /// </summary>
        private async Task EnsureAccessTokenAsync()
        {
            // Check if the token is missing or has expired
            if (string.IsNullOrEmpty(_accessToken) || DateTime.UtcNow >= _tokenExpiration)
            {
                _logger.LogInformation("Fetching new access token from Reddit API");

                // Prepare the client credentials for the token request
                var clientCredentials = Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_configuration["Reddit:ClientId"]}:{_configuration["Reddit:ClientSecret"]}"));

                var request = new HttpRequestMessage(HttpMethod.Post, "https://www.reddit.com/api/v1/access_token");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", clientCredentials);
                request.Headers.Add("User-Agent", _configuration["Reddit:UserAgent"]);

                var content = new FormUrlEncodedContent(new[]
                {
                    new KeyValuePair<string, string>("grant_type", "client_credentials")
                });

                request.Content = content;

                // Send the token request to Reddit
                var response = await _httpClient.SendAsync(request);

                // Check if the token request was successful
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogError($"Error fetching token: {response.StatusCode}, Reason: {response.ReasonPhrase}, Response: {responseBody}");
                    throw new HttpRequestException($"Token request failed with status {response.StatusCode} and reason '{response.ReasonPhrase}'");
                }

                // Deserialize the token response and store the access token and expiration time
                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenResponse = Newtonsoft.Json.JsonConvert.DeserializeObject<TokenResponse>(responseContent);

                _accessToken = tokenResponse.AccessToken;
                _tokenExpiration = DateTime.UtcNow.AddSeconds(tokenResponse.ExpiresIn - 60); // Set expiration 1 minute before actual expiry

                _logger.LogInformation("Successfully retrieved access token");
            }
        }

        /// <summary>
        /// Updates statistics based on the fetched posts, including tracking the user with the most posts and the post with the most upvotes.
        /// </summary>
        /// <param name="posts">The list of RedditPost DTOs representing the fetched posts.</param>
        private void UpdateStatistics(List<RedditPost> posts)
        {
            foreach (var post in posts)
            {
                // Update the post count for the post's author
                _userPostCounts.AddOrUpdate(post.Author, 1, (key, count) => count + 1);

                // Check if the current post has more upvotes than the current top post
                // Lock to ensure thread-safety when updating the top post
                lock (_upvoteLock)
                {
                    if (_postWithMostUpvotes == null || post.UpVotes > _postWithMostUpvotes.UpVotes)
                    {
                        _postWithMostUpvotes = post;
                    }
                }
            }
        }

        /// <summary>
        /// Fetches posts from multiple subreddits concurrently.
        /// </summary>
        /// <param name="subredditNames">A list of subreddit names to fetch posts from.</param>
        public async Task FetchPostsFromSubredditsAsync(List<string> subredditNames)
        {
            try
            {
                _logger.LogInformation("Attempting to fetch posts from multiple subreddits.");

                // Ensure that a valid access token is available
                await EnsureAccessTokenAsync();

                // Create a list of tasks to fetch posts from each subreddit concurrently
                var tasks = subredditNames.Select(subredditName => GetPostsFromSubredditAsync(subredditName)).ToList();

                // Wait for all subreddit fetch tasks to complete
                await Task.WhenAll(tasks);

                _logger.LogInformation("Successfully fetched posts from all subreddits.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while fetching posts from multiple subreddits.");
                throw;
            }
        }

        /// <summary>
        /// Retrieves the post with the most upvotes across all fetched posts.
        /// </summary>
        /// <returns>The RedditPost DTO representing the post with the most upvotes.</returns>
        public RedditPost GetPostWithMostUpvotes()
        {
            return _postWithMostUpvotes;
        }

        /// <summary>
        /// Retrieves the user with the most posts across all fetched posts.
        /// </summary>
        /// <returns>A KeyValuePair where the key is the username and the value is the number of posts.</returns>
        public KeyValuePair<string, int> GetUserWithMostPosts()
        {
            return _userPostCounts.OrderByDescending(x => x.Value).FirstOrDefault();
        }

       

    }
}
