using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace fitbit.toggl.backsync
{
    class Program
    {
        static void Main(string[] args)
        {
            // SEE: https://jcpretorius.com/post/2018/automatically-sync-fitbit-workouts-to-toggl

            // SECRETS:
            var FITBIT_API_TOKEN = "your fitbit api token here";
            var TOGGL_API_TOKEN = "your toggl api key here";
            int TOGGL_PROJECT_ID = 123456; //<- your toggl project id there
            
            // Client for fitbit api
            using (HttpClient fitbitClient = new HttpClient())
            {
                // Default Fitbit headers
                fitbitClient.BaseAddress = new Uri("https://api.fitbit.com/1/user/-/activities/list.json");
                fitbitClient.DefaultRequestHeaders.Accept.Clear();
                fitbitClient.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
                fitbitClient.DefaultRequestHeaders.Add("Authorization", "Bearer " + FITBIT_API_TOKEN);

                // Client for Toggl Api
                using (var togglClient = new HttpClient())
                {
                    // Default Toggl Headers
                    togglClient.BaseAddress = new Uri("https://www.toggl.com/api/v8/time_entries");
                    togglClient.DefaultRequestHeaders.Accept.Clear();
                    togglClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                    var credentials = Convert.ToBase64String(Encoding.ASCII.GetBytes(TOGGL_API_TOKEN + ":api_token"));
                    togglClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", credentials);

                    var offset = 0;
                    // API rate limit is 150 calls per hour, 2800 offset should be safely in that
                    while (offset <= 2800)
                    {
                        // Get fitbit data
                        try
                        {
                            Console.WriteLine($"Getting workouts for offset {offset}");

                            // Update the URL
                            var apiUrl = $"https://api.fitbit.com/1/user/-/activities/list.json?afterDate=2018-01-01&sort=desc&logType=manual&offset={offset}&limit=20";

                            // Hit fitbit API
                            HttpResponseMessage response = fitbitClient.GetAsync(apiUrl).Result;
                            if (response.IsSuccessStatusCode)
                            {
                                // get the data
                                dynamic json = Newtonsoft.Json.JsonConvert.DeserializeObject(response.Content.ReadAsStringAsync().Result);

                                // Check for results
                                if (json != null && json.activities != null)
                                {
                                    // Loop results
                                    foreach (var workout in json.activities)
                                    {
                                        try
                                        {
                                            // WORKING WITH THE DATE OBJECTS:
                                            // TOGGL wants this: 2012-02-16T15:35:47+02:00
                                            // we get this from FITBIT: 02/16/2012 15:35:47

                                            // Create the object to send to toggl
                                            var task = new
                                            {
                                                time_entry = new
                                                {
                                                    description = (string)workout.activityName,
                                                    duration = (int)(workout.duration / 1000),
                                                    start = DateTime.ParseExact((string)workout.startTime, "MM/dd/yyyy HH:mm:ss", null).ToUniversalTime().ToString("O"),
                                                    pid = TOGGL_PROJECT_ID,
                                                    created_with = "API"
                                                }
                                            };

                                            // HTTP POST
                                            var content = new StringContent(JsonConvert.SerializeObject(task), Encoding.UTF8, "application/json");
                                            var loopRes = togglClient.PostAsync("", content).Result;
                                            if (loopRes.IsSuccessStatusCode)
                                            {
                                                Console.WriteLine($"\tadded: {workout.activityName} {workout.startTime}");
                                            }
                                        }
                                        catch
                                        {
                                            Console.WriteLine("error adding a workout");
                                        }

                                        // all done with this workout, sleep for 1sec (Toggl API recommendation)
                                        Thread.Sleep(1100);
                                    }
                                }
                                else
                                {
                                    Console.WriteLine("No more data to process");
                                    offset = 5000;
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error on getting fitbit api data : {ex}");
                            offset = 5000;
                        }

                        // All done with these 20 workout results
                        Thread.Sleep(1100);
                        offset = offset + 20;
                    }
                }

                Console.WriteLine("Done and dusted.");
                Console.WriteLine("https://jcpretorius.com/post/2018/automatically-sync-fitbit-workouts-to-toggl");
                Console.ReadLine();
            }
        }
    }
}
