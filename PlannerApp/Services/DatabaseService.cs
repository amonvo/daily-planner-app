using PlannerApp.Helpers;
using PlannerApp.Models;
using SQLite;

namespace PlannerApp.Services
{
    public class DatabaseService
    {
        private SQLiteAsyncConnection? _database;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _initialized;

        public string DatabasePath =>
            Path.Combine(FileSystem.AppDataDirectory, "plannerapp.db3");

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
            var count = await _database.Table<ScheduleBlock>().CountAsync().ConfigureAwait(false);
            if (count > 0) return;

            var seed = new List<ScheduleBlock>
            {
                // Workday
                Make(DayType.Workday, "05:30", "06:00", "Ranní rutina", Category.Routine, false, "Èas vstát a pøipravit se na den."),
                Make(DayType.Workday, "06:00", "06:45", "Bìžící pás", Category.Running, true, "Teï máš cvièení – bìžící pás 45 minut."),
                Make(DayType.Workday, "06:45", "07:15", "Sprcha a pøíprava", Category.Routine, false, "Sprcha, oblékání, pøíprava."),
                Make(DayType.Workday, "07:15", "08:00", "Cesta do práce", Category.Commute, false, "Cesta do práce – zapni audioknihu nebo podcast."),
                Make(DayType.Workday, "08:00", "16:00", "Práce", Category.Work, false, "Pracovní doba – soustøedìní na práci."),
                Make(DayType.Workday, "16:00", "16:30", "Cesta domù", Category.Commute, false, "Cesta domù – dekomprese, podcast."),
                Make(DayType.Workday, "16:30", "17:00", "Veèeøe a pauza", Category.Routine, false, "Veèeøe – žádná obrazovka, odpoèinek."),
                Make(DayType.Workday, "17:00", "18:30", ".NET studium", Category.DotNet, true, "Teï máš .NET studium – otevøi projekt a kóduj."),
                Make(DayType.Workday, "18:30", "19:15", "Vedlejší projekt", Category.Project, true, "Èas na vedlejší projekt – LearKPI nebo domovsnadno."),
                Make(DayType.Workday, "19:15", "19:45", "Ètení", Category.Reading, true, "Ètení – fyzická kniha, žádný telefon."),
                Make(DayType.Workday, "19:45", "20:30", "Volný èas", Category.FreeTime, false, "Volný èas – YouTube, zprávy, brainstorming."),
                Make(DayType.Workday, "20:30", "21:00", "Pøíprava na zítøek", Category.Routine, false, "Wind-down – pøiprav vìci na zítøek."),
                Make(DayType.Workday, "21:00", "05:30", "Spánek", Category.Sleep, false, "Èas spát – telefon dolù."),

                // Weekend
                Make(DayType.Weekend, "07:30", "09:00", "Ranní volno", Category.FreeTime, false, "Ranní volno – snídanì, káva, klid."),
                Make(DayType.Weekend, "09:00", "11:00", ".NET nebo projekt", Category.DotNet, true, "Hlavní blok – .NET studium nebo vedlejší projekt."),
                Make(DayType.Weekend, "11:00", "12:00", "Bìžící pás", Category.Running, true, "Cvièení – delší trénink na bìžícím pásu."),
                Make(DayType.Weekend, "12:00", "14:00", "Obìd a volno", Category.FreeTime, false, "Obìd, pochùzky, odpoèinek."),
                Make(DayType.Weekend, "14:00", "15:30", "Projekt nebo tech", Category.Project, true, "Projekt nebo nová technologie – Docker, Angular, Python."),
                Make(DayType.Weekend, "15:30", "16:30", "Ètení", Category.Reading, true, "Ètení – hodina soustøedìného ètení."),
                Make(DayType.Weekend, "16:30", "22:00", "Volný èas", Category.FreeTime, false, "Volný èas – sociální aktivity, seriál, odpoèinek."),
                Make(DayType.Weekend, "22:00", "07:30", "Spánek", Category.Sleep, false, "Èas spát."),

                // Tabor
                Make(DayType.Tabor, "00:00", "23:59", "Tábor – volno s pøítelkyní", Category.FreeTime, false, "Tábor víkend – volno, žádný plán."),
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

        public async Task<DayLog> GetOrCreateDayLogAsync(DateTime date)
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
            }
        }

        public async Task<List<ScheduleBlock>> GetScheduleBlocksForDateAsync(DateTime date)
        {
            await EnsureInitializedAsync().ConfigureAwait(false);
            var scheduleDay = DateHelper.GetScheduleDayType(date);
            try
            {
                return await _database!.Table<ScheduleBlock>()
                    .Where(b => b.DayType == scheduleDay)
                    .OrderBy(b => b.TimeFrom)
                    .ToListAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"GetScheduleBlocks error: {ex}");
                return new List<ScheduleBlock>();
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
                return new List<BlockCompletion>();
            }
        }

        public async Task UpsertCompletionAsync(BlockCompletion completion)
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
            }
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
                return new List<DayLog>();
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
                return new List<DayLog>();
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
                return new List<DayLog>();
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
                return new List<BlockCompletion>();
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
                return new List<ScheduleBlock>();
            }
        }
    }
}
