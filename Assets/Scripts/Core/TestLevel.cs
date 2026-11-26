namespace CatMart
{
    /// <summary>
    /// 测试关：每次改进之后，把这次改动涉及的东西放进来，方便直接试。
    /// 开场界面点「测试关」进入；不存档、不影响正式关卡进度和钱包。
    /// （每次改进都要更新这个文件：Version / Checklist / Orders。）
    /// </summary>
    public static class TestLevel
    {
        public class OrderSpec
        {
            public string Note;              // 这一单要测什么（显示在顾客要求面板上）
            public string[] Items;           // 商品 id（见 ItemCatalog）
            public BagMaterial Material = BagMaterial.Plastic;
            public float Patience = -1;      // 秒；-1 = 按件数自动算
            public bool Memory;              // 记忆单（无光边、无属性说明）
        }

        public const string Version = "2026-11-25 · 关卡模式 / 小费 / 记忆单 / 画风统一";

        /// <summary>这次改了什么、要看什么。</summary>
        public static readonly string[] Checklist =
        {
            "左上角：今日收入 / 目标金额 + 进度条，达标后变绿",
            "顾客要求面板：耐心条上两条白线是小费分界；面板里显示“现在完成可得小费”",
            "结算：小费一行（♥♥♥ 5% / ♥♥ 2.5% / ♥ 1%），弄坏或漏装就没有小费",
            "第 3 单结算按钮提示“下一位是记忆单”；第 4 单没有光边、悬停没有属性说明",
            "画风：肉托盘变浅色、网兜变粗网眼、果皮/肉是色块；包装背面没有营养表和条码",
            "当日结算：达标显示“过关啦”，没达标可以重来（测试关都不存档）",
        };

        public static readonly OrderSpec[] Orders =
        {
            new OrderSpec
            {
                Note = "画风：肉托盘、网兜、果皮。装快一点试试 ♥♥♥ 小费",
                Items = new[] { "chicken", "steak", "fish_fillet", "potatoes", "onions", "bananas" },
                Material = BagMaterial.Plastic,
            },
            new OrderSpec
            {
                Note = "包装背面（转过来看）+ 各种颜色的光边，蛋糕/插花不能倒",
                Items = new[] { "cereal", "cat_kibble", "chips", "eggs", "knife", "cake", "flower_vase" },
                Material = BagMaterial.Paper,
            },
            new OrderSpec
            {
                Note = "小费档次：耐心只有 35 秒，看着小费一档档往下掉",
                Items = new[] { "canned_soup", "crackers", "apples" },
                Material = BagMaterial.Plastic,
                Patience = 35,
            },
            new OrderSpec
            {
                Note = "记忆单：没有光边和属性说明，商品都是前面出现过的",
                Items = new[] { "eggs", "cake", "chips", "steak", "knife", "flower_vase", "bananas" },
                Material = BagMaterial.Paper,
                Memory = true,
            },
        };
    }
}
