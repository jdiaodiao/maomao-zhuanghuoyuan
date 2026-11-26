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

        public const string Version = "2026-11-25 第 4 版 · 圆鼓塑料袋 / 方底纸袋 / 新计分规则（+ 上一版没测的项目）";

        /// <summary>测试关目标金额 = 正常算法 × 这个比例（测试关简单一点）。</summary>
        public const float GoalScale = 0.5f;

        /// <summary>这次要测的项目（显示时自动编号 1、2、3…）。</summary>
        public static readonly string[] Checklist =
        {
            "【新】塑料袋：比纸袋矮，袋身圆鼓鼓的（截面是圆角、侧面往外鼓），装洋葱土豆这种不规则的东西更好装",
            "【新】方底袋改成纸袋了：袋架上纸袋 中号 / 大号 / 方底，塑料袋 中号 / 大号",
            "【新】计分规则按你的版本改了：开场「计分规则」页面、结算界面都看看对不对（一件不漏 ×1.2；多个袋子平衡取平均）",
            "【新】大件装进袋子、小件直接放推车都算漏装，结算里会写原因",
            "字变大了：开场说明分条、帮助栏分四行；各面板字有没有挤出去",
            "蛋糕、甜甜圈盒能立着放进方底纸袋；顾客的话里会说“要立着放”",
            "拿错袋子：点一下空袋子就放回袋架（里面有东西放不回去）",
            "压坏和扎坏分开算：重东西压在薯片上是“压坏”；刀上再压东西、刀顶着面包是“被扎破”；罐头、玻璃扎不坏",
            "悬停商品时：“受压 N%”和“被尖东西顶着 N%”分开显示；坏了会写是压坏/扎坏/摔坏",
            "弄坏东西只赔半价（飘字、提示、结算都是半价）",
            "顾客要求面板：耐心条上两条白线是小费分界，面板里显示“现在完成可得小费”；结算里有小费一行",
            "记忆单（第 4 位）：没有光边、悬停没有属性说明；第 3 单结算按钮会提前提示",
        };

        public static readonly OrderSpec[] Orders =
        {
            new OrderSpec
            {
                Note = "圆鼓塑料袋装不规则的东西（第 1、3 项）；故意拿错一个袋子再点一下放回（第 7 项）",
                Items = new[] { "onions", "potatoes", "apples", "bananas", "crackers" },
                Material = BagMaterial.Plastic,
                Patience = 300,
            },
            new OrderSpec
            {
                Note = "方底纸袋 + 立着放（第 2、6 项）",
                Items = new[] { "cake", "donuts", "strawberries", "bread", "cereal" },
                Material = BagMaterial.Paper,
                Patience = 300,
            },
            new OrderSpec
            {
                Note = "压坏 vs 扎坏：刀上压个罐头、罐头放薯片上；猫砂故意放进袋子看漏装（第 4、8、9、10 项）",
                Items = new[] { "knife", "bread", "chips", "canned_soup", "canned_tuna", "cat_litter" },
                Material = BagMaterial.Paper,
                Patience = 300,
            },
            new OrderSpec
            {
                Note = "记忆单：没有光边和属性说明，商品都是前面出现过的（第 11、12 项）",
                Items = new[] { "cake", "onions", "chips", "apples", "knife" },
                Material = BagMaterial.Plastic,
                Patience = 300,
                Memory = true,
            },
        };
    }
}
