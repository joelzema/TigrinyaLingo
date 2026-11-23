# TigrinyaLingo

> Aktuellt 16 november 2026: ljudarbetet är pausat. Alla 32 Omniglot-inspelningar har tagits bort ur appen. Lektioner, quiz, repetition och spel fungerar med läsning och översättning. Lärarens egna inspelningar kan läggas till senare. Äldre avsnitt nedan beskriver delvis utvecklingshistoriken.

## Projekt

Öppna `TigrinyaLingo/TigrinyaLingo.sln` i Visual Studio. Projektkopplingen är MVC → Infrastructure → Domain. Alla tre projektmappar måste finnas bredvid varandra.

Domain innehåller datamodellerna. Infrastructure innehåller DbContext och migrationer. TigrinyaLingo innehåller webbgränssnittet och Identitys standardsidor.

## Databas och migrationshistorik

Appen använder SQL Server LocalDB-instansen `MSSQLLocalDB` och databasen `TigrinyaLingoDb`. Inställningen finns i webbprojektets appsettings.json.

Den befintliga databasen hade två migrationer men deras källkod saknades:

- `00000000000000_CreateIdentitySchema`
- `20260121112847_Init`

De två filparen i Infrastructure/Migrations är **återskapade baslinjer, inte återfunnen originalkod**. Baslinjens Identity-kolumner, index och främmande nycklar har jämförts med den befintliga databasen. Init bevarar det historiska ID:t utan ytterligare schemaändringar. Baslinjen kan också bygga upp Identity i en ny databas.

`AddLearningSchema` lägger till Courses, Lessons, Questions, Choices och UserProgresses samt deras relationer och index. Den ändrar inte befintliga Identity-tabeller. Snapshot-filen beskriver den senaste modellen och används när EF Core beräknar nästa migration.

I Visual Studios Package Manager Console kan kommandona anges uttryckligen:

```powershell
Get-Migration -Project Infrastructure -StartupProject TigrinyaLingo
Update-Database -Project Infrastructure -StartupProject TigrinyaLingo
```

Migrationerna körs inte automatiskt varje gång appen startas. Kontrollera alltid vilken anslutningssträng som används när databasen uppdateras.

## Testa konton lokalt

1. Starta webbappen med HTTPS-profilen i Visual Studio.
2. Välj Skapa konto och fyll i mejladress och lösenord.
3. Välj Bekräfta mitt konto på nästa sida. Inget mejl skickas i den lokala utvecklingsversionen.
4. Logga in och kontrollera att namnet visas i menyn.
5. Välj Logout.

Kontobekräftelse krävs fortfarande. Ingen riktig e-postleverans är implementerad. Bekräftelseflödet behöver förberedas för publicering med en e-posttjänst och utan en direkt bekräftelselänk på sidan.

## Integrationstest

Från den gemensamma projektmappen:

```powershell
dotnet run --project IntegrationVerification/IntegrationVerification.csproj
```

Testet skapar en ny databas med ett slumpmässigt namn, jämför Identity-strukturen med TigrinyaLingoDb och kör migrationerna. Det startar sedan webbappen lokalt mot testdatabasen och kontrollerar registrering, kontobekräftelse, rätt/fel lösenord och utloggning. Formulären skickas med antiforgery-token.

Testet ändrar inte TigrinyaLingoDb. Webbprocessen stoppas och testdatabasen tas bort efter testet, även vid testfel. Det kontrollerar också kurs- och lektionssidor, enbart latinsk visning av tigrinja, numerisk lektionsordning, tomma sidor, saknade ID:n och att innehållet inte dubbleras.

## Nästa utvecklingssteg

Nästa steg kan vara fler lektioner och övningstyper. Lyssningsquiz med serverrättning och sparade framsteg är nu implementerat.

## Första kursen och lektionen

Starta appen och välj **Utforska kurser → Visa lektioner → Öppna lektionen**. Den första kursen heter Tigrinja för nybörjare. Lektionen Dina första hälsningar innehåller fyra ordkort. Svenska visas först, och under det visas hur ordet skrivs på tigrinja med latinska bokstäver. Läs ordet högt. Tigrinjan visas enbart med latinska bokstäver.

| Svenska | Tigrinja med latinska bokstäver | Originalskrift |
| --- | --- | --- |
| Hej | selam | ሰላም |
| Välkommen | merhaba | መርሓባ |
| Tack | yekenyeley | የቐንየለይ |
| Hej då | selamat | ሰላማት |

Fraserna och den latinska skrivningen har kontrollerats mot [Omniglots frassamling](https://www.omniglot.com/language/phrases/tigrinya.php). Här används svenska motsvarigheter till de engelska betydelserna. Latinsk skrivning kallas translitterering. Den hjälper användaren att läsa orden. Uttalsinspelningar planeras med en tigrinjalärare.

`LearningSeedData.cs` beskriver det fasta startinnehållet. Migrationen SeedFirstLesson lägger in en kurs, en lektion, fyra frågor och sexton svarsalternativ. Varje fråga har ordet som Text och ett korrekt alternativ med den svenska betydelsen. Alternativen förbereder modellen för quizet som kommer senare. Migrationen körs en gång per databas, inte vid varje start.

Migrationen AddLatinText lägger till Question.LatinText och fyller de fyra befintliga orden med deras latinska skrivning. Exempel: Text = ሰላም, LatinText = selam och det korrekta svarsalternativets Text = Hej. Inga ord eller användarframsteg raderas.

Att läsa eller öppna ordkorten sparar inga framsteg. Kurs- och lektionssidorna går att läsa utan inloggning. Quiz och personliga framsteg kräver däremot inloggning.

## Så hänger koden ihop

1. Länken **Kurser** går till `CoursesController.Index`.
2. Controllern använder ApplicationDbContext för att läsa Courses från SQL Server. `Select` väljer den information sidan behöver, exempelvis titel och antal lektioner. `AsNoTracking` passar för läsning utan ändringar.
3. Informationen skickas i en vy-modell till `Views/Courses/Index.cshtml`. Razor-vyn skapar HTML-korten.
4. **Visa lektioner** går till `CoursesController.Details` med kursens ID. Lektionerna sorteras efter Order och sedan Id.
5. **Öppna lektionen** går till `LessonsController.Details`. Den hämtar orden, deras svenska betydelser och LatinText till `Views/Lessons/Details.cshtml`.
6. Vyn visar exempelvis Hej och selam direkt. Tigrinjatecknen hämtas inte till vymodellen och skickas inte till webbläsaren. Presentationslektionens användningstips ligger separat under ordkorten.

Vy-modellerna i `LearningViewModels.cs` beskriver sidornas data. Entities i Domain beskriver databasens data. En saknad kurs eller lektion ger HTTP 404, medan en befintlig men tom kurs eller lektion får ett begripligt meddelande.

## Uttalsinspelningar – pausat

Appen har för närvarande inga medföljande uttalsinspelningar. Omniglot-ljuden är borttagna på användarens begäran. Question.AudioFileName och ljudspelaren finns kvar som stöd för lärarens framtida inspelningar. När ljud saknas visar lärandet läsning och översättning. Ingen betald ljudtjänst anropas av webbappen.

## Quiz och sparade framsteg

1. Logga in och öppna lektionen. Välj **Starta quiz**.
2. Lyssna på varje tigrinjaord och välj en svensk betydelse. Om du behöver lässtöd kan du visa ordet med latinska bokstäver.
3. Välj **Rätta och spara**. Resultatsidan visar poängen för försöket och återkoppling per fråga.
4. Välj **Mina framsteg** för att se dina sparade bästa resultat. Du ser också statusen i kursens lektionslista.

En poäng betyder ett rätt svar. Bästa resultatet sparas per användare och lektion. Alla rätt i samma försök markerar lektionen som klar och sparar den första klar-tiden i UTC. Ett sämre försök sänker inte den bästa poängen och tar inte bort klar-statusen. Tabellen UserProgresses fanns redan, så quizet kräver ingen ny databasstruktur eller migration.

Det finns en sammanfattande rad per användare och lektion, inte en historik med varje försök. Efter ett nytt försök visas dess svar via skyddad TempData. Vid omladdning av resultatsidan visas den sparade sammanfattningen utan att quizet skickas igen.

### Kodens flöde

- **QuizController.Index** läser lektionens frågor och skickar svarsalternativen till vyn. Inga IsCorrect-fält skickas med som facit.
- **QuizController.Submit** tar emot fråge- och svarsalternativens ID:n. Den kontrollerar att varje fråga hör till lektionen och varje alternativ hör till rätt fråga. Ofullständiga svar ger hjälptext och sparas inte.
- Rättningen jämför de valda alternativen med databasens IsCorrect. Poäng och användar-ID från formuläret används inte. Kontots ID hämtas från Identitys inloggningsuppgifter.
- Bästa resultatet uppdateras med en SQL-jämförelse mot den aktuella poängen. Det unika indexet och hanteringen av samtidiga första inskickningar förhindrar dubbletter.
- Efter en godkänd inskickning omdirigeras webbläsaren till **QuizController.Result**. Resultat, kursstatus och **ProgressController.Index** läser endast den inloggade användarens framsteg.
- **_WordAudio.cshtml** är en gemensam delvy för ljudknapparna i både ordkorten och quizet.

Integrationstestet omfattar två separata konton, inloggningskrav, antiforgery-token, ofullständiga svar, felaktiga eller dubbla fråge-ID:n, alternativ från andra frågor, ändrade poäng/konto-ID:n, upprepade försök, samtidiga första försök och samtidiga uppdateringar. Testkontona och databasen städas bort efteråt.

För en manuell webbläsarkontroll kan testservern hållas öppen med:

```powershell
dotnet run --project IntegrationVerification/IntegrationVerification.csproj -- --preview
```

Efter testerna visas en lokal adress. Förhandsvisningen använder testkontot verification@example.invalid med testlösenordet Verification!4820x. Det är en tillfällig testanvändare, inte ett konto i TigrinyaLingoDb. Tryck Enter i terminalen för att stoppa servern och ta bort testdatabasen.

## Lektion 2: Presentera dig

Kursen innehåller nu även fyra presentationsfraser, latinsk skrivning och fyra lokala inspelningar. Lektionssidan förklarar hur man fyller i sitt namn och sin hemort samt skillnaden mellan shimka (till en man) och shimki (till en kvinna). Latinsk skrivning är ett lässtöd; lyssna på inspelningen för uttalet.

`LearningSeedData.cs` beskriver innehållet. Migrationen `SeedIntroductionsLesson` lägger till lektion 2, frågor 5–8 och alternativ 17–32. Befintliga frågor och framsteg ändras inte. Samma quizcontroller rättar båda lektionerna genom deras ID. Framsteg sparas separat för varje användare och lektion. Den lektionsspecifika övningstexten finns i `Views/Lessons/Details.cshtml`.

Fraser och ljud: Asmeret Hagos via https://www.omniglot.com/language/phrases/tigrinya.php . Se ljudmappens SOURCES.md för originalfiler och användningsvillkor.
## Startsida före inloggning

Båda startprofilerna i `Properties/launchSettings.json` öppnar uttryckligen `Home/Index`. Startsidan är tillgänglig utan konto och erbjuder Utforska kurser och Logga in. Kurser, lektionskort och ljud kan användas utan inloggning. Quiz och personliga framsteg kräver fortfarande konto. Befintliga konton och resultat påverkas inte.
## Lektionsväg och nästa steg

Kurssidan visar en mätare med antalet klara lektioner för det inloggade kontot och rekommenderar den första återstående lektionen. Alla lektioner är fortfarande möjliga att öppna. Utloggade besökare får ett Börja här-val utan personliga framsteg. Tomma lektioner räknas inte som tillgängliga.

Efter ett avklarat quiz föreslår resultatsidan första återstående lektionen i samma kurs (sorterat på Order och sedan Id). Om alla tillgängliga lektioner är klara visas en avslutning med möjlighet att repetera. Detta använder befintliga UserProgress-rader; ingen databasändring behövs. CourseDetailsViewModel räknar samman kursöversikten och QuizController hämtar nästa lektion för det aktuella kontot.
## Svenska kontosidor

Login, Register, RegisterConfirmation och ConfirmEmail har nu egna Razor Pages i Areas/Identity/Pages/Account. Identity hanterar fortfarande lösenord, bekräftelsetoken och inloggningskakor. Lösenordskraven läses från Identity-inställningarna och visas på svenska; även felaktiga och ofullständiga formulär ger svensk hjälptext. Kom ihåg mig är ett val på inloggningssidan. Quizets lokala returadress följer med vid skapande och bekräftelse av kontot.

I Development skickas inget mejl. Knappen Bekräfta mitt konto visas efter registreringen, endast för den webbläsare som nyss skapade kontot. Uppgifterna ligger i skyddad TempData och tas bort när bekräftelsen lyckas. En mejladress i webbadressen räcker inte för att få en bekräftelselänk för ett befintligt konto. Gamla, redan bekräftade konton kan logga in som tidigare. Om ett tidigare skapat konto fortfarande är obekräftat och dess lokala bekräftelsesida saknas finns ännu inget återställningsflöde för det.

Nyregistrering i Production är stängd tills en riktig e-posttjänst och ett fullständigt bekräftelse-/återställningsflöde har införts. Bekräftelsekravet är oförändrat. Ingen databasändring krävs för kontosidorna 
## Naturliga översättningar, bara latinska bokstäver

Översättningarna är naturliga motsvarigheter, inte strikt ord-för-ord: Hej → selam och Jag heter … → simey … yebehal. Tigrinjafraserna visas enbart via LatinText i lektioner, quiz och resultat. Knappen för originalskriften är borttagen och startsidans symbol är nu TL. Originalskriften finns kvar i databasen som källmaterial men hämtas inte av LessonsController till studieordens vymodell.

Både menyu shimka och menyu shimki betyder Vad heter du? på naturlig svenska. Förklaringen om vem man talar till finns separat i det valfria tipsavsnittet. Migrationen SimplifyTranslationLabels tar bort parentesförklaringarna ur svarsalternativen och ersätter annars dubbla felalternativ med Hej. Varje fråga har fortfarande fyra olika alternativ och samma korrekta alternativ-ID. Frågeantalet, ljuden och befintliga framsteg ändras inte.
## Öva på mina fel

Efter ett vanligt quiz länkar resultatet och Mina framsteg till en repetitionsövning med de frågor användaren senast svarat fel på. Ljud, naturliga svenska betydelser och latinsk skrivning återanvänds. Rätt svar i repetition tar bort frågan från listan; fel svar ligger kvar. Ett nytt fel i ett vanligt quiz lägger tillbaka frågan även om lektionen tidigare klarats.

QuestionReviewState i Domain innehåller UserId, QuestionId, NeedsReview och LastAnsweredAt (UTC). Infrastructure konfigurerar tabellen och ett unikt index per konto och fråga. Migrationen AddQuestionReviewStates skapar en ny tom tabell. Gamla quiz sparade bara bästa totalpoäng, så inga tidigare fel kan återskapas: listan byggs från nästa quizförsök.

QuizProgressStore sparar frågornas senaste rättning. Vid hela quiz sparas också den bästa lektionspoängen och första klar-tiden; repetition ändrar aldrig dessa. Sparandet sker i en SQL-transaktion med ett applikationslås för samma konto och lektion så att samtidiga försök inte skapar dubbletter eller blandar ihop resultaten. Olika användares listor filtreras alltid med ID från den inloggade användaren, aldrig från formuläret.

QuizController.Review visar listan. SubmitReview validerar frågor, alternativ och antiforgery-token och rättar mot databasens facit. Om en annan flik redan ändrat listan får användaren öppna den aktuella listan. ReviewResult visar återkoppling efter redirect; omladdning visar bara sparat antal kvarvarande ord. Detta är senaste status per fråga, inte en full historik över alla försök.
## Lektion 3: Enkla vardagsfraser

Grundkursen har nu tre lektioner. Lektion 3 innehåller Jag förstår (teredi’uni), Jag förstår inte (aytered’anin), Jag vet inte (ayfeletikun) och Förlåt (yiqreta). Svenska betydelser är naturliga motsvarigheter till Omniglots fraser. Endast latinsk skrivning visas för tigrinjan, utan extra etikett på ordkorten. De tidigare inspelningarna har tagits bort; fraserna tränas nu utan ljud.

LearningSeedData innehåller det fasta innehållet. Migrationen SeedEverydayPhrasesLesson lägger till lektion 3, frågor 9–12 och alternativ 33–48. Den vanliga lektionsvyn, quizet, lektionsvägen, sparade framsteg och Öva på mina fel återanvänds. Tidigare frågor och framsteg ändras inte. Kursens totala antal tillgängliga lektioner ökar till tre.

Källa: https://www.omniglot.com/language/phrases/tigrinya.php . Ljudens originalfilnamn och användningsvillkor finns i wwwroot/audio/tigrinya/SOURCES.md.
## Modernare quiz och XP

Quizet visar med JavaScript en fråga i taget. Välj ett av fyra alternativ, välj Kontrollera och fortsätt efter återkopplingen. Förloppsraden räknar besvarade frågor. Träningen växlar mellan lyssning, läsning och svenska → tigrinja. Omvända alternativ skapas bara när alla fyra betydelser kan kopplas entydigt till fyra olika latinska uttryck; shimka/shimki ställs aldrig mot varandra som enda rätta svar på samma svenska fras. Repetition använder samma flöde för de kvarvarande frågorna.

QuizController.CheckAnswer rättar ett valt svar från databasen och kräver inloggning och antiforgery-token. Det sparar ingenting. quiz-run.js låser svaret i gränssnittet och visar återkoppling; efter sista frågan skickas alla val till den vanliga serverrättningen. Hela quizet valideras och rättas igen innan något sparas. Utan JavaScript fungerar formuläret med alla frågor samtidigt.

LearningRewards definierar XP: 10 XP per rätt i det bästa lektionsresultatet och 20 bonus-XP första gången hela lektionen klaras. Det ger högst 60 XP för en befintlig fyrfrågorslektion. Total XP beräknas från befintliga UserProgress-rader, även för äldre resultat. Ett förbättrat resultat ger bara skillnaden; återbesök, sämre försök, direkträttning och delrepetition ger inga extra XP. QuizProgressStore räknar belöningen under samma transaktion/lås som sparandet. Varje 100 XP höjer nivån. Resultatsidan och Mina framsteg visar nivå och XP-mätare. Ingen ny migration behövs.
## Diskret eritreanskt kulturtema

culture.css ger en varm pappersbakgrund, jordnära accenttoner och tunna geometriska bårder. En egenritad jebena finns vid appnamnet och som lågmäld dekor på startsidan och i sidans ytterkant. Dekoren i ytterkanten döljs på mindre skärmar. Vita frågekort, tydliga svarsknappar och återkopplingens gröna/röda färger behålls.

Layouten laddar kulturtemat efter grundstilarna. Motiven i wwwroot/images är lokala SVG-filer; inga externa bildanrop eller nya paket används. De är markerade som dekorativa och är inte klickbara. Formreferensen och designanteckningar finns i images/SOURCES.md. Kulturtemat ändrar inte quizlogik, XP, inloggning eller databas.
## Utökad vardagskurs: 13 lektioner och 54 uttryck

Tio nya lektioner ger 42 fler ord/fraser: småprat, hälsningar under dagen, hjälp i samtal, artighet, resa, grundtal 1–10, mat/dryck, familj och frågeord. De tidigare 20 Omniglot-inspelningarna har tagits bort. Alla 42 uttryck tränas nu genom läsning och översättning; ingen lyssningsfråga skapas utan ljud.

Varje lektion har ett konkret mål, separat kontext/tilltal och en muntlig situation under “Din tur att prata”. Övningen är för egen träning och bedömer inte tal automatiskt. Quizet använder fortsatt fyra alternativ, direkträttning, skyddat sparande, XP och repetition av fel. Sifferlektionerna har fem frågor och ger högst 70 XP; fyrfrågorslektioner ger 60 XP. Att göra allt perfekt i kursen ger 800 XP (nivå 9).

ExtendedLearningSeedData lägger till fasta ID:n. Migrationen ExpandEverydayLearning innehåller bara nya seedrader; gamla resultat och konton ändras inte. När databasen uppdaterats föreslår lektionsvägen lektion 4 för den som redan klarat de tre första. Migrationskommandot är `dotnet ef database update --project Infrastructure --startup-project TigrinyaLingo` från lösningsroten (med dotnet-ef 9.0.11).

Källor, tilltalsval, ordlista och begränsningar finns i LEARNING_CONTENT_SOURCES.md. Mer innehåll bygger ordförråd, men flytande tal kräver även återkommande lyssning och riktiga samtal.
## Lärande och quiz i samma lektion

För inloggade öppnar Lessons/Details och den äldre Quiz/Index-länken samma lektionsvy. För varje uttryck kommer först ett lärsteg med kort svensk betydelse, latinsk skrivning och inspelning om sådan finns. “Öva på uttrycket” döljer ordkortet och visar frågan. Efter serverns rättning går Fortsätt till nästa uttrycks lärsteg. Förloppsraden räknar rättade frågor, inte bara lästa ordkort. Sist får man en muntlig situation och väljer Visa mitt resultat för att spara.

LessonsController återanvänder QuizController.BuildQuiz och Quiz/Index-vyn. Formuläret anger uttryckligen Quiz som controller så att sparandet går rätt även på Lessons/Details-adressen. StudyMeaning används för undervisningen; betydelserna finns avsiktligt i HTML. IsCorrect och klientberäknade poäng används inte för sparande. Samma servervalidering, antiforgery, transaktioner, XP och personliga repetitionslistor gäller som tidigare. Inga framsteg sparas förrän hela flödet skickas in.

Öva på mina fel går direkt till frågorna utan lärkort och muntlig avslutning. Utan JavaScript visas lärkort och uppgifter tillsammans med det vanliga serverformuläret. Gäster kan fortfarande läsa och lyssna utan konto; en inloggningsknapp öppnar den kombinerade lektionen. Inloggning krävs fortsatt för frågor, rättning och sparade framsteg. Ingen migration eller ändring av tidigare resultat behövs.
## Dagens träning: blandad repetition

Dagens träning finns i menyn för inloggade och som en knapp på Mina framsteg. DailyTrainingController väljer upp till tio frågor från lektioner där kontot har ett sparat UserProgress-resultat. NeedsReview prioriteras, därefter äldst LastAnsweredAt. Datum (UTC), konto och fråge-ID bryter lika värden stabilt; detta är en första repetitionsmodell, inte ett system med beräknade minnesintervall. Om bara fyra kända uttryck finns får man fyra frågor. Ett nytt konto får en länk till sin första lektion. Man kan träna flera gånger samma dag.

Samma frågevy och direkträttning används, utan lärkort innan svaret. Ljud, läsning och omvänd översättning skapas från respektive ursprungslektion så att tilltalsformer förblir entydiga. Resultatet visar antal rätt, felbesvarade uttryck med rätt betydelse och länkar till deras repetition. Alla uttryck går att fälla ut. Dagens träning ger inga extra XP och ändrar inte bästa lektionsresultat eller klar-tid; bara senaste repetitionsstatus uppdateras.

Urvalet ligger i en Data Protection-skyddad SessionToken som binds till kontot och gäller i två timmar. Servern validerar alla fråge-ID:n, alternativ, träningsformer och antiforgery-token. Direkträttning sparar ingenting. Den slutliga inskickningen låser berörda lektioner i stigande ID-ordning i samma transaktion, med samma lås som vanliga lektioner. Senaste svarstid jämförs med urvalet innan något skrivs, så en dubbel inskickning eller en gammal flik inte skriver över nyare svar. Resultatet använder skyddad TempData och kan visas en gång; omladdning visar sparad repetitionsöversikt. Ingen ny tabell eller migration behövs.

## Lägga till egna ord och fraser manuellt

Befintliga fraser kan nu ändras via Redigera fraser i den lokala utvecklingsversionen (se avsnittet nedan). För vanliga nya fraser används nu Lägg till fras i appen. För utveckling av startinnehåll i kod gäller följande: innehållet för lektion 4 och framåt finns i Infrastructure/Data/ExtendedLearningSeedData.cs. De tre första lektionerna finns i LearningSeedData.cs.

1. Öppna ExtendedLearningSeedData.cs i Visual Studio och följ en befintlig lektion som exempel. Lesson samlar frågorna. Question.LatinText är det latinska tigrinjauttrycket. Question.Text innehåller originalskriften som källmaterial men visas inte i lärandet.
2. Lägg fyra Choice-rader per Question. Choice.Text är det svenska svarsalternativet. Exakt ett ska ha IsCorrect = true. Svarsalternativen ska vara olika och ha entydig betydelse. Använd nya, unika ID:n för varje rad; återanvänd aldrig tidigare ID:n för andra ord. I nuvarande grundinnehåll är största lektions-ID 1003, fråge-ID 1014 och alternativ-ID 4056. Kontrollera både kod och aktuell databas innan du väljer fasta seed-ID:n. Frågor och lektioner som skapats i formuläret får databasgenererade ID:n som inte syns i seedfilen; återanvänd inte dessa i senare migrationer.
3. För ljud: lägg en MP3 du har rätt att använda i TigrinyaLingo/wwwroot/audio/tigrinya och ange bara filnamnet i AudioFileName. Utan inspelning används null; quizet väljer då läsning/översättning. Anteckna språk- och ljudkällor. Prova uttal och tilltal med en modersmålstalare.
4. Skapa en ny migration, inte en ändring i en redan körd migrationsfil. Från lösningsroten med dotnet-ef 9.0.11: `dotnet ef migrations add AddMyPhrases --project Infrastructure --startup-project TigrinyaLingo`. Kontrollera den genererade migrationen innan du kör `dotnet ef database update --project Infrastructure --startup-project TigrinyaLingo`.
5. Starta om appen och prova lektionen. Ändra helst inte frågeantalet i en lektion som användare redan har klarat; skapa en ny lektion så att gamla resultat förblir begripliga. En lektion kan ha fler än fyra frågor, men varje fråga måste ha fyra svarsalternativ.

Att bara ändra C#-filen uppdaterar inte redan sparade databasrader. EF-migrationen är steget som för in innehållet i databasen. Lektionens valfria mål och tips ligger separat i TigrinyaLingo/Models/LessonPracticeGuide.cs. För ändring av befintliga fraser kan den lokala redigeringssidan användas i stället för detta manuella arbetsflöde.
## Lokal redigering av befintliga fraser

När du kör Development på den egna datorn och är inloggad visas Redigera fraser i menyn. Sök på svenska, tigrinja med latinska bokstäver eller lektionsnamn. Öppna en fras, ändra uttrycket och/eller dess fyra svenska svarsalternativ, markera rätt svar och välj Spara ändringar. Lektionssidan och quizet visar ändringen nästa gång de öppnas. Inga migrationskommandon behövs för sparandet i formuläret.

Uttalsinspelning kan väljas bland befintliga MP3-filer eller tas bort från frasen genom Ingen inspelning. När tigrinjatexten ändras och samma inspelning är vald kopplas den gamla inspelningen automatiskt bort vid sparandet. Väljs en annan befintlig ljudfil uttryckligen behålls det nya valet. Ändras bara svenska svar och tigrinjan är oförändrad behålls ljudet. Funktionen spelar inte in nytt ljud och kontrollerar inte talinnehållet i en ny vald fil. Ingen MP3-fil raderas. Lyssna på den befintliga inspelningen och kontrollera även eventuella användningstips när du ändrar ett uttryck. Nya fraser och lektioner skapas nu också i den lokala redigeraren via Lägg till fras, enligt avsnittet nedan.

Behörigheten LocalContentEditing kräver både inloggning, Development och en faktisk loopback-adress från anslutningen. Den är avstängd i Production och för anslutningar från andra datorer. Det är ett lokalt utvecklingsverktyg för den som använder datorn, inte ett rollbaserat administrationssystem för en publicerad app. HTTP-headers ger ingen redigeringsbehörighet. Alla åtgärder omfattas av samma policy, även exporten.

ContentEditorController binder bara LatinText, AudioFileName, svarstexter och korrekt alternativ. Frågans och alternativens ID:n, lektionskoppling och källskrift bevaras. Befintliga bästa resultat, XP och repetitionsstatus ändras inte. Fyra olika icke-tomma svar krävs, exakt ett korrekt alternativ och högst 200 tecken per text. Ljudfilen måste vara ett befintligt filnamn i ljudmappen; sökvägar accepteras inte. Razor kodar text vid rendering och POST kräver antiforgery-token.

Ett Data Protection-skyddat EditStamp innehåller konto, fråge-ID, SHA-256-fingeravtryck av innehållet och öppningstid. Det gäller i tolv timmar. Sparandet tar ett SQL-applikationslås för frågan i en transaktion, laddar aktuell version och jämför fingeravtrycket. En gammal flik visar konflikt (HTTP 409) med aktuell sparad text och behåller de inmatade ändringarna. Användaren måste granska och spara igen för att uttryckligen ersätta den nya versionen. Flera fråge-ID:n eller främmande svar kan inte skickas in i samma ändring.

Exportera fraser som JSON hämtar aktuella uttryck, svenska svar, ID:n, lektionsnamn och ljudfilnamn. Exporten innehåller inte ljudfiler, konton eller personliga framsteg. Automatisk import/återställning ingår inte i denna version. Exporten är bra att ta innan en större korrigeringsomgång.

Formuläret sparar i databasen, inte tillbaka i LearningSeedData eller ExtendedLearningSeedData. Dessa kodfiler och äldre migrationer beskriver startinnehåll för en ny databas. En nyinstallation får startinnehållet; radera eller återskapa därför inte databasen för att spara textändringar. Senare kodmigrationer som uttryckligen ändrar samma frasfält kan ersätta en redigering, så granska sådana migrationer. Ingen ny tabell eller migration införs för redigeraren. Användarens tidigare fraskorrigeringar och migrationer behålls.
## Skapa nya fraser och lektioner på sidan

Redigera fraser har nu knappen Lägg till fras. Fyll i tigrinjan med latinska bokstäver, fyra olika svenska svar och markera rätt svar. Välj en befintlig lektion utan sparade resultat, eller Skapa en ny lektion och ange namn och kurs. Uttalsinspelning är valfri och väljs bland befintliga MP3-filer. Spara ny fras skapar innehållet direkt i databasen och öppnar redigeraren, där man kan fortsätta med Lägg till en fras i lektionen eller Öppna lektionen. Användaren behöver inga SQL-kommandon, egna ID:n eller migrationskommandon. Inga nya tabeller eller kolumner införs.

PhraseCreationController validerar placering, fyra unika icke-tomma svar, rätt alternativ, textlängd och ljudfil. Lektionen och den första frågan med svar skapas i samma transaktion; ett avvisat formulär lämnar inga tomma lektioner. Databasen tilldelar nya ID:n. Frasen visas i vanliga lärkort och quiz direkt; efter ett sparat quiz kan den även väljas i Dagens träning. Exporten omfattar också egna fraser. Seedfiler och gamla migrationer är endast startinnehåll och skrivs inte om av formuläret.

Lektioner som har en UserProgress-rad för något konto kan inte få fler frågor genom formuläret, eftersom poäng och klar-status annars skulle hänvisa till ett äldre frågeantal. Befintliga fraser är fortfarande redigerbara. Skapa en ny lektion när en tidigare lektion är påbörjad. Fullständiga quiz tar nu ett delat lektionsinnehållslås och läser om frågorna före sparandet. Skapande av en fras tar exklusivt lås på samma lektion och kontrollerar sparade resultat under låset. Ett äldre quiz med fel frågelista kan därför inte felaktigt klara en utökad lektion. Repetition och blandad träning använder samma låsordning som tidigare.

Samma LocalContentEditing-policy gäller för skapandet: inloggat konto, Development och loopback. POST kräver antiforgery samt en Data Protection-skyddad engångsavsikt med konto, nonce och öppningstid (giltig i två timmar). Ett SQL-lås per nonce och minnescache för skapad frågas ID skyddar mot vanliga dubbelklick under den aktuella appkörningen. Cachen är inte en beständig skapandehistorik; efter omstart ska ett nytt formulär öppnas. Nya lektionsordningar tilldelas under kurslås. Ingen fil laddas upp eller skapas på filsystemet av sparandet.
## Spela med dina ord

Den inloggade menyn har nu Spela med Para ihop och Memory. Orden hämtas från lektioner som kontot har gjort quiz på, även egna fraser som skapats på sidan. Minst två entydiga par behövs. Högst tolv uttryck fördelas på upp till tre rundor med två till fyra par. Uttryck med samma normaliserade tigrinjatext eller svenska betydelse väljs inte tillsammans. Repetitionsbehov och äldsta svar prioriteras, sedan blandas korten.

Para ihop visar uttrycken direkt. Memory döljer korten tills de vänds. Båda använder egna spelpoäng: 100 för ett par utan tidigare fel, upp till 40 extra för rätt i följd, och 50 för ett par som tidigare missats. En klar omgång ger tre stjärnor utan fel, två vid högst lika många fel som par, annars en. Inga tidsgränser används för kortval och felvända kort ligger kvar tills användaren väljer Prova igen. Tillgängliga ljud kan spelas med en separat knapp. Ljud spelas aldrig automatiskt.

GamesController rättar varje par på servern. Kortens slumpnycklar delar inga fråge-ID:n mellan kolumnerna. En Data Protection-skyddad session binds till det inloggade kontot; antiforgery krävs för kortval och avslut. En spärr per session och request-ID gör nätverksomskickning idempotent. Poäng räknas aldrig från inmatade klientvärden. Aktiva omgångar och deras resultat ligger i IMemoryCache i högst en timme och försvinner när appen startas om. Spelpoäng är inte en beständig resultathistorik och ändrar inte XP eller bästa lektionspoäng. Vid flera appinstanser krävs en gemensam sessionslösning innan denna spelmodell används.

Först efter en färdig Para ihop-omgång och Visa resultat uppdateras kontots repetitionsstatus: ett par utan fel markeras rätt, ett missat par behålls för repetition. Memory sparar ingen repetitionsstatus eftersom fel där kan bero på kortens placering. Sparandet tar befintliga lektions-/användarlås och delade frågelås, jämför innehållets versionsfingeravtryck och svarstid med starten och avstår helt från uppdatering om någon fras eller något svar har ändrats under omgången. Dubbelt avslut skriver inte igen. Inga entities, tabeller eller migrationer behövs för spelen.

## AI-ljudtest – pausat

Tools/AiAudioTrial är ett separat tidigare experiment och används inte av appen. Addis AI är en betald tjänst och ska inte användas i det fortsatta arbetet. Inga API-anrop görs av appen. Ljudarbetet återupptas först med användarens egna lärarinspelningar.

## Borttagna Omniglot-inspelningar

Migrationen RemoveOmniglotRecordings rensar enbart AudioFileName-värden som matchar de 32 borttagna filnamnen. Den omfattar även egna fraser som återanvänt dessa ljud. Egna inspelningar med andra filnamn lämnas kvar. Frastexter, svenska svar, konton, resultat, XP och repetitionsstatus ändras inte. Seeddata och modellsnapshot har uppdaterats; tidigare körda migrationer behålls som historik. Vid nedgradering återkopplas inga borttagna ljud. I en annan databas körs Update-Database som vanligt för att tillämpa migrationen.
## Personlig lärväg och förkunskaper

Fortsätt lära (/Learn) samlar kursvägen, klara lektioner, XP och nästa rekommenderade aktivitet för det inloggade kontot. Repetitionsord rekommenderas först. Annars föreslås en påbörjad öppen lektion eller nästa öppna lektion, med kurser där kontot redan har framsteg prioriterade. När alla färdiga lektioner är klara föreslås spel och repetition. Påbörjade försök återupptas som en ny lektionsomgång; enskilda osparade kortsvar sparas inte mellan besök.

LearningPathService använder samma innehållskrav som quizet och sorterar varje kurs efter Lesson.Order och sedan Id. Ett nytt lektionsquiz är öppet när alla föregående färdiga lektioner i samma kurs är klara. Klar-status är den befintliga UserProgress.CompletedAt: alla rätt vid ett fullständigt quiz. Antalet försök är obegränsat. Tomt eller ofullständigt innehåll blockerar inte färdiga lektioner. Kurser har självständiga startpunkter. Redan klarade lektioner förblir öppna även om de klarades i en annan ordning före denna funktion. XP-nivå ger ingen genväg förbi lektionsordningen.

Kursöversikten och /Learn visar låsta quiz och vilket tidigare steg som krävs. Ordkorten är fortfarande läsbara, även när ett quiz är låst. Lessons/Details visar därför en ordkortsförhandsvisning för inloggade som saknar förkunskaper. Quiz/Index och Quiz/Submit kontrollerar samma regel på servern och svarar med en begriplig 403-vy vid låsning. Submit kontrollerar åter åtkomsten under den befintliga spartransaktionen innan något resultat skrivs. Direkträttning av ett nytt låst quiz avvisas; repetition av tidigare mötta ord fungerar fortfarande för äldre delvisa framsteg. Repetition och spel låser inte upp nya lektionsquiz eller skapar extra lektions-XP.

Regeln bygger på befintliga data och kräver ingen migration. Konton, gamla resultat, fraser och frågelistor ändras inte av att lärvägen läses. Ljudarbetet är fortsatt pausat och inga externa ljudtjänster anropas.

## Vardagssamtal

Spela har nu Vardagssamtal (/Conversations). Det finns tre lokala scener med fyra repliker: Ett första möte (lektion 1), När du behöver hjälp (lektion 3) och Hälsningar under dagen (lektion 5, tilltal till en man). Varje scen öppnas först när det egna kontot har klarat den angivna lektionen. Ett delvis sparat quiz räcker inte. Scenerna nås också från Fortsätt lära när det finns klara lektioner.

Samtalspartnerns repliker och situationer är svenskt lässtöd. Eleven väljer ett av fyra aktuella tigrinjauttryck från lektionen. Det är fasta övningar utan externa AI-anrop och utan ljud. ConversationCatalog innehåller bara scenens svenska sammanhang och förväntade svenska betydelser; frassvaren hämtas från Questions.LatinText och korrekta Choice.Text i databasen. Latinska frasändringar syns därför i nya omgångar direkt. Om betydelserna inte längre passar katalogen, eller om fyra entydiga olika uttryck saknas, stoppas scenen för innehållsgranskning. Det ersätter inte en modersmålstalares kontroll av uttal och språkbruk.

ConversationsController kontrollerar konto, antiforgery, skyddad sessionsnyckel, replikordning och alternativens slumpnycklar. Rätt svar ger 100 samtalspoäng; fel ger återkoppling och visar ett passande svar. Varje replik rättas en gång. Samma request-ID och svar kan skickas om efter nätverksfel utan dubbla poäng; ett ändrat svar med samma ID avvisas. Resultatet ger tre stjärnor för alla rätt, två för minst 75 procent rätt och annars en. Spelpoäng och stjärnor hör till omgången och ändrar inte XP, bästa lektionsresultat eller upplåsningar.

Först efter hela samtalet och Visa resultat uppdateras repetitionsstatus med samma QuizProgressStore som befintlig träning. Sparandet tar lektions-/användarlås och frågelås, jämför aktuellt frasinnehåll och LastAnsweredAt med starten och skriver ingenting om något har ändrats. Då behålls nyare repetition. En avslutad omgång kan skickas in igen utan nya skrivningar. Omgång och resultat ligger i minnescache i högst en timme och försvinner vid appomstart; redan sparad repetition finns kvar i databasen. Inga entities, tabeller eller migrationer tillkommer.

Fler scener kan senare läggas i ConversationCatalog: ange en lektion och fyra unika svenska betydelser med passande situationer. Granska sammanhang och tilltal med läraren. Behåll själva tigrinjafraserna i den vanliga redigeraren så att lärandet, spelen och samtalen använder samma aktuella innehåll.

## Tilltal: till en man eller en kvinna

Ordkort, kombinerade lektioner, quiz, felrepetition, Dagens träning, Para ihop, Memory och Vardagssamtal visar nu tilltal intill berörda fraser. (m) Till en man och (f) Till en kvinna avser mottagaren, inte talaren. Lärdelen visar granskad motform när den finns. Till exempel kemey aleka och kemey aleki har samma svenska betydelse men olika tilltal. Tilltal placeras inte i Choice.Text och ändrar inte rättningen. Befintliga entydighetsregler för quiz och spel behålls.

Question.Audience är ett nytt enumfält: Automatic, Man, Woman, SeveralOrPolite eller SameForBoth. Migrationen AddPhraseAudience lägger enbart till en int-kolumn med standardvärdet 0 (Automatic); frastexter, svar, ID:n, ljudkopplingar och personliga framsteg behålls. Befintlig databas behöver Update-Database innan den nya appversionen körs. Ingen ny migration behövs för varje senare ändring i redigeraren.

Redigera fraser och Lägg till fras har fältet Vem säger man frasen till? Automatiskt känner igen hela granskade latinska fraser via PhraseUsageGuide och visar ingen markering för okända uttryck. För egna uttryck och stavningsvarianter väljer författaren tilltal uttryckligen efter språkkontroll. Manuell märkning sparas i databasen, finns med i exporten och vinner över automatiken; en motform visas bara om den granskade frasen har samma tilltal. När tigrinjan ändras ska även detta fält granskas. Sidan visar nuvarande sparat tilltal. Samma lokala behörighetskontroll, enumvalidering, antiforgery och konfliktkontroll gäller. Tilltalsändring ingår i editor-, spel- och samtalsfingeravtryck. Gamla omgångar kan därför inte skriva över repetition efter en tilltalsändring.

Dagshälsningssamtalets tre böjda uttryck måste också passa det manliga sammanhanget; ändras de till kvinnliga eller ogranskade former stoppas scenen för granskning. Nya ljud ingår inte. Språkunderlag, avgränsningar och den särskilda resefrasen dokumenteras i LEARNING_CONTENT_SOURCES.md.

## Välj rätt tilltal

Spela och Fortsätt lära länkar nu till /AddressPractice. Här väljer eleven rätt manlig eller kvinnlig form i en svensk situation med svenskt dialogstöd och fyra latinska tigrinjaalternativ. Tilltalsetiketterna på alternativen döljs under frågan. Rättningen visar sedan rätt form och förklarar skillnaden mot den andra formen. Det är fasta övningar utan externa API-anrop eller ljud.

AddressPracticeStore använder elva granskade fraspar från PhraseUsageGuide. Minst en av parets former med rätt svensk betydelse måste finnas i en färdig lektion som det egna kontot har klarat. Motformen kan komma från den befintliga granskade guiden när den ännu inte har en egen databasrad. Egna okända fraser böjs inte automatiskt. Uttryck med motsägande manuell tilltalsmärkning väljs inte som källa. Kända fraser från klarade lektioner används som distraktioner. Det behövs två andra olika uttryck för att göra fyra entydiga alternativ.

En omgång innehåller upp till tre fraspar och övar varje par en gång till en man och en gång till en kvinna: två, fyra eller sex situationer. Sparat repetitionsbehov och äldre svar prioriteras före slumpning. Rätt svar ger 100 träningspoäng. Poäng och stjärnor gäller omgången, aldrig extra lektions-XP eller upplåsningar. När hela omgången avslutas uppdateras befintlig repetitionsstatus. Om två former bygger på samma databasfråga måste båda vara rätt för att dess repetitionsbehov ska försvinna. Det kan exempelvis gälla en kvinnlig motform som hittills bara finns i guiden.

Kontot, antiforgery, rättningsordning, slumpnycklar, skyddad sessionsnyckel och idempotenta request-ID:n kontrolleras på servern. Tilltal och Vardagssamtal har separata skyddsändamål och cacheprefix. Den gemensamma samtalsvyn och dess JavaScript återanvänds med en tilltalsrubrik och förklaring efter svaret. Aktiva omgångar och resultat lagras i minnescache i en timme.

Sparandet använder lektions-/användarlås och delade frågelås i stabil ordning. Innehållsfingeravtryck sparas för både fraskällor och använda distraktionsalternativ. Senaste svarstid kontrolleras för de frågor som faktiskt repeteras. Om någon av dessa uppgifter har ändrats skrivs inga gamla svar över nyare innehåll eller repetition. Dubbelt avslut skriver inte igen. Ingen databasändring eller migration behövs för detta steg. Utvecklingsordningen framåt finns i DEVELOPMENT_PLAN.md.

## Fler vardagsfraser och tidsformer (17 november 2026)

Grundkursen har nu 16 lektioner och 68 ord/fraser med 272 svarsalternativ. Tre nya lektioner ger 14 fraser: Fråga om språk, Korta svar och följdfrågor samt Svara på hälsningar och säga hej då. ConversationLearningSeedData använder nya lektions-ID:n 1001–1003, fråge-ID:n 1001–1014 och alternativ-ID:n 4001–4056. Tidigare fraser och korrigeringar uppdateras inte. Tilltalsguiden och den riktade tilltalsträningen omfattar nu också språkfrågorna, och du och dehan kun/kuni. Underlag och latinsk notation finns i LEARNING_CONTENT_SOURCES.md. Inga ljud tillkommer.

/Tenses är en offentlig introduktion med jämförelsen nu/tidigare/senare. Inga kompletta tigrinska verbgrupper seedas utan språkgranskning. Den lokala frasredigeraren har valfria fält för träningsgrupp (verb och person), tidsreferens, svenskt användningssammanhang, notering och granskningsmarkering. Samma fält finns när nya fraser skapas och i JSON-exporten. Sammanhanget beskriver när just formen används; tidsreferensen är en pedagogisk etikett, inte en automatisk grammatikanalys.

En grupp blir tillgänglig i Tidsformer först när den innehåller exakt tre olika latinska former, en per tidsreferens, alla markerade som granskade med sammanhang och giltiga svarsalternativ. Ogranskade grupper och brister visas endast för den lokalt behöriga redigeraren. Ogranskade tidsformsfraser hålls också utanför ordkort och gör lektionen ofärdig för quiz. Vanliga fraser utan tidsformsmetadata ändrar inte beteende. Publicering bygger på innehållsförfattarens granskningsmarkering, inte maskinell kontroll av språklig riktighet. Granskningen behöver kontrolleras på nytt vid innehållsändringar.

Gruppens tre jämförelsekort går att läsa utan konto. Tidsformsträningen kräver inloggning, klara källektionsresultat och minst ett annat känt uttryck för ett fjärde entydigt alternativ. Tre situationer rättas på servern och sparar befintlig repetition först när omgången är klar. Rätt svar ger 100 omgångspoäng; tre rätt ger tre stjärnor, två rätt två stjärnor. Lektions-XP och upplåsningar ändras inte. Skyddade sessioner, antiforgery, idempotens, ägarkontroll, innehållslås och versionskontroll återanvänds. Ändrat sammanhang eller återkallad granskning hindrar gamla omgångar från att skriva över repetition.

Vanliga lärkort och resultat visar tidsformstips för granskade fraser. Direkträttning i quiz och Dagens träning förklarar nu också tilltal och tidsform när underlag finns. Editorn låser lektionens innehåll före frågan vid sparande, i samma låsordning som övningarna.

Migrationen AddTenseLearningAndEverydayPhrases lägger till fem kolumner på Questions och de tre nya lektionerna med nytt innehåll. Den ändrar inte tidigare frastexter, svar eller personliga framsteg. Vid uppdatering av en befintlig databas ska de reserverade nya ID-intervallen först vara lediga. Alla nya frasändringar genom formuläret sparas därefter utan nya migrationer. Tidsformernas innehåll kan fyllas på efter språkgranskning; integrationstestens uttryckligen syntetiska verbformer används enbart i en tillfällig testdatabas och införs aldrig i appens startinnehåll eller användarens databas.
