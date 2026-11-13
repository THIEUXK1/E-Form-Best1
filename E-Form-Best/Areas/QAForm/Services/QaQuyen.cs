using System.Security.Claims;

namespace E_Form_Best.Areas.QAForm.Services
{
    /// <summary>
    /// Ai được vào phân hệ QA. Dùng chung cho policy của QAFormController và thẻ QA ở trang menu,
    /// để menu không hiện thẻ mà bấm vào lại bị chặn (hoặc ngược lại).
    /// </summary>
    public static class QaQuyen
    {
        public const string Policy = "QAForm";
        public const string MaQuyen = "QA_VIEW";

        public static bool CoQuyenQa(ClaimsPrincipal? user)
        {
            if (user?.Identity?.IsAuthenticated != true) return false;
            if (user.IsInRole("All")) return true;
            if (!user.IsInRole(MaQuyen)) return false;
            // QA_VIEW gán cho bộ phận "QA 品控部"; tài khoản MEGA dùng chung chuỗi phòng ban này nên khớp nhầm.
            // Theo yêu cầu chỉ mở cho BPVN: chặn MEGA (tài khoản BPVN có cả dòng chưa ghi công ty nên không đòi = BPVN).
            var congTy = user.FindFirst("TenCongTy")?.Value?.Trim() ?? "";
            return !congTy.Equals("MEGA", StringComparison.OrdinalIgnoreCase);
        }
    }
}
