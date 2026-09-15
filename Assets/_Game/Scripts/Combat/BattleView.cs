using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using FinalDefense.Core;
using FinalDefense.UI;
using FinalDefense.Shop;

namespace FinalDefense.Combat
{
    // A native Unity presentation of BattleSimulation: no physics or UI timing affects combat rules.
    public sealed class BattleView : MonoBehaviour
    {
        public TMP_FontAsset Font;
        public BattleSimulation Simulation { get; private set; }
        private BattleConfiguration configuration;
        private RectTransform canvasRect, board, unitLayer, effectsLayer;
        private TextMeshProUGUI details, hint, eventText, resultText, selectedName;
        private BattleHUD hud;
        private GameObject detailPanel;
        private Vector2 lastCanvasSize;
        private Image detailPortrait, previewPortrait;
        private Button start, upgrade, revive, withdraw, retry, menuRestart, menuExit;
        private GameObject resultPanel, menuPanel;
        private GameObject rosterPanel;
        private GameObject suppliesPanel;
        private bool suppliesWasPaused;
        private Button rosterButton;
        private RectTransform placementPreview;
        private ScrollRect detailScroll;
        private TextMeshProUGUI placementPreviewText;
        private Vector2 dragPosition;
        private readonly Dictionary<string, Image> cells = new Dictionary<string, Image>();
        private readonly Dictionary<int, UnitVisual> visuals = new Dictionary<int, UnitVisual>();
        private readonly List<Image> projectileVisuals = new List<Image>();
        private readonly Dictionary<string, Button> cards = new Dictionary<string, Button>();
        private readonly Dictionary<string, TextMeshProUGUI> cardTexts = new Dictionary<string, TextMeshProUGUI>();
        private string selectedId;
        private UnitState selectedUnit;
        private bool started, reported, menuWasPaused, dragging, requiresRoster;
        private int facing=0;
        private float uiTimer, hintUntil;
        private float CellSize=90;
        private Color ink => UITheme.Instance.foreground;
        private Color muted => UITheme.Instance.mutedForeground;
        private Color paper => UITheme.Instance.card;
        private Color blue => UITheme.Instance.primary;
        private readonly Dictionary<string,Color> cardColors = new Dictionary<string,Color>();
        private sealed class UnitVisual
        {
            public RectTransform root;
            public Image body,portrait,health,shield,sp;
            public TextMeshProUGUI label;
            public int attacks;
            public float poseUntil;
        }
        public void Initialize(BattleConfiguration config)
        {
            configuration=config;
            requiresRoster=GameManager.Instance!=null&&GameManager.Instance.PersonalitySelected&&config.towers.Length!=8;
            Simulation=new BattleSimulation(config,731){Paused=true};
            BuildUI(); Render();
            if(requiresRoster)OpenRoster();
        }
        private void Update()
        {
            if(Simulation==null)return;
            Simulation.Tick(UnityEngine.Time.unscaledDeltaTime);
            if(Keyboard.current!=null)
            {
                if(Keyboard.current.escapeKey.wasPressedThisFrame)
                {
                    if(suppliesPanel!=null&&suppliesPanel.activeSelf)CloseSupplies();
                    else if(rosterPanel!=null&&rosterPanel.activeSelf)
                    {
                        if(requiresRoster)SceneLoader.LoadMainMenu();else rosterPanel.SetActive(false);
                    }
                    else if(menuPanel.activeSelf)ToggleMenu();
                    else if(selectedId!=null)CancelSelection();
                    else ToggleMenu();
                }
                if(Keyboard.current.rKey.wasPressedThisFrame && !ModalOpen())Rotate();
                if(Keyboard.current.spaceKey.wasPressedThisFrame && !ModalOpen())TogglePause();
            }
            if(Mouse.current!=null&&Mouse.current.rightButton.wasPressedThisFrame)CancelSelection();
            uiTimer+=UnityEngine.Time.unscaledDeltaTime;
            if(uiTimer>=1f/30){uiTimer=0;Render();}
        }
        private void BuildUI()
        {
            // Keep the scene's authored Canvas, HUD, camera and bottom card rail.
            var canvas=GetComponent<Canvas>();
            if(canvas==null)throw new InvalidOperationException("BattleView must bind to the authored Canvas.");
            canvasRect=(RectTransform)canvas.transform;
            foreach(var text in canvas.GetComponentsInChildren<TextMeshProUGUI>(true))
                if(Font!=null)text.font=Font;
            hud=canvas.GetComponent<BattleHUD>();hud.Bind(Simulation,Font);
            gameObject.AddComponent<BattleBoardPresentation>().Initialize(configuration);

            board=Rect(canvasRect,"Board",Vector2.zero,new Vector2(990,810));
            board.SetAsFirstSibling();
            foreach(var c in configuration.ground.Concat(configuration.highGround))
            {
                var rt=Rect(board,"Cell "+c.x+","+c.y,BoardPoint(c.x,c.y),new Vector2(CellSize-3,CellSize-3));
                var image=rt.gameObject.AddComponent<Image>();image.color=Color.clear;
                var button=rt.gameObject.AddComponent<Button>();button.targetGraphic=image;button.transition=Selectable.Transition.None;
                button.onClick.AddListener(()=>CellClicked(c.x,c.y));
                cells[c.x+","+c.y]=image;
            }
            var entrance=configuration.path[0];var goal=configuration.path[configuration.path.Length-1];
            Label(board,"Entrance",BoardPoint(entrance.x,entrance.y)+new Vector2(0,35),new Vector2(95,28),16,new Color(.3f,.9f,.45f)).text="START";
            Label(board,"Goal",BoardPoint(goal.x,goal.y)+new Vector2(0,35),new Vector2(95,28),16,new Color(1f,.45f,.45f)).text="GOAL";
            unitLayer=Rect(board,"Units",Vector2.zero,board.sizeDelta);
            effectsLayer=Rect(board,"Projectiles",Vector2.zero,board.sizeDelta);
            placementPreview=Panel(effectsLayer,"Placement Preview",Vector2.zero,new Vector2(54,54),new Color(.2f,.7f,.4f,.7f));
            placementPreviewText=Label(placementPreview,"Preview Direction",Vector2.zero,new Vector2(54,54),22,Color.white);
            previewPortrait=Art(placementPreview,"Preview Art",Vector2.zero,new Vector2(50,54),null);
            placementPreviewText.transform.SetAsLastSibling();placementPreview.gameObject.SetActive(false);

            BuildCardRail();
            BuildSelectionPopup();
            start=ButtonAt(canvasRect,"Start","开始对决",new Vector2(-700,450),new Vector2(175,52),TogglePause);
            ButtonAt(canvasRect,"Menu","菜单  Esc",new Vector2(700,450),new Vector2(150,52),ToggleMenu);
            ButtonAt(canvasRect,"Rotate","朝向  R",new Vector2(-430,-450),new Vector2(140,52),Rotate);
            // The original optional AI-button slot now opens the implemented formation selection.
            rosterButton=canvasRect.Find("AIAgentBtn").GetComponent<Button>();
            rosterButton.gameObject.name="Roster Setup";rosterButton.onClick.RemoveAllListeners();
            rosterButton.onClick.AddListener(OpenRoster);SetButtonText(rosterButton,"调整阵容");
            ((RectTransform)rosterButton.transform).anchoredPosition=new Vector2(430,-450);
            UIStyler.ApplySecondaryButtonStyle(rosterButton);
            ButtonAt(canvasRect,"Supplies","补给 · "+(configuration.battleItems?.Length??0),new Vector2(590,-450),new Vector2(140,52),OpenSupplies);
            hint=Label(canvasRect,"Hint",new Vector2(0,405),new Vector2(1000,28),16,Color.white);
            eventText=Label(canvasRect,"Battle Event",new Vector2(0,-520),new Vector2(1000,28),15,Color.white);
            BuildOverlays();UpdateBoardProjection();
        }
        private void BuildCardRail()
        {
            var panel=(RectTransform)canvasRect.Find("TowerPanel");
            var layout=panel.GetComponent<HorizontalLayoutGroup>();if(layout!=null)layout.enabled=false;
            var originals=panel.GetComponentsInChildren<Button>(true);
            if(originals.Length==0)throw new InvalidOperationException("TowerPanel requires its authored card templates.");
            var viewport=Rect(panel,"Roster",Vector2.zero,new Vector2(600,100));
            var background=viewport.gameObject.AddComponent<Image>();background.color=Color.clear;
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll=viewport.gameObject.AddComponent<ScrollRect>();
            var content=Rect(viewport,"Cards",Vector2.zero,new Vector2(configuration.towers.Length*170,100));
            content.anchorMin=content.anchorMax=content.pivot=new Vector2(0,.5f);
            scroll.viewport=viewport;scroll.content=content;scroll.horizontal=true;scroll.vertical=false;scroll.movementType=ScrollRect.MovementType.Clamped;
            var templates=originals.Select(b=>Instantiate(b.gameObject,canvasRect)).ToArray();
            foreach(var template in templates)template.SetActive(false);
            for(int i=0;i<configuration.towers.Length;i++)
            {
                var d=configuration.towers[i];
                var button=i<originals.Length?originals[i]:Instantiate(templates[i%templates.Length],content).GetComponent<Button>();
                button.gameObject.SetActive(true);button.transform.SetParent(content,false);button.gameObject.name="Card "+d.id;
                var rt=(RectTransform)button.transform;rt.anchorMin=rt.anchorMax=new Vector2(0,.5f);rt.pivot=new Vector2(.5f,.5f);
                rt.anchoredPosition=new Vector2(85+i*170,0);rt.sizeDelta=new Vector2(150,80);
                var old=button.GetComponent<TowerButton>();if(old!=null){old.enabled=false;Destroy(old);}
                button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>SelectCard(d.id));
                var text=button.GetComponentInChildren<TextMeshProUGUI>();
                text.gameObject.name="Card Label";if(Font!=null)text.font=Font;
                text.raycastTarget=false;text.fontSize=14;text.alignment=TextAlignmentOptions.Center;
                var tr=text.rectTransform;tr.anchorMin=tr.anchorMax=tr.pivot=new Vector2(.5f,.5f);tr.anchoredPosition=new Vector2(26,0);tr.sizeDelta=new Vector2(91,74);
                Art(rt,"Card Art",new Vector2(-47,0),new Vector2(48,70),GameArt.Tower(d.id));
                cards[d.id]=button;cardTexts[d.id]=text;cardColors[d.id]=button.GetComponent<Image>().color;
                var drag=button.gameObject.AddComponent<BattleCardDrag>();drag.Owner=this;drag.UnitId=d.id;
            }
            foreach(var template in templates)Destroy(template);
            for(int i=configuration.towers.Length;i<originals.Length;i++)originals[i].gameObject.SetActive(false);
            Action<int> page=direction=>
            {
                float next=scroll.horizontalNormalizedPosition+direction*Mathf.Min(.5f,510f/Mathf.Max(1,content.sizeDelta.x-600));
                if(direction>0&&scroll.horizontalNormalizedPosition>=.999f)next=0;
                if(direction<0&&scroll.horizontalNormalizedPosition<=.001f)next=1;
                scroll.horizontalNormalizedPosition=Mathf.Clamp01(next);
            };
            ButtonAt(canvasRect,"Previous Cards","‹",new Vector2(-332,-450),new Vector2(40,80),()=>page(-1));
            ButtonAt(canvasRect,"Next Cards","›",new Vector2(332,-450),new Vector2(40,80),()=>page(1));
        }
        private void BuildSelectionPopup()
        {
            // Contextual controls, shown only while a card or deployed unit is selected.
            var side=Panel(canvasRect,"Unit Detail",new Vector2(-700,20),new Vector2(330,530),paper);
            UIStyler.ApplyCardStyle(side.gameObject);detailPanel=side.gameObject;
            side.GetComponent<Image>().raycastTarget=true;
            detailPortrait=Art(side,"Selected Portrait",new Vector2(-113,213),new Vector2(55,62),null);
            selectedName=Label(side,"Unit Name",new Vector2(22,216),new Vector2(205,45),20,ink,TextAlignmentOptions.Left);
            var detailViewport=Panel(side,"Detail Scroll",new Vector2(0,20),new Vector2(298,310),paper);
            detailViewport.GetComponent<Image>().raycastTarget=true;detailViewport.gameObject.AddComponent<RectMask2D>();
            detailScroll=detailViewport.gameObject.AddComponent<ScrollRect>();
            details=Label(detailViewport,"Details",Vector2.zero,new Vector2(280,310),16,ink,TextAlignmentOptions.TopLeft);
            details.rectTransform.anchorMin=details.rectTransform.anchorMax=details.rectTransform.pivot=new Vector2(.5f,1);
            detailScroll.viewport=detailViewport;detailScroll.content=details.rectTransform;
            detailScroll.horizontal=false;detailScroll.vertical=true;detailScroll.movementType=ScrollRect.MovementType.Clamped;
            upgrade=ButtonAt(side,"Upgrade","升级",new Vector2(-76,-172),new Vector2(140,42),Upgrade);
            revive=ButtonAt(side,"Revive","重新部署",new Vector2(76,-172),new Vector2(140,42),Revive);
            withdraw=ButtonAt(side,"Withdraw","撤回",new Vector2(-76,-224),new Vector2(140,42),Withdraw);
            ButtonAt(side,"Close Detail","关闭",new Vector2(76,-224),new Vector2(140,42),CancelSelection);
            detailPanel.SetActive(false);
        }
        private void UpdateBoardProjection()
        {
            Canvas.ForceUpdateCanvases();
            var camera=Camera.main;lastCanvasSize=canvasRect.rect.size;
            CellSize=canvasRect.rect.height/(2*camera.orthographicSize);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect,camera.WorldToScreenPoint(Vector3.zero),null,out var origin);
            board.anchoredPosition=origin;board.sizeDelta=new Vector2(11*CellSize,9*CellSize);
            foreach(var cell in configuration.ground.Concat(configuration.highGround))
            {
                var rect=cells[cell.x+","+cell.y].rectTransform;
                rect.anchoredPosition=BoardPoint(cell.x,cell.y);rect.sizeDelta=Vector2.one*(CellSize-3);
            }
            var first=configuration.path[0];var last=configuration.path[configuration.path.Length-1];
            ((RectTransform)board.Find("Entrance")).anchoredPosition=BoardPoint(first.x,first.y)+Vector2.up*35;
            ((RectTransform)board.Find("Goal")).anchoredPosition=BoardPoint(last.x,last.y)+Vector2.up*35;
        }
        private bool ModalOpen()=>menuPanel.activeSelf||resultPanel.activeSelf||(rosterPanel!=null&&rosterPanel.activeSelf)||(suppliesPanel!=null&&suppliesPanel.activeSelf);
        private void OpenSupplies()
        {
            if(ModalOpen())return;
            suppliesWasPaused=Simulation.Paused;Simulation.Paused=true;
            if(suppliesPanel!=null){suppliesPanel.SetActive(true);return;}
            var panel=ModalDialog("Battle Supplies",new Vector2(1240,800));
            suppliesPanel=panel.gameObject;
            Label(panel,"Supplies Title",new Vector2(0,355),new Vector2(1050,60),32,ink).text="本局补给";
            var items=(configuration.battleItems??Array.Empty<string>()).Select(BattleItemDefs.Find).Where(item=>item!=null).ToArray();
            if(items.Length==0)
                Label(panel,"No Supplies",Vector2.zero,new Vector2(1000,160),23,ink).text="本局没有携带补给。\n在小卖部购买或通过对决取得的道具，会在下一天各携带一件。\n同一天重新挑战会保留已携带的效果。";
            else
            {
                var viewport=Panel(panel,"Supplies Viewport",new Vector2(0,0),new Vector2(1100,540),paper);
                viewport.GetComponent<Image>().raycastTarget=true;viewport.gameObject.AddComponent<RectMask2D>();
                var scroll=viewport.gameObject.AddComponent<ScrollRect>();
                var content=Rect(viewport,"Supplies List",Vector2.zero,new Vector2(1060,Mathf.Max(540,items.Length*96)));
                content.anchorMin=content.anchorMax=content.pivot=new Vector2(.5f,1);
                scroll.viewport=viewport;scroll.content=content;scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;
                for(int i=0;i<items.Length;i++)
                {
                    var row=Rect(content,"Supply "+items[i].key,new Vector2(0,-48-i*96),new Vector2(1020,90));
                    row.anchorMin=row.anchorMax=new Vector2(.5f,1);
                    Art(row,"Supply Art",new Vector2(-440,0),new Vector2(70,74),GameArt.Item(items[i].key));
                    Label(row,"Supply Effect",new Vector2(45,0),new Vector2(850,80),21,ink,TextAlignmentOptions.Left).text="<b>"+items[i].name+"</b>\n"+items[i].description.Replace("下局","本局");
                }
            }
            ButtonAt(panel,"Close Supplies","返回对决",new Vector2(0,-355),new Vector2(260,55),CloseSupplies);
        }
        private void CloseSupplies(){suppliesPanel.SetActive(false);Simulation.Paused=suppliesWasPaused;}
        private void OpenRoster()
        {
            if(started||Simulation.Finished||ModalOpen())return;
            if(rosterPanel!=null){rosterPanel.SetActive(true);return;}
            var allTowers=JsonUtility.FromJson<UnitCatalog>(Resources.Load<TextAsset>("Battle/Towers").text).units;
            var allEnemies=JsonUtility.FromJson<UnitCatalog>(Resources.Load<TextAsset>("Battle/Enemies").text).units;
            var recommended=new[]{"counter_shield","counter_store","pursuit_strike","pursuit_support","poison_amp","burn_burst","heal_aura","heal_support"};
            var chosen=new HashSet<string>(configuration.towers.Length==8?configuration.towers.Select(t=>t.id):recommended.Select(id=>"tower_"+id));
            var panel=ModalDialog("Choose Roster",new Vector2(1240,800));rosterPanel=panel.gameObject;
            Label(panel,"Roster Title",new Vector2(0,335),new Vector2(1000,65),34,ink).text="选择 8 种棋子组成阵容";
            var counter=Label(panel,"Roster Count",new Vector2(0,275),new Vector2(1000,40),20,ink);
            counter.text="已选 8 / 8 · 可调整推荐阵容，对手使用剩余 8 种棋子";
            var rosterCards=new Dictionary<string,Button>();
            for(int i=0;i<allTowers.Length;i++)
            {
                var d=allTowers[i];
                var button=ButtonAt(panel,"Pick "+d.id,"",new Vector2((i%4-1.5f)*265,175-i/4*100),new Vector2(250,84),()=>
                {
                    if(!chosen.Remove(d.id))
                    {
                        if(chosen.Count==8){counter.text="阵容已满，请先取消一个已选棋子，再加入新的棋子。";return;}
                        chosen.Add(d.id);
                    }
                    foreach(var entry in rosterCards)entry.Value.GetComponent<Image>().color=chosen.Contains(entry.Key)?UITheme.Instance.secondary:UITheme.Instance.card;
                    counter.text=$"已选 {chosen.Count} / 8 · 对手使用剩余棋子";
                });
                Art(button.transform,"Draft Art",new Vector2(-77,0),new Vector2(72,72),GameArt.Tower(d.id));
                Label(button.transform,"Draft Label",new Vector2(38,0),new Vector2(150,70),18,ink).text=d.name+"\n<size=14>"+d.tag+" · "+d.deployCost+" COST</size>";
                rosterCards[d.id]=button;button.GetComponent<Image>().color=chosen.Contains(d.id)?UITheme.Instance.secondary:UITheme.Instance.card;
            }
            ButtonAt(panel,"Cancel Roster",requiresRoster?"返回主菜单":"返回关卡",new Vector2(-180,-325),new Vector2(270,55),()=>{if(requiresRoster)SceneLoader.LoadMainMenu();else rosterPanel.SetActive(false);});
            ButtonAt(panel,"Confirm Roster","确认阵容并进入对决",new Vector2(180,-325),new Vector2(310,55),()=>
            {
                if(chosen.Count!=8){counter.text="需要选择 8 种棋子";return;}
                FinalDefense.Battle.BattleSceneBootstrap.NextBattle=BattleFactory.CreateCampaignDuel(configuration,allTowers,allEnemies,chosen);
                SceneLoader.LoadBattle();
            });
        }
        private void BuildOverlays()
        {
            var menu=Panel(canvasRect,"Pause Menu",Vector2.zero,new Vector2(1920,1080),new Color(0,0,0,.7f));menuPanel=menu.gameObject;
            menu.GetComponent<Image>().raycastTarget=true;
            Label(menu,"Pause Title",new Vector2(0,135),new Vector2(480,70),38,Color.white).text="对决已暂停";
            ButtonAt(menu,"Continue","继续对决",new Vector2(0,30),new Vector2(280,55),ToggleMenu);
            menuRestart=ButtonAt(menu,"Restart","放弃本局并重新挑战",new Vector2(0,-45),new Vector2(320,55),Restart);
            menuExit=ButtonAt(menu,"Exit","放弃本局 · 返回主菜单",new Vector2(0,-120),new Vector2(320,55),ExitToMainMenu);
            menuPanel.SetActive(false);
            var result=Panel(canvasRect,"Battle Result",Vector2.zero,new Vector2(1920,1080),new Color(0,0,0,.85f));resultPanel=result.gameObject;
            result.GetComponent<Image>().raycastTarget=true;
            resultText=Label(result,"Report",new Vector2(0,100),new Vector2(820,330),26,Color.white);
            retry=ButtonAt(result,"Retry","再挑战一次",new Vector2(-180,-150),new Vector2(280,55),Restart);
            ButtonAt(result,"Next","前往结算",new Vector2(180,-150),new Vector2(280,55),()=>SceneLoader.LoadResult());
            resultPanel.SetActive(false);
        }
        private void Render()
        {
            rosterButton.interactable=!started&&!ModalOpen();
            start.interactable=!requiresRoster&&!Simulation.Finished&&!ModalOpen();
            var hover=PreviewCell();var selected=SelectedDefinition();
            bool preview=selectedId!=null&&hover!=null&&!ModalOpen();
            placementPreview.gameObject.SetActive(preview);
            if(preview)
            {
                placementPreview.anchoredPosition=BoardPoint(hover.x,hover.y);
                bool valid=(selected.highGround?configuration.highGround:configuration.ground).Any(c=>c.x==hover.x&&c.y==hover.y)
                    &&!Simulation.Towers.Any(t=>t.alive&&t.x==hover.x&&t.y==hover.y)&&Simulation.Cost>=selected.deployCost
                    &&Simulation.Towers.Count(t=>t.alive)<configuration.maxDeployed&&!Simulation.Finished;
                placementPreview.GetComponent<Image>().color=valid?new Color(.2f,.65f,.4f,.8f):new Color(.9f,.25f,.25f,.8f);
                placementPreviewText.text=new[]{"→","↓","←","↑"}[facing];
                previewPortrait.sprite=GameArt.Tower(selected.id);
            }
            if(canvasRect.rect.size!=lastCanvasSize)UpdateBoardProjection();
            hud.RefreshSimulation();
            foreach(var c in configuration.ground.Concat(configuration.highGround))
            {
                Color color=Color.clear;
                if(Simulation.PoisonTiles.Any(p=>p.x==c.x&&p.y==c.y))color=new Color(.64f,.79f,.29f,.45f);
                var active=selectedUnit;
                var d=SelectedDefinition();
                if(d!=null && active!=null && Simulation.InRange(active,c.x,c.y))color=Color.Lerp(color,blue,.3f);
                if(preview)
                {var dir=FacingVector();if(CombatMath.InRange(hover.x,hover.y,dir.x,dir.y,c.x,c.y,selected.rangeWidth,selected.rangeDepth,selected.radial))color=Color.Lerp(color,blue,.35f);}
                cells[c.x+","+c.y].color=color;
            }
            foreach(var u in Simulation.Towers.Concat(Simulation.Enemies))RenderUnit(u);
            foreach(var d in configuration.towers)
            {
                cards[d.id].GetComponent<Image>().color=selectedId==d.id?Color.Lerp(cardColors[d.id],Color.white,.3f):Simulation.Cost<d.deployCost?Color.Lerp(cardColors[d.id],Color.gray,.55f):cardColors[d.id];
                cardTexts[d.id].text=$"{d.name}\n<size=11>{d.tag}·{(d.highGround?"高台":"地面")}</size>\n<b>{d.deployCost} COST</b>";
            }
            int projectileIndex=0;
            foreach(var p in Simulation.Projectiles.Where(p=>p.speed>0))
            {
                if(projectileIndex==projectileVisuals.Count)
                    projectileVisuals.Add(Panel(effectsLayer,"Projectile",Vector2.zero,new Vector2(9,9),Color.white).GetComponent<Image>());
                var dot=projectileVisuals[projectileIndex++];
                dot.gameObject.SetActive(true);dot.rectTransform.anchoredPosition=BoardPoint(p.x,p.y);
                dot.color=p.healing?new Color(.26f,.70f,.38f):p.source.enemy?new Color(.95f,.35f,.28f):blue;
            }
            for(int i=projectileIndex;i<projectileVisuals.Count;i++)projectileVisuals[i].gameObject.SetActive(false);
            RenderDetails();
            while(Simulation.Events.Count>0)
            {
                var e=Simulation.Events.Dequeue();
                if(e.kind=="wave"||e.kind=="skill"||e.kind=="down"||e.kind=="enemy-skill")eventText.text=$"{e.time:0.0}s  {e.text}";
            }
            if(UnityEngine.Time.unscaledTime>hintUntil)hint.text=started?(Simulation.Paused?"暂停中 · 空格继续":"拖动或点击卡牌部署 · R 调整朝向 · 右键取消") : $"先部署棋子，再开始对决。下方可查看 {configuration.towers.Length} 种棋子。";
            if(Simulation.Finished&&!reported)ShowResult();
        }
        private void RenderUnit(UnitState u)
        {
            if(!visuals.TryGetValue(u.id,out var v))
            {
                var rect=Rect(unitLayer,"Unit "+u.id,BoardPoint(u.x,u.y),new Vector2(54,61));
                var target=rect.gameObject.AddComponent<Image>();target.color=Color.clear;
                var b=rect.gameObject.AddComponent<Button>();b.targetGraphic=target;b.transition=Selectable.Transition.None;
                b.onClick.AddListener(()=>SelectUnit(u));
                v=new UnitVisual {root=(RectTransform)b.transform};
                v.body=Panel(v.root,"Faction Base",new Vector2(0,-18),new Vector2(49,15),u.enemy?new Color(.82f,.40f,.29f):new Color(.36f,.59f,.46f)).GetComponent<Image>();
                v.portrait=Art(v.root,"Unit Art",new Vector2(0,5),new Vector2(57,58),UnitSprite(u));
                v.label=Label(v.root,"Name",new Vector2(0,-30),new Vector2(70,17),12,Color.white);
                Panel(v.root,"HP Track",new Vector2(0,39),new Vector2(52,5),new Color(.2f,.2f,.2f));
                v.health=Panel(v.root,"HP",new Vector2(0,39),new Vector2(52,5),new Color(.32f,.68f,.42f)).GetComponent<Image>();
                v.shield=Panel(v.root,"Shield",new Vector2(0,44),new Vector2(52,3),new Color(.48f,.78f,1f)).GetComponent<Image>();
                v.sp=Panel(v.root,"SP",new Vector2(0,-30),new Vector2(52,3),new Color(1f,.75f,.2f)).GetComponent<Image>();
                v.sp.rectTransform.anchoredPosition=new Vector2(0,-40);
                visuals[u.id]=v;
            }
            v.root.gameObject.SetActive(u.alive);if(!u.alive)return;
            v.root.anchoredPosition=BoardPoint(u.x,u.y);
            v.root.localScale=Vector3.one*(u.scale>1?1.2f:1);
            v.body.color=u.downed?new Color(.45f,.47f,.52f):u.enemy?new Color(.76f,.25f,.23f):TagColor(u.Tag);
            v.portrait.color=u.downed?new Color(.55f,.55f,.55f,.65f):Color.white;
            if(u.frozenUntil>Simulation.Time)v.portrait.color=new Color(.6f,.82f,1);
            if(u.lastHitAt>0 && Simulation.Time-u.lastHitAt<.12f)v.portrait.color=new Color(1,.6f,.55f);
            if(v.attacks!=u.TotalAttacks){v.attacks=u.TotalAttacks;v.poseUntil=Simulation.Time+.16f;}
            float strike=Simulation.Time<v.poseUntil?3:0;
            v.portrait.rectTransform.anchoredPosition=new Vector2(u.facingX*strike,5+u.facingY*strike+(u.downed?0:Mathf.Sin(Simulation.Time*3+u.id)*1.1f));
            string shortName=u.definition.name.Length>4?u.definition.name.Substring(0,4):u.definition.name;
            string arrow=u.facingX>0?"→":u.facingX<0?"←":u.facingY>0?"↑":"↓";
            v.label.text=u.enemy?(u.scale>=2?"首领":u.scale>1?"精英":shortName.Replace("E_","")):$"{(u.downed?"倒地":"Lv."+u.level)} {arrow}";
            SetBar(v.health,u.hp/Simulation.MaxHP(u));SetBar(v.shield,u.shield/Simulation.MaxHP(u));
            float maxSp=u.definition.skill==null?0:u.definition.skill.spCost;
            SetBar(v.sp,u.skillActive?1:maxSp>0?u.sp/maxSp:0);
        }
        private void SetBar(Image image,float fraction)
        {
            var rect=(RectTransform)image.transform;rect.sizeDelta=new Vector2(52*Mathf.Clamp01(fraction),rect.sizeDelta.y);
            rect.anchoredPosition=new Vector2(-26+rect.sizeDelta.x*.5f,rect.anchoredPosition.y);
        }
        private UnitDefinition SelectedDefinition() => selectedUnit!=null?selectedUnit.definition:configuration.towers.FirstOrDefault(d=>d.id==selectedId);
        private void RenderDetails()
        {
            var d=SelectedDefinition();var u=selectedUnit;
            detailPanel.SetActive(d!=null&&!dragging&&!ModalOpen());
            if(d==null)return;
            detailPortrait.sprite=u!=null?UnitSprite(u):GameArt.Tower(d.id);
            upgrade.interactable=u!=null&&!u.enemy&&!u.downed&&u.level<3&&!Simulation.Finished;
            revive.interactable=u!=null&&!u.enemy&&u.downed&&Simulation.Time>=u.reviveAt&&!Simulation.Finished;
            withdraw.interactable=u!=null&&!u.enemy&&u.alive&&!u.downed&&!Simulation.Finished;
            selectedName.text=d.name;
            int level=u==null?1:Math.Max(1,u.level);
            string stats=u==null?$"HP {d.AtLevel(d.hp,level)}   ATK {d.AtLevel(d.attack,level)}   DEF {d.AtLevel(d.defense,level)}":$"HP {u.hp:0}/{Simulation.MaxHP(u)}   盾 {u.shield:0}\nATK {Simulation.Attack(u)}   DEF {Simulation.Defense(u)}   阻挡 {Simulation.BlockCapacity(u)}";
            string skill=d.skill==null?"无技能":$"<b>{d.skill.name}</b>\n{d.skill.description}";
            string sp=u==null?"":u.downed?$"\n重新部署冷却 {Math.Max(0,u.reviveAt-Simulation.Time):0.0}s":$"\nSP {u.sp:0.0}/{d.skill?.spCost:0} {(u.skillActive?"施放中 "+u.skillRemaining.ToString("0.0")+"s":"")}";
            string direction=u==null?FacingText():u.facingX>0?"右 →":u.facingX<0?"左 ←":u.facingY>0?"上 ↑":"下 ↓";
            SetDetails($"<color=#64748B>{d.tag} · {(d.highGround?"高台":"地面")} · 朝向 {direction}</color>\n{stats}{sp}\n\n<size=16>{skill}</size>");
            SetButtonText(upgrade,u==null||u.enemy?"升级":u.downed?"倒地需重部署":u.level<3&&d.upgradeCost!=null?$"升级 · {d.upgradeCost[u.level]} COST":"已达最高等级");
            SetButtonText(revive,u!=null&&u.downed?$"部署 {d.deployCost} COST":"重新部署");
        }
        private void SetDetails(string value)
        {
            if(details.text==value)return;
            details.text=value;
            details.rectTransform.sizeDelta=new Vector2(280,Mathf.Max(310,details.preferredHeight+12));
        }
        public void SelectCard(string id)
        {if(ModalOpen())return;selectedId=id;selectedUnit=null;detailScroll.verticalNormalizedPosition=1;ShowHint("点击合法地块部署 · 当前朝向 "+FacingText());RenderDetails();}
        private void SelectUnit(UnitState u)
        {if(ModalOpen())return;if(dragging||selectedId!=null){CellClicked(Mathf.RoundToInt(u.x),Mathf.RoundToInt(u.y));return;}selectedUnit=u;selectedId=null;detailScroll.verticalNormalizedPosition=1;RenderDetails();}
        private void CellClicked(int x,int y)
        {
            if(selectedId==null||ModalOpen())return;
            var dir=FacingVector();var u=Simulation.TryDeploy(selectedId,x,y,dir.x,dir.y);
            if(u==null)ShowHint(Simulation.LastError);else {selectedId=null;selectedUnit=u;ShowHint("已部署 "+u.definition.name);}
            Render();
        }
        public void BeginCardDrag(string id){dragging=true;SelectCard(id);}
        public void DragCard(Vector2 point){dragPosition=point;}
        private Cell PreviewCell()
        {
            Vector2 screen=dragging?dragPosition:Mouse.current!=null?Mouse.current.position.ReadValue():new Vector2(-10000,-10000);
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(board,screen,null,out var point))
            {int x=Mathf.RoundToInt(point.x/CellSize+5),y=Mathf.RoundToInt(point.y/CellSize+4);if(x>=0&&x<=10&&y>=0&&y<=8)return new Cell(x,y);}
            return null;
        }
        public void EndCardDrag(Vector2 screenPosition)
        {
            dragging=false;
            if(RectTransformUtility.ScreenPointToLocalPointInRectangle(board,screenPosition,null,out var local))
            {int x=Mathf.RoundToInt(local.x/CellSize+5);int y=Mathf.RoundToInt(local.y/CellSize+4);CellClicked(x,y);}
        }
        private Vector2 BoardPoint(float x,float y)=>new Vector2((x-5)*CellSize,(y-4)*CellSize);
        private void CancelSelection(){selectedId=null;selectedUnit=null;detailPanel.SetActive(false);ShowHint("已取消选择");}
        private Vector2Int FacingVector()=>facing==0?Vector2Int.right:facing==1?Vector2Int.down:facing==2?Vector2Int.left:Vector2Int.up;
        private string FacingText()=>new[]{"右 →","下 ↓","左 ←","上 ↑"}[facing];
        private void Rotate(){if(ModalOpen())return;facing=(facing+1)%4;ShowHint("部署朝向："+FacingText());}
        private void Upgrade(){if(ModalOpen())return;if(selectedUnit!=null&&!Simulation.TryUpgrade(selectedUnit))ShowHint("无法升级：等级已满或 COST 不足");Render();}
        private void Revive(){if(ModalOpen())return;if(selectedUnit!=null&&!Simulation.TryRedeploy(selectedUnit))ShowHint("冷却未结束或 COST 不足");Render();}
        private void Withdraw(){if(ModalOpen())return;if(selectedUnit!=null)Simulation.Withdraw(selectedUnit);selectedUnit=null;Render();}
        private void TogglePause()
        {
            if(Simulation.Finished||requiresRoster||ModalOpen())return;started=true;Simulation.Paused=!Simulation.Paused;
            SetButtonText(start,Simulation.Paused?"继续对决":"暂停  Space");
        }
        private void ToggleMenu()
        {
            if(Simulation.Finished)return;
            if(ModalOpen()&&!menuPanel.activeSelf)return;
            if(menuPanel.activeSelf){menuPanel.SetActive(false);Simulation.Paused=menuWasPaused;}
            else
            {
                menuWasPaused=Simulation.Paused;Simulation.Paused=true;menuPanel.SetActive(true);
                var gm=GameManager.Instance;
                SetButtonText(menuRestart,!started?"重置战前部署":gm!=null&&gm.RetryCount>0?"放弃并重试 · 5 GPA":"放弃并重试 · 首次免费");
                SetButtonText(menuExit,started?"放弃本局 · 返回主菜单":"返回主菜单");
            }
        }
        private void ShowHint(string message){hint.text=message;hintUntil=UnityEngine.Time.unscaledTime+3;}
        private void Restart()
        {
            var gm=GameManager.Instance;
            if(started&&!Simulation.Finished){Simulation.Forfeit();ShowResult();menuPanel.SetActive(false);}
            if(Simulation.Finished&&(gm==null||!gm.TryRetryBattle())){ShowHint("本次对决已结算，请继续下一步");return;}
            FinalDefense.Battle.BattleSceneBootstrap.NextBattle=configuration;SceneLoader.LoadBattle();
        }
        private void ExitToMainMenu()
        {
            CommitAbandonedBattle();SceneLoader.LoadMainMenu();
        }
        private void CommitAbandonedBattle()
        {
            if(Simulation==null||!started||Simulation.Finished)return;
            Simulation.Forfeit();GameManager.Instance?.CompleteBattle(Simulation.Report);
        }
        private void OnApplicationQuit()
        {
            if(!Application.isEditor)CommitAbandonedBattle();
        }
        private void ShowResult()
        {
            reported=true;Simulation.Paused=true;
            var r=Simulation.Report;var gm=GameManager.Instance;
            if(gm!=null)
            {
                gm.CompleteBattle(r);
            }
            retry.interactable=!r.won&&gm!=null&&!gm.IsGameComplete&&(gm.RetryCount==0||gm.CurrentGPA>5);
            SetButtonText(retry,r.won?"对决已完成":gm!=null&&gm.RetryCount>0?"再次挑战 · 5 GPA":"免费再挑战");
            FinalDefense.UI.ResultScreenUI.SetResult(r.won);
            resultText.text=$"<size=42>{(r.won?"对决胜利":"保护点失守")}</size>\n\n用时 {r.duration:0.0} 秒    击败 {r.killed}/{r.spawned}    漏怪 {r.leaked}\n部署 {r.deployed}    升级 {r.upgrades}    花费 {r.costSpent} COST\n总伤害 {r.damage:0}    总治疗 {r.healing:0}\n保护损失 −{r.protectionLost} GPA{(r.won?"    胜利奖励 +"+(5+r.bonusGpa)+" GPA":"")}\n\n{(r.won?"完成本次对决，可前往结算与商店。":"调整阻挡、治疗和部署朝向，再挑战一次。")}";
            resultPanel.SetActive(true);
            FinalDefense.Audio.BackgroundMusic.PlayBattleResult();
        }
        private Color TagColor(string tag)
        {
            switch(tag){case "灼烧":return new Color(.79f,.34f,.17f);case "中毒":return new Color(.35f,.52f,.19f);case "治疗":return new Color(.13f,.52f,.46f);case "盾反":return new Color(.35f,.41f,.54f);case "控制":return new Color(.3f,.43f,.77f);case "削弱":return new Color(.55f,.34f,.68f);default:return blue;}
        }
        private Image Art(Transform parent,string name,Vector2 position,Vector2 size,Sprite sprite)
        {
            var rect=Rect(parent,name,position,size);
            var image=rect.gameObject.AddComponent<Image>();
            image.sprite=sprite;image.preserveAspect=true;image.raycastTarget=false;
            return image;
        }
        private Sprite UnitSprite(UnitState unit)
        {
            // The supplied pack contains friendly pieces. Stage enemies reuse those
            // illustrations with a red faction base until an enemy art pack is supplied.
            switch(unit.definition.id)
            {
                case "stage_small":return GameArt.Tower("direct_crit");
                case "stage_runner":return GameArt.Tower("pursuit_strike");
                case "stage_shield":return GameArt.Tower("counter_shield");
                case "stage_elite":return GameArt.Tower("counter_store");
                default:return GameArt.Tower(unit.definition.id);
            }
        }
        private RectTransform Rect(Transform parent,string name,Vector2 pos,Vector2 size)
        {
            var go=new GameObject(name,typeof(RectTransform));go.transform.SetParent(parent,false);
            var rt=(RectTransform)go.transform;rt.anchorMin=rt.anchorMax=rt.pivot=new Vector2(.5f,.5f);rt.anchoredPosition=pos;rt.sizeDelta=size;return rt;
        }
        private RectTransform Panel(Transform parent,string name,Vector2 pos,Vector2 size,Color color)
        {
            var rt=Rect(parent,name,pos,size);var image=rt.gameObject.AddComponent<Image>();
            image.color=color;image.raycastTarget=false;
            if(size.x<1550&&size.y>12)UIStyler.ApplyRoundedRect(rt.gameObject,size.y<90?UITheme.Instance.radiusButton:UITheme.Instance.radiusCard);
            return rt;
        }
        private RectTransform ModalDialog(string name,Vector2 size)
        {
            var overlay=Panel(canvasRect,name,Vector2.zero,new Vector2(1920,1080),new Color(0,0,0,.65f));
            overlay.anchorMin=Vector2.zero;overlay.anchorMax=Vector2.one;overlay.sizeDelta=Vector2.zero;
            overlay.GetComponent<Image>().raycastTarget=true;
            var sheet=Panel(overlay,"Dialog",Vector2.zero,size,UITheme.Instance.popover);
            UIStyler.ApplyCardStyle(sheet.gameObject);
            return overlay;
        }
        private TextMeshProUGUI Label(Transform parent,string name,Vector2 pos,Vector2 size,int fontSize,Color color,TextAlignmentOptions align=TextAlignmentOptions.Center)
        {
            var rt=Rect(parent,name,pos,size);var text=rt.gameObject.AddComponent<TextMeshProUGUI>();if(Font!=null)text.font=Font;
            text.fontSize=fontSize;text.color=color;text.alignment=align;text.raycastTarget=false;text.enableWordWrapping=true;return text;
        }
        private Button ButtonAt(Transform parent,string name,string label,Vector2 pos,Vector2 size,Action action)
        {
            bool primary=name=="Start"||name=="Next"||name=="Confirm Roster"||name=="Continue";
            var button=UIBootstrap.CreateButton(name,parent,label,primary);
            var rect=(RectTransform)button.transform;rect.anchoredPosition=pos;rect.sizeDelta=size;
            var text=button.GetComponentInChildren<TextMeshProUGUI>();
            if(text!=null){if(Font!=null)text.font=Font;text.raycastTarget=false;}
            if(label.Length==0)text.gameObject.SetActive(false);
            button.onClick.AddListener(()=>action());return button;
        }
        private void SetButtonText(Button b,string text){var label=b.GetComponentInChildren<TextMeshProUGUI>();if(label!=null)label.text=text;}
    }
    public sealed class BattleCardDrag : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public BattleView Owner;public string UnitId;
        public void OnBeginDrag(PointerEventData e){Owner.BeginCardDrag(UnitId);Owner.DragCard(e.position);}
        public void OnDrag(PointerEventData e){Owner.DragCard(e.position);}
        public void OnEndDrag(PointerEventData e){Owner.EndCardDrag(e.position);}
    }
}
