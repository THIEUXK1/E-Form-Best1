// Dịch offline (không qua Google Translate) cho khối có [data-dich-offline].
// Trang đặt data-ngon-ngu="@CultureInfo.CurrentUICulture.Name" + translate="no" lên khối đó: máy chủ biết ngôn ngữ
// từ cookie .AspNetCore.Culture nên không phụ thuộc mạng ra Internet. Phần lớn nội dung báo cáo do JS vẽ
// bằng innerHTML nên dịch theo nút chữ (MutationObserver) thay vì sửa từng chuỗi trong 6 file JS.
// Chữ trên canvas (Chart.js) không phải nút DOM → nơi vẽ biểu đồ gọi window.dichGd().
(function () {
    'use strict';

    var goc = document.querySelector('[data-ngon-ngu]');
    var ma = goc ? (goc.getAttribute('data-ngon-ngu') || '').toLowerCase() : '';
    // 0 = English, 1 = 中文; -1 = tiếng Việt → không dịch gì
    var i = ma.indexOf('en') === 0 ? 0 : ma.indexOf('zh') === 0 ? 1 : -1;

    // Khoá = đúng chữ tiếng Việt đã trim. [English, 中文]
    var TU = {
        // --- Khung trang / tab ---
        'Thiết bị': ['Devices', '设备'],
        'Thống kê đơn IT': ['IT request statistics', 'IT工单统计'],
        'Thống kê công việc': ['Task statistics', '工作统计'],
        'Xuất báo cáo tổng hợp': ['Export summary report', '导出综合报告'],
        'Xuất toàn bộ các mục vào một file Excel': ['Export all sections into one Excel file', '将所有项目导出到一个Excel文件'],
        'Đang tải báo cáo...': ['Loading report...', '正在加载报告...'],
        'Đang tải...': ['Loading...', '加载中...'],
        'Đang tải số liệu...': ['Loading data...', '正在加载数据...'],
        'Đang tạo file...': ['Generating file...', '正在生成文件...'],
        'So với kỳ trước': ['Compared with previous period', '与上期比较'],
        'Kỳ so sánh': ['Comparison period', '比较期间'],
        'Kỳ báo cáo': ['Report period', '报告期间'],
        'Phạm vi báo cáo': ['Report scope', '报告范围'],
        'Tuần này': ['This week', '本周'],
        'Tuần trước': ['Last week', '上周'],
        '7 ngày gần nhất': ['Last 7 days', '最近7天'],
        '30 ngày gần nhất': ['Last 30 days', '最近30天'],
        'Tháng này': ['This month', '本月'],
        'Tháng trước': ['Last month', '上月'],
        'Tuỳ chọn...': ['Custom...', '自定义...'],
        'Từ ngày': ['From date', '开始日期'],
        'Đến ngày': ['To date', '结束日期'],
        'Xem': ['View', '查看'],
        'Xuất Excel': ['Export Excel', '导出Excel'],
        'Xuất báo cáo cho': ['Export report for', '导出报告：'],
        'BÁO CÁO ĐỊNH KỲ': ['PERIODIC REPORT', '定期报告'],
        'Người lập': ['Prepared by', '制表人'],
        'Bạn không có quyền xem báo cáo này.': ['You do not have permission to view this report.', '您无权查看此报告。'],
        'Không tải được báo cáo': ['Could not load the report', '无法加载报告'],
        'Không tải được báo cáo.': ['Could not load the report.', '无法加载报告。'],
        'Không tải được số liệu thiết bị': ['Could not load device data', '无法加载设备数据'],
        'Không tải được số liệu so sánh.': ['Could not load comparison data.', '无法加载比较数据。'],
        'máy chủ báo lỗi': ['server returned an error', '服务器报错'],

        // --- Camera / AP / Switch ---
        'Tổng quan camera': ['Camera overview', '摄像头概览'],
        'Tổng quan AP Wi-Fi': ['Wi-Fi AP overview', 'Wi-Fi AP概览'],
        'Tổng quan Switch': ['Switch overview', '交换机概览'],
        'Công ty': ['Company', '公司'],
        'Toàn bộ': ['All', '全部'],
        'Lần mất KN': ['Disconnects', '断线次数'],
        'Lần mất KN (kỳ)': ['Disconnects (period)', '断线次数（期内）'],
        'Camera bị mất': ['Cameras affected', '断线摄像头'],
        'AP bị mất': ['APs affected', '断线AP'],
        'Switch bị mất': ['Switches affected', '断线交换机'],
        'Tổng giờ mất': ['Total downtime', '总断线时长'],
        'Giờ mất': ['Downtime', '断线时长'],
        'Giờ mất (kỳ)': ['Downtime (period)', '断线时长（期内）'],
        '% hoạt động': ['% online', '在线率'],
        'Kỳ này': ['This period', '本期'],
        'Kỳ trước': ['Previous period', '上期'],
        'Thay đổi': ['Change', '变化'],
        'So kỳ trước': ['vs. previous', '与上期比'],
        'Tổng hợp theo công ty': ['Summary by company', '按公司汇总'],
        'Hiện tại': ['Current', '当前'],
        'Trong kỳ': ['In period', '期内'],
        'Tổng': ['Total', '总计'],
        'Tổng camera': ['Total cameras', '摄像头总数'],
        'Tổng AP': ['Total APs', 'AP总数'],
        'Tổng switch': ['Total switches', '交换机总数'],
        'Theo dõi': ['Monitored', '监控中'],
        // Dùng chung cho camera/AP (đang online) và trạng thái thiết bị (đang dùng) → chọn chữ hợp cả hai
        'Hoạt động': ['Active', '运行中'],
        'hoạt động': ['active', '运行中'],
        'Mất KN': ['Offline', '离线'],
        'Đầu ghi': ['NVR', '录像机'],
        'Chưa có ảnh': ['No snapshot', '无截图'],
        'Chưa có IP': ['No IP', '无IP'],
        'Hết BH': ['Warranty expired', '保修过期'],
        'Theo đầu ghi': ['By NVR', '按录像机'],
        'Theo bộ phận': ['By department', '按部门'],
        'Bộ phận': ['Department', '部门'],
        'Kênh': ['Channel', '通道'],
        'Camera': ['Camera', '摄像头'],
        'Tên AP': ['AP name', 'AP名称'],
        'Tên switch': ['Switch name', '交换机名称'],
        'Vị trí': ['Location', '位置'],
        'Mất từ': ['Offline since', '断线开始'],
        'Đã mất': ['Offline for', '已断线'],
        'Ghi chú': ['Note', '备注'],
        'Lần': ['Times', '次数'],
        'Camera đang mất kết nối': ['Cameras currently offline', '当前离线的摄像头'],
        'AP đang mất kết nối': ['APs currently offline', '当前离线的AP'],
        'Switch đang mất kết nối': ['Switches currently offline', '当前离线的交换机'],
        'Mất kết nối nhiều nhất trong kỳ (top 20)': ['Most disconnects in period (top 20)', '期内断线最多（前20）'],
        'Không rõ': ['Unknown', '未知'],
        'đang mất': ['offline', '离线中'],
        'lần mất KN': ['disconnects', '次断线'],
        'lần mất': ['disconnects', '断线'],
        'Chưa có dữ liệu camera.': ['No camera data yet.', '暂无摄像头数据。'],
        'Chưa có dữ liệu đầu ghi.': ['No NVR data yet.', '暂无录像机数据。'],
        'Chưa có AP nào.': ['No APs yet.', '暂无AP。'],
        'Chưa có switch nào.': ['No switches yet.', '暂无交换机。'],
        'Chưa có AP nào đang theo dõi.': ['No monitored APs.', '暂无监控中的AP。'],
        'Chưa có switch nào đang theo dõi.': ['No monitored switches.', '暂无监控中的交换机。'],
        'Không có camera nào đang mất kết nối.': ['No cameras are offline.', '没有离线的摄像头。'],
        'Không có AP nào đang mất kết nối.': ['No APs are offline.', '没有离线的AP。'],
        'Không có switch nào đang mất kết nối.': ['No switches are offline.', '没有离线的交换机。'],
        'Không có camera nào mất kết nối trong kỳ.': ['No cameras went offline in this period.', '期内没有摄像头断线。'],
        'Không có AP nào mất kết nối trong kỳ.': ['No APs went offline in this period.', '期内没有AP断线。'],
        'Không có switch nào mất kết nối trong kỳ.': ['No switches went offline in this period.', '期内没有交换机断线。'],
        'Chưa có dữ liệu để so sánh.': ['No data to compare yet.', '暂无可比较的数据。'],
        'Hiện tại: BPVN theo hệ thống giám sát (không tính camera đã loại trừ), PFVN/MEGA theo lần kiểm tra trạng thái gần nhất.':
            ['Current: BPVN from the monitoring system (excluded cameras not counted), PFVN/MEGA from the latest status check.',
             '当前：BPVN按监控系统（不含已排除的摄像头），PFVN/MEGA按最近一次状态检查。'],
        'Trong kỳ: tính từ lịch sử đổi trạng thái; % hoạt động trong kỳ = 1 − tổng thời gian mất / (số camera × độ dài kỳ).':
            ['In period: computed from status-change history; % online in period = 1 − total downtime / (cameras × period length).',
             '期内：根据状态变更历史计算；期内在线率 = 1 − 总断线时间 / (摄像头数 × 期间长度)。'],
        'Trong kỳ: tính từ lịch sử đổi trạng thái; % hoạt động trong kỳ = 1 − tổng thời gian mất / (số AP theo dõi × độ dài kỳ).':
            ['In period: computed from status-change history; % online in period = 1 − total downtime / (monitored APs × period length).',
             '期内：根据状态变更历史计算；期内在线率 = 1 − 总断线时间 / (监控AP数 × 期间长度)。'],
        'Trong kỳ: tính từ lịch sử đổi trạng thái; % hoạt động trong kỳ = 1 − tổng thời gian mất / (số switch theo dõi × độ dài kỳ).':
            ['In period: computed from status-change history; % online in period = 1 − total downtime / (monitored switches × period length).',
             '期内：根据状态变更历史计算；期内在线率 = 1 − 总断线时间 / (监控交换机数 × 期间长度)。'],
        'Kỳ trước: tuần so với cùng các ngày tuần trước, tháng so với cùng khoảng ngày tháng trước; cả 2 kỳ tính trên danh sách camera hiện tại.':
            ['Previous period: a week is compared with the same days last week, a month with the same date range last month; both periods use the current camera list.',
             '上期：周与上周相同日期比较，月与上月相同日期范围比较；两期均按当前摄像头清单计算。'],
        'Kỳ trước: tuần so với cùng các ngày tuần trước, tháng so với cùng khoảng ngày tháng trước; cả 2 kỳ tính trên danh sách AP đang theo dõi.':
            ['Previous period: a week is compared with the same days last week, a month with the same date range last month; both periods use the currently monitored AP list.',
             '上期：周与上周相同日期比较，月与上月相同日期范围比较；两期均按当前监控的AP清单计算。'],
        'Kỳ trước: tuần so với cùng các ngày tuần trước, tháng so với cùng khoảng ngày tháng trước; cả 2 kỳ tính trên danh sách switch đang theo dõi.':
            ['Previous period: a week is compared with the same days last week, a month with the same date range last month; both periods use the currently monitored switch list.',
             '上期：周与上周相同日期比较，月与上月相同日期范围比较；两期均按当前监控的交换机清单计算。'],
        'Theo dõi = AP có IP và không ở tình trạng Ngừng sử dụng / Trong kho. Hiện tại theo lần ping gần nhất của job nền.':
            ['Monitored = APs that have an IP and are not Retired / In stock. Current status comes from the latest background ping.',
             '监控中 = 有IP且不处于停用/库存状态的AP。当前状态取自后台任务最近一次ping。'],
        'Theo dõi = switch có IP và không ở tình trạng Ngừng sử dụng / Trong kho. Hiện tại theo lần ping gần nhất của job nền.':
            ['Monitored = switches that have an IP and are not Retired / In stock. Current status comes from the latest background ping.',
             '监控中 = 有IP且不处于停用/库存状态的交换机。当前状态取自后台任务最近一次ping。'],

        // --- So sánh kỳ: đơn IT / công việc / thiết bị ---
        'Không có dữ liệu.': ['No data.', '无数据。'],
        'Tổng đơn': ['Total requests', '工单总数'],
        'Đang quá hạn': ['Overdue', '逾期中'],
        'Theo trạng thái': ['By status', '按状态'],
        'Trạng thái': ['Status', '状态'],
        'Theo danh mục (top 10)': ['By category (top 10)', '按类别（前10）'],
        'Danh mục': ['Category', '类别'],
        'Theo bộ phận yêu cầu (top 10)': ['By requesting department (top 10)', '按申请部门（前10）'],
        'Theo người hỗ trợ (người được gán sau cùng)': ['By assignee (last assigned)', '按支持人员（最后指派）'],
        'Người hỗ trợ': ['Assignee', '支持人员'],
        'Số đơn': ['Requests', '工单数'],
        'TB xử lý': ['Avg. handling', '平均处理'],
        'CHỜ QL': ['AWAITING MANAGER', '待主管审批'],
        'CHỜ IT': ['AWAITING IT', '待IT处理'],
        'ĐANG XỬ LÝ': ['IN PROGRESS', '处理中'],
        'HOÀN TẤT': ['COMPLETED', '已完成'],
        'HỦY': ['CANCELLED', '已取消'],
        'TỪ CHỐI': ['REJECTED', '已拒绝'],
        'Tính các đơn có thời điểm quản lý duyệt trong kỳ. Kỳ này chưa hết thì kỳ trước cũng chỉ tính tới cùng mốc. Trạng thái, quá hạn của cả 2 kỳ là trạng thái hiện tại của các đơn đó.':
            ['Counts requests approved by a manager within the period. If this period is not over, the previous period is cut at the same point. Status and overdue for both periods reflect the requests\' current state.',
             '统计期内经主管审批的工单。本期未结束时，上期也只计算到相同时间点。两期的状态和逾期均为工单的当前状态。'],
        'Windows (máy tính, laptop)': ['Windows (desktops, laptops)', 'Windows（台式机、笔记本）'],
        'Office (máy tính, laptop)': ['Office (desktops, laptops)', 'Office（台式机、笔记本）'],
        'Trạng thái thiết bị': ['Device status', '设备状态'],
        'Đạt - MAK công ty': ['OK - company MAK', '合格 - 公司MAK'],
        'Đạt - OEM Pro theo máy': ['OK - OEM Pro with device', '合格 - 随机OEM Pro'],
        'Không cần (macOS / Linux)': ['Not needed (macOS / Linux)', '不需要（macOS / Linux）'],
        'Gốc Home - mua key nâng cấp Pro': ['Home edition - buy Pro upgrade key', 'Home版 - 购买Pro升级密钥'],
        'Cần mua key Windows Pro': ['Need Windows Pro key', '需购买Windows Pro密钥'],
        'Chưa xác minh loại key': ['Key type not verified', '密钥类型未核实'],
        'Cần Office - đã có key': ['Needs Office - has key', '需要Office - 已有密钥'],
        'Cần Office - chưa có key (cần mua)': ['Needs Office - no key (buy)', '需要Office - 无密钥（需购买）'],
        'Không cần Office - đang có key': ['No Office needed - has key', '不需要Office - 有密钥'],
        'Không cần Office - không có key': ['No Office needed - no key', '不需要Office - 无密钥'],
        'Chưa trả lời cần Office - có key': ['Office need unanswered - has key', '未答复是否需要Office - 有密钥'],
        'Chưa trả lời cần Office - không có key': ['Office need unanswered - no key', '未答复是否需要Office - 无密钥'],
        'Cần mua (đã trừ key còn trống)': ['To purchase (spare keys deducted)', '需购买（已扣除空闲密钥）'],
        'Hạng mục': ['Item', '项目'],
        'Office - key Office': ['Office - Office key', 'Office - Office密钥'],
        'Windows - key nâng cấp Home lên Pro': ['Windows - Home to Pro upgrade key', 'Windows - Home升级Pro密钥'],
        'Windows - key Windows Pro': ['Windows - Windows Pro key', 'Windows - Windows Pro密钥'],
        'Tổng Windows cần mua': ['Total Windows to purchase', '需购买的Windows总数'],
        'Key còn trống đã trừ': ['Spare keys deducted', '已扣除的空闲密钥'],
        'không có': ['none', '无'],
        'Giá trị': ['Value', '值'],
        'Windows · Office · Trạng thái': ['Windows · Office · Status', 'Windows · Office · 状态'],
        'số hiện tại': ['current figures', '当前数据'],
        'không có bản chụp trong kỳ': ['no snapshot in period', '期内无快照'],
        'chưa có bản chụp nào — job chụp mỗi ngày, có số kỳ trước sau khi đủ 1 kỳ':
            ['no snapshots yet — taken daily, previous-period figures appear after one full period', '尚无快照 — 每日拍摄，满一个周期后才有上期数据'],
        'Chưa có số kỳ trước để so sánh — hệ thống mới bắt đầu chụp số liệu bản quyền/Office mỗi ngày.':
            ['No previous-period figures yet — the system has just started taking daily license/Office snapshots.', '暂无上期数据可比较 — 系统刚开始每日拍摄许可证/Office数据快照。'],
        'Phân loại như báo cáo bản quyền 280926: chỉ MAK công ty / OEM theo máy là đạt. Kết luận từng máy BPVN lấy từ file 280926; đồng bộ agent không ghi đè nhãn này. "Chưa xác minh loại key" = máy chưa có kết luận/nhãn MAK-OEM — nhập Key Windows từ Excel để kết luận.':
            ['Classified as in license report 280926: only company MAK / device OEM count as OK. Per-device conclusions for BPVN come from file 280926; agent sync does not overwrite this label. "Key type not verified" = device has no MAK/OEM conclusion yet — import Windows keys from Excel to conclude.',
             '分类同许可证报告280926：仅公司MAK / 随机OEM视为合格。BPVN各设备结论取自文件280926；代理同步不会覆盖此标签。"密钥类型未核实" = 设备尚无MAK/OEM结论 — 从Excel导入Windows密钥以确定。'],
        'Biến động trong kỳ': ['Changes in period', '期内变动'],
        'Thêm mới': ['Added', '新增'],
        'Xoá': ['Deleted', '删除'],
        'Kiểm kê': ['Inventory checks', '盘点'],
        'Không có thiết bị nào thêm, xoá hay kiểm kê trong 2 kỳ.': ['No devices were added, deleted or checked in either period.', '两期内没有新增、删除或盘点的设备。'],
        'Chỉ so được các số có mốc thời gian (ngày tạo, ngày xoá, lần kiểm kê). Kiểm kê tính theo lần kiểm gần nhất của mỗi máy — máy kiểm lại ở kỳ này không còn tính cho kỳ trước. Bản quyền/Office/trạng thái so theo bản chụp số liệu mỗi ngày.':
            ['Only time-stamped figures can be compared (created date, deleted date, inventory check). Checks count each device\'s latest check — a device re-checked this period no longer counts for the previous period. License/Office/status are compared using daily snapshots.',
             '仅可比较带时间戳的数据（创建日期、删除日期、盘点）。盘点按每台设备最近一次计算 — 本期重新盘点的设备不再计入上期。许可证/Office/状态按每日数据快照比较。'],

        // --- Thiết bị (kiemke-tong-quan.js) ---
        'Phạm vi:': ['Scope:', '范围：'],
        'Không tính thiết bị trong thùng rác và thiết bị bị chặn.': ['Devices in the recycle bin and blocked devices are excluded.', '不含回收站中的设备和被屏蔽的设备。'],
        'Loại thiết bị theo Trạng thái': ['Device type by status', '设备类型（按状态）'],
        'Tỷ lệ Trạng thái': ['Status ratio', '状态比例'],
        'Theo Công ty': ['By company', '按公司'],
        'Lần kiểm tra gần nhất': ['Latest check', '最近检查'],
        'Tình trạng Bảo hành': ['Warranty status', '保修状态'],
        'Top 15 Bộ phận nhiều thiết bị nhất': ['Top 15 departments by device count', '设备最多的前15个部门'],
        'Theo Vị trí địa lý': ['By location', '按地理位置'],
        'Bản quyền Windows': ['Windows license', 'Windows许可证'],
        'Bản quyền Office': ['Office license', 'Office许可证'],
        'Phiên bản Agent': ['Agent version', '代理版本'],
        'Thiết bị thêm mới & lượt kiểm gần nhất — 12 tháng': ['New devices & latest checks — 12 months', '新增设备与最近检查 — 12个月'],
        'Chi tiết theo Bộ phận': ['Details by department', '按部门明细'],
        'Lọc bộ phận / công ty...': ['Filter department / company...', '筛选部门/公司...'],
        'Thiết bị cần chú ý': ['Devices needing attention', '需关注的设备'],
        'Tổng thiết bị': ['Total devices', '设备总数'],
        'Đang hoạt động': ['In use', '使用中'],
        'Hỏng / Cần sửa': ['Broken / Needs repair', '损坏/需维修'],
        'Đang bảo trì': ['Under maintenance', '维护中'],
        'Đã cấp phát người dùng': ['Assigned to users', '已分配给用户'],
        'Chưa cấp phát': ['Unassigned', '未分配'],
        'Hết hạn bảo hành': ['Warranty expired', '保修已过期'],
        'Chưa khai hạn BH': ['No warranty date', '未填写保修期'],
        'Có agent kết nối': ['Agent connected', '代理已连接'],
        'agent báo lỗi': ['agent errors', '代理报错'],
        'Agent báo lỗi': ['Agent errors', '代理报错'],
        'Thiếu Hostname': ['Missing hostname', '缺少主机名'],
        'Thiếu Serial': ['Missing serial', '缺少序列号'],
        'Cần cài Office': ['Office needed', '需要安装Office'],
        'Chưa trả lời': ['Unanswered', '未答复'],
        'Đã có địa chỉ IP': ['Has IP address', '已有IP地址'],
        'chưa kiểm lần nào': ['never checked', '从未检查'],
        'Chưa kiểm lần nào': ['Never checked', '从未检查'],
        'Có hạn bảo hành còn hiệu lực': ['Warranty still valid', '保修仍有效'],
        'Đủ Hostname + Serial': ['Hostname + serial complete', '主机名和序列号齐全'],
        'Thiếu Hostname / Serial': ['Missing hostname / serial', '缺少主机名/序列号'],
        'Chưa cấp phát người dùng': ['Not assigned to a user', '未分配给用户'],
        'Hỏng': ['Broken', '损坏'],
        'Bảo trì': ['Maintenance', '维护'],
        'Kho IT': ['IT warehouse', 'IT仓库'],
        'Khác': ['Other', '其他'],
        'khác': ['other', '其他'],
        'Đang ở Kho IT': ['In IT warehouse', '在IT仓库'],
        'Còn > 1 năm': ['> 1 year left', '剩余 > 1年'],
        'Đã hết hạn': ['Expired', '已过期'],
        'Chưa khai báo': ['Not declared', '未填写'],
        'Chưa xác định': ['Undetermined', '未确定'],
        'Chưa rõ': ['Unknown', '未知'],
        'Trống / Chưa rõ': ['Empty / Unknown', '空/未知'],
        'Chưa có agent': ['No agent', '无代理'],
        'Số thiết bị': ['Devices', '设备数'],
        'Lần kiểm gần nhất': ['Latest check', '最近检查'],
        'Loại': ['Type', '类型'],
        'Loại khác': ['Other types', '其他类型'],
        'Chưa kiểm': ['Not checked', '未检查'],
        'Tổng cộng': ['Grand total', '合计'],
        'Không có bộ phận nào khớp.': ['No matching departments.', '没有匹配的部门。'],
        'Hostname / Serial': ['Hostname / Serial', '主机名/序列号'],
        'Công ty / Bộ phận': ['Company / Department', '公司/部门'],
        'Người dùng': ['User', '用户'],
        'Kiểm gần nhất': ['Last checked', '最近检查'],
        'Hạn BH': ['Warranty', '保修期'],
        'Mở thiết bị': ['Open device', '打开设备'],
        '(thiếu hostname)': ['(missing hostname)', '（缺少主机名）'],
        '(thiếu serial)': ['(missing serial)', '（缺少序列号）'],
        'Không có thiết bị nào trong nhóm này.': ['No devices in this group.', '此组中没有设备。'],
        // Giá trị lấy từ CSDL hay gặp (loại / trạng thái thiết bị)
        'Máy tính': ['Desktop', '台式机'],
        'máy tính': ['desktop', '台式机'],
        'Màn hình': ['Monitor', '显示器'],
        'Máy in': ['Printer', '打印机'],
        'Điện thoại': ['Phone', '电话'],
        'Máy chiếu': ['Projector', '投影仪'],
        'Ngừng sử dụng': ['Retired', '停用'],
        'Trong kho': ['In stock', '库存中'],
        'Thanh lý': ['Disposed', '已处置']
    };

    function L(en, zh) { return i === 0 ? en : zh; }
    function t(s) { var k = dichLoi(s); return k === null ? s : k; }
    function thietBi(s) { return s === 'camera' ? L('Camera', '摄像头') : s === 'Switch' ? L('Switch', '交换机') : s; }
    function donVi(s) {
        return s.replace(/ ngày/g, L(' d', '天')).replace(/ giờ/g, L(' h', '小时')).replace(/ phút/g, L(' min', '分钟'));
    }

    // Chuỗi có số / tên ghép vào → mẫu. m = kết quả regex, t() dịch tiếp phần ghép.
    var MAU = [
        [/^[\d.,]+ (ngày|giờ|phút)( [\d.,]+ (giờ|phút))?$/, function (m) { return donVi(m[0]); }],
        [/^([\d.,]+) điểm$/, function (m) { return m[1] + L(' pts', '个百分点'); }],
        [/^([▲▼] )(.+)$/, function (m) { return m[1] + t(m[2]); }],
        [/^([\d.,\/]+) công ty$/, function (m) { return L(m[1] + ' companies', m[1] + '家公司'); }],
        [/^Tổng ([\d.,]+) công ty$/, function (m) { return L('All ' + m[1] + ' companies', '全部' + m[1] + '家公司'); }],
        [/^([\d.,]+) bộ phận$/, function (m) { return L(m[1] + ' departments', m[1] + '个部门'); }],
        [/^([\d.,]+) loại$/, function (m) { return L(m[1] + ' types', m[1] + '种类型'); }],
        [/^([\d.,]+%) tổng số$/, function (m) { return L(m[1] + ' of total', '占总数' + m[1]); }],
        [/^kiểm tra (.+)$/, function (m) { return L('checked ' + m[1], '检查于 ' + m[1]); }],
        [/^Kỳ (.+) \(so với (.+)\)$/, function (m) { return L('Period ' + m[1] + ' (vs. ' + m[2] + ')', '期间 ' + m[1] + '（对比 ' + m[2] + '）'); }],
        [/^\((.+) so với (.+)\)$/, function (m) { return L('(' + m[1] + ' vs. ' + m[2] + ')', '（' + m[1] + ' 对比 ' + m[2] + '）'); }],
        [/^lập lúc (.+)$/, function (m) { return L('generated ' + m[1], '生成于 ' + m[1]); }],
        [/^Báo cáo (camera|AP|Switch) (.+)$/, function (m) { return L(thietBi(m[1]) + ' report – ' + m[2], m[2] + ' ' + thietBi(m[1]) + '报告'); }],
        [/^Tình trạng (camera|AP Wi-Fi|Switch) (.*)$/, function (m) { return L(thietBi(m[1]) + ' status – ' + m[2], thietBi(m[1]) + '状态 – ' + m[2]); }],
        [/^Lỗi kết nối máy chủ \((.*)\)\.$/, function (m) { return L('Server connection error (' + m[1] + ').', '服务器连接错误（' + m[1] + '）。'); }],
        [/^Lịch sử đổi trạng thái chỉ có từ (.+) — số liệu "trong kỳ" trước mốc này chưa đầy đủ\.$/, function (m) {
            return L('Status history is only available from ' + m[1] + ' — "in period" figures before that are incomplete.', '状态历史仅从 ' + m[1] + ' 开始 — 此前的"期内"数据不完整。'); }],
        [/^Lịch sử đổi trạng thái chỉ có từ (.+) — kỳ trước \((.+)\) chưa đủ dữ liệu, phần so sánh chỉ để tham khảo\.$/, function (m) {
            return L('Status history is only available from ' + m[1] + ' — previous period (' + m[2] + ') is incomplete, comparison is for reference only.', '状态历史仅从 ' + m[1] + ' 开始 — 上期（' + m[2] + '）数据不足，比较仅供参考。'); }],
        [/^Mở trang (camera|AP|switch) (.+)$/, function (m) { return L('Open ' + m[1] + ' page ' + m[2], '打开' + m[2] + ' ' + thietBi(m[1] === 'switch' ? 'Switch' : m[1]) + '页面'); }],
        [/^bản chụp (.+)$/, function (m) { return L('snapshot ' + m[1], '快照 ' + m[1]); }],
        [/^chưa có bản chụp trong kỳ trước \(bắt đầu chụp từ (.+)\)$/, function (m) {
            return L('no snapshot in previous period (snapshots started ' + m[1] + ')', '上期无快照（从 ' + m[1] + ' 开始拍摄）'); }],
        [/^(Đã kiểm|Kiểm) trong (\d+) ngày$/, function (m) { return L('Checked within ' + m[2] + ' days', m[2] + '天内已检查'); }],
        [/^Kiểm ≤ (\d+) ngày$/, function (m) { return L('Checked ≤ ' + m[1] + ' days', '≤' + m[1] + '天内检查'); }],
        [/^([≤>] ?\d+|\d+ – \d+) ngày$/, function (m) { return L(m[1] + ' days', m[1] + '天'); }],
        [/^Còn (\d+) ngày – 1 năm$/, function (m) { return L(m[1] + ' days – 1 year left', '剩余' + m[1] + '天–1年'); }],
        [/^Sắp hết (BH )?\(≤ ?(\d+) ngày\)$/, function (m) { return L((m[1] ? 'Warranty ending' : 'Ending soon') + ' (≤' + m[2] + ' days)', (m[1] ? '保修' : '') + '即将到期（≤' + m[2] + '天）'); }],
        [/^Chưa kiểm > (\d+) ngày$/, function (m) { return L('Not checked > ' + m[1] + ' days', '超过' + m[1] + '天未检查'); }],
        [/^(\d+) ngày trước$/, function (m) { return L(m[1] + ' days ago', m[1] + '天前'); }],
        [/^quá (\d+) ngày$/, function (m) { return L(m[1] + ' days overdue', '已过期' + m[1] + '天'); }],
        [/^còn (\d+) ngày$/, function (m) { return L(m[1] + ' days left', '剩余' + m[1] + '天'); }],
        [/^Đang hiện ([\d.,]+)\/([\d.,]+) thiết bị — dùng bộ lọc ở tab Thiết bị để xem đủ\.$/, function (m) {
            return L('Showing ' + m[1] + '/' + m[2] + ' devices — use the filters on the Devices tab to see all.', '显示 ' + m[1] + '/' + m[2] + ' 台设备 — 请在"设备"标签页使用筛选查看全部。'); }],
        [/^Tất cả công ty \(([\d.,]+)\)$/, function (m) { return L('All companies (' + m[1] + ')', '全部公司（' + m[1] + '）'); }],
        [/^Số liệu lúc (.+)$/, function (m) { return L('Data as of ' + m[1], '数据时间 ' + m[1]); }]
    ];

    var bo = {};   // cache: cùng chuỗi lặp lại nhiều lần trong bảng
    function dichLoi(s) {
        if (Object.prototype.hasOwnProperty.call(bo, s)) return bo[s];
        var kq = dichMoi(s);
        bo[s] = kq;
        return kq;
    }
    function dichMoi(s) {
        if (TU.hasOwnProperty(s)) return TU[s][i];
        // Ghi chú nhiều dòng trong .cshtml là một nút chữ → dịch từng dòng
        if (s.indexOf('\n') >= 0) {
            var dong = s.split('\n'), coDoi = false;
            dong = dong.map(function (x) { var y = dichChuoi(x); if (y !== x) coDoi = true; return y; });
            return coDoi ? dong.join('\n') : null;
        }
        for (var k = 0; k < MAU.length; k++) {
            var m = MAU[k][0].exec(s);
            if (m) return MAU[k][1](m);
        }
        if (s.indexOf(' · ') > 0) {
            var doi = false;
            var ds = s.split(' · ').map(function (p) { var x = dichLoi(p); if (x !== null) { doi = true; return x; } return p; });
            return doi ? ds.join(' · ') : null;
        }
        // "Nhãn: giá trị" — chỉ dịch khi nhãn có trong từ điển (giá trị có thể là tên riêng)
        var n = /^([^:]+): (.+)$/.exec(s);
        if (n) {
            var nhan = dichLoi(n[1]);
            if (nhan !== null) return nhan + L(': ', '：') + t(n[2]);
        }
        // Dấu ngăn ở đầu/cuối nút chữ (" · ", "Trong kỳ: "...) → dịch phần lõi
        var v = /^([·(]\s*)?(.*?)(\s*[·:.)])?$/.exec(s);
        if (v && (v[1] || v[3]) && v[2]) {
            var loi = dichLoi(v[2]);
            if (loi !== null) return (v[1] || '') + loi + (v[3] || '');
        }
        return null;
    }

    function dichChuoi(s) {
        if (i < 0 || !s) return s;
        var loi = s.trim();
        if (!loi || !/[^\s\d.,:%\/–\-()=▲▼·]/.test(loi)) return s;
        var kq = dichLoi(loi);
        if (kq === null) return s;
        return s.slice(0, s.indexOf(loi)) + kq + s.slice(s.indexOf(loi) + loi.length);
    }
    window.dichGd = dichChuoi;
    if (i < 0) return;

    var THUOC_TINH = ['title', 'placeholder', 'aria-label'];
    function dichPhanTu(el) {
        THUOC_TINH.forEach(function (a) {
            var cu = el.getAttribute(a);
            if (cu) { var moi = dichChuoi(cu); if (moi !== cu) el.setAttribute(a, moi); }
        });
    }
    function dichCay(nut) {
        if (nut.nodeType === 3) {
            var moi = dichChuoi(nut.nodeValue);
            if (moi !== nut.nodeValue) nut.nodeValue = moi;
            return;
        }
        if (nut.nodeType !== 1 || nut.tagName === 'SCRIPT' || nut.tagName === 'STYLE') return;
        dichPhanTu(nut);
        var w = document.createTreeWalker(nut, NodeFilter.SHOW_ELEMENT | NodeFilter.SHOW_TEXT);
        var x;
        while ((x = w.nextNode())) {
            if (x.nodeType === 3) {
                var p = x.parentNode;
                if (p && (p.tagName === 'SCRIPT' || p.tagName === 'STYLE')) continue;
                var m2 = dichChuoi(x.nodeValue);
                if (m2 !== x.nodeValue) x.nodeValue = m2;
            } else {
                dichPhanTu(x);
            }
        }
    }

    document.querySelectorAll('[data-dich-offline]').forEach(function (khoi) {
        dichCay(khoi);
        new MutationObserver(function (ds) {
            ds.forEach(function (mu) {
                if (mu.type === 'attributes') dichPhanTu(mu.target);
                else if (mu.type === 'characterData') dichCay(mu.target);
                else mu.addedNodes.forEach(dichCay);
            });
        }).observe(khoi, { childList: true, subtree: true, characterData: true, attributes: true, attributeFilter: THUOC_TINH });
    });
})();
