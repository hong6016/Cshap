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
    /// 공매도 데이터 그래프 / 그리드 조회 폼
    ///
    /// 상단 ChartArea 1 (70%): 종가(꺾은선) + 공매도비중%(꺾은선, 우축)
    /// 하단 ChartArea 2 (30%): 공매도거래량(막대, 우축 %)
    /// </summary>
    public class ShortSellingViewForm : Form
    {
        private readonly List<ShortSellingData> _data;
        private readonly string _stockCode;
        private readonly string _stockName;

        // ── 컨트롤 ──────────────────────────────────────────────────
        private Chart            chart;
        private DataGridView     dgvData;
        private SplitContainer   splitMain;       // 좌: 차트  우: 그리드
        private ComboBox         cmbChartType;
        private Panel            panelSummary;
        private Label            lblSummaryValues;
        private TabControl       tabControl;

        // 차트 시리즈 이름
        private const string SeriesClose       = "종가";
        private const string SeriesShortRatio  = "공매도비중(%)";
        private const string SeriesShortVolume = "공매도거래량";

        public ShortSellingViewForm(List<ShortSellingData> data, string stockCode, string stockName)
        {
            _data       = data;
            _stockCode  = stockCode;
            _stockName  = string.IsNullOrEmpty(stockName) ? stockCode : stockName;

            InitializeComponent();
            LoadSummary();
            BuildChart();
            PopulateGrid();
        }

        // ────────────────────────────────────────────────────────────
        // UI 구성
        // ────────────────────────────────────────────────────────────
        private void InitializeComponent()
        {
            this.Text          = $"공매도 현황 차트 — {_stockName}({_stockCode})";
            this.Size          = new Size(1280, 800);
            this.MinimumSize   = new Size(1024, 640);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Font          = new Font("맑은 고딕", 9f);
            this.BackColor     = Color.WhiteSmoke;

            // ── 상단 툴바 패널 ─────────────────────────────────────
            var panelTop = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 44,
                BackColor = Color.White,
                Padding   = new Padding(8, 8, 8, 0),
            };
            panelTop.Paint += (s, e) =>
                e.Graphics.DrawLine(Pens.LightGray, 0, panelTop.Height - 1, panelTop.Width, panelTop.Height - 1);

            var lblTitle = new Label
            {
                Text      = $"[{_stockName}] 공매도 현황 추이  ({_data.First().Date} ~ {_data.Last().Date})",
                Font      = new Font("맑은 고딕", 10f, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue,
                AutoSize  = true,
                Location  = new Point(10, 12),
            };

            var lblChartType = new Label { Text = "차트 유형:", AutoSize = true, Location = new Point(550, 14) };

            cmbChartType = new ComboBox
            {
                Location      = new Point(615, 10),
                Width         = 140,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            cmbChartType.Items.AddRange(new object[]
            {
                "종가 + 공매도비중",
                "공매도거래량 + 비중",
                "공매도거래대금 + 비중",
                "모두 표시",
            });
            cmbChartType.SelectedIndex  = 0;
            cmbChartType.SelectedIndexChanged += (s, e) => BuildChart();

            var btnRefresh = MakeButton("차트 갱신", new Point(762, 9), 80, Color.SteelBlue);
            btnRefresh.Click += (s, e) => BuildChart();

            panelTop.Controls.AddRange(new Control[]
                { lblTitle, lblChartType, cmbChartType, btnRefresh });

            // ── 요약 패널 ──────────────────────────────────────────
            panelSummary = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 38,
                BackColor = Color.FromArgb(240, 248, 255),
                Padding   = new Padding(10, 6, 10, 0),
            };
            lblSummaryValues = new Label
            {
                Dock      = DockStyle.Fill,
                AutoSize  = false,
                ForeColor = Color.DarkSlateGray,
                Font      = new Font("맑은 고딕", 8.5f),
            };
            panelSummary.Controls.Add(lblSummaryValues);

            // ── TabControl: 차트 / 그리드 ──────────────────────────
            tabControl = new TabControl { Dock = DockStyle.Fill };

            var tabChart = new TabPage("차트");
            chart = new Chart { Dock = DockStyle.Fill, BackColor = Color.White };
            tabChart.Controls.Add(chart);

            var tabGrid = new TabPage("데이터 그리드");
            dgvData = BuildDataGridView();
            tabGrid.Controls.Add(dgvData);

            tabControl.TabPages.Add(tabChart);
            tabControl.TabPages.Add(tabGrid);

            // ── 레이아웃 조립 ──────────────────────────────────────
            this.Controls.Add(tabControl);
            this.Controls.Add(panelSummary);
            this.Controls.Add(panelTop);
        }

        // ────────────────────────────────────────────────────────────
        // 요약 통계
        // ────────────────────────────────────────────────────────────
        private void LoadSummary()
        {
            double avgRatio  = _data.Average(d => d.ShortRatio);
            double maxRatio  = _data.Max(d => d.ShortRatio);
            long   totalVol  = _data.Sum(d => d.ShortVolume);
            int    maxClose  = _data.Max(d => d.ClosePrice);
            int    minClose  = _data.Min(d => d.ClosePrice);

            lblSummaryValues.Text =
                $"기간: {_data.Count}일   " +
                $"평균 공매도비중: {avgRatio:F2}%   " +
                $"최고 공매도비중: {maxRatio:F2}%   " +
                $"누적 공매도거래량: {totalVol:N0}주   " +
                $"종가범위: {minClose:N0} ~ {maxClose:N0}원";
        }

        // ────────────────────────────────────────────────────────────
        // 차트 구성
        // ────────────────────────────────────────────────────────────
        private void BuildChart()
        {
            chart.Series.Clear();
            chart.ChartAreas.Clear();
            chart.Legends.Clear();

            int mode = cmbChartType.SelectedIndex; // 0~3

            // ── ChartArea 1: 가격/비중 메인 차트 ──────────────────
            var area1 = new ChartArea("Area1");
            StyleArea(area1);
            area1.AxisX.LabelStyle.Format = "MM/dd";
            area1.AxisX.MajorGrid.LineColor = Color.FromArgb(40, 0, 0, 0);
            area1.AxisY.Title              = mode == 0 ? "종가 (원)" : "공매도거래량 (주)";
            area1.AxisY.LabelStyle.Format  = "N0";
            area1.AxisY2.Enabled           = AxisEnabled.True;
            area1.AxisY2.Title             = "공매도비중 (%)";
            area1.AxisY2.LabelStyle.Format = "F2";
            area1.AxisY2.MajorGrid.Enabled = false;
            area1.Position = new ElementPosition(0, 5, 100, 65);

            // ── ChartArea 2: 공매도 거래량 막대 ──────────────────
            var area2 = new ChartArea("Area2");
            StyleArea(area2);
            area2.AxisX.LabelStyle.Format = "MM/dd";
            area2.AxisY.Title             = "공매도거래량 (주)";
            area2.AxisY.LabelStyle.Format = "N0";
            area2.AlignWithChartArea      = "Area1";
            area2.AlignmentOrientation    = AreaAlignmentOrientations.Vertical;
            area2.AlignmentStyle          = AreaAlignmentStyles.All;
            area2.Position = new ElementPosition(0, 73, 100, 24);

            chart.ChartAreas.Add(area1);
            chart.ChartAreas.Add(area2);

            // ── 범례 ──────────────────────────────────────────────
            var legend = new Legend
            {
                Docking          = Docking.Top,
                Alignment        = StringAlignment.Center,
                BackColor        = Color.Transparent,
                Font             = new Font("맑은 고딕", 8.5f),
                IsDockedInsideChartArea = false,
            };
            chart.Legends.Add(legend);

            // ── 시리즈: 종가 ──────────────────────────────────────
            if (mode == 0 || mode == 3)
            {
                var sClose = new Series(SeriesClose)
                {
                    ChartType   = SeriesChartType.Line,
                    ChartArea   = "Area1",
                    YAxisType   = AxisType.Primary,
                    Color       = Color.DodgerBlue,
                    BorderWidth = 2,
                    IsVisibleInLegend = true,
                };
                foreach (var d in _data)
                    sClose.Points.AddXY(d.DateValue, d.ClosePrice);
                chart.Series.Add(sClose);
            }

            // ── 시리즈: 공매도 거래대금 ───────────────────────────
            if (mode == 2 || mode == 3)
            {
                var sAmt = new Series("공매도거래대금(백만원)")
                {
                    ChartType   = SeriesChartType.Column,
                    ChartArea   = "Area1",
                    YAxisType   = AxisType.Primary,
                    Color       = Color.FromArgb(160, 255, 140, 0),
                    IsVisibleInLegend = true,
                };
                foreach (var d in _data)
                    sAmt.Points.AddXY(d.DateValue, d.ShortAmount / 1_000_000);
                chart.Series.Add(sAmt);
            }

            // ── 시리즈: 공매도 비중 (공통 — Area1 우축) ──────────
            var sRatio = new Series(SeriesShortRatio)
            {
                ChartType   = SeriesChartType.Line,
                ChartArea   = "Area1",
                YAxisType   = AxisType.Secondary,
                Color       = Color.Crimson,
                BorderWidth = 2,
                BorderDashStyle = ChartDashStyle.Dash,
                IsVisibleInLegend = true,
            };
            foreach (var d in _data)
                sRatio.Points.AddXY(d.DateValue, d.ShortRatio);
            chart.Series.Add(sRatio);

            // ── 시리즈: 공매도 거래량 (Area2) ─────────────────────
            var sVol = new Series(SeriesShortVolume)
            {
                ChartType   = SeriesChartType.Column,
                ChartArea   = "Area2",
                YAxisType   = AxisType.Primary,
                Color       = Color.FromArgb(180, 70, 130, 180),
                IsVisibleInLegend = true,
            };
            foreach (var d in _data)
                sVol.Points.AddXY(d.DateValue, d.ShortVolume);
            chart.Series.Add(sVol);

            // 이동평균선 (5일) — 공매도 비중
            if (_data.Count >= 5)
            {
                var sMa5 = new Series("공매도비중 MA5")
                {
                    ChartType   = SeriesChartType.Line,
                    ChartArea   = "Area1",
                    YAxisType   = AxisType.Secondary,
                    Color       = Color.DarkOrange,
                    BorderWidth = 1,
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
        }

        private static void StyleArea(ChartArea area)
        {
            area.BackColor          = Color.White;
            area.BorderColor        = Color.LightGray;
            area.AxisX.MajorGrid.LineColor = Color.FromArgb(30, 0, 0, 0);
            area.AxisY.MajorGrid.LineColor = Color.FromArgb(30, 0, 0, 0);
            area.AxisX.LabelStyle.Font     = new Font("맑은 고딕", 7.5f);
            area.AxisY.LabelStyle.Font     = new Font("맑은 고딕", 7.5f);
        }

        // ────────────────────────────────────────────────────────────
        // 데이터 그리드
        // ────────────────────────────────────────────────────────────
        private DataGridView BuildDataGridView()
        {
            var grid = new DataGridView
            {
                Dock              = DockStyle.Fill,
                ReadOnly          = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                AllowUserToAddRows = false,
                RowHeadersVisible = false,
                SelectionMode     = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor   = Color.White,
                BorderStyle       = BorderStyle.None,
                GridColor         = Color.LightGray,
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.AliceBlue },
                ColumnHeadersDefaultCellStyle   = new DataGridViewCellStyle
                {
                    Font      = new Font("맑은 고딕", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(50, 100, 150),
                    ForeColor = Color.White,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                },
                EnableHeadersVisualStyles = false,
            };

            var cols = new (string name, string header, string fmt, int fill, DataGridViewContentAlignment align)[]
            {
                ("Date",        "날짜",               "",    70,  DataGridViewContentAlignment.MiddleCenter),
                ("ShortVol",    "공매도거래량(주)",    "N0",  110, DataGridViewContentAlignment.MiddleRight),
                ("ShortAmt",    "공매도거래대금(백만원)", "N0", 130, DataGridViewContentAlignment.MiddleRight),
                ("ShortRatio",  "공매도비중(%)",       "F2",  90,  DataGridViewContentAlignment.MiddleRight),
                ("ClosePrice",  "종가(원)",            "N0",  85,  DataGridViewContentAlignment.MiddleRight),
                ("TotalVol",    "총거래량(주)",        "N0",  110, DataGridViewContentAlignment.MiddleRight),
                ("VolumeRatio", "공매도/총거래(%)",    "F2",  110, DataGridViewContentAlignment.MiddleRight),
            };

            foreach (var (name, header, fmt, fill, align) in cols)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name       = name,
                    HeaderText = header,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Format    = fmt,
                        Alignment = align,
                    },
                    FillWeight = fill,
                });
            }

            foreach (var d in _data)
            {
                double volRatio = d.TotalVolume > 0
                    ? (double)d.ShortVolume / d.TotalVolume * 100
                    : 0;

                int idx = grid.Rows.Add(
                    d.Date,
                    d.ShortVolume,
                    d.ShortAmount / 1_000_000,
                    d.ShortRatio,
                    d.ClosePrice,
                    d.TotalVolume,
                    volRatio);

                // 공매도비중 5% 이상 강조
                if (d.ShortRatio >= 5.0)
                {
                    grid.Rows[idx].DefaultCellStyle.BackColor = Color.MistyRose;
                    grid.Rows[idx].DefaultCellStyle.ForeColor = Color.DarkRed;
                }
            }

            // 정렬 기준: 날짜 내림차순 (최신 순)
            grid.Sort(grid.Columns["Date"], System.ComponentModel.ListSortDirection.Descending);

            return grid;
        }

        // ────────────────────────────────────────────────────────────
        // 헬퍼
        // ────────────────────────────────────────────────────────────
        private static Button MakeButton(string text, Point loc, int width, Color back)
        {
            return new Button
            {
                Text      = text,
                Location  = loc,
                Width     = width,
                Height    = 26,
                BackColor = back,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("맑은 고딕", 9f, FontStyle.Bold),
                Cursor    = Cursors.Hand,
            };
        }
    }
}
