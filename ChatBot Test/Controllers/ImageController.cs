using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net.Http;
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
        public async Task<IActionResult> PostImage([FromBody] ImageUrlModel model)
        {
            try
            {
                // Extraemos el texto de la imagen utilizando OCR - Extract text de rapidapi.com
                var extractedText = await ExtractTextFromImage(model.ImageUrl);

                // En este punto se da el diagnóstico mediante IA

                // Creamos un objeto de reporte y le asignamos el texto extraído a su campo descripción
                var report = new Report { Description = extractedText };
                SaveReportToDatabase(report);

                return Ok("El reporte fue creado con éxito.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Internal server error: {ex.Message}");
            }
        }

        private async Task<string> ExtractTextFromImage(string imageUrl)
        {
            using (var client = new HttpClient())
            {
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri($"https://ocr-extract-text.p.rapidapi.com/ocr?url={Uri.EscapeDataString(imageUrl)}"),
                    Headers =
                    {
                        { "X-RapidAPI-Key", rapidApiKey },
                        { "X-RapidAPI-Host", "ocr-extract-text.p.rapidapi.com" },
                    },
                };

                using (var response = await client.SendAsync(request))
                {
                    response.EnsureSuccessStatusCode();
                    var responseBody = await response.Content.ReadAsStringAsync();

                    // Parseamos el JSON de la respuesta para acceder a la propiedad text en este
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

    public class ImageUrlModel
    {
        public string ImageUrl { get; set; }
    }

    public class Report
    {
        public string Description { get; set; }
    }
}
