# 真实 Unity 全流程与 Mac 构建验收

这套验收从真实 MainMenu 进入新游戏，只通过屏幕射线命中的按钮和拖拽操作进入后续页面。允许加速战斗时间，不注入胜负、数值、费用、日期或技能状态。

## 范围

- MainMenu 首次“开始游戏”（已有进度时用“新游戏”重置）→ 10 道人格题 → 人格结果 → 主菜单继续 → 日程行动点。
- 无部署的真实失败 → Battle 结算遮罩 → Result → 免费重试。
- 通过实际卡牌、棋盘、朝向和升级按钮完成 41 敌胜局 → Result → 商店消费 → 下一天。
- 按 `GameManager.TotalDays` 跑完整日程，最后一天结束，再从终局界面重开，验证人格、日期、金币、胜负、重试和临时战斗配置清空。
- 原生导入 45 张提供的美术，校验资源路径、16 个角色身份和运行时裁剪边界。页面断言改为 Git `30b6d962` 的粉色主题、原背景和原卡片容器；非战斗页面只允许商店既有 56×56 图标槽使用新增素材，不要求整页立绘。
- 检查原 Canvas／顶部 HUD／600×100 TowerPanel，分页后卡牌至少90%位于实际窗口和裁剪区域中。所有点击点均须在真实窗口内且为 EventSystem 的最上层命中。
- 运行音乐5项图形回归，保留 `OnAudioFilterRead` PCM、暂停、循环和结果／重试切曲验证。
- 原 Unity 场景测试与纯逻辑测试仍独立保留。

**当前 28 天流程复用同一份 Level1（4 波、41 敌）关卡内容；通过完整循环不代表实现了 28 个不同关卡。**

## 运行

先创建独立项目副本，并同步当前 `Assets`、`Packages`、`ProjectSettings`；可复制已有 `Library` 作为导入缓存。下例副本路径仅为本机验收目录；不要在另一个正在打开的 Unity 项目目录启动第二个编辑器。

```bash
verification_project=/tmp/unitygame-compile.qRyRAD
verification_output=/tmp/final-defense-original-ui
verification_private=/tmp/final-defense-original-ui-private
unity_editor=/Applications/Unity/Hub/Editor/6000.5.1f1/Unity.app/Contents/MacOS/Unity

python3 Tools/prepare_ui_verification_copy.py --project-copy "$verification_project" \
  --evidence "$verification_output/test-identity-settings.json"
export FINAL_DEFENSE_TEST_COMPANY=CodexVerification
export FINAL_DEFENSE_TEST_PRODUCT=OriginalUI20260915

"$unity_editor" -batchmode -nographics -projectPath "$verification_project" \
  -runTests -testPlatform EditMode -testFilter FinalDefense.Tests \
  -testResults "$verification_output/editmode-results.xml" \
  -logFile "$verification_output/editmode.log"

FINAL_DEFENSE_RELEASE_CAPTURE_DIR="$verification_output" \
FINAL_DEFENSE_CAPTURE_DIR="$verification_output/battle-smoke" \
FINAL_DEFENSE_PREFS_BACKUP_DIR="$verification_private" \
FINAL_DEFENSE_CHECKPOINT_OUTPUT="$verification_private/day-two-checkpoint.json" \
FINAL_DEFENSE_AUDIO_OUTPUT="$verification_output/audio-measurements" \
"$unity_editor" -projectPath "$verification_project" \
  -runTests -testPlatform PlayMode -testFilter FinalDefense.Tests \
  -testResults "$verification_output/playmode-results.xml" \
  -logFile "$verification_output/playmode.log"
```

完整流程需要图形模式，不能加 `-batchmode` 或 `-nographics`；未设置截图目录时该长流程测试会明确跳过。动作记录输出到 `journey-actions.txt`，包括每次真实 Pointer／Drag、场景切换、战斗推进及每天战果。每张截图旁的 `.layout.json` 记录实际文字字号、矩形、首选高度、是否溢出及屏幕范围，供原布局复核。

## 两个 Unity 进程之间的恢复测试

完整图形套件中的 `ColdStartFromSerializedDayTwoSaveResumesThroughRealUI` 先通过真实 UI 首胜、买四水、进入次日并保存回主菜单。它原样导出专用测试域中的测试存档及 SHA-256／进程 ID。第一进程退出后，再启动一个全新 Unity 进程：

```bash
FINAL_DEFENSE_RELEASE_CAPTURE_DIR="$verification_output/cold-resume" \
FINAL_DEFENSE_PREFS_BACKUP_DIR="$verification_private" \
FINAL_DEFENSE_RESUME_SAVE_PATH="$verification_private/day-two-checkpoint.json" \
"$unity_editor" -projectPath "$verification_project" \
  -runTests -testPlatform PlayMode \
  -testFilter FinalDefense.Tests.ReleaseFlowTests.ColdStartFromSerializedDayTwoSaveResumesThroughRealUI \
  -testResults "$verification_output/cold-resume-results.xml" \
  -logFile "$verification_output/cold-resume.log"
```

第二进程把同一份原样 JSON 放入隔离的编辑器 PlayerPrefs，在加载 MainMenu 前完成；生产 UI 自己执行存档恢复。测试检查进程 ID 不同、输入哈希相同、GPA／四水库存正确，然后实际点击继续、日程、编队，启动完整战斗并赢得 41 杀、0 漏怪。该回归专门覆盖先前“null battle 被 JsonUtility 还原为空配置、恢复后战斗白屏”的缺口，不依赖上一个进程的单例或静态变量。

导出的测试存档只留在临时目录；归档保留哈希、进程 ID、UI 动作、截图与 XML。`manual/` 中发现问题的原生成品证据及 `baseline-before-save-fix/` 历史报告必须保留。

## 存档隔离

仅复制项目不能隔离 PlayerPrefs。本轮先在副本中把 company/product/bundle 改为专用测试身份；原工作区不变。编辑器预期使用 `unity.CodexVerification.OriginalUI20260915`，独立测试 Mac 使用 `com.codexverification.originalui20260915`。正式产品仍为 `com.DefaultCompany.2D-URP`，原编辑器为 `unity.DefaultCompany.game`；本轮不备份、清空或恢复这两个用户域。

`CampaignSaveIsolation` 先检查环境变量中的预期 company/product，确认专用域配置后，才在该测试域备份并清除 `FinalDefense.Campaign.v1` 单键，测试后恢复。备份保留在 `FINAL_DEFENSE_PREFS_BACKUP_DIR`，正常退出删除；不得归档私有备份或测试存档。绝不能拿旧手动试玩保险覆盖用户之后的新进度。

## 构建本机 Mac 成品

安装目录已经包含 `MacStandaloneSupport`。在独立项目副本运行正常 Player 构建；工具要求构建列表首场景为 MainMenu，不包含测试程序集：

```bash
FINAL_DEFENSE_MAC_BUILD_PATH="$PWD/Builds/Mac/OriginalUIVerification.app" \
"$unity_editor" -batchmode -nographics -quit \
  -projectPath "$verification_project" \
  -executeMethod FinalDefense.Tests.ReleasePlayerBuilder.BuildMacPlayer \
  -logFile "$verification_output/mac-build.log"
```

先用测试身份构建独立试玩应用，记录新偏好域；它与用户正在运行的旧版本可以同时存在。然后将副本 `ProjectSettings` 重新同步为原工作区版本，重新核对输入哈希，再使用同一命令把 `FINAL_DEFENSE_MAC_BUILD_PATH` 改为 `Builds/Mac/Staging/FinalDefense.app` 构建正式身份成品。不要覆盖正在运行的 `FinalDefense.app`。

生成 `.app` 及同目录 `mac-build-summary.json`；分别及时归档两份摘要，避免后一构建覆盖前一摘要。`Builds/` 已由项目忽略；不发布远端。构建成功还需要实际启动独立试玩成品、检查窗口和输入，不能把编译成功当成可玩验收。

本轮最终结果、截图及对应生产／数据／美术哈希归档到 `Docs/OriginalUIVerification/`，明确恢复前后版本。保留历史 `Docs/ReleaseVerification/`、`Docs/Audio/Verification/`；旧截图不能当成本轮通过证据。

### 成品人工试玩的存档保护

本轮只检查独立测试域，不修改用户正在推进的正式存档：

```bash
python3 Tools/campaign_prefs.py status --domain com.codexverification.originalui20260915
# 启动独立 OriginalUIVerification.app 并新建测试局，再次 status 核实专用域。
```

只有用户以后明确要求操作正式存档时，才另行使用 `campaign_prefs.py` 的单键备份／恢复功能；本轮不使用该流程。

### 最终归档

归档前核对 EditMode／完整 PlayMode／冷启动 XML 均零失败、零跳过，生产代码和资源哈希与冻结输入一致；单独记录测试身份的三个设置差异和 Unity 导入生成差异。正式构建必须恢复原身份并再次核对原输入。归档精简 XML、离线结果、真实截图、文字布局记录、动作记录、两个应用哈希及构建摘要，不复制 Library、完整 Unity 日志或私有存档。
