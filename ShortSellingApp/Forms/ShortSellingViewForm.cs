using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;
using ShortSellingApp.Models;

namespace ShortSellingApp.Forms
{
    /// <summary>
    /// 종목별 공매도 추이 — 차트 / 그리드 조회 폼 (CpSvr7238)
    ///
    /// ChartArea 1 (상단 65%): 종가 꺾은선(좌축) + 공매도비중% 꺾은선(우축)
    /// ChartArea 2 (하단 30%): 공매도량 막대
    /// </summary>
    public class ShortSellingViewForm : Form
    {
        private readonly List<ShortSellData> _data;
        private readonly string _stockCode;
        private readonly string _stockName;

        private Chart        chart;
        private DataGridView dgvData;
        private TabControl   tabControl;
        private ComboBox     cmbChartMode;
        private Label        lblSummary;

        public ShortSellingViewForm(List<ShortSellData> data, string stockCode, string stockName)
        {
            _data      = data;
            _stockCode = stockCode;
            _stockName = string.IsNullOrEmpty(stockName) ? stockCode : stockName;

            InitializeComponent();
            BuildChart();
            PopulateGrid();
            LoadSummary();
        }

        private void InitializeComponent()
        {
            this.Text          = $"공매도 추이 차트 — {_stockName}({_stockCode})";
            this.Size          = new Size(1300, 820);
            this.MinimumSize   = new Size(1024, 640);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font          = new Font("맑은 고딕", 9f);
            this.BackColor     = Color.WhiteSmoke;

            // ── 상단 툴바 ─────────────────────────────────────────
            var panelTop = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 44,
                BackColor = Color.White,
                Padding   = new Padding(8, 8, 8, 0),
            };
            panelTop.Paint += (s, e) =>
                e.Graphics.DrawLine(Pens.LightGray, 0, panelTop.Height - 1,
                                    panelTop.Width, panelTop.Height - 1);

            panelTop.Controls.Add(new Label
            {
                Text      = $"[{_stockName}] 공매도 추이  ({_data.First().Date} ~ {_data.Last().Date})",
                Font      = new Font("맑은 고딕", 10f, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue,
                AutoSize  = true,
                Location  = new Point(10, 12),
            });

            panelTop.Controls.Add(new Label
                { Text = "차트 유형:", AutoSize = true, Location = new Point(530, 14) });

            cmbChartMode = new ComboBox
            {
                Location      = new Point(592, 10),
                Width         = 180,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Parent        = panelTop,
            };
            cmbChartMode.Items.AddRange(new object[]
            {
                "종가 + 공매도비중",
                "공매도량 + 공매도비중",
                "공매도거래대금 + 비중",
                "모두 표시",
            });
            cmbChartMode.SelectedIndex         = 0;
            cmbChartMode.SelectedIndexChanged += (s, e) => BuildChart();

            var btnRefresh = new Button
            {
                Text      = "갱신",
                Location  = new Point(780, 9),
                Width     = 60,
                Height    = 26,
                BackColor = Color.SteelBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("맑은 고딕", 9f, FontStyle.Bold),
                Parent    = panelTop,
            };
            btnRefresh.Click += (s, e) => BuildChart();

            // ── 요약 패널 ─────────────────────────────────────────
            var panelSummary = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 32,
                BackColor = Color.FromArgb(240, 248, 255),
                Padding   = new Padding(10, 6, 10, 0),
            };
            lblSummary = new Label
            {
                Dock      = DockStyle.Fill,
                Font      = new Font("맑은 고딕", 8.5f),
                ForeColor = Color.DarkSlateGray,
                AutoSize  = false,
            };
            panelSummary.Controls.Add(lblSummary);

            // ── TabControl ────────────────────────────────────────
            tabControl = new TabControl { Dock = DockStyle.Fill };

            var tabChart = new TabPage("차트");
            chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.White };
            tabChart.Controls.Add(chart);

            var tabGrid = new TabPage("데이터 그리드");
            dgvData = BuildGrid();
            tabGrid.Controls.Add(dgvData);

            tabControl.TabPages.Add(tabChart);
            tabControl.TabPages.Add(tabGrid);

            this.Controls.Add(tabControl);
            this.Controls.Add(panelSummary);
            this.Controls.Add(panelTop);
        }

        // ────────────────────────────────────────────────────────────
        // 차트 구성
        // ────────────────────────────────────────────────────────────
        private void BuildChart()
        {
            chart.Series.Clear();
            chart.ChartAreas.Clear();
            chart.Legends.Clear();

            int mode = cmbChartMode.SelectedIndex;

            // ── ChartArea 1: 가격/비중 ────────────────────────────
            var area1 = MakeArea("Area1");
            area1.AxisY.Title             = mode == 0 ? "종가 (원)"
                                          : mode == 2 ? "공매도거래대금 (원)"
                                          : "공매도량 (주)";
            area1.AxisY.LabelStyle.Format = "N0";
            area1.AxisY2.Enabled          = AxisEnabled.True;
            area1.AxisY2.Title            = "공매도비중 (%)";
            area1.AxisY2.LabelStyle.Format = "F2";
            area1.AxisY2.MajorGrid.Enabled = false;
            area1.Position = new ElementPosition(0, 5, 100, 63);

            // ── ChartArea 2: 공매도량 막대 ────────────────────────
            var area2 = MakeArea("Area2");
            area2.AxisY.Title             = "공매도량 (주)";
            area2.AxisY.LabelStyle.Format = "N0";
            area2.AlignWithChartArea      = "Area1";
            area2.AlignmentOrientation    = AreaAlignmentOrientations.Vertical;
            area2.AlignmentStyle          = AreaAlignmentStyles.All;
            area2.Position = new ElementPosition(0, 71, 100, 26);

            chart.ChartAreas.Add(area1);
            chart.ChartAreas.Add(area2);

            var legend = new Legend
            {
                Docking   = Docking.Top,
                Alignment = StringAlignment.Center,
                BackColor = Color.Transparent,
                Font      = new Font("맑은 고딕", 8.5f),
                IsDockedInsideChartArea = false,
            };
            chart.Legends.Add(legend);

            // ── 종가 (Area1, 좌축) ────────────────────────────────
            if (mode == 0 || mode == 3)
            {
                var sClose = new Series("종가")
                {
                    ChartType   = SeriesChartType.Line,
                    ChartArea   = "Area1",
                    YAxisType   = AxisType.Primary,
                    Color       = Color.DodgerBlue,
                    BorderWidth = 2,
                    IsVisibleInLegend = true,
                };
                foreach (var d in _data) sClose.Points.AddXY(d.DateValue, d.ClosePrice);
                chart.Series.Add(sClose);
            }

            // ── 공매도거래대금 (Area1, 좌축) ─────────────────────
            if (mode == 2 || mode == 3)
            {
                var sAmt = new Series("공매도거래대금")
                {
                    ChartType   = SeriesChartType.Column,
                    ChartArea   = "Area1",
                    YAxisType   = AxisType.Primary,
                    Color       = Color.FromArgb(160, 255, 165, 0),
                    IsVisibleInLegend = true,
                };
                foreach (var d in _data) sAmt.Points.AddXY(d.DateValue, d.ShortAmount);
                chart.Series.Add(sAmt);
            }

            // ── 공매도비중% (Area1, 우축) — 공통 ─────────────────
            var sRatio = new Series("공매도비중(%)")
            {
                ChartType       = SeriesChartType.Line,
                ChartArea       = "Area1",
                YAxisType       = AxisType.Secondary,
                Color           = Color.Crimson,
                BorderWidth     = 2,
                BorderDashStyle = ChartDashStyle.Dash,
                IsVisibleInLegend = true,
            };
            foreach (var d in _data) sRatio.Points.AddXY(d.DateValue, d.ShortRatio);
            chart.Series.Add(sRatio);

            // ── 공매도비중 5일 이동평균 (Area1, 우축) ────────────
            if (_data.Count >= 5)
            {
                var sMa5 = new Series("공매도비중 MA5")
                {
                    ChartType       = SeriesChartType.Line,
                    ChartArea       = "Area1",
                    YAxisType       = AxisType.Secondary,
                    Color           = Color.DarkOrange,
                    BorderWidth     = 1,
                    BorderDashStyle = ChartDashStyle.Dot,
                    IsVisibleInLegend = true,
                };
                for (int i = 4; i < _data.Count; i++)
                {
                    double avg = _data.Skip(i - 4).Take(5).Average(d => d.ShortRatio);
                    sMa5.Points.AddXY(_data[i].DateValue, avg);
                }
                chart.Series.Add(sMa5);
            }

            // ── 공매도량 막대 (Area2) ─────────────────────────────
            var sVol = new Series("공매도량")
            {
                ChartType   = SeriesChartType.Column,
                ChartArea   = "Area2",
                YAxisType   = AxisType.Primary,
                Color       = Color.FromArgb(180, 70, 130, 180),
                IsVisibleInLegend = true,
            };
            foreach (var d in _data) sVol.Points.AddXY(d.DateValue, d.ShortVolume);
            chart.Series.Add(sVol);

            // X축 날짜 포맷
            foreach (var area in chart.ChartAreas)
                area.AxisX.LabelStyle.Format = "MM/dd";
        }

        private static ChartArea MakeArea(string name)
        {
            var area = new ChartArea(name)
            {
                BackColor   = Color.White,
                BorderColor = Color.LightGray,
            };
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(30, 0, 0, 0);
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(30, 0, 0, 0);
            area.AxisX.LabelStyle.Font     = new Font("맑은 고딕", 7.5f);
            area.AxisY.LabelStyle.Font     = new Font("맑은 고딕", 7.5f);
            return area;
        }

        // ────────────────────────────────────────────────────────────
        // 데이터 그리드
        // ────────────────────────────────────────────────────────────
        private DataGridView BuildGrid()
        {
            var grid = new DataGridView
            {
                Dock                    = DockStyle.Fill,
                ReadOnly                = true,
                AutoSizeColumnsMode     = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows      = false,
                RowHeadersVisible       = false,
                SelectionMode           = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor         = Color.White,
                BorderStyle             = BorderStyle.None,
                GridColor               = Color.LightGray,
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                    { BackColor = Color.AliceBlue },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    Font      = new Font("맑은 고딕", 8.5f, FontStyle.Bold),
                    BackColor = Color.FromArgb(50, 100, 150),
                    ForeColor = Color.White,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                },
                EnableHeadersVisualStyles = false,
            };

            var cols = new (string H, string Fmt, int W)[]
            {
                ("날짜",              "",    70),
                ("종가(원)",          "N0",  80),
                ("전일대비",          "N0",  75),
                ("전일대비율(%)",     "F2",  85),
                ("거래량(주)",        "N0",  95),
                ("공매도량(주)",      "N0",  95),
                ("공매도비중(%)",     "F2",  85),
                ("공매도거래대금(원)","N0", 130),
                ("평균가(원)",        "N0",  80),
                ("평균가대비",        "N0",  80),
            };

            foreach (var (h, fmt, w) in cols)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText       = h,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Format    = fmt,
                        Alignment = h == "날짜"
                            ? DataGridViewContentAlignment.MiddleCenter
                            : DataGridViewContentAlignment.MiddleRight,
                    },
                    FillWeight = w,
                });
            }

            return grid;
        }

        private void PopulateGrid()
        {
            dgvData.Rows.Clear();
            foreach (var d in _data.OrderByDescending(x => x.DateValue))
            {
                int idx = dgvData.Rows.Add(
                    d.Date, d.ClosePrice, d.PriceChange, d.ChangeRate,
                    d.Volume, d.ShortVolume, d.ShortRatio,
                    d.ShortAmount, d.AvgPrice, d.AvgPriceDiff);

                // 공매도비중 5% 이상 강조
                if (d.ShortRatio >= 5.0)
                {
                    dgvData.Rows[idx].DefaultCellStyle.BackColor = Color.MistyRose;
                    dgvData.Rows[idx].DefaultCellStyle.ForeColor = Color.DarkRed;
                }

                // 전일대비 색상
                dgvData.Rows[idx].Cells[2].Style.ForeColor =
                    d.PriceChange > 0 ? Color.Crimson
                  : d.PriceChange < 0 ? Color.DodgerBlue
                  : Color.Black;
            }
        }

        // ────────────────────────────────────────────────────────────
        // 요약 통계
        // ────────────────────────────────────────────────────────────
        private void LoadSummary()
        {
            if (_data.Count == 0) return;

            double avgRatio = _data.Average(d => d.ShortRatio);
            double maxRatio = _data.Max(d => d.ShortRatio);
            string maxDate  = _data.OrderByDescending(d => d.ShortRatio).First().Date;
            long   sumVol   = _data.Sum(d => d.ShortVolume);
            long   maxClose = _data.Max(d => d.ClosePrice);
            long   minClose = _data.Min(d => d.ClosePrice);

            lblSummary.Text =
                $"기간: {_data.Count}일   " +
                $"평균 공매도비중: {avgRatio:F2}%   " +
                $"최고 공매도비중: {maxRatio:F2}% ({maxDate})   " +
                $"누적 공매도량: {sumVol:N0}주   " +
                $"종가 범위: {minClose:N0} ~ {maxClose:N0}원";
        }
    }
}
