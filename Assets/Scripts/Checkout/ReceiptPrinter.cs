using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CatMart
{
    /// <summary>小票打印机：3D 小票纸随扫描逐行变长，同时供 HUD 显示。</summary>
    public class ReceiptPrinter : MonoBehaviour
    {
        public Transform Paper;      // 小票纸（缩放 y 表示长度）
        public TextMesh Text;
        public const float TaxRate = 0.0725f;

        public readonly List<string> Lines = new List<string>();
        public float Subtotal { get; private set; }
        public int ItemCount { get; private set; }
        float _targetLen = 0.04f, _len = 0.04f;
        int _lane = 3;
        int _ticket;

        public void Begin(int ticket, string customerName)
        {
            _ticket = ticket;
            Lines.Clear();
            Subtotal = 0;
            ItemCount = 0;
            Lines.Add("   CatMart 喵喵超市   ");
            Lines.Add($"  第{_lane}号收银台 · 单号 {ticket:0000}");
            Lines.Add($"  顾客: {customerName}");
            Lines.Add("------------------------------");
            _targetLen = 0.06f;
            Refresh();
        }

        public void AddLine(string name, float price)
        {
            ItemCount++;
            Subtotal += price;
            string n = name.Length > 11 ? name.Substring(0, 11) : name;
            Lines.Add($"{n.PadRight(12, '　')}{price,7:0.00}");
            _targetLen += 0.012f;
            Refresh();
        }

        public void PrintTotal()
        {
            float tax = Subtotal * TaxRate;
            Lines.Add("------------------------------");
            Lines.Add($"  共 {ItemCount} 件     小计 {Subtotal,8:0.00}");
            Lines.Add($"  税 7.25%          {tax,8:0.00}");
            Lines.Add($"  合计              {Subtotal + tax,8:0.00}");
            Lines.Add("    谢谢惠顾 喵~ ฅ^•ﻌ•^ฅ");
            _targetLen += 0.06f;
            Refresh();
        }

        void Refresh()
        {
            if (Text == null) return;
            var sb = new StringBuilder();
            // 3D 小票只显示最后若干行
            int start = Mathf.Max(0, Lines.Count - 22);
            for (int i = start; i < Lines.Count; i++) sb.AppendLine(Lines[i]);
            Text.text = sb.ToString();
        }

        void Update()
        {
            _len = Mathf.MoveTowards(_len, Mathf.Min(_targetLen, 0.32f), Time.deltaTime * 0.08f);
            if (Paper != null)
            {
                Paper.localScale = new Vector3(Paper.localScale.x, _len, Paper.localScale.z);
                Paper.localPosition = new Vector3(Paper.localPosition.x, _len / 2f, Paper.localPosition.z);
            }
            if (Text != null) Text.transform.localPosition = new Vector3(Text.transform.localPosition.x, _len - 0.004f, Text.transform.localPosition.z);
        }

        public string FullText()
        {
            var sb = new StringBuilder();
            foreach (var l in Lines) sb.AppendLine(l);
            return sb.ToString();
        }
    }
}
