using Google.Apis.Classroom.v1;
using Google.Apis.Classroom.v1.Data;
using ScriptAutomation.Config;
using ScriptAutomation.Models;

namespace ScriptAutomation.Services;

/// <summary>
/// Service chính thực hiện 3 quy trình tự động hóa Google Classroom
/// </summary>
public class ClassroomAutomationService
{
    private readonly ClassroomService _classroom;
    private readonly List<TeachingSession> _sessions;

    public ClassroomAutomationService(ClassroomService classroom, List<TeachingSession> sessions)
    {
        _classroom = classroom;
        _sessions = sessions;
    }

    // ══════════════════════════════════════════════════════════════
    //  TIỆN ÍCH: Liệt kê tất cả lớp trên Google Classroom
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Liệt kê tất cả courses mà tài khoản có quyền truy cập.
    /// Dùng để lấy Course ID điền vào AppConfig.
    /// </summary>
    public async Task ListAllCoursesAsync()
    {
        Console.WriteLine("\n╔══════════════════════════════════════════════╗");
        Console.WriteLine("║   DANH SÁCH CÁC LỚP TRÊN GOOGLE CLASSROOM  ║");
        Console.WriteLine("╚══════════════════════════════════════════════╝\n");

        var request = _classroom.Courses.List();
        request.PageSize = 100;
        var response = await request.ExecuteAsync();

        if (response.Courses == null || response.Courses.Count == 0)
        {
            Console.WriteLine("  Không tìm thấy lớp nào!");
            return;
        }

        Console.WriteLine($"  {"Tên lớp",-30} {"Course ID",-20} {"Trạng thái",-15}");
        Console.WriteLine($"  {new string('─', 30)} {new string('─', 20)} {new string('─', 15)}");

        foreach (var course in response.Courses)
        {
            Console.WriteLine($"  {course.Name,-30} {course.Id,-20} {course.CourseState,-15}");
        }

        Console.WriteLine($"\n  Tổng: {response.Courses.Count} lớp");
        Console.WriteLine("\n  → Copy Course ID vào file Config/AppConfig.cs");
    }

    // ══════════════════════════════════════════════════════════════
    //  TIỆN ÍCH: Chọn lớp từ danh sách
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Lấy danh sách tên lớp (đã có Course ID) để chọn
    /// </summary>
    public List<string> GetAvailableClasses()
    {
        return _sessions
            .Select(s => s.ClassName)
            .Distinct()
            .Where(c => !string.IsNullOrEmpty(AppConfig.GetCourseId(c)))
            .ToList();
    }

    // ══════════════════════════════════════════════════════════════
    //  QUY TRÌNH 1: Tạo Topics (Thư mục) cho lớp học
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Tạo Topics cho 1 lớp:
    /// - "Thư mục Học Sinh"
    /// - "Thư mục [Cô/Thầy] [Tên]" cho mỗi GV dạy lớp đó
    /// </summary>
    public async Task<Dictionary<string, string>> CreateTopicsForClass(string className)
    {
        var courseId = AppConfig.GetCourseId(className);
        if (courseId == null)
        {
            Console.WriteLine($"  ⚠ Lớp '{className}' chưa có Course ID! Bỏ qua.");
            return new Dictionary<string, string>();
        }

        Console.WriteLine($"\n  📁 Tạo Topics cho lớp: {className} (ID: {courseId})");

        // Lấy danh sách topics đã có
        var existingTopics = await GetExistingTopicsAsync(courseId);

        var topicMap = new Dictionary<string, string>(); // TopicName -> TopicId

        // 1. Tạo "Thư mục Học Sinh"
        var studentTopicName = "Thư mục Học Sinh";
        var studentTopicId = await CreateTopicIfNotExists(courseId, studentTopicName, existingTopics);
        topicMap[studentTopicName] = studentTopicId;

        // 2. Tạo thư mục cho từng GV
        var teachers = GetUniqueTeachersForClass(className);
        foreach (var teacher in teachers)
        {
            var topicName = $"Thư mục {teacher.FullTitle}";
            var topicId = await CreateTopicIfNotExists(courseId, topicName, existingTopics);
            topicMap[topicName] = topicId;
        }

        Console.WriteLine($"  ✅ Hoàn thành tạo Topics cho lớp {className}!");
        return topicMap;
    }

    // ══════════════════════════════════════════════════════════════
    //  QUY TRÌNH 2: Tạo Assignments (Bài tập) cho Học Sinh
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Tạo bài tập trống cho 1 lớp cụ thể.
    /// - Tiêu đề: "Tuần X - Buổi Y - [Thầy/Cô] Z"
    /// - Hạn nộp: 18:00 ngày học
    /// - Lên lịch: 18:00 Thứ 7 tuần trước
    /// - Đặt vào Topic "Thư mục Học Sinh"
    /// </summary>
    public async Task RunProcess2_CreateAssignmentsForClass(
        string className, Dictionary<string, string> topicMap)
    {
        var courseId = AppConfig.GetCourseId(className);
        if (courseId == null)
        {
            Console.WriteLine($"  ⚠ Lớp '{className}' chưa có Course ID! Bỏ qua.");
            return;
        }

        // Tìm Topic "Thư mục Học Sinh"
        if (!topicMap.TryGetValue("Thư mục Học Sinh", out var studentTopicId))
        {
            Console.WriteLine($"  ⚠ Không tìm thấy 'Thư mục Học Sinh' cho lớp {className}!");
            return;
        }

        Console.WriteLine($"\n  📝 Tạo bài tập cho lớp: {className}");

        // Lấy sessions của lớp này
        var classSessions = _sessions
            .Where(s => s.ClassName == className)
            .OrderBy(s => s.WeekNumber)
            .ThenBy(s => s.SessionNumber)
            .ToList();

        int success = 0, fail = 0;

        foreach (var session in classSessions)
        {
            // Xử lý trường hợp nhiều GV (VD: Chuyên 9 có "Tài (Ca 1), Hải (Ca 2)")
            var teacherDisplay = GetTeacherDisplayForTitle(session.Teachers);

            var title = $"Tuần {session.WeekNumber} - Buổi {session.SessionNumber} - {teacherDisplay}";

            // Tính hạn nộp: 18:00 ngày học
            var dueDateTime = session.Date.Add(AppConfig.DueTime);

            // Tính scheduled time: 18:00 Thứ 7 tuần trước
            var scheduledDateTime = GetPreviousSaturdayAt18(session);

            try
            {
                var courseWork = new CourseWork
                {
                    Title = title,
                    Description = $"Bài tập {title}\n\n(Tài liệu sẽ được cập nhật sau)",
                    WorkType = "ASSIGNMENT",
                    State = "DRAFT", // Tạo nháp, sẽ scheduled publish
                    TopicId = studentTopicId,
                    MaxPoints = 100,

                    // Hạn nộp
                    DueDate = new Google.Apis.Classroom.v1.Data.Date
                    {
                        Year = dueDateTime.Year,
                        Month = dueDateTime.Month,
                        Day = dueDateTime.Day
                    },
                    DueTime = new TimeOfDay
                    {
                        Hours = 11, // 18:00 UTC+7 = 11:00 UTC
                        Minutes = 0
                    },

                    // Lên lịch đăng
                    ScheduledTimeDateTimeOffset = ToDateTimeOffset(scheduledDateTime)
                };

                var request = _classroom.Courses.CourseWork.Create(courseWork, courseId);
                var result = await request.ExecuteAsync();

                Console.WriteLine($"    ✓ {title}");
                Console.WriteLine($"      Hạn nộp: {dueDateTime:dd/MM/yyyy HH:mm} | Lên lịch: {scheduledDateTime:dd/MM/yyyy HH:mm}");
                success++;

                await Task.Delay(300); // Rate limiting
            }
            catch (Exception ex)
            {
                Console.WriteLine($"    ✗ LỖI tạo '{title}': {ex.Message}");
                fail++;
            }
        }

        Console.WriteLine($"\n  📊 Kết quả: {success} thành công, {fail} lỗi (trên tổng {classSessions.Count} buổi)");
    }

    // ══════════════════════════════════════════════════════════════
    //  QUY TRÌNH 3: Tạo Materials (Tài liệu) cho Giáo Viên
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Tạo tài liệu trống cho GV của 1 lớp.
    /// - Tiêu đề: "Tuần X - Buổi Y - Ngày DD/MM - [Cô/Thầy] Z"
    /// - Đặt vào "Thư mục [Cô/Thầy] Z"
    /// - Chỉ giao cho acc phụ "Kỹ Thuật" → HS không thấy topic GV
    /// </summary>
    public async Task RunProcess3_CreateMaterialsForClass(
        string className, Dictionary<string, string> topicMap)
    {
        var courseId = AppConfig.GetCourseId(className);
        if (courseId == null)
        {
            Console.WriteLine($"  ⚠ Lớp '{className}' chưa có Course ID! Bỏ qua.");
            return;
        }

        Console.WriteLine($"\n  📄 Tạo tài liệu GV cho lớp: {className}");

        // Tìm Student ID của acc phụ "Kỹ Thuật"
        var techStudentId = await FindStudentIdByNameAsync(courseId, "Kỹ Thuật");
        if (techStudentId == null)
        {
            Console.WriteLine($"  ❌ Không tìm thấy học sinh 'Kỹ Thuật' trong lớp {className}!");
            Console.WriteLine($"     → Hãy thêm acc phụ 'Kỹ Thuật' vào lớp trước.");
            return;
        }
        Console.WriteLine($"  ✓ Tìm thấy acc 'Kỹ Thuật' (ID: {techStudentId})");

        var classSessions = _sessions
            .Where(s => s.ClassName == className)
            .OrderBy(s => s.WeekNumber)
            .ThenBy(s => s.SessionNumber)
            .ToList();

        int success = 0, fail = 0;

        foreach (var session in classSessions)
        {
            foreach (var teacher in session.Teachers)
            {
                var dateStr = session.Date.ToString("d/M");
                var title = $"Tuần {session.WeekNumber} - Buổi {session.SessionNumber} - Ngày {dateStr} - {teacher.FullTitle}";

                // Tìm topic của GV
                var topicName = $"Thư mục {teacher.FullTitle}";
                if (!topicMap.TryGetValue(topicName, out var teacherTopicId))
                {
                    Console.WriteLine($"    ⚠ Không tìm thấy topic '{topicName}'!");
                    fail++;
                    continue;
                }

                try
                {
                    var material = new CourseWorkMaterial
                    {
                        Title = title,
                        Description = $"Tài liệu giảng dạy: {title}\n\n(File sẽ được upload sau)",
                        TopicId = teacherTopicId,
                        State = "PUBLISHED",
                        // Chỉ giao cho acc "Kỹ Thuật" → HS khác không thấy
                        AssigneeMode = "INDIVIDUAL_STUDENTS",
                        IndividualStudentsOptions = new IndividualStudentsOptions
                        {
                            StudentIds = new List<string> { techStudentId }
                        }
                    };

                    var request = _classroom.Courses.CourseWorkMaterials.Create(material, courseId);
                    var result = await request.ExecuteAsync();

                    Console.WriteLine($"    ✓ {title} → {topicName} (chỉ giao cho Kỹ Thuật)");
                    success++;

                    await Task.Delay(300);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ✗ LỖI tạo '{title}': {ex.Message}");
                    fail++;
                }
            }
        }

        Console.WriteLine($"\n  📊 Kết quả: {success} thành công, {fail} lỗi");
    }

    // ══════════════════════════════════════════════════════════════
    //  HỦY THAY ĐỔI (UNDO) - Xóa tất cả cho 1 lớp
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Xóa TẤT CẢ Topics, Assignments, Materials do tool tạo ra cho 1 lớp.
    /// </summary>
    public async Task UndoAllForClass(string className)
    {
        var courseId = AppConfig.GetCourseId(className);
        if (courseId == null)
        {
            Console.WriteLine($"  ⚠ Lớp '{className}' chưa có Course ID!");
            return;
        }

        Console.WriteLine($"\n  🗑️ HỦY tất cả thay đổi cho lớp: {className} (ID: {courseId})");
        Console.WriteLine("  ──────────────────────────────────────────");

        // 1. Xóa tất cả CourseWork (Assignments)
        Console.WriteLine("\n  [1/3] Xóa Bài tập (Assignments)...");
        await DeleteAllCourseWorkAsync(courseId);

        // 2. Xóa tất cả CourseWorkMaterials
        Console.WriteLine("\n  [2/3] Xóa Tài liệu (Materials)...");
        await DeleteAllMaterialsAsync(courseId);

        // 3. Xóa tất cả Topics do tool tạo (có prefix "Thư mục")
        Console.WriteLine("\n  [3/3] Xóa Chủ đề (Topics)...");
        await DeleteToolTopicsAsync(courseId);

        Console.WriteLine($"\n  ✅ Đã hủy tất cả thay đổi cho lớp {className}!");
    }

    /// <summary>
    /// Xóa tất cả CourseWork (Assignments) trong 1 lớp.
    /// Lấy cả PUBLISHED và DRAFT vì bài tập được tạo dạng DRAFT (lên lịch).
    /// </summary>
    private async Task DeleteAllCourseWorkAsync(string courseId)
    {
        try
        {
            var allCourseWork = new List<CourseWork>();

            // Lấy cả PUBLISHED và DRAFT (mặc định API chỉ trả PUBLISHED)
            var states = new[]
            {
                CoursesResource.CourseWorkResource.ListRequest.CourseWorkStatesEnum.PUBLISHED,
                CoursesResource.CourseWorkResource.ListRequest.CourseWorkStatesEnum.DRAFT
            };

            foreach (var state in states)
            {
                string? pageToken = null;
                do
                {
                    var listRequest = _classroom.Courses.CourseWork.List(courseId);
                    listRequest.PageSize = 100;
                    listRequest.CourseWorkStates = state;
                    if (pageToken != null) listRequest.PageToken = pageToken;

                    var response = await listRequest.ExecuteAsync();
                    if (response.CourseWork != null)
                        allCourseWork.AddRange(response.CourseWork);

                    pageToken = response.NextPageToken;
                } while (!string.IsNullOrEmpty(pageToken));
            }

            if (allCourseWork.Count == 0)
            {
                Console.WriteLine("    (Không có bài tập nào)");
                return;
            }

            Console.WriteLine($"    Tìm thấy {allCourseWork.Count} bài tập (PUBLISHED + DRAFT)");

            int count = 0;
            foreach (var work in allCourseWork)
            {

                try
                {
                    var deleteRequest = _classroom.Courses.CourseWork.Delete(courseId, work.Id);
                    await deleteRequest.ExecuteAsync();
                    Console.WriteLine($"    🗑️ Đã xóa: {work.Title} [{work.State}]");
                    count++;
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ✗ Không xóa được '{work.Title}': {ex.Message}");
                }
            }
            Console.WriteLine($"    → Đã xóa {count}/{allCourseWork.Count} bài tập");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ✗ Lỗi lấy danh sách bài tập: {ex.Message}");
        }
    }

    /// <summary>
    /// Xóa tất cả CourseWorkMaterials trong 1 lớp
    /// </summary>
    private async Task DeleteAllMaterialsAsync(string courseId)
    {
        try
        {
            var listRequest = _classroom.Courses.CourseWorkMaterials.List(courseId);
            listRequest.PageSize = 100;
            var response = await listRequest.ExecuteAsync();

            if (response.CourseWorkMaterial == null || response.CourseWorkMaterial.Count == 0)
            {
                Console.WriteLine("    (Không có tài liệu nào)");
                return;
            }

            int count = 0;
            foreach (var material in response.CourseWorkMaterial)
            {

                try
                {
                    var deleteRequest = _classroom.Courses.CourseWorkMaterials.Delete(courseId, material.Id);
                    await deleteRequest.ExecuteAsync();
                    Console.WriteLine($"    🗑️ Đã xóa: {material.Title}");
                    count++;
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ✗ Không xóa được '{material.Title}': {ex.Message}");
                }
            }
            Console.WriteLine($"    → Đã xóa {count}/{response.CourseWorkMaterial.Count} tài liệu");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ✗ Lỗi lấy danh sách tài liệu: {ex.Message}");
        }
    }

    /// <summary>
    /// Xóa các Topics có prefix "Thư mục" (do tool tạo ra).
    /// Topics khác sẽ KHÔNG bị xóa.
    /// </summary>
    private async Task DeleteToolTopicsAsync(string courseId)
    {
        try
        {
            var existingTopics = await GetExistingTopicsAsync(courseId);

            var toolTopics = existingTopics
                .Where(t => t.Key.StartsWith("Thư mục"))
                .ToList();

            if (toolTopics.Count == 0)
            {
                Console.WriteLine("    (Không có topic nào do tool tạo)");
                return;
            }

            int count = 0;
            foreach (var (name, topicId) in toolTopics)
            {
                try
                {
                    var deleteRequest = _classroom.Courses.Topics.Delete(courseId, topicId);
                    await deleteRequest.ExecuteAsync();
                    Console.WriteLine($"    🗑️ Đã xóa topic: {name}");
                    count++;
                    await Task.Delay(200);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    ✗ Không xóa được topic '{name}': {ex.Message}");
                }
            }
            Console.WriteLine($"    → Đã xóa {count}/{toolTopics.Count} topics");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ✗ Lỗi xóa topics: {ex.Message}");
        }
    }

    // ══════════════════════════════════════════════════════════════
    //  TÌM STUDENT ID THEO TÊN
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// Tìm Student ID trong lớp dựa trên tên hiển thị.
    /// Tìm kiếm theo keyword trong fullName (case-insensitive).
    /// </summary>
    private async Task<string?> FindStudentIdByNameAsync(string courseId, string nameKeyword)
    {
        try
        {
            var request = _classroom.Courses.Students.List(courseId);
            request.PageSize = 100;
            var response = await request.ExecuteAsync();

            if (response.Students == null || response.Students.Count == 0)
            {
                Console.WriteLine($"    ⚠ Lớp không có học sinh nào!");
                return null;
            }

            foreach (var student in response.Students)
            {
                var fullName = student.Profile?.Name?.FullName ?? "";
                if (fullName.Contains(nameKeyword, StringComparison.OrdinalIgnoreCase))
                {
                    return student.UserId;
                }
            }

            // Log danh sách HS để debug
            Console.WriteLine($"    ℹ Danh sách HS trong lớp:");
            foreach (var student in response.Students)
            {
                Console.WriteLine($"      - {student.Profile?.Name?.FullName} (ID: {student.UserId})");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ✗ Lỗi lấy danh sách HS: {ex.Message}");
        }

        return null;
    }

    // ══════════════════════════════════════════════════════════════
    //  HELPER METHODS
    // ══════════════════════════════════════════════════════════════

    private async Task<Dictionary<string, string>> GetExistingTopicsAsync(string courseId)
    {
        var result = new Dictionary<string, string>();
        try
        {
            var request = _classroom.Courses.Topics.List(courseId);
            var response = await request.ExecuteAsync();
            if (response.Topic != null)
            {
                foreach (var topic in response.Topic)
                {
                    result[topic.Name] = topic.TopicId;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ⚠ Lỗi khi lấy topics: {ex.Message}");
        }
        return result;
    }

    private async Task<string> CreateTopicIfNotExists(
        string courseId, string topicName, Dictionary<string, string> existing)
    {
        if (existing.TryGetValue(topicName, out var existingId))
        {
            Console.WriteLine($"    ↺ Topic đã tồn tại: '{topicName}'");
            return existingId;
        }

        try
        {
            var topic = new Topic { Name = topicName };
            var request = _classroom.Courses.Topics.Create(topic, courseId);
            var result = await request.ExecuteAsync();
            Console.WriteLine($"    ✓ Tạo topic: '{topicName}'");
            existing[topicName] = result.TopicId; // Cache lại
            return result.TopicId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"    ✗ Lỗi tạo topic '{topicName}': {ex.Message}");
            return string.Empty;
        }
    }

    private List<TeacherInfo> GetUniqueTeachersForClass(string className)
    {
        return _sessions
            .Where(s => s.ClassName == className)
            .SelectMany(s => s.Teachers)
            .GroupBy(t => t.Name)
            .Select(g => g.First())
            .ToList();
    }

    /// <summary>
    /// Tạo chuỗi hiển thị GV cho tiêu đề bài tập.
    /// VD: 1 GV: "Thầy Hải"
    ///     2 GV: "Thầy Tài, Thầy Hải"
    /// </summary>
    private string GetTeacherDisplayForTitle(List<TeacherInfo> teachers)
    {
        return string.Join(", ", teachers.Select(t => t.FullTitle));
    }

    /// <summary>
    /// Tính ngày Thứ 7 tuần trước, lúc 18:00 (giờ VN).
    /// Logic: Lấy ngày đầu tuần (Thứ 2) của tuần chứa session, lùi về Thứ 7 = Thứ 2 - 2
    /// </summary>
    private DateTime GetPreviousSaturdayAt18(TeachingSession session)
    {
        // Tìm Thứ 2 (Monday) của tuần chứa buổi học
        var sessionDate = session.Date;
        var monday = sessionDate;

        // Lùi về Monday
        while (monday.DayOfWeek != DayOfWeek.Monday)
        {
            monday = monday.AddDays(-1);
        }

        // Thứ 7 tuần trước = Monday - 2
        var previousSaturday = monday.AddDays(-2);

        return previousSaturday.Add(AppConfig.ScheduleTime);
    }

    /// <summary>
    /// Chuyển DateTime (giờ VN) sang DateTimeOffset (UTC)
    /// </summary>
    private DateTimeOffset ToDateTimeOffset(DateTime vietnamDateTime)
    {
        // VN = UTC+7
        var dto = new DateTimeOffset(vietnamDateTime, TimeSpan.FromHours(7));
        return dto.ToUniversalTime();
    }
}
