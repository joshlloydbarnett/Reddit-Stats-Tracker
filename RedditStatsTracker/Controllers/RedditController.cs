using Microsoft.AspNetCore.Mvc;
using RedditStatsTracker.Services;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace RedditStatsTracker.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RedditController : ControllerBase
    {
        private readonly RedditService _redditService;

        public RedditController(RedditService redditService)
        {
            _redditService = redditService;
        }

        // GET: api/reddit/posts
        // Fetch posts from multiple subreddits (supply subreddit names as a comma-separated list)
        [HttpGet("posts")]
        public async Task<IActionResult> FetchPostsFromSubreddits([FromQuery] string subreddits)
        {
            if (string.IsNullOrWhiteSpace(subreddits))
            {
                return BadRequest(new { Message = "No subreddits provided. Please provide a list of subreddit names." });
            }

            // Split the comma-separated list of subreddit names
            var subredditList = new List<string>(subreddits.Split(','));

            await _redditService.FetchPostsFromSubredditsAsync(subredditList);

            return Ok(new { Message = "Posts fetched from subreddits.", Subreddits = subredditList });
        }

        // GET: api/reddit/top-post
        // Get the post with the most upvotes across all subreddits
        [HttpGet("top-post")]
        public IActionResult GetPostWithMostUpvotes()
        {
            var post = _redditService.GetPostWithMostUpvotes();

            if (post == null)
            {
                return NotFound(new { Message = "No top post found yet. Please fetch subreddit posts first." });
            }

            return Ok(new { post.Title, post.Author, post.UpVotes });
        }

        // GET: api/reddit/top-user
        // Get the user with the most posts across all subreddits
        [HttpGet("top-user")]
        public IActionResult GetUserWithMostPosts()
        {
            var user = _redditService.GetUserWithMostPosts();

            if (string.IsNullOrEmpty(user.Key))
            {
                return NotFound(new { Message = "No top user found yet. Please fetch subreddit posts first." });
            }

            return Ok(new { userName = user.Key, postCount = user.Value });
        }
    }
}
