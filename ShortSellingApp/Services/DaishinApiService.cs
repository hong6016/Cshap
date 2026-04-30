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
        private const int RequestDelayMs = 250;  // API 호출 간격 (초당 5회 제한)

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

        // ── 종목별 공매도 추이 수집 (CpSysDib.CpSvr7238) ────────────
        //
        // SetInputValue
        //   0  string  순매도 종목코드   예) "A005930"
        //   1  char    거래소구분        'A'=전체, 'K'=KRX(default), 'N'=NXT
        //
        // GetHeaderValue
        //   0  long    수신갯수
        //   1  char    거래소구분(A/K/N)
        //
        // GetDataValue(field, index)
        //   0  ulong   거래일자
        //   1  ulong   종가
        //   2  long    전일대비
        //   3  long    전일대비율
        //   4  long    거래량
        //   5  ulong   공매도량
        //   6  double  공매도비중(%)
        //   7  ulong   공매도거래대금
        //   8  ulong   평균가
        //   9  long    평균가대비
        //
        // 연속여부: O (Continue 프로퍼티로 추가 데이터 수신)
        // ─────────────────────────────────────────────────────────────
        public List<ShortSellData> GetShortSellData(
            string stockCode,
            string fromDate,
            string toDate,
            char   exchange = 'K',
            IProgress<string> progress = null)
        {
            var result = new List<ShortSellData>();

            Type t = Type.GetTypeFromProgID("CpSysDib.CpSvr7238");
            if (t == null)
                throw new InvalidOperationException(
                    "CpSysDib.CpSvr7238 COM 오브젝트를 찾을 수 없습니다.\n" +
                    "CYBOS Plus HTS가 실행 중인지 확인하십시오.");

            object obj = Activator.CreateInstance(t);
            try
            {
                SetInput(obj, 0, stockCode);
                SetInput(obj, 1, exchange);

                string   stockName = GetStockName(stockCode);
                DateTime dtFrom    = DateTime.ParseExact(fromDate, "yyyyMMdd", null);
                DateTime dtTo      = DateTime.ParseExact(toDate,   "yyyyMMdd", null);

                progress?.Report($"[{stockCode}] 공매도 추이 요청 ({fromDate}~{toDate}) ...");

                int iteration = 0;
                const int MaxIterations = 200;

                while (iteration++ < MaxIterations)
                {
                    BlockRequest(obj);
                    Thread.Sleep(RequestDelayMs);

                    int count = Convert.ToInt32(GetHeader(obj, 0));
                    if (count == 0) break;

                    progress?.Report($"[{stockCode}] {iteration}차 수신 {count}건 파싱 중 ...");

                    bool reachedFrom = false;
                    for (int i = 0; i < count; i++)
                    {
                        string   dateStr = Convert.ToUInt64(GetData(obj, 0, i)).ToString();
                        DateTime dt      = DateTime.ParseExact(dateStr, "yyyyMMdd", null);

                        if (dt < dtFrom) { reachedFrom = true; break; }
                        if (dt > dtTo)   continue;

                        result.Add(new ShortSellData
                        {
                            StockCode    = stockCode,
                            StockName    = stockName,
                            Exchange     = exchange.ToString(),
                            Date         = dateStr,
                            DateValue    = dt,
                            ClosePrice   = Convert.ToInt64(GetData(obj, 1, i)),
                            PriceChange  = Convert.ToInt64(GetData(obj, 2, i)),
                            ChangeRate   = Convert.ToDouble(GetData(obj, 3, i)),
                            Volume       = Convert.ToInt64(GetData(obj, 4, i)),
                            ShortVolume  = Convert.ToInt64(GetData(obj, 5, i)),
                            ShortRatio   = Convert.ToDouble(GetData(obj, 6, i)),
                            ShortAmount  = Convert.ToInt64(GetData(obj, 7, i)),
                            AvgPrice     = Convert.ToInt64(GetData(obj, 8, i)),
                            AvgPriceDiff = Convert.ToInt64(GetData(obj, 9, i)),
                        });
                    }

                    if (reachedFrom) break;

                    // 연속 데이터 여부 확인
                    bool canContinue = false;
                    try { canContinue = Convert.ToBoolean(GetProp(obj, "Continue")); }
                    catch { }
                    if (!canContinue) break;
                }

                result.Sort((a, b) => a.DateValue.CompareTo(b.DateValue));
                progress?.Report($"[{stockCode}] 완료 — {result.Count}건");
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
                System.Reflection.BindingFlags.InvokeMethod, null, obj,
                new object[] { field });

        private static object GetData(object obj, int field, int row) =>
            obj.GetType().InvokeMember("GetDataValue",
                System.Reflection.BindingFlags.InvokeMethod, null, obj,
                new object[] { field, row });

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
