using WebPush;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace E_Form_Best.Areas.AdminForm.Controllers
{
    public class PushNotificationService
    {
        private readonly VapidDetails _vapidDetails;

        // Constructor lấy thông tin Vapid từ cấu hình hệ thống
        public PushNotificationService(IOptions<VapidDetails> vapidOptions)
        {
            _vapidDetails = vapidOptions.Value;
        }

        /// <summary>
        /// Gửi thông báo đẩy đến một thiết bị cụ thể
        /// </summary>
        /// <param name="endpoint">URL định danh thiết bị</param>
        /// <param name="p256dh">Khóa mã hóa thiết bị</param>
        /// <param name="auth">Mã xác thực thiết bị</param>
        /// <param name="title">Tiêu đề thông báo</param>
        /// <param name="message">Nội dung thông báo</param>
        /// <param name="url">Đường dẫn khi click vào thông báo sẽ mở ra</param>
        public async Task SendNotification(string endpoint, string p256dh, string auth, string title, string message, string url)
        {
            // Nếu User chưa đăng ký Push trên trình duyệt này thì bỏ qua
            if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(p256dh) || string.IsNullOrEmpty(auth))
            {
                return;
            }

            var subscription = new PushSubscription(endpoint, p256dh, auth);
            var webPushClient = new WebPushClient();

            // Tạo gói dữ liệu (Payload) gửi đi
            var payload = JsonConvert.SerializeObject(new
            {
                title = title,
                message = message,
                url = url
            });

            try
            {
                await webPushClient.SendNotificationAsync(subscription, payload, _vapidDetails);
            }
            catch (WebPushException ex)
            {
                // Status 410 (Gone) có nghĩa là Token đã hết hạn hoặc người dùng đã chặn thông báo
                if (ex.StatusCode == System.Net.HttpStatusCode.Gone)
                {
                    // Bạn có thể thêm logic xóa thông tin Push của User này trong DB tại đây nếu muốn
                    Console.WriteLine("Thiết bị này đã hết hạn đăng ký thông báo.");
                }
                else
                {
                    Console.WriteLine($"Lỗi gửi thông báo: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Lỗi hệ thống: {ex.Message}");
            }
        }
    }
}