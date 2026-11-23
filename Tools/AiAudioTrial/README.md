> Pausat 16 november 2026. Detta tidigare test använder en betald extern tjänst. Det används inte av appen och ska inte köras i det fortsatta ljudarbetet. Användaren planerar egna lärarinspelningar.

# Testa AI-ljud för tigrinja

Det här är ett separat testverktyg för fem fasta exempel: selam, merhaba, yekenyeley, aytered’anin och yiqreta. Det ersätter inte ljud i TigrinyaLingo och skriver ingenting till appens databas. Egna och redigerade fraser omfattas inte av det första testet.

## Skaffa åtkomst

1. Öppna https://addisassistant.com/apikeys och skapa/logga in på ett eget konto.
2. Skapa en API-nyckel i kontots API keys. Nyckeln ska hanteras som ett lösenord. Klistra inte in den i chatten, Git eller appsettings.json.
3. Kontrollera kontots aktuella saldo och villkor. Officiellt listpris är 5 ETB per genererad ljudminut (kontrollerat 2026-11-16). Testet uppskattar kostnaden innan det genererar. Ingen automatisk påfyllning eller betalning görs av verktyget.

## Kör från projektets rot i en PowerShell-terminal

Börja med bara kostnadsuppskattningen:

```powershell
& .\Tools\AiAudioTrial\Start-Trial.ps1
```

Nyckeln efterfrågas dolt och används bara medan kommandot kör. Den sparas inte på disken. Om en ADDIS_API_KEY redan finns i terminalmiljön används den i stället.

När uppskattningen ser bra ut, skapa de fem testljuden:

```powershell
& .\Tools\AiAudioTrial\Start-Trial.ps1 -Generate
```

Standardgränsen är en uppskattad totalkostnad på 1 ETB för saknade ljud. Överstiger uppskattningen gränsen startas ingen generering. Vid behov kan en annan gräns uttryckligen anges med -MaxEtb. Uppskattningen är inte en garanti för slutlig debitering; tjänsten debiterar faktisk generering. Verktyget kontrollerar att den valda tigrinjarösten är tillgänglig. Annan röst kan anges med -Voice, till exempel ett ID ur den aktuella röstkatalogen.

Öppna därefter index.html på sökvägen som skrivs i terminalen. Standardmappen är `%LOCALAPPDATA%\TigrinyaLingo\AiAudioTrial`. Där finns MP3-filerna och en lyssningssida med svensk betydelse och latinsk tigrinja. Originalskriften från projektets startinnehåll används som underlag för talsyntesen; automatisk uppläsning av våra latinska stavningar är ännu inte verifierad. En senare koppling för egna fraser behöver hantera detta separat så att gammal originalskrift inte läses upp efter en latinändring.

Låt någon som talar tigrinja bedöma uttal, tilltal och eritreansk språkvariant innan ljuden kopplas till lärandet. Varken ljudkvalitet eller verklig API-kompatibilitet har bekräftats förrän ett test med egen nyckel lyckas.

## Sparande och nya försök

Befintliga test-MP3:er återanvänds. Request-ID sparas före varje generering. Vid timeout eller misslyckad hämtning behålls samma ID så att ett nytt försök avser samma generering. Ta inte bort request-ID-filerna under återhämtning. Ändrad källtext/röst får ett eget filnamn och request-ID. Inga konton, befintliga inspelningar eller framsteg ändras.

Lokal kontroll utan API-nyckel eller nätverksanrop:

```powershell
dotnet run --project .\Tools\AiAudioTrial -- --self-test
```

API-underlag: https://docs.addisassistant.com/docs/capabilities/text-to-speech
Prisuppgifter: https://docs.addisassistant.com/docs/platform/pricing

### Felsökning av inklistring och HTTP 400

Startskriptet kontrollerar nu att minst 20 tecken utan blanksteg/kontrolltecken kommit med innan något nätverksanrop görs. Det skriver aldrig ut nyckeln. I Developer PowerShell kan Shift+Insert eller högerklick fungera om Ctrl+V inte klistrar in hela nyckeln. Stjärnorna visar inte säkert hur många tecken som klistrats in.

Kostnadsanropet utelämnar nu det valfria client_request_id helt, som i tjänstens dokumenterade exempel; fältet skickas endast vid generering. Ett avvisat anrop visar vilket steg som stoppades och kända, maskerade felmeddelandefält från tjänsten. API-nyckeln, webbadresser och råa svarskroppar visas inte. Ett HTTP 400 bör felsökas utifrån felorsaken och inte bara köras om oförändrat. Startskriptet har UTF-8 BOM för svenska tecken även i Windows PowerShell 5.1.
