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
    /// 종목별 공매도 현황 데이터 수집 폼
    /// </summary>
    public class ShortSellingCollectForm : Form
    {
        // ── 서비스 ──────────────────────────────────────────────────
        private readonly DaishinApiService _api = new DaishinApiService();

        // ── 수집 결과 ────────────────────────────────────────────────
        private List<ShortSellingData> _collectedData = new List<ShortSellingData>();

        // ── 컨트롤 ──────────────────────────────────────────────────
        private TextBox      txtStockCode;
        private TextBox      txtStockName;
        private Button       btnSearchName;
        private DateTimePicker dtpFrom;
        private DateTimePicker dtpTo;
        private Button       btnCollect;
        private Button       btnViewChart;
        private Button       btnExportCsv;
        private DataGridView dgvData;
        private StatusStrip  statusStrip;
        private ToolStripStatusLabel lblStatus;
        private ProgressBar  progressBar;
        private Label        lblConnectionStatus;

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
            this.Text            = "공매도 현황 데이터 수집 - 대신플러스";
            this.Size            = new Size(1000, 700);
            this.MinimumSize     = new Size(900, 600);
            this.StartPosition   = FormStartPosition.CenterScreen;
            this.Font            = new Font("맑은 고딕", 9f);
            this.BackColor       = Color.WhiteSmoke;

            // ── 상단 검색 패널 ─────────────────────────────────────
            var panelSearch = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 110,
                BackColor = Color.White,
                Padding   = new Padding(10),
            };
            panelSearch.Paint += (s, e) =>
            {
                e.Graphics.DrawLine(Pens.LightGray, 0, panelSearch.Height - 1,
                                    panelSearch.Width, panelSearch.Height - 1);
            };

            // 연결 상태 레이블
            lblConnectionStatus = new Label
            {
                AutoSize = true,
                Font     = new Font("맑은 고딕", 8.5f, FontStyle.Bold),
                Location = new Point(14, 12),
            };

            // 종목코드
            var lblCode = MakeLabel("종목코드:", 14, 40);
            txtStockCode = new TextBox
            {
                Location     = new Point(80, 38),
                Width        = 90,
                CharacterCasing = CharacterCasing.Upper,
                MaxLength    = 8,
            };
            txtStockCode.KeyDown += (s, e) =>
            {
                if (e.KeyCode == Keys.Enter) btnSearchName.PerformClick();
            };

            btnSearchName = MakeButton("종목명 조회", new Point(178, 37), 80, Color.SteelBlue);
            btnSearchName.Click += BtnSearchName_Click;

            txtStockName = new TextBox
            {
                Location  = new Point(266, 38),
                Width     = 150,
                ReadOnly  = true,
                BackColor = Color.LightYellow,
            };

            // 조회 기간
            var lblFrom = MakeLabel("시작일:", 14, 72);
            dtpFrom = new DateTimePicker
            {
                Location = new Point(60, 70),
                Width    = 120,
                Format   = DateTimePickerFormat.Short,
                Value    = DateTime.Today.AddMonths(-3),
            };

            var lblTo = MakeLabel("종료일:", 196, 72);
            dtpTo = new DateTimePicker
            {
                Location = new Point(242, 70),
                Width    = 120,
                Format   = DateTimePickerFormat.Short,
                Value    = DateTime.Today,
            };

            btnCollect = MakeButton("데이터 수집", new Point(380, 68), 100, Color.SeaGreen);
            btnCollect.Height = 28;
            btnCollect.Click += BtnCollect_Click;

            btnViewChart = MakeButton("그래프 보기", new Point(488, 68), 100, Color.DarkSlateGray);
            btnViewChart.Height = 28;
            btnViewChart.Enabled = false;
            btnViewChart.Click += BtnViewChart_Click;

            btnExportCsv = MakeButton("CSV 내보내기", new Point(596, 68), 100, Color.SaddleBrown);
            btnExportCsv.Height = 28;
            btnExportCsv.Enabled = false;
            btnExportCsv.Click += BtnExportCsv_Click;

            panelSearch.Controls.AddRange(new Control[]
            {
                lblConnectionStatus,
                lblCode, txtStockCode, btnSearchName, txtStockName,
                lblFrom, dtpFrom, lblTo, dtpTo,
                btnCollect, btnViewChart, btnExportCsv,
            });

            // ── ProgressBar ───────────────────────────────────────
            progressBar = new ProgressBar
            {
                Dock    = DockStyle.Top,
                Height  = 6,
                Minimum = 0,
                Maximum = 100,
                Style   = ProgressBarStyle.Marquee,
                MarqueeAnimationSpeed = 30,
                Visible = false,
            };

            // ── DataGridView ──────────────────────────────────────
            dgvData = new DataGridView
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
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
                {
                    BackColor = Color.AliceBlue,
                },
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
                {
                    Font      = new Font("맑은 고딕", 9f, FontStyle.Bold),
                    BackColor = Color.FromArgb(50, 100, 150),
                    ForeColor = Color.White,
                    Alignment = DataGridViewContentAlignment.MiddleCenter,
                },
                EnableHeadersVisualStyles = false,
            };
            BuildGridColumns();

            // ── StatusStrip ───────────────────────────────────────
            statusStrip = new StatusStrip { BackColor = Color.White };
            lblStatus   = new ToolStripStatusLabel("준비") { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            statusStrip.Items.Add(lblStatus);

            // ── 레이아웃 조립 ──────────────────────────────────────
            this.Controls.Add(dgvData);
            this.Controls.Add(progressBar);
            this.Controls.Add(panelSearch);
            this.Controls.Add(statusStrip);
        }

        private void BuildGridColumns()
        {
            dgvData.Columns.Clear();
            dgvData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "Date", HeaderText = "날짜",
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter },
                FillWeight = 80,
            });
            dgvData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ShortVolume", HeaderText = "공매도거래량(주)",
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N0",
                },
                FillWeight = 120,
            });
            dgvData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ShortAmount", HeaderText = "공매도거래대금(백만원)",
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N0",
                },
                FillWeight = 140,
            });
            dgvData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ShortRatio", HeaderText = "공매도비중(%)",
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "F2",
                },
                FillWeight = 100,
            });
            dgvData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "ClosePrice", HeaderText = "종가(원)",
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N0",
                },
                FillWeight = 90,
            });
            dgvData.Columns.Add(new DataGridViewTextBoxColumn
            {
                Name = "TotalVolume", HeaderText = "총거래량(주)",
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N0",
                },
                FillWeight = 110,
            });
        }

        // ────────────────────────────────────────────────────────────
        // 이벤트 핸들러
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
            if (string.IsNullOrEmpty(code)) return;
            txtStockName.Text = _api.GetStockName(code);
        }

        private void BtnCollect_Click(object sender, EventArgs e)
        {
            string code = txtStockCode.Text.Trim();
            if (string.IsNullOrEmpty(code))
            {
                MessageBox.Show("종목코드를 입력하세요.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (dtpFrom.Value > dtpTo.Value)
            {
                MessageBox.Show("시작일이 종료일보다 큽니다.", "알림", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetCollecting(true);

            string fromDate = dtpFrom.Value.ToString("yyyyMMdd");
            string toDate   = dtpTo.Value.ToString("yyyyMMdd");

            var progress = new Progress<string>(msg => SetStatus(msg));

            // STA 스레드에서 COM 호출 (CYBOS Plus는 STA 필수)
            var thread = new Thread(() =>
            {
                List<ShortSellingData> data = null;
                Exception ex = null;
                try
                {
                    data = _api.GetShortSellingData(code, fromDate, toDate, 2000, progress);
                }
                catch (Exception err)
                {
                    ex = err;
                }

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
                    btnViewChart.Enabled  = data.Count > 0;
                    btnExportCsv.Enabled  = data.Count > 0;
                    SetStatus($"수집 완료 — {data.Count}건 ({fromDate} ~ {toDate})");

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
            var form = new ShortSellingViewForm(_collectedData,
                txtStockCode.Text.Trim(), txtStockName.Text.Trim());
            form.Show(this);
        }

        private void BtnExportCsv_Click(object sender, EventArgs e)
        {
            if (_collectedData == null || _collectedData.Count == 0) return;

            using (var dlg = new SaveFileDialog
            {
                Title    = "CSV 내보내기",
                Filter   = "CSV 파일 (*.csv)|*.csv",
                FileName = $"공매도_{txtStockCode.Text.Trim()}_{DateTime.Today:yyyyMMdd}.csv",
            })
            {
                if (dlg.ShowDialog() != DialogResult.OK) return;
                try
                {
                    using (var sw = new System.IO.StreamWriter(dlg.FileName,
                        false, System.Text.Encoding.UTF8))
                    {
                        sw.WriteLine("날짜,공매도거래량(주),공매도거래대금(원),공매도비중(%),종가(원),총거래량(주)");
                        foreach (var d in _collectedData)
                        {
                            sw.WriteLine($"{d.Date},{d.ShortVolume},{d.ShortAmount}," +
                                         $"{d.ShortRatio:F2},{d.ClosePrice},{d.TotalVolume}");
                        }
                    }
                    SetStatus($"CSV 저장 완료: {dlg.FileName}");
                    MessageBox.Show("CSV 파일이 저장되었습니다.", "완료",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
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
        private void PopulateGrid(List<ShortSellingData> data)
        {
            dgvData.Rows.Clear();
            foreach (var d in data)
            {
                dgvData.Rows.Add(
                    d.Date,
                    d.ShortVolume,
                    d.ShortAmount / 1_000_000,  // 백만원 단위
                    d.ShortRatio,
                    d.ClosePrice,
                    d.TotalVolume);
            }
        }

        private void SetCollecting(bool collecting)
        {
            btnCollect.Enabled   = !collecting;
            btnViewChart.Enabled = !collecting;
            progressBar.Visible  = collecting;
            if (collecting) progressBar.Style = ProgressBarStyle.Marquee;
        }

        private void SetStatus(string msg) => lblStatus.Text = msg;

        private static Label MakeLabel(string text, int x, int y) =>
            new Label { Text = text, Location = new Point(x, y), AutoSize = true };

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
