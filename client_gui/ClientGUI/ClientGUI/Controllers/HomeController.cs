﻿using ClientGUI.Models;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Npgsql;
using System.Diagnostics;
using System.Drawing;
using System.Text;

namespace ClientGUI.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        private string SENTIMENT_SOURCE = @"http://host.docker.internal:8000/analyze"; //@"https://localhost:8000/analyze";
        private string connString = "Server=host.docker.internal;Port=5432;Database=DataAnalysis;User Id=root;Password=CSCI5400;";

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            List<SentimentModel> sentiments = new List<SentimentModel>();

            NpgsqlConnection conn = new NpgsqlConnection(connString);
            conn.Open();
            string query = "SELECT * FROM SentimentAnalysis";
            NpgsqlCommand cmd = new NpgsqlCommand(query, conn);
            
            using (var rdr = cmd.ExecuteReader())
            {
                while (rdr.Read())
                {
                    object[] vals = new object[rdr.FieldCount];
                    rdr.GetValues(vals);
                    sentiments.Add(
                        new SentimentModel
                        {
                            Id = (int)vals[0],
                            Timestamp = (DateTime)vals[1],
                            TextSearched = (string)vals[2],
                            SentimentResult = (string)vals[3],
                            PercentageScore = (double)vals[4]
                        });
                }
            }
            conn.Close();
            //Need to have database set up - for now, I'll just hardcode some in
            /* 
            using (var httpClient = new HttpClient())
            {
                using (var response = await httpClient.GetAsync(DATABASE_SOURCE))
                {
                    string apiResponse = await response.Content.ReadAsStringAsync();

                    sentiments = JsonConvert.DeserializeObject<List<SentimentModel>>(apiResponse);
                }
            }
            */

            //Dummy sentiment data
            //sentiments.Add(new SentimentModel { Id = 1, Timestamp = new DateTime(2023, 2, 21, 20, 28, 0), TextSearched = "example", SentimentResult = "postive", PercentageScore = 0.23});
            //sentiments.Add(new SentimentModel { Id = 2, Timestamp = new DateTime(2023, 2, 21, 20, 29, 0), TextSearched = "test", SentimentResult = "negative", PercentageScore = 0.57});
            //sentiments.Add(new SentimentModel { Id = 3, Timestamp = new DateTime(2023, 2, 21, 20, 30, 0), TextSearched = "another", SentimentResult = "neutral", PercentageScore = 0.98});

            return View(sentiments);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Create(SentenceModel s)
        {
            System.Diagnostics.Debug.WriteLine($"Sentence: {s.Sentence}");

            using (var httpClient = new HttpClient())
            {
                //Package up the sentence to send
                //StringContent content = new StringContent(s.Sentence, Encoding.UTF8, "application/json");
                StringContent content = new StringContent(JsonConvert.SerializeObject(s), Encoding.UTF8, "application/json");

                //Send it to the API and ask to analyze
                using (var response = await httpClient.PostAsync(SENTIMENT_SOURCE, content))
                {
                    System.Diagnostics.Debug.WriteLine($"Response: {response.ToString()}");

                    NpgsqlConnection conn = new NpgsqlConnection(connString);
                    conn.Open();

                    string query = "INSERT INTO SentimentAnalysis " +
                           "(TimeStamp, Text, SentimentScore, SentimentPercentage) " +
                           "VALUES (@TimeStamp, @Text, @SentimentScore, @SentimentPercentage);";

                    NpgsqlCommand cmd = new NpgsqlCommand(query, conn);
                    cmd.Parameters.AddWithValue("@TimeStamp", DateTime.Now);
                    cmd.Parameters.AddWithValue("@Text", s.Sentence);
                    cmd.Parameters.AddWithValue("@SentimentScore", "test");
                    cmd.Parameters.AddWithValue("@SentimentPercentage", 1.0);

                    try
                    {
                        cmd.ExecuteNonQuery();
                    }
                    catch (NpgsqlException e)
                    {
                        System.Diagnostics.Debug.WriteLine($"Failed to create in DB: {e.Message}");
                    }

                    conn.Close();

                    /*
                    //Receive analysis back + package to send to DB
                    string apiResponse = await response.Content.ReadAsStringAsync();
                    StringContent content2 = new StringContent(JsonConvert.SerializeObject(apiResponse), Encoding.UTF8, "application/json");

                    //Send the response to the DB
                    using (var response2 = await httpClient.PostAsync(DATABASE_SOURCE, content2))
                    {
                        string apiResponse2 = await response.Content.ReadAsStringAsync();
                    }
                    */
                }
            }

            //Then, we want to go back to Index
            return RedirectToAction("Index");
        }
    }
}