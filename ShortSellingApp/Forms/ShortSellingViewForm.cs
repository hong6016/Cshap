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
    /// 투자주체별 매매현황 — 차트 / 그리드 조회 폼
    ///
    /// ChartArea 1 (상단): 개인·외국인·기관계 꺾은선
    /// ChartArea 2 (하단): 선택 항목 막대 비교
    /// </summary>
    public class ShortSellingViewForm : Form
    {
        private readonly List<InvestorTradeData> _data;
        private readonly string _stockCode;
        private readonly string _stockName;

        // ── 컨트롤 ──────────────────────────────────────────────────
        private Chart        chart;
        private DataGridView dgvData;
        private TabControl   tabControl;
        private ComboBox     cmbChartMode;
        private CheckedListBox clbInvestors;
        private Label        lblSummary;

        // 투자자 항목 목록 (표시명, 속성접근자)
        private static readonly (string Name, Func<InvestorTradeData, long> Get)[] InvestorDefs =
        {
            ("개인",        d => d.Individual),
            ("외국인",      d => d.Foreigner),
            ("기관계",      d => d.Institution),
            ("금융투자",    d => d.FinancialInvest),
            ("보험",        d => d.Insurance),
            ("투신",        d => d.InvestTrust),
            ("은행",        d => d.Bank),
            ("기타금융",    d => d.OtherFinancial),
            ("연기금",      d => d.PensionFund),
            ("기타법인",    d => d.OtherCorp),
            ("사모펀드",    d => d.PrivateEquity),
            ("정부/지자체", d => d.Government),
        };

        // 시리즈 색상
        private static readonly Color[] SeriesColors =
        {
            Color.DodgerBlue, Color.Crimson,     Color.SeaGreen,
            Color.DarkOrange, Color.Purple,       Color.SaddleBrown,
            Color.Teal,       Color.DarkSlateGray, Color.HotPink,
            Color.OliveDrab,  Color.SteelBlue,   Color.DarkGoldenrod,
        };

        public ShortSellingViewForm(List<InvestorTradeData> data, string stockCode, string stockName)
        {
            _data      = data;
            _stockCode = stockCode;
            _stockName = string.IsNullOrEmpty(stockName) ? stockCode : stockName;

            InitializeComponent();
            BuildChart();
            PopulateGrid();
            LoadSummary();
        }

        // ────────────────────────────────────────────────────────────
        // UI 구성
        // ────────────────────────────────────────────────────────────
        private void InitializeComponent()
        {
            string unit = _data.Count > 0 ? _data[0].Unit : "";
            this.Text          = $"공매도 현황 (투자주체별) — {_stockName}({_stockCode})  [{unit}]";
            this.Size          = new Size(1400, 860);
            this.MinimumSize   = new Size(1100, 660);
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
                e.Graphics.DrawLine(Pens.LightGray, 0, panelTop.Height - 1, panelTop.Width, panelTop.Height - 1);

            var lblTitle = new Label
            {
                Text      = $"[{_stockName}] 공매도 현황 추이 (투자주체별)  " +
                            $"({_data.First().Date} ~ {_data.Last().Date})",
                Font      = new Font("맑은 고딕", 10f, FontStyle.Bold),
                ForeColor = Color.DarkSlateBlue,
                AutoSize  = true,
                Location  = new Point(10, 12),
            };

            new Label { Text = "차트 유형:", AutoSize = true, Location = new Point(520, 14), Parent = panelTop };

            cmbChartMode = new ComboBox
            {
                Location      = new Point(582, 10),
                Width         = 160,
                DropDownStyle = ComboBoxStyle.DropDownList,
                Parent        = panelTop,
            };
            cmbChartMode.Items.AddRange(new object[]
            {
                "꺾은선 (개인/외국인/기관)",
                "막대 비교 (선택 항목)",
                "누적 막대",
                "누적 영역",
            });
            cmbChartMode.SelectedIndex         = 0;
            cmbChartMode.SelectedIndexChanged += (s, e) => BuildChart();

            var btnRefresh = new Button
            {
                Text      = "갱신",
                Location  = new Point(750, 9),
                Width     = 60,
                Height    = 26,
                BackColor = Color.SteelBlue,
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("맑은 고딕", 9f, FontStyle.Bold),
                Parent    = panelTop,
            };
            btnRefresh.Click += (s, e) => BuildChart();

            panelTop.Controls.Add(lblTitle);

            // ── 요약 레이블 ───────────────────────────────────────
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

            // ── 좌측 투자자 체크박스 패널 ─────────────────────────
            var panelLeft = new Panel
            {
                Dock      = DockStyle.Left,
                Width     = 120,
                BackColor = Color.White,
                Padding   = new Padding(6),
            };
            panelLeft.Paint += (s, e) =>
                e.Graphics.DrawLine(Pens.LightGray, panelLeft.Width - 1, 0,
                                    panelLeft.Width - 1, panelLeft.Height);

            var lblCheck = new Label
            {
                Text     = "표시 항목",
                Font     = new Font("맑은 고딕", 8.5f, FontStyle.Bold),
                Dock     = DockStyle.Top,
                Height   = 22,
                TextAlign = ContentAlignment.MiddleCenter,
            };

            clbInvestors = new CheckedListBox
            {
                Dock          = DockStyle.Fill,
                CheckOnClick  = true,
                BorderStyle   = BorderStyle.None,
                Font          = new Font("맑은 고딕", 8.5f),
                BackColor     = Color.White,
            };
            foreach (var (name, _) in InvestorDefs)
                clbInvestors.Items.Add(name, name == "개인" || name == "외국인" || name == "기관계");
            clbInvestors.ItemCheck += (s, e) => this.BeginInvoke((Action)BuildChart);

            panelLeft.Controls.Add(clbInvestors);
            panelLeft.Controls.Add(lblCheck);

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

            // ── 레이아웃 ──────────────────────────────────────────
            this.Controls.Add(tabControl);
            this.Controls.Add(panelLeft);
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

            // 체크된 항목
            var selected = new List<(string Name, Func<InvestorTradeData, long> Get, Color Color)>();
            for (int i = 0; i < clbInvestors.Items.Count; i++)
            {
                if (clbInvestors.GetItemChecked(i))
                    selected.Add((InvestorDefs[i].Name, InvestorDefs[i].Get, SeriesColors[i]));
            }
            if (selected.Count == 0) return;

            // ── 상단 ChartArea: 주요 추이 ─────────────────────────
            var area1 = MakeArea("Area1");
            area1.AxisX.LabelStyle.Format = "MM/dd";
            area1.AxisY.Title             = _data.Count > 0 ? _data[0].Unit : "";
            area1.AxisY.LabelStyle.Format = "N0";
            area1.AxisY.StripLines.Add(new StripLine
            {
                Interval     = 0,
                IntervalOffset = 0,
                StripWidth   = 0.001,
                BackColor    = Color.Black,
            });
            area1.Position = new ElementPosition(0, 5, 100, 60);
            chart.ChartAreas.Add(area1);

            // ── 하단 ChartArea: 막대 비교 ─────────────────────────
            var area2 = MakeArea("Area2");
            area2.AxisX.LabelStyle.Format  = "MM/dd";
            area2.AxisY.LabelStyle.Format  = "N0";
            area2.AlignWithChartArea       = "Area1";
            area2.AlignmentOrientation     = AreaAlignmentOrientations.Vertical;
            area2.AlignmentStyle           = AreaAlignmentStyles.All;
            area2.Position = new ElementPosition(0, 68, 100, 28);
            chart.ChartAreas.Add(area2);

            // ── 범례 ──────────────────────────────────────────────
            var legend = new Legend
            {
                Docking   = Docking.Top,
                Alignment = StringAlignment.Center,
                BackColor = Color.Transparent,
                Font      = new Font("맑은 고딕", 8.5f),
                IsDockedInsideChartArea = false,
            };
            chart.Legends.Add(legend);

            // ── 시리즈 추가 ───────────────────────────────────────
            SeriesChartType upperType = mode == 0 ? SeriesChartType.Line
                                      : mode == 2 ? SeriesChartType.StackedColumn
                                      : mode == 3 ? SeriesChartType.StackedArea
                                      : SeriesChartType.Column;

            foreach (var (name, get, color) in selected)
            {
                // 상단 시리즈
                var s1 = new Series(name)
                {
                    ChartType   = upperType,
                    ChartArea   = "Area1",
                    Color       = color,
                    BorderWidth = 2,
                    IsVisibleInLegend = true,
                };
                if (upperType == SeriesChartType.Line)
                    s1["EmptyPointValue"] = "Zero";
                foreach (var d in _data)
                    s1.Points.AddXY(d.DateValue, get(d));
                chart.Series.Add(s1);

                // 하단 막대 시리즈 (Column 고정)
                var s2 = new Series(name + "_bar")
                {
                    ChartType         = SeriesChartType.Column,
                    ChartArea         = "Area2",
                    Color             = Color.FromArgb(160, color),
                    IsVisibleInLegend = false,
                };
                foreach (var d in _data)
                    s2.Points.AddXY(d.DateValue, get(d));
                chart.Series.Add(s2);
            }

            // 기준선 (0)
            area1.AxisY.StripLines.Clear();
            area1.AxisY.StripLines.Add(new StripLine
            {
                Interval       = 0,
                IntervalOffset = 0,
                StripWidth     = 0.0001,
                BackColor      = Color.DimGray,
            });
        }

        private static ChartArea MakeArea(string name)
        {
            var area = new ChartArea(name)
            {
                BackColor    = Color.White,
                BorderColor  = Color.LightGray,
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
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.AliceBlue },
                ColumnHeadersDefaultCellStyle   = new DataGridViewCellStyle
                {
                    Font      = new Font("맑은 고딕", 8.5f, FontStyle.Bold),
                    BackColor = Color.FromArgb(50, 100, 150),
                    ForeColor = Color.White,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                },
                EnableHeadersVisualStyles = false,
            };

            var headers = new[] { "날짜", "개인(공매도)", "외국인(공매도)", "기관계(공매도)",
                                   "금융투자", "보험", "투신", "은행", "기타금융", "연기금",
                                   "기타법인", "사모펀드", "정부/지자체" };

            grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = headers[0],
                DefaultCellStyle = new DataGridViewCellStyle
                    { Alignment = DataGridViewContentAlignment.MiddleCenter },
                FillWeight = 70,
            });
            for (int i = 1; i < headers.Length; i++)
            {
                grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText = headers[i],
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleRight,
                        Format    = "N0",
                    },
                    FillWeight = 75,
                });
            }

            return grid;
        }

        private void PopulateGrid()
        {
            dgvData.Rows.Clear();
            // 최신 날짜 순 표시
            var sorted = _data.OrderByDescending(d => d.DateValue).ToList();
            foreach (var d in sorted)
            {
                int idx = dgvData.Rows.Add(
                    d.Date, d.Individual, d.Foreigner, d.Institution,
                    d.FinancialInvest, d.Insurance, d.InvestTrust, d.Bank,
                    d.OtherFinancial, d.PensionFund, d.OtherCorp,
                    d.PrivateEquity, d.Government);

                // 외국인 순매수 상위 강조
                if (d.Foreigner > 0)
                {
                    dgvData.Rows[idx].Cells[2].Style.ForeColor  = Color.DarkBlue;
                    dgvData.Rows[idx].Cells[2].Style.Font =
                        new Font("맑은 고딕", 8.5f, FontStyle.Bold);
                }
                else if (d.Foreigner < 0)
                {
                    dgvData.Rows[idx].Cells[2].Style.ForeColor = Color.Crimson;
                }
            }
        }

        // ────────────────────────────────────────────────────────────
        // 요약 통계
        // ────────────────────────────────────────────────────────────
        private void LoadSummary()
        {
            if (_data.Count == 0) return;
            long sumInd = _data.Sum(d => d.Individual);
            long sumFor = _data.Sum(d => d.Foreigner);
            long sumIns = _data.Sum(d => d.Institution);
            string unit = _data[0].Unit;

            lblSummary.Text =
                $"기간: {_data.Count}일   " +
                $"개인 공매도 누적: {sumInd:N0}   " +
                $"외국인 공매도 누적: {sumFor:N0}   " +
                $"기관계 공매도 누적: {sumIns:N0}   " +
                $"단위: {unit}";
        }
    }
}
