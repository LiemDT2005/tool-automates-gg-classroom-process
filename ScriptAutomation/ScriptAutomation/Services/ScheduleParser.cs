using ScriptAutomation.Models;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ScriptAutomation.Services;

/// <summary>
/// Parse file lich_day.md thành danh sách TeachingSession
/// </summary>
public class ScheduleParser
{
    // Giáo viên nữ (Cô)
    private static readonly HashSet<string> FemaleTeachers = new(StringComparer.OrdinalIgnoreCase)
    {
        "Hằng", "Lan", "Nhã", "Kim", "Nhi"
    };

    // Mapping thứ -> DayOfWeek number (Thứ 2 = Monday = 1, CN = 0)
    private static readonly Dictionary<string, DayOfWeek> DayMapping = new()
    {
        ["Thứ 2"] = System.DayOfWeek.Monday,
        ["Thứ 3"] = System.DayOfWeek.Tuesday,
        ["Thứ 4"] = System.DayOfWeek.Wednesday,
        ["Thứ 5"] = System.DayOfWeek.Thursday,
        ["Thứ 6"] = System.DayOfWeek.Friday,
        ["Thứ 7"] = System.DayOfWeek.Saturday,
        ["Chủ nhật"] = System.DayOfWeek.Sunday
    };

    /// <summary>
    /// Parse toàn bộ file lịch dạy và trả về danh sách sessions theo từng lớp
    /// </summary>
    public List<TeachingSession> Parse(string filePath)
    {
        var lines = File.ReadAllLines(filePath);
        var sessions = new List<TeachingSession>();

        // Parse Tuần 0
        sessions.AddRange(ParseWeek0(lines));

        // Parse Tuần 1-8
        sessions.AddRange(ParseWeek1To8(lines));

        // Parse Tuần 9
        sessions.AddRange(ParseWeek9(lines));

        // Gán số buổi (SessionNumber) cho từng lớp trong từng tuần
        AssignSessionNumbers(sessions);

        return sessions;
    }

    /// <summary>
    /// Lấy tất cả giáo viên unique cho 1 lớp
    /// </summary>
    public List<TeacherInfo> GetUniqueTeachers(List<TeachingSession> sessions, string className)
    {
        return sessions
            .Where(s => s.ClassName == className)
            .SelectMany(s => s.Teachers)
            .GroupBy(t => t.Name)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Lấy danh sách tất cả các lớp
    /// </summary>
    public List<string> GetAllClasses(List<TeachingSession> sessions)
    {
        return sessions.Select(s => s.ClassName).Distinct().ToList();
    }

    #region Parse Tuần 0

    private List<TeachingSession> ParseWeek0(string[] lines)
    {
        var sessions = new List<TeachingSession>();
        bool inWeek1Table = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line == "## TUẦN 0")
            {
                inWeek1Table = true;
                continue;
            }

            if (inWeek1Table && line.StartsWith("## "))
                break;

            if (!inWeek1Table || !line.StartsWith("|") || line.Contains("---"))
                continue;

            var cols = line.Split('|', StringSplitOptions.None)
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToArray();

            if (cols.Length < 4 || cols[0] == "Lớp") continue;

            string className = cols[0].Trim();
            string dateStr = cols[1].Trim();
            string dayStr = cols[2].Trim();
            string teacherStr = cols[3].Trim();
            string note = cols.Length > 4 ? cols[4].Trim() : "";

            var date = ParseDate(dateStr);
            var teachers = ParseTeachers(teacherStr);

            sessions.Add(new TeachingSession
            {
                ClassName = className,
                WeekNumber = 0,
                Date = date,
                DayOfWeek = dayStr,
                Teachers = teachers,
                Note = note
            });
        }

        return sessions;
    }

    #endregion

    #region Parse Tuần 1-8

    private List<TeachingSession> ParseWeek1To8(string[] lines)
    {
        var sessions = new List<TeachingSession>();

        // Parse week start dates
        var weekStartDates = new Dictionary<int, DateTime>
        {
            [1] = new DateTime(2026, 7, 6),   // 06/07
            [2] = new DateTime(2026, 7, 13),  // 13/07
            [3] = new DateTime(2026, 7, 20),  // 20/07
            [4] = new DateTime(2026, 7, 27),  // 27/07
            [5] = new DateTime(2026, 8, 3),   // 03/08
            [6] = new DateTime(2026, 8, 10),  // 10/08
            [7] = new DateTime(2026, 8, 17),  // 17/08
            [8] = new DateTime(2026, 8, 24),  // 24/08
        };

        // Parse template table
        bool inWeek2Table = false;
        var templateRows = new List<(string ClassName, string DayStr, string TeacherStr, string Note)>();

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line.Contains("TUẦN 1–8") || line.Contains("TUẦN 1-8"))
            {
                inWeek2Table = true;
                continue;
            }

            if (inWeek2Table && line.StartsWith("## "))
                break;

            if (!inWeek2Table || !line.StartsWith("|") || line.Contains("---"))
                continue;

            var cols = line.Split('|', StringSplitOptions.None)
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToArray();

            if (cols.Length < 3 || cols[0] == "Lớp") continue;

            templateRows.Add((
                cols[0].Trim(),
                cols[1].Trim(),
                cols[2].Trim(),
                cols.Length > 3 ? cols[3].Trim() : ""
            ));
        }

        // Generate sessions for each week 2-9
        foreach (var (weekNum, weekStart) in weekStartDates)
        {
            // weekStart is a Sunday (start of Vietnamese week: Thứ 2 = Monday)
            // Actually weekStart is a Monday for Vietnamese calendar
            // Let's compute: weekStart is the date of Thứ 2 (Monday) of that week
            // Need to find the Monday of that week
            var monday = weekStart;
            while (monday.DayOfWeek != System.DayOfWeek.Monday)
                monday = monday.AddDays(1);

            // Actually, looking at the data:
            // Tuần 2: 06/07 = Sunday -> Thứ 2 is 07/07
            // Wait, 06/07/2025 is a Sunday
            // So weekStart points to Sunday, Thứ 2 = weekStart + 1
            // Let me re-check: the range says "06/07 – 12/07" which is Sun-Sat
            // Vietnamese school week: Thứ 2 (Mon) to Chủ nhật (Sun)
            // But the range 06/07 is a Sunday... Let me just use the weekStart as reference
            // and calculate each day

            foreach (var row in templateRows)
            {
                if (!DayMapping.TryGetValue(row.DayStr, out var targetDay))
                    continue;

                // Calculate the actual date
                var date = GetDateForDayOfWeek(weekStart, targetDay);

                sessions.Add(new TeachingSession
                {
                    ClassName = row.ClassName,
                    WeekNumber = weekNum,
                    Date = date,
                    DayOfWeek = row.DayStr,
                    Teachers = ParseTeachers(row.TeacherStr),
                    Note = row.Note
                });
            }
        }

        return sessions;
    }

    /// <summary>
    /// Tính ngày cụ thể từ ngày bắt đầu tuần và thứ mong muốn.
    /// weekStart là ngày đầu tiên được ghi trong lịch (có thể là Chủ nhật hoặc Thứ 2).
    /// </summary>
    private DateTime GetDateForDayOfWeek(DateTime weekStart, DayOfWeek targetDay)
    {
        // weekStart format: "06/07" for week 2 = Sunday July 6
        // We need to find the date within that week range that matches targetDay
        // The week range is 7 days starting from weekStart
        for (int d = 0; d < 7; d++)
        {
            var candidate = weekStart.AddDays(d);
            if (candidate.DayOfWeek == targetDay)
                return candidate;
        }

        // Fallback: shouldn't happen
        return weekStart;
    }

    #endregion

    #region Parse Tuần 9

    private List<TeachingSession> ParseWeek9(string[] lines)
    {
        var sessions = new List<TeachingSession>();
        bool inWeek10Table = false;

        for (int i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();

            if (line == "## TUẦN 9")
            {
                inWeek10Table = true;
                continue;
            }

            if (inWeek10Table && line.StartsWith("## "))
                break;

            if (!inWeek10Table || !line.StartsWith("|") || line.Contains("---"))
                continue;

            var cols = line.Split('|', StringSplitOptions.None)
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToArray();

            if (cols.Length < 4 || cols[0] == "Lớp") continue;

            sessions.Add(new TeachingSession
            {
                ClassName = cols[0].Trim(),
                WeekNumber = 9,
                Date = ParseDate(cols[1].Trim()),
                DayOfWeek = cols[2].Trim(),
                Teachers = ParseTeachers(cols[3].Trim()),
                Note = cols.Length > 4 ? cols[4].Trim() : ""
            });
        }

        return sessions;
    }

    #endregion

    #region Helpers

    private DateTime ParseDate(string dateStr)
    {
        // Format: "02/07/2025"
        return DateTime.ParseExact(dateStr, "dd/MM/yyyy", CultureInfo.InvariantCulture);
    }

    private List<TeacherInfo> ParseTeachers(string teacherStr)
    {
        var teachers = new List<TeacherInfo>();

        // Case: "Tài (Ca 1), Hải (Ca 2)"
        var parts = teacherStr.Split(',', StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var match = Regex.Match(part.Trim(), @"^(\p{L}+)\s*(?:\((.+?)\))?$");
            if (match.Success)
            {
                var name = match.Groups[1].Value.Trim();
                var shift = match.Groups[2].Success ? match.Groups[2].Value.Trim() : null;

                teachers.Add(new TeacherInfo
                {
                    Name = name,
                    IsFemale = FemaleTeachers.Contains(name),
                    Shift = shift
                });
            }
            else
            {
                // Fallback: just use the raw text as name
                var name = part.Trim();
                teachers.Add(new TeacherInfo
                {
                    Name = name,
                    IsFemale = FemaleTeachers.Contains(name)
                });
            }
        }

        return teachers;
    }

    /// <summary>
    /// Gán số buổi (SessionNumber) cho từng session trong cùng 1 lớp + tuần,
    /// sắp xếp theo ngày.
    /// </summary>
    private void AssignSessionNumbers(List<TeachingSession> sessions)
    {
        var grouped = sessions
            .GroupBy(s => new { s.ClassName, s.WeekNumber })
            .ToList();

        foreach (var group in grouped)
        {
            var sorted = group.OrderBy(s => s.Date).ToList();
            for (int i = 0; i < sorted.Count; i++)
            {
                sorted[i].SessionNumber = i + 1;
            }
        }
    }

    #endregion
}
