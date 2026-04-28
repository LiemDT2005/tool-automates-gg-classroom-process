using Google.Apis.Auth.OAuth2;
using Google.Apis.Classroom.v1;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace ScriptAutomation.Services;

/// <summary>
/// Xử lý OAuth 2.0 authentication với Google
/// </summary>
public class GoogleAuthService
{
    private static readonly string[] Scopes = {
        ClassroomService.Scope.ClassroomCourses,
        ClassroomService.Scope.ClassroomCourseworkStudents,
        ClassroomService.Scope.ClassroomCourseworkMe,
        ClassroomService.Scope.ClassroomTopics,
        ClassroomService.Scope.ClassroomAnnouncements,
        ClassroomService.Scope.ClassroomCourseworkmaterials,
        ClassroomService.Scope.ClassroomRostersReadonly
    };

    private const string ApplicationName = "Classroom Automation";

    /// <summary>
    /// Tạo ClassroomService đã authenticated.
    /// Lần đầu sẽ mở browser để đăng nhập, sau đó token được lưu lại.
    /// </summary>
    public async Task<ClassroomService> AuthenticateAsync(string credentialsPath)
    {
        if (!File.Exists(credentialsPath))
        {
            throw new FileNotFoundException(
                $"Không tìm thấy file credentials tại: {credentialsPath}\n" +
                "Hãy tải file JSON từ Google Cloud Console và đặt vào thư mục project.");
        }

        UserCredential credential;

        using (var stream = new FileStream(credentialsPath, FileMode.Open, FileAccess.Read))
        {
            // Token sẽ được lưu tại thư mục "token" cạnh file exe
            var tokenPath = Path.Combine(
                Path.GetDirectoryName(credentialsPath) ?? ".",
                "token"
            );

            credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                GoogleClientSecrets.FromStream(stream).Secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(tokenPath, true)
            );

            Console.WriteLine($"[Auth] Token lưu tại: {tokenPath}");
        }

        var service = new ClassroomService(new BaseClientService.Initializer()
        {
            HttpClientInitializer = credential,
            ApplicationName = ApplicationName
        });

        Console.WriteLine("[Auth] Đăng nhập Google thành công!");
        return service;
    }
}
