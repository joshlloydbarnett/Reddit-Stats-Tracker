using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace RedditStatsTracker.Services
{
    /// <summary>
    /// This service periodically reports statistics about Reddit posts and users.
    /// It runs as a background service in the application and handles rate limiting.
    /// </summary>
    public class StatisticsReportingService : BackgroundService
    {
        private readonly ILogger<StatisticsReportingService> _logger;
        private readonly RedditService _redditService;

        // Interval between each reporting cycle (default: 1 minute)
        private readonly TimeSpan _reportingInterval = TimeSpan.FromMinutes(1);

        // Variables to track API request counts and rate limits
        private int _requestCount = 0; // Number of requests made in the current interval
        private const int _maxRequests = 60; // Max number of requests allowed before rate limiting (example value)
        private TimeSpan _rateLimitResetInterval = TimeSpan.FromMinutes(10); // Time period after which rate limit resets (example value)
        private DateTime _lastResetTime = DateTime.Now; // Timestamp of the last rate limit reset

        /// <summary>
        /// Constructor that initializes the service with logger and RedditService.
        /// </summary>
        /// <param name="logger">Logger for recording information and errors.</param>
        /// <param name="redditService">Service for interacting with Reddit API.</param>
        public StatisticsReportingService(ILogger<StatisticsReportingService> logger, RedditService redditService)
        {
            _logger = logger;
            _redditService = redditService;
        }

        /// <summary>
        /// Main execution method for the background service.
        /// This method continuously runs, reporting statistics and handling rate limiting.
        /// </summary>
        /// <param name="stoppingToken">Cancellation token to handle service stopping.</param>
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Statistics Reporting Service started at: {time}", DateTimeOffset.Now);

            try
            {
                // Loop runs continuously until the service is stopped or cancelled
                while (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogInformation("Executing periodic reporting at: {time}", DateTimeOffset.Now);

                    // Check if the rate limit reset interval has passed and reset the request count if needed
                    if (DateTime.Now - _lastResetTime > _rateLimitResetInterval)
                    {
                        _logger.LogInformation("Resetting rate limit counter.");
                        _requestCount = 0; // Reset request count
                        _lastResetTime = DateTime.Now; // Update the reset time
                    }

                    // If the request count has reached the max allowed, pause the service until the reset interval passes
                    if (_requestCount >= _maxRequests)
                    {
                        _logger.LogWarning("Rate limit reached. Pausing requests until reset interval.");
                        var waitTime = _rateLimitResetInterval - (DateTime.Now - _lastResetTime);
                        await Task.Delay(waitTime, stoppingToken); // Wait for the remaining reset interval
                        _requestCount = 0; // Reset request count after waiting
                    }

                    // Report statistics (posts with most upvotes and users with most posts)
                    ReportStatistics();

                    // Increment the request count after making an API call
                    _requestCount++;

                    // Wait for the next reporting interval (e.g., 1 minute) before reporting again
                    await Task.Delay(_reportingInterval, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred in the Statistics Reporting Service.");
            }

            _logger.LogInformation("Statistics Reporting Service stopped at: {time}", DateTimeOffset.Now);
        }

        /// <summary>
        /// Reports the current statistics, such as the top post with the most upvotes
        /// and the user with the most posts, by logging the information.
        /// </summary>
        private void ReportStatistics()
        {
            _logger.LogInformation("Reporting statistics at: {time}", DateTimeOffset.Now);

            // Retrieve the post with the most upvotes
            var topPost = _redditService.GetPostWithMostUpvotes();
            // Retrieve the user with the most posts
            var topUser = _redditService.GetUserWithMostPosts();

            // Log information about the top post, if available
            if (topPost != null)
            {
                _logger.LogInformation($"Top Post: {topPost.Title} by {topPost.Author} with {topPost.UpVotes} upvotes.");
            }
            else
            {
                _logger.LogInformation("No top post available yet.");
            }

            // Log information about the top user, if available
            if (!string.IsNullOrEmpty(topUser.Key))
            {
                _logger.LogInformation($"Top User: {topUser.Key} with {topUser.Value} posts.");
            }
            else
            {
                _logger.LogInformation("No top user available yet.");
            }
        }
    }
}
