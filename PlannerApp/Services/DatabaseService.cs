using PlannerApp.Helpers;

using PlannerApp.Models;

using SQLite;

namespace PlannerApp.Services

{

    public class DatabaseService : IAsyncDisposable

    {

        private SQLiteAsyncConnection? _database;

        private readonly SemaphoreSlim _initLock = new(1, 1);

        private bool _initialized;
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public string DatabasePath { get; }
        public DatabaseService(string databasePath) => DatabasePath = databasePath;

        public async Task InitializeAsync()

        {

            if (_initialized) return;

            await _initLock.WaitAsync().ConfigureAwait(false);

            try

            {

                if (_initialized) return;

                _database = new SQLiteAsyncConnection(DatabasePath);

                await _database.CreateTableAsync<ScheduleBlock>().ConfigureAwait(false);

                await _database.CreateTableAsync<DayLog>().ConfigureAwait(false);

                await _database.CreateTableAsync<BlockCompletion>().ConfigureAwait(false);

                await SeedScheduleBlocksAsync().ConfigureAwait(false);

                _initialized = true;

            }

            finally

            {

                _initLock.Release();

            }

        }

        private async Task EnsureInitializedAsync()

        {

            if (!_initialized) await InitializeAsync().ConfigureAwait(false);

        }

        private async Task SeedScheduleBlocksAsync()

        {

            if (_database is null) return;

            var count = await _database.Table<ScheduleBlock>()

                .Where(b => b.DayType == DayType.Workday || b.DayType == DayType.Weekend)

                .CountAsync().ConfigureAwait(false);

            if (count > 0) return;

            var seed = new List<ScheduleBlock>

            {

                // Workday

                Make(DayType.Workday, "05:30", "06:00", "RannĂ­ rutina", Category.Routine, false, "ÄŚas vstĂˇt a pĹ™ipravit se na den."),

                Make(DayType.Workday, "06:00", "06:45", "BÄ›ĹľĂ­cĂ­ pĂˇs", Category.Running, true, "TeÄŹ mĂˇĹˇ cviÄŤenĂ­ â€“ bÄ›ĹľĂ­cĂ­ pĂˇs 45 minut."),

                Make(DayType.Workday, "06:45", "07:15", "Sprcha a pĹ™Ă­prava", Category.Routine, false, "Sprcha, oblĂ©kĂˇnĂ­, pĹ™Ă­prava."),

                Make(DayType.Workday, "07:15", "08:00", "Cesta do prĂˇce", Category.Commute, false, "Cesta do prĂˇce â€“ zapni audioknihu nebo podcast."),

                Make(DayType.Workday, "08:00", "16:00", "PrĂˇce", Category.Work, false, "PracovnĂ­ doba â€“ soustĹ™edÄ›nĂ­ na prĂˇci."),

                Make(DayType.Workday, "16:00", "16:30", "Cesta domĹŻ", Category.Commute, false, "Cesta domĹŻ â€“ dekomprese, podcast."),

                Make(DayType.Workday, "16:30", "17:00", "VeÄŤeĹ™e a pauza", Category.Routine, false, "VeÄŤeĹ™e â€“ ĹľĂˇdnĂˇ obrazovka, odpoÄŤinek."),

                Make(DayType.Workday, "17:00", "18:30", ".NET studium", Category.DotNet, true, "TeÄŹ mĂˇĹˇ .NET studium â€“ otevĹ™i projekt a kĂłduj."),

                Make(DayType.Workday, "18:30", "19:15", "VedlejĹˇĂ­ projekt", Category.Project, true, "ÄŚas na vedlejĹˇĂ­ projekt."),

                Make(DayType.Workday, "19:15", "19:45", "ÄŚtenĂ­", Category.Reading, true, "ÄŚtenĂ­ â€“ fyzickĂˇ kniha, ĹľĂˇdnĂ˝ telefon."),

                Make(DayType.Workday, "19:45", "20:30", "VolnĂ˝ ÄŤas", Category.FreeTime, false, "VolnĂ˝ ÄŤas â€“ YouTube, zprĂˇvy, brainstorming."),

                Make(DayType.Workday, "20:30", "21:00", "PĹ™Ă­prava na zĂ­tĹ™ek", Category.Routine, false, "Wind-down â€“ pĹ™iprav vÄ›ci na zĂ­tĹ™ek."),

                Make(DayType.Workday, "21:00", "05:30", "SpĂˇnek", Category.Sleep, false, "ÄŚas spĂˇt â€“ telefon dolĹŻ."),

                // Weekend

                Make(DayType.Weekend, "07:30", "09:00", "RannĂ­ volno", Category.FreeTime, false, "RannĂ­ volno â€“ snĂ­danÄ›, kĂˇva, klid."),

                Make(DayType.Weekend, "09:00", "11:00", ".NET nebo projekt", Category.DotNet, true, "HlavnĂ­ blok â€“ .NET studium nebo vedlejĹˇĂ­ projekt."),

                Make(DayType.Weekend, "11:00", "12:00", "BÄ›ĹľĂ­cĂ­ pĂˇs", Category.Running, true, "CviÄŤenĂ­ â€“ delĹˇĂ­ trĂ©nink na bÄ›ĹľĂ­cĂ­m pĂˇsu."),

                Make(DayType.Weekend, "12:00", "14:00", "ObÄ›d a volno", Category.FreeTime, false, "ObÄ›d, pochĹŻzky, odpoÄŤinek."),

                Make(DayType.Weekend, "14:00", "15:30", "Projekt nebo tech", Category.Project, true, "Projekt nebo novĂˇ technologie."),

                Make(DayType.Weekend, "15:30", "16:30", "ÄŚtenĂ­", Category.Reading, true, "ÄŚtenĂ­ â€“ hodina soustĹ™edÄ›nĂ©ho ÄŤtenĂ­."),

                Make(DayType.Weekend, "16:30", "22:00", "VolnĂ˝ ÄŤas", Category.FreeTime, false, "VolnĂ˝ ÄŤas â€“ sociĂˇlnĂ­ aktivity, seriĂˇl, odpoÄŤinek."),

                Make(DayType.Weekend, "22:00", "07:30", "SpĂˇnek", Category.Sleep, false, "ÄŚas spĂˇt."),

            };

            foreach (var b in seed)

                await _database.InsertAsync(b).ConfigureAwait(false);

        }

        private static ScheduleBlock Make(DayType day, string from, string to, string name, Category cat, bool req, string msg) =>

            new()

            {

                DayType = day,

                TimeFrom = TimeSpan.Parse(from),

                TimeTo = TimeSpan.Parse(to),

                ActivityName = name,

                Category = cat,

                IsRequired = req,

                NotificationMessage = msg

            };

        private static List<ScheduleBlock> BuildTaborFridayBlocks(List<ScheduleBlock> workdayBlocks)

        {

            // Use workday blocks up to and including 16:30-17:00 (VeÄŤeĹ™e a pauza), then add Tabor travel block

            var result = workdayBlocks

                .Where(b => b.TimeFrom < TimeSpan.FromHours(19))

                .Select(b => new ScheduleBlock

                {

                    Id = b.Id,

                    DayType = DayType.Tabor,

                    TimeFrom = b.TimeFrom,

                    TimeTo = b.TimeTo > TimeSpan.FromHours(19) ? TimeSpan.FromHours(19) : b.TimeTo,

                    ActivityName = b.ActivityName,

                    Category = b.Category,

                    IsRequired = b.IsRequired,

                    NotificationMessage = b.NotificationMessage

                })

                .ToList();

            result.Add(new ScheduleBlock

            {

                Id = -1,

                DayType = DayType.Tabor,

                TimeFrom = TimeSpan.FromHours(19),

                TimeTo = new TimeSpan(23, 59, 0),

                ActivityName = "Cesta do TĂˇbora â€“ volno",

                Category = Category.FreeTime,

                IsRequired = false,

                NotificationMessage = "JedeĹˇ do TĂˇbora."

            });

            return result;

        }

        private static List<ScheduleBlock> BuildTaborSaturdayBlocks() =>

            new()

            {

                new ScheduleBlock

                {

                    Id = -2,

                    DayType = DayType.Tabor,

                    TimeFrom = TimeSpan.Zero,

                    TimeTo = new TimeSpan(23, 59, 0),

                    ActivityName = "TĂˇbor â€“ volnĂ˝ den",

                    Category = Category.FreeTime,

                    IsRequired = false,

                    NotificationMessage = "TĂˇbor vĂ­kend â€“ volno."

                }

            };

        private static List<ScheduleBlock> BuildTaborSundayBlocks() =>

            new()

            {

                new ScheduleBlock

                {

                    Id = -3,

                    DayType = DayType.Tabor,

                    TimeFrom = TimeSpan.Zero,

                    TimeTo = TimeSpan.FromHours(19),

                    ActivityName = "TĂˇbor â€“ volnĂ˝ den",

                    Category = Category.FreeTime,

                    IsRequired = false,

                    NotificationMessage = "TĂˇbor vĂ­kend â€“ volno."

                },

                new ScheduleBlock

                {

                    Id = -4,

                    DayType = DayType.Tabor,

                    TimeFrom = TimeSpan.FromHours(19),

                    TimeTo = new TimeSpan(23, 59, 0),

                    ActivityName = "Cesta zpÄ›t do PlznÄ›",

                    Category = Category.Commute,

                    IsRequired = false,

                    NotificationMessage = "Cesta zpÄ›t domĹŻ do PlznÄ›."

                }

            };

        public async Task<DayLog> GetOrCreateDayLogAsync(DateTime date)

        {
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {

            await EnsureInitializedAsync().ConfigureAwait(false);

            date = date.Date;

            try

            {

                var existing = await _database!.Table<DayLog>()

                    .Where(d => d.Date == date)

                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (existing is not null) return existing;

                var dayType = DateHelper.DetectDayType(date);

                var log = new DayLog

                {

                    Date = date,

                    DayType = dayType,

                    HolidayName = CzechHolidayHelper.GetHolidayName(date)

                };

                await _database.InsertAsync(log).ConfigureAwait(false);

                return log;

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"GetOrCreateDayLog error: {ex}");

                throw;

            }

            }
            finally { _writeLock.Release(); }
        }

        public async Task UpdateDayLogAsync(DayLog log)

        {

            await EnsureInitializedAsync().ConfigureAwait(false);

            try

            {

                await _database!.UpdateAsync(log).ConfigureAwait(false);

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"UpdateDayLog error: {ex}");
                throw;

            }

        }

        public async Task<List<ScheduleBlock>> GetScheduleBlocksForDateAsync(DateTime date)

        {

            await EnsureInitializedAsync().ConfigureAwait(false);

            date = date.Date;

            try

            {

                if (TaborHelper.IsTaborFriday(date))

                {

                    var workdayBlocks = await _database!.Table<ScheduleBlock>()

                        .Where(b => b.DayType == DayType.Workday)

                        .OrderBy(b => b.TimeFrom)

                        .ToListAsync().ConfigureAwait(false);

                    return BuildTaborFridayBlocks(workdayBlocks);

                }

                if (TaborHelper.IsTaborSaturday(date))

                    return BuildTaborSaturdayBlocks();

                if (TaborHelper.IsTaborSunday(date))

                    return BuildTaborSundayBlocks();

                var scheduleDay = DateHelper.GetScheduleDayType(date);

                return await _database!.Table<ScheduleBlock>()

                    .Where(b => b.DayType == scheduleDay)

                    .OrderBy(b => b.TimeFrom)

                    .ToListAsync().ConfigureAwait(false);

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"GetScheduleBlocks error: {ex}");

                throw;

            }

        }

        public async Task<List<BlockCompletion>> GetCompletionsForDayAsync(int dayLogId)

        {

            await EnsureInitializedAsync().ConfigureAwait(false);

            try

            {

                return await _database!.Table<BlockCompletion>()

                    .Where(c => c.DayLogId == dayLogId)

                    .ToListAsync().ConfigureAwait(false);

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"GetCompletions error: {ex}");

                throw;

            }

        }

        public async Task UpsertCompletionAsync(BlockCompletion completion)

        {
            await _writeLock.WaitAsync().ConfigureAwait(false);
            try
            {

            await EnsureInitializedAsync().ConfigureAwait(false);

            try

            {

                var existing = await _database!.Table<BlockCompletion>()

                    .Where(c => c.DayLogId == completion.DayLogId && c.ScheduleBlockId == completion.ScheduleBlockId)

                    .FirstOrDefaultAsync().ConfigureAwait(false);

                if (existing is null)

                {

                    await _database.InsertAsync(completion).ConfigureAwait(false);

                }

                else

                {

                    existing.Status = completion.Status;

                    existing.CompletedAt = completion.CompletedAt;

                    await _database.UpdateAsync(existing).ConfigureAwait(false);

                }

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"UpsertCompletion error: {ex}");
                throw;

            }

            }
            finally { _writeLock.Release(); }
        }

        public async Task<List<DayLog>> GetLogsAsync(DateTime from, DateTime to)
        {
            await EnsureInitializedAsync();
            var start = from.Date;
            var end = to.Date.AddDays(1);
            return await _database!.Table<DayLog>().Where(day => day.Date >= start && day.Date < end).OrderBy(day => day.Date).ToListAsync();
        }

        public async Task<List<DayLog>> GetWeekLogsAsync(int weekNumber, int year)

        {

            await EnsureInitializedAsync().ConfigureAwait(false);

            var start = DateHelper.GetIsoWeekStart(year, weekNumber);

            var end = start.AddDays(7);

            try

            {

                return await _database!.Table<DayLog>()

                    .Where(d => d.Date >= start && d.Date < end)

                    .OrderBy(d => d.Date)

                    .ToListAsync().ConfigureAwait(false);

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"GetWeekLogs error: {ex}");

                throw;

            }

        }

        public async Task<List<DayLog>> GetMonthLogsAsync(int month, int year)

        {

            await EnsureInitializedAsync().ConfigureAwait(false);

            var start = new DateTime(year, month, 1);

            var end = start.AddMonths(1);

            try

            {

                return await _database!.Table<DayLog>()

                    .Where(d => d.Date >= start && d.Date < end)

                    .OrderBy(d => d.Date)

                    .ToListAsync().ConfigureAwait(false);

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"GetMonthLogs error: {ex}");

                throw;

            }

        }

        public async Task<List<DayLog>> GetYearLogsAsync(int year)

        {

            await EnsureInitializedAsync().ConfigureAwait(false);

            var start = new DateTime(year, 1, 1);

            var end = new DateTime(year + 1, 1, 1);

            try

            {

                return await _database!.Table<DayLog>()

                    .Where(d => d.Date >= start && d.Date < end)

                    .OrderBy(d => d.Date)

                    .ToListAsync().ConfigureAwait(false);

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"GetYearLogs error: {ex}");

                throw;

            }

        }

        public async Task<List<BlockCompletion>> GetCompletionsForDaysAsync(List<int> dayLogIds)

        {

            await EnsureInitializedAsync().ConfigureAwait(false);

            if (dayLogIds.Count == 0) return new List<BlockCompletion>();

            try

            {

                var all = new List<BlockCompletion>();

                foreach (var id in dayLogIds)

                {

                    var list = await _database!.Table<BlockCompletion>()

                        .Where(c => c.DayLogId == id)

                        .ToListAsync().ConfigureAwait(false);

                    all.AddRange(list);

                }

                return all;

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"GetCompletionsForDays error: {ex}");

                throw;

            }

        }

        public async Task<List<ScheduleBlock>> GetAllScheduleBlocksAsync()

        {

            await EnsureInitializedAsync().ConfigureAwait(false);

            try

            {

                return await _database!.Table<ScheduleBlock>().ToListAsync().ConfigureAwait(false);

            }

            catch

            {

                throw;

            }

        }

        public async ValueTask DisposeAsync()
        {
            if (_database is not null) await _database.CloseAsync();
            _initialized = false;
        }

        public async Task SaveScheduleBlockAsync(ScheduleBlock block)
        {
            await EnsureInitializedAsync();
            if (block.Id <= 0 || string.IsNullOrWhiteSpace(block.ActivityName) || block.ActivityName.Length > 120 ||
                block.TimeFrom < TimeSpan.Zero || block.TimeFrom >= TimeSpan.FromDays(1) ||
                block.TimeTo < TimeSpan.Zero || block.TimeTo >= TimeSpan.FromDays(1) || block.TimeFrom == block.TimeTo)
                throw new ArgumentException("Vyplňte název a dva různé časy v rámci dne.");
            await _writeLock.WaitAsync();
            try
            {
                var existing = await _database!.FindAsync<ScheduleBlock>(block.Id) ?? throw new ArgumentException("Aktivita již neexistuje.");
                var others = await _database.Table<ScheduleBlock>().Where(b => b.DayType == existing.DayType && b.Id != block.Id).ToListAsync();
                if (others.Any(other => ScheduleTime.Overlaps(block, other))) throw new ArgumentException("Čas se překrývá s jinou aktivitou.");
                existing.ActivityName = block.ActivityName.Trim(); existing.TimeFrom = block.TimeFrom; existing.TimeTo = block.TimeTo;
                existing.NotificationMessage = block.NotificationMessage.Trim();
                await _database.UpdateAsync(existing);
            }
            finally { _writeLock.Release(); }
        }

        public sealed record Backup(int Version, List<ScheduleBlock> Schedule, List<DayLog> Days, List<BlockCompletion> Completions);

        public async Task<string> ExportBackupAsync()
        {
            await EnsureInitializedAsync();
            Backup? snapshot = null;
            await _database!.RunInTransactionAsync(connection => snapshot = new Backup(1, connection.Table<ScheduleBlock>().ToList(), connection.Table<DayLog>().ToList(), connection.Table<BlockCompletion>().ToList()));
            return System.Text.Json.JsonSerializer.Serialize(snapshot, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        }

        public async Task RestoreBackupAsync(string json)
        {
            if (json.Length > 10_000_000) throw new ArgumentException("Záloha je příliš velká.");
            var backup = System.Text.Json.JsonSerializer.Deserialize<Backup>(json) ?? throw new ArgumentException("Neplatná záloha.");
            if (backup.Version != 1 || backup.Schedule is null || backup.Days is null || backup.Completions is null || backup.Schedule.Count == 0 ||
                backup.Schedule.Any(b => b.Id <= 0 || !Enum.IsDefined(b.DayType) || !Enum.IsDefined(b.Category) || string.IsNullOrWhiteSpace(b.ActivityName)) ||
                backup.Schedule.Select(b => b.Id).Distinct().Count() != backup.Schedule.Count ||
                backup.Days.Any(d => d.Id <= 0 || d.Date != d.Date.Date || !Enum.IsDefined(d.DayType)) ||
                backup.Days.Select(d => d.Id).Distinct().Count() != backup.Days.Count || backup.Days.Select(d => d.Date).Distinct().Count() != backup.Days.Count ||
                backup.Completions.Select(c => c.Id).Distinct().Count() != backup.Completions.Count ||
                backup.Completions.Select(c => (c.DayLogId, c.ScheduleBlockId)).Distinct().Count() != backup.Completions.Count ||
                backup.Completions.Any(c => c.Id <= 0 || !Enum.IsDefined(c.Status) || !backup.Days.Any(d => d.Id == c.DayLogId) ||
                    !(backup.Schedule.Any(b => b.Id == c.ScheduleBlockId) || c.ScheduleBlockId is >= -4 and <= -1)))
                throw new ArgumentException("Záloha obsahuje neplatné nebo duplicitní záznamy.");
            await EnsureInitializedAsync();
            await _writeLock.WaitAsync();
            try
            {
                await _database!.RunInTransactionAsync(connection => {
                    connection.DeleteAll<BlockCompletion>(); connection.DeleteAll<DayLog>(); connection.DeleteAll<ScheduleBlock>();
                    connection.InsertAll(backup.Schedule); connection.InsertAll(backup.Days); connection.InsertAll(backup.Completions);
                });
            }
            finally { _writeLock.Release(); }
        }
    }

}

