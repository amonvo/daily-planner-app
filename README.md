# Daily Planner

Osobní denní plánovač v .NET MAUI 10. Denní rozvrh, splněné aktivity, poznámky a přehledy za týden, měsíc a rok jsou uložené lokálně v SQLite.

## Co aplikace umí

- Dnešní plán s aktuální aktivitou, další aktivitou a pokrokem povinných bloků.
- Zaznamenání splnění, přeskočení a zpětná editace dne v kalendáři.
- Zvláštní dny, které se nezapočítávají do statistik, a střídavé táborské víkendy.
- České státní svátky vypočítané podle zvoleného roku včetně Velikonoc.
- Pracovní a víkendové aktivity s upravitelným názvem, časem a textem připomenutí.
- Světlý, tmavý a systémový vzhled, nastavitelný na stránce Nastavení.
- JSON záloha a obnova rozvrhu, poznámek a splnění. Export denních poznámek za aktuální rok do CSV.
- Mobilní lokální notifikace po povolení systémového oprávnění.

## Spuštění

Vyžaduje .NET 10 SDK, odpovídající MAUI workload a platformní nástroje. Pro Android použijte SDK API 36 a zařízení/emulátor Android 8.0 nebo novější. Pro Windows použijte Windows 10/11 s MAUI Windows nástroji.

```sh
git clone https://github.com/amonvo/daily-planner-app.git
cd daily-planner-app
dotnet workload install maui-android
dotnet build PlannerApp/PlannerApp.csproj -f net10.0-android -p:TargetFrameworks=net10.0-android
```

Ve Visual Studiu otevřete `PlannerApp/PlannerApp.sln`, vyberte Android emulátor nebo USB zařízení a spusťte projekt. Android APK je také dostupné mezi artefakty úspěšného běhu GitHub Actions. Jde o vývojový balíček podepsaný testovacím klíčem, určený k ověření aplikace.

Windows sestavení na Windows hostiteli

```sh
dotnet workload install maui-windows
dotnet build PlannerApp/PlannerApp.csproj -f net10.0-windows10.0.19041.0 -p:TargetFrameworks=net10.0-windows10.0.19041.0
```

Na Windows umístěte pracovní kopii do krátké cesty. Android nástroje mohou při dlouhé cestě projektu selhat při zpracování prostředků. Lokální ověření použilo kratší `BaseIntermediateOutputPath` a `BaseOutputPath`.

## Práce s daty

Identifikátor aplikace a název databáze `plannerapp.db3` zůstávají kvůli návaznosti na předchozí instalace. Při upgradu SQLite doplní nové sloupce a zachová existující záznamy. Táborské bloky ze starších verzí se nemažou. Souběžné vytváření dne a ukládání splnění je serializované.

V Nastavení lze upravit existující aktivitu. Časy mohou přecházet přes půlnoc, sousední bloky se mohou dotýkat, ale překryvy jsou odmítnuté. Změny zachovávají identifikátory aktivit a povinnost aktivity. Historické přehledy používají současný rozvrh, proto změny časů a názvů platí i pro zobrazení starších dní.

Před obnovou vytvořte aktuální JSON zálohu. Obnova po potvrzení nahradí obsah databáze atomicky. Neplatná verze, duplicitní záznamy a neexistující vazby jsou odmítnuté. Zálohy obsahují osobní poznámky v čitelné podobě, ukládejte je do chráněného úložiště. CSV je přehled poznámek a zvláštních dní, plnou obnovu umožňuje JSON.

Notifikace závisejí na oprávněních, úsporách baterie a podpoře Android/iOS. Windows verze je určena pro plán a přehledy. iOS a Mac Catalyst targety jsou zachované, jejich ověření vyžaduje Mac a odpovídající Apple nástroje.

## Testy a CI

```sh
dotnet test tests/PlannerApp.Tests/PlannerApp.Tests.csproj
```

Regresní testy ověřují Velikonoce ve více letech, přelom roku, půlnoc, překryvy, souběžné ukládání, starší SQLite schéma a obnovu zálohy. CI spouští testy na Linuxu a sestavuje Android a Windows. Datum, stav nativního připomenutí a skutečný životní cyklus aplikace je vhodné ověřit i na fyzickém telefonu.

## Struktura

`PlannerApp/Views` obsahuje obrazovky, `ViewModels` stav a příkazy, `Services` databázi a notifikace, `Helpers` kalendář a časové výpočty a `Models` SQLite záznamy. `tests/PlannerApp.Tests` testuje skutečnou databázovou službu bez mobilního prostředí.

Projekt je určen pro osobní použití. Samostatná licence pro další distribuci zatím nebyla stanovena.

## Přehled a grafy

Úvodní obrazovka ukazuje dnešní priority, aktuální aktivitu a trend sedmi dní. Filtry Celý den, Zbývající a Priority zjednodušují plán. Poslední změnu stavu lze vrátit. Týdenní, měsíční a roční grafy počítají pouze zaznamenané povinné aktivity, vynechávají zvláštní i budoucí dny a odlišují chybějící záznam od nulového plnění. Klepnutím na sloupec v denním přehledu otevřete detail dne. Série alespoň 75% plnění musí navazovat v kalendáři bez mezer.
