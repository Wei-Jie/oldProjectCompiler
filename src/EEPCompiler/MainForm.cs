using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace EEPCompiler
{
    /// <summary>
    /// 繁體中文桌面視窗介面 (GUI 模式)，支援拖放專案、檢視 EEP 參考組件、一鍵修復規則 2~4 與雙核心編譯
    /// </summary>
    public class MainForm : Form
    {
        private TextBox _txtInputPath;
        private TextBox _txtEepRoot;
        private ComboBox _cmbEngine;
        private ComboBox _cmbTargetClr;
        private CheckBox _chkAutoRepair;

        private Button _btnAnalyze;
        private Button _btnRepair;
        private Button _btnBuild;
        private Button _btnCopyClaude;

        private TabControl _tabMain;
        private ListView _lvReferences;
        private TextBox _txtBuildLog;
        private ListView _lvSyntaxIssues;
        private TextBox _txtClaudeReport;
        private Label _lblStatus;

        private readonly BuildEngine _engine = new BuildEngine();
        private List<ProjectBuildResult> _lastResults = new List<ProjectBuildResult>();

        public MainForm()
        {
            InitializeGuiComponents();
            LoadDefaultPathsIfExist();
        }

        private void InitializeGuiComponents()
        {
            this.Text = "Infolight EEP / Visual Studio 2005 可攜式編譯與自動修復工具 (CLI + GUI 雙棲版)";
            this.Size = new Size(1060, 740);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Regular);
            this.AllowDrop = true;
            this.DragEnter += OnFormDragEnter;
            this.DragDrop += OnFormDragDrop;

            // 頂部控制面板
            Panel topPanel = new Panel();
            topPanel.Dock = DockStyle.Top;
            topPanel.Height = 165;
            topPanel.Padding = new Padding(12);
            topPanel.BackColor = Color.FromArgb(245, 247, 250);

            Label lblInput = new Label { Text = "專案 / 方案路徑 (.sln / .csproj / 資料夾):", Location = new Point(15, 16), AutoSize = true };
            _txtInputPath = new TextBox { Location = new Point(280, 13), Width = 540, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            Button btnBrowseFile = new Button { Text = "選取專案檔...", Location = new Point(830, 11), Width = 100, Height = 27, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnBrowseFile.Click += OnBrowseFileClick;

            Button btnBrowseDir = new Button { Text = "選取資料夾...", Location = new Point(936, 11), Width = 100, Height = 27, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnBrowseDir.Click += OnBrowseDirClick;

            Label lblEep = new Label { Text = "訊光 EEP 根目錄 (選填，搜尋 DLL 用):", Location = new Point(15, 50), AutoSize = true };
            _txtEepRoot = new TextBox { Location = new Point(280, 47), Width = 650, Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };

            Button btnBrowseEep = new Button { Text = "瀏覽 EEP...", Location = new Point(936, 45), Width = 100, Height = 27, Anchor = AnchorStyles.Top | AnchorStyles.Right };
            btnBrowseEep.Click += OnBrowseEepClick;

            Label lblEngine = new Label { Text = "編譯引擎模式:", Location = new Point(15, 85), AutoSize = true };
            _cmbEngine = new ComboBox { Location = new Point(115, 82), Width = 270, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbEngine.Items.Add("Auto (優先 MSBuild，缺 SDK 自動切換內建引擎)");
            _cmbEngine.Items.Add("MSBuild (規則 1：強制呼叫本機 MSBuild.exe)");
            _cmbEngine.Items.Add("Direct CSC (免 SDK 內建 ResX + CSC 跨編譯引擎)");
            _cmbEngine.SelectedIndex = 0;

            Label lblClr = new Label { Text = "目標 CLR 版本:", Location = new Point(400, 85), AutoSize = true };
            _cmbTargetClr = new ComboBox { Location = new Point(500, 82), Width = 210, DropDownStyle = ComboBoxStyle.DropDownList };
            _cmbTargetClr.Items.Add("Auto (自動依 Srvtools.dll 偵測)");
            _cmbTargetClr.Items.Add(".NET 2.0 (CLR v2.0.50727 - VS2005)");
            _cmbTargetClr.Items.Add(".NET 4.0 (CLR v4.0.30319)");
            _cmbTargetClr.SelectedIndex = 0;

            _chkAutoRepair = new CheckBox
            {
                Text = "編譯前自動修復 (規則 2~4: 修正 HintPath、清空 licenses.licx、防堵 MSB3073)",
                Location = new Point(725, 84),
                AutoSize = true,
                Checked = true
            };

            // 操作按鈕列
            _btnAnalyze = new Button
            {
                Text = "1. 診斷專案與參考",
                Location = new Point(15, 120),
                Width = 170,
                Height = 34,
                BackColor = Color.White
            };
            _btnAnalyze.Click += OnAnalyzeClick;

            _btnRepair = new Button
            {
                Text = "2. 僅執行自動修復 (規則 2~4)",
                Location = new Point(195, 120),
                Width = 210,
                Height = 34,
                BackColor = Color.FromArgb(255, 248, 225)
            };
            _btnRepair.Click += OnRepairClick;

            _btnBuild = new Button
            {
                Text = "3. 一鍵修復並開始編譯 (Build)",
                Location = new Point(415, 120),
                Width = 250,
                Height = 34,
                BackColor = Color.FromArgb(40, 167, 69),
                ForeColor = Color.White,
                Font = new Font("Microsoft JhengHei UI", 9.5f, FontStyle.Bold)
            };
            _btnBuild.Click += OnBuildClick;

            _btnCopyClaude = new Button
            {
                Text = "複製 Claude Code 除錯報告",
                Location = new Point(675, 120),
                Width = 220,
                Height = 34,
                BackColor = Color.FromArgb(232, 240, 254)
            };
            _btnCopyClaude.Click += OnCopyClaudeClick;

            topPanel.Controls.AddRange(new Control[]
            {
                lblInput, _txtInputPath, btnBrowseFile, btnBrowseDir,
                lblEep, _txtEepRoot, btnBrowseEep,
                lblEngine, _cmbEngine, lblClr, _cmbTargetClr, _chkAutoRepair,
                _btnAnalyze, _btnRepair, _btnBuild, _btnCopyClaude
            });

            // 底部狀態列
            Panel bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 30, BackColor = Color.FromArgb(235, 238, 242) };
            _lblStatus = new Label
            {
                Text = "就緒。可直接將 .sln、.csproj 或專案資料夾拖放至此視窗中。",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            bottomPanel.Controls.Add(_lblStatus);

            // 中央分頁區
            _tabMain = new TabControl { Dock = DockStyle.Fill };

            TabPage tabLog = new TabPage("編譯輸出與診斷日誌");
            _txtBuildLog = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new Font("Consolas", 10f),
                BackColor = Color.FromArgb(30, 30, 30),
                ForeColor = Color.FromArgb(220, 220, 220)
            };
            tabLog.Controls.Add(_txtBuildLog);

            TabPage tabRefs = new TabPage("EEP 參考組件狀態 (規則 2)");
            _lvReferences = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _lvReferences.Columns.Add("所屬專案", 130);
            _lvReferences.Columns.Add("組件名稱 (Include)", 160);
            _lvReferences.Columns.Add("類別", 90);
            _lvReferences.Columns.Add("解析狀態", 95);
            _lvReferences.Columns.Add("原 .csproj HintPath", 230);
            _lvReferences.Columns.Add("實際解析路徑 (ResolvedPath)", 320);
            tabRefs.Controls.Add(_lvReferences);

            TabPage tabSyntax = new TabPage("C# 語法降級與 Designer 預檢 (規則 5)");
            _lvSyntaxIssues = new ListView
            {
                Dock = DockStyle.Fill,
                View = View.Details,
                FullRowSelect = true,
                GridLines = true
            };
            _lvSyntaxIssues.Columns.Add("規則代碼", 130);
            _lvSyntaxIssues.Columns.Add("檔案名稱", 180);
            _lvSyntaxIssues.Columns.Add("行號", 60);
            _lvSyntaxIssues.Columns.Add("問題說明", 300);
            _lvSyntaxIssues.Columns.Add("AI 降級修改建議", 340);
            tabSyntax.Controls.Add(_lvSyntaxIssues);

            TabPage tabClaude = new TabPage("Claude Code 專屬報告 (--claude)");
            _txtClaudeReport = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9.5f)
            };
            tabClaude.Controls.Add(_txtClaudeReport);

            _tabMain.TabPages.Add(tabLog);
            _tabMain.TabPages.Add(tabRefs);
            _tabMain.TabPages.Add(tabSyntax);
            _tabMain.TabPages.Add(tabClaude);

            this.Controls.Add(_tabMain);
            this.Controls.Add(topPanel);
            this.Controls.Add(bottomPanel);
        }

        private void LoadDefaultPathsIfExist()
        {
            string sampleSln = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\samples\EEPSampleSolution.sln");
            if (File.Exists(sampleSln))
            {
                _txtInputPath.Text = Path.GetFullPath(sampleSln);
            }
            AppendLog("=== Infolight EEP / VS2005 可攜式編譯與修復工具已啟動 ===");
            List<string> msbuilds = ReferenceResolver.FindAvailableMSBuilds();
            AppendLog("本機偵測到的 MSBuild.exe 數量: " + msbuilds.Count);
            foreach (string m in msbuilds) AppendLog("  - " + m);
            AppendLog("內建免 SDK 原生 .resx 資源編譯器與 Direct CSC 跨編譯引擎已就緒。\r\n");
        }

        private CompilerOptions GatherGuiOptions()
        {
            CompilerOptions opt = new CompilerOptions();
            opt.InputPath = _txtInputPath.Text.Trim();
            opt.EepRootPath = _txtEepRoot.Text.Trim();
            opt.AutoRepair = _chkAutoRepair.Checked;

            if (_cmbEngine.SelectedIndex == 1) opt.Engine = EngineMode.MSBuild;
            else if (_cmbEngine.SelectedIndex == 2) opt.Engine = EngineMode.Direct;
            else opt.Engine = EngineMode.Auto;

            if (_cmbTargetClr.SelectedIndex == 1) opt.TargetClr = TargetClrVersion.Net20;
            else if (_cmbTargetClr.SelectedIndex == 2) opt.TargetClr = TargetClrVersion.Net40;
            else opt.TargetClr = TargetClrVersion.Auto;

            return opt;
        }

        private void OnBrowseFileClick(object sender, EventArgs e)
        {
            using (OpenFileDialog ofd = new OpenFileDialog())
            {
                ofd.Filter = "Visual Studio 方案或專案 (*.sln;*.csproj)|*.sln;*.csproj|所有檔案 (*.*)|*.*";
                if (ofd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtInputPath.Text = ofd.FileName;
                }
            }
        }

        private void OnBrowseDirClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "請選擇包含 EEP 專案 (.csproj 或 .cs) 的資料夾";
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtInputPath.Text = fbd.SelectedPath;
                }
            }
        }

        private void OnBrowseEepClick(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "請選擇訊光 EEP 安裝根目錄 (包含 EEPNetServer / EEPNetClient)";
                if (fbd.ShowDialog(this) == DialogResult.OK)
                {
                    _txtEepRoot.Text = fbd.SelectedPath;
                }
            }
        }

        private void OnFormDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
        }

        private void OnFormDragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                _txtInputPath.Text = files[0];
                OnAnalyzeClick(sender, EventArgs.Empty);
            }
        }

        private void OnAnalyzeClick(object sender, EventArgs e)
        {
            CompilerOptions opt = GatherGuiOptions();
            if (string.IsNullOrEmpty(opt.InputPath))
            {
                MessageBox.Show(this, "請先選擇專案檔 (.sln / .csproj) 或資料夾！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _lvReferences.Items.Clear();
            _lvSyntaxIssues.Items.Clear();
            _txtBuildLog.Clear();

            List<string> projPaths = _engine.Projects.DiscoverProjects(opt.InputPath);
            if (projPaths.Count == 0)
            {
                AppendLog("[錯誤] 在指定路徑找不到任何專案: " + opt.InputPath);
                return;
            }

            int unresolvedCount = 0;
            int syntaxCount = 0;

            foreach (string p in projPaths)
            {
                EepProjectInfo info = _engine.Projects.LoadProject(p, opt);
                AppendLog(string.Format("已載入專案: {0} | 角色: {1} | 輸出類型: {2} | OutputPath: {3}",
                    info.ProjectName, info.EepRole, info.OutputType, info.OutputPath));

                foreach (ReferenceItem r in info.References)
                {
                    ListViewItem lvi = new ListViewItem(info.ProjectName);
                    lvi.SubItems.Add(r.ShortName);
                    lvi.SubItems.Add(r.IsEepAssembly ? "訊光 EEP" : (r.IsSystemAssembly ? "系統組件" : "外部組件"));
                    lvi.SubItems.Add(r.IsResolved ? "✔ 已找到" : "✘ 遺失 (CS0246)");
                    lvi.SubItems.Add(r.OriginalHintPath);
                    lvi.SubItems.Add(r.ResolvedPath);
                    if (!r.IsResolved)
                    {
                        lvi.BackColor = Color.FromArgb(255, 230, 230);
                        unresolvedCount++;
                    }
                    else if (r.IsEepAssembly)
                    {
                        lvi.BackColor = Color.FromArgb(230, 255, 235);
                    }
                    _lvReferences.Items.Add(lvi);
                }

                List<SyntaxIssue> issues = _engine.Analyzer.AnalyzeProject(info, true);
                syntaxCount += issues.Count;
                foreach (SyntaxIssue si in issues)
                {
                    ListViewItem sItem = new ListViewItem(si.RuleCode);
                    sItem.SubItems.Add(Path.GetFileName(si.FilePath));
                    sItem.SubItems.Add(si.LineNumber.ToString());
                    sItem.SubItems.Add(si.Message);
                    sItem.SubItems.Add(si.Suggestion);
                    sItem.BackColor = si.IsError ? Color.FromArgb(255, 235, 230) : Color.FromArgb(255, 250, 225);
                    _lvSyntaxIssues.Items.Add(sItem);
                }
            }

            _lblStatus.Text = string.Format("診斷完成：共 {0} 個專案，{1} 個遺失參考，{2} 個語法相容性提醒。",
                projPaths.Count, unresolvedCount, syntaxCount);
            _tabMain.SelectedIndex = 1;
        }

        private void OnRepairClick(object sender, EventArgs e)
        {
            CompilerOptions opt = GatherGuiOptions();
            if (string.IsNullOrEmpty(opt.InputPath))
            {
                MessageBox.Show(this, "請先選擇專案檔 (.sln / .csproj) 或資料夾！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _txtBuildLog.Clear();
            List<string> projPaths = _engine.Projects.DiscoverProjects(opt.InputPath);
            int totalRepairs = 0;

            foreach (string p in projPaths)
            {
                EepProjectInfo info = _engine.Projects.LoadProject(p, opt);
                List<RepairRecord> records = _engine.Projects.RepairProject(info, opt);
                totalRepairs += records.Count;
                AppendLog(string.Format("=== 修復專案: {0} ===", info.ProjectName));
                if (records.Count == 0)
                {
                    AppendLog("  所有設定皆已符合規則 2~4，無須修改。");
                }
                else
                {
                    foreach (RepairRecord r in records)
                    {
                        AppendLog(string.Format("  [{0}] {1}", r.RuleId, r.Description));
                    }
                }
            }

            _lblStatus.Text = string.Format("自動修復完成，共執行 {0} 項規則修復。", totalRepairs);
            _tabMain.SelectedIndex = 0;
        }

        private void OnBuildClick(object sender, EventArgs e)
        {
            CompilerOptions opt = GatherGuiOptions();
            if (string.IsNullOrEmpty(opt.InputPath))
            {
                MessageBox.Show(this, "請先選擇專案檔 (.sln / .csproj) 或資料夾！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _txtBuildLog.Clear();
            _lvReferences.Items.Clear();
            _lvSyntaxIssues.Items.Clear();
            _lastResults.Clear();

            List<string> projPaths = _engine.Projects.DiscoverProjects(opt.InputPath);
            if (projPaths.Count == 0)
            {
                AppendLog("[錯誤] 在指定路徑找不到任何專案: " + opt.InputPath);
                return;
            }

            bool allOk = true;
            foreach (string p in projPaths)
            {
                ProjectBuildResult res = _engine.ExecuteBuild(p, opt);
                _lastResults.Add(res);
                if (!res.Success) allOk = false;

                foreach (string line in res.Logs)
                {
                    AppendLog(line);
                }

                if (res.Diagnostics.Count > 0)
                {
                    AppendLog("--- 編譯診斷明細 ---");
                    foreach (CompilerDiagnostic d in res.Diagnostics)
                    {
                        AppendLog(string.Format("[{0} {1}] {2}({3},{4}): {5}",
                            d.Severity, d.Code, d.FilePath, d.Line, d.Column, d.Message));
                        if (!string.IsNullOrEmpty(d.AiHint))
                            AppendLog("  -> AI 修復提示: " + d.AiHint);
                    }
                }

                if (res.Project != null)
                {
                    foreach (ReferenceItem r in res.Project.References)
                    {
                        ListViewItem lvi = new ListViewItem(res.Project.ProjectName);
                        lvi.SubItems.Add(r.ShortName);
                        lvi.SubItems.Add(r.IsEepAssembly ? "訊光 EEP" : (r.IsSystemAssembly ? "系統組件" : "外部組件"));
                        lvi.SubItems.Add(r.IsResolved ? "✔ 已找到" : "✘ 遺失");
                        lvi.SubItems.Add(r.OriginalHintPath);
                        lvi.SubItems.Add(r.ResolvedPath);
                        _lvReferences.Items.Add(lvi);
                    }
                }

                foreach (SyntaxIssue si in res.SyntaxIssues)
                {
                    ListViewItem sItem = new ListViewItem(si.RuleCode);
                    sItem.SubItems.Add(Path.GetFileName(si.FilePath));
                    sItem.SubItems.Add(si.LineNumber.ToString());
                    sItem.SubItems.Add(si.Message);
                    sItem.SubItems.Add(si.Suggestion);
                    _lvSyntaxIssues.Items.Add(sItem);
                }

                AppendLog("");
            }

            _txtClaudeReport.Text = CliRunner.FormatClaudeReport(_lastResults);
            _tabMain.SelectedIndex = 0;

            if (allOk)
            {
                _lblStatus.Text = "✔ 編譯成功 (0 Error(s))！所有組件已輸出。";
            }
            else
            {
                _lblStatus.Text = "✘ 編譯失敗，請檢視編譯日誌或點擊「複製 Claude Code 除錯報告」交由 AI 自動修復。";
            }
        }

        private void OnCopyClaudeClick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(_txtClaudeReport.Text))
            {
                MessageBox.Show(this, "請先執行「開始編譯 (Build)」以產生診斷報告！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            Clipboard.SetText(_txtClaudeReport.Text);
            _lblStatus.Text = "已將 Claude Code 專屬 Markdown 診斷報告複製到剪貼簿！";
            _tabMain.SelectedIndex = 3;
        }

        private void AppendLog(string text)
        {
            _txtBuildLog.AppendText(text + Environment.NewLine);
        }
    }
}
