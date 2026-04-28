using ScriptAutomation.Config;
using ScriptAutomation.Models;
using ScriptAutomation.Services;

namespace ScriptAutomation;

internal class Program
{
    static async Task Main(string[] args)
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        Console.WriteLine("╔══════════════════════════════════════════════════════╗");
        Console.WriteLine("║   GOOGLE CLASSROOM AUTOMATION - HÈ 2025             ║");
        Console.WriteLine("║   Tự động tạo Topics, Bài tập, Tài liệu            ║");
        Console.WriteLine("╚══════════════════════════════════════════════════════╝");

        // ═══ BƯỚC 1: Parse lịch dạy ═══
        Console.WriteLine("\n[1/2] Đọc lịch dạy...");
        var schedulePath = AppConfig.SchedulePath;

        if (!File.Exists(schedulePath))
        {
            Console.WriteLine($"  ✗ Không tìm thấy file lịch dạy!");
            Console.WriteLine($"    Đường dẫn đã thử: {schedulePath}");
            Console.WriteLine("    Hãy đảm bảo file lich_day.md nằm ở thư mục Automation/");
            return;
        }

        var parser = new ScheduleParser();
        var sessions = parser.Parse(schedulePath);
        var allClasses = parser.GetAllClasses(sessions);
        Console.WriteLine($"  ✓ Đã parse {sessions.Count} buổi học cho {allClasses.Count} lớp");

        // ═══ BƯỚC 2: Đăng nhập Google ═══
        Console.WriteLine("\n[2/2] Đăng nhập Google...");
        var authService = new GoogleAuthService();

        var credPath = AppConfig.CredentialsPath;
        if (!File.Exists(credPath))
            credPath = Path.Combine(Directory.GetCurrentDirectory(), "credentials.json");

        if (!File.Exists(credPath))
        {
            Console.WriteLine($"  ✗ Không tìm thấy file credentials.json!");
            Console.WriteLine("    Tải file từ Google Cloud Console và đặt vào thư mục project.");
            return;
        }

        var classroomService = await authService.AuthenticateAsync(credPath);
        var automation = new ClassroomAutomationService(classroomService, sessions);

        // ═══ MENU CHÍNH ═══
        while (true)
        {
            Console.WriteLine("\n┌─────────────────────────────────────────────┐");
            Console.WriteLine("│              MENU CHÍNH                     │");
            Console.WriteLine("├─────────────────────────────────────────────┤");
            Console.WriteLine("│  0. Liệt kê lớp trên Classroom (lấy ID)   │");
            Console.WriteLine("│  1. QT1: Tạo Topics cho 1 lớp             │");
            Console.WriteLine("│  2. QT2: Tạo Bài tập cho 1 lớp            │");
            Console.WriteLine("│  3. QT3: Tạo Tài liệu GV cho 1 lớp       │");
            Console.WriteLine("│  4. Chạy tất cả (1→2→3) cho 1 lớp         │");
            Console.WriteLine("│  5. Preview lịch 1 lớp                    │");
            Console.WriteLine("│  6. ⚠ HỦY tất cả thay đổi cho 1 lớp      │");
            Console.WriteLine("│  q. Thoát                                 │");
            Console.WriteLine("└─────────────────────────────────────────────┘");
            Console.Write("\n  Lựa chọn: ");

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "0":
                    await automation.ListAllCoursesAsync();
                    break;

                case "1":
                {
                    var className = PickClass(automation);
                    if (className == null) break;
                    await automation.CreateTopicsForClass(className);
                    break;
                }

                case "2":
                {
                    var className = PickClass(automation);
                    if (className == null) break;

                    // Tự động lấy/tạo topics trước
                    Console.WriteLine("  ℹ Đang kiểm tra Topics...");
                    var topicMap = await automation.CreateTopicsForClass(className);
                    await automation.RunProcess2_CreateAssignmentsForClass(className, topicMap);
                    break;
                }

                case "3":
                {
                    var className = PickClass(automation);
                    if (className == null) break;

                    Console.WriteLine("  ℹ Đang kiểm tra Topics...");
                    var topicMap = await automation.CreateTopicsForClass(className);
                    await automation.RunProcess3_CreateMaterialsForClass(className, topicMap);
                    break;
                }

                case "4":
                {
                    var className = PickClass(automation);
                    if (className == null) break;

                    Console.WriteLine($"\n  🚀 Chạy tất cả quy trình cho lớp: {className}");
                    var topicMap = await automation.CreateTopicsForClass(className);
                    await automation.RunProcess2_CreateAssignmentsForClass(className, topicMap);
                    await automation.RunProcess3_CreateMaterialsForClass(className, topicMap);
                    Console.WriteLine($"\n  🎉 ĐÃ HOÀN THÀNH tất cả cho lớp {className}!");
                    break;
                }

                case "5":
                {
                    var className = PickClassFromSchedule(allClasses);
                    if (className == null) break;
                    PreviewClassSchedule(sessions, className);
                    break;
                }

                case "6":
                {
                    var className = PickClass(automation);
                    if (className == null) break;

                    Console.WriteLine($"\n  ⚠⚠⚠ CẢNH BÁO ⚠⚠⚠");
                    Console.WriteLine($"  Bạn sắp XÓA TẤT CẢ bài tập, tài liệu, và topics");
                    Console.WriteLine($"  của lớp: {className}");
                    Console.WriteLine($"  Hành động này KHÔNG THỂ hoàn tác!");
                    Console.Write($"\n  Gõ tên lớp '{className}' để xác nhận: ");

                    var confirm = Console.ReadLine()?.Trim();
                    if (confirm != className)
                    {
                        Console.WriteLine("  ❌ Hủy bỏ! Tên lớp không khớp.");
                        break;
                    }

                    await automation.UndoAllForClass(className);
                    break;
                }

                case "q":
                case "Q":
                    Console.WriteLine("\n  Tạm biệt! 👋");
                    return;

                default:
                    Console.WriteLine("  ⚠ Lựa chọn không hợp lệ!");
                    break;
            }
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  CHỌN LỚP
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Hiển thị danh sách lớp đã có Course ID, cho user chọn bằng số thứ tự
    /// </summary>
    static string? PickClass(ClassroomAutomationService automation)
    {
        var classes = automation.GetAvailableClasses();

        if (classes.Count == 0)
        {
            Console.WriteLine("\n  ✗ Chưa có lớp nào được cấu hình Course ID!");
            Console.WriteLine("    → Chạy option 0 để lấy ID, rồi điền vào AppConfig.cs");
            return null;
        }

        Console.WriteLine("\n  ── Chọn lớp ──");
        for (int i = 0; i < classes.Count; i++)
        {
            Console.WriteLine($"    {i + 1}. {classes[i]}");
        }
        Console.Write($"\n  Nhập số (1-{classes.Count}): ");

        var input = Console.ReadLine()?.Trim();
        if (int.TryParse(input, out int idx) && idx >= 1 && idx <= classes.Count)
        {
            return classes[idx - 1];
        }

        Console.WriteLine("  ⚠ Lựa chọn không hợp lệ!");
        return null;
    }

    /// <summary>
    /// Chọn lớp từ tất cả lớp trong lịch dạy (dù chưa có Course ID)
    /// </summary>
    static string? PickClassFromSchedule(List<string> allClasses)
    {
        Console.WriteLine("\n  ── Chọn lớp ──");
        for (int i = 0; i < allClasses.Count; i++)
        {
            var hasId = !string.IsNullOrEmpty(AppConfig.GetCourseId(allClasses[i]));
            var status = hasId ? "✓" : "✗ (chưa có ID)";
            Console.WriteLine($"    {i + 1}. {allClasses[i]} {status}");
        }
        Console.Write($"\n  Nhập số (1-{allClasses.Count}): ");

        var input = Console.ReadLine()?.Trim();
        if (int.TryParse(input, out int idx) && idx >= 1 && idx <= allClasses.Count)
        {
            return allClasses[idx - 1];
        }

        Console.WriteLine("  ⚠ Lựa chọn không hợp lệ!");
        return null;
    }

    // ══════════════════════════════════════════════════════════════
    //  PREVIEW
    // ══════════════════════════════════════════════════════════════

    static void PreviewClassSchedule(List<TeachingSession> sessions, string className)
    {
        var classSessions = sessions
            .Where(s => s.ClassName == className)
            .OrderBy(s => s.WeekNumber)
            .ThenBy(s => s.SessionNumber)
            .ToList();

        Console.WriteLine($"\n  ══ LỊCH DẠY: {className} ({classSessions.Count} buổi) ══\n");
        Console.WriteLine($"  {"Tuần",-6} {"Buổi",-6} {"Ngày",-12} {"Thứ",-8} {"Giáo viên",-25} {"Ghi chú"}");
        Console.WriteLine($"  {new string('─', 6)} {new string('─', 6)} {new string('─', 12)} {new string('─', 8)} {new string('─', 25)} {new string('─', 10)}");

        foreach (var s in classSessions)
        {
            var teachers = string.Join(", ", s.Teachers.Select(t => t.FullTitle));
            Console.WriteLine($"  {s.WeekNumber,-6} {s.SessionNumber,-6} {s.Date:dd/MM/yyyy}  {s.DayOfWeek,-8} {teachers,-25} {s.Note}");
        }

        // Thêm preview tiêu đề bài tập + tài liệu sẽ tạo
        Console.WriteLine($"\n  ── Preview tiêu đề Bài tập (QT2) ──");
        foreach (var s in classSessions)
        {
            var teacherDisplay = string.Join(", ", s.Teachers.Select(t => t.FullTitle));
            Console.WriteLine($"    📝 Tuần {s.WeekNumber} - Buổi {s.SessionNumber} - {teacherDisplay}");
        }

        Console.WriteLine($"\n  ── Preview tiêu đề Tài liệu GV (QT3) ──");
        foreach (var s in classSessions)
        {
            foreach (var t in s.Teachers)
            {
                Console.WriteLine($"    📄 Tuần {s.WeekNumber} - Buổi {s.SessionNumber} - Ngày {s.Date:d/M} - {t.FullTitle}");
            }
        }

        // Preview danh sách GV unique
        var uniqueTeachers = classSessions
            .SelectMany(s => s.Teachers)
            .GroupBy(t => t.Name)
            .Select(g => g.First())
            .ToList();

        Console.WriteLine($"\n  ── Topics sẽ tạo (QT1) ──");
        Console.WriteLine($"    📁 Thư mục Học Sinh");
        foreach (var t in uniqueTeachers)
            Console.WriteLine($"    📁 Thư mục {t.FullTitle}");
    }
}
