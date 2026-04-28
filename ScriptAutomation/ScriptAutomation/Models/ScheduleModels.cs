namespace ScriptAutomation.Models;

/// <summary>
/// Represents a single teaching session (1 buổi dạy cụ thể)
/// </summary>
public class TeachingSession
{
    /// <summary>Tên lớp (VD: "Chuyên 6", "Anh 9")</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Tuần thứ mấy (1-10)</summary>
    public int WeekNumber { get; set; }

    /// <summary>Buổi thứ mấy trong tuần đó cho lớp này</summary>
    public int SessionNumber { get; set; }

    /// <summary>Ngày cụ thể</summary>
    public DateTime Date { get; set; }

    /// <summary>Thứ trong tuần (VD: "Thứ 2", "Thứ 5")</summary>
    public string DayOfWeek { get; set; } = string.Empty;

    /// <summary>Danh sách giáo viên dạy buổi này</summary>
    public List<TeacherInfo> Teachers { get; set; } = new();

    /// <summary>Ghi chú (VD: "2 ca")</summary>
    public string Note { get; set; } = string.Empty;
}

/// <summary>
/// Thông tin 1 giáo viên
/// </summary>
public class TeacherInfo
{
    /// <summary>Tên GV (VD: "Hải", "Đại")</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Giới tính: true = nữ (Cô), false = nam (Thầy)</summary>
    public bool IsFemale { get; set; }

    /// <summary>Ghi chú ca (VD: "Ca 1", "Ca 2")</summary>
    public string? Shift { get; set; }

    /// <summary>Trả về danh xưng: "Cô" hoặc "Thầy"</summary>
    public string Title => IsFemale ? "Cô" : "Thầy";

    /// <summary>Trả về tên đầy đủ: VD "Thầy Hải", "Cô Hằng"</summary>
    public string FullTitle => $"{Title} {Name}";
}

/// <summary>
/// Thông tin cấu hình cho 1 lớp trên Google Classroom
/// </summary>
public class ClassConfig
{
    /// <summary>Tên lớp (phải khớp với tên trong lịch dạy)</summary>
    public string ClassName { get; set; } = string.Empty;

    /// <summary>Course ID trên Google Classroom</summary>
    public string CourseId { get; set; } = string.Empty;
}

/// <summary>
/// Kết quả sau khi tạo Topic
/// </summary>
public class TopicResult
{
    public string CourseId { get; set; } = string.Empty;
    public string TopicId { get; set; } = string.Empty;
    public string TopicName { get; set; } = string.Empty;
}
