using System;

namespace ShortSellingApp.Models
{
    /// <summary>
    /// CpSysDib.CpSvr7254 — 공매도 현황 (투자주체별) 1행 데이터
    /// </summary>
    public class InvestorTradeData
    {
        public string StockCode  { get; set; }
        public string StockName  { get; set; }

        // YYYYMMDD 문자열 / DateTime
        public string   Date      { get; set; }
        public DateTime DateValue { get; set; }

        // GetDataValue 필드 0~13
        public long Individual      { get; set; }   // 1: 개인
        public long Foreigner       { get; set; }   // 2: 외국인
        public long Institution     { get; set; }   // 3: 기관계
        public long FinancialInvest { get; set; }   // 4: 금융투자
        public long Insurance       { get; set; }   // 5: 보험
        public long InvestTrust     { get; set; }   // 6: 투신
        public long Bank            { get; set; }   // 7: 은행
        public long OtherFinancial  { get; set; }   // 8: 기타금융
        public long PensionFund     { get; set; }   // 9: 연기금
        public long OtherCorp       { get; set; }   // 10: 기타법인
        public long ForeignerEtc    { get; set; }   // 11: 외국인(기타)
        public long PrivateEquity   { get; set; }   // 12: 사모펀드
        public long Government      { get; set; }   // 13: 정부/지자체

        // 데이터 단위 메모 ("순매수수량(주)" or "추정금액(백만원)")
        public string Unit { get; set; }
    }
}
