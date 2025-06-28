using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PushoverNotifier
{
    /// <summary>
    /// アプリケーションの設定を保存するためのクラス。
    /// APIトークンは暗号化されて保存され、ユーザーキーと時間プリセットは平文で保存されます。
    /// </summary>
    public class AppSettings
    {
        /// <summary>暗号化されたPushover APIトークン（Base64文字列）</summary>
        public string? EncryptedApiToken { get; set; }
        /// <summary>Pushoverユーザーキー</summary>
        public string? UserKey { get; set; }
        /// <summary>タイマー時間のプリセットリスト (hh:mm:ss形式)</summary>
        public List<string>? TimePresets { get; set; }
    }

    /// <summary>
    /// メインフォームクラス。Pushover通知タイマーのGUIを提供します。
    /// </summary>
    public partial class Form1 : Form
    {
        // UIコントロールの宣言
        private TextBox? apiTokenTextBox;
        private TextBox? userKeyTextBox;
        private TextBox? timeTextBox;
        private Button? startButton;
        private Button? stopButton; // Stopボタンを追加
        private Label? statusLabel;
        private CheckBox? showTokenCheckBox;
        private List<Button> presetButtons = new List<Button>(); // プリセットボタンのリスト

        // 設定ファイルのパスとディレクトリ
        private readonly string settingsFilePath;
        private readonly string settingsDirectory;
        private List<string> timePresets = new List<string>(); // 時間プリセットの内部リスト

        // カウントダウンタイマーと終了時刻
        private System.Windows.Forms.Timer? countdownTimer;
        private DateTime endTime;
        private CancellationTokenSource? cancellationTokenSource; // キャンセルトークンソースを追加

        /// <summary>
        /// Form1クラスの新しいインスタンスを初期化します。
        /// </summary>
        public Form1()
        {
            InitializeComponent(); // デザイナーで生成されたコンポーネントの初期化
            InitializeCustomComponents(); // カスタムUIコンポーネントの初期化
            this.Text = "Pushover Notifier"; // フォームのタイトル設定
            this.Size = new System.Drawing.Size(480, 320); // フォームのサイズ設定

            // 設定ファイルのパスを決定 (AppData/PushoverNotifier/settings.json)
            settingsDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PushoverNotifier");
            settingsFilePath = Path.Combine(settingsDirectory, "settings.json");

            LoadSettings(); // アプリケーション起動時に設定を読み込む

            // フォームが閉じられるときに設定を保存するためのイベントハンドラを登録
            this.FormClosing += Form1_FormClosing;
        }

        /// <summary>
        /// フォームが閉じられるときに呼び出され、設定を保存します。
        /// </summary>
        private void Form1_FormClosing(object? sender, FormClosingEventArgs e)
        {
            SaveSettings();
        }

        /// <summary>
        /// settings.jsonファイルからアプリケーション設定を読み込みます。
        /// APIトークンは復号されます。
        /// </summary>
        private void LoadSettings()
        {
            try
            {
                if (File.Exists(settingsFilePath))
                {
                    string json = File.ReadAllText(settingsFilePath);
                    var settings = JsonSerializer.Deserialize<AppSettings>(json);
                    if (settings != null)
                    {
                        // ユーザーキーをテキストボックスに設定
                        if (userKeyTextBox != null) userKeyTextBox.Text = settings.UserKey;

                        // 暗号化されたAPIトークンが存在する場合、復号してテキストボックスに設定
                        if (!string.IsNullOrEmpty(settings.EncryptedApiToken) && apiTokenTextBox != null)
                        {
                            byte[] encryptedData = Convert.FromBase64String(settings.EncryptedApiToken);
                            // DPAPIを使用してデータを復号 (現在のユーザーに紐付け)
                            byte[] unprotectedData = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
                            apiTokenTextBox.Text = Encoding.UTF8.GetString(unprotectedData);
                        }

                        // 時間プリセットを読み込む。存在しない場合は空のリストを初期化
                        timePresets = settings.TimePresets ?? new List<string>();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定の読み込みと復号に失敗しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            // プリセットが設定されていない場合、デフォルト値を設定
            if (timePresets.Count == 0)
            {
                timePresets.AddRange(new[] { "00:15:00", "00:30:00", "01:00:00" });
            }
            // プリセットが3つ未満の場合、不足分を"00:00:00"で埋める
            while (timePresets.Count < 3) { timePresets.Add("00:00:00"); }

            // プリセットボタンのテキストを更新
            for (int i = 0; i < presetButtons.Count; i++)
            {
                presetButtons[i].Text = timePresets[i];
            }
        }

        /// <summary>
        /// 現在のアプリケーション設定をsettings.jsonファイルに保存します。
        /// APIトークンは暗号化されて保存されます。
        /// </summary>
        private void SaveSettings()
        {
            if (apiTokenTextBox == null || userKeyTextBox == null) return;

            try
            {
                // 設定ディレクトリが存在しない場合は作成
                Directory.CreateDirectory(settingsDirectory);
                string? encryptedToken = null;

                // APIトークンが入力されている場合、暗号化
                if (!string.IsNullOrEmpty(apiTokenTextBox.Text))
                {
                    byte[] userData = Encoding.UTF8.GetBytes(apiTokenTextBox.Text);
                    // DPAPIを使用してデータを暗号化 (現在のユーザーに紐付け)
                    byte[] encryptedData = ProtectedData.Protect(userData, null, DataProtectionScope.CurrentUser);
                    encryptedToken = Convert.ToBase64String(encryptedData); // Base64文字列として保存
                }

                // AppSettingsオブジェクトを作成し、設定を格納
                var settings = new AppSettings
                {
                    EncryptedApiToken = encryptedToken,
                    UserKey = userKeyTextBox.Text,
                    TimePresets = this.timePresets // 現在のプリセットを保存
                };

                // 設定をJSON形式でファイルに書き込む (整形して読みやすくする)
                string json = JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(settingsFilePath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"設定の保存と暗号化に失敗しました: {ex.Message}", "エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// カスタムUIコンポーネントを初期化し、フォームに追加します。
        /// </summary>
        private void InitializeCustomComponents()
        {
            // カウントダウンタイマーの初期化 (1秒間隔)
            countdownTimer = new System.Windows.Forms.Timer { Interval = 1000 };
            countdownTimer.Tick += CountdownTimer_Tick; // Tickイベントハンドラを登録

            // APIトークン関連のUI
            var apiTokenLabel = new Label() { Text = "API Token:", Location = new System.Drawing.Point(10, 20), Size = new System.Drawing.Size(100, 20) };
            apiTokenTextBox = new TextBox() { Location = new System.Drawing.Point(120, 20), Size = new System.Drawing.Size(250, 20), PasswordChar = '*' }; // パスワード文字でマスク
            showTokenCheckBox = new CheckBox() { Text = "Show", Location = new System.Drawing.Point(380, 20), Size = new System.Drawing.Size(60, 20) };
            showTokenCheckBox.CheckedChanged += ShowTokenCheckBox_CheckedChanged; // チェックボックスの変更イベントハンドラを登録
            this.Controls.Add(apiTokenLabel);
            this.Controls.Add(apiTokenTextBox);
            this.Controls.Add(showTokenCheckBox);

            // ユーザーキー関連のUI
            var userKeyLabel = new Label() { Text = "User Key:", Location = new System.Drawing.Point(10, 50), Size = new System.Drawing.Size(100, 20) };
            userKeyTextBox = new TextBox() { Location = new System.Drawing.Point(120, 50), Size = new System.Drawing.Size(320, 20) };
            this.Controls.Add(userKeyLabel);
            this.Controls.Add(userKeyTextBox);

            // 時間入力関連のUI
            var timeLabel = new Label() { Text = "Time (hh:mm:ss):", Location = new System.Drawing.Point(10, 80), Size = new System.Drawing.Size(110, 30), TextAlign = ContentAlignment.MiddleLeft };
            timeTextBox = new TextBox() { Location = new System.Drawing.Point(120, 80), Size = new System.Drawing.Size(150, 30), Text = "00:01:00" };
            timeTextBox.Font = new Font(timeTextBox.Font.FontFamily, 14F, FontStyle.Bold); // フォントサイズとスタイルを設定
            this.Controls.Add(timeLabel);
            this.Controls.Add(timeTextBox);

            // プリセットボタンの生成と配置
            for (int i = 0; i < 3; i++)
            {
                var presetButton = new Button() { Location = new System.Drawing.Point(120 + (i * 80), 115), Size = new System.Drawing.Size(75, 23) };
                presetButton.Click += PresetButton_Click; // クリックイベントハンドラを登録
                this.Controls.Add(presetButton);
                presetButtons.Add(presetButton);
            }

            // タイマー開始ボタン
            startButton = new Button() { Text = "Start Timer", Location = new System.Drawing.Point(120, 150), Size = new System.Drawing.Size(150, 30) };
            startButton.Click += StartButton_Click; // クリックイベントハンドラを登録
            this.Controls.Add(startButton);

            // タイマー停止ボタン
            stopButton = new Button() { Text = "Stop Timer", Location = new System.Drawing.Point(280, 150), Size = new System.Drawing.Size(150, 30), Enabled = false }; // 最初は無効
            stopButton.Click += StopButton_Click; // クリックイベントハンドラを登録
            this.Controls.Add(stopButton);

            // ステータス表示ラベル
            statusLabel = new Label() { Text = "Status: Waiting", Location = new System.Drawing.Point(10, 195), Size = new System.Drawing.Size(440, 50), AutoSize = false, ForeColor = System.Drawing.Color.Blue };
            this.Controls.Add(statusLabel);
        }

        /// <summary>
        /// 「Stop Timer」ボタンがクリックされたときに、タイマーを停止し、通知の送信を中止します。
        /// </summary>
        private void StopButton_Click(object? sender, EventArgs e)
        {
            countdownTimer?.Stop();
            cancellationTokenSource?.Cancel(); // 非同期処理をキャンセル
            if (statusLabel != null) { statusLabel.Text = "Status: Timer stopped."; }
            if (startButton != null) { startButton.Enabled = true; }
            if (stopButton != null) { stopButton.Enabled = false; }
        }

        /// <summary>
        /// プリセットボタンがクリックされたときに、そのボタンのテキストを時間入力欄に設定します。
        /// </summary>
        private void PresetButton_Click(object? sender, EventArgs e)
        {
            if (timeTextBox != null && sender is Button clickedButton)
            {
                timeTextBox.Text = clickedButton.Text;
            }
        }

        /// <summary>
        /// カウントダウンタイマーのTickイベントハンドラ。
        /// 残り時間を計算し、ステータスラベルを更新します。
        /// </summary>
        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            if (statusLabel == null) return;

            TimeSpan remaining = endTime - DateTime.Now; // 残り時間を計算
            if (remaining.TotalSeconds > 0)
            {
                // 残り時間が正の場合、hh:mm:ss形式で表示
                statusLabel.Text = $"Status: Time remaining: {remaining:hh\\:mm\\:ss}";
            }
            else
            {
                // 残り時間が0以下になったらタイマーを停止し、通知送信中メッセージを表示
                countdownTimer?.Stop();
                statusLabel.Text = "Status: Sending notification...";
            }
        }

        /// <summary>
        /// 「Show Token」チェックボックスの状態が変更されたときに、APIトークンの表示/非表示を切り替えます。
        /// </summary>
        private void ShowTokenCheckBox_CheckedChanged(object? sender, EventArgs e)
        {
            if (apiTokenTextBox != null)
            {
                // チェックボックスの状態に応じてPasswordCharを設定
                apiTokenTextBox.PasswordChar = (showTokenCheckBox?.Checked == true) ? '\0' : '*';
            }
        }

        /// <summary>
        /// 「Start Timer」ボタンがクリックされたときに、タイマーを開始し、Pushover通知を送信します。
        /// </summary>
        private async void StartButton_Click(object? sender, EventArgs e)
        {
            // UIコンポーネントが正しく初期化されているか確認
            if (apiTokenTextBox == null || userKeyTextBox == null || timeTextBox == null || startButton == null || stopButton == null || statusLabel == null || countdownTimer == null)
            {
                MessageBox.Show("UIコンポーネントが正しく初期化されていません。", "初期化エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 入力値を取得
            string apiToken = apiTokenTextBox.Text;
            string userKey = userKeyTextBox.Text;
            string timeString = timeTextBox.Text;

            // APIトークンとユーザーキーが入力されているか確認
            if (string.IsNullOrWhiteSpace(apiToken) || string.IsNullOrWhiteSpace(userKey))
            {
                MessageBox.Show("APIトークンとユーザーキーを入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 時間入力の形式を検証
            if (!TimeSpan.TryParseExact(timeString, "hh\\:mm\\:ss", CultureInfo.InvariantCulture, out TimeSpan waitDuration) || waitDuration <= TimeSpan.Zero)
            {
                MessageBox.Show("hh:mm:ss形式で有効な時間を入力してください。", "入力エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 自動生成される通知メッセージ
            string message = $"{timeString}経ちました";

            try
            {
                startButton.Enabled = false; // タイマー開始中はボタンを無効化
                stopButton.Enabled = true; // Stopボタンを有効化
                cancellationTokenSource = new CancellationTokenSource(); // 新しいキャンセルトークンソースを作成

                endTime = DateTime.Now + waitDuration; // 通知送信目標時刻を設定
                countdownTimer.Start(); // カウントダウンタイマーを開始
                CountdownTimer_Tick(null, EventArgs.Empty); // ステータスラベルを即座に更新
                
                await Task.Delay(waitDuration, cancellationTokenSource.Token); // 指定された時間だけ非同期で待機

                countdownTimer.Stop(); // 待機終了後、カウントダウンタイマーを停止
                statusLabel.Text = "Status: Sending notification..."; // 通知送信中メッセージを表示
                
                // HTTPクライアントを使用してPushover APIに通知を送信
                using (var client = new HttpClient())
                {
                    var content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("token", apiToken),
                        new KeyValuePair<string, string>("user", userKey),
                        new KeyValuePair<string, string>("message", message)
                    });

                    var response = await client.PostAsync("https://api.pushover.net/1/messages.json", content);
                    var responseString = await response.Content.ReadAsStringAsync();

                    if (response.IsSuccessStatusCode)
                    {
                        statusLabel.Text = "Status: Notification sent successfully!"; // 成功メッセージ
                    }
                    else
                    {
                        // 失敗メッセージとエラー詳細を表示
                        statusLabel.Text = $"Status: Failed to send notification. Response: {responseString}";
                        MessageBox.Show($"通知の送信に失敗しました。\n応答: {responseString}", "APIエラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // キャンセルされた場合
                statusLabel.Text = "Status: Timer stopped by user.";
            }
            catch (Exception ex)
            {
                // 例外発生時のエラーメッセージ表示
                statusLabel.Text = $"Status: An error occurred: {ex.Message}";
                MessageBox.Show($"エラーが発生しました: {ex.Message}", "実行時エラー", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                countdownTimer.Stop(); // 処理終了後、カウントダウンタイマーを停止
                startButton.Enabled = true; // ボタンを再度有効化
                stopButton.Enabled = false; // Stopボタンを無効化
                cancellationTokenSource?.Dispose(); // キャンセルトークンソースを破棄
                cancellationTokenSource = null; // nullに設定してリソースを解放

                // ステータスが残り時間表示または送信中の場合、待機中に戻す
                if (statusLabel.Text.Contains("remaining") || statusLabel.Text.StartsWith("Status: Sending"))
                {
                   statusLabel.Text = "Status: Waiting";
                }
            }
        }
    }
}