using E_Form_Best.Context;
using Microsoft.AspNetCore.Mvc;

namespace E_Form_Best.Areas.QLCongViec.Controllers
{
    [Area("QLCongViec")]
    public class CongViecFormController : Controller
    {
        public ITFormContext _context;
        public CongViecFormController()
        {
            _context = new ITFormContext();
        }
        #region logo
        [Route("QLCongViec/Logo")]
        public IActionResult Logo()
        {
            return View();
        }
        #endregion

    }
}
