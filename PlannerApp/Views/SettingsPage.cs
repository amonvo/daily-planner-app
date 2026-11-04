using CommunityToolkit.Mvvm.Messaging;
using PlannerApp.Messages;
using PlannerApp.Models;
using PlannerApp.Services;

namespace PlannerApp.Views;

public class SettingsPage : ContentPage
{
    private readonly DatabaseService database;
    private readonly NotificationService notifications;
    private readonly Picker schedule = new() { Title = "Vyberte aktivitu k úpravě" };
    private List<ScheduleBlock> blocks = [];

    public SettingsPage(DatabaseService database, NotificationService notifications)
    {
        this.database = database;
        this.notifications = notifications;
        Title = "Nastavení";
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F6F7F2"), Color.FromArgb("#14231E"));
        var theme = new Picker { Title = "Vzhled", ItemsSource = new[] { "Podle systému", "Světlý", "Tmavý" }, SelectedIndex = Preferences.Get("planner-theme", 0) };
        theme.SelectedIndexChanged += (_, _) => {
            Preferences.Set("planner-theme", theme.SelectedIndex);
            Application.Current!.UserAppTheme = theme.SelectedIndex switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.Unspecified };
        };
        Content = new ScrollView { Content = new VerticalStackLayout {
            Padding = 24, Spacing = 22, MaximumWidthRequest = 760, HorizontalOptions = LayoutOptions.Fill,
            Children = {
                new Label { Text = "Váš den. Vaše tempo.", Style = (Style)Application.Current!.Resources["H1"] },
                new Label { Text = "Upravte svůj rozvrh a uchovejte si přehled i mimo aplikaci.", Style = (Style)Application.Current.Resources["Body"] },
                Card("Vzhled aplikace", theme, Action("O aplikaci", async () => await Shell.Current.GoToAsync("aboutdetails"))),
                Card("Pracovní a víkendový rozvrh", new Label { Text = "Změny názvu a času zachovají přiřazenou historii. Sousední aktivity se nesmějí překrývat." }, schedule, Action("Upravit aktivitu", EditSchedule)),
                Card("Vaše data", new Label { Text = "JSON záloha obsahuje rozvrh, poznámky a splněné aktivity. Soubor uchovávejte bezpečně. Obnova nahradí data na tomto zařízení." }, Action("Vytvořit zálohu", ExportBackup), Action("Obnovit ze zálohy", ImportBackup), Action("Exportovat přehled do CSV", ExportCsv)),
                Card("Připomenutí", new Label { Text = "Mobilní připomenutí vyžadují oprávnění systému. Časy odpovídají místnímu času zařízení." }, Action("Povolit a obnovit připomenutí", async () => {
                    var granted = await notifications.RequestPermissionAsync();
                    if (granted) await notifications.RescheduleForTodayAsync();
                    await DisplayAlertAsync("Připomenutí", granted ? "Připomenutí dnešního dne byla obnovena." : "Oprávnění není dostupné. Zkontrolujte nastavení systému.", "OK");
                }))
            }
        }};
    }

    private static Border Card(string title, params View[] content)
    {
        var layout = new VerticalStackLayout { Spacing = 14 };
        layout.Add(new Label { Text = title, Style = (Style)Application.Current!.Resources["H2"] });
        foreach (var view in content) layout.Add(view);
        return new Border { Style = (Style)Application.Current.Resources["Card"], Content = layout };
    }

    private Button Action(string text, Func<Task> action)
    {
        var button = new Button { Text = text };
        button.Clicked += async (_, _) => {
            button.IsEnabled = false;
            try { await action(); }
            catch (Exception exception) { await DisplayAlertAsync("Akci se nepodařilo dokončit", exception.Message, "OK"); }
            finally { button.IsEnabled = true; }
        };
        return button;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try {
            blocks = (await database.GetAllScheduleBlocksAsync()).Where(b => b.DayType is DayType.Workday or DayType.Weekend).OrderBy(b => b.DayType).ThenBy(b => b.TimeFrom).ToList();
            schedule.ItemsSource = blocks.Select(b => $"{(b.DayType == DayType.Workday ? "Pracovní den" : "Víkend")} · {b.TimeFrom:hh\\:mm} · {b.ActivityName}").ToList();
        }
        catch (Exception exception) { await DisplayAlertAsync("Rozvrh se nepodařilo načíst", exception.Message, "OK"); }
    }

    private async Task EditSchedule()
    {
        if (schedule.SelectedIndex < 0) { await DisplayAlertAsync("Rozvrh", "Nejprve vyberte aktivitu.", "OK"); return; }
        var block = blocks[schedule.SelectedIndex];
        var editor = new ContentPage { Title = "Upravit aktivitu" };
        var name = new Entry { Text = block.ActivityName, MaxLength = 120 };
        var from = new TimePicker { Time = block.TimeFrom, Format = "HH:mm" };
        var to = new TimePicker { Time = block.TimeTo, Format = "HH:mm" };
        var message = new Editor { Text = block.NotificationMessage, AutoSize = EditorAutoSizeOption.TextChanges };
        var save = Action("Uložit změny", async () => {
            await database.SaveScheduleBlockAsync(new ScheduleBlock { Id = block.Id, DayType = block.DayType, ActivityName = name.Text ?? "", TimeFrom = from.Time ?? block.TimeFrom, TimeTo = to.Time ?? block.TimeTo, NotificationMessage = message.Text ?? "" });
            await notifications.RescheduleForTodayAsync(); WeakReferenceMessenger.Default.Send(new CompletionChangedMessage());
            await Navigation.PopModalAsync();
        });
        var cancel = Action("Zrušit", async () => await Navigation.PopModalAsync());
        editor.Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 16, Children = {
            new Label { Text = "Název aktivity" }, name, new Label { Text = "Začátek" }, from,
            new Label { Text = "Konec (může přecházet přes půlnoc)" }, to, new Label { Text = "Text připomenutí" }, message, save, cancel
        }}};
        await Navigation.PushModalAsync(new NavigationPage(editor));
    }

    private async Task ShareText(string name, string text)
    {
        var path = Path.Combine(FileSystem.CacheDirectory, name);
        await File.WriteAllTextAsync(path, text, System.Text.Encoding.UTF8);
        await Share.Default.RequestAsync(new ShareFileRequest { Title = name, File = new ShareFile(path) });
    }

    private async Task ExportBackup() => await ShareText($"planner-backup-{DateTime.Now:yyyyMMdd-HHmmss}.json", await database.ExportBackupAsync());
    private async Task ImportBackup()
    {
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Vyberte JSON zálohu plánovače" });
        if (file is null) return;
        using var stream = await file.OpenReadAsync();
        if (stream.CanSeek && stream.Length > 10_000_000) throw new ArgumentException("Záloha je příliš velká.");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        if (!await DisplayAlertAsync("Nahradit data?", "Obnova nahradí rozvrh a všechny záznamy touto zálohou. Předem si vytvořte aktuální zálohu.", "Obnovit", "Zrušit")) return;
        await database.RestoreBackupAsync(json); await notifications.RescheduleForTodayAsync();
        WeakReferenceMessenger.Default.Send(new CompletionChangedMessage()); OnAppearing();
        await DisplayAlertAsync("Hotovo", "Data byla obnovena.", "OK");
    }

    private async Task ExportCsv()
    {
        var logs = await database.GetYearLogsAsync(DateTime.Today.Year);
        var rows = new List<string> { "Datum,Typ dne,Poznámka,Speciální den" };
        static string Cell(string? value) {
            var text = value ?? ""; if (text.Length > 0 && "=+-@\\t\\r\\n".Contains(text[0])) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        rows.AddRange(logs.Select(day => string.Join(",", new[] { Cell(day.Date.ToString("yyyy-MM-dd")), Cell(day.DayType.ToString()), Cell(day.Notes), Cell(day.SpecialDayLabel) })));
        await ShareText($"planner-{DateTime.Today.Year}.csv", string.Join(Environment.NewLine, rows));
    }
}
