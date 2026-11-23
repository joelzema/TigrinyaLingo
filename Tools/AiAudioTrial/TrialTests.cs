using System.Net;
using System.Text;
using System.Text.Json;

static class TrialTests
{
    public static async Task Run()
    {
        var folder = Path.Combine(Path.GetTempPath(), "TigrinyaLingo_AiTrial_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var sample = new Sample("selam", "Hej <test>", "ሰላም");
            var job = new TrialJob(sample, "ti-berhane", folder);
            var id = job.RequestId();
            Check(job.RequestId() == id, "Samma request-ID används vid nytt försök.");
            Check(new TrialJob(sample with { Source = "መርሓባ" }, "ti-berhane", folder).Stem != job.Stem,
                "Ny text får separat fil och request-ID.");
            string? firstId = null; var generated = 0; var downloads = 0;
            var apiHandler = new FakeHandler(async request => {
                Check(request.Headers.GetValues("x-api-key").Single() == "fake-local-test-key", "Endast API-klienten har nyckeln.");
                if (request.RequestUri!.AbsolutePath.EndsWith("/voices"))
                    return Json("{\"data\":[{\"id\":\"ti-berhane\",\"is_available\":true}]}");
                var payload = JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement;
                Check(payload.GetProperty("text").GetString() == sample.Source && payload.GetProperty("language").GetString() == "ti", "Tigrinjakällan skickas med rätt språk.");
                if (request.RequestUri.AbsolutePath.EndsWith("/estimate"))
                {
                    Check(!payload.TryGetProperty("client_request_id", out _), "Kostnadsanropet innehåller inget tomt request-ID eller extra genereringsfält.");
                    return Json("{\"data\":{\"can_generate\":true,\"currency\":\"ETB\",\"estimated_cost\":0.05}}");
                }
                generated++;
                var requestId = payload.GetProperty("client_request_id").GetString();
                firstId ??= requestId; Check(firstId == requestId, "Oklart svar skapar inte en ny genereringsavsikt.");
                return Json("{\"data\":{\"audio_url\":\"https://audio.example.invalid/test.mp3\"}}");
            });
            var downloadHandler = new FakeHandler(request => {
                downloads++;
                Check(!request.Headers.Contains("x-api-key"), "Nyckeln skickas aldrig till ljudadressen.");
                if (downloads == 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
                var audio = new byte[64]; Encoding.ASCII.GetBytes("ID3").CopyTo(audio, 0);
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(audio) });
            });
            using var api = new HttpClient(apiHandler) { BaseAddress = new Uri("https://api.example.invalid/") };
            api.DefaultRequestHeaders.Add("x-api-key", "fake-local-test-key");
            using var download = new HttpClient(downloadHandler);
            var client = new TrialClient(api, download);
            await client.CheckVoice("ti-berhane");
            Check(await client.Estimate(job) == .05m && generated == 0, "Kostnadsuppskattning genererar inget ljud.");
            try { await client.Generate(job); throw new Exception("Testet skulle avvisa en misslyckad hämtning."); }
            catch (TrialError) { Check(!job.HasAudio, "En misslyckad hämtning sparas inte som ljud."); }
            await client.Generate(job);
            Check(job.HasAudio && generated == 2 && firstId == id, "Ett nytt försök hämtar MP3 med samma request-ID.");
            Check(!TrialClient.IsMp3(Encoding.UTF8.GetBytes(new string('x', 64))), "HTML eller andra ogiltiga svar godtas inte som MP3.");
            TrialPage.Save(folder, new[] { job });
            var html = await File.ReadAllTextAsync(Path.Combine(folder, "index.html"));
            Check(html.Contains("Hej &lt;test&gt;") && !html.Contains(sample.Source) && !html.Contains("fake-local-test-key") && html.Contains("<audio"),
                "Lyssningssidan visar latin och ljud utan nyckel, originalskrift eller oescapad text.");
            using var errorApi = new HttpClient(new FakeHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest) {
                Content = new StringContent(JsonSerializer.Serialize(new { error = new {
                    code = "VALIDATION_ERROR", message = "Unknown field client_request_id, fake-local-test-key https://example.invalid/private?token=secret",
                    details = new[] { new { message = "Remove null field", input = "must-never-be-shown" } }
                }}), Encoding.UTF8, "application/json")
            }))) { BaseAddress = new Uri("https://api.example.invalid/") };
            errorApi.DefaultRequestHeaders.Add("x-api-key", "fake-local-test-key");
            try { await new TrialClient(errorApi, download).Estimate(job); throw new Exception("HTTP 400 skulle stoppas."); }
            catch (TrialError error)
            {
                Check(error.Message.Contains("kostnadsuppskattning") && error.Message.Contains("VALIDATION_ERROR") && error.Message.Contains("Remove null field")
                    && !error.Message.Contains("fake-local-test-key") && !error.Message.Contains("example.invalid") && !error.Message.Contains("must-never-be-shown"),
                    "HTTP 400 visar steg och serverns felorsak men döljer nyckel, adresser och inmatningsfält.");
            }
            using var htmlError = new StringContent("<html>fake-local-test-key</html>");
            Check(!(await SafeApiError.Read(htmlError, errorApi.DefaultRequestHeaders)).Contains("fake-local-test-key"), "Ett HTML-felsvar skrivs inte ut rått.");
            Console.WriteLine("Alla lokala tester passerade. Ingen kontakt med Addis AI har gjorts.");
        }
        finally
        {
            // Enbart filer som just detta test skapade i sin egen slumpmapp tas bort.
            foreach (var file in Directory.GetFiles(folder)) File.Delete(file);
            Directory.Delete(folder);
        }
    }
    static HttpResponseMessage Json(string json) => new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
    static void Check(bool condition, string message) { if (!condition) throw new Exception(message); Console.WriteLine("PASS: " + message); }
    sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => respond(request);
    }
}
