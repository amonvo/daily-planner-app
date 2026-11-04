using CommunityToolkit.Mvvm.Messaging;
using PlannerApp.Messages;
using PlannerApp.Models;
using PlannerApp.Services;

namespace PlannerApp.Views;

public class SettingsPage : ContentPage
{
    private readonly DatabaseService database;
    private readonly NotificationService notifications;
    private readonly Picker schedule = new() { Title = "Vyberte aktivitu k ĂşpravÄ›" };
    private List<ScheduleBlock> blocks = [];

    public SettingsPage(DatabaseService database, NotificationService notifications)
    {
        this.database = database;
        this.notifications = notifications;
        Title = "NastavenĂ­";
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F6F7F2"), Color.FromArgb("#14231E"));
        var theme = new Picker { Title = "Vzhled", ItemsSource = new[] { "Podle systĂ©mu", "SvÄ›tlĂ˝", "TmavĂ˝" }, SelectedIndex = Preferences.Get("planner-theme", 0) };
        theme.SelectedIndexChanged += (_, _) => {
            Preferences.Set("planner-theme", theme.SelectedIndex);
            Application.Current!.UserAppTheme = theme.SelectedIndex switch { 1 => AppTheme.Light, 2 => AppTheme.Dark, _ => AppTheme.Unspecified };
        };
        Content = new ScrollView { Content = new VerticalStackLayout {
            Padding = 24, Spacing = 22, MaximumWidthRequest = 760, HorizontalOptions = LayoutOptions.Fill,
            Children = {
                new Label { Text = "VĂˇĹˇ den. VaĹˇe tempo.", Style = (Style)Application.Current!.Resources["H1"] },
                new Label { Text = "Upravte svĹŻj rozvrh a uchovejte si pĹ™ehled i mimo aplikaci.", Style = (Style)Application.Current.Resources["Body"] },
                Card("Vzhled aplikace", theme, Action("O aplikaci", async () => await Shell.Current.GoToAsync("aboutdetails"))),
                Card("PracovnĂ­ a vĂ­kendovĂ˝ rozvrh", new Label { Text = "ZmÄ›ny nĂˇzvu a ÄŤasu zachovajĂ­ pĹ™iĹ™azenou historii. SousednĂ­ aktivity se nesmÄ›jĂ­ pĹ™ekrĂ˝vat." }, schedule, Action("Upravit aktivitu", EditSchedule)),
                Card("VaĹˇe data", new Label { Text = "JSON zĂˇloha obsahuje rozvrh, poznĂˇmky a splnÄ›nĂ© aktivity. Soubor uchovĂˇvejte bezpeÄŤnÄ›. Obnova nahradĂ­ data na tomto zaĹ™Ă­zenĂ­." }, Action("VytvoĹ™it zĂˇlohu", ExportBackup), Action("Obnovit ze zĂˇlohy", ImportBackup), Action("Exportovat pĹ™ehled do CSV", ExportCsv)),
                Card("PĹ™ipomenutĂ­", new Label { Text = "MobilnĂ­ pĹ™ipomenutĂ­ vyĹľadujĂ­ oprĂˇvnÄ›nĂ­ systĂ©mu. ÄŚasy odpovĂ­dajĂ­ mĂ­stnĂ­mu ÄŤasu zaĹ™Ă­zenĂ­." }, Action("Povolit a obnovit pĹ™ipomenutĂ­", async () => {
                    var granted = await notifications.RequestPermissionAsync();
                    if (granted) await notifications.RescheduleForTodayAsync();
                    await DisplayAlertAsync("PĹ™ipomenutĂ­", granted ? "PĹ™ipomenutĂ­ dneĹˇnĂ­ho dne byla obnovena." : "OprĂˇvnÄ›nĂ­ nenĂ­ dostupnĂ©. Zkontrolujte nastavenĂ­ systĂ©mu.", "OK");
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
            catch (Exception exception) { await DisplayAlertAsync("Akci se nepodaĹ™ilo dokonÄŤit", exception.Message, "OK"); }
            finally { button.IsEnabled = true; }
        };
        return button;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        try {
            blocks = (await database.GetAllScheduleBlocksAsync()).Where(b => b.DayType is DayType.Workday or DayType.Weekend).OrderBy(b => b.DayType).ThenBy(b => b.TimeFrom).ToList();
            schedule.ItemsSource = blocks.Select(b => $"{(b.DayType == DayType.Workday ? "PracovnĂ­ den" : "VĂ­kend")} Â· {b.TimeFrom:hh\\:mm} Â· {b.ActivityName}").ToList();
        }
        catch (Exception exception) { await DisplayAlertAsync("Rozvrh se nepodaĹ™ilo naÄŤĂ­st", exception.Message, "OK"); }
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
        var save = Action("UloĹľit zmÄ›ny", async () => {
            await database.SaveScheduleBlockAsync(new ScheduleBlock { Id = block.Id, DayType = block.DayType, ActivityName = name.Text ?? "", TimeFrom = from.Time ?? block.TimeFrom, TimeTo = to.Time ?? block.TimeTo, NotificationMessage = message.Text ?? "" });
            await notifications.RescheduleForTodayAsync(); WeakReferenceMessenger.Default.Send(new CompletionChangedMessage());
            await Navigation.PopModalAsync();
        });
        var cancel = Action("ZruĹˇit", async () => await Navigation.PopModalAsync());
        editor.Content = new ScrollView { Content = new VerticalStackLayout { Padding = 24, Spacing = 16, Children = {
            new Label { Text = "NĂˇzev aktivity" }, name, new Label { Text = "ZaÄŤĂˇtek" }, from,
            new Label { Text = "Konec (mĹŻĹľe pĹ™echĂˇzet pĹ™es pĹŻlnoc)" }, to, new Label { Text = "Text pĹ™ipomenutĂ­" }, message, save, cancel
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
        var file = await FilePicker.Default.PickAsync(new PickOptions { PickerTitle = "Vyberte JSON zĂˇlohu plĂˇnovaÄŤe" });
        if (file is null) return;
        using var stream = await file.OpenReadAsync();
        if (stream.CanSeek && stream.Length > 10_000_000) throw new ArgumentException("ZĂˇloha je pĹ™Ă­liĹˇ velkĂˇ.");
        using var reader = new StreamReader(stream);
        var json = await reader.ReadToEndAsync();
        if (!await DisplayAlertAsync("Nahradit data?", "Obnova nahradĂ­ rozvrh a vĹˇechny zĂˇznamy touto zĂˇlohou. PĹ™edem si vytvoĹ™te aktuĂˇlnĂ­ zĂˇlohu.", "Obnovit", "ZruĹˇit")) return;
        await database.RestoreBackupAsync(json); await notifications.RescheduleForTodayAsync();
        WeakReferenceMessenger.Default.Send(new CompletionChangedMessage()); OnAppearing();
        await DisplayAlertAsync("Hotovo", "Data byla obnovena.", "OK");
    }

    private async Task ExportCsv()
    {
        var logs = await database.GetYearLogsAsync(DateTime.Today.Year);
        var rows = new List<string> { "Datum,Typ dne,PoznĂˇmka,SpeciĂˇlnĂ­ den" };
        static string Cell(string? value) {
            var text = value ?? ""; if (text.Length > 0 && "=+-@\\t\\r\\n".Contains(text[0])) text = "'" + text;
            return "\"" + text.Replace("\"", "\"\"") + "\"";
        }
        rows.AddRange(logs.Select(day => string.Join(",", new[] { Cell(day.Date.ToString("yyyy-MM-dd")), Cell(day.DayType.ToString()), Cell(day.Notes), Cell(day.SpecialDayLabel) })));
        await ShareText($"planner-{DateTime.Today.Year}.csv", string.Join(Environment.NewLine, rows));
    }
}
