using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Separat provlyssning. Ingen anslutning till appens databas eller ljudmapp.
var samples = new[] {
    new Sample("selam", "Hej", "ሰላም"),
    new Sample("merhaba", "Välkommen", "መርሓባ"),
    new Sample("yekenyeley", "Tack", "የቐንየለይ"),
    new Sample("aytered’anin", "Jag förstår inte", "ኣይተረድኣኒን"),
    new Sample("yiqreta", "Förlåt", "ይቅሬታ")
};
string Option(string name, string fallback) => args.FirstOrDefault(value => value.StartsWith(name + "=", StringComparison.Ordinal))?[(name.Length + 1)..] ?? fallback;
var mode = args.FirstOrDefault(value => value is "--list" or "--estimate" or "--generate" or "--self-test") ?? "--list";
var voice = Option("--voice", "ti-berhane");
var output = Path.GetFullPath(Option("--output", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "TigrinyaLingo", "AiAudioTrial")));
var maxCost = decimal.Parse(Option("--max-etb", "1"), CultureInfo.InvariantCulture);
if (maxCost <= 0) throw new ArgumentException("--max-etb måste vara positivt.");

try
{
    if (mode == "--self-test") { await TrialTests.Run(); return 0; }
    foreach (var sample in samples) Console.WriteLine($"{sample.Meaning} → {sample.Latin}");
    if (mode == "--list")
    {
        Console.WriteLine("Ingen ljudgenerering har gjorts. Använd --estimate eller --generate när ADDIS_API_KEY är konfigurerad.");
        return 0;
    }
    var key = Environment.GetEnvironmentVariable("ADDIS_API_KEY");
    if (string.IsNullOrWhiteSpace(key))
    {
        Console.Error.WriteLine("ADDIS_API_KEY saknas. Läs Tools/AiAudioTrial/README.md. Skriv aldrig nyckeln i chatten eller i källkoden.");
        return 2;
    }
    using var api = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new Uri("https://api.addisassistant.com/"), Timeout = TimeSpan.FromSeconds(110) };
    api.DefaultRequestHeaders.Add("x-api-key", key.Trim());
    using var downloader = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(60) };
    var client = new TrialClient(api, downloader);
    Console.WriteLine("Kontrollerar tillgängliga tigrinjaröster …");
    await client.CheckVoice(voice);
    Directory.CreateDirectory(output);
    decimal total = 0;
    var jobs = samples.Select(sample => new TrialJob(sample, voice, output)).ToList();
    foreach (var job in jobs.Where(job => !job.HasAudio))
    {
        Console.WriteLine($"Uppskattar kostnaden för: {job.Sample.Latin} …");
        total += await client.Estimate(job);
    }
    Console.WriteLine($"Uppskattad kostnad för saknade testljud: {total:0.####} ETB. Testgräns: {maxCost:0.####} ETB.");
    if (mode == "--estimate") return 0;
    if (total > maxCost) throw new TrialError("Uppskattningen överstiger testgränsen. Ingen ljudgenerering har startats. Välj en annan gräns med --max-etb om du vill fortsätta.");
    // Kostnaden är en uppskattning från tjänsten, ingen garanterad fakturagräns.
    foreach (var job in jobs)
    {
        if (job.HasAudio) { Console.WriteLine($"Återanvänder testljud: {job.Sample.Latin}"); continue; }
        Console.WriteLine($"Skapar testljud: {job.Sample.Latin} …");
        await client.Generate(job);
        TrialPage.Save(output, jobs);
    }
    TrialPage.Save(output, jobs);
    Console.WriteLine("Klart för provlyssning: " + Path.Combine(output, "index.html"));
    Console.WriteLine("Testljuden har inte kopplats till appens fraser. Bedöm uttalet innan de används i undervisningen.");
    return 0;
}
catch (Exception error) when (error is TrialError or HttpRequestException or TaskCanceledException or JsonException or IOException or UnauthorizedAccessException)
{
    // Inga svarskroppar, hemliga headers eller signerade ljudadresser skrivs ut.
    Console.Error.WriteLine(error is TrialError ? error.Message : "Testet kunde inte slutföras. Kontrollera anslutningen och försök igen. Sparade testljud och request-ID:n behålls.");
    return 1;
}

record Sample(string Latin, string Meaning, string Source);
sealed class TrialError(string message) : Exception(message);
sealed class TrialJob
{
    public Sample Sample { get; }
    public string Voice { get; }
    public string Stem { get; }
    public string AudioPath { get; }
    public string RequestPath { get; }
    public bool HasAudio => File.Exists(AudioPath) && TrialClient.IsMp3(File.ReadAllBytes(AudioPath));
    public TrialJob(Sample sample, string voice, string output)
    {
        Sample = sample; Voice = voice;
        Stem = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { sample.Source, voice, Language = "ti", Format = "mp3_44100" })))).ToLowerInvariant();
        AudioPath = Path.Combine(output, Stem + ".mp3"); RequestPath = Path.Combine(output, Stem + ".request-id");
    }
    public Dictionary<string, object> Payload(string? requestId = null)
    {
        var payload = new Dictionary<string, object> { ["text"] = Sample.Source, ["voice_id"] = Voice, ["language"] = "ti", ["output_format"] = "mp3_44100" };
        if (requestId is not null) payload["client_request_id"] = requestId;
        return payload;
    }
    public string RequestId()
    {
        if (File.Exists(RequestPath)) return Guid.Parse(File.ReadAllText(RequestPath)).ToString();
        var id = Guid.NewGuid().ToString();
        using var stream = new FileStream(RequestPath, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream); writer.Write(id); return id;
    }
}
sealed class TrialClient(HttpClient api, HttpClient downloader)
{
    private static JsonElement Data(JsonElement root) => root.TryGetProperty("data", out var data) ? data : root;
    private async Task<JsonElement> Send(HttpRequestMessage request)
    {
        using (request)
        using (var response = await api.SendAsync(request))
        {
            if (!response.IsSuccessStatusCode)
            {
                var stage = request.RequestUri!.OriginalString.Contains("/estimate") ? "kostnadsuppskattning"
                    : request.RequestUri.OriginalString.Contains("/generations") ? "ljudgenerering" : "röstkontroll";
                var explanation = await SafeApiError.Read(response.Content, api.DefaultRequestHeaders);
                var advice = response.StatusCode switch {
                    HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "Kontrollera att hela nyckeln klistrats in och att den har åtkomst till talsyntes.",
                    HttpStatusCode.PaymentRequired => "Kontrollera kontots saldo. Ingen automatisk påfyllning görs.",
                    HttpStatusCode.TooManyRequests => "Vänta innan du kör igen; samma request-ID behålls.",
                    HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => "Anropet avvisades. Skicka detta felmeddelande för felsökning, utan API-nyckeln.",
                    _ => "Försök igen senare. Sparade request-ID:n behålls."
                };
                throw new TrialError($"Steg: {stage} (HTTP {(int)response.StatusCode}). {explanation} {advice}");
            }
            using var json = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            return Data(json.RootElement).Clone();
        }
    }
    public async Task CheckVoice(string voice)
    {
        var catalog = await Send(new HttpRequestMessage(HttpMethod.Get, "api/v1/voice/voices?language=ti"));
        if (catalog.ValueKind == JsonValueKind.Object && catalog.TryGetProperty("voices", out var voices)) catalog = voices;
        if (catalog.ValueKind != JsonValueKind.Array || !catalog.EnumerateArray().Any(item => item.TryGetProperty("id", out var id) && id.GetString() == voice
            && item.TryGetProperty("is_available", out var available) && available.ValueKind == JsonValueKind.True))
            throw new TrialError("Den valda tigrinjarösten är inte tillgänglig. Kontrollera röstkatalogen innan testet fortsätter.");
    }
    public async Task<decimal> Estimate(TrialJob job)
    {
        var data = await Send(new HttpRequestMessage(HttpMethod.Post, "api/v1/voice/estimate") { Content = JsonContent.Create(job.Payload()) });
        if (!data.GetProperty("can_generate").GetBoolean()) throw new TrialError("Kontot saknar tillräckligt saldo eller åtkomst för testet.");
        if (data.GetProperty("currency").GetString() != "ETB") throw new TrialError("Tjänstens valuta skiljer sig från testets kostnadsgräns.");
        var cost = data.GetProperty("estimated_cost").GetDecimal();
        if (cost < 0) throw new TrialError("Tjänsten lämnade en ogiltig kostnadsuppskattning.");
        return cost;
    }
    public async Task Generate(TrialJob job)
    {
        var data = await Send(new HttpRequestMessage(HttpMethod.Post, "api/v1/voice/generations") { Content = JsonContent.Create(job.Payload(job.RequestId())) });
        if (!data.TryGetProperty("audio_url", out var value) || !Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri)
            || uri.Scheme != "https" || uri.HostNameType != UriHostNameType.Dns || uri.IsLoopback || uri.Host.EndsWith(".local", StringComparison.OrdinalIgnoreCase))
            throw new TrialError("Tjänsten lämnade ingen giltig HTTPS-adress till testljudet. Sparat request-ID kan återanvändas.");
        using var response = await downloader.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead);
        if (!response.IsSuccessStatusCode) throw new TrialError("Testljudet kunde inte hämtas. Kör igen för att hämta samma generering.");
        const int maxBytes = 10 * 1024 * 1024;
        if (response.Content.Headers.ContentLength > maxBytes) throw new TrialError("Testljudet är oväntat stort och sparades inte.");
        await using var source = await response.Content.ReadAsStreamAsync();
        using var bytes = new MemoryStream();
        var buffer = new byte[8192]; int read;
        while ((read = await source.ReadAsync(buffer)) != 0)
        {
            if (bytes.Length + read > maxBytes) throw new TrialError("Testljudet är oväntat stort och sparades inte.");
            await bytes.WriteAsync(buffer.AsMemory(0, read));
        }
        var audio = bytes.ToArray();
        if (!IsMp3(audio)) throw new TrialError("Svaret ser inte ut som en MP3-fil. Inget ljud har lagts in i appen.");
        var temporary = job.AudioPath + ".tmp";
        await File.WriteAllBytesAsync(temporary, audio);
        File.Move(temporary, job.AudioPath, overwrite: true);
    }
    internal static bool IsMp3(byte[] audio) => audio.Length >= 32 && ((audio[0] == 'I' && audio[1] == 'D' && audio[2] == '3') || (audio[0] == 0xff && (audio[1] & 0xe0) == 0xe0));
}
static class TrialPage
{
    public static void Save(string output, IEnumerable<TrialJob> jobs)
    {
        string H(string value) => WebUtility.HtmlEncode(value);
        var rows = string.Join("\n", jobs.Select(job => $"<article><h2>{H(job.Sample.Meaning)}</h2><p lang=\"ti-Latn\">{H(job.Sample.Latin)}</p>" + (job.HasAudio ? $"<audio controls preload=\"none\" aria-label=\"Lyssna på {H(job.Sample.Latin)}\" src=\"{job.Stem}.mp3\"></audio>" : "<p>Inte genererat ännu.</p>") + "</article>"));
        File.WriteAllText(Path.Combine(output, "index.html"), """
            <!doctype html><html lang="sv"><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1"><title>Provlyssna på AI-tigrinja</title>
            <style>body{font:18px system-ui,sans-serif;background:#f8f7f1;color:#243c34;max-width:720px;margin:40px auto;padding:20px}h1{font-size:2rem}article{background:#fff;border:1px solid #d9dece;border-radius:16px;padding:20px;margin:16px 0}h2{margin:0;font-size:1.1rem}audio{max-width:100%}.note{color:#536259}</style>
            <h1>Provlyssna på AI-tigrinja</h1><p class="note">Fem testuttryck. Ljuden är skapade med AI och behöver bedömas av en tigrinjatalande person. De har inte ersatt inspelningarna i appen.</p>
            """ + rows + "<p class=\"note\">Originalskrift används som underlag för talsyntesen. Lärandet visar fortsatt latinska bokstäver.</p></html>");
    }
}
