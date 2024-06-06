using Microsoft.AspNetCore.Mvc;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace ChatBot_Test.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ImageController : ControllerBase
    {
        private readonly string rapidApiKey = "bdd6d8d993msh3da56b5aa8ea4cep1943c1jsn3c7a14260135";
        private readonly string connectionString = "Server=MYSQL5046.site4now.net;Database=db_aa771b_chatdb;Uid=aa771b_chatdb;Pwd=admin123";

        [HttpPost]
        public async Task<IActionResult> PostImage([FromBody] ImageUrlsModel model)
        {
            try
            {
                if (model == null || (string.IsNullOrWhiteSpace(model.ImageUrl1) && string.IsNullOrWhiteSpace(model.ImageUrl2) && string.IsNullOrWhiteSpace(model.ImageUrl3)))
                {
                    return BadRequest("No se proporcionaron URLs de imágenes válidas.");
                }

                // Crear el reporte con los datos proporcionados
                int reportId = await CreateReport(model.Contact, model.Description);

                // Procesar cada URL de imagen si no están vacías
                if (!string.IsNullOrWhiteSpace(model.ImageUrl1))
                {
                    await ProcessImageUrl(reportId, model.ImageUrl1);
                }

                if (!string.IsNullOrWhiteSpace(model.ImageUrl2))
                {
                    await ProcessImageUrl(reportId, model.ImageUrl2);
                }

                if (!string.IsNullOrWhiteSpace(model.ImageUrl3))
                {
                    await ProcessImageUrl(reportId, model.ImageUrl3);
                }

                return Ok("Las imágenes han sido procesadas correctamente.");
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"Error interno del servidor: {ex.Message}");
            }
        }


        private async Task<int> CreateReport(string contact, string description)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "INSERT INTO Report (Contact, Description) VALUES (@Contact, @Description); SELECT LAST_INSERT_ID();";
                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@Contact", string.IsNullOrWhiteSpace(contact) ? DBNull.Value : (object)contact);
                    cmd.Parameters.AddWithValue("@Description", string.IsNullOrWhiteSpace(description) ? DBNull.Value : (object)description);
                    return Convert.ToInt32(cmd.ExecuteScalar());
                }
            }
        }


        private async Task ProcessImageUrl(int reportId, string imageUrl)
        {
            if (!string.IsNullOrWhiteSpace(imageUrl))
            {
                // Extraer texto de la imagen
                var extractedText = await ExtractTextFromImage(imageUrl);

                // Guardar el texto extraído en la base de datos
                SaveScreenshotToDatabase(reportId, imageUrl, extractedText);
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

                    // Parsear el JSON de la respuesta para obtener el texto extraído
                    var jsonResponse = JObject.Parse(responseBody);
                    var extractedText = jsonResponse["text"]?.ToString();

                    return extractedText;
                }
            }
        }

        private void SaveScreenshotToDatabase(int reportId, string imageUrl, string transcription)
        {
            using (var connection = new MySqlConnection(connectionString))
            {
                connection.Open();

                var query = "INSERT INTO Screenshot (Url, Transcription, ReportId) VALUES (@Url, @Transcription, @ReportId)";
                using (var cmd = new MySqlCommand(query, connection))
                {
                    cmd.Parameters.AddWithValue("@Url", imageUrl);
                    cmd.Parameters.AddWithValue("@Transcription", transcription);
                    cmd.Parameters.AddWithValue("@ReportId", reportId);
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

                    var query = @"
                SELECT 
                    r.Id AS ReportId,
                    r.Description AS ReportDescription,
                    r.State AS ReportState,
                    r.Contact AS ReportContact,
                    s.Url AS ScreenshotUrl,
                    s.Transcription AS ScreenshotTranscription
                FROM 
                    Report r
                LEFT JOIN 
                    Screenshot s ON r.Id = s.ReportId";

                    using (var cmd = new MySqlCommand(query, connection))
                    {
                        using (var reader = cmd.ExecuteReader())
                        {
                            int currentReportId = -1;
                            Report currentReport = null;

                            while (reader.Read())
                            {
                                int reportId = reader.GetInt32("ReportId");

                                if (reportId != currentReportId)
                                {
                                    // Nuevo reporte encontrado
                                    currentReportId = reportId;
                                    currentReport = new Report
                                    {
                                        Id = reportId,
                                        Description = reader.GetString("ReportDescription"),
                                        State = reader.GetString("ReportState"),
                                        Contact = reader.GetString("ReportContact"),
                                        Screenshots = new List<Screenshot>()
                                    };
                                    reports.Add(currentReport);
                                }

                                // Agregar la información de la screenshot, si existe
                                if (!reader.IsDBNull(reader.GetOrdinal("ScreenshotUrl")))
                                {
                                    currentReport.Screenshots.Add(new Screenshot
                                    {
                                        Url = reader.GetString("ScreenshotUrl"),
                                        Transcription = reader.GetString("ScreenshotTranscription")
                                    });
                                }
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

        public class ImageUrlsModel
        {
            public string ImageUrl1 { get; set; }
            public string ImageUrl2 { get; set; }
            public string ImageUrl3 { get; set; }
            public string Contact { get; set; }
            public string Description { get; set; }
        }

        public class Report
        {
            public int Id { get; set; }
            public string Description { get; set; }
            public string State { get; set; }
            public string Contact { get; set; }
            public List<Screenshot> Screenshots { get; set; }
        }

        public class Screenshot
        {
            public string Url { get; set; }
            public string Transcription { get; set; }
        }

    }
}
