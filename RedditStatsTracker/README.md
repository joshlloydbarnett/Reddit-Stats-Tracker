Reddit Stats Tracker
Overview
Reddit Stats Tracker is a .NET 6/7 application that consumes posts from specified subreddits, tracks statistics on the most upvoted posts, and identifies the user with the most posts. The application can fetch posts from multiple subreddits concurrently and provides API endpoints to report the statistics.

Requirements
.NET 6/7 SDK
Visual Studio 2022 (or any preferred IDE)
Reddit API credentials (Client ID, Client Secret, User Agent)
Installation
Clone the repository:

git clone https://github.com/Reddit-Stats-Tracker/reddit-stats-tracker.git
Open the project in Visual Studio by opening the .sln file.

Install the necessary NuGet packages. Visual Studio should automatically restore the NuGet packages when the project is opened. If not, right-click on the solution and choose Restore NuGet Packages.

Update the appsettings.json file with your Reddit API credentials. Go to the Reddit App Management page and create an application. Copy the ClientId and ClientSecret, then update the appsettings.json file as follows:

json
Copy code
{
  "Reddit": {
    "ClientId": "YourClientId",
    "ClientSecret": "YourClientSecret",
    "UserAgent": "YourUserAgent"
  }
}
Running the Application
Open the project in Visual Studio.
Start the application using Ctrl + F5 or by pressing the Run button.
The application will start on https://localhost:7205 (or the configured URL).
Testing the API Endpoints
You can test the API endpoints using Postman or any other HTTP client. The API provides the following endpoints:

GET /api/reddit/posts?subreddits={subreddits}: Fetches posts from the specified subreddits. Example: /api/reddit/posts?subreddits=gaming,technology
GET /api/reddit/top-post: Retrieves the post with the most upvotes.
GET /api/reddit/top-user: Retrieves the user with the most posts.
How the Application Works
The application periodically fetches posts from the specified subreddits using the Reddit API and tracks statistics such as the most upvoted post and the user with the most posts. Rate limiting is handled by tracking the number of requests made and waiting for the rate limit to reset if necessary. The application periodically logs the statistics to the console and provides them via API endpoints.

Known Issues and Limitations
Reddit imposes rate limits on API requests. The application attempts to handle these limits, but high request volumes may still result in delays. Additionally, concurrent fetching of posts from multiple subreddits may impact performance depending on the number of subreddits and posts being processed.

Testing the Application
To test the application, use Postman, cURL, or any other HTTP client to test the API endpoints. You can view logs in the console to monitor the application’s activity, including statistics reporting and rate limit handling.

Contributing
Feel free to fork this repository, submit issues, or create pull requests. Contributions are welcome!

License
This project is licensed under the MIT License - see the LICENSE file for details.