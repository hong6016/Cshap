using System;

namespace ShortSellingApp.Models
{
    public class ShortSellingData
    {
        public string StockCode { get; set; }
        public string StockName { get; set; }

        // YYYYMMDD 문자열
        public string Date { get; set; }
        public DateTime DateValue { get; set; }

        // 공매도 거래량 (주)
        public long ShortVolume { get; set; }

        // 공매도 거래대금 (원)
        public long ShortAmount { get; set; }

        // 공매도 비중 (%)
        public double ShortRatio { get; set; }

        // 종가 (원)
        public int ClosePrice { get; set; }

        // 총 거래량 (주)
        public long TotalVolume { get; set; }
    }
}
