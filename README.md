# ?? PlannerApp

Mobilní denní plánovaè postavenı na **.NET MAUI 9** s podporou Androidu, iOS, macOS a Windows.

---

## ? Funkce

- **Dnes** – pøehled denního plánu s bloky aktivit a jejich plnìním
- **Tıden** – tıdenní pohled s pøehledem splnìnıch aktivit
- **Mìsíc** – mìsíèní kalendáø s barevnım zvıraznìním produktivity
- **Rok** – roèní statistiky, pøehled po mìsících a aktivity za celı rok
- ?? Lokální notifikace s pøipomenutím aktivit
- ?? Podpora svìtlého a tmavého reimu
- ???? Èeské státní svátky a prázdniny

---

## ??? Technologie

| Technologie | Verze |
|---|---|
| .NET MAUI | 9.0 |
| CommunityToolkit.Maui | 9.1.0 |
| CommunityToolkit.Mvvm | 8.3.2 |
| SQLite (sqlite-net-pcl) | 1.9.172 |
| Plugin.LocalNotification | 11.1.2 |

---

## ?? Struktura projektu

```
PlannerApp/
??? Models/             # Datové modely (DayLog, ScheduleBlock, ...)
??? ViewModels/         # MVVM ViewModely (MVVM Community Toolkit)
??? Views/              # XAML stránky (Today, Week, Month, Year)
??? Services/           # DatabaseService, NotificationService
??? Helpers/            # CzechHolidayHelper, DateHelper, TaborHelper
??? Resources/
?   ??? Styles/         # Barvy a globální styly
?   ??? Fonts/          # OpenSans
?   ??? Images/
??? Platforms/          # Android, iOS, macOS, Windows
```

---

## ?? Spuštìní projektu

### Poadavky
- [Visual Studio 2022](https://visualstudio.microsoft.com/) s workloadem **.NET MAUI**
- Android SDK (API 26+) pro Android

### Kroky
1. Naklonuj repozitáø:
   ```bash
   git clone https://github.com/amonvo/daily-planner-app.git
   ```
2. Otevøi `PlannerApp.sln` ve Visual Studiu
3. Vyber cílovou platformu (Android, iOS, Windows)
4. Spus pomocí **F5**

---

## ?? Testování na fyzickém Android zaøízení

1. Zapni **Reim vıvojáøe** ? **Ladìní pøes USB**
2. Pøipoj telefon USB kabelem a potvrï ladìní
3. Ve Visual Studiu vyber zaøízení v nástrojové lištì
4. Spus pomocí **F5**

---

## ?? Licence

Tento projekt je urèen pro osobní pouití.
