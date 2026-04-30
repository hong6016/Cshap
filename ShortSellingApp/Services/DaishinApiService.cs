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
        // BlockRequest 연속 호출 시 최소 대기 (API 제한: 초당 최대 5회)
        private const int RequestDelayMs = 250;

        // ──────────────────────────────────────────────────────────────
        // 연결 상태 확인
        // ──────────────────────────────────────────────────────────────
        public bool IsConnected()
        {
            try
            {
                Type t = Type.GetTypeFromProgID("CpUtil.CpCybos");
                if (t == null) return false;
                object obj = Activator.CreateInstance(t);
                int state = (int)Invoke(obj, "IsConnect");
                Release(obj);
                return state == 1;
            }
            catch
            {
                return false;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 종목코드 → 종목명
        // ──────────────────────────────────────────────────────────────
        public string GetStockName(string stockCode)
        {
            try
            {
                Type t = Type.GetTypeFromProgID("CpUtil.CpStockCode");
                if (t == null) return stockCode;
                object obj = Activator.CreateInstance(t);
                string name = InvokeMethod(obj, "CodeToName", stockCode)?.ToString() ?? stockCode;
                Release(obj);
                return name;
            }
            catch
            {
                return stockCode;
            }
        }

        // ──────────────────────────────────────────────────────────────
        // 공매도 현황 수집  (CpSysDib.CpSvr8562)
        //
        // 입력
        //   0 : 종목코드  (예: "A005930")
        //   1 : 조회시작일 (YYYYMMDD)
        //   2 : 조회종료일 (YYYYMMDD)
        //   3 : 최대조회수
        //
        // 헤더
        //   0 : 종목코드
        //   1 : 조회갯수
        //
        // 데이터 필드
        //   0 : 날짜           (int,    YYYYMMDD)
        //   1 : 공매도거래량    (long,   주)
        //   2 : 공매도거래대금  (long,   원)
        //   3 : 공매도비중      (double, %)
        //   4 : 종가            (int,    원)
        //   5 : 거래량          (long,   주)
        // ──────────────────────────────────────────────────────────────
        public List<ShortSellingData> GetShortSellingData(
            string stockCode, string fromDate, string toDate,
            int maxCount = 2000, IProgress<string> progress = null)
        {
            var result = new List<ShortSellingData>();

            Type t = Type.GetTypeFromProgID("CpSysDib.CpSvr8562");
            if (t == null)
                throw new InvalidOperationException(
                    "CpSysDib.CpSvr8562 COM 오브젝트를 찾을 수 없습니다.\n" +
                    "CYBOS Plus HTS가 실행 중인지 확인하십시오.");

            object obj = Activator.CreateInstance(t);
            try
            {
                progress?.Report($"[{stockCode}] 공매도 데이터 요청 중 ({fromDate} ~ {toDate}) ...");

                SetInputValue(obj, 0, stockCode);
                SetInputValue(obj, 1, fromDate);
                SetInputValue(obj, 2, toDate);
                SetInputValue(obj, 3, maxCount);

                BlockRequest(obj);
                Thread.Sleep(RequestDelayMs);

                int count = Convert.ToInt32(GetHeaderValue(obj, 1));
                progress?.Report($"[{stockCode}] 수신 {count}건 파싱 중 ...");

                string stockName = GetStockName(stockCode);

                for (int i = 0; i < count; i++)
                {
                    int rawDate  = Convert.ToInt32(GetDataValue(obj, 0, i));
                    string dateStr = rawDate.ToString();
                    DateTime dt = DateTime.ParseExact(dateStr, "yyyyMMdd", null);

                    result.Add(new ShortSellingData
                    {
                        StockCode   = stockCode,
                        StockName   = stockName,
                        Date        = dateStr,
                        DateValue   = dt,
                        ShortVolume = Convert.ToInt64(GetDataValue(obj, 1, i)),
                        ShortAmount = Convert.ToInt64(GetDataValue(obj, 2, i)),
                        ShortRatio  = Convert.ToDouble(GetDataValue(obj, 3, i)),
                        ClosePrice  = Convert.ToInt32(GetDataValue(obj, 4, i)),
                        TotalVolume = Convert.ToInt64(GetDataValue(obj, 5, i)),
                    });
                }

                // 날짜 오름차순 정렬
                result.Sort((a, b) => a.DateValue.CompareTo(b.DateValue));

                progress?.Report($"[{stockCode}] 완료 ({result.Count}건)");
            }
            finally
            {
                Release(obj);
            }

            return result;
        }

        // ──────────────────────────────────────────────────────────────
        // COM Interop 헬퍼
        // ──────────────────────────────────────────────────────────────
        private static void SetInputValue(object obj, int field, object value)
        {
            obj.GetType().InvokeMember("SetInputValue",
                System.Reflection.BindingFlags.InvokeMethod,
                null, obj, new object[] { field, value });
        }

        private static void BlockRequest(object obj)
        {
            obj.GetType().InvokeMember("BlockRequest",
                System.Reflection.BindingFlags.InvokeMethod,
                null, obj, null);
        }

        private static object GetHeaderValue(object obj, int field)
        {
            return obj.GetType().InvokeMember("GetHeaderValue",
                System.Reflection.BindingFlags.InvokeMethod,
                null, obj, new object[] { field });
        }

        private static object GetDataValue(object obj, int field, int index)
        {
            return obj.GetType().InvokeMember("GetDataValue",
                System.Reflection.BindingFlags.InvokeMethod,
                null, obj, new object[] { field, index });
        }

        private static object Invoke(object obj, string prop)
        {
            return obj.GetType().InvokeMember(prop,
                System.Reflection.BindingFlags.GetProperty,
                null, obj, null);
        }

        private static object InvokeMethod(object obj, string method, params object[] args)
        {
            return obj.GetType().InvokeMember(method,
                System.Reflection.BindingFlags.InvokeMethod,
                null, obj, args);
        }

        private static void Release(object obj)
        {
            try { Marshal.ReleaseComObject(obj); } catch { }
        }
    }
}
