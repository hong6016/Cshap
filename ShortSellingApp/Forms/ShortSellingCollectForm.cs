using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;
using ShortSellingApp.Models;
using ShortSellingApp.Services;

namespace ShortSellingApp.Forms
{
    /// <summary>
    /// 종목별 공매도 추이 수집 폼 (CpSysDib.CpSvr7238)
    /// </summary>
    public class ShortSellingCollectForm : Form
    {
        private readonly DaishinApiService _api = new DaishinApiService();
        private List<ShortSellData> _collectedData = new List<ShortSellData>();

        private TextBox        txtStockCode;
        private TextBox        txtStockName;
        private Button         btnSearchName;
        private DateTimePicker dtpFrom;
        private DateTimePicker dtpTo;
        private ComboBox       cmbExchange;
        private Button         btnCollect;
        private Button         btnViewChart;
        private Button         btnExportCsv;
        private DataGridView   dgvData;
        private ProgressBar    progressBar;
        private Label          lblConnectionStatus;
        private ToolStripStatusLabel lblStatus;

        public ShortSellingCollectForm()
        {
            InitializeComponent();
            CheckConnection();
        }

        private void InitializeComponent()
        {
            this.Text          = "종목별 공매도 추이 수집 - 대신플러스 [CpSvr7238]";
            this.Size          = new Size(1100, 700);
            this.MinimumSize   = new Size(900, 580);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font          = new Font("맑은 고딕", 9f);
            this.BackColor     = Color.WhiteSmoke;

            // ── 상단 입력 패널 ────────────────────────────────────
            var panelTop = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 110,
                BackColor = Color.White,
                Padding   = new Padding(12),
            };
            panelTop.Paint += (s, e) =>
                e.Graphics.DrawLine(Pens.LightGray, 0, panelTop.Height - 1,
                                    panelTop.Width, panelTop.Height - 1);

            lblConnectionStatus = new Label
            {
                AutoSize  = true,
                Font      = new Font("맑은 고딕", 8.5f, FontStyle.Bold),
                Location  = new Point(14, 10),
            };
            panelTop.Controls.Add(lblConnectionStatus);

            // 행 1: 종목코드
            AddLabel("종목코드:", 14, 38, panelTop);
            txtStockCode = new TextBox
            {
                Location        = new Point(82, 36),
                Width           = 85,
                CharacterCasing = CharacterCasing.Upper,
                MaxLength       = 8,
            };
            txtStockCode.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) btnSearchName.PerformClick();
            };
            panelTop.Controls.Add(txtStockCode);

            btnSearchName = AddButton("종목명 조회", new Point(175, 35), 82, Color.SteelBlue, panelTop);
            btnSearchName.Click += BtnSearchName_Click;

            txtStockName = new TextBox
            {
                Location  = new Point(264, 36),
                Width     = 160,
                ReadOnly  = true,
                BackColor = Color.LightYellow,
            };
            panelTop.Controls.Add(txtStockName);

            // 행 2: 조회 기간 + 거래소구분
            AddLabel("시작일:", 14, 72, panelTop);
            dtpFrom = new DateTimePicker
            {
                Location = new Point(60, 70),
                Width    = 115,
                Format   = DateTimePickerFormat.Short,
                Value    = DateTime.Today.AddMonths(-3),
            };
            panelTop.Controls.Add(dtpFrom);

            AddLabel("종료일:", 183, 72, panelTop);
            dtpTo = new DateTimePicker
            {
                Location = new Point(229, 70),
                Width    = 115,
                Format   = DateTimePickerFormat.Short,
                Value    = DateTime.Today,
            };
            panelTop.Controls.Add(dtpTo);

            AddLabel("거래소:", 358, 72, panelTop);
            cmbExchange = new ComboBox
            {
                Location      = new Point(400, 69),
                Width         = 90,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            cmbExchange.Items.AddRange(new object[] { "KRX", "NXT", "전체" });
            cmbExchange.SelectedIndex = 0;
            panelTop.Controls.Add(cmbExchange);

            btnCollect = AddButton("데이터 수집", new Point(502, 68), 100, Color.SeaGreen, panelTop);
            btnCollect.Click += BtnCollect_Click;

            btnViewChart = AddButton("그래프 보기", new Point(610, 68), 100, Color.DarkSlateGray, panelTop);
            btnViewChart.Enabled = false;
            btnViewChart.Click  += BtnViewChart_Click;

            btnExportCsv = AddButton("CSV 내보내기", new Point(718, 68), 108, Color.SaddleBrown, panelTop);
            btnExportCsv.Enabled = false;
            btnExportCsv.Click  += BtnExportCsv_Click;

            // ── ProgressBar ───────────────────────────────────────
            progressBar = new ProgressBar
            {
                Dock                  = DockStyle.Top,
                Height                = 5,
                Style                 = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible               = false,
            };

            // ── DataGridView ──────────────────────────────────────
            dgvData = new DataGridView
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
            BuildGridColumns();

            // ── StatusStrip ───────────────────────────────────────
            var statusStrip = new StatusStrip { BackColor = Color.White };
            lblStatus = new ToolStripStatusLabel("준비") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            statusStrip.Items.Add(lblStatus);

            this.Controls.Add(dgvData);
            this.Controls.Add(progressBar);
            this.Controls.Add(panelTop);
            this.Controls.Add(statusStrip);
        }

        private void BuildGridColumns()
        {
            dgvData.Columns.Clear();

            var defs = new (string Header, string Fmt, int Fill, DataGridViewContentAlignment Align)[]
            {
                ("날짜",             "",    70,  DataGridViewContentAlignment.MiddleCenter),
                ("종가(원)",         "N0",  80,  DataGridViewContentAlignment.MiddleRight),
                ("전일대비",         "N0",  75,  DataGridViewContentAlignment.MiddleRight),
                ("전일대비율(%)",    "F2",  85,  DataGridViewContentAlignment.MiddleRight),
                ("거래량(주)",       "N0",  95,  DataGridViewContentAlignment.MiddleRight),
                ("공매도량(주)",     "N0",  95,  DataGridViewContentAlignment.MiddleRight),
                ("공매도비중(%)",    "F2",  85,  DataGridViewContentAlignment.MiddleRight),
                ("공매도거래대금(원)", "N0", 120, DataGridViewContentAlignment.MiddleRight),
                ("평균가(원)",       "N0",  80,  DataGridViewContentAlignment.MiddleRight),
                ("평균가대비",       "N0",  80,  DataGridViewContentAlignment.MiddleRight),
            };

            foreach (var (header, fmt, fill, align) in defs)
            {
                dgvData.Columns.Add(new DataGridViewTextBoxColumn
                {
                    HeaderText       = header,
                    DefaultCellStyle = new DataGridViewCellStyle { Format = fmt, Alignment = align },
                    FillWeight       = fill,
                });
            }
        }

        // ── 이벤트 ───────────────────────────────────────────────────
        private void CheckConnection()
        {
            bool ok = _api.IsConnected();
            lblConnectionStatus.Text      = ok ? "● CYBOS Plus 연결됨" : "● CYBOS Plus 미연결";
            lblConnectionStatus.ForeColor = ok ? Color.Green : Color.Red;
        }

        private void BtnSearchName_Click(object sender, EventArgs e)
        {
            string code = txtStockCode.Text.Trim();
            if (!string.IsNullOrEmpty(code))
                txtStockName.Text = _api.GetStockName(code);
        }

        private void BtnCollect_Click(object sender, EventArgs e)
        {
            string code = txtStockCode.Text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show("종목코드를 입력하세요.", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (dtpFrom.Value > dtpTo.Value)
            {
                MessageBox.Show("시작일이 종료일보다 큽니다.", "알림",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetCollecting(true);

            string fromDate  = dtpFrom.Value.ToString("yyyyMMdd");
            string toDate    = dtpTo.Value.ToString("yyyyMMdd");
            char   exchange  = cmbExchange.SelectedIndex == 0 ? 'K'
                             : cmbExchange.SelectedIndex == 1 ? 'N' : 'A';
            var    progress  = new Progress<string>(msg => lblStatus.Text = msg);

            var thread = new Thread(() =>
            {
                List<ShortSellData> data = null;
                Exception ex = null;
                try
                {
                    data = _api.GetShortSellData(code, fromDate, toDate, exchange, progress);
                }
                catch (Exception err) { ex = err; }

                this.Invoke((Action)(() =>
                {
                    SetCollecting(false);
                    if (ex != null)
                    {
                        MessageBox.Show($"데이터 수집 실패:\n{ex.Message}", "오류",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        lblStatus.Text = "오류 발생";
                        return;
                    }

                    _collectedData = data;
                    PopulateGrid(data);
                    btnViewChart.Enabled = data.Count > 0;
                    btnExportCsv.Enabled = data.Count > 0;
                    lblStatus.Text = $"수집 완료 — {data.Count}건  ({fromDate} ~ {toDate})";

                    if (string.IsNullOrEmpty(txtStockName.Text) || txtStockName.Text == code)
                        txtStockName.Text = _api.GetStockName(code);
                }));
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
        }

        private void BtnViewChart_Click(object sender, EventArgs e)
        {
            if (_collectedData == null || _collectedData.Count == 0) return;
            new ShortSellingViewForm(
                _collectedData,
                txtStockCode.Text.Trim(),
                txtStockName.Text.Trim()).Show(this);
        }

        private void BtnExportCsv_Click(object sender, EventArgs e)
        {
            if (_collectedData == null || _collectedData.Count == 0) return;

            using (var dlg = new SaveFileDialog
            {
                Title    = "CSV 내보내기",
                Filter   = "CSV 파일 (*.csv)|*.csv",
                FileName = $"공매도추이_{txtStockCode.Text.Trim()}_{DateTime.Today:yyyyMMdd}.csv",
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    using (var sw = new System.IO.StreamWriter(
                        dlg.FileName, false, System.Text.Encoding.UTF8))
                    {
                        sw.WriteLine("날짜,종가,전일대비,전일대비율(%),거래량," +
                                     "공매도량,공매도비중(%),공매도거래대금,평균가,평균가대비");
                        foreach (var d in _collectedData)
                            sw.WriteLine($"{d.Date},{d.ClosePrice},{d.PriceChange}," +
                                         $"{d.ChangeRate:F2},{d.Volume},{d.ShortVolume}," +
                                         $"{d.ShortRatio:F2},{d.ShortAmount},{d.AvgPrice},{d.AvgPriceDiff}");
                    }
                    MessageBox.Show("CSV 파일이 저장되었습니다.", "완료",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    lblStatus.Text = $"CSV 저장: {dlg.FileName}";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"저장 실패:\n{ex.Message}", "오류",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ── 헬퍼 ─────────────────────────────────────────────────────
        private void PopulateGrid(List<ShortSellData> data)
        {
            dgvData.Rows.Clear();
            foreach (var d in data)
            {
                int idx = dgvData.Rows.Add(
                    d.Date, d.ClosePrice, d.PriceChange, d.ChangeRate,
                    d.Volume, d.ShortVolume, d.ShortRatio,
                    d.ShortAmount, d.AvgPrice, d.AvgPriceDiff);

                // 공매도비중 5% 이상 행 강조
                if (d.ShortRatio >= 5.0)
                {
                    dgvData.Rows[idx].DefaultCellStyle.BackColor = Color.MistyRose;
                    dgvData.Rows[idx].DefaultCellStyle.ForeColor = Color.DarkRed;
                }
                // 전일대비 색상
                var cell = dgvData.Rows[idx].Cells[2];
                cell.Style.ForeColor = d.PriceChange > 0 ? Color.Crimson
                                     : d.PriceChange < 0 ? Color.DodgerBlue
                                     : Color.Black;
            }
        }

        private void SetCollecting(bool on)
        {
            btnCollect.Enabled   = !on;
            btnViewChart.Enabled = !on;
            progressBar.Visible  = on;
        }

        private static void AddLabel(string text, int x, int y, Control parent) =>
            parent.Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true });

        private static Button AddButton(string text, Point loc, int width, Color back, Control parent)
        {
            var btn = new Button
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
            parent.Controls.Add(btn);
            return btn;
        }
    }
}
