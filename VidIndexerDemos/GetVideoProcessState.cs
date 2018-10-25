using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;

namespace VidIndexerDemos
{
    public static class GetVideoProcessState
    {
        //Video Indexer Settings
        static string apiUrl;
        static string accountId;
        static string location;
        static string apiKey;

        [FunctionName("GetVideoProcessState")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log,
            ExecutionContext context)
        {
            log.LogInformation("Request Recieved....");

            string videoId = req.Query["videoId"];

            if (videoId == null)
            {
                return new BadRequestObjectResult("Error: Please pass a video id on the query string");
            }

            //Get settings from config
            GetSettings(context);


            // create the http client
            var handler = new HttpClientHandler();
            handler.AllowAutoRedirect = false;
            var client = new HttpClient(handler);
            client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", apiKey);

            // obtain video access token used in subsequent calls
            log.LogInformation($"{apiUrl}/auth/{location}/Accounts/{accountId}/Videos/{videoId}/AccessToken");
            var videoAccessTokenRequestResult = await client.GetAsync($"{apiUrl}/auth/{location}/Accounts/{accountId}/Videos/{videoId}/AccessToken");
            var videoAccessToken = await videoAccessTokenRequestResult.Content.ReadAsStringAsync();
            videoAccessToken = videoAccessToken.Replace("\"", "");

            client.DefaultRequestHeaders.Remove("Ocp-Apim-Subscription-Key");

            // Get video download URL
            var videoIndexResult = await client.GetAsync($"{apiUrl}/{location}/Accounts/{accountId}/Videos/{videoId}/Index?accessToken={videoAccessToken}");
            var videoIndexData = await videoIndexResult.Content.ReadAsStringAsync();

            //Customize here
            dynamic videoIndexDeserialized = JsonConvert.DeserializeObject(videoIndexData);

            string output = "";

            foreach (var vid in videoIndexDeserialized.videos)
            {
                output += string.Format("Video: {0} State: {1} \n", vid.id.ToString(), vid.state.ToString());
            }

            return (ActionResult)new OkObjectResult($"{output}");
        }

        /// <summary>
        /// Loads all of the settings needed for the api calls to Video Indexer
        /// </summary>
        static void GetSettings(ExecutionContext context)
        {
            var config = new ConfigurationBuilder()
                .SetBasePath(context.FunctionAppDirectory)
                .AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
            apiUrl = config["viApiUrl"];
            accountId = config["viAccountID"];
            location = config["viRegion"];
            apiKey = config["viAPIKey"];
        }
    }
}
