using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace ChatBot_Test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageController : ControllerBase
    {
        private readonly string rapidApiKey = "bdd6d8d993msh3da56b5aa8ea4cep1943c1jsn3c7a14260135";
        private readonly string connectionString = "Server=MYSQL5046.site4now.net;Database=db_aa771b_botdb;Uid=aa771b_botdb;Pwd=admin123";

        [HttpPost]
        public async Task<IActionResult> PostImage([FromForm] ImageUploadModel model)
        {
            try
            {
                // Save the image temporarily
                var filePath = Path.GetTempFileName();
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await model.Image.CopyToAsync(stream);
                }

                // Extract text using OCR API
                var extractedText = await ExtractTextFromImage(filePath);

                // Create and save report
                var report = new Report { Description = extractedText };
                SaveReportToDatabase(report);

                return Ok("Text extracted and report saved successfully.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private async Task<string> ExtractTextFromImage(string imagePath)
        {
            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("X-RapidAPI-Key", rapidApiKey);
                client.DefaultRequestHeaders.Add("X-RapidAPI-Host", "ocr-extract-text.p.rapidapi.com");

                var content = new MultipartFormDataContent();
                content.Add(new StreamContent(new FileStream(imagePath, FileMode.Open)), "image", "image.jpg");

                using (var response = await client.PostAsync("https://ocr-extract-text.p.rapidapi.com/ocr", content))
                {
                    response.EnsureSuccessStatusCode();
                    var responseBody = await response.Content.ReadAsStringAsync();

                    // Parse the JSON response and extract the "text" field
                    var jsonResponse = JObject.Parse(responseBody);
                    var extractedText = jsonResponse["text"]?.ToString();

                    return extractedText;
                }
            }
        }

        private void SaveReportToDatabase(Report report)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "INSERT INTO Reports (Description) VALUES (@Description)";
                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@Description", report.Description);
                    cmd.ExecuteNonQuery();
                }
            }
        }

        [HttpGet("GetReports")]
        public IActionResult GetReports()
        {
            try
            {
                var reports = new List<Report>();

                using (var connection = new MySqlConnection(connectionString))
                {
                    connection.Open();

                    var query = "SELECT Description FROM Reports";
                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                reports.Add(new Report { Description = reader.GetString(0) });
                            }
                        }
                    }
                }

                return Ok(reports);
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }
    }

    public class ImageUploadModel
    {
        public Microsoft.AspNetCore.Http.IFormFile Image { get; set; }
    }

    public class Report
    {
        public string Description { get; set; }
    }
}
