using System;
using System.Windows;
using System.Windows.Controls;
using Momonga.Platform;
using Momonga.Simulation;

namespace Momonga.UI;

public sealed class SettingsWindow : CompanionPanelWindow
{
    public SettingsWindow(LifeSimulation life, Action<double> resize, Action changed, Action? back = null, Action<Momonga.Animation.PetPose>? preview = null) : base("설정", life.Character.DisplayName + " · " + life.Character.Theme.Name, true, back)
    {
        Body.Children.Add(Theme.Text(life.Character.DisplayName + " · 가구 크기", 16, true));
        var scaleText = Theme.Text($"{life.Data.Settings.PetScale:P0}", 13);
        var scale = new Slider { Minimum = 0.5, Maximum = 2, Value = life.Data.Settings.PetScale, TickFrequency = 0.1, IsSnapToTickEnabled = false, Margin = new Thickness(4, 8, 4, 8) };
        scale.ValueChanged += (_, _) => { scaleText.Text = $"{scale.Value:P0}"; resize(scale.Value); changed(); };
        var sizeCard = new StackPanel(); sizeCard.Children.Add(scaleText); sizeCard.Children.Add(scale); Body.Children.Add(Theme.Card(sizeCard));
        void Toggle(string text, bool initial, Func<bool, bool> set)
        {
            var check = new CheckBox { Content = Theme.Text(text, 13), IsChecked = initial, Margin = new Thickness(0, 6, 0, 6) };
            check.Click += (_, _) => { var desired = check.IsChecked == true; if (!set(desired)) check.IsChecked = !desired; changed(); }; Body.Children.Add(Theme.Card(check));
        }
        Toggle("클릭·드래그로 바로 쓰다듬기 / 볼 당기기", life.Data.Settings.DirectTouch, value => { life.Data.Settings.DirectTouch = value; return true; });
        Toggle("전체화면 작업 중 먼저 말 걸지 않기", life.Data.Settings.FullscreenCourtesy, value => { life.Data.Settings.FullscreenCourtesy = value; return true; });
        Toggle("Windows 시작할 때 함께 시작", life.Data.Settings.StartWithWindows, value =>
        {
            try { DesktopAwareness.SetStartup(value); life.Data.Settings.StartWithWindows = value; Notice.Text = "시작 설정을 저장했어요."; return true; }
            catch (Exception error) when (error is System.IO.IOException or UnauthorizedAccessException or System.Security.SecurityException)
            { Notice.Text = "시작 설정 변경 실패: " + error.Message; return false; }
        });
        Body.Children.Add(Theme.Text("생활비", 16, true));
        var rate = new ComboBox { ItemsSource = new[] { 1, 2, 3 }, SelectedItem = life.Data.Settings.PointsPerInput, Margin = new Thickness(0, 8, 0, 8) };
        rate.SelectionChanged += (_, _) => { life.Data.Settings.PointsPerInput = (int)rate.SelectedItem; changed(); };
        Body.Children.Add(Theme.Text("타자 또는 클릭 1회마다 AP", 12)); Body.Children.Add(rate);
        void Adjust(string label, double min, double max, double value, Action<double> set)
        {
            var text = Theme.Text(label, 13, true);
            var slider = new Slider { Minimum = min, Maximum = max, Value = value, Margin = new Thickness(4,8,4,8) };
            slider.ValueChanged += (_, _) => { set(slider.Value); changed(); };
            Body.Children.Add(text); Body.Children.Add(slider);
        }
        Adjust("말풍선 폰트 크기", 10, 22, life.Data.Settings.SpeechFontSize, v => life.Data.Settings.SpeechFontSize = v);
        Adjust("말풍선 크기", .6, 1.5, life.Data.Settings.BubbleScale, v => life.Data.Settings.BubbleScale = v);
        var explanation = new StackPanel(); explanation.Children.Add(Theme.Text("조용히 함께 있기", 15, true));
        explanation.Children.Add(Theme.Text("물건은 바탕화면 어디든 드래그해서 놓으세요. 방석 우클릭에서 방해금지를 켜고 펫을 가까이 놓으면 조용히 쉽니다. 끄거나 멀어지면 해제됩니다.", 13));
        Body.Children.Add(Theme.Card(explanation));
        if (preview != null)
        {
            Body.Children.Add(Theme.Text("모션 미리보기", 16, true));
            var poses = new WrapPanel();
            foreach (var (label, pose) in new[] { ("기쁨", Momonga.Animation.PetPose.Happy), ("먹기", Momonga.Animation.PetPose.Eat), ("잠자기", Momonga.Animation.PetPose.Sleep), ("짜증", Momonga.Animation.PetPose.Annoyed), ("삐짐", Momonga.Animation.PetPose.Sulk), ("놀람", Momonga.Animation.PetPose.Startled), ("놀기", Momonga.Animation.PetPose.Play) })
                poses.Children.Add(Theme.Button(label, () => preview(pose)));
            Body.Children.Add(poses);
        }
        Notice.Text = "입력 내용과 키 종류는 읽거나 저장하지 않아요.";
    }
}
