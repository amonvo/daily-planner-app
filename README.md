# PlannerApp

Mobilní denní plánovač pro Android postavený na .NET 9 MAUI. Aplikace slouží jako osobní organizér dne — zobrazuje fixní denní rozvrh rozdělený do časových bloků, umožňuje zaznamenávat splnění každé aktivity a zobrazuje statistiky za týden, měsíc a rok. Lokální notifikace automaticky upozorní na změnu aktivity.

---

## Funkce

- **Dnes** — přehled celého dne rozdělený do časových bloků, aktuální a následující aktivita, zaznamenávání splnění (Splněno / Přeskočeno / Nesplněno), denní poznámka
- **Týden** — týdenní přehled s plněním per den a souhrnem per kategorie
- **Měsíc** — kalendářní grid s barevným zvýrazněním produktivity, přehled svátků a Tábor víkendů
- **Rok** — roční statistiky, přehled po měsících, nejdelší série splněných dní
- Lokální push notifikace s připomenutím při každé změně aktivity
- Automatické rozlišení typu dne: pracovní den / víkend / státní svátek / Tábor víkend
- České státní svátky 2026 přednastaveny
- Podpora světlého a tmavého režimu
- Data uložena lokálně v SQLite — bez internetu, bez cloudu

---

## Technologie

| Technologie | Verze |
|---|---|
| .NET MAUI | 9.0 |
| CommunityToolkit.Maui | 9.1.0 |
| CommunityToolkit.Mvvm | 8.3.2 |
| sqlite-net-pcl | 1.9.172 |
| SQLitePCLRaw.bundle_green | 2.1.6 |
| Plugin.LocalNotification | 11.1.2 |

---

## Struktura projektu

```
PlannerApp/
├── Models/
│   ├── ScheduleBlock.cs        # Definice časového bloku (čas, aktivita, kategorie)
│   ├── DayLog.cs               # Záznam dne (datum, typ dne, poznámka)
│   ├── BlockCompletion.cs      # Splnění konkrétního bloku v konkrétní den
│   └── Enums.cs                # DayType, Category, CompletionStatus
├── ViewModels/
│   ├── BaseViewModel.cs
│   ├── BlockViewModel.cs       # Wrapper bloku pro UI (barva, stav, příkazy)
│   ├── TodayViewModel.cs
│   ├── WeekViewModel.cs
│   ├── MonthViewModel.cs
│   └── YearViewModel.cs
├── Views/
│   ├── TodayPage.xaml
│   ├── WeekPage.xaml
│   ├── MonthPage.xaml
│   └── YearPage.xaml
├── Services/
│   ├── DatabaseService.cs      # SQLite CRUD, seed dat při prvním spuštění
│   └── NotificationService.cs  # Plánování lokálních notifikací
├── Helpers/
│   ├── CzechHolidayHelper.cs   # Státní svátky 2026
│   ├── TaborHelper.cs          # Detekce Tábor víkendů (ob týden od 9. 1. 2026)
│   └── DateHelper.cs           # ISO týden, český formát data, typ dne
├── Resources/
│   ├── Styles/
│   │   ├── Colors.xaml
│   │   └── Styles.xaml
│   └── Fonts/
└── Platforms/
    └── Android/
```

---

## Spuštění projektu

### Požadavky

- Visual Studio 2022 17.8+ s nainstalovaným workloadem **.NET MAUI**
- Android SDK API 26+ (Android 8.0 nebo vyšší)

### Postup

```bash
git clone https://github.com/amonvo/PlannerApp.git
```

Otevři `PlannerApp.sln` ve Visual Studiu 2022, vyber cílovou platformu **Android** a spusť pomocí **F5**.

---

## Testování na fyzickém zařízení (Samsung A56)

1. Na telefonu zapni **Nastavení → O telefonu → Číslo sestavení** (klepni 7×) → povolí se Režim vývojáře
2. **Nastavení → Možnosti vývojáře → Ladění přes USB** — zapnout
3. Připoj telefon USB kabelem a na telefonu potvrď hlášku "Povolit ladění přes USB"
4. Ve Visual Studiu vyber zařízení v rozbalovací liště nástrojů (místo emulátoru)
5. Spusť pomocí **F5**

### Oprávnění vyžadovaná na Androidu

Aplikace při prvním spuštění požádá o oprávnění k odesílání notifikací. Toto oprávnění je nutné pro funkci připomenutí aktivit. Bez něj aplikace funguje, ale notifikace se nezobrazí.

---

## Denní rozvrh

Rozvrh je přednastavený a rozdělený na tři typy dní:

**Pracovní den (Po–Pá)**

| Čas | Aktivita | Kategorie |
|---|---|---|
| 05:30–06:00 | Ranní rutina | Rutina |
| 06:00–06:45 | Běžící pás | Pohyb |
| 07:15–08:00 | Cesta do práce | Dojíždění |
| 08:00–16:00 | Práce | Práce |
| 17:00–18:30 | .NET studium | .NET |
| 18:30–19:15 | Vedlejší projekt | Projekt |
| 19:15–19:45 | Čtení | Čtení |
| 19:45–20:30 | Volný čas | Volno |
| 21:00–05:30 | Spánek | Spánek |

**Víkend** a **Tábor víkend** mají vlastní rozvrh nastavený v `DatabaseService.cs` (seed data).

---

## Licence

Projekt je určen pro osobní použití.
