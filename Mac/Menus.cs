using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Momonga.Animation;
using Momonga.Character;
using Momonga.Inventory;
using Momonga.Persistence;

namespace Momonga.Mac;

public sealed partial class CompanionController
{
    private Button Button(string text,Action action)
    {
        var button=new Button {Content=text,HorizontalContentAlignment=HorizontalAlignment.Center,Margin=new Thickness(3),Padding=new Thickness(12,8),Background=Brush(life.Character.Theme.Soft),Foreground=Brush(life.Character.Theme.Ink),CornerRadius=new CornerRadius(18)};
        button.Click+=(_,_)=>action();return button;
    }
    private void ShowPanel(string title,Control body,Action? back=null)
    {
        panel?.Close();var win=TransparentWindow(370,440);panel=win;
        var header=new Grid {ColumnDefinitions=new ColumnDefinitions("auto,*,auto")};
        header.Children.Add(Button("뒤로",()=>{if(back!=null)back();else{win.Close();panel=null;}}));
        var label=new TextBlock {Text=title,FontSize=17,FontWeight=FontWeight.Bold,VerticalAlignment=VerticalAlignment.Center,Foreground=Brush(life.Character.Theme.Ink),Margin=new Thickness(8)};Grid.SetColumn(label,1);header.Children.Add(label);
        var close=Button("닫기",()=>win.Close());Grid.SetColumn(close,2);header.Children.Add(close);
        var content=new DockPanel();DockPanel.SetDock(header,Dock.Top);content.Children.Add(header);content.Children.Add(new ScrollViewer {Content=body});
        win.Content=new Border {Background=Brush(life.Character.Theme.Background),BorderBrush=Brush(life.Character.Theme.Border),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(20),Padding=new Thickness(12),Child=content};
        SetPosition(win,ClampPopup(new Point(position.X+(pet.Width-370)/2,position.Y-100),win));
        win.Deactivated+=(_,_)=>win.Close();win.Closed+=(_,_)=>{if(panel==win)panel=null;};win.KeyDown+=(_,e)=>{if(e.Key==Key.Escape)win.Close();};win.Show();win.Activate();
    }
    private void Radial()
    {
        panel?.Close();var win=TransparentWindow(350,380);panel=win;
        var canvas=new Canvas();win.Content=canvas;
        var options=new (string,Action)[]{("애정",Affection),("돌보기",Care),("놀기",Play),("장난",Pranks),("대화",()=>{life.Interact("Talk");win.Close();}),("생활",Living),("더보기",More)};
        for(var i=0;i<options.Length;i++)
        {
            var angle=-Math.PI/2+i*Math.PI*2/options.Length;var button=Button(options[i].Item1,options[i].Item2);button.Width=94;
            Canvas.SetLeft(button,175+125*Math.Cos(angle)-47);Canvas.SetTop(button,148+102*Math.Sin(angle)-20);canvas.Children.Add(button);
        }
        var needs=new StackPanel {Orientation=Orientation.Horizontal,Spacing=8};
        foreach(var (name,value) in new[]{("식사",100-life.Data.Pet.Hunger),("물",100-life.Data.Pet.Thirst),("기운",life.Data.Pet.Energy),("관심",life.Data.Pet.Social),("재미",life.Data.Pet.Fun)})
        {
            var col=new StackPanel {Width=54};col.Children.Add(new TextBlock {Text=name,FontSize=11,Foreground=Brush(life.Character.Theme.Ink)});col.Children.Add(new ProgressBar {Value=value,Minimum=0,Maximum=100,MinWidth=0,Width=54,Height=5,Foreground=Brush(life.Character.Theme.Healthy)});needs.Children.Add(col);
        }
        var footer=new StackPanel {Spacing=8};footer.Children.Add(new TextBlock {Text=$"{life.Data.ActivityPoints} AP · {life.Character.DisplayName}",Foreground=Brush(life.Character.Theme.Ink)});footer.Children.Add(needs);
        var card=new Border {Background=Brush(life.Character.Theme.Background),CornerRadius=new CornerRadius(14),Padding=new Thickness(10),Child=footer};Canvas.SetLeft(card,18);Canvas.SetTop(card,290);canvas.Children.Add(card);
        SetPosition(win,ClampPopup(new Point(position.X+pet.Width/2-175,position.Y+pet.Height/2-148),win));
        win.Deactivated+=(_,_)=>win.Close();win.Closed+=(_,_)=>{if(panel==win)panel=null;};win.KeyDown+=(_,e)=>{if(e.Key==Key.Escape)win.Close();};win.Show();win.Activate();
    }
    private void List(string title,IEnumerable<(string,Action)> options,Action? back=null)
    {
        var body=new StackPanel();foreach(var (text,action) in options)body.Children.Add(Button(text,action));ShowPanel(title,body,back??Radial);
    }
    private void Affection()=>List("애정",new (string,Action)[]{("머리 쓰다듬기",()=>Arm("Pet")),("턱 쓰다듬기",()=>Arm("Chin")),("칭찬하기",()=>{life.Interact("Praise");panel?.Close();}),("달래기",()=>{life.Interact("Soothe");panel?.Close();})});
    private void Pranks()=>List("장난",new (string,Action)[]{("볼 잡아당기기",()=>Arm("CheekPull")),("딱밤 준비",()=>Arm("Flick")),("콕 찌르기",()=>Arm("Poke"))});
    private void Care()=>List("돌보기",new (string,Action)[]{("간식 고르기",()=>Items("간식",i=>i.Category is "Snack" or "Drink",false,null)),("밥 넣기",()=>ChooseBowl()),("물 채우기",()=>{foreach(var b in life.Data.Items.Where(i=>i.ItemId=="water-bowl"))b.Water=100;panel?.Close();})});
    private void Play()=>List("놀기",new (string,Action)[]{("함께 놀기",()=>{life.StartPlay(true);panel?.Close();}),("공 놀이",()=>UseToy("ball")),("인형 놀이",()=>UseToy("doll")),("놀이 그만",()=>{life.StopPlay();panel?.Close();})});
    private void UseToy(string id){var toy=life.Data.Items.FirstOrDefault(i=>i.ItemId==id);if(toy==null){if(life.Shop.AvailableFurniture(id)==0)life.Shop.Buy(id);toy=life.Shop.Place(id);}if(toy!=null)life.UseItem(toy);panel?.Close();SyncFurniture();}
    private void Living()=>List("생활",new (string,Action)[]{("쉬기",()=>{life.Rest(30);panel?.Close();}),("잠자기",()=>{var bed=life.Habitat.Find("Bed");if(bed!=null)life.UseItem(bed);panel?.Close();}),("깨우기",()=>{life.Rest(15);life.Interact("Talk");panel?.Close();}),("가구 놓기",()=>Items("가구",i=>!i.Consumable,false,null))});
    private void More()=>List("더보기",new (string,Action)[]{("캐릭터 변경",Characters),("설정",Settings),("상점",()=>Items("상점",_=>true,true,null)),("보관함",()=>Items("보관함",_=>true,false,null)),("잠깐 숨기기",Hide),("종료",Exit)});
    private void ChooseBowl()
    {
        var bowls=life.Data.Items.Where(i=>i.ItemId=="food-bowl").ToArray();
        if(bowls.Length==0){var bowl=life.Shop.Place("food-bowl");if(bowl!=null)bowls=new[]{bowl};}
        if(bowls.Length==1)Items("밥 고르기",i=>i.Category=="Food",false,bowls[0]);
        else List("밥그릇 선택",bowls.Select((b,i)=>($"밥그릇 {i+1}",(Action)(()=>Items("밥 고르기",d=>d.Category=="Food",false,b)))));
    }
    private void ItemMenu(HabitatItem item)
    {
        var entries=new List<(string,Action)> {("사용하기",()=>{life.UseItem(item);panel?.Close();})};
        if(item.ItemId=="food-bowl")
        {
            entries.Add(("밥 넣기",()=>Items("밥 고르기",i=>i.Category=="Food",false,item)));
            entries.Add(("밥 상점",()=>Items("밥 상점",i=>i.Category=="Food",true,item)));
            entries.Add(("무료 흰밥",()=>{if(item.FoodQuantity==0){item.FoodId="free-rice";item.FoodQuantity=1;item.FoodPortion=1;}panel?.Close();}));
            entries.Add(($"{life.Catalog[item.FoodId].Name} · 남은 양 {item.FoodPortion:P0} 회수",()=>{life.Shop.ReturnFood(item);panel?.Close();}));
        }
        if(item.ItemId=="water-bowl")entries.Add(("물 채우기",()=>{item.Water=100;panel?.Close();}));
        if(life.Catalog[item.ItemId].Category=="Bed") {entries.Add((item.Active?"방해금지 끄기":"방해금지 켜기",()=>{item.Active=!item.Active;panel?.Close();}));entries.Add(("깨우기",()=>{life.Rest(15);panel?.Close();}));}
        if(life.Catalog[item.ItemId].Category=="Toy")entries.Add(("놀이 그만",()=>{life.StopPlay();panel?.Close();}));
        entries.Add(("크기 조절",Settings));entries.Add((item.Locked?"잠금 해제":"위치 잠금",()=>{item.Locked=!item.Locked;panel?.Close();}));
        entries.Add(("가구 회수",()=>{if(item.ItemId=="food-bowl"&&!life.Shop.ReturnFood(item))return;life.Data.Items.Remove(item);panel?.Close();SyncFurniture();}));
        List(life.Catalog[item.ItemId].Name,entries);
    }
    private void Characters()
    {
        var body=new StackPanel {Spacing=6};
        foreach(var group in CharacterDefinition.All.GroupBy(c=>c.Category))
        {
            var grid=new WrapPanel {Orientation=Orientation.Horizontal};
            foreach(var c in group)
            {
                var card=new StackPanel {Width=92};card.Children.Add(new SpriteView {Width=82,Height=88,Sprite=Sprites.Frame(c.AssetSet,2)});card.Children.Add(new TextBlock {Text=c.DisplayName,HorizontalAlignment=HorizontalAlignment.Center,FontSize=12});
                var button=Button("",()=>Switch(c.CharacterId));button.Content=card;button.Padding=new Thickness(2);grid.Children.Add(button);
            }
            body.Children.Add(new Expander {Header=group.Key,IsExpanded=true,Content=grid});
        }
        ShowPanel("캐릭터 변경",body,More);
    }
    private void Switch(string id)
    {
        var definition=CharacterDefinition.Load(id);life.Data.CharacterId=id;life=new Simulation.LifeSimulation(life.Data,definition,life.Catalog);dialogue=new Content.DialogueService(life.Data,definition.DialogueSetId);motion=new Simulation.PetMotion(character:definition);AttachLife();panel?.Close();Speak("Greeting",false);
    }
    private void Items(string title,Func<ItemDefinition,bool> filter,bool shop,HabitatItem? bowl)
    {
        var body=new StackPanel {Spacing=7};var status=new TextBlock {Text=$"{life.Data.ActivityPoints} AP",Foreground=Brush(life.Character.Theme.Ink)};body.Children.Add(status);
        body.Children.Add(Button(shop?"보관함":"상점",()=>Items(shop?"보관함":"상점",filter,!shop,bowl)));
        var grid=new WrapPanel {Orientation=Orientation.Horizontal};body.Children.Add(grid);
        foreach(var item in life.Catalog.Values.Where(filter))
        {
            var sprite=item.Category=="Food"?Sprites.Bowl(item.Id,1):item.Consumable?Sprites.Snack(life.Character.AssetSet,item.AnimationFrame):item.Id=="food-bowl"?Sprites.Bowl("meal",0):item.Id=="water-bowl"?Sprites.Water(100):Sprites.Furniture(item.Id);
            var card=new StackPanel {Width=130,Spacing=3};card.Children.Add(new SpriteView {Width=90,Height=90,Sprite=sprite});card.Children.Add(new TextBlock {Text=item.Name,TextWrapping=TextWrapping.Wrap,FontSize=12});
            card.Children.Add(new TextBlock {Text=shop?$"{item.Price} AP":$"보유 {life.Shop.Quantity(item.Id)}",FontSize=11});
            var button=Button("",()=>
            {
                bool success;
                if(shop)success=life.Shop.Buy(item.Id);
                else if(item.Category=="Food")success=bowl!=null?life.Shop.FillFood(bowl,item.Id):false;
                else if(item.Consumable)success=life.GiveSnack(item.Id);
                else success=life.Shop.Place(item.Id)!=null;
                if(success){SyncFurniture();Items(title,filter,shop,bowl);}else status.Text=item.Category=="Food"&&!shop?"빈 밥그릇에서 밥 넣기를 선택해라.":"AP 또는 보유 수량을 확인해줘.";
            });button.Content=card;button.Padding=new Thickness(7);grid.Children.Add(button);
        }
        ShowPanel(title,body,bowl!=null?()=>ItemMenu(bowl):More);
    }
    private void Settings()
    {
        var body=new StackPanel {Spacing=8};
        void Slider(string title,double value,double min,double max,Action<double> changed)
        {
            var label=new TextBlock {Text=title,Foreground=Brush(life.Character.Theme.Ink)};body.Children.Add(label);var slider=new Slider {Minimum=min,Maximum=max,Value=value};slider.ValueChanged+=(_,e)=>{changed(e.NewValue);label.Text=$"{title} · {e.NewValue:0.00}";};body.Children.Add(slider);
        }
        Slider("캐릭터·가구 크기",life.Data.Settings.PetScale,.5,2,v=>{life.Data.Settings.PetScale=v;pet.Width=128*v;pet.Height=136*v;position=Clamp(position);SyncFurniture();});
        Slider("말풍선 글꼴",life.Data.Settings.SpeechFontSize,9,22,v=>life.Data.Settings.SpeechFontSize=v);
        Slider("말풍선 크기",life.Data.Settings.BubbleScale,.55,1.5,v=>life.Data.Settings.BubbleScale=v);
        var direct=new CheckBox {Content="클릭·드래그로 바로 쓰다듬기",IsChecked=life.Data.Settings.DirectTouch};direct.IsCheckedChanged+=(_,_)=>life.Data.Settings.DirectTouch=direct.IsChecked==true;body.Children.Add(direct);
        body.Children.Add(new TextBlock {Text=activity.Enabled?"타자·클릭 AP 집계 켜짐":"다른 앱의 타자·클릭 집계에는 macOS 입력 모니터링 권한이 필요해.",TextWrapping=TextWrapping.Wrap,Foreground=Brush(life.Character.Theme.Ink)});
        body.Children.Add(Button("입력 모니터링 허용",()=>{activity.RequestPermission();Settings();}));
        body.Children.Add(Button("잠깐 숨기기",Hide));body.Children.Add(Button("종료",Exit));ShowPanel("설정",body,More);
    }
    public void CheckUI()
    {
        Directory.CreateDirectory("obj/mac-ui-check");
        void Capture(Control control,string name)
        {
            control.UpdateLayout();using var bitmap=new Avalonia.Media.Imaging.RenderTargetBitmap(new PixelSize(Math.Max(1,(int)control.Bounds.Width),Math.Max(1,(int)control.Bounds.Height)));bitmap.Render(control);bitmap.Save("obj/mac-ui-check/"+name+".png");
        }
        foreach(var c in CharacterDefinition.All)
        {
            petView.Sprite=Sprites.Frame(c.AssetSet,2);Capture(petView,c.CharacterId);
            foreach(var item in life.Catalog.Values.Where(i=>i.Category=="Food"))_=Sprites.Meal(c.AssetSet,item.Id,1);
            _=Sprites.Drink(c.AssetSet,0);
        }
        Radial();Capture((Control)panel!.Content!,"radial");Characters();Capture((Control)panel!.Content!,"characters");Settings();Capture((Control)panel!.Content!,"settings");
        Items("밥 상점",i=>i.Category=="Food",true,null);Capture((Control)panel!.Content!,"shop");
        ShowUpdate(new Momonga.Updates.AvailableUpdate(new Version(0,3,2),"오데 대사 수정\n실행 시 새 버전 확인","",154000000,""));Capture((Control)panel!.Content!,"updates");panel.Close();
        foreach(var item in life.Data.Items){ItemMenu(item);Capture((Control)panel!.Content!,item.ItemId);}
        Hide();if(tray?.IsVisible!=true||pet.IsVisible)throw new InvalidOperationException("Hidden pet lost tray recall");Recall();if(!pet.IsVisible)throw new InvalidOperationException("Recall failed");
        Switch("kuromi");if(life.Data.CharacterId!="kuromi")throw new InvalidOperationException("Character switch failed");
    }
}
