namespace TigrinyaLingo.Models;

// Övningshjälp hålls skild från de korta betydelser som quizet rättar.
public record LessonPractice(string Goal, string Context, string Challenge);

public static class LessonPracticeGuide
{
    public static LessonPractice? For(int lessonId) => lessonId switch
    {
        1 => new("Hälsa, välkomna, tacka och säga hej då.",
            "Lär in uttrycken som hela hälsningar. Läs uttrycket och försök sedan minnas betydelsen utan att titta.",
            "Hälsa på en vän med selam. Välkomna personen med merhaba, tacka med yekenyeley och avsluta med selamat."),
        2 => new("Presentera dig och fråga vad någon heter.",
            "I presentationsfrasen ersätter du punkterna med ditt eget namn. Menyu shimka används när du frågar en man; menyu shimki när du frågar en kvinna. Det är personen du talar till som avgör formen. I ane kab … metsiye fyller du i platsen du kommer från.",
            "Hälsa och presentera dig med ditt eget namn. Fråga sedan vad den andra personen heter och berätta var du kommer ifrån."),
        3 => new("Säga om du förstår och använda en enkel ursäkt.",
            "Lägg märke till skillnaden mellan teredi’uni och aytered’anin. Öva dem tillsammans så att du kan välja rätt i ett samtal.",
            "Föreställ dig att du inte förstår. Säg förlåt och jag förstår inte. Låtsas sedan att personen förklarar igen och säg jag förstår."),
        4 => new("Fråga hur någon mår och var personen kommer ifrån.",
            "Aleka och metsika används till en man; aleki och metsiki till en kvinna. Betydelsen är densamma. Välj form efter personen du talar till.",
            "Spela två roller: hälsa med selam, fråga hur personen mår och fråga sedan var personen kommer ifrån. Byt tilltalsform och prova igen."),
        5 => new("Välja en hälsning som passar tiden på dagen.",
            "De tre dagshälsningarna här används till en man. Till en kvinna säger man kemey hadirki, kemey wu’elki och kemey amsiki. De fungerar som hälsningar och frågar samtidigt hur personen har haft det. Senay leyti används när man önskar god natt.",
            "Tänk dig att du träffar en vän på morgonen, eftermiddagen och kvällen. Säg en passande hälsning varje gång och avsluta med god natt."),
        6 => new("Be om hjälp så att du kan fortsätta ett samtal.",
            "De tre uppmaningarna här riktas till en man. Till en kvinna: kes elki tezarebi bejaki, eske digemiley och tsehafiyo bejaki. Ewe kurub kurub betyder ja, lite grann och passar när någon frågar om du kan ett språk.",
            "Låtsas att en person talar för snabbt. Be personen att tala långsammare, säga det igen och skriva ner det. Försök säga fraserna utan att titta."),
        7 => new("Be artigt, svara på ett tack och avsluta ett möte.",
            "Bejaka används till en man; bejaki till en kvinna. Genzebka är ett svar på tack till en man, genzebki till en kvinna. Det svenska varsågod betyder här ingen orsak, inte här har du. Dehan w’alu är en artig form eller ett tilltal till flera.",
            "Öva en kort scen: säg trevligt att träffas, tacka med yekenyeley, svara med genzebka eller genzebki och önska en bra dag."),
        8 => new("Fråga om pris och toalett och använda två vanliga lyckönskningar.",
            "Lär in frågorna som hela uttryck. Bdehan eto är frasen för trevlig resa i den här lektionen. Latinsk skrivning är ett lässtöd; träna först på uttryckens betydelse.",
            "Peka på en sak och fråga vad den kostar. Fråga sedan efter toaletten. Tänk dig en måltid och en avresa och välj rätt uttryck."),
        9 => new("Känna igen och säga talen ett till fem.",
            "Här tränar du grundtal. Skrivningen med latinska bokstäver kan variera mellan källor; kilte motsvarar också skrivningen kelete. Dessa ord har ännu ingen inspelning i appen.",
            "Räkna fem föremål. Titta sedan på olika antal fingrar och säg talet direkt, utan att först räkna på svenska."),
        10 => new("Fortsätta räkna till tio och känna igen enskilda tal.",
            "Öva först i ordning, sedan blandat. Apostrofen hjälper till att skilja ljud i läsningen. Dessa ord har ännu ingen inspelning i appen.",
            "Räkna från ett till tio med förra lektionen. Be någon visa mellan sex och tio fingrar, eller skriv siffror på små kort och dra ett i taget."),
        11 => new("Känna igen fyra vanliga ord vid matbordet.",
            "May är vatten, bani bröd, bun kaffe och tseba mjölk. Börja med att känna igen orden i vardagen. Inspelningar för de här orden kommer senare.",
            "Peka på vatten, bröd, kaffe och mjölk hemma och säg orden. Täck sedan över de latinska orden och försök minnas dem från de svenska betydelserna."),
        12 => new("Tala om de närmaste familjemedlemmarna.",
            "Det här är grundord för familjerelationer. De betyder inte min mamma eller min bror; ägande behöver en egen form. Hafti kan också skrivas hawti i vissa källor. Inspelningar kommer senare.",
            "Tänk på eller rita en familj. Peka ut mamma, pappa, bror och syster och säg orden utan att läsa."),
        13 => new("Känna igen vem, vad, var och när i frågor.",
            "Frågeorden hjälper dig att uppfatta vad någon vill veta. Men frågar om en person, entay om en sak, abey om en plats och me’as om en tid. Ett frågeord är inte alltid en hel mening. Inspelningar kommer senare.",
            "Välj rätt frågeord för fyra svar: en person, en sak, en plats och en tid. Lyssna sedan efter abey i frasen abey alo shnti bet från reselektionen."),
        1001 => new("Fråga vilket språk någon pratar.", "Tzarebdo används till en man och tzarebido till en kvinna i de här frågorna. Englizgna är engelska och tigrigna är tigrinja. Träna hela frågan, inte ord för ord.", "Fråga två personer om de pratar tigrinja eller engelska. Byt mellan manligt och kvinnligt tilltal."),
        1002 => new("Ge ett kort svar och fråga tillbaka.", "Ewe och ayfal är korta svar. Bwegenka ke används när du frågar en man och bwegenki ke när du frågar en kvinna. Här används och du som en följdfråga efter att du har berättat något om dig själv.", "Svara ja eller nej på en språkfråga. Berätta sedan hur du mår och fråga tillbaka med rätt tilltalsform."),
        1003 => new("Svara på en hälsning och avsluta ett möte.", "Tsbuq aleku passar som svar när någon frågar hur du mår. Tsbuq är det korta bra. Dehan kun riktas till en man, dehan kuni till en kvinna och dehan kunu till flera eller som artigt tilltal. Dahray yerakbena används som vi ses.", "Spela upp ett kort möte: hälsa, fråga hur personen mår, svara och säg hej då. Prova sedan samtalet med ett annat tilltal."),
        2001 => new("Berätta om en måltid som pågår, är avslutad eller väntar.",
            "Jämför hela formerna för jag. Aleku används här när något pågår. Beli'e gäller här en avslutad måltid. Kbel'e eye används för det du ska göra senare. Sammanhanget avgör hur dåtidsformen bäst översätts.",
            "Tänk dig själv vid matbordet nu, efter måltiden och före nästa måltid. Välj en av lektionens tre fraser för varje situation."),
        2002 => new("Berätta att du är på väg, gick tidigare eller ska gå senare.",
            "Ykeyd aleku beskriver att du är på väg just nu. Keyde gäller här en avslutad förflyttning och kkeyd eye något du planerar. Gå betyder här att bege sig; formen säger inte att du måste gå till fots.",
            "Föreställ dig en utflykt: först är du hemma, sedan är du på väg och till sist berättar du om besöket. Säg rätt fras vid varje steg."),
        2003 => new("Skilja mellan att prata nu, ha pratat och prata senare.",
            "Öva yzareb aleku, tezaribe och kzareb eye tillsammans. Här gäller nutidsformen ett pågående samtal. Jag-formen ändras inte efter om talaren är man eller kvinna.",
            "Tänk på ett telefonsamtal. Välj frasen för ett samtal som pågår, ett som är avslutat och ett som ännu inte har börjat."),
        _ => null
    };
}
