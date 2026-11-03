# BatchBaker – brugervejledning

BatchBaker opretter brugere i Active Directory, enten én ad gangen eller mange på én gang fra en CSV-fil. Du behøver ikke PowerShell eller Active Directory Users and Computers.

## Før du starter

- Computeren skal være medlem af domænet.
- Din Windows-konto skal have rettigheder til at oprette brugere og sætte adgangskoder i afdelingernes OU'er. BatchBaker bruger den konto, du er logget på med, og beder aldrig om en adgangskode.
- Ved start tjekker BatchBaker forbindelsen og afdelingernes OU'er. Du får kun en besked, hvis noget er galt.

## Opret én bruger

1. Vælg **Filer > Tilføj bruger**.
2. Udfyld felterne. **Brugernavn**, **Afdeling** og **Midlertidig adgangskode** er påkrævet. Adgangskoden er allerede udfyldt, og **Generér** laver en ny.
3. Lad **Tving skift af adgangskode ved første login** være slået til, medmindre der er en særlig grund til andet.
4. Klik **Gem** i formularen, og følg derefter afsnittet *Gem og opret brugerne*.

## Opret mange brugere fra en CSV-fil

1. Åbn **BatchBaker-skabelon.xlsx**, og udfyld arket **Brugere** med én bruger pr. række.
2. Gem som **CSV UTF-8 (kommasepareret)**, mens du står på arket Brugere.
3. Vælg **Filer > Importér** i BatchBaker, og vælg filen.
4. Forhåndsvisningen viser, hvor mange rækker der er gyldige. Dobbeltklik på en række markeret **Ugyldig** for at rette den. Årsagen står i kolonnen **Årsag**. Rækker, der ikke skal med, kan du markere og fjerne med **Kassér valgte**.
5. Klik **Importér gyldige rækker**. Ugyldige rækker kommer ikke med.

Alle importerede brugere får en ny, genereret adgangskode og skal skifte den ved første login. Det kan ændres for den enkelte bruger ved at dobbeltklikke på rækken.

## Gem og opret brugerne

1. Vælg **Filer > Gem**. Brugerne gemmes i BatchBakers database.
2. Svar **Ja**, når du bliver spurgt, om brugerne skal oprettes i Active Directory.
3. Til sidst vises, hvor mange der blev oprettet, og mappen med PDF'erne åbnes.

Hver bruger får en PDF med brugernavn og midlertidig adgangskode. Du finder dem i mappen **PDF** på skrivebordet, i en undermappe for hver kørsel. **PDF'erne indeholder adgangskoder, så giv dem kun til den rette bruger, og slet dem bagefter.**

## Hvis brugere bliver afvist

Hvis brugere bliver afvist, fx fordi brugernavnet allerede findes, åbner vinduet **Ret afviste brugere** med netop de brugere. Det sker både før brugerne gemmes og efter oprettelsen i Active Directory.

- **Stavefejl:** dobbeltklik på rækken, og ret den.
- **Reel dublet:** markér en eller flere rækker, og klik **Kassér valgte**. Det fjerner kun rækken fra BatchBaker, aldrig en konto i Active Directory.
- **Fortsæt** går videre, og efter en kørsel i Active Directory prøves kun de rettede brugere igen.
- **Annullér** beholder alle rækker, som de er. Du kan prøve igen med **Filer > Gem**.

Brugere, der allerede er oprettet, springes over.

## Se brugere

Listen **Vis** i venstre side bestemmer, hvad der vises:

- **Denne batch**: de brugere, du arbejder på. Kun de kan rettes.
- **Oprettet med BatchBaker**: alle brugere, BatchBaker har gemt.
- **Active Directory**: de brugere, der lige nu findes i afdelingernes OU'er. **Opdatér** henter dem igen.

Klik på en afdeling i træet for kun at se dens brugere.

## Log

**Log** i menulinjen viser alt, hvad BatchBaker har gjort: importer, rettelser, oprettede brugere, PDF'er og fejl, med de tekniske detaljer.

## Hvis noget går galt

| Besked | Hvad du kan gøre |
|---|---|
| Der kan ikke oprettes forbindelse til domænecontrolleren | Tjek netværket, og at serveren kører. |
| Manglende rettigheder | Log på med en konto, der må oprette brugere i OU'en. |
| Brugernavnet findes allerede i Active Directory | Ret brugernavnet, kassér rækken, hvis det er en reel dublet, eller slet den gamle konto, hvis den er overflødig. |
| Adgangskoden opfylder ikke domænets adgangskodepolitik | Dobbeltklik på rækken, og klik **Generér**, eller skriv en stærkere adgangskode. |
| Ukendt afdeling | Dobbeltklik på rækken, og vælg afdelingen på listen. |
| Kolonnerne i filens første række er ikke som forventet | Brug skabelonen, så kolonnerne står i den rigtige rækkefølge. |
| Disse OU'er findes ikke | Ret OU-stierne i `appsettings.json`, eller opret OU'erne. |

Vælg **Administrér > Kontrollér domæne og OU'er** for at tjekke forbindelsen igen. **Administrér > Åbn datamappe** åbner mappen med `crash.log`, hvis programmet melder en uventet fejl.

## CSV-format

Første række skal være disse kolonnenavne, i denne rækkefølge, adskilt af komma eller semikolon:

`Fornavn, Efternavn, Brugernavn, Email, Afdeling, Land, Titel, Telefonnummer`

| Kolonne | Regler |
|---|---|
| Brugernavn | Påkrævet og unikt. Højst 20 tegn, uden mellemrum og tegn som `@`, `,` og `;`. |
| Afdeling | Påkrævet. Programmør, Infrastruktur, IT Support, Cyber Security, Administration, Handel, Økonomi, Automatik, Elektronik eller Elektriker. |
| Land | Tomt felt bliver til Denmark. |
| Titel | Kolonnen skal være der, men værdien bruges ikke. |
| Fornavn, Efternavn, Email, Telefonnummer | Valgfrie. |

En eventuel niende kolonne med adgangskoder ignoreres.

## Forbindelse til Active Directory

Indstillingerne står i `appsettings.json` i programmappen. Genstart BatchBaker efter ændringer.

| Indstilling | Betydning |
|---|---|
| `Domain` | Domænet. Tomt betyder det domæne, computeren er medlem af. |
| `DomainControllers` | Domænecontrollere, der prøves i rækkefølge, fx `["dc01.firma.local", "dc02.firma.local"]`. Tomt betyder, at Windows vælger. |
| `Departments` | OU'en for hver afdeling. Domænedelen (`DC=...`) tilføjes automatisk. |
| `PDF:FilePath` | Mappe til PDF'er. Tomt betyder mappen `PDF` på skrivebordet. |

Filen ligger i programmappen, så du skal normalt åbne din editor som administrator for at gemme ændringer.
