using System.Net;
using System.Text.RegularExpressions;

internal static class FirstMeetingVerification
{
    public static async Task RunAsync(HttpClient client, Action<bool,string> check)
    {
        var page = await client.GetStringAsync("/FirstMeeting");
        var csrf = Regex.Match(page, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value;
        check(page.Contains("Visa ledtråd") && page.Contains("Visa alternativ") && csrf.Length > 0,
            "Skrivdialogen erbjuder ledtråd och flerval före första avslutade lektionen");
        async Task<string> Reply(int step, string answer)
        {
            var response = await client.PostAsync("/FirstMeeting/Answer", new FormUrlEncodedContent(new Dictionary<string,string> {
                ["__RequestVerificationToken"] = csrf, ["step"] = step.ToString(), ["answer"] = answer
            }));
            response.EnsureSuccessStatusCode();
            return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        }
        check((await Reply(1,"  SÄLAM! ")).Contains("Fortsätt samtalet"), "Hälsningen godtar stavningsvariant och skiljetecken");
        check((await Reply(2,"kemey aleka")).Contains("säger du till en man"), "Fel tilltal ger specifik återkoppling");
        check((await Reply(2,"kemey al")).Contains("Kontrollera stavningen"), "Okänd stavning går att rätta");
        check((await Reply(2,"kemey   aleki?")).Contains("Fortsätt samtalet"), "Rätt tilltal leder till presentationen");
        var last = WebUtility.HtmlDecode(await client.GetStringAsync("/FirstMeeting?step=3"));
        var example = Regex.Matches(last, "name=\"answer\" value=\"([^\"]+)\"").Select(m => m.Groups[1].Value).Single(x=>x.Contains("Sara"));
        check((await Reply(3,example)).Contains("Du har hälsat"), "Namnplatsen fylls i och dialogen får ett avslut");
    }
}
