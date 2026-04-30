using System;

namespace ShortSellingApp.Models
{
    /// <summary>
    /// CpSysDib.CpSvr7238 [종목별 공매도 추이] 1행 데이터
    ///
    /// GetDataValue 필드
    ///   0 거래일자       (ulong)
    ///   1 종가           (ulong)
    ///   2 전일대비       (long)
    ///   3 전일대비율     (long)
    ///   4 거래량         (long)
    ///   5 공매도량       (ulong)
    ///   6 공매도비중     (double, %)
    ///   7 공매도거래대금 (ulong)
    ///   8 평균가         (ulong)
    ///   9 평균가대비     (long)
    /// </summary>
    public class ShortSellData
    {
        public string   StockCode    { get; set; }
        public string   StockName    { get; set; }
        public string   Exchange     { get; set; }  // A/K/N

        public string   Date         { get; set; }  // YYYYMMDD
        public DateTime DateValue    { get; set; }

        public long     ClosePrice   { get; set; }  // 1: 종가
        public long     PriceChange  { get; set; }  // 2: 전일대비
        public double   ChangeRate   { get; set; }  // 3: 전일대비율(%)
        public long     Volume       { get; set; }  // 4: 거래량
        public long     ShortVolume  { get; set; }  // 5: 공매도량
        public double   ShortRatio   { get; set; }  // 6: 공매도비중(%)
        public long     ShortAmount  { get; set; }  // 7: 공매도거래대금
        public long     AvgPrice     { get; set; }  // 8: 평균가
        public long     AvgPriceDiff { get; set; }  // 9: 평균가대비
    }
}
