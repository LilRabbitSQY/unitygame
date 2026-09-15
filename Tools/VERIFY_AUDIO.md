# Unity 背景音乐验证

使用 Unity 6000.5.1f1，在独立项目副本同步 `Assets`、`Packages`、`ProjectSettings` 并复用副本自己的 `Library`。运行前记录这些输入的 SHA-256；测试或构建之后另记 Unity 自动序列化的差异。不要在用户正在打开的原项目上执行 Test Runner。

## 测试

资源测试 `FinalDefense.Tests.BackgroundMusicResourceTests` 通过真实 `AudioImporter` 检查三首音乐的立体声、44.1 kHz、时长、Streaming、Vorbis 0.7、后台加载和禁用预载。可以使用 `-batchmode -nographics` 的 EditMode Test Runner。

`FinalDefense.Tests.BackgroundMusicTests` 必须使用带图形界面的 Unity，不能使用 `-nographics`。测试为每个真实 `AudioSource` 加入仅测试程序集中的 `OnAudioFilterRead` 探针，记录音频线程回调、PCM 样本数、声道数及 RMS；探针不修改音频。它证明 Unity 已实际解码出非静音 PCM，不能代替扬声器录音或主观音质检查。

```bash
FINAL_DEFENSE_AUDIO_OUTPUT=/tmp/audio-run/measurements \
FINAL_DEFENSE_PREFS_BACKUP_DIR=/tmp/audio-private \
"/Applications/Unity/Hub/Editor/6000.5.1f1/Unity.app/Contents/MacOS/Unity" \
  -projectPath /tmp/your-isolated-unity-project \
  -runTests -testPlatform PlayMode \
  -testFilter FinalDefense.Tests.BackgroundMusicTests \
  -testResults /tmp/audio-run/audio-playmode-results.xml \
  -logFile /tmp/audio-run/audio-playmode.log
```

覆盖场景路由、主菜单到商店的同曲游标连续、暂停下播放、三首音乐原生循环越过尾部、快速反向淡入淡出、保留对象时重复 RuntimeInit、音量钳制，以及真实战斗结果和 UI 重试切曲。循环用例通过 `AudioSource.time` 将位置移到尾部，再等待真实 DSP 时钟跨越边界。重复 RuntimeInit 用例直接调用运行时初始化入口，模拟保留对象；没有修改编辑器 Domain Reload 偏好设置。

可另跑 `FinalDefense.Tests.BattleSceneSmokeTests` 的五项既有图形烟测；本轮无需重复 28 天长流程。离线编译及规则回归仍使用 `python3 Tools/verify_battle.py --mode all --output /tmp/audio-run/offline`。

## 存档与构建

PlayMode 的 `CampaignSaveIsolation` 只备份、清空及恢复 `unity.DefaultCompany.game` 中的 `FinalDefense.Campaign.v1` 单键。新音频用例在此保险范围内各自重置临时测试存档。它们不触碰正常 Mac 成品的 `com.DefaultCompany.2D-URP` 域，不改变系统音量。测试暂时解除编辑器 Game View 静音，结束后恢复。

全套音频检查通过后，使用已有 `FinalDefense.Tests.ReleasePlayerBuilder.BuildMacPlayer` 在独立副本构建普通 Mac 玩家版本。通过 `FINAL_DEFENSE_MAC_BUILD_PATH` 指定 `Builds/Mac/FinalDefense.app`，测试程序集和 PCM 探针不进入成品。按 `VERIFY_RELEASE.md` 的单键保险方式进行正常成品手动验收。

本轮 XML、PCM 记录、输入哈希和 Mac 构建摘要归档到 `Docs/Audio/Verification`。保留 `Docs/ReleaseVerification` 的历史战斗验收记录。
