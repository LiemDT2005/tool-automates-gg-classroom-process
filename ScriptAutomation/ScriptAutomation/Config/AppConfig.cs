using ScriptAutomation.Models;

namespace ScriptAutomation.Config;

/// <summary>
/// Cấu hình mapping giữa tên lớp trong lịch dạy và Course ID trên Google Classroom.
/// BẠN CẦN ĐIỀN COURSE ID VÀO ĐÂY!
/// </summary>
public static class AppConfig
{
    /// <summary>
    /// Đường dẫn tới file credentials.json (tải từ Google Cloud Console)
    /// </summary>
    public static string CredentialsPath => Path.Combine(
        AppDomain.CurrentDomain.BaseDirectory, "credentials.json"
    );

    /// <summary>
    /// Đường dẫn tới file lịch dạy
    /// </summary>
    public static string SchedulePath
    {
        get
        {
            // Thử tìm ở thư mục cha của dự án (Automation/)
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            
            // Tìm ngược lên trên để thấy file lich_day.md
            var current = new DirectoryInfo(baseDir);
            while (current != null)
            {
                var target = Path.Combine(current.FullName, "lich_day.md");
                if (File.Exists(target)) return target;
                
                // Thử tìm trong thư mục Automation ở cùng cấp
                var automationTarget = Path.Combine(current.FullName, "Automation", "lich_day.md");
                if (File.Exists(automationTarget)) return automationTarget;

                current = current.Parent;
            }

            // Mặc định lùi 2 cấp từ project root (khi chạy dotnet run)
            return Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "lich_day.md");
        }
    }

    /// <summary>
    /// Múi giờ Việt Nam (UTC+7)
    /// </summary>
    public static readonly TimeZoneInfo VietnamTimeZone =
        TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

    /// <summary>
    /// Giờ hạn nộp bài tập (18:00)
    /// </summary>
    public static readonly TimeSpan DueTime = new(18, 0, 0);

    /// <summary>
    /// Giờ lên lịch đăng bài (18:00 Thứ 7 tuần trước)
    /// </summary>
    public static readonly TimeSpan ScheduleTime = new(18, 0, 0);

    /// <summary>
    /// Mapping tên lớp -> Course ID trên Google Classroom.
    /// 
    /// HƯỚNG DẪN LẤY COURSE ID:
    /// 1. Mở Google Classroom trên trình duyệt
    /// 2. Vào lớp học cần lấy ID
    /// 3. Nhìn URL: https://classroom.google.com/c/XXXXXXXXXX
    ///    -> XXXXXXXXXX chính là Course ID
    /// 
    /// Hoặc chạy chương trình với option "list" để liệt kê tất cả lớp.
    /// </summary>
    public static readonly List<ClassConfig> CourseMapping = new()
    {
        // ===== TOÁN CHUYÊN =====
        new() { ClassName = "Chuyên 6",   CourseId = "861053741216" },  // TODO: Điền Course ID
        new() { ClassName = "Chuyên 7",   CourseId = "861055197930" },
        new() { ClassName = "Chuyên 8",   CourseId = "861055747586" },
        new() { ClassName = "Chuyên 9",   CourseId = "860844700611" },

        // ===== TOÁN NÂNG CAO =====
        new() { ClassName = "Nâng cao 6", CourseId = "861045834968" },
        new() { ClassName = "Nâng cao 7", CourseId = "861040346227" },
        new() { ClassName = "Nâng cao 8", CourseId = "849550384660" },
        new() { ClassName = "Nâng cao 9", CourseId = "861053506598" },

        // ===== LỚP 5 =====
        new() { ClassName = "Lớp 5",      CourseId = "849550177713" },

        // ===== ANH VĂN =====
        new() { ClassName = "Anh 6",      CourseId = "861053753455" },
        new() { ClassName = "Anh 7",      CourseId = "861056509463" },
        new() { ClassName = "Anh 8",      CourseId = "849550429646" },
        new() { ClassName = "Anh 9",      CourseId = "861053194327" },

        // ===== LỚP TEST =====
        new() { ClassName = "test",       CourseId = "861730952163" }, 
    };

    /// <summary>
    /// Lấy Course ID theo tên lớp
    /// </summary>
    public static string? GetCourseId(string className)
    {
        var config = CourseMapping.FirstOrDefault(c =>
            c.ClassName.Equals(className, StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrEmpty(config?.CourseId) ? null : config.CourseId;
    }
}
