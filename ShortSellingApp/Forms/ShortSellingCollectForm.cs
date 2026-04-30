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
    /// 투자주체별 매매현황 데이터 수집 폼 (CpSysDib.CpSvr7254)
    /// </summary>
    public class ShortSellingCollectForm : Form
    {
        private readonly DaishinApiService _api = new DaishinApiService();
        private List<InvestorTradeData>    _collectedData = new List<InvestorTradeData>();

        // ── 컨트롤 ──────────────────────────────────────────────────
        private TextBox        txtStockCode;
        private TextBox        txtStockName;
        private Button         btnSearchName;
        private DateTimePicker dtpFrom;
        private DateTimePicker dtpTo;
        private ComboBox       cmbTradeType;    // 매매구분
        private ComboBox       cmbDataType;     // 데이터구분
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

        // ────────────────────────────────────────────────────────────
        // UI 구성
        // ────────────────────────────────────────────────────────────
        private void InitializeComponent()
        {
            this.Text          = "투자주체별 매매현황 수집 - 대신플러스 (CpSvr7254)";
            this.Size          = new Size(1200, 720);
            this.MinimumSize   = new Size(1000, 600);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font          = new Font("맑은 고딕", 9f);
            this.BackColor     = Color.WhiteSmoke;

            // ── 상단 패널 ─────────────────────────────────────────
            var panelTop = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 122,
                BackColor = Color.White,
                Padding   = new Padding(12),
            };
            panelTop.Paint += (s, e) =>
                e.Graphics.DrawLine(Pens.LightGray, 0, panelTop.Height - 1,
                                    panelTop.Width, panelTop.Height - 1);

            // 연결 상태
            lblConnectionStatus = new Label
            {
                AutoSize  = true,
                Font      = new Font("맑은 고딕", 8.5f, FontStyle.Bold),
                Location  = new Point(14, 10),
            };

            // ── 행 1: 종목 ─────────────────────────────────────
            MakeLabel("종목코드:", 14, 38, panelTop);
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

            btnSearchName = MakeButton("종목명 조회", new Point(175, 35), 82, Color.SteelBlue, panelTop);
            btnSearchName.Click += BtnSearchName_Click;

            txtStockName = new TextBox
            {
                Location  = new Point(264, 36),
                Width     = 160,
                ReadOnly  = true,
                BackColor = Color.LightYellow,
            };
            panelTop.Controls.Add(txtStockName);

            // ── 행 2: 조회 기간 + 옵션 ────────────────────────
            MakeLabel("시작일:", 14, 72, panelTop);
            dtpFrom = new DateTimePicker
            {
                Location = new Point(60, 70),
                Width    = 115,
                Format   = DateTimePickerFormat.Short,
                Value    = DateTime.Today.AddMonths(-3),
            };
            panelTop.Controls.Add(dtpFrom);

            MakeLabel("종료일:", 183, 72, panelTop);
            dtpTo = new DateTimePicker
            {
                Location = new Point(229, 70),
                Width    = 115,
                Format   = DateTimePickerFormat.Short,
                Value    = DateTime.Today,
            };
            panelTop.Controls.Add(dtpTo);

            MakeLabel("매매구분:", 358, 72, panelTop);
            cmbTradeType = new ComboBox
            {
                Location      = new Point(420, 69),
                Width         = 100,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            cmbTradeType.Items.AddRange(new object[] { "순매수", "매매비중" });
            cmbTradeType.SelectedIndex = 0;
            panelTop.Controls.Add(cmbTradeType);

            MakeLabel("데이터구분:", 530, 72, panelTop);
            cmbDataType = new ComboBox
            {
                Location      = new Point(608, 69),
                Width         = 130,
                DropDownStyle = ComboBoxStyle.DropDownList,
            };
            cmbDataType.Items.AddRange(new object[] { "순매수수량(주)", "추정금액(백만원)" });
            cmbDataType.SelectedIndex = 0;
            panelTop.Controls.Add(cmbDataType);

            // ── 버튼 ───────────────────────────────────────────
            btnCollect = MakeButton("데이터 수집", new Point(756, 68), 100, Color.SeaGreen, panelTop);
            btnCollect.Click += BtnCollect_Click;

            btnViewChart = MakeButton("그래프 보기", new Point(864, 68), 100, Color.DarkSlateGray, panelTop);
            btnViewChart.Enabled = false;
            btnViewChart.Click   += BtnViewChart_Click;

            btnExportCsv = MakeButton("CSV 내보내기", new Point(972, 68), 108, Color.SaddleBrown, panelTop);
            btnExportCsv.Enabled = false;
            btnExportCsv.Click   += BtnExportCsv_Click;

            // ── ProgressBar ───────────────────────────────────
            progressBar = new ProgressBar
            {
                Dock                  = DockStyle.Top,
                Height                = 5,
                Style                 = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible               = false,
            };

            // ── DataGridView ──────────────────────────────────
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
                {
                    BackColor = Color.AliceBlue,
                },
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

            // ── StatusStrip ───────────────────────────────────
            var statusStrip = new StatusStrip { BackColor = Color.White };
            lblStatus = new ToolStripStatusLabel("준비") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            statusStrip.Items.Add(lblStatus);

            // ── 레이아웃 ──────────────────────────────────────
            this.Controls.Add(dgvData);
            this.Controls.Add(progressBar);
            this.Controls.Add(panelTop);
            this.Controls.Add(statusStrip);
        }

        private void BuildGridColumns()
        {
            dgvData.Columns.Clear();

            var rightFmt = new Func<string, int, DataGridViewTextBoxColumn>((hdr, fill) =>
                new DataGridViewTextBoxColumn
                {
                    HeaderText = hdr,
                    DefaultCellStyle = new DataGridViewCellStyle
                    {
                        Alignment = DataGridViewContentAlignment.MiddleRight,
                        Format    = "N0",
                    },
                    FillWeight = fill,
                });

            dgvData.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "날짜",
                DefaultCellStyle = new DataGridViewCellStyle
                { Alignment = DataGridViewContentAlignment.MiddleCenter },
                FillWeight = 70,
            });
            dgvData.Columns.Add(rightFmt("개인",       80));
            dgvData.Columns.Add(rightFmt("외국인",     80));
            dgvData.Columns.Add(rightFmt("기관계",     80));
            dgvData.Columns.Add(rightFmt("금융투자",   80));
            dgvData.Columns.Add(rightFmt("보험",       70));
            dgvData.Columns.Add(rightFmt("투신",       70));
            dgvData.Columns.Add(rightFmt("은행",       70));
            dgvData.Columns.Add(rightFmt("기타금융",   75));
            dgvData.Columns.Add(rightFmt("연기금",     75));
            dgvData.Columns.Add(rightFmt("기타법인",   75));
            dgvData.Columns.Add(rightFmt("사모펀드",   75));
            dgvData.Columns.Add(rightFmt("정부/지자체", 85));
        }

        // ────────────────────────────────────────────────────────────
        // 이벤트
        // ────────────────────────────────────────────────────────────
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

            string fromDate    = dtpFrom.Value.ToString("yyyyMMdd");
            string toDate      = dtpTo.Value.ToString("yyyyMMdd");
            char   tradeType   = cmbTradeType.SelectedIndex == 0 ? '0' : '1';
            char   dataType    = cmbDataType.SelectedIndex == 0  ? '1' : '2';
            var    progress    = new Progress<string>(msg => SetStatus(msg));

            // STA 스레드 필수 (CYBOS Plus COM)
            var thread = new Thread(() =>
            {
                List<InvestorTradeData> data = null;
                Exception ex = null;
                try
                {
                    data = _api.GetInvestorTradeData(
                        code, fromDate, toDate, tradeType, 0, dataType, progress);
                }
                catch (Exception err) { ex = err; }

                this.Invoke((Action)(() =>
                {
                    SetCollecting(false);
                    if (ex != null)
                    {
                        MessageBox.Show($"데이터 수집 실패:\n{ex.Message}", "오류",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
                        SetStatus("오류 발생");
                        return;
                    }

                    _collectedData = data;
                    PopulateGrid(data);
                    btnViewChart.Enabled = data.Count > 0;
                    btnExportCsv.Enabled = data.Count > 0;
                    SetStatus($"수집 완료 — {data.Count}건 ({fromDate} ~ {toDate})  " +
                              $"단위: {(dataType == '1' ? "순매수수량(주)" : "추정금액(백만원)")}");

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
                FileName = $"투자주체_{txtStockCode.Text.Trim()}_{DateTime.Today:yyyyMMdd}.csv",
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    using (var sw = new System.IO.StreamWriter(
                        dlg.FileName, false, System.Text.Encoding.UTF8))
                    {
                        string unit = _collectedData.Count > 0 ? _collectedData[0].Unit : "";
                        sw.WriteLine($"날짜,개인,외국인,기관계,금융투자,보험,투신,은행," +
                                     $"기타금융,연기금,기타법인,사모펀드,정부/지자체  [{unit}]");
                        foreach (var d in _collectedData)
                            sw.WriteLine($"{d.Date},{d.Individual},{d.Foreigner}," +
                                         $"{d.Institution},{d.FinancialInvest},{d.Insurance}," +
                                         $"{d.InvestTrust},{d.Bank},{d.OtherFinancial}," +
                                         $"{d.PensionFund},{d.OtherCorp},{d.PrivateEquity},{d.Government}");
                    }
                    MessageBox.Show("CSV 파일이 저장되었습니다.", "완료",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SetStatus($"CSV 저장: {dlg.FileName}");
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"저장 실패:\n{ex.Message}", "오류",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // ────────────────────────────────────────────────────────────
        // 헬퍼
        // ────────────────────────────────────────────────────────────
        private void PopulateGrid(List<InvestorTradeData> data)
        {
            dgvData.Rows.Clear();
            foreach (var d in data)
            {
                dgvData.Rows.Add(d.Date,
                    d.Individual, d.Foreigner, d.Institution,
                    d.FinancialInvest, d.Insurance, d.InvestTrust, d.Bank,
                    d.OtherFinancial, d.PensionFund, d.OtherCorp,
                    d.PrivateEquity, d.Government);
            }
        }

        private void SetCollecting(bool on)
        {
            btnCollect.Enabled   = !on;
            btnViewChart.Enabled = !on;
            progressBar.Visible  = on;
        }

        private void SetStatus(string msg) => lblStatus.Text = msg;

        private static void MakeLabel(string text, int x, int y, Control parent)
        {
            parent.Controls.Add(new Label { Text = text, Location = new Point(x, y), AutoSize = true });
        }

        private static Button MakeButton(string text, Point loc, int width, Color back, Control parent)
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
