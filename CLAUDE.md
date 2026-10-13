# CLAUDE.md — Textil Robno planiranje

Interni alat za planiranje prodaje po SKU i mesecima. Nabavka šalje listu SKU, komercijalisti unose planirane količine, vođe prodaje i direktor prodaje odobravaju, konsolidovan plan se vraća nabavci kao fajl. Zamenjuje razmenu Excel tabela mailom.

Jezik komunikacije sa korisnikom: srpski. Jezik koda, identifikatora i komentara u kodu: engleski. UI tekstovi: srpski (latinica).

## Stek i okruženje

- ASP.NET Core 8, **Razor Pages** (ne MVC, ne Blazor, ne SPA). Server-rendered + minimalan vanilla JS (autosave tabele, modali). Bez frontend build pipeline-a (bez npm/webpack/vite).
- EF Core 8 + **SQL Server Express** (već postoji na serveru). Migracije u repou.
- ClosedXML za čitanje/pisanje xlsx; CsvHelper ili ručni writer za CSV.
- Auth: ASP.NET Identity sa cookie auth, lokalni nalozi. Prijava preko B2B platforme je planirana kasnije — ne implementirati, ali auth držati iza jednog servisa da se može zameniti.
- Email: SMTP preko `IEmailSender`, podesivo u `appsettings`. U developmentu loguje u konzolu.
- Hosting: IIS na internom Textil serveru iza VPN-a, isto kao postojeći alat TeamTalk (C#). Deploy = `dotnet publish` → folder. Bez Dockera.
- Logovanje: Serilog u fajl, dnevna rotacija.

## Struktura projekta

Jedan projekat `Textil.Planning` (nema potrebe za više slojeva za ovaj obim) + `Textil.Planning.Tests` (xUnit):

```
/Data        DbContext, entiteti, migracije, seed
/Domain      status mašine, pravila dozvola, servisi (CycleService, PlanService, ApprovalService, ImportService, ExportService)
/Pages       Razor Pages po ulozi: /Procurement, /Plan, /Team, /Director, /Admin
/Services    email, trenutni korisnik, audit
/wwwroot     css, js (jedan fajl po stranici koja ima JS)
/docs        spec, prototip, primeri fajlova
```

Pravila za ceo kod:
- Sva pravila dozvola i statusa se proveravaju u Domain servisima, nikad samo u Razor stranici. Stranica sakriva dugme; servis odbija akciju.
- Svaka izmena količine, statusa, uploada i exporta ide u `AuditLog`.
- Nema brisanja: SKU, batch-evi i ciklusi se ne brišu, ni soft.

## Rečnik (srpski → engleski u kodu)

| Domen | Kod |
| --- | --- |
| ciklus | `Cycle` |
| slanje / batch | `Batch` |
| SKU u ciklusu | `CycleSku` |
| stavka plana | `PlanEntry` |
| odobrenje | `Approval` |
| komentar | `Comment` |
| nabavka (Mikula) | role `Procurement` |
| komercijalista | role `Sales` |
| vođa prodaje | role `TeamLead` |
| direktor prodaje | role `Director` |

Jedan korisnik može imati više uloga: vođa je i `Sales` i `TeamLead`, jer pravi i sopstveni plan.

## Model podataka

- `User`: Identity user + `DisplayName`, `ManagerId` (nullable; kome šalje plan na odobrenje — vođa za komercijalistu, direktor za vođu, null za direktora i nabavku).
- `Cycle`: `Id`, `Name`, `Deadline` (datetime), `Status` (`Open | Locked | Finalized`), `CreatedById`, `CreatedAt`, `FinalizedAt`. Meseci u tabeli `CycleMonth` (`CycleId`, `Month` kao `YYYY-MM`, `Order`).
- `Batch`: `Id`, `CycleId`, `Number` (redni broj u ciklusu), `UploadedById`, `UploadedAt`, `OriginalFileName`, `OriginalFile` (varbinary — čuva se original).
- `CycleSku`: `Id`, `CycleId`, `BatchId`, `Code`, `Order`. **Unique (CycleId, Code).** Samo šifra, bez naziva — odluka klijenta.
- `PlanEntry`: `Id`, `CycleSkuId`, `UserId`, `Month`, `Quantity` (int ≥ 0, null = 0), `CorrectedQuantity` (nullable), `CorrectedById`, `CorrectedAt`. **Unique (CycleSkuId, UserId, Month).** Važeća vrednost = `CorrectedQuantity ?? Quantity ?? 0`.
- `Approval`: `Id`, `BatchId`, `UserId`, `Status` (`Draft | Submitted | Returned | Approved`), `ChangedById`, `ChangedAt`, `Note`. **Unique (BatchId, UserId).** Kreira se u `Draft` za svakog planera kad nastane batch.
- `Comment`: `Id`, `CycleSkuId`, `UserId` (vlasnik plana), `Month`, `AuthorId`, `Text`, `CreatedAt`. Vezan za ćeliju, ne za PlanEntry, jer ćelija može biti prazna.
- `AuditLog`: `Id`, `Entity`, `EntityId`, `Action`, `UserId`, `At`, `OldValue`, `NewValue` (JSON).

Konsolidacija nije tabela: `SUM(važeća količina) GROUP BY Code, Month` po ciklusu, plus total po SKU. Uvek mora da se može razbiti po korisniku.

## Status mašine

**Cycle:** `Open → Locked` (automatski po isteku `Deadline`, ili direktor ručno) · `Locked → Open` (direktor, "otključaj radi ispravke") · `Locked → Finalized` (direktor, nepovratno). Promena roka samo dok je `Open`.

**Approval** (po korisniku, po batch-u): `Draft → Submitted` (vlasnik plana) · `Submitted → Returned` (manager; zahteva bar jedan komentar na nekoj ćeliji tog batch-a) · `Returned → Submitted` (vlasnik) · `Submitted → Approved` (manager). Novi batch ne dira odobrenja postojećih batch-eva.

Istek roka: `IHostedService` koji na svakih N minuta zaključava cikluse čiji je `Deadline` prošao. Pri svakom upisu se dodatno proverava `Deadline` — ne oslanjati se samo na servis.

Finalizacija sa neodobrenim planovima je dozvoljena uz eksplicitnu potvrdu; ti planovi ulaze sa trenutnim važećim vrednostima i obeležavaju se.

## Matrica dozvola

| Akcija | Procurement | Sales | TeamLead | Director | Cycle status |
| --- | --- | --- | --- | --- | --- |
| Kreiraj ciklus, menjaj rok | – | – | – | da | rok samo dok Open |
| Upload batch-a | da | – | – | – | Open |
| Izmeni/obriši SKU | – | – | – | – | nikad |
| Unos količina (svoj plan) | – | Draft/Returned | Draft/Returned | – | Open |
| Korekcija količina tuđeg plana | – | – | svoj tim, Submitted | planovi vođa, Submitted | Open |
| Komentar na ćeliju | – | – | svoj tim | planovi vođa | Open |
| Submit svog plana | – | da | da | – | Open |
| Return / Approve | – | – | svoj tim | planovi vođa | Open |
| Vidi konsolidaciju | posle Finalized | – | svoj tim | sve + po korisniku | – |
| Lock / Unlock | – | – | – | da | – |
| Finalize | – | – | – | da | Locked |
| Export | da | – | – | da | Finalized |
| Audit log | – | – | – | da | – |
| Admin korisnika i hijerarhije | – | – | – | da | – |

Sales vidi samo sopstveni plan. TeamLead vidi sebe i korisnike čiji je `ManagerId` = on. Director vidi sve. "Manager" u tekstu iznad = korisnik iz `ManagerId`.

## Fajlovi

**Import (nabavka → alat), xlsx:** prvi sheet, red 1 = zaglavlje `SKU | 2027-01 | 2027-02 | ...`, dalje jedna šifra po redu, ćelije meseca prazne. Validacije: meseci u zaglavlju `YYYY-MM`, hronološki, bez rupa; prvi batch u ciklusu definiše mesece ciklusa, svaki sledeći mora imati identične mesece (inače import odbijen); duplikati unutar fajla ili već postojeći u ciklusu se preskaču uz upozorenje sa listom; prazan fajl odbijen; redosled SKU iz fajla se čuva (`Order`).

**Export (alat → nabavka), CSV:** UTF-8, zaglavlje `SKU,2027-01,...,Total`, jedan red po SKU iz svih batch-eva ciklusa (i oni sa 0), redosled kao pri uploadu. Separator (`,` ili `;`) i prisustvo kolone `Total` su **konfigurabilni** u `appsettings`, jer firma koja održava Mikulin Excel alat još nije potvrdila šta njihov import očekuje. Default: zarez, sa Total.

Uz CSV i xlsx sa dva sheet-a: `Plan` (konsolidacija) i `Po prodavcu` (ista tabela + kolona Prodavac, samo redovi sa količinom > 0). Naziv fajla: `Plan-<ciklus>_final_<datum>.<ext>`. Primeri oba fajla su u `/docs/samples/`.

## UI pravila

- Tabela plana: SKU u redovima grupisani po batch-u, meseci u kolonama, Total na kraju reda. Input `type=number min=0 step=1`. Autosave na `input` sa debounce ~500 ms preko malog JSON endpointa, bez dugmeta "Sačuvaj". Fokus se ne gubi pri čuvanju — ažurirati samo ćeliju i Total, ne re-renderovati tabelu.
- Korigovana vrednost: crveni okvir, originalna ostaje vidljiva precrtana ispod, sa imenom ko je korigovao. Komentar se otvara ikonicom na ćeliji; ćelija sa komentarom je označena.
- Zaključan ciklus: svi inputi `disabled` + traka upozorenja. Nikad ne sakrivati podatke, samo onemogućiti izmenu.
- Konsolidacija: red po SKU, klik proširuje na redove po korisniku; SKU bez količine nosi oznaku "nije planiran"; korisnik bez odobrenog plana nosi oznaku "nije odobreno".
- Dizajn: crno/belo/crvena `#e3001b`, serif naslovi, sans tekst (prati Textil B2B sajt). `/docs/prototype.html` je referenca za izgled i tok — ne kopirati njegov JS, to je demo sa stanjem u memoriji.

## Notifikacije (email)

- Sales/TeamLead: novi batch stigao; plan vraćen; rok za 2 dana.
- Manager: podređeni poslao plan.
- Director: svi planovi tima odobreni; ciklus zaključan.
- Procurement: ciklus finalizovan, export spreman.

## Van obima V1 — ne implementirati bez dogovora

- Prijava preko B2B naloga (V2).
- Veza sa ERP-om (Informix kod provajdera, Textil nema pristup bazi — nije izvodljivo) i sa B2B platformom.
- Naziv artikla, stanje zaliha, istorija prodaje uz SKU.
- Preračun nabavke (ostaje u Mikulinom Excel alatu).
- Međuexport pre finalizacije; kopiranje prošlog ciklusa; uvoz korisnikovog Excela.

## Test nalozi (seed u developmentu)

`mikula` (Procurement), `tijana` i `marko` (Sales, ManagerId → milan), `milan` (Sales + TeamLead, ManagerId → pop), `pop` (Director). Lozinka za sve u dev: `Textil123!`. Seed i jedan otvoren ciklus "Plan 2027", meseci 2027-01..2027-06, batch #1 sa 5 SKU i nekoliko popunjenih planova, da aplikacija ima podatke odmah po pokretanju.

## Redosled razvoja

1. Projekat, Identity, uloge, seed, admin korisnika i hijerarhije.
2. Cycle + Batch + import xlsx sa validacijama.
3. Tabela plana sa autosave i Submit.
4. Tim: pregled, korekcija, komentar, Return/Approve.
5. Direktor: status, konsolidacija, Lock/Unlock/Finalize.
6. Export CSV + xlsx.
7. Email notifikacije, hosted service za rok.
8. Audit log pregled, publish profil za IIS.

Domain servisi imaju unit testove (xUnit) za status mašine, matricu dozvola, import validacije i konsolidaciju — to je obavezan minimum. UI testovi nisu obavezni. Pre svakog koraka: `dotnet test` prolazi. Jedan korak = jedan commit sa opisom na engleskom.
