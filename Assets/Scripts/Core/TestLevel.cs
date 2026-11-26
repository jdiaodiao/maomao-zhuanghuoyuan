namespace CatMart
{
    /// <summary>
    /// 测试关：每次改进之后，把这次改动涉及的东西放进来，方便直接试。
    /// 开场界面点「测试关」进入；不存档、不影响正式关卡进度和钱包。
    /// （每次改进都要更新这个文件：Version / Checklist / Orders。Checklist 显示时自动编号。）
    /// 已确认没问题（不再放进来）：Esc 暂停、左上角今日收入、光边和画风、当日结算。
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

        public const string Version = "2026-11-25 第 3 版 · 字体 / 计分规则 / 方底袋 / 放回袋子 / 压坏和扎坏 / 半价赔偿";

        /// <summary>测试关目标金额 = 正常算法 × 这个比例（测试关简单一点）。</summary>
        public const float GoalScale = 0.5f;

        /// <summary>这次要测的项目（显示时自动编号 1、2、3…）。</summary>
        public static readonly string[] Checklist =
        {
            "字变大了：开场说明分成一条一条的，帮助栏分成 拿放 / 旋转 / 袋子 / 其他 四行；各个面板字有没有挤出去",
            "开场界面新的「计分规则」按钮：完整的算分规则（星星怎么算也在里面）；结算界面标题下面也写了星星规则",
            "袋架：塑料袋 中号 / 大号 / 方底，纸袋 中号 / 大号（没有小号了）；看着袋架有尺寸说明",
            "蛋糕、甜甜圈盒能立着放进方底袋；顾客的话里会说“要立着放”，需要时会点名要方底袋",
            "拿错袋子：点一下空袋子就放回袋架（里面有东西放不回去）",
            "压坏和扎坏分开算：重东西压在薯片上是“压坏”；刀、叉子、竹签顶着面包/薯片是“被扎破”；罐头、玻璃扎不坏；罐头不再算尖锐",
            "悬停商品时：“受压 N%”和“被尖东西顶着 N%”分开显示；坏了会写是压坏/扎坏/摔坏",
            "弄坏东西只赔半价（飘字、提示、结算都是半价）",
            "顾客要求面板：耐心条上的两条白线是小费分界，面板里显示“现在完成可得小费”；结算里有小费一行",
            "记忆单（第 4 位）：没有光边、悬停没有属性说明；第 3 单结算按钮会提前提示",
        };

        public static readonly OrderSpec[] Orders =
        {
            new OrderSpec
            {
                Note = "方底袋 + 立着放（第 3、4 项）",
                Items = new[] { "cake", "donuts", "strawberries", "bread", "apples" },
                Material = BagMaterial.Plastic,
                Patience = 300,
            },
            new OrderSpec
            {
                Note = "压坏 vs 扎坏：试试把刀放在面包上、罐头放在薯片上（第 6、7、8 项）",
                Items = new[] { "knife", "forks", "bread", "chips", "canned_soup", "canned_tuna" },
                Material = BagMaterial.Paper,
                Patience = 300,
            },
            new OrderSpec
            {
                Note = "先故意拿错一个袋子，点一下放回（第 5 项）；装快一点看小费（第 9 项）",
                Items = new[] { "crackers", "apples", "milk_half" },
                Material = BagMaterial.Plastic,
                Patience = 120,
            },
            new OrderSpec
            {
                Note = "记忆单：没有光边和属性说明，商品都是前面出现过的（第 10 项）",
                Items = new[] { "cake", "bread", "chips", "apples", "knife" },
                Material = BagMaterial.Plastic,
                Patience = 300,
                Memory = true,
            },
        };
    }
}
