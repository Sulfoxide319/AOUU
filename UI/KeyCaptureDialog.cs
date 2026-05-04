using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AOUU.Services;

namespace AOUU.UI;

public sealed class KeyCaptureDialog : Form
{
    private readonly Label _resultLabel;
    private readonly Label _statusLabel;
    private readonly Button _startCaptureButton;
    private readonly Button _retryButton;
    private readonly Button _confirmButton;
    private readonly Button _cancelButton;
    private readonly System.Windows.Forms.Timer _armTimer;
    private readonly InputCaptureService _inputCaptureService;

    private CaptureState _state = CaptureState.Idle;

    public KeyCaptureDialog(string keyPurpose, string currentKeyName, InputCaptureService inputCaptureService)
    {
        _inputCaptureService = inputCaptureService;

        Text = $"设置{keyPurpose}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        TopMost = true;
        Width = 460;
        Height = 320;

        var helpPanel = new Panel
        {
            Left = 24,
            Top = 24,
            Width = 392,
            Height = 96,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.FromArgb(247, 240, 220)
        };

        var titleLabel = new Label
        {
            Left = 18,
            Top = 12,
            Width = 356,
            Height = 24,
            Text = "快捷键录制",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold)
        };

        var helpLabel = new Label
        {
            Left = 18,
            Top = 40,
            Width = 356,
            Height = 38,
            Text = "先点“开始录制”，再按下目标组合键。\r\n支持键盘和常见 XInput 手柄按钮组合，鼠标输入会被忽略。",
            TextAlign = ContentAlignment.MiddleCenter
        };

        helpPanel.Controls.Add(titleLabel);
        helpPanel.Controls.Add(helpLabel);

        var currentLabel = new Label
        {
            Left = 24,
            Top = 136,
            Width = 392,
            Height = 24,
            Text = $"当前绑定：{currentKeyName}",
            TextAlign = ContentAlignment.MiddleCenter
        };

        _resultLabel = new Label
        {
            Left = 24,
            Top = 168,
            Width = 392,
            Height = 28,
            Text = "识别结果：等待识别",
            TextAlign = ContentAlignment.MiddleCenter,
            Font = new Font(SystemFonts.DefaultFont.FontFamily, 10, FontStyle.Bold)
        };

        _statusLabel = new Label
        {
            Left = 24,
            Top = 202,
            Width = 392,
            Height = 34,
            Text = "点击“开始录制”后开始监听输入。",
            TextAlign = ContentAlignment.MiddleCenter
        };

        _startCaptureButton = new Button
        {
            Left = 24,
            Top = 246,
            Width = 116,
            Text = "开始录制"
        };
        _startCaptureButton.Click += StartCaptureButton_Click;

        _retryButton = new Button
        {
            Left = 152,
            Top = 246,
            Width = 88,
            Text = "重试",
            Enabled = false
        };
        _retryButton.Click += StartCaptureButton_Click;

        _confirmButton = new Button
        {
            Left = 252,
            Top = 246,
            Width = 76,
            Text = "确认",
            Enabled = false
        };
        _confirmButton.Click += ConfirmButton_Click;

        _cancelButton = new Button
        {
            Left = 340,
            Top = 246,
            Width = 76,
            Text = "取消"
        };
        _cancelButton.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        Controls.Add(helpPanel);
        Controls.Add(currentLabel);
        Controls.Add(_resultLabel);
        Controls.Add(_statusLabel);
        Controls.Add(_startCaptureButton);
        Controls.Add(_retryButton);
        Controls.Add(_confirmButton);
        Controls.Add(_cancelButton);

        _armTimer = new System.Windows.Forms.Timer();
        _armTimer.Interval = 180;
        _armTimer.Tick += ArmTimer_Tick;

        Shown += KeyCaptureDialog_Shown;
        FormClosed += KeyCaptureDialog_FormClosed;
    }

    public List<int> CapturedKeyCodes { get; private set; } = [];

    public string? CapturedKeyName { get; private set; }

    private void KeyCaptureDialog_Shown(object? sender, EventArgs e)
    {
        Activate();
    }

    private void StartCaptureButton_Click(object? sender, EventArgs e)
    {
        CapturedKeyCodes = [];
        CapturedKeyName = null;
        _resultLabel.Text = "识别结果：等待识别";
        _statusLabel.Text = "正在准备监听，请稍后按下目标组合键...";
        _confirmButton.Enabled = false;
        _retryButton.Enabled = false;
        _startCaptureButton.Enabled = false;
        _state = CaptureState.Arming;

        _armTimer.Stop();
        _armTimer.Start();
    }

    private void ArmTimer_Tick(object? sender, EventArgs e)
    {
        _armTimer.Stop();

        if (_state != CaptureState.Arming)
        {
            return;
        }

        _state = CaptureState.Armed;
        _statusLabel.Text = "正在监听，请同时按下想设置的快捷键组合。";
    }

    private void InputCaptureService_InputPressed(int keyCode)
    {
        if (!IsHandleCreated || IsMouseInput(keyCode))
        {
            return;
        }

        BeginInvoke(new Action(UpdateCapturedHotkeyPreview));
    }

    private void UpdateCapturedHotkeyPreview()
    {
        if (_state != CaptureState.Armed)
        {
            return;
        }

        var recordableKeys = TriggerMonitorService.GetPressedKeys()
            .Where(IsRecordableInput)
            .ToList();
        if (recordableKeys.Count == 0)
        {
            return;
        }

        CapturedKeyCodes = recordableKeys;
        CapturedKeyName = TriggerMonitorService.GetHotkeyName(recordableKeys);
        _resultLabel.Text = $"识别结果：{CapturedKeyName}";
        _statusLabel.Text = "按下新的组合会重新采样并替换结果，点击“确认”后保存。";
        _confirmButton.Enabled = true;
        _retryButton.Enabled = true;
        _startCaptureButton.Enabled = true;
    }

    private static bool IsMouseInput(int keyCode)
    {
        return keyCode is 0x01 or 0x02 or 0x04 or 0x05 or 0x06;
    }

    private static bool IsRecordableInput(int keyCode)
    {
        return !IsMouseInput(keyCode) && TriggerMonitorService.IsSupportedHotkey(keyCode);
    }

    private void ConfirmButton_Click(object? sender, EventArgs e)
    {
        if (CapturedKeyCodes.Count == 0)
        {
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void KeyCaptureDialog_FormClosed(object? sender, FormClosedEventArgs e)
    {
        _armTimer.Dispose();
        _inputCaptureService.InputPressed -= InputCaptureService_InputPressed;
    }

    protected override void OnVisibleChanged(EventArgs e)
    {
        base.OnVisibleChanged(e);

        if (Visible)
        {
            _inputCaptureService.InputPressed += InputCaptureService_InputPressed;
        }
    }

    private enum CaptureState
    {
        Idle,
        Arming,
        Armed
    }
}
