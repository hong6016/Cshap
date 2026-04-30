using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using ShortSellingApp.Models;

namespace ShortSellingApp.Services
{
    /// <summary>
    /// 대신증권 CYBOS Plus COM API 래퍼
    /// CYBOS HTS가 실행·로그인된 상태에서만 동작합니다.
    /// </summary>
    public class DaishinApiService
    {
        // API 연속 호출 시 최소 대기 (초당 최대 5회 제한)
        private const int RequestDelayMs = 250;

        // ── 연결 상태 ────────────────────────────────────────────────
        public bool IsConnected()
        {
            try
            {
                Type t = Type.GetTypeFromProgID("CpUtil.CpCybos");
                if (t == null) return false;
                object obj = Activator.CreateInstance(t);
                int state  = (int)GetProp(obj, "IsConnect");
                Release(obj);
                return state == 1;
            }
            catch { return false; }
        }

        // ── 종목코드 → 종목명 ────────────────────────────────────────
        public string GetStockName(string stockCode)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("CpUtil.CpStockCode");
                if (t == null) return stockCode;
                object obj  = Activator.CreateInstance(t);
                string name = CallMethod(obj, "CodeToName", stockCode)?.ToString() ?? stockCode;
                Release(obj);
                return name;
            }
            catch { return stockCode; }
        }

        // ── 공매도 현황 수집 — 투자주체별 (CpSysDib.CpSvr7254) ─────────
        //
        // SetInputValue
        //   0  string  종목코드          예) "A005930"
        //   1  short   기간구분          0=직접입력, 1=1개월, 2=2개월,
        //                                3=3개월, 4=6개월, 5=최근5일, 6=일별
        //   2  long    시작일자          YYYYMMDD (기간구분=0 일 때)
        //   3  long    종료일자          YYYYMMDD (기간구분=0 일 때)
        //   4  char    매매구분          '0'=순매수, '1'=매매비중
        //   5  short   투자자구분        0=전체, 1=개인, 2=외국인, 3=기관계,
        //                                4=금융투자, 5=보험, 6=투신, 7=은행,
        //                                8=기타금융, 9=연기금, 10=기타법인,
        //                                11=외국인기타, 12=사모펀드, 13=정부/지자체
        //   6  char    데이터구분        '1'=순매수수량(주), '2'=추정금액(백만원)
        //
        // GetHeaderValue(3) → 행 수
        //
        // GetDataValue(field, row)
        //   0  날짜       1  개인      2  외국인   3  기관계
        //   4  금융투자   5  보험      6  투신     7  은행
        //   8  기타금융   9  연기금   10  기타법인 11  외국인기타
        //  12  사모펀드  13  정부/지자체
        // ─────────────────────────────────────────────────────────────
        public List<InvestorTradeData> GetInvestorTradeData(
            string stockCode,
            string fromDate,
            string toDate,
            char   tradeType   = '0',   // '0'=순매수, '1'=매매비중
            short  investorType = 0,    // 0=전체
            char   dataType    = '1',   // '1'=수량, '2'=금액
            IProgress<string> progress = null)
        {
            var result = new List<InvestorTradeData>();

            Type t = Type.GetTypeFromProgID("CpSysDib.CpSvr7254");
            if (t == null)
                throw new InvalidOperationException(
                    "CpSysDib.CpSvr7254 COM 오브젝트를 찾을 수 없습니다.\n" +
                    "CYBOS Plus HTS가 실행 중인지 확인하십시오.");

            object obj = Activator.CreateInstance(t);
            try
            {
                progress?.Report($"[{stockCode}] 투자주체별 매매현황 요청 ({fromDate}~{toDate}) ...");

                SetInput(obj, 0, stockCode);
                SetInput(obj, 1, (short)0);      // 직접입력
                SetInput(obj, 2, long.Parse(fromDate));
                SetInput(obj, 3, long.Parse(toDate));
                SetInput(obj, 4, tradeType);
                SetInput(obj, 5, investorType);
                SetInput(obj, 6, dataType);

                BlockRequest(obj);
                Thread.Sleep(RequestDelayMs);

                int count = Convert.ToInt32(GetHeader(obj, 3));
                progress?.Report($"[{stockCode}] {count}건 수신 중 ...");

                string unit      = dataType == '1' ? "순매수수량(주)" : "추정금액(백만원)";
                string stockName = GetStockName(stockCode);

                for (int i = 0; i < count; i++)
                {
                    int    rawDate = Convert.ToInt32(GetData(obj, 0, i));
                    string dateStr = rawDate.ToString();
                    DateTime dt   = DateTime.ParseExact(dateStr, "yyyyMMdd", null);

                    result.Add(new InvestorTradeData
                    {
                        StockCode      = stockCode,
                        StockName      = stockName,
                        Date           = dateStr,
                        DateValue      = dt,
                        Unit           = unit,
                        Individual     = Convert.ToInt64(GetData(obj, 1,  i)),
                        Foreigner      = Convert.ToInt64(GetData(obj, 2,  i)),
                        Institution    = Convert.ToInt64(GetData(obj, 3,  i)),
                        FinancialInvest= Convert.ToInt64(GetData(obj, 4,  i)),
                        Insurance      = Convert.ToInt64(GetData(obj, 5,  i)),
                        InvestTrust    = Convert.ToInt64(GetData(obj, 6,  i)),
                        Bank           = Convert.ToInt64(GetData(obj, 7,  i)),
                        OtherFinancial = Convert.ToInt64(GetData(obj, 8,  i)),
                        PensionFund    = Convert.ToInt64(GetData(obj, 9,  i)),
                        OtherCorp      = Convert.ToInt64(GetData(obj, 10, i)),
                        ForeignerEtc   = Convert.ToInt64(GetData(obj, 11, i)),
                        PrivateEquity  = Convert.ToInt64(GetData(obj, 12, i)),
                        Government     = Convert.ToInt64(GetData(obj, 13, i)),
                    });
                }

                result.Sort((a, b) => a.DateValue.CompareTo(b.DateValue));
                progress?.Report($"[{stockCode}] 완료 ({result.Count}건)");
            }
            finally
            {
                Release(obj);
            }

            return result;
        }

        // ── COM 헬퍼 ─────────────────────────────────────────────────
        private static void SetInput(object obj, int field, object value) =>
            obj.GetType().InvokeMember("SetInputValue",
                System.Reflection.BindingFlags.InvokeMethod, null, obj,
                new[] { (object)field, value });

        private static void BlockRequest(object obj) =>
            obj.GetType().InvokeMember("BlockRequest",
                System.Reflection.BindingFlags.InvokeMethod, null, obj, null);

        private static object GetHeader(object obj, int field) =>
            obj.GetType().InvokeMember("GetHeaderValue",
                System.Reflection.BindingFlags.InvokeMethod, null, obj, new object[] { field });

        private static object GetData(object obj, int field, int row) =>
            obj.GetType().InvokeMember("GetDataValue",
                System.Reflection.BindingFlags.InvokeMethod, null, obj, new object[] { field, row });

        private static object GetProp(object obj, string prop) =>
            obj.GetType().InvokeMember(prop,
                System.Reflection.BindingFlags.GetProperty, null, obj, null);

        private static object CallMethod(object obj, string method, params object[] args) =>
            obj.GetType().InvokeMember(method,
                System.Reflection.BindingFlags.InvokeMethod, null, obj, args);

        private static void Release(object obj)
        {
            try { Marshal.ReleaseComObject(obj); } catch { }
        }
    }
}
